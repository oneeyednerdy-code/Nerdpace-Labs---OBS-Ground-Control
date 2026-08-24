using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class PluginDiscoveryService
{
    public const string OfficialObsPluginDirectoryUrl = "https://obsproject.com/forum/plugins/";

    private readonly PluginRegistryService _registry;
    private readonly UpdateService _updates;
    private readonly LoggingService _logger;
    private readonly Dictionary<string, (DateTimeOffset CheckedAt, GitHubReleaseInfo? Release)> _releaseCache = new(StringComparer.OrdinalIgnoreCase);

    public PluginDiscoveryService(PluginRegistryService registry, UpdateService updates, LoggingService logger)
    {
        _registry = registry;
        _updates = updates;
        _logger = logger;
    }

    public string CatalogSummary
    {
        get
        {
            var generated = _registry.GeneratedAtUtc?.ToLocalTime().ToString("MMM d, yyyy") ?? "unknown date";
            return $"{_registry.ResourceCount} OBS resource(s) preloaded • {_registry.SourceVerifiedCount} source link(s) verified by OBS resource pages • catalog {generated}";
        }
    }

    public async Task<IReadOnlyList<PluginDiscoveryInfo>> SearchAsync(
        string? query,
        IReadOnlyList<PluginInfo> installedPlugins,
        bool checkLatestVersions,
        CancellationToken cancellationToken = default)
    {
        var entries = _registry.Search(query);
        var results = new List<PluginDiscoveryInfo>();
        var allowBulkLiveRefresh = entries.Count <= 25;

        foreach (var entry in entries)
        {
            var installed = installedPlugins.FirstOrDefault(plugin => _registry.MatchesInstalledPlugin(entry, plugin));
            GitHubReleaseInfo? release = null;

            if (checkLatestVersions && allowBulkLiveRefresh && entry.HasGitHubRepository)
                release = await GetReleaseAsync(entry.Repository!, cancellationToken);

            var catalogVersion = string.IsNullOrWhiteSpace(entry.ResourceVersion) ? "Not listed" : entry.ResourceVersion;
            var latest = checkLatestVersions
                ? !allowBulkLiveRefresh ? $"OBS catalog {catalogVersion} • narrow search to live-refresh"
                    : entry.HasGitHubRepository ? release?.Version ?? $"OBS catalog {catalogVersion}"
                    : $"OBS catalog {catalogVersion} • no GitHub release source"
                : $"OBS catalog {catalogVersion}";

            results.Add(new PluginDiscoveryInfo(
                entry,
                installed is not null,
                installed?.Version ?? "—",
                latest,
                release?.ReleaseUrl));
        }

        return results;
    }

    private async Task<GitHubReleaseInfo?> GetReleaseAsync(string repository, CancellationToken cancellationToken)
    {
        if (_releaseCache.TryGetValue(repository, out var cached) && DateTimeOffset.UtcNow - cached.CheckedAt < TimeSpan.FromMinutes(30))
            return cached.Release;

        var release = await _updates.GetLatestGitHubReleaseAsync(repository, cancellationToken);
        _releaseCache[repository] = (DateTimeOffset.UtcNow, release);
        if (release is null)
            _logger.Warn($"Discovery could not retrieve the latest release for {repository}.");
        return release;
    }
}
