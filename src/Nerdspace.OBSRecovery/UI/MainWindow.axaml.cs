using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;
using Nerdspace.OBSRecovery.Services;

namespace Nerdspace.OBSRecovery.UI;

public partial class MainWindow : Window
{
    private readonly IObsPlatformService _platform;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly LoggingService _logger;
    private readonly ObsDetector _detector;
    private readonly RecoveryService _recovery;
    private readonly PluginInventoryService _plugins;
    private readonly PluginDiscoveryService _pluginDiscovery;
    private readonly UpdateDeferralService _updateDeferrals;
    private readonly PluginQuarantineService _quarantine;
    private readonly BackupService _backups;
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
    private readonly PreflightService _preflight;
    private readonly SupportReportService _support;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly ObservableCollection<string> _history = new();
    private ObsSnapshot? _lastSnapshot;
    private bool _monitorBusy;
    private bool _allowClose;
    private IReadOnlyList<PluginInfo> _pluginItems = Array.Empty<PluginInfo>();
    private IReadOnlyList<PluginInfo> _pluginUpdateItems = Array.Empty<PluginInfo>();
    private IReadOnlyList<PluginDiscoveryInfo> _pluginDiscoveryItems = Array.Empty<PluginDiscoveryInfo>();
    private IReadOnlyList<PluginQuarantineService.QuarantineItem> _quarantineItems = Array.Empty<PluginQuarantineService.QuarantineItem>();
    private IReadOnlyList<BackupInfo> _backupItems = Array.Empty<BackupInfo>();
    private GraphicsDriverSnapshot? _graphicsSnapshot;
    private BandwidthTestResult? _lastBandwidthResult;

    public MainWindow(
        IObsPlatformService platform,
        AppSettings settings,
        SettingsService settingsService,
        LoggingService logger,
        ObsDetector detector,
        RecoveryService recovery,
        PluginInventoryService plugins,
        PluginDiscoveryService pluginDiscovery,
        UpdateDeferralService updateDeferrals,
        PluginQuarantineService quarantine,
        BackupService backups,
        LogAnalyzerService logs,
        SceneAssetScannerService assets,
        SystemHealthService health,
        GraphicsDriverService graphics,
        ElgatoHealthService elgato,
        SteelSeriesSonarService sonar,
        WindowsUpdateService windowsUpdates,
        CrashHistoryService crashes,
        ObsConfigurationInspectorService obsConfig,
        BandwidthAdvisorService bandwidth,
        PreflightService preflight,
        SupportReportService support)
    {
        _platform = platform;
        _settings = settings;
        _settingsService = settingsService;
        _logger = logger;
        _detector = detector;
        _recovery = recovery;
        _plugins = plugins;
        _pluginDiscovery = pluginDiscovery;
        _updateDeferrals = updateDeferrals;
        _quarantine = quarantine;
        _backups = backups;
        _logs = logs;
        _assets = assets;
        _health = health;
        _graphics = graphics;
        _elgato = elgato;
        _sonar = sonar;
        _windowsUpdates = windowsUpdates;
        _crashes = crashes;
        _obsConfig = obsConfig;
        _bandwidth = bandwidth;
        _preflight = preflight;
        _support = support;

        InitializeComponent();
        InitializeState();
        WireEvents();

        _logger.EntryWritten += line => Dispatcher.UIThread.Post(() => AddHistory(line));
        _logger.Info($"OBS Ground Control started on {_platform.PlatformName}.");
        _timer.Tick += async (_, _) => await MonitorTickAsync();
        _timer.Start();
        Closing += OnClosing;
        Opened += async (_, _) =>
        {
            await MonitorTickAsync();
            RefreshHealthLocal();
            RefreshBackupList();
            RefreshQuarantineList();
            await RefreshDiagnosticsAsync();
            UpdatePreflightBandwidthProfile();
            RefreshCurrentObsOutput();
        };
    }

    private void InitializeState()
    {
        PlatformText.Text = _platform.PlatformName;
        VersionBadge.Text = AppVersion.DisplayVersion;
        FooterBrand.Text = $"Nerdspace Labs by OneEyedNerdy • {AppVersion.DisplayVersion}";

        ObsPathBox.Text = _settings.ObsPath;
        ProtectionToggle.IsChecked = _settings.RecoveryProtection;
        RelaunchToggle.IsChecked = _settings.RelaunchAfterHungRecovery;
        StartAtLoginToggle.IsChecked = _settings.StartWithOperatingSystem;
        OnlineUpdatesToggle.IsChecked = _settings.CheckUpdatesOnline;
        AutoBackupRestoreToggle.IsChecked = _settings.AutoBackupBeforeRestore;
        BackupAgeBox.Text = _settings.BackupWarningAgeDays.ToString();
        DiskWarningBox.Text = _settings.RecordingDiskWarningGb.ToString("0.#");
        BackupDirectoryBox.Text = _settings.BackupDirectory;
        RunBandwidthInPreflightToggle.IsChecked = _settings.RunBandwidthTestInPreflight;
        LaunchAfterReadyToggle.IsChecked = false;
        SkipUpdatesThisRunToggle.IsChecked = false;

        ProtectionNote.Text = "Windows Recovery Protection uses OBS window state, process responsiveness, and stuck-shutdown timing before taking recovery action.";

        HistoryList.ItemsSource = _history;
        RestoreScopeSelect.ItemsSource = Enum.GetNames<BackupRestoreScope>();
        RestoreScopeSelect.SelectedIndex = 0;

        BandwidthPlatformSelect.ItemsSource = Enum.GetNames<StreamingPlatform>();
        BandwidthMotionSelect.ItemsSource = Enum.GetNames<MotionProfile>();
        BandwidthPlatformSelect.SelectedItem = Enum.TryParse<StreamingPlatform>(_settings.PreferredStreamingPlatform, out var p) ? p.ToString() : StreamingPlatform.Twitch.ToString();
        BandwidthMotionSelect.SelectedItem = Enum.TryParse<MotionProfile>(_settings.PreferredMotionProfile, out var m) ? m.ToString() : MotionProfile.Balanced.ToString();
        BandwidthEnhancedToggle.IsChecked = _settings.TwitchEnhancedBroadcasting;
        BandwidthServerTranscodeToggle.IsChecked = _settings.TwitchServerSideTranscode;
        UpdateTwitchBandwidthControls();

        CheckWindowsButton.IsEnabled = true;
        OpenWindowsUpdateButton.IsEnabled = true;
        CheckSonarButton.IsEnabled = true;

    }

    private void WireEvents()
    {
        LaunchButton.Click += async (_, _) => await RunActionAsync(() => _recovery.LaunchAsync());
        ShowButton.Click += async (_, _) => await RunActionAsync(() => _recovery.ShowAsync(_lastSnapshot?.ProcessId));
        RestartButton.Click += async (_, _) => await ConfirmAndRestartAsync(false);
        SafeModeButton.Click += async (_, _) => await LaunchSafeModeAsync();
        ForceCloseButton.Click += async (_, _) => await ConfirmAndForceCloseAsync();

        PreflightHeaderButton.Click += (_, _) => MainTabs.SelectedItem = PreflightTab;
        DashboardPreflightButton.Click += (_, _) => MainTabs.SelectedItem = PreflightTab;
        RunPreflightButton.Click += async (_, _) => await RunPreflightAsync();
        OpenBandwidthFromPreflightButton.Click += (_, _) => MainTabs.SelectedItem = BandwidthTab;

        RunBandwidthButton.Click += async (_, _) => await RunBandwidthAsync();
        CalculateManualBandwidthButton.Click += async (_, _) => await CalculateManualBandwidthAsync();
        OpenCloudflareSpeedTestButton.Click += (_, _) => TryAction(_bandwidth.OpenCloudflareSpeedTest);
        BandwidthPlatformSelect.SelectionChanged += async (_, _) => { UpdateTwitchBandwidthControls(); UpdatePreflightBandwidthProfile(); await SaveSettingsAsync(true); };
        BandwidthMotionSelect.SelectionChanged += async (_, _) => { UpdatePreflightBandwidthProfile(); await SaveSettingsAsync(true); };
        BandwidthEnhancedToggle.IsCheckedChanged += async (_, _) => { UpdateTwitchBandwidthControls(); UpdatePreflightBandwidthProfile(); await SaveSettingsAsync(true); };
        BandwidthServerTranscodeToggle.IsCheckedChanged += async (_, _) => { UpdatePreflightBandwidthProfile(); await SaveSettingsAsync(true); };
        RunBandwidthInPreflightToggle.IsCheckedChanged += async (_, _) => await SaveSettingsAsync(true);

        CheckObsUpdateButton.Click += async (_, _) => await CheckObsUpdateAsync();
        CheckGraphicsButton.Click += async (_, _) => await CheckGraphicsAsync();
        OpenNvidiaDriversButton.Click += (_, _) => TryAction(() => _graphics.OpenVendorDriverPage("NVIDIA"));
        OpenAmdDriversButton.Click += (_, _) => TryAction(() => _graphics.OpenVendorDriverPage("AMD"));
        CheckElgatoButton.Click += async (_, _) => await CheckElgatoAsync();
        OpenElgatoButton.Click += (_, _) => TryAction(_elgato.OpenDownloads);
        CheckSonarButton.Click += async (_, _) => await CheckSonarAsync();
        OpenSonarButton.Click += (_, _) => TryAction(_sonar.OpenSonarPage);
        CheckWindowsButton.Click += async (_, _) => await CheckWindowsUpdatesAsync();
        OpenWindowsUpdateButton.Click += (_, _) => TryAction(_windowsUpdates.OpenWindowsUpdate);
        CheckAllUpdatesButton.Click += async (_, _) => await CheckAllUpdatesAsync();

        ScanPluginsButton.Click += async (_, _) => await ScanPluginsAsync(false);
        CheckPluginUpdatesButton.Click += async (_, _) => await ScanPluginsAsync(true);
        PluginSelect.SelectionChanged += (_, _) => UpdatePluginDetail();
        PluginUpdateSelect.SelectionChanged += (_, _) => UpdatePluginUpdateDetail();
        OpenPluginObsResourceButton.Click += (_, _) => OpenSelectedPluginObsResource();
        OpenPluginPageButton.Click += (_, _) => OpenSelectedPluginPage();
        OpenPluginUpdateButton.Click += (_, _) => OpenSelectedUpdatePluginPage();
        SnoozePluginButton.Click += async (_, _) => await SnoozeSelectedPluginAsync();
        SkipPluginVersionButton.Click += async (_, _) => await SkipSelectedPluginVersionAsync();
        ClearPluginDeferralButton.Click += async (_, _) => await ClearSelectedPluginDeferralAsync();
        QuarantineButton.Click += async (_, _) => await QuarantineSelectedAsync();
        RestorePluginButton.Click += async (_, _) => await RestoreQuarantinedAsync();

        DiscoverPluginSearchButton.Click += async (_, _) => await SearchDiscoveryAsync(false);
        DiscoverCheckVersionsButton.Click += async (_, _) => await SearchDiscoveryAsync(true);
        DiscoverPluginSelect.SelectionChanged += (_, _) => UpdateDiscoveryDetail();
        OpenDiscoverObsPageButton.Click += (_, _) => OpenSelectedDiscoveryObsPage();
        OpenDiscoverRepositoryButton.Click += (_, _) => OpenSelectedDiscoveryRepository();
        BrowseOfficialPluginsButton.Click += (_, _) => OpenUrl(PluginDiscoveryService.OfficialObsPluginDirectoryUrl);
        DiscoverCatalogSummaryText.Text = _pluginDiscovery.CatalogSummary;

        CreateBackupButton.Click += async (_, _) => await CreateBackupAsync();
        BackupSelect.SelectionChanged += (_, _) => UpdateBackupDetail();
        RestoreBackupButton.Click += async (_, _) => await RestoreBackupAsync();
        CompareBackupButton.Click += async (_, _) => await CompareBackupAsync();
        OpenBackupFolderButton.Click += (_, _) => OpenFolder(_backups.BackupDirectory);

        RefreshDiagnosticsButton.Click += async (_, _) => await RefreshDiagnosticsWithBusyAsync();
        DiagnosticButton.Click += async (_, _) => await CreateSupportReportAsync();
        OpenLogsButton.Click += (_, _) => OpenFolder(_logger.LogDirectory);
        OpenObsLogsButton.Click += (_, _) => OpenFolder(_platform.GetObsLogDirectory());

        AutoDetectButton.Click += (_, _) => AutoDetect();
        SaveSettingsButton.Click += async (_, _) => await SaveSettingsAsync();
        ProtectionToggle.IsCheckedChanged += async (_, _) => await SaveSettingsAsync(true);
        RelaunchToggle.IsCheckedChanged += async (_, _) => await SaveSettingsAsync(true);
        StartAtLoginToggle.IsCheckedChanged += async (_, _) => await SaveSettingsAsync(true);
    }

    private async Task MonitorTickAsync()
    {
        if (_monitorBusy) return;
        _monitorBusy = true;
        try
        {
            var snapshot = _detector.Inspect();
            UpdateStatus(snapshot);
            if (_lastSnapshot?.State != snapshot.State)
                AddHistory($"{DateTime.Now:HH:mm:ss}  {snapshot.State}: {snapshot.Message}");
            _lastSnapshot = snapshot;
            await _recovery.HandleAutomaticRecoveryAsync(snapshot);
            RefreshHealthLocal();
        }
        catch (Exception ex)
        {
            _logger.Error($"Monitor error: {ex.Message}");
        }
        finally { _monitorBusy = false; }
    }

    private void UpdateStatus(ObsSnapshot snapshot)
    {
        StatusTitle.Text = snapshot.State switch
        {
            ObsHealthState.Healthy => "OBS is healthy",
            ObsHealthState.Offline => "OBS is offline",
            ObsHealthState.Hung => "OBS appears hung",
            ObsHealthState.StuckShutdown => "Stuck OBS shutdown",
            ObsHealthState.TemporarilyUnresponsive => "OBS is not responding",
            ObsHealthState.MultipleInstances => "Multiple OBS instances",
            ObsHealthState.LimitedMonitoring => "OBS is running",
            _ => "OBS state needs attention"
        };
        StatusDetail.Text = snapshot.Message;
        CapabilityText.Text = snapshot.CapabilityNote;
        PidText.Text = snapshot.ProcessId.HasValue ? $"PID {snapshot.ProcessId}" : "PID —";
        StatusDot.Fill = snapshot.State switch
        {
            ObsHealthState.Healthy or ObsHealthState.LimitedMonitoring => Brush.Parse("#4BC27A"),
            ObsHealthState.Offline => Brush.Parse("#747B87"),
            ObsHealthState.TemporarilyUnresponsive or ObsHealthState.MultipleInstances => Brush.Parse("#FFB340"),
            ObsHealthState.Hung or ObsHealthState.StuckShutdown => Brush.Parse("#FF5D68"),
            _ => Brush.Parse("#A071FF")
        };
        LaunchButton.IsEnabled = snapshot.CanLaunch;
        ShowButton.IsEnabled = snapshot.CanShow;
        RestartButton.IsEnabled = snapshot.CanRestart;
        ForceCloseButton.IsEnabled = snapshot.CanForceClose;
    }

    private void RefreshHealthLocal()
    {
        var health = _health.Sample();
        var cpu = health.ObsCpuPercent.HasValue ? $"{health.ObsCpuPercent.Value:F1}% CPU" : "CPU sampling…";
        var disk = health.RecordingFreeGb.HasValue ? $"{health.RecordingFreeGb.Value:F1} GB recording disk free" : "recording disk unknown";
        HealthSummaryText.Text = $"OBS {health.ObsVersion} • {health.ObsProcessCount} process(es) • {health.ObsMemoryMb:F0} MB • {cpu} • {disk}";
        ObsUpdateText.Text = $"OBS update status: {health.UpdateStatus} • latest check: {health.LatestObsVersion}";
    }

    private async Task CheckObsUpdateAsync()
    {
        await WithBusyAsync(CheckObsUpdateButton, "Checking…", UpdatesProgress, UpdatesProgressText, "Checking the latest OBS release…", async () =>
        {
            await RefreshObsUpdateCoreAsync();
        });
    }

    private async Task RefreshObsUpdateCoreAsync()
    {
        await _health.RefreshLatestVersionAsync();
        var health = _health.Sample();
        RefreshHealthLocal();
        ObsUpdateCardText.Text = $"Installed: {health.ObsVersion}\nLatest: {health.LatestObsVersion}\nStatus: {health.UpdateStatus}";
    }

    private async Task CheckGraphicsAsync()
    {
        await WithBusyAsync(CheckGraphicsButton, "Scanning…", UpdatesProgress, UpdatesProgressText, "Inspecting NVIDIA and AMD graphics state…", RefreshGraphicsCoreAsync);
    }

    private async Task RefreshGraphicsCoreAsync()
    {
        _graphicsSnapshot = await _graphics.InspectAsync();
        var nvidia = _graphicsSnapshot.Adapters.Where(x => x.Vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase)).ToList();
        var amd = _graphicsSnapshot.Adapters.Where(x => x.Vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase)).ToList();

        NvidiaUpdateText.Text = nvidia.Count == 0
            ? "Not detected — No NVIDIA GPU found. Check skipped."
            : $"{string.Join("\n", nvidia.Select(x => x.Display))}\nInstalled state detected. Use the official NVIDIA page to verify the newest compatible driver.";
        AmdUpdateText.Text = amd.Count == 0
            ? "Not detected — No AMD GPU found. Check skipped."
            : $"{string.Join("\n", amd.Select(x => x.Display))}\nInstalled state detected. Use the official AMD page to verify the newest compatible driver.";
    }

    private async Task CheckElgatoAsync()
    {
        await WithBusyAsync(CheckElgatoButton, "Scanning…", UpdatesProgress, UpdatesProgressText, "Inspecting Elgato hardware and software…", RefreshElgatoCoreAsync);
    }

    private async Task RefreshElgatoCoreAsync()
    {
        var snapshot = await _elgato.InspectAsync();
        ElgatoText.Text = !snapshot.AnyDetected
            ? "Not detected — No supported Elgato hardware or software found. Check skipped."
            : $"{snapshot.Summary}\n{snapshot.Detail}";
    }

    private async Task CheckSonarAsync()
    {
        await WithBusyAsync(CheckSonarButton, "Scanning…", UpdatesProgress, UpdatesProgressText, "Inspecting SteelSeries GG and Sonar…", RefreshSonarCoreAsync);
    }

    private async Task RefreshSonarCoreAsync()
    {
        var snapshot = await _sonar.InspectAsync();
        SteelSeriesText.Text = snapshot.Summary + "\n" + snapshot.Detail;
    }

    private async Task CheckWindowsUpdatesAsync()
    {
        await WithBusyAsync(CheckWindowsButton, "Checking…", UpdatesProgress, UpdatesProgressText, "Checking Windows main updates…", RefreshWindowsUpdateCoreAsync);
    }

    private async Task RefreshWindowsUpdateCoreAsync()
    {
        var snapshot = await _windowsUpdates.CheckMainUpdatesAsync();
        WindowsUpdateText.Text = snapshot.Count == 0
            ? $"{snapshot.Summary}\n{snapshot.Detail}"
            : $"{snapshot.Summary}\n{string.Join("\n", snapshot.Updates.Take(5).Select(x => "• " + x.Display))}{(snapshot.Count > 5 ? $"\n+ {snapshot.Count - 5} more" : string.Empty)}";
    }

    private async Task CheckAllUpdatesAsync()
    {
        var original = CheckAllUpdatesButton.Content;
        CheckAllUpdatesButton.IsEnabled = false;
        CheckAllUpdatesButton.Content = "Checking…";
        UpdatesProgress.IsVisible = true;
        try
        {
            UpdatesProgressText.Text = "Checking OBS release…";
            await RefreshObsUpdateCoreAsync();
            UpdatesProgressText.Text = "Inspecting graphics drivers…";
            await RefreshGraphicsCoreAsync();
            UpdatesProgressText.Text = "Inspecting Elgato hardware and software…";
            await RefreshElgatoCoreAsync();
            UpdatesProgressText.Text = "Inspecting SteelSeries Sonar…";
            await RefreshSonarCoreAsync();
            UpdatesProgressText.Text = "Checking Windows main updates…";
            await RefreshWindowsUpdateCoreAsync();
            UpdatesProgressText.Text = "Update Center scan complete.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            UpdatesProgressText.Text = "Update Center scan stopped with an error.";
            await ShowErrorAsync(ex.Message);
        }
        finally
        {
            UpdatesProgress.IsVisible = false;
            CheckAllUpdatesButton.Content = original;
            CheckAllUpdatesButton.IsEnabled = true;
        }
    }

    private ObsOutputSnapshot RefreshCurrentObsOutput()
    {
        var output = _obsConfig.Inspect();
        CurrentObsOutputText.Text = output.Found ? $"Current OBS output: {output.Display}" : $"Current OBS output: {output.Detail}";
        return output;
    }

    private async Task RunBandwidthAsync()
    {
        var platform = SelectedStreamingPlatform();
        var motion = SelectedMotionProfile();
        var enhanced = BandwidthEnhancedToggle.IsChecked == true && platform == StreamingPlatform.Twitch;
        var transcode = BandwidthServerTranscodeToggle.IsChecked == true && enhanced;
        var original = RunBandwidthButton.Content;
        RunBandwidthButton.IsEnabled = false;
        RunBandwidthButton.Content = "Testing…";
        BandwidthProgress.IsVisible = true;
        BandwidthSamplesList.ItemsSource = Array.Empty<string>();
        BandwidthSummaryText.Text = "Measuring sustained upload…";
        BandwidthDetailText.Text = "Generated test bytes are being uploaded. No personal files are included.";
        try
        {
            var progress = new Progress<string>(message => BandwidthProgressText.Text = message);
            _lastBandwidthResult = await _bandwidth.RunAsync(platform, motion, enhanced, transcode, progress);
            BandwidthSamplesList.ItemsSource = _lastBandwidthResult.Samples.Select(x => x.Display).ToList();
            BandwidthSummaryText.Text = _lastBandwidthResult.Summary;
            if (_lastBandwidthResult.Recommendation is { } recommendation)
            {
                var output = RefreshCurrentObsOutput();
                var comparison = output.VideoBitrateKbps.HasValue
                    ? output.VideoBitrateKbps.Value > recommendation.VideoBitrateKbps
                        ? $"\n\n⚠ Current OBS video bitrate ({output.VideoBitrateKbps:N0} Kbps) is above this conservative recommendation ({recommendation.VideoBitrateKbps:N0} Kbps)."
                        : $"\n\nCurrent OBS video bitrate ({output.VideoBitrateKbps:N0} Kbps) fits within this recommendation."
                    : "\n\nCurrent OBS video bitrate could not be read for comparison.";
                BandwidthDetailText.Text = $"Average: {_lastBandwidthResult.AverageUploadMbps:F1} Mbps • Peak: {_lastBandwidthResult.PeakUploadMbps:F1} Mbps • Variation: {_lastBandwidthResult.VariationPercent:F0}% • Uploaded: {_lastBandwidthResult.UploadedMegabytes:F0} MB\n\n{recommendation.Rationale}\n{recommendation.PlatformNote}{comparison}\n\n{_lastBandwidthResult.Detail}";
            }
            else
            {
                BandwidthDetailText.Text = _lastBandwidthResult.Detail;
            }
            BandwidthProgressText.Text = _lastBandwidthResult.Status;
            await SaveSettingsAsync(true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Bandwidth Advisor failed: {ex.Message}");
            BandwidthSummaryText.Text = "Bandwidth scan could not complete.";
            BandwidthDetailText.Text = ex.Message;
            BandwidthProgressText.Text = "Scan stopped.";
        }
        finally
        {
            BandwidthProgress.IsVisible = false;
            RunBandwidthButton.Content = original;
            RunBandwidthButton.IsEnabled = true;
            UpdatePreflightBandwidthProfile();
        }
    }


    private async Task CalculateManualBandwidthAsync()
    {
        if (!double.TryParse(ManualUploadBox.Text, out var upload) || upload <= 0 || upload > 100000)
        {
            await ShowErrorAsync("Enter a valid measured upload speed in Mbps, for example 24.3.");
            return;
        }

        var platform = SelectedStreamingPlatform();
        var motion = SelectedMotionProfile();
        var enhanced = BandwidthEnhancedToggle.IsChecked == true && platform == StreamingPlatform.Twitch;
        var transcode = BandwidthServerTranscodeToggle.IsChecked == true && enhanced;
        var recommendation = _bandwidth.Recommend(upload, platform, motion, enhanced, transcode);
        BandwidthSummaryText.Text = $"{recommendation.Headline}\n{recommendation.BudgetLine}\nConnection confidence: MANUAL INPUT";
        var output = RefreshCurrentObsOutput();
        var comparison = output.VideoBitrateKbps.HasValue
            ? output.VideoBitrateKbps.Value > recommendation.VideoBitrateKbps
                ? $"\n\n⚠ Current OBS video bitrate ({output.VideoBitrateKbps:N0} Kbps) is above this conservative recommendation ({recommendation.VideoBitrateKbps:N0} Kbps)."
                : $"\n\nCurrent OBS video bitrate ({output.VideoBitrateKbps:N0} Kbps) fits within this recommendation."
            : "\n\nCurrent OBS video bitrate could not be read for comparison.";
        BandwidthDetailText.Text = $"This recommendation uses the measured upload value you entered rather than an automatic upload scan.\n\n{recommendation.Rationale}\n{recommendation.PlatformNote}{comparison}\n\nFor the most reliable result, use a sustained upload measurement rather than your ISP's advertised plan speed.";
        BandwidthProgressText.Text = "Manual bitrate recommendation calculated.";
        BandwidthSamplesList.ItemsSource = new[] { $"Manual measured upload • {upload:F1} Mbps", $"Conservative budget • {recommendation.SafeBudgetMbps:F1} Mbps" };
        await SaveSettingsAsync(true);
    }

    private async Task RunPreflightAsync()
    {
        var original = RunPreflightButton.Content;
        RunPreflightButton.IsEnabled = false;
        RunPreflightButton.Content = "Running…";
        PreflightProgress.IsVisible = true;
        PreflightSummaryText.Text = "Running Pre-Flight…";
        PreflightList.ItemsSource = Array.Empty<string>();
        try
        {
            var options = new PreflightRunOptions(
                SkipUpdatesThisRunToggle.IsChecked == true,
                RunBandwidthInPreflightToggle.IsChecked == true,
                LaunchAfterReadyToggle.IsChecked == true,
                SelectedStreamingPlatform(),
                SelectedMotionProfile(),
                BandwidthEnhancedToggle.IsChecked == true && SelectedStreamingPlatform() == StreamingPlatform.Twitch,
                BandwidthServerTranscodeToggle.IsChecked == true && BandwidthEnhancedToggle.IsChecked == true && SelectedStreamingPlatform() == StreamingPlatform.Twitch);

            var progress = new Progress<string>(message => PreflightProgressText.Text = message);
            var results = await _preflight.RunAsync(options, progress);
            PreflightList.ItemsSource = results.Select(FormatPreflight).ToList();
            var failures = results.Count(x => x.Severity == CheckSeverity.Fail);
            var warnings = results.Count(x => x.Severity == CheckSeverity.Warning);
            PreflightSummaryText.Text = failures > 0
                ? $"NOT READY • {failures} failure(s) • {warnings} warning(s)"
                : warnings > 0
                    ? $"READY WITH {warnings} WARNING(S)"
                    : "READY • ALL REQUIRED CHECKS CLEAR";

            if (options.LaunchObsAfterReady && failures == 0 && !IsObsRunning())
            {
                PreflightProgressText.Text = "Ready result confirmed. Launching OBS because you opted in…";
                await _recovery.LaunchAsync();
            }
            else if (options.LaunchObsAfterReady && failures > 0)
            {
                PreflightProgressText.Text = "OBS was not launched because Pre-Flight found a failure-level issue.";
            }
            else
            {
                PreflightProgressText.Text = options.SkipUpdateChecks
                    ? "Pre-Flight complete. Update checks were intentionally skipped for this run."
                    : "Pre-Flight complete. OBS was not launched.";
            }

            await SaveSettingsAsync(true);
        }
        catch (Exception ex)
        {
            PreflightSummaryText.Text = "Pre-Flight could not complete.";
            PreflightProgressText.Text = "The scan stopped before finishing.";
            await ShowErrorAsync(ex.Message);
        }
        finally
        {
            PreflightProgress.IsVisible = false;
            RunPreflightButton.Content = original;
            RunPreflightButton.IsEnabled = true;
        }
    }

    private static string FormatPreflight(PreflightResult result)
    {
        var icon = result.Severity switch
        {
            CheckSeverity.Pass => "✓",
            CheckSeverity.Info => "○",
            CheckSeverity.Warning => "⚠",
            CheckSeverity.Fail => "✕",
            _ => "•"
        };
        return $"{icon}  {result.Name.ToUpperInvariant()}  —  {result.Summary}\n    {result.Detail}";
    }

    private StreamingPlatform SelectedStreamingPlatform()
        => Enum.TryParse<StreamingPlatform>(BandwidthPlatformSelect.SelectedItem?.ToString(), out var value) ? value : StreamingPlatform.Twitch;

    private MotionProfile SelectedMotionProfile()
        => Enum.TryParse<MotionProfile>(BandwidthMotionSelect.SelectedItem?.ToString(), out var value) ? value : MotionProfile.Balanced;

    private void UpdateTwitchBandwidthControls()
    {
        var twitch = SelectedStreamingPlatform() == StreamingPlatform.Twitch;
        BandwidthEnhancedToggle.IsEnabled = twitch;
        BandwidthServerTranscodeToggle.IsEnabled = twitch && BandwidthEnhancedToggle.IsChecked == true;
        if (!twitch)
        {
            BandwidthEnhancedToggle.IsChecked = false;
            BandwidthServerTranscodeToggle.IsChecked = false;
        }
    }

    private void UpdatePreflightBandwidthProfile()
    {
        var platform = SelectedStreamingPlatform();
        var motion = SelectedMotionProfile();
        var extras = platform == StreamingPlatform.Twitch
            ? $" • Enhanced {(BandwidthEnhancedToggle.IsChecked == true ? "on" : "off")}" + (BandwidthServerTranscodeToggle.IsChecked == true ? " • server transcode" : string.Empty)
            : string.Empty;
        PreflightBandwidthProfileText.Text = $"{platform} • {MotionLabel(motion)}{extras}. Bandwidth testing is {(RunBandwidthInPreflightToggle.IsChecked == true ? "included" : "not included")} in Pre-Flight.";
    }

    private static string MotionLabel(MotionProfile motion) => motion switch
    {
        MotionProfile.LowMotion => "Low motion / detail-first",
        MotionProfile.HighMotion => "High motion / FPS-first",
        _ => "Balanced"
    };

    private async Task ScanPluginsAsync(bool online)
    {
        var button = online ? CheckPluginUpdatesButton : ScanPluginsButton;
        var progress = online ? PluginUpdateProgress : PluginProgress;
        var progressText = online ? PluginUpdateProgressText : PluginProgressText;
        await WithBusyAsync(button, online ? "Checking…" : "Scanning…", progress, progressText,
            online ? "Checking trusted plugin release sources…" : "Scanning installed third-party OBS plugins…", async () =>
            {
                _pluginItems = await _plugins.ScanAsync(online);
                PluginList.ItemsSource = _pluginItems.Select(x => x.Display).ToList();
                PluginSelect.ItemsSource = _pluginItems.Select(x => x.Name).ToList();
                PluginSelect.SelectedIndex = _pluginItems.Count > 0 ? 0 : -1;
                UpdatePluginDetail();

                _pluginUpdateItems = _pluginItems.Where(x => !string.IsNullOrWhiteSpace(x.Repository)).ToList();
                PluginUpdateList.ItemsSource = _pluginUpdateItems.Select(x => x.Display).ToList();
                PluginUpdateSelect.ItemsSource = _pluginUpdateItems.Select(x => x.Name).ToList();
                PluginUpdateSelect.SelectedIndex = _pluginUpdateItems.Count > 0 ? 0 : -1;
                UpdatePluginUpdateDetail();

                var available = _pluginUpdateItems.Count(x => x.IsUpdateAvailable);
                var deferred = _pluginUpdateItems.Count(x => x.IsDeferred);
                var current = _pluginUpdateItems.Count(x => x.UpdateStatus.Equals("Current", StringComparison.OrdinalIgnoreCase));
                var unknown = _pluginItems.Count(x => string.IsNullOrWhiteSpace(x.Repository));
                PluginUpdateSummaryText.Text = online
                    ? $"{available} update(s) available • {current} current • {deferred} deferred/skipped • {unknown} unverified source(s)."
                    : $"{_pluginItems.Count} installed third-party plugin(s) found • {_pluginUpdateItems.Count} matched to the trusted registry. Run Check Updates for latest versions.";

                RefreshQuarantineList();
                if (online)
                    PluginUpdateProgressText.Text = $"Update check complete • {_pluginUpdateItems.Count} trusted installed plugin(s) reviewed.";
                else
                    PluginProgressText.Text = $"Plugin scan complete • {_pluginItems.Count} installed third-party plugin(s).";
            });
    }

    private void UpdatePluginDetail()
    {
        var plugin = SelectedPlugin();
        if (plugin is null)
        {
            PluginDetailText.Text = "No installed third-party plugin selected.";
            OpenPluginPageButton.IsEnabled = false;
            OpenPluginObsResourceButton.IsEnabled = false;
            return;
        }

        var source = !string.IsNullOrWhiteSpace(plugin.Repository)
            ? $"Trusted registry: {plugin.Repository}"
            : "Update source not verified in Ground Control's registry";
        PluginDetailText.Text =
            $"{plugin.Name}\nInstalled version: {plugin.Version}\nLatest verified version: {plugin.LatestVersion ?? "Not checked"}\nStatus: {plugin.UpdateStatus}\nUpdate source: {source}\nCompatibility: {plugin.CompatibilityStatus}\nCan quarantine safely: {(plugin.CanQuarantine ? "Yes" : "No")}";

        OpenPluginPageButton.IsEnabled = plugin.HasVerifiedRelease || !string.IsNullOrWhiteSpace(plugin.Repository);
        OpenPluginObsResourceButton.IsEnabled = plugin.HasOfficialObsResource;
    }

    private void UpdatePluginUpdateDetail()
    {
        var plugin = SelectedUpdatePlugin();
        if (plugin is null)
        {
            PluginUpdateDetailText.Text = "Run Check Updates, then select a registered installed plugin.";
            OpenPluginUpdateButton.IsEnabled = false;
            SnoozePluginButton.IsEnabled = false;
            SkipPluginVersionButton.IsEnabled = false;
            ClearPluginDeferralButton.IsEnabled = false;
            return;
        }

        PluginUpdateDetailText.Text =
            $"{plugin.Name}\nInstalled: {plugin.Version}\nLatest verified: {plugin.LatestVersion ?? "Not checked"}\nStatus: {plugin.UpdateStatus}\nRepository: {plugin.Repository ?? "Not verified"}";

        var updateKnown = plugin.HasKnownLatestVersion && plugin.HasKnownInstalledVersion &&
            (plugin.IsUpdateAvailable || plugin.IsDeferred);
        OpenPluginUpdateButton.IsEnabled = plugin.HasVerifiedRelease || !string.IsNullOrWhiteSpace(plugin.Repository);
        SnoozePluginButton.IsEnabled = updateKnown;
        SkipPluginVersionButton.IsEnabled = updateKnown;
        ClearPluginDeferralButton.IsEnabled = plugin.HasKnownLatestVersion &&
            _updateDeferrals.Get(PluginInventoryService.DeferralKey(plugin.Id), plugin.LatestVersion) is not null;
    }

    private PluginInfo? SelectedPlugin()
        => PluginSelect.SelectedIndex >= 0 && PluginSelect.SelectedIndex < _pluginItems.Count ? _pluginItems[PluginSelect.SelectedIndex] : null;

    private PluginInfo? SelectedUpdatePlugin()
        => PluginUpdateSelect.SelectedIndex >= 0 && PluginUpdateSelect.SelectedIndex < _pluginUpdateItems.Count ? _pluginUpdateItems[PluginUpdateSelect.SelectedIndex] : null;

    private void OpenSelectedPluginObsResource()
    {
        var plugin = SelectedPlugin();
        if (!string.IsNullOrWhiteSpace(plugin?.ObsResourceUrl)) OpenUrl(plugin.ObsResourceUrl);
    }

    private void OpenSelectedPluginPage()
    {
        var plugin = SelectedPlugin();
        OpenPluginReleaseOrRepository(plugin);
    }

    private void OpenSelectedUpdatePluginPage()
    {
        var plugin = SelectedUpdatePlugin();
        OpenPluginReleaseOrRepository(plugin);
    }

    private void OpenPluginReleaseOrRepository(PluginInfo? plugin)
    {
        if (plugin is null) return;
        if (!string.IsNullOrWhiteSpace(plugin.ReleaseUrl))
        {
            OpenUrl(plugin.ReleaseUrl);
            return;
        }
        if (!string.IsNullOrWhiteSpace(plugin.Repository))
        {
            OpenUrl($"https://github.com/{plugin.Repository}/releases");
            return;
        }
        _logger.Warn("No verified update source is registered for this plugin.");
    }

    private async Task SnoozeSelectedPluginAsync()
    {
        var plugin = SelectedUpdatePlugin();
        if (plugin?.LatestVersion is null) return;
        await _updateDeferrals.SnoozeAsync(PluginInventoryService.DeferralKey(plugin.Id), plugin.LatestVersion, TimeSpan.FromDays(7));
        _logger.Info($"{plugin.Name} {plugin.LatestVersion} update reminder deferred for one week.");
        await ScanPluginsAsync(true);
    }

    private async Task SkipSelectedPluginVersionAsync()
    {
        var plugin = SelectedUpdatePlugin();
        if (plugin?.LatestVersion is null) return;
        await _updateDeferrals.SkipVersionAsync(PluginInventoryService.DeferralKey(plugin.Id), plugin.LatestVersion);
        _logger.Info($"{plugin.Name} {plugin.LatestVersion} will be skipped until a newer release is published or the deferral is cleared.");
        await ScanPluginsAsync(true);
    }

    private async Task ClearSelectedPluginDeferralAsync()
    {
        var plugin = SelectedUpdatePlugin();
        if (plugin is null) return;
        await _updateDeferrals.ClearAsync(PluginInventoryService.DeferralKey(plugin.Id));
        _logger.Info($"Cleared the update reminder/skip state for {plugin.Name}.");
        await ScanPluginsAsync(true);
    }

    private async Task SearchDiscoveryAsync(bool checkLatestVersions)
    {
        var button = checkLatestVersions ? DiscoverCheckVersionsButton : DiscoverPluginSearchButton;
        await WithBusyAsync(button, checkLatestVersions ? "Refreshing…" : "Searching…", DiscoverPluginProgress, DiscoverPluginProgressText,
            checkLatestVersions ? "Checking verified plugin release sources…" : "Searching the trusted plugin registry…", async () =>
            {
                // Refresh the local installed list if it has not been scanned yet so Discover can label installed entries.
                if (_pluginItems.Count == 0)
                    _pluginItems = await _plugins.ScanAsync(false);

                _pluginDiscoveryItems = await _pluginDiscovery.SearchAsync(
                    DiscoverPluginSearchBox.Text,
                    _pluginItems,
                    checkLatestVersions);

                DiscoverPluginList.ItemsSource = _pluginDiscoveryItems.Select(x => x.Display).ToList();
                DiscoverPluginSelect.ItemsSource = _pluginDiscoveryItems.Select(x => x.Entry.DisplayName).ToList();
                DiscoverPluginSelect.SelectedIndex = _pluginDiscoveryItems.Count > 0 ? 0 : -1;
                UpdateDiscoveryDetail();
                DiscoverPluginProgressText.Text = _pluginDiscoveryItems.Count == 0
                    ? "No preloaded OBS resource matches this search. Try Browse Official OBS Plugins for the live directory."
                    : $"{_pluginDiscoveryItems.Count} catalog result(s) • {(checkLatestVersions ? "verified GitHub release sources refreshed where available" : "online version lookup skipped")}.";
            });
    }

    private PluginDiscoveryInfo? SelectedDiscoveryPlugin()
        => DiscoverPluginSelect.SelectedIndex >= 0 && DiscoverPluginSelect.SelectedIndex < _pluginDiscoveryItems.Count
            ? _pluginDiscoveryItems[DiscoverPluginSelect.SelectedIndex]
            : null;

    private void UpdateDiscoveryDetail()
    {
        var item = SelectedDiscoveryPlugin();
        if (item is null)
        {
            DiscoverPluginDetailText.Text = "No discovery result selected.";
            OpenDiscoverObsPageButton.IsEnabled = false;
            OpenDiscoverRepositoryButton.IsEnabled = false;
            return;
        }

        var sourceLabel = item.Entry.HasVerifiedSource
            ? $"Verified source: {item.Entry.SourceUrl}"
            : "Source: No source URL is published on this OBS resource page";
        var minimumObs = string.IsNullOrWhiteSpace(item.Entry.MinimumObsVersion) ? "Not listed" : item.Entry.MinimumObsVersion;
        DiscoverPluginDetailText.Text =
            $"{item.Entry.DisplayName}\nAuthor: {item.Entry.Author}\n{item.Entry.Description}\nPlatforms: {item.Entry.PlatformSummary}\nMinimum OBS: {minimumObs}\nLocal status: {(item.Installed ? $"Installed {item.InstalledVersion}" : "Not detected on this PC")}\nLatest release check: {item.LatestVersion}\n{sourceLabel}";
        OpenDiscoverObsPageButton.IsEnabled = !string.IsNullOrWhiteSpace(item.Entry.ObsResourceUrl);
        OpenDiscoverRepositoryButton.IsEnabled = item.Entry.HasVerifiedSource;
    }

    private void OpenSelectedDiscoveryObsPage()
    {
        var item = SelectedDiscoveryPlugin();
        if (item is not null) OpenUrl(item.Entry.ObsResourceUrl);
    }

    private void OpenSelectedDiscoveryRepository()
    {
        var item = SelectedDiscoveryPlugin();
        if (item is null || !item.Entry.HasVerifiedSource) return;
        var target = !string.IsNullOrWhiteSpace(item.LatestReleaseUrl)
            ? item.LatestReleaseUrl
            : item.Entry.HasGitHubRepository ? item.Entry.ReleasesUrl : item.Entry.SourceUrl ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(target)) OpenUrl(target);
    }

    private async Task QuarantineSelectedAsync()
    {
        var plugin = SelectedPlugin();
        if (plugin is null) return;
        if (!await ConfirmAsync("Quarantine plugin?", $"Ground Control will move the complete {plugin.Name} plugin bundle out of OBS and keep a restore manifest. OBS must be closed.")) return;
        try
        {
            await _quarantine.QuarantineAsync(plugin);
            await ScanPluginsAsync(false);
            RefreshQuarantineList();
        }
        catch (Exception ex) { await ShowErrorAsync(ex.Message); }
    }

    private void RefreshQuarantineList()
    {
        _quarantineItems = _quarantine.List();
        QuarantineSelect.ItemsSource = _quarantineItems.Select(x => x.Display).ToList();
        QuarantineSelect.SelectedIndex = _quarantineItems.Count > 0 ? 0 : -1;
    }

    private async Task RestoreQuarantinedAsync()
    {
        if (QuarantineSelect.SelectedIndex < 0 || QuarantineSelect.SelectedIndex >= _quarantineItems.Count) return;
        try
        {
            await _quarantine.RestoreAsync(_quarantineItems[QuarantineSelect.SelectedIndex]);
            RefreshQuarantineList();
            await ScanPluginsAsync(false);
        }
        catch (Exception ex) { await ShowErrorAsync(ex.Message); }
    }

    private void RefreshBackupList()
    {
        _backupItems = _backups.ListBackups();
        BackupSelect.ItemsSource = _backupItems.Select(x => x.Display).ToList();
        BackupSelect.SelectedIndex = _backupItems.Count > 0 ? 0 : -1;
        UpdateBackupDetail();
    }

    private BackupInfo? SelectedBackup()
        => BackupSelect.SelectedIndex >= 0 && BackupSelect.SelectedIndex < _backupItems.Count ? _backupItems[BackupSelect.SelectedIndex] : null;

    private void UpdateBackupDetail()
    {
        var backup = SelectedBackup();
        BackupDetailText.Text = backup is null
            ? "No Ground Control backup found."
            : $"{backup.Created.LocalDateTime:g} • {backup.FileCount} files • {backup.SizeBytes / 1024d / 1024d:F1} MB\n{backup.Path}";
    }

    private async Task CreateBackupAsync()
    {
        await WithBusyAsync(CreateBackupButton, "Creating…", BackupProgress, BackupProgressText, "Creating a sanitized OBS configuration checkpoint…", async () =>
        {
            var backup = await _backups.CreateBackupAsync();
            RefreshBackupList();
            BackupProgressText.Text = "Backup created successfully.";
            await ShowInfoAsync("Backup created", backup.Path);
        });
    }

    private async Task RestoreBackupAsync()
    {
        var backup = SelectedBackup();
        if (backup is null) return;
        var scope = Enum.TryParse<BackupRestoreScope>(RestoreScopeSelect.SelectedItem?.ToString(), out var parsed) ? parsed : BackupRestoreScope.Everything;
        if (!await ConfirmAsync("Restore OBS backup?", $"Restore {scope} from {backup.Name}? OBS must be closed. Existing files in this scope can be overwritten.")) return;

        await WithBusyAsync(RestoreBackupButton, "Restoring…", BackupProgress, BackupProgressText, "Restoring the selected OBS checkpoint…", async () =>
        {
            await _backups.RestoreAsync(backup, scope, IsObsRunning);
            BackupProgressText.Text = "Restore complete.";
            await ShowInfoAsync("Restore complete", $"Restored {scope} from {backup.Name}.");
        });
    }

    private async Task CompareBackupAsync()
    {
        var backup = SelectedBackup();
        if (backup is null) return;
        await WithBusyAsync(CompareBackupButton, "Comparing…", BackupProgress, BackupProgressText, "Comparing the checkpoint with the current OBS configuration…", async () =>
        {
            var diff = await _backups.CompareToCurrentAsync(backup);
            BackupProgressText.Text = "Comparison complete.";
            await ShowInfoAsync("Backup comparison", $"{diff.Summary}\n\nAdded:\n{string.Join("\n", diff.Added.Take(8))}\n\nRemoved:\n{string.Join("\n", diff.Removed.Take(8))}\n\nChanged:\n{string.Join("\n", diff.Changed.Take(8))}");
        });
    }

    private Task RefreshDiagnosticsAsync()
    {
        DiagnosticList.ItemsSource = _logs.AnalyzeLatest().Select(x => x.Display + "\n    " + x.Detail).ToList();
        MissingAssetsList.ItemsSource = _assets.Scan().Select(x => x.Display).DefaultIfEmpty("No missing local assets detected.").ToList();
        CrashList.ItemsSource = _crashes.List().Select(x => x.Display).DefaultIfEmpty("No OBS crash reports detected.").ToList();
        return Task.CompletedTask;
    }

    private async Task RefreshDiagnosticsWithBusyAsync()
    {
        await WithBusyAsync(RefreshDiagnosticsButton, "Analyzing…", DiagnosticsProgress, DiagnosticsProgressText, "Analyzing OBS logs, scene assets, and crash history…", async () =>
        {
            await RefreshDiagnosticsAsync();
            DiagnosticsProgressText.Text = "Diagnostics refreshed.";
        });
    }

    private async Task CreateSupportReportAsync()
    {
        await WithBusyAsync(DiagnosticButton, "Generating…", DiagnosticsProgress, DiagnosticsProgressText, "Generating a sanitized support report…", async () =>
        {
            var path = await _support.GenerateAsync();
            DiagnosticsProgressText.Text = "Sanitized support report created.";
            await ShowInfoAsync("Support report created", path);
        });
    }

    private async Task SaveSettingsAsync(bool silent = false)
    {
        _settings.ObsPath = ObsPathBox.Text?.Trim() ?? string.Empty;
        _settings.RecoveryProtection = ProtectionToggle.IsChecked == true;
        _settings.RelaunchAfterHungRecovery = RelaunchToggle.IsChecked == true;
        _settings.StartWithOperatingSystem = StartAtLoginToggle.IsChecked == true;
        _settings.CheckUpdatesOnline = OnlineUpdatesToggle.IsChecked == true;
        _settings.AutoBackupBeforeRestore = AutoBackupRestoreToggle.IsChecked == true;
        _settings.RunBandwidthTestInPreflight = RunBandwidthInPreflightToggle.IsChecked == true;
        _settings.PreferredStreamingPlatform = SelectedStreamingPlatform().ToString();
        _settings.PreferredMotionProfile = SelectedMotionProfile().ToString();
        _settings.TwitchEnhancedBroadcasting = BandwidthEnhancedToggle.IsChecked == true;
        _settings.TwitchServerSideTranscode = BandwidthServerTranscodeToggle.IsChecked == true;
        if (int.TryParse(BackupAgeBox.Text, out var days)) _settings.BackupWarningAgeDays = days;
        if (double.TryParse(DiskWarningBox.Text, out var gb)) _settings.RecordingDiskWarningGb = gb;
        _settings.BackupDirectory = BackupDirectoryBox.Text?.Trim() ?? string.Empty;
        await _settingsService.SaveAsync(_settings);
        if (!silent) _logger.Success("Settings saved.");
    }

    private void AutoDetect()
    {
        var path = _platform.FindObsInstall();
        if (path is null) _logger.Warn("OBS Studio was not automatically detected.");
        else
        {
            ObsPathBox.Text = path;
            _logger.Success($"OBS detected: {path}");
        }
    }

    private async Task ConfirmAndRestartAsync(bool safeMode)
    {
        if (await ConfirmAsync("Restart OBS?", "OBS will be closed and launched again. Unsaved OBS settings could be lost."))
            await RunActionAsync(() => _recovery.RestartAsync(safeMode));
    }

    private async Task LaunchSafeModeAsync()
    {
        if (IsObsRunning()) await ConfirmAndRestartAsync(true);
        else await RunActionAsync(() => _recovery.LaunchAsync(true));
    }

    private async Task ConfirmAndForceCloseAsync()
    {
        if (await ConfirmAsync("Force close OBS?", "This immediately terminates OBS. Unsaved OBS settings could be lost."))
            await RunActionAsync(() => _recovery.ForceCloseAllAsync());
    }

    private bool IsObsRunning()
    {
        var processes = _platform.GetObsProcesses();
        try { return processes.Count > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        try
        {
            await action();
            await MonitorTickAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            await ShowErrorAsync(ex.Message);
        }
    }

    private async Task WithBusyAsync(Button button, string busyText, ProgressBar progressBar, TextBlock progressText, string activity, Func<Task> action)
    {
        var original = button.Content;
        button.IsEnabled = false;
        button.Content = busyText;
        progressBar.IsVisible = true;
        progressText.Text = activity;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            progressText.Text = "Operation stopped with an error.";
            await ShowErrorAsync(ex.Message);
        }
        finally
        {
            progressBar.IsVisible = false;
            button.Content = original;
            button.IsEnabled = true;
        }
    }

    private void TryAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { _logger.Error(ex.Message); }
    }

    private void OpenUrl(string url) => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    private void AddHistory(string line)
    {
        _history.Insert(0, line);
        while (_history.Count > 150) _history.RemoveAt(_history.Count - 1);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var yes = new Button { Content = "Continue", MinWidth = 100 };
        yes.Classes.Add("primary");
        var no = new Button { Content = "Cancel", MinWidth = 100 };
        var result = false;
        var dialog = new Window { Title = title, Width = 500, Height = 240, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(26),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.Bold },
                new TextBlock { Text = message, Foreground = Brush.Parse("#A9AFBC"), TextWrapping = TextWrapping.Wrap },
                new WrapPanel { ItemSpacing = 10, Children = { yes, no } }
            }
        };
        await dialog.ShowDialog(this);
        return result;
    }

    private Task ShowErrorAsync(string message) => ShowInfoAsync("OBS Ground Control", message);

    private async Task ShowInfoAsync(string title, string message)
    {
        var close = new Button { Content = "Close", MinWidth = 100 };
        var dialog = new Window { Title = title, Width = 580, Height = 320, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(26),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeight.Bold },
                new ScrollViewer { MaxHeight = 190, Content = new TextBlock { Text = message, Foreground = Brush.Parse("#A9AFBC"), TextWrapping = TextWrapping.Wrap } },
                close
            }
        };
        await dialog.ShowDialog(this);
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex) { _logger.Error($"Could not open folder: {ex.Message}"); }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown) return;
        if (_settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void PrepareForShutdown() => _allowClose = true;
    public void LaunchObsFromTray() => _ = RunActionAsync(() => _recovery.LaunchAsync());
    public Task RestartObsFromTrayAsync() => RunActionAsync(() => _recovery.RestartAsync());
}
