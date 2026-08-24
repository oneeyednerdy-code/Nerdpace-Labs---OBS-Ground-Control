namespace Nerdspace.OBSRecovery.Models;

public sealed record PluginInfo(
    string Id,
    string Name,
    string Version,
    string Path,
    string Location,
    bool CanQuarantine,
    string? Repository,
    string? LatestVersion,
    string? ReleaseUrl,
    string UpdateStatus,
    string CompatibilityStatus)
{
    public bool HasVerifiedRelease => !string.IsNullOrWhiteSpace(Repository) && !string.IsNullOrWhiteSpace(ReleaseUrl);
    public bool HasKnownInstalledVersion => !string.IsNullOrWhiteSpace(Version) && !Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    public bool HasKnownLatestVersion => !string.IsNullOrWhiteSpace(LatestVersion) && LatestVersion is not "Unknown" and not "Unavailable";
    public bool IsUpdateAvailable => UpdateStatus.StartsWith("Update available", StringComparison.OrdinalIgnoreCase);
    public bool IsDeferred => UpdateStatus.StartsWith("Deferred until", StringComparison.OrdinalIgnoreCase) || UpdateStatus.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase);

    public string Display
    {
        get
        {
            var latest = LatestVersion ?? "Not checked";
            return $"{Name} • Installed {Version} → Latest {latest} • {UpdateStatus} • {CompatibilityStatus}";
        }
    }
}
