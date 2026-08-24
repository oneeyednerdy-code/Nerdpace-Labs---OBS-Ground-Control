namespace Nerdspace.OBSRecovery.Models;

public sealed record ObsSnapshot(
    ObsHealthState State,
    int? ProcessId,
    string Message,
    bool CanLaunch,
    bool CanShow,
    bool CanRestart,
    bool CanForceClose,
    DateTimeOffset Timestamp,
    string CapabilityNote);
