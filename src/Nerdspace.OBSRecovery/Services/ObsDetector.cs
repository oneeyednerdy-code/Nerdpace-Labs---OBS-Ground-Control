using System.Diagnostics;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class ObsDetector
{
    private readonly AppSettings _settings;
    private readonly LoggingService _logger;
    private readonly IObsPlatformService _platform;
    private readonly Dictionary<int, Memory> _memory = new();

    public ObsDetector(AppSettings settings, LoggingService logger, IObsPlatformService platform)
    {
        _settings = settings;
        _logger = logger;
        _platform = platform;
    }

    public ObsSnapshot Inspect()
    {
        var now = DateTimeOffset.Now;
        var processes = _platform.GetObsProcesses().ToList();
        var active = processes.Select(p => p.Id).ToHashSet();

        foreach (var pid in _memory.Keys.Where(id => !active.Contains(id)).ToList())
            _memory.Remove(pid);

        if (processes.Count == 0)
            return Snapshot(ObsHealthState.Offline, null, "OBS is not running.", true, false, false, false, now);

        if (!_platform.SupportsWindowDetection)
        {
            var first = processes[0];
            var state = processes.Count > 1 ? ObsHealthState.MultipleInstances : ObsHealthState.LimitedMonitoring;
            var message = processes.Count > 1
                ? $"{processes.Count} OBS processes are running. Automatic process termination is disabled."
                : "OBS is running. This platform uses conservative process-only monitoring.";
            var pid = first.Id;
            foreach (var p in processes) p.Dispose();
            return Snapshot(state, pid, message, false, true, true, true, now);
        }

        var details = new List<Detail>();
        foreach (var process in processes)
        {
            try
            {
                var hasWindow = _platform.HasUsableWindow(process);
                var responding = _platform.IsResponding(process);

                if (!_memory.TryGetValue(process.Id, out var memory))
                {
                    memory = new Memory();
                    _memory[process.Id] = memory;
                }

                if (hasWindow)
                {
                    memory.EverHadWindow = true;
                    memory.WindowMissingSince = null;
                }
                else if (memory.EverHadWindow)
                {
                    memory.WindowMissingSince ??= now;
                }

                if (hasWindow && !responding)
                    memory.UnresponsiveSince ??= now;
                else
                    memory.UnresponsiveSince = null;

                details.Add(new Detail(process.Id, hasWindow, responding, memory));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not inspect OBS PID {process.Id}: {ex.Message}");
            }
            finally { process.Dispose(); }
        }

        if (details.Count == 0)
            return Snapshot(ObsHealthState.Unknown, null, "OBS is running, but its state could not be inspected.", false, false, false, true, now);

        var healthy = details.FirstOrDefault(d => d.HasWindow && d.Responding);
        if (healthy is not null)
        {
            var state = details.Count > 1 ? ObsHealthState.MultipleInstances : ObsHealthState.Healthy;
            var msg = details.Count > 1
                ? $"OBS is healthy, but {details.Count} OBS processes are running. Extras will not be auto-killed."
                : "OBS is running normally.";
            return Snapshot(state, healthy.Id, msg, false, true, true, true, now);
        }

        var hung = details.FirstOrDefault(d => d.Memory.UnresponsiveSince.HasValue &&
            (now - d.Memory.UnresponsiveSince.Value).TotalSeconds >= _settings.HungThresholdSeconds);
        if (hung is not null)
            return Snapshot(ObsHealthState.Hung, hung.Id, $"OBS has been unresponsive for at least {_settings.HungThresholdSeconds} seconds.", false, false, true, true, now);

        var stuck = details.FirstOrDefault(d => d.Memory.WindowMissingSince.HasValue &&
            (now - d.Memory.WindowMissingSince.Value).TotalSeconds >= _settings.StuckShutdownThresholdSeconds);
        if (stuck is not null)
            return Snapshot(ObsHealthState.StuckShutdown, stuck.Id, "The OBS window closed, but its process is still running.", false, false, true, true, now);

        var temporary = details.FirstOrDefault(d => d.HasWindow && !d.Responding);
        if (temporary is not null)
            return Snapshot(ObsHealthState.TemporarilyUnresponsive, temporary.Id, "OBS is briefly not responding. Recovery is waiting before taking action.", false, false, true, true, now);

        return Snapshot(ObsHealthState.Unknown, details[0].Id, "OBS is running in an ambiguous background state. Automatic recovery will not kill it.", false, false, true, true, now);
    }

    private ObsSnapshot Snapshot(ObsHealthState state, int? pid, string message, bool launch, bool show, bool restart, bool force, DateTimeOffset now)
        => new(state, pid, message, launch, show, restart, force, now, _platform.MonitoringCapability);

    private sealed class Memory
    {
        public bool EverHadWindow { get; set; }
        public DateTimeOffset? UnresponsiveSince { get; set; }
        public DateTimeOffset? WindowMissingSince { get; set; }
    }

    private sealed record Detail(int Id, bool HasWindow, bool Responding, Memory Memory);
}
