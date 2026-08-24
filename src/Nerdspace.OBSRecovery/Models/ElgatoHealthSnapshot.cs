namespace Nerdspace.OBSRecovery.Models;

public sealed record ElgatoSoftwareInfo(string Name, string Version, string? InstallLocation = null)
{
    public string Display => string.IsNullOrWhiteSpace(Version) ? Name : $"{Name} • {Version}";
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
    public string SoftwareSummary => Software.Count == 0 ? "No Elgato software detected" : string.Join(" | ", Software.Select(x => x.Display));
    public string HardwareSummary => Hardware.Count == 0 ? "No Elgato hardware detected" : string.Join(" | ", Hardware.Select(x => x.Display));
    public string Summary => $"Software: {SoftwareSummary}\nHardware: {HardwareSummary}";
}
