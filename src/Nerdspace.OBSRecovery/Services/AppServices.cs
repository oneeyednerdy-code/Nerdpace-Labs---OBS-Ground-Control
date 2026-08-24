using Nerdspace.OBSRecovery.Platform;
using Nerdspace.OBSRecovery.UI;

namespace Nerdspace.OBSRecovery.Services;

public static class AppServices
{
    public static MainWindow CreateMainWindow()
    {
        var platform = new WindowsObsPlatformService();
        var settingsService = new SettingsService(platform);
        var settings = settingsService.Load();
        var logger = new LoggingService(platform);
        var updates = new UpdateService(logger);
        var updateDeferrals = new UpdateDeferralService(settings, settingsService);
        var detector = new ObsDetector(settings, logger, platform);
        var recovery = new RecoveryService(settings, settingsService, logger, platform, detector);
        var plugins = new PluginInventoryService(platform, logger, updates, updateDeferrals);
        var quarantine = new PluginQuarantineService(platform, logger);
        var backups = new BackupService(settings, platform, logger);
        var logs = new LogAnalyzerService(platform, logger);
        var assets = new SceneAssetScannerService(platform, logger);
        var health = new SystemHealthService(settings, platform, updates, logger);
        var graphics = new GraphicsDriverService(logger);
        var elgato = new ElgatoHealthService(logger);
        var sonar = new SteelSeriesSonarService(logger);
        var windowsUpdates = new WindowsUpdateService(logger);
        var crashes = new CrashHistoryService(platform);
        var obsConfig = new ObsConfigurationInspectorService(platform, logger);
        var bandwidth = new BandwidthAdvisorService(logger);
        var preflight = new PreflightService(settings, platform, detector, backups, plugins, logs, assets, health,
            graphics, elgato, sonar, windowsUpdates, crashes, obsConfig, bandwidth);
        var support = new SupportReportService(platform, settings, logger, detector, plugins, logs, assets, crashes,
            health, graphics, elgato, sonar, windowsUpdates, backups);

        return new MainWindow(platform, settings, settingsService, logger, detector, recovery, plugins, updateDeferrals, quarantine,
            backups, logs, assets, health, graphics, elgato, sonar, windowsUpdates, crashes, obsConfig, bandwidth, preflight, support);
    }
}
