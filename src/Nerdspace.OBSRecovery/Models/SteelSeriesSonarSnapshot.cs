namespace Nerdspace.OBSRecovery.Models;

public sealed record SteelSeriesSonarSnapshot(
    bool Supported,
    bool GgInstalled,
    string GgVersion,
    bool SonarDetected,
    bool SonarRunning,
    bool VirtualAudioDetected,
    string Status,
    string Detail)
{
    public bool AnyDetected => GgInstalled || SonarDetected;
    public string Summary
    {
        get
        {
            if (!Supported) return Status;
            if (!AnyDetected) return "Not detected — SteelSeries GG / Sonar not found.";
            var parts = new List<string>();
            if (GgInstalled) parts.Add($"SteelSeries GG {GgVersion}");
            if (SonarDetected) parts.Add("Sonar detected");
            if (SonarRunning) parts.Add("running");
            if (VirtualAudioDetected) parts.Add("virtual audio endpoints present");
            return string.Join(" • ", parts);
        }
    }
}
