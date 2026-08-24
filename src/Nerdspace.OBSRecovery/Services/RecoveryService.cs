using System.Diagnostics;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class RecoveryService
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly LoggingService _logger;
    private readonly IObsPlatformService _platform;
    private readonly ObsDetector _detector;
    private readonly Queue<DateTimeOffset> _automaticRecoveries = new();

    public RecoveryService(AppSettings settings, SettingsService settingsService, LoggingService logger, IObsPlatformService platform, ObsDetector detector)
    {
        _settings = settings;
        _settingsService = settingsService;
        _logger = logger;
        _platform = platform;
        _detector = detector;
    }

    public async Task HandleAutomaticRecoveryAsync(ObsSnapshot snapshot)
    {
        if (!_settings.RecoveryProtection || !_platform.SupportsAutomaticHangDetection) return;

        if (snapshot.State == ObsHealthState.Hung && snapshot.ProcessId.HasValue)
        {
            if (RecoveryLoopGuardTriggered())
            {
                _settings.RecoveryProtection = false;
                await _settingsService.SaveAsync(_settings);
                _logger.Warn("Recovery Protection paused after repeated automatic recoveries. Try Safe Mode and review OBS logs.");
                return;
            }

            _automaticRecoveries.Enqueue(DateTimeOffset.Now);
            _logger.Warn($"Automatic recovery triggered for hung OBS PID {snapshot.ProcessId.Value}.");
            await ForceStopPidAsync(snapshot.ProcessId.Value);

            if (_settings.RelaunchAfterHungRecovery)
            {
                await Task.Delay(1000);
                await LaunchAsync();
            }
        }
        else if (snapshot.State == ObsHealthState.StuckShutdown && snapshot.ProcessId.HasValue)
        {
            _logger.Warn($"Cleaning stuck OBS shutdown PID {snapshot.ProcessId.Value}. OBS will remain closed.");
            await ForceStopPidAsync(snapshot.ProcessId.Value);
        }
    }

    public async Task LaunchAsync(bool safeMode = false)
    {
        await _platform.LaunchAsync(_settings.ObsPath, safeMode);
        _logger.Info(safeMode ? "OBS launched in Safe Mode." : "OBS launched.");
    }

    public Task ShowAsync(int? processId = null) => _platform.ShowAsync(processId);

    public async Task RestartAsync(bool safeMode = false)
    {
        var processes = _platform.GetObsProcesses().ToList();
        foreach (var process in processes)
        {
            using (process)
            {
                await _platform.GracefulStopAsync(process);
                if (!process.HasExited) await _platform.ForceStopAsync(process);
            }
        }
        await Task.Delay(750);
        await LaunchAsync(safeMode);
    }

    public async Task ForceCloseAllAsync()
    {
        var processes = _platform.GetObsProcesses().ToList();
        foreach (var process in processes)
        {
            using (process) await _platform.ForceStopAsync(process);
        }
        _logger.Warn("Force-close requested for all OBS processes.");
    }

    public async Task<string> GenerateDiagnosticReportAsync()
    {
        var snapshot = _detector.Inspect();
        var path = Path.Combine(_logger.LogDirectory, $"diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines = new[]
        {
            "NERDSPACE LABS OBS RECOVERY DIAGNOSTIC",
            $"Generated: {DateTimeOffset.Now:O}",
            $"App version: {AppVersion.DisplayVersion}",
            $"Platform: {_platform.PlatformName}",
            $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
            $"Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}",
            $"Monitoring: {_platform.MonitoringCapability}",
            $"OBS path configured: {!string.IsNullOrWhiteSpace(_settings.ObsPath)}",
            $"Recovery protection: {_settings.RecoveryProtection}",
            $"Current OBS state: {snapshot.State}",
            $"Current PID: {(snapshot.ProcessId?.ToString() ?? "none")}",
            $"Status: {snapshot.Message}",
            "",
            "Privacy: This report does not collect stream keys, OAuth tokens, chat messages, scene contents, or browser-source URLs."
        };
        await File.WriteAllLinesAsync(path, lines);
        _logger.Success($"Diagnostic report created: {path}");
        return path;
    }

    private async Task ForceStopPidAsync(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            await _platform.ForceStopAsync(process);
            _logger.Success($"Terminated OBS PID {pid}.");
        }
        catch (ArgumentException)
        {
            _logger.Info($"OBS PID {pid} exited before recovery was needed.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Unable to terminate OBS PID {pid}: {ex.Message}");
            throw;
        }
    }

    private bool RecoveryLoopGuardTriggered()
    {
        var cutoff = DateTimeOffset.Now.AddMinutes(-10);
        while (_automaticRecoveries.Count > 0 && _automaticRecoveries.Peek() < cutoff)
            _automaticRecoveries.Dequeue();
        return _automaticRecoveries.Count >= 2;
    }
}
