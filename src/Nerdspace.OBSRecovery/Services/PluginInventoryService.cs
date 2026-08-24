using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class PluginInventoryService
{
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;
    private readonly UpdateService _updates;
    private readonly UpdateDeferralService _deferrals;
    private readonly PluginRegistryService _registry;

    // OBS modules that ship with the Windows application. These are filtered only
    // from OBS's legacy mixed plugin directory. ProgramData remains user-installed.
    private static readonly HashSet<string> BundledWindowsModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "aja", "aja-output-ui", "coreaudio-encoder", "decklink", "decklink-captions", "decklink-output-ui",
        "frontend-tools", "image-source", "nv-filters", "obs-browser", "obs-ffmpeg", "obs-filters",
        "obs-libfdk", "obs-nvenc", "obs-outputs", "obs-qsv11", "obs-text", "obs-transitions", "obs-vst",
        "obs-webrtc", "obs-websocket", "obs-x264", "rtmp-services", "text-freetype2", "vlc-video",
        "win-capture", "win-dshow", "win-wasapi"
    };

    public PluginInventoryService(IObsPlatformService platform, LoggingService logger, UpdateService updates, UpdateDeferralService deferrals, PluginRegistryService registry)
    {
        _platform = platform;
        _logger = logger;
        _updates = updates;
        _deferrals = deferrals;
        _registry = registry;
    }

    public async Task<IReadOnlyList<PluginInfo>> ScanAsync(bool checkUpdates, CancellationToken cancellationToken = default)
    {
        var plugins = new List<PluginInfo>();
        var bundledRoot = GetBundledPluginRoot();
        foreach (var root in _platform.GetPluginDirectories().Where(Directory.Exists))
        {
            try { ScanWindows(root, plugins, PathsEqual(root, bundledRoot)); }
            catch (Exception ex) { _logger.Warn($"Plugin directory scan failed for {root}: {ex.Message}"); }
        }

        var deduped = plugins
            .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(ApplyRegistryFallback)
            .OrderBy(p => p.Name)
            .Select(p => p with { CompatibilityStatus = GetCompatibility(p.Name) })
            .ToList();
        if (!checkUpdates) return deduped;

        var releaseCache = new Dictionary<string, GitHubReleaseInfo?>(StringComparer.OrdinalIgnoreCase);
        var enriched = new List<PluginInfo>();
        foreach (var plugin in deduped)
        {
            var catalog = _registry.Match($"{plugin.Name} {plugin.Id}", plugin.Path);
            var repository = plugin.Repository ?? catalog?.Repository;
            var sourceUrl = plugin.SourceUrl ?? catalog?.SourceUrl;
            var obsResourceUrl = plugin.ObsResourceUrl ?? catalog?.ObsResourceUrl;
            var sourceConfidence = plugin.SourceConfidence;

            if (plugin.HasManifest && catalog is not null && !string.IsNullOrWhiteSpace(repository))
                sourceConfidence = "OBS manifest + catalog match";
            else if (!plugin.HasManifest && catalog is not null)
                sourceConfidence = catalog.HasVerifiedSource ? "Official OBS catalog" : "OBS catalog metadata";

            if (!string.IsNullOrWhiteSpace(repository))
            {
                if (!releaseCache.TryGetValue(repository, out var release))
                {
                    release = await _updates.GetLatestGitHubReleaseAsync(repository, cancellationToken);
                    releaseCache[repository] = release;
                }

                if (release is null)
                {
                    enriched.Add(plugin with
                    {
                        Repository = repository,
                        SourceUrl = sourceUrl,
                        ObsResourceUrl = obsResourceUrl,
                        LatestVersion = "Unavailable",
                        ReleaseUrl = sourceUrl,
                        UpdateStatus = "Latest version unavailable",
                        SourceConfidence = sourceConfidence
                    });
                    continue;
                }

                var baseStatus = UpdateService.Compare(plugin.Version, release.Version);
                var finalStatus = _deferrals.Apply(DeferralKey(plugin.Id), release.Version, baseStatus);
                enriched.Add(plugin with
                {
                    Repository = repository,
                    SourceUrl = sourceUrl,
                    ObsResourceUrl = obsResourceUrl,
                    LatestVersion = release.Version,
                    ReleaseUrl = release.ReleaseUrl,
                    UpdateStatus = finalStatus,
                    SourceConfidence = sourceConfidence
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                enriched.Add(plugin with
                {
                    SourceUrl = sourceUrl,
                    ObsResourceUrl = obsResourceUrl,
                    LatestVersion = catalog?.ResourceVersion,
                    ReleaseUrl = sourceUrl,
                    UpdateStatus = plugin.HasManifest ? "Manifest source • manual update check" : "Official source • manual update check",
                    SourceConfidence = sourceConfidence
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(plugin.WebsiteUrl))
            {
                enriched.Add(plugin with
                {
                    ReleaseUrl = plugin.WebsiteUrl,
                    LatestVersion = catalog?.ResourceVersion,
                    UpdateStatus = "Plugin website available • manual update check",
                    SourceConfidence = plugin.HasManifest ? "OBS manifest" : plugin.SourceConfidence
                });
                continue;
            }

            if (catalog is not null)
            {
                enriched.Add(plugin with
                {
                    ObsResourceUrl = catalog.ObsResourceUrl,
                    LatestVersion = catalog.ResourceVersion,
                    UpdateStatus = "Official OBS page available • release source unavailable",
                    SourceConfidence = "OBS catalog metadata"
                });
                continue;
            }

            enriched.Add(plugin with
            {
                LatestVersion = null,
                ReleaseUrl = null,
                UpdateStatus = "Update source not found",
                SourceConfidence = plugin.HasManifest ? "OBS manifest • no source URL" : "Unverified"
            });
        }
        return enriched;
    }

    public static string DeferralKey(string pluginId) => $"plugin:{pluginId}";

    private static void ScanWindows(string root, ICollection<PluginInfo> result, bool filterBundledModules)
    {
        var programDataStyle = Directory.GetDirectories(root).Any(d => Directory.Exists(Path.Combine(d, "bin")));
        if (programDataStyle)
        {
            // ProgramData packages are complete plugin bundles. Search the bundle for
            // the same manifest.json OBS reads from the module data path.
            foreach (var dir in Directory.GetDirectories(root))
            {
                var dll = Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories).FirstOrDefault();
                if (dll is null) continue;
                var moduleName = Path.GetFileNameWithoutExtension(dll);
                result.Add(BuildPlugin(Path.GetFileName(dir), moduleName, dll, dir, root, CanMove(dir)));
            }
        }
        else
        {
            foreach (var dll in Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var moduleName = Path.GetFileNameWithoutExtension(dll);
                if (filterBundledModules && IsBundledObsModule(moduleName))
                    continue;

                result.Add(BuildPlugin(moduleName, moduleName, dll, dll, root, false));
            }
        }
    }

    private static PluginInfo BuildPlugin(string fallbackName, string moduleName, string dllPath, string movablePath, string location, bool canQuarantine)
    {
        var version = "Unknown";
        string? productName = null;
        string? publisher = null;
        string? dllDescription = null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dllPath);
            version = FirstValue(info.ProductVersion?.Split(' ')[0], info.FileVersion) ?? "Unknown";
            productName = FirstValue(info.ProductName, info.FileDescription);
            publisher = info.CompanyName;
            dllDescription = info.FileDescription;
        }
        catch { }

        var manifestPath = FindManifestPath(moduleName, dllPath, movablePath, location);
        var manifest = manifestPath is null ? null : ReadManifest(manifestPath);

        var manifestRepositoryUrl = manifest?.RepositoryUrl;
        var githubRepository = NormalizeGitHubRepository(manifestRepositoryUrl);
        var displayName = FirstValue(manifest?.DisplayName, manifest?.Name, productName, PrettyName(fallbackName)) ?? PrettyName(fallbackName);
        var id = FirstValue(manifest?.Id, NormalizeId(moduleName), NormalizeId(fallbackName)) ?? NormalizeId(fallbackName);
        var installedVersion = FirstValue(manifest?.Version, version) ?? "Unknown";
        var description = FirstValue(manifest?.Description, dllDescription);

        return new PluginInfo(
            id,
            displayName,
            installedVersion,
            movablePath,
            location,
            canQuarantine,
            githubRepository,
            null,
            null,
            null,
            "Not checked",
            "Not verified",
            manifestRepositoryUrl,
            manifest?.WebsiteUrl,
            manifest?.SupportUrl,
            manifestPath,
            manifest is null ? "DLL metadata" : "OBS manifest",
            manifest is null ? "Unverified" : !string.IsNullOrWhiteSpace(manifestRepositoryUrl) ? "OBS manifest" : "OBS manifest • no source URL",
            description,
            publisher);
    }

    private PluginInfo ApplyRegistryFallback(PluginInfo plugin)
    {
        var catalog = _registry.Match($"{plugin.Name} {plugin.Id}", plugin.Path);
        if (catalog is null) return plugin;

        // Manifest metadata is primary. The bundled catalog fills only missing fields
        // and remains useful for older plugins that do not ship OBS metadata yet.
        return plugin with
        {
            Id = plugin.HasManifest && !string.IsNullOrWhiteSpace(plugin.Id) ? plugin.Id : catalog.Id,
            Name = plugin.HasManifest && !string.IsNullOrWhiteSpace(plugin.Name) ? plugin.Name : catalog.DisplayName,
            Repository = plugin.Repository ?? catalog.Repository,
            SourceUrl = plugin.SourceUrl ?? catalog.SourceUrl,
            ObsResourceUrl = plugin.ObsResourceUrl ?? catalog.ObsResourceUrl,
            SourceConfidence = plugin.HasManifest
                ? catalog.HasVerifiedSource ? "OBS manifest + catalog match" : plugin.SourceConfidence
                : catalog.HasVerifiedSource ? "Official OBS catalog" : "OBS catalog metadata"
        };
    }

    private static string? FindManifestPath(string moduleName, string dllPath, string movablePath, string pluginRoot)
    {
        var candidates = new List<string>();

        if (Directory.Exists(movablePath))
        {
            candidates.Add(Path.Combine(movablePath, "manifest.json"));
            candidates.Add(Path.Combine(movablePath, "data", "manifest.json"));
            candidates.Add(Path.Combine(movablePath, "data", "obs-plugins", moduleName, "manifest.json"));
            try { candidates.AddRange(Directory.EnumerateFiles(movablePath, "manifest.json", SearchOption.AllDirectories)); }
            catch { }
        }

        var dllDir = Path.GetDirectoryName(dllPath);
        if (!string.IsNullOrWhiteSpace(dllDir))
        {
            candidates.Add(Path.Combine(dllDir, "manifest.json"));
            candidates.Add(Path.Combine(dllDir, "..", "..", "data", "obs-plugins", moduleName, "manifest.json"));
        }

        // Legacy OBS install layout: <obs>\obs-plugins\64bit\module.dll and
        // <obs>\data\obs-plugins\module\manifest.json.
        try
        {
            var rootName = new DirectoryInfo(pluginRoot).Name;
            var rootParent = Directory.GetParent(pluginRoot);
            if (rootName.Equals("64bit", StringComparison.OrdinalIgnoreCase) &&
                rootParent?.Name.Equals("obs-plugins", StringComparison.OrdinalIgnoreCase) == true)
            {
                var obsRoot = rootParent.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(obsRoot))
                    candidates.Add(Path.Combine(obsRoot, "data", "obs-plugins", moduleName, "manifest.json"));
            }
        }
        catch { }

        var existing = candidates
            .Select(SafeFullPath)
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        if (existing.Count <= 1) return existing.FirstOrDefault();

        // Prefer a manifest whose id/name matches the DLL module name.
        foreach (var path in existing)
        {
            var metadata = ReadManifest(path);
            var identity = $"{metadata?.Id} {metadata?.Name} {metadata?.DisplayName}";
            if (NormalizeId(identity).Contains(NormalizeId(moduleName), StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return existing[0];
    }

    private static PluginManifestMetadata? ReadManifest(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;
            var urls = root.TryGetProperty("urls", out var urlElement) && urlElement.ValueKind == JsonValueKind.Object ? urlElement : default;
            return new PluginManifestMetadata(
                ReadString(root, "id"),
                ReadString(root, "display_name"),
                ReadString(root, "name"),
                ReadString(root, "version"),
                ReadString(root, "description"),
                urls.ValueKind == JsonValueKind.Object ? ReadString(urls, "repository") : null,
                urls.ValueKind == JsonValueKind.Object ? ReadString(urls, "website") : null,
                urls.ValueKind == JsonValueKind.Object ? ReadString(urls, "support") : null);
        }
        catch { return null; }
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? FirstValue(value.GetString())
            : null;

    private static string? NormalizeGitHubRepository(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var input = value.Trim();

        if (input.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            input = "https://github.com/" + input["git@github.com:".Length..];

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        return $"{parts[0]}/{parts[1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase)}";
    }

    private string GetCompatibility(string pluginName)
    {
        try
        {
            var logDir = _platform.GetObsLogDirectory();
            if (!Directory.Exists(logDir)) return "Not verified";
            var latest = Directory.EnumerateFiles(logDir, "*.txt").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (latest is null) return "Not verified";
            var tokens = pluginName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 4).ToArray();
            var seen = false;
            foreach (var line in File.ReadLines(latest))
            {
                if (!tokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                seen = true;
                if (line.Contains("failed", StringComparison.OrdinalIgnoreCase) || line.Contains("error", StringComparison.OrdinalIgnoreCase))
                    return "Load issue detected";
            }
            return seen ? "Seen in latest log" : "Not verified";
        }
        catch { return "Not verified"; }
    }

    private string? GetBundledPluginRoot()
    {
        try
        {
            var install = _platform.FindObsInstall();
            if (string.IsNullOrWhiteSpace(install)) return null;
            var bin64 = Path.GetDirectoryName(install);
            var bin = bin64 is null ? null : Directory.GetParent(bin64)?.FullName;
            var obsRoot = bin is null ? null : Directory.GetParent(bin)?.FullName;
            return obsRoot is null ? null : Path.Combine(obsRoot, "obs-plugins", "64bit");
        }
        catch { return null; }
    }

    private static bool PathsEqual(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right)) return false;
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool IsBundledObsModule(string moduleName) => BundledWindowsModules.Contains(moduleName);

    private static bool CanMove(string path)
    {
        try
        {
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return false;
            var probe = Path.Combine(parent, $".ground-control-write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    private static string? FirstValue(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }

    private static string NormalizeId(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    private static string PrettyName(string value) => Regex.Replace(value.Replace('_', ' ').Replace('-', ' '), @"\s+", " ").Trim();

    private sealed record PluginManifestMetadata(
        string? Id,
        string? DisplayName,
        string? Name,
        string? Version,
        string? Description,
        string? RepositoryUrl,
        string? WebsiteUrl,
        string? SupportUrl);
}
