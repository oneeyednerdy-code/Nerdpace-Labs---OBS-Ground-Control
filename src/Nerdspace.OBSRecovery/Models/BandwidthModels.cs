namespace Nerdspace.OBSRecovery.Models;

public enum StreamingPlatform
{
    Twitch,
    YouTube,
    Other
}

public enum MotionProfile
{
    LowMotion,
    Balanced,
    HighMotion
}

public sealed record BandwidthSample(int Number, long Bytes, double Seconds, double Mbps)
{
    public string Display => $"Sample {Number} • {Mbps:F1} Mbps upload • {Bytes / 1024d / 1024d:F1} MB • {Seconds:F2}s";
}

public sealed record BandwidthRecommendation(
    StreamingPlatform Platform,
    MotionProfile Motion,
    double StableUploadMbps,
    double SafeBudgetMbps,
    int VideoBitrateKbps,
    int AudioBitrateKbps,
    string Resolution,
    int Fps,
    string Codec,
    string Confidence,
    string PlatformNote,
    string Rationale)
{
    public string Headline => $"{Resolution} @ {Fps} FPS • {VideoBitrateKbps:N0} Kbps video • {AudioBitrateKbps} Kbps audio";
    public string BudgetLine => $"Stable upload {StableUploadMbps:F1} Mbps ÷ 4 = {SafeBudgetMbps:F1} Mbps conservative stream budget";
}

public sealed record BandwidthTestResult(
    bool Success,
    IReadOnlyList<BandwidthSample> Samples,
    double AverageUploadMbps,
    double StableUploadMbps,
    double PeakUploadMbps,
    double VariationPercent,
    double UploadedMegabytes,
    string Status,
    string Detail,
    BandwidthRecommendation? Recommendation)
{
    public string Summary => !Success || Recommendation is null
        ? Status
        : $"{Recommendation.Headline}\n{Recommendation.BudgetLine}\nConnection confidence: {Recommendation.Confidence}";
}

public sealed record PreflightRunOptions(
    bool SkipUpdateChecks,
    bool RunBandwidthTest,
    bool LaunchObsAfterReady,
    StreamingPlatform Platform,
    MotionProfile Motion,
    bool TwitchEnhancedBroadcasting,
    bool TwitchServerSideTranscode);
