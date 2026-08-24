namespace Nerdspace.OBSRecovery.Models;

public enum ObsHealthState
{
    Offline,
    Healthy,
    TemporarilyUnresponsive,
    Hung,
    StuckShutdown,
    MultipleInstances,
    LimitedMonitoring,
    Unknown
}
