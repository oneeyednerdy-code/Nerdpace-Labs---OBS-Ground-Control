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

    private static readonly PluginCatalogEntry[] Catalog =
    {
        new("aitum-multistream", "Aitum Multistream", "Aitum/obs-aitum-multistream", new[] { "aitum-multistream", "multistream" }),
        new("aitum-vertical", "Aitum Vertical", "Aitum/obs-vertical-canvas", new[] { "vertical-canvas", "aitum-vertical", "vertical" }),
        new("source-record", "Source Record", "exeldro/obs-source-record", new[] { "source-record", "sourcerecord" })
    };

    public PluginInventoryService(IObsPlatformService platform, LoggingService logger, UpdateService updates, UpdateDeferralService deferrals)
    {
        _platform = platform;
        _logger = logger;
        _updates = updates;
        _deferrals = deferrals;
    }

    public async Task<IReadOnlyList<PluginInfo>> ScanAsync(bool checkUpdates, CancellationToken cancellationToken = default)
    {
        var plugins = new List<PluginInfo>();
        foreach (var root in _platform.GetPluginDirectories().Where(Directory.Exists))
        {
            try { ScanWindows(root, plugins); }
            catch (Exception ex) { _logger.Warn($"Plugin directory scan failed for {root}: {ex.Message}"); }
        }

        var deduped = plugins
            .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name)
            .Select(p => p with { CompatibilityStatus = GetCompatibility(p.Name) })
            .ToList();
        if (!checkUpdates) return deduped;

        var releaseCache = new Dictionary<string, GitHubReleaseInfo?>(StringComparer.OrdinalIgnoreCase);
        var enriched = new List<PluginInfo>();
        foreach (var plugin in deduped)
        {
            var catalog = MatchCatalog(plugin.Name, plugin.Path);
            if (catalog is null)
            {
                enriched.Add(plugin with { UpdateStatus = "Update source not verified", Repository = null, LatestVersion = null, ReleaseUrl = null });
                continue;
            }

            if (!releaseCache.TryGetValue(catalog.Repository, out var release))
            {
                release = await _updates.GetLatestGitHubReleaseAsync(catalog.Repository, cancellationToken);
                releaseCache[catalog.Repository] = release;
            }

            if (release is null)
            {
                enriched.Add(plugin with
                {
                    Id = catalog.Id, Name = catalog.DisplayName, Repository = catalog.Repository,
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
                LatestVersion = release.Version,
                ReleaseUrl = release.ReleaseUrl,
                UpdateStatus = finalStatus,
                CompatibilityStatus = plugin.CompatibilityStatus
            });
        }
        return enriched;
    }

    public static string DeferralKey(string pluginId) => $"plugin:{pluginId}";

    private static void ScanWindows(string root, ICollection<PluginInfo> result)
    {
        var programDataStyle = Directory.GetDirectories(root).Any(d => Directory.Exists(Path.Combine(d, "bin")));
        if (programDataStyle)
        {
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
                result.Add(BuildPlugin(Path.GetFileNameWithoutExtension(dll), dll, dll, root, false));
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
        return new PluginInfo(NormalizeId(name), PrettyName(name), version, movablePath, location, canQuarantine, null, null, null, "Not checked", "Not verified");
    }

    private static PluginCatalogEntry? MatchCatalog(string name, string path)
    {
        var haystack = $"{name} {path}".ToLowerInvariant();
        return Catalog.FirstOrDefault(c => c.MatchTokens.Any(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private string GetCompatibility(string pluginName)
    {
        try
        {
            var logDir = _platform.GetObsLogDirectory();
            if (!Directory.Exists(logDir)) return "Not verified";
            var latest = Directory.EnumerateFiles(logDir, "*.txt").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (latest is null) return "Not verified";
            var tokens = pluginName.Split(' ', '-', '_', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 4).ToArray();
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
