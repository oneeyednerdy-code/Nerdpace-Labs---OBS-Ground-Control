namespace Nerdspace.OBSRecovery.Models;

public sealed record ObsOutputSnapshot(
    bool Found,
    string ProfileName,
    int? VideoBitrateKbps,
    int? AudioBitrateKbps,
    int? OutputWidth,
    int? OutputHeight,
    int? Fps,
    string Encoder,
    string Detail)
{
    public string Resolution => OutputWidth.HasValue && OutputHeight.HasValue ? $"{OutputWidth}x{OutputHeight}" : "Unknown resolution";
    public string Display => !Found
        ? Detail
        : $"{ProfileName} • {Resolution}{(Fps.HasValue ? $" @ {Fps} FPS" : string.Empty)} • {(VideoBitrateKbps.HasValue ? $"{VideoBitrateKbps:N0} Kbps" : "bitrate unknown")} • {Encoder}";
}
