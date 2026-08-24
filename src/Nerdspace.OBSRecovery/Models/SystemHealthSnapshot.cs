namespace Nerdspace.OBSRecovery.Models;

public sealed record SystemHealthSnapshot(
    int ObsProcessCount,
    double ObsMemoryMb,
    double? ObsCpuPercent,
    string RecordingPath,
    double? RecordingFreeGb,
    string ObsVersion,
    string LatestObsVersion,
    string UpdateStatus,
    DateTimeOffset Timestamp);
