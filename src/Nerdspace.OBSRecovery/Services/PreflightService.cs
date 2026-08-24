using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class PreflightService
{
    private readonly AppSettings _settings;
    private readonly IObsPlatformService _platform;
    private readonly ObsDetector _detector;
    private readonly BackupService _backups;
    private readonly PluginInventoryService _plugins;
    private readonly LogAnalyzerService _logs;
    private readonly SceneAssetScannerService _assets;
    private readonly SystemHealthService _health;
    private readonly GraphicsDriverService _graphics;
    private readonly ElgatoHealthService _elgato;
    private readonly SteelSeriesSonarService _sonar;
    private readonly WindowsUpdateService _windowsUpdates;
    private readonly CrashHistoryService _crashes;
    private readonly ObsConfigurationInspectorService _obsConfig;
    private readonly BandwidthAdvisorService _bandwidth;

    public PreflightService(AppSettings settings, IObsPlatformService platform, ObsDetector detector, BackupService backups,
        PluginInventoryService plugins, LogAnalyzerService logs, SceneAssetScannerService assets, SystemHealthService health,
        GraphicsDriverService graphics, ElgatoHealthService elgato, SteelSeriesSonarService sonar, WindowsUpdateService windowsUpdates,
        CrashHistoryService crashes, ObsConfigurationInspectorService obsConfig, BandwidthAdvisorService bandwidth)
    {
        _settings = settings; _platform = platform; _detector = detector; _backups = backups; _plugins = plugins; _logs = logs;
        _assets = assets; _health = health; _graphics = graphics; _elgato = elgato; _sonar = sonar; _windowsUpdates = windowsUpdates;
        _crashes = crashes; _obsConfig = obsConfig; _bandwidth = bandwidth;
    }

    public async Task<IReadOnlyList<PreflightResult>> RunAsync(
        PreflightRunOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PreflightResult>();

        progress?.Report("Checking OBS installation…");
        var install = _platform.FindObsInstall();
        results.Add(install is null
            ? new("OBS Installation", CheckSeverity.Fail, "OBS was not detected", "Set the OBS path in Settings before launch.")
            : new("OBS Installation", CheckSeverity.Pass, "OBS detected", install));

        progress?.Report("Inspecting OBS process health…");
        var snapshot = _detector.Inspect();
        results.Add(snapshot.State switch
        {
            ObsHealthState.Hung or ObsHealthState.StuckShutdown => new("OBS Process", CheckSeverity.Fail, snapshot.Message, "Use Recovery before starting a stream."),
            ObsHealthState.MultipleInstances or ObsHealthState.TemporarilyUnresponsive => new("OBS Process", CheckSeverity.Warning, snapshot.Message, "Review the running OBS state before going live."),
            _ => new("OBS Process", CheckSeverity.Pass, snapshot.State == ObsHealthState.Offline ? "OBS is cleanly offline" : "OBS process state looks usable", snapshot.Message)
        });

        progress?.Report(options.SkipUpdateChecks ? "Skipping software update checks for this run…" : "Checking OBS release status…");
        if (!options.SkipUpdateChecks && _settings.CheckUpdatesOnline)
            await _health.RefreshLatestVersionAsync(cancellationToken);
        var health = _health.Sample();
        if (options.SkipUpdateChecks)
        {
            results.Add(new("OBS Version", CheckSeverity.Info, "Update check skipped this run", $"Installed OBS: {health.ObsVersion}. Local readiness checks continue normally."));
        }
        else
        {
            var obsUpdateSeverity = health.UpdateStatus == "Update available" ? CheckSeverity.Warning : health.UpdateStatus == "Current" ? CheckSeverity.Pass : CheckSeverity.Info;
            results.Add(new("OBS Version", obsUpdateSeverity, $"{health.ObsVersion} installed • {health.UpdateStatus}", $"Latest release check: {health.LatestObsVersion}"));
        }

        progress?.Report("Checking recording storage…");
        if (health.RecordingFreeGb.HasValue)
        {
            var low = health.RecordingFreeGb.Value < _settings.RecordingDiskWarningGb;
            results.Add(new("Recording Disk", low ? CheckSeverity.Warning : CheckSeverity.Pass, $"{health.RecordingFreeGb.Value:F1} GB free", $"Recording path: {health.RecordingPath}"));
        }
        else results.Add(new("Recording Disk", CheckSeverity.Info, "Recording path not detected", "Ground Control could not determine the recording drive from OBS profiles."));

        progress?.Report("Reading current OBS output profile…");
        var obsOutput = _obsConfig.Inspect();
        results.Add(obsOutput.Found
            ? new("Current OBS Output", CheckSeverity.Info, obsOutput.Display, obsOutput.Detail)
            : new("Current OBS Output", CheckSeverity.Info, "Current output settings not identified", obsOutput.Detail));

        progress?.Report("Inspecting GPU and local driver state…");
        var graphics = await _graphics.InspectAsync(cancellationToken);
        var nvidia = graphics.Adapters.Where(x => x.Vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase)).ToList();
        var amd = graphics.Adapters.Where(x => x.Vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase)).ToList();

        results.Add(nvidia.Count == 0
            ? new("NVIDIA", CheckSeverity.Info, "Not detected — No NVIDIA GPU found. Check skipped.", "This is normal on systems that do not use NVIDIA graphics.")
            : new("NVIDIA", nvidia.Any(x => x.TemperatureC >= 85) ? CheckSeverity.Warning : CheckSeverity.Info,
                string.Join(" | ", nvidia.Select(x => x.Display)),
                "Installed driver and local telemetry detected. Use Updates for the official NVIDIA driver page."));

        results.Add(amd.Count == 0
            ? new("AMD", CheckSeverity.Info, "Not detected — No AMD GPU found. Check skipped.", "This is normal on systems that do not use AMD graphics.")
            : new("AMD", CheckSeverity.Info, string.Join(" | ", amd.Select(x => x.Display)),
                "Installed AMD graphics state detected. Use Updates for the official AMD driver page."));

        progress?.Report("Inspecting Elgato hardware and software…");
        var elgato = await _elgato.InspectAsync(cancellationToken);
        results.Add(!elgato.AnyDetected
            ? new("Elgato Hardware & Software", CheckSeverity.Info, "Not detected — No supported Elgato hardware or software found. Check skipped.", "This is normal if you do not use Elgato products on this computer.")
            : new("Elgato Hardware & Software", elgato.AttentionRecommended ? CheckSeverity.Warning : CheckSeverity.Info, elgato.Summary, elgato.Detail));

        progress?.Report("Inspecting SteelSeries Sonar…");
        var sonar = await _sonar.InspectAsync(cancellationToken);
        results.Add(!sonar.Supported
            ? new("SteelSeries Sonar", CheckSeverity.Info, sonar.Status, sonar.Detail)
            : !sonar.AnyDetected
                ? new("SteelSeries Sonar", CheckSeverity.Info, "Not detected — SteelSeries GG / Sonar not found. Check skipped.", "This is normal if you do not use SteelSeries Sonar on this computer.")
                : new("SteelSeries Sonar", CheckSeverity.Info, sonar.Summary, sonar.Detail));

        if (options.SkipUpdateChecks)
        {
            results.Add(new("Windows Update", CheckSeverity.Info, "Main update check skipped this run", "Driver, preview, optional, Defender-definition, and other non-main updates are not part of Ground Control's main update check."));
        }
        else
        {
            progress?.Report("Checking Windows main updates…");
            var windows = await _windowsUpdates.CheckMainUpdatesAsync(cancellationToken);
            results.Add(windows.Supported
                ? new("Windows Update", windows.Count > 0 ? CheckSeverity.Warning : CheckSeverity.Pass, windows.Summary, windows.Detail)
                : new("Windows Update", CheckSeverity.Warning, windows.Status, windows.Detail));
        }

        if (options.RunBandwidthTest)
        {
            progress?.Report("Running Bandwidth Advisor…");
            try
            {
                var bandwidth = await _bandwidth.RunAsync(options.Platform, options.Motion, options.TwitchEnhancedBroadcasting,
                    options.TwitchServerSideTranscode, progress, cancellationToken);
                if (!bandwidth.Success || bandwidth.Recommendation is null)
                    results.Add(new("Bandwidth Advisor", CheckSeverity.Warning, bandwidth.Status, bandwidth.Detail));
                else
                {
                    var r = bandwidth.Recommendation;
                    var currentTooHigh = obsOutput.VideoBitrateKbps.HasValue && obsOutput.VideoBitrateKbps.Value > r.VideoBitrateKbps;
                    var severity = r.SafeBudgetMbps < 1.2 || r.Confidence.StartsWith("LOW", StringComparison.OrdinalIgnoreCase) || currentTooHigh
                        ? CheckSeverity.Warning : CheckSeverity.Pass;
                    var comparison = currentTooHigh
                        ? $" Current OBS video bitrate is {obsOutput.VideoBitrateKbps:N0} Kbps, above Ground Control's conservative {r.VideoBitrateKbps:N0} Kbps recommendation."
                        : obsOutput.VideoBitrateKbps.HasValue
                            ? $" Current OBS video bitrate is {obsOutput.VideoBitrateKbps:N0} Kbps."
                            : " Current OBS video bitrate could not be read.";
                    results.Add(new("Bandwidth Advisor", severity, r.Headline,
                        $"{r.BudgetLine}. {r.Rationale} {r.PlatformNote}{comparison}"));
                }
            }
            catch (Exception ex)
            {
                results.Add(new("Bandwidth Advisor", CheckSeverity.Warning, "Bandwidth scan could not complete", ex.Message));
            }
        }
        else
        {
            results.Add(new("Bandwidth Advisor", CheckSeverity.Info, "Bandwidth scan not requested", "Enable 'Run Bandwidth Advisor' when you want an upload-based bitrate and resolution recommendation."));
        }

        progress?.Report(options.SkipUpdateChecks ? "Scanning installed OBS plugins (update lookup skipped)…" : "Scanning OBS plugins and verified update sources…");
        var pluginOnlineCheck = !options.SkipUpdateChecks && _settings.CheckUpdatesOnline;
        var plugins = await _plugins.ScanAsync(checkUpdates: pluginOnlineCheck, cancellationToken);
        var loadIssues = plugins.Count(x => x.CompatibilityStatus == "Load issue detected");
        var updatesAvailable = plugins.Count(x => x.UpdateStatus.Equals("Update available", StringComparison.OrdinalIgnoreCase));
        var deferred = plugins.Count(x => x.UpdateStatus.StartsWith("Deferred until", StringComparison.OrdinalIgnoreCase) || x.UpdateStatus.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase));
        var unknownSources = plugins.Count(x => x.UpdateStatus.Contains("not verified", StringComparison.OrdinalIgnoreCase));
        var pluginSeverity = loadIssues > 0 || updatesAvailable > 0 ? CheckSeverity.Warning : CheckSeverity.Pass;
        var pluginSummary = loadIssues > 0
            ? $"{loadIssues} load issue(s) • {updatesAvailable} update(s) available • {deferred} deferred/skipped"
            : pluginOnlineCheck
                ? $"{updatesAvailable} update(s) available • {deferred} deferred/skipped • {plugins.Count - unknownSources} verified/known entries"
                : $"{plugins.Count} plugin entries detected • update lookup skipped this run";
        results.Add(new("Plugins", pluginSeverity, pluginSummary,
            unknownSources > 0
                ? $"{unknownSources} plugin(s) do not have a verified update source in Ground Control. Deferred/skipped exact versions do not create repeated Pre-Flight warnings."
                : "Installed versions were compared with verified latest releases where available. Deferred/skipped exact versions do not create repeated Pre-Flight warnings."));

        progress?.Report("Analyzing the latest OBS log…");
        var logFindings = _logs.AnalyzeLatest();
        var serious = logFindings.Count(x => x.Severity is CheckSeverity.Warning or CheckSeverity.Fail);
        results.Add(serious > 0
            ? new("Latest OBS Log", CheckSeverity.Warning, $"{serious} warning/error pattern(s) detected", "Open Diagnostics for details.")
            : new("Latest OBS Log", CheckSeverity.Pass, "No known high-priority patterns detected", "Ground Control scanned the latest OBS log."));

        progress?.Report("Checking scene assets…");
        var missing = _assets.Scan();
        results.Add(missing.Count == 0
            ? new("Scene Assets", CheckSeverity.Pass, "No missing local assets detected", "Scene collection JSON was checked for missing absolute local file references.")
            : new("Scene Assets", CheckSeverity.Warning, $"{missing.Count} missing local asset(s)", "Open Diagnostics to review the missing-file list."));

        progress?.Report("Checking backup freshness…");
        var backups = _backups.ListBackups();
        if (!_settings.PreflightCheckBackupAge)
            results.Add(new("Backup", CheckSeverity.Info, "Backup-age check disabled", "You can re-enable it in Settings."));
        else if (backups.Count == 0)
            results.Add(new("Backup", CheckSeverity.Warning, "No Ground Control backup found", "Create a backup before large OBS/plugin changes."));
        else
        {
            var age = DateTimeOffset.Now - backups[0].Created;
            results.Add(new("Backup", age.TotalDays > _settings.BackupWarningAgeDays ? CheckSeverity.Warning : CheckSeverity.Pass,
                $"Latest backup is {Math.Max(0, (int)age.TotalDays)} day(s) old", backups[0].Name));
        }

        progress?.Report("Checking recent OBS crash history…");
        var recentCrash = _crashes.List(1).FirstOrDefault();
        if (recentCrash is not null && DateTime.Now - recentCrash.Timestamp < TimeSpan.FromDays(7))
            results.Add(new("Crash History", CheckSeverity.Warning, "Recent OBS crash report found", recentCrash.Display));
        else
            results.Add(new("Crash History", CheckSeverity.Pass, "No recent OBS crash report detected", "Ground Control checked the OBS crash-report directory."));

        progress?.Report("Pre-Flight complete.");
        return results;
    }
}
