using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SupportReportService
{
    private readonly IObsPlatformService _platform; private readonly AppSettings _settings; private readonly LoggingService _logger;
    private readonly ObsDetector _detector; private readonly PluginInventoryService _plugins; private readonly LogAnalyzerService _logs;
    private readonly SceneAssetScannerService _assets; private readonly CrashHistoryService _crashes; private readonly SystemHealthService _health;
    private readonly GraphicsDriverService _graphics; private readonly ElgatoHealthService _elgato; private readonly SteelSeriesSonarService _sonar; private readonly WindowsUpdateService _windowsUpdates; private readonly BackupService _backups;

    public SupportReportService(IObsPlatformService platform, AppSettings settings, LoggingService logger, ObsDetector detector,
        PluginInventoryService plugins, LogAnalyzerService logs, SceneAssetScannerService assets, CrashHistoryService crashes,
        SystemHealthService health, GraphicsDriverService graphics, ElgatoHealthService elgato, SteelSeriesSonarService sonar, WindowsUpdateService windowsUpdates, BackupService backups)
    {
        _platform=platform; _settings=settings; _logger=logger; _detector=detector; _plugins=plugins; _logs=logs; _assets=assets;
        _crashes=crashes; _health=health; _graphics=graphics; _elgato=elgato; _sonar=sonar; _windowsUpdates=windowsUpdates; _backups=backups;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var snapshot=_detector.Inspect(); var health=_health.Sample(); var graphics=await _graphics.InspectAsync(cancellationToken);
        var elgato=await _elgato.InspectAsync(cancellationToken); var sonar=await _sonar.InspectAsync(cancellationToken); var win=await _windowsUpdates.CheckMainUpdatesAsync(cancellationToken);
        var plugins=await _plugins.ScanAsync(false,cancellationToken); var findings=_logs.AnalyzeLatest(); var missing=_assets.Scan();
        var crashes=_crashes.List(); var backups=_backups.ListBackups();
        var path=Path.Combine(_logger.LogDirectory,$"ground-control-support-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines=new List<string>{
            "NERDSPACE LABS OBS GROUND CONTROL - SANITIZED SUPPORT REPORT",
            $"Generated: {DateTimeOffset.Now:O}", $"Ground Control: {AppVersion.DisplayVersion}", $"Platform: {_platform.PlatformName}",
            $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}", $"Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}",
            $"Monitoring: {_platform.MonitoringCapability}", $"OBS version: {health.ObsVersion}", $"OBS state: {snapshot.State}",
            $"OBS process count: {health.ObsProcessCount}", $"OBS memory: {health.ObsMemoryMb:F0} MB",
            $"OBS CPU sample: {(health.ObsCpuPercent.HasValue?$"{health.ObsCpuPercent.Value:F1}%":"n/a")}",
            $"Recording free space: {(health.RecordingFreeGb.HasValue?$"{health.RecordingFreeGb.Value:F1} GB":"unknown")}",
            $"Graphics: {graphics.Summary}", $"Elgato software count: {elgato.Software.Count}", $"Elgato hardware count: {elgato.Hardware.Count}", $"SteelSeries Sonar: {sonar.Summary}",
            $"Windows main updates: {(win.Supported?win.Count.ToString():"n/a")}",
            $"Recovery Protection: {_settings.RecoveryProtection}", $"Backups found: {backups.Count}", $"Missing local assets: {missing.Count}", $"OBS crash reports found: {crashes.Count}", "", "PLUGINS"};
        lines.AddRange(plugins.Select(p=>$"- {p.Name} | installed {p.Version} | {p.CompatibilityStatus}"));
        lines.Add(""); lines.Add("LATEST LOG FINDINGS"); lines.AddRange(findings.Select(f=>$"- {f.Severity} | {f.Category} | {f.Summary} | occurrences={f.Occurrences}"));
        lines.Add(""); lines.Add("PRIVACY");
        lines.Add("This report intentionally omits stream keys, OAuth tokens, browser URLs, chat/messages, scene contents, local asset paths, configured OBS paths, and usernames/home-directory paths.");
        await File.WriteAllLinesAsync(path,lines,cancellationToken); _logger.Success($"Sanitized support report created: {path}"); return path;
    }
}
