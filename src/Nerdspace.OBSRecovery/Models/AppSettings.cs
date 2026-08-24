namespace Nerdspace.OBSRecovery.Models;

public sealed class AppSettings
{
    public string ObsPath { get; set; } = string.Empty;
    public bool RecoveryProtection { get; set; } = true;
    public bool RelaunchAfterHungRecovery { get; set; } = true;
    // Retained for older settings-file compatibility. Close now always exits.
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public double WindowWidth { get; set; } = 1060;
    public double WindowHeight { get; set; } = 700;
    public bool StartWithOperatingSystem { get; set; }
    public int HungThresholdSeconds { get; set; } = 15;
    public int StuckShutdownThresholdSeconds { get; set; } = 12;

    public bool AutoBackupBeforeRestore { get; set; } = true;
    public bool PreflightCheckBackupAge { get; set; } = true;
    public int BackupWarningAgeDays { get; set; } = 14;
    public double RecordingDiskWarningGb { get; set; } = 25;
    public bool CheckUpdatesOnline { get; set; } = true;
    public string BackupDirectory { get; set; } = string.Empty;

    // Optional executable overrides for creator tools, especially portable installs.
    public string MixItUpPath { get; set; } = string.Empty;
    public string StreamerBotPath { get; set; } = string.Empty;
    public string FirebotPath { get; set; } = string.Empty;

    public string PreferredStreamingPlatform { get; set; } = nameof(StreamingPlatform.Twitch);
    public string PreferredMotionProfile { get; set; } = nameof(MotionProfile.Balanced);
    public bool TwitchEnhancedBroadcasting { get; set; }
    public bool TwitchServerSideTranscode { get; set; }
    public bool RunBandwidthTestInPreflight { get; set; }

    // Generic update deferrals keyed by a stable item id such as "plugin:aitum-vertical".
    public Dictionary<string, UpdateDeferral> UpdateDeferrals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
