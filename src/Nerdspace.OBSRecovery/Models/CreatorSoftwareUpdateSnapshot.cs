namespace Nerdspace.OBSRecovery.Models;

public sealed record CreatorSoftwareUpdateSnapshot(
    string Id,
    string Name,
    bool Detected,
    string InstalledVersion,
    string LatestVersion,
    string Status,
    string Detail,
    string ExecutablePath,
    string UpdateUrl,
    string LatestSource)
{
    public bool UpdateAvailable => Status.Equals("Update available", StringComparison.OrdinalIgnoreCase);

    public string Display => Detected
        ? $"Installed: {InstalledVersion}\nLatest stable: {LatestVersion}\nStatus: {Status}\n{Detail}"
        : $"Nothing found — {Name} was not detected.\nStatus: {Status}\n{Detail}";
}
