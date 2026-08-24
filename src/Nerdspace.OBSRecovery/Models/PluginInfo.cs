namespace Nerdspace.OBSRecovery.Models;

public sealed record PluginInfo(
    string Id,
    string Name,
    string Version,
    string Path,
    string Location,
    bool CanQuarantine,
    string? Repository,
    string? ObsResourceUrl,
    string? LatestVersion,
    string? ReleaseUrl,
    string UpdateStatus,
    string CompatibilityStatus,
    string? SourceUrl = null,
    string? WebsiteUrl = null,
    string? SupportUrl = null,
    string? ManifestPath = null,
    string MetadataSource = "DLL metadata",
    string SourceConfidence = "Unverified",
    string? Description = null,
    string? Publisher = null)
{
    public bool HasVerifiedRelease => !string.IsNullOrWhiteSpace(ReleaseUrl);
    public bool HasOfficialObsResource => !string.IsNullOrWhiteSpace(ObsResourceUrl);
    public bool HasKnownInstalledVersion => !string.IsNullOrWhiteSpace(Version) && !Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    public bool HasKnownLatestVersion => !string.IsNullOrWhiteSpace(LatestVersion) && LatestVersion is not "Unknown" and not "Unavailable" and not "Not checked";
    public bool IsUpdateAvailable => UpdateStatus.StartsWith("Update available", StringComparison.OrdinalIgnoreCase);
    public bool IsDeferred => UpdateStatus.StartsWith("Deferred until", StringComparison.OrdinalIgnoreCase) || UpdateStatus.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase);
    public bool HasManifest => !string.IsNullOrWhiteSpace(ManifestPath);
    public bool HasSourcePage => HasVerifiedRelease || !string.IsNullOrWhiteSpace(SourceUrl) || !string.IsNullOrWhiteSpace(Repository) || !string.IsNullOrWhiteSpace(WebsiteUrl) || HasOfficialObsResource;

    public string Display
    {
        get
        {
            var latest = LatestVersion ?? "Not checked";
            return $"{Name} • Installed {Version} → Latest {latest} • {UpdateStatus} • {MetadataSource} • {CompatibilityStatus}";
        }
    }
}
