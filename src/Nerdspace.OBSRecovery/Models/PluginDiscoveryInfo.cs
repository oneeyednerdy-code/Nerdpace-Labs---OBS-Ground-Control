namespace Nerdspace.OBSRecovery.Models;

public sealed record PluginDiscoveryInfo(
    PluginCatalogEntry Entry,
    bool Installed,
    string InstalledVersion,
    string LatestVersion,
    string? LatestReleaseUrl)
{
    public string Display =>
        $"{Entry.DisplayName} • {Entry.Author} • {(Installed ? $"Installed {InstalledVersion}" : "Not installed")} • Latest {LatestVersion}";
}
