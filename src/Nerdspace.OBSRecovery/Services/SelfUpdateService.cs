using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using NetSparkleUpdater;
using NetSparkleUpdater.AppCastHandlers;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Events;
using NetSparkleUpdater.SignatureVerifiers;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SelfUpdateService
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly LoggingService _logger;
    private readonly SelfUpdateFeedConfig _config;

    private SparkleUpdater? _sparkle;
    private AppCastItem? _selectedUpdate;
    private string? _activeFeedUrl;

    public event Action? CloseApplicationRequested;

    public SelfUpdateService(
        AppSettings settings,
        SettingsService settingsService,
        LoggingService logger)
    {
        _settings = settings;
        _settingsService = settingsService;
        _logger = logger;
        _config = LoadConfig();
    }

    public bool IsConfigured => _config.IsConfigured;

    public string RepositoryUrl => string.IsNullOrWhiteSpace(_config.Repository)
        ? string.Empty
        : $"https://github.com/{_config.Repository}";

    public async Task<SelfUpdateSnapshot> CheckAsync(
        bool userInitiated,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var channel = NormalizeChannel(_settings.MissionControlUpdateChannel);

        if (!_settings.CheckUpdatesOnline)
        {
            return Snapshot(
                configured: _config.IsConfigured,
                channel,
                latest: "Not checked",
                status: "Online update checks disabled",
                detail: "Enable online update checks in Settings to check Streamer Mission Control releases.",
                updateAvailable: false,
                canInstall: false,
                releaseUrl: null,
                checkedAt: _settings.LastMissionControlUpdateCheckUtc);
        }

        if (!_config.IsConfigured)
        {
            return Snapshot(
                configured: false,
                channel,
                latest: "Unavailable",
                status: "Self-update signing not configured",
                detail: "The application build does not contain a signed Mission Control update feed. Use View Releases or configure the NetSparkle signing keys in GitHub Actions.",
                updateAvailable: false,
                canInstall: false,
                releaseUrl: RepositoryUrl.Length > 0 ? $"{RepositoryUrl}/releases" : null,
                checkedAt: _settings.LastMissionControlUpdateCheckUtc);
        }

        if (!userInitiated &&
            _settings.MissionControlUpdateSnoozedUntilUtc is { } snooze &&
            snooze > DateTimeOffset.UtcNow)
        {
            return Snapshot(
                configured: true,
                channel,
                latest: "Deferred",
                status: "Reminder snoozed",
                detail: $"Automatic update reminders are snoozed until {snooze.ToLocalTime():g}. You can still use Check Now at any time.",
                updateAvailable: false,
                canInstall: false,
                releaseUrl: $"{RepositoryUrl}/releases",
                checkedAt: _settings.LastMissionControlUpdateCheckUtc);
        }

        try
        {
            var sparkle = EnsureSparkle(channel);

            _logger.Info($"Checking Streamer Mission Control {channel} update feed.");
            var info = await sparkle.CheckForUpdatesQuietly();
            cancellationToken.ThrowIfCancellationRequested();

            var checkedAt = DateTimeOffset.UtcNow;
            _settings.LastMissionControlUpdateCheckUtc = checkedAt;
            await _settingsService.SaveAsync(_settings);

            if (info is null)
            {
                _selectedUpdate = null;
                return Snapshot(
                    true,
                    channel,
                    "Unknown",
                    "Update check unavailable",
                    "NetSparkle did not return update information. No installer was downloaded or executed.",
                    false,
                    false,
                    $"{RepositoryUrl}/releases",
                    checkedAt);
            }

            switch (info.Status)
            {
                case UpdateStatus.UpdateAvailable:
                {
                    var item = info.Updates.FirstOrDefault();
                    if (item is null)
                    {
                        _selectedUpdate = null;
                        return Snapshot(
                            true,
                            channel,
                            "Unknown",
                            "Update metadata incomplete",
                            "The signed feed reported an update but did not provide an installable item.",
                            false,
                            false,
                            $"{RepositoryUrl}/releases",
                            checkedAt);
                    }

                    _selectedUpdate = item;
                    var latest = ReadItemVersion(item);
                    var releaseUrl = BuildReleaseUrl(latest);

                    return Snapshot(
                        true,
                        channel,
                        latest,
                        "Update available",
                        "A newer signed Streamer Mission Control installer is available. Update Now downloads it, verifies its Ed25519 signature, closes Mission Control, and hands off to the normal installer.",
                        true,
                        true,
                        releaseUrl,
                        checkedAt);
                }

                case UpdateStatus.UpdateNotAvailable:
                    _selectedUpdate = null;
                    return Snapshot(
                        true,
                        channel,
                        AppVersion.DisplayVersion,
                        "Up to date",
                        "No newer release is available on the selected update channel.",
                        false,
                        false,
                        $"{RepositoryUrl}/releases",
                        checkedAt);

                case UpdateStatus.UserSkipped:
                    _selectedUpdate = null;
                    return Snapshot(
                        true,
                        channel,
                        "Skipped",
                        "Update skipped",
                        "The currently offered release was previously skipped.",
                        false,
                        false,
                        $"{RepositoryUrl}/releases",
                        checkedAt);

                case UpdateStatus.CouldNotDetermine:
                    _selectedUpdate = null;
                    _logger.Warn(
                        $"NetSparkle could not fetch or validate the {channel} appcast from {_activeFeedUrl}.");
                    return Snapshot(
                        true,
                        channel,
                        "Unavailable",
                        "Update feed unavailable",
                        "The signed update feed could not be fetched or validated. Mission Control update assets must be anonymously readable. If the source repository is private, publish the installer and signed appcast from a separate public release repository. Nothing was downloaded or executed.",
                        false,
                        false,
                        $"{RepositoryUrl}/releases",
                        checkedAt);

                default:
                    _selectedUpdate = null;
                    _logger.Warn($"Unexpected NetSparkle update status: {info.Status}.");
                    return Snapshot(
                        true,
                        channel,
                        "Unknown",
                        $"Unexpected updater status: {info.Status}",
                        "Mission Control received an updater status it does not recognize. Nothing was downloaded or executed.",
                        false,
                        false,
                        $"{RepositoryUrl}/releases",
                        checkedAt);
            }
        }
        catch (Exception ex)
        {
            _selectedUpdate = null;
            _logger.Warn($"Mission Control self-update check failed: {ex.Message}");
            return Snapshot(
                true,
                channel,
                "Unavailable",
                "Update check failed",
                $"{ex.Message}\nNo installer was downloaded or executed.",
                false,
                false,
                RepositoryUrl.Length > 0 ? $"{RepositoryUrl}/releases" : null,
                DateTimeOffset.UtcNow);
        }
    }

    public async Task DownloadAndInstallAsync(
        IProgress<SelfUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_selectedUpdate is null)
            throw new InvalidOperationException("Check for a Mission Control update before choosing Update Now.");

        var sparkle = EnsureSparkle(NormalizeChannel(_settings.MissionControlUpdateChannel));
        var selected = _selectedUpdate;
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void DownloadStarted(AppCastItem item, string path)
            => progress?.Report(new SelfUpdateProgress("Downloading signed Mission Control installer"));

        void DownloadFinished(AppCastItem item, string path)
            => completion.TrySetResult(path);

        void DownloadError(AppCastItem item, string path, Exception exception)
            => completion.TrySetException(exception);

        void DownloadProgress(object sender, AppCastItem item, ItemDownloadProgressEventArgs e)
            => progress?.Report(new SelfUpdateProgress("Downloading signed Mission Control installer", e.ProgressPercentage));

        sparkle.DownloadStarted += DownloadStarted;
        sparkle.DownloadFinished += DownloadFinished;
        sparkle.DownloadHadError += DownloadError;
        sparkle.DownloadMadeProgress += DownloadProgress;

        try
        {
            progress?.Report(new SelfUpdateProgress("Preparing secure update download"));
            await sparkle.InitAndBeginDownload(selected);

            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));

            var downloadPath = await completion.Task;

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new SelfUpdateProgress("Signature verified • preparing installer"));

            void CloseRequested()
            {
                sparkle.CloseApplication -= CloseRequested;
                CloseApplicationRequested?.Invoke();
            }

            sparkle.CloseApplication += CloseRequested;
            sparkle.InstallUpdate(selected, downloadPath);
        }
        finally
        {
            sparkle.DownloadStarted -= DownloadStarted;
            sparkle.DownloadFinished -= DownloadFinished;
            sparkle.DownloadHadError -= DownloadError;
            sparkle.DownloadMadeProgress -= DownloadProgress;
        }
    }

    public async Task SnoozeAsync(TimeSpan duration)
    {
        _settings.MissionControlUpdateSnoozedUntilUtc = DateTimeOffset.UtcNow.Add(duration);
        await _settingsService.SaveAsync(_settings);
    }

    public void ClearSnooze()
    {
        _settings.MissionControlUpdateSnoozedUntilUtc = null;
    }

    public void OpenReleasePage(string? releaseUrl)
    {
        var url = !string.IsNullOrWhiteSpace(releaseUrl)
            ? releaseUrl
            : RepositoryUrl.Length > 0
                ? $"{RepositoryUrl}/releases"
                : string.Empty;

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("The Mission Control GitHub repository has not been configured for this build.");

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private SparkleUpdater EnsureSparkle(string channel)
    {
        var feedUrl = channel.Equals("Stable", StringComparison.OrdinalIgnoreCase)
            ? _config.StableFeedUrl
            : _config.PreviewFeedUrl;

        if (_sparkle is not null &&
            string.Equals(_activeFeedUrl, feedUrl, StringComparison.OrdinalIgnoreCase))
            return _sparkle;

        _sparkle = new SparkleUpdater(
            feedUrl,
            new Ed25519Checker(SecurityMode.Strict, _config.PublicKey))
        {
            UIFactory = null,
            RelaunchAfterUpdate = false
        };

        _activeFeedUrl = feedUrl;
        return _sparkle;
    }

    private SelfUpdateSnapshot Snapshot(
        bool configured,
        string channel,
        string latest,
        string status,
        string detail,
        bool updateAvailable,
        bool canInstall,
        string? releaseUrl,
        DateTimeOffset? checkedAt)
        => new(
            configured,
            channel,
            AppVersion.DisplayVersion,
            latest,
            status,
            detail,
            updateAvailable,
            canInstall,
            releaseUrl,
            checkedAt);

    private string BuildReleaseUrl(string version)
    {
        var clean = version.Trim();
        if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            clean = clean[1..];

        return string.IsNullOrWhiteSpace(clean) || clean.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? $"{RepositoryUrl}/releases"
            : $"{RepositoryUrl}/releases/tag/v{clean}";
    }

    private static string ReadItemVersion(AppCastItem item)
    {
        // NetSparkle's AppCastItem shape has evolved over time. Keep the UI resilient
        // by reading known version-like properties instead of coupling display code
        // to one property name. The strongly typed AppCastItem is still used for
        // secure download/install operations.
        foreach (var propertyName in new[] { "Version", "ShortVersion", "Title" })
        {
            var property = item.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            var value = property?.GetValue(item)?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value!;
        }

        return "Unknown";
    }

    private static string NormalizeChannel(string? value)
        => string.Equals(value, "Stable", StringComparison.OrdinalIgnoreCase)
            ? "Stable"
            : "Preview";

    private static SelfUpdateFeedConfig LoadConfig()
    {
        try
        {
            var assembly = typeof(SelfUpdateService).Assembly;
            var resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith("Data.update-config.json", StringComparison.OrdinalIgnoreCase));

            if (resource is null)
                return EmptyConfig();

            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
                return EmptyConfig();

            return JsonSerializer.Deserialize<SelfUpdateFeedConfig>(
                       stream,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? EmptyConfig();
        }
        catch
        {
            return EmptyConfig();
        }
    }

    private static SelfUpdateFeedConfig EmptyConfig()
        => new(string.Empty, string.Empty, string.Empty, string.Empty);
}
