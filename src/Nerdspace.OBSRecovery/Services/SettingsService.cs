using System.Text.Json;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SettingsService
{
    private readonly IObsPlatformService _platform;
    private readonly string _settingsPath;

    public SettingsService(IObsPlatformService platform)
    {
        _platform = platform;
        Directory.CreateDirectory(_platform.GetSettingsDirectory());
        _settingsPath = Path.Combine(_platform.GetSettingsDirectory(), "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)) ?? new();
                ApplyDefaults(settings);
                return settings;
            }
        }
        catch { }

        var defaults = new AppSettings();
        ApplyDefaults(defaults);
        SaveAsync(defaults).GetAwaiter().GetResult();
        return defaults;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ApplyDefaults(settings);
        Directory.CreateDirectory(_platform.GetSettingsDirectory());
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json, cancellationToken);
        await _platform.ConfigureStartupAsync(settings.StartWithOperatingSystem, cancellationToken);
    }

    private void ApplyDefaults(AppSettings settings)
    {
        settings.UpdateDeferrals ??= new Dictionary<string, Nerdspace.OBSRecovery.Models.UpdateDeferral>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(settings.ObsPath)) settings.ObsPath = _platform.FindObsInstall() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(settings.BackupDirectory)) settings.BackupDirectory = Path.Combine(_platform.GetSettingsDirectory(), "Backups");
        settings.HungThresholdSeconds = Math.Clamp(settings.HungThresholdSeconds, 5, 120);
        settings.StuckShutdownThresholdSeconds = Math.Clamp(settings.StuckShutdownThresholdSeconds, 5, 120);
        settings.BackupWarningAgeDays = Math.Clamp(settings.BackupWarningAgeDays, 1, 365);
        settings.RecordingDiskWarningGb = Math.Clamp(settings.RecordingDiskWarningGb, 1, 5000);
        settings.WindowWidth = Math.Clamp(settings.WindowWidth <= 0 ? 1060 : settings.WindowWidth, 760, 2400);
        settings.WindowHeight = Math.Clamp(settings.WindowHeight <= 0 ? 700 : settings.WindowHeight, 540, 1600);
        settings.MinimizeToTrayOnClose = false;
        if (!Enum.TryParse<Nerdspace.OBSRecovery.Models.StreamingPlatform>(settings.PreferredStreamingPlatform, out _))
            settings.PreferredStreamingPlatform = Nerdspace.OBSRecovery.Models.StreamingPlatform.Twitch.ToString();
        if (!Enum.TryParse<Nerdspace.OBSRecovery.Models.MotionProfile>(settings.PreferredMotionProfile, out _))
            settings.PreferredMotionProfile = Nerdspace.OBSRecovery.Models.MotionProfile.Balanced.ToString();
    }
}
