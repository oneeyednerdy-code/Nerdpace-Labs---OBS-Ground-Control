namespace Nerdspace.OBSRecovery.Models;

public sealed record ElgatoSoftwareInfo(
    string Name,
    string Version,
    string? InstallLocation = null,
    string LatestVersion = "Not checked",
    string UpdateStatus = "Not checked",
    string? ReleaseNotesUrl = null)
{
    public bool UpdateAvailable => UpdateStatus.Equals("Update available", StringComparison.OrdinalIgnoreCase);

    public string Display =>
        $"{Name}\nInstalled: {Version}\nLatest verified: {LatestVersion}\nStatus: {UpdateStatus}";
}

public sealed record ElgatoHardwareInfo(string Name, string Connection = "USB")
{
    public string Display => string.IsNullOrWhiteSpace(Connection) ? Name : $"{Name} • {Connection}";
}

public sealed record ElgatoHealthSnapshot(
    bool SoftwareCheckSupported,
    bool HardwareCheckSupported,
    IReadOnlyList<ElgatoSoftwareInfo> Software,
    IReadOnlyList<ElgatoHardwareInfo> Hardware,
    bool AttentionRecommended,
    string Status,
    string Detail)
{
    public bool AnyDetected => Software.Count > 0 || Hardware.Count > 0;
    public bool HasSoftwareUpdates => Software.Any(x => x.UpdateAvailable);

    public string SoftwareSummary => Software.Count == 0
        ? "Nothing found — No supported Elgato software is installed."
        : string.Join("\n\n", Software.Select(x => x.Display));

    public string HardwareSummary => Hardware.Count == 0
        ? "Nothing found — No currently connected Elgato hardware was detected."
        : string.Join("\n", Hardware.Select(x => $"• {x.Display}"));

    public string Summary => $"SOFTWARE\n{SoftwareSummary}\n\nCONNECTED HARDWARE\n{HardwareSummary}";
}
