using System.Diagnostics;
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
        "aja",
        "aja-output-ui",
        "coreaudio-encoder",
        "decklink",
        "decklink-captions",
        "decklink-output-ui",
        "frontend-tools",
        "image-source",
        "nv-filters",
        "obs-browser",
        "obs-ffmpeg",
        "obs-filters",
        "obs-libfdk",
        "obs-nvenc",
        "obs-outputs",
        "obs-qsv11",
        "obs-text",
        "obs-transitions",
        "obs-vst",
        "obs-webrtc",
        "obs-websocket",
        "obs-x264",
        "rtmp-services",
        "text-freetype2",
        "vlc-video",
        "win-capture",
        "win-dshow",
        "win-wasapi"
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
            .Select(ApplyRegistryMetadata)
            .OrderBy(p => p.Name)
            .Select(p => p with { CompatibilityStatus = GetCompatibility(p.Name) })
            .ToList();
        if (!checkUpdates) return deduped;

        var releaseCache = new Dictionary<string, GitHubReleaseInfo?>(StringComparer.OrdinalIgnoreCase);
        var enriched = new List<PluginInfo>();
        foreach (var plugin in deduped)
        {
            var catalog = _registry.Match(plugin.Name, plugin.Path);
            if (catalog is null)
            {
                enriched.Add(plugin with { UpdateStatus = "Update source not verified", Repository = null, ObsResourceUrl = null, LatestVersion = null, ReleaseUrl = null });
                continue;
            }

            if (!catalog.HasVerifiedSource)
            {
                enriched.Add(plugin with
                {
                    Id = catalog.Id, Name = catalog.DisplayName, Repository = null, ObsResourceUrl = catalog.ObsResourceUrl,
                    LatestVersion = catalog.ResourceVersion, ReleaseUrl = null, UpdateStatus = "Source URL not published on OBS resource page"
                });
                continue;
            }

            if (!catalog.HasGitHubRepository)
            {
                enriched.Add(plugin with
                {
                    Id = catalog.Id, Name = catalog.DisplayName, Repository = null, ObsResourceUrl = catalog.ObsResourceUrl,
                    LatestVersion = catalog.ResourceVersion, ReleaseUrl = catalog.SourceUrl, UpdateStatus = "Verified source • manual update check"
                });
                continue;
            }

            var repository = catalog.Repository!;
            if (!releaseCache.TryGetValue(repository, out var release))
            {
                release = await _updates.GetLatestGitHubReleaseAsync(repository, cancellationToken);
                releaseCache[repository] = release;
            }

            if (release is null)
            {
                enriched.Add(plugin with
                {
                    Id = catalog.Id, Name = catalog.DisplayName, Repository = catalog.Repository, ObsResourceUrl = catalog.ObsResourceUrl,
                    LatestVersion = "Unavailable", ReleaseUrl = null, UpdateStatus = "Latest version unavailable"
                });
                continue;
            }

            var baseStatus = UpdateService.Compare(plugin.Version, release.Version);
            var finalStatus = _deferrals.Apply(DeferralKey(catalog.Id), release.Version, baseStatus);
            enriched.Add(plugin with
            {
                Id = catalog.Id,
                Name = catalog.DisplayName,
                Repository = catalog.Repository,
                ObsResourceUrl = catalog.ObsResourceUrl,
                LatestVersion = release.Version,
                ReleaseUrl = release.ReleaseUrl,
                UpdateStatus = finalStatus,
                CompatibilityStatus = plugin.CompatibilityStatus
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
            // OBS's recommended ProgramData structure is for externally installed plugins.
            foreach (var dir in Directory.GetDirectories(root))
            {
                var dll = Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories).FirstOrDefault();
                if (dll is null) continue;
                result.Add(BuildPlugin(Path.GetFileName(dir), dll, dir, root, CanMove(dir)));
            }
        }
        else
        {
            foreach (var dll in Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var moduleName = Path.GetFileNameWithoutExtension(dll);
                if (filterBundledModules && IsBundledObsModule(moduleName))
                    continue;

                result.Add(BuildPlugin(moduleName, dll, dll, root, false));
            }
        }
    }

    private static PluginInfo BuildPlugin(string name, string versionSource, string movablePath, string location, bool canQuarantine)
    {
        var version = "Unknown";
        try
        {
            var info = FileVersionInfo.GetVersionInfo(versionSource);
            version = info.ProductVersion?.Split(' ')[0] ?? info.FileVersion ?? "Unknown";
        }
        catch { }
        return new PluginInfo(NormalizeId(name), PrettyName(name), version, movablePath, location, canQuarantine, null, null, null, null, "Not checked", "Not verified");
    }

    private PluginInfo ApplyRegistryMetadata(PluginInfo plugin)
    {
        var catalog = _registry.Match(plugin.Name, plugin.Path);
        if (catalog is null) return plugin;
        return plugin with
        {
            Id = catalog.Id,
            Name = catalog.DisplayName,
            Repository = catalog.Repository,
            ObsResourceUrl = catalog.ObsResourceUrl
        };
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
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool IsBundledObsModule(string moduleName)
        => BundledWindowsModules.Contains(moduleName);

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

    private static string NormalizeId(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    private static string PrettyName(string value) => Regex.Replace(value.Replace('_', ' ').Replace('-', ' '), @"\s+", " ").Trim();
}
