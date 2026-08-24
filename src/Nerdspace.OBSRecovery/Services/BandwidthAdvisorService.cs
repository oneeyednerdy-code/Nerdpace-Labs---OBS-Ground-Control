using System.Diagnostics;
using System.Net.Http.Headers;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class BandwidthAdvisorService
{
    public const string CloudflareSpeedTestUrl = "https://speed.cloudflare.com/";
    public const string CloudflareUploadEndpoint = "https://speed.cloudflare.com/__up";
    private readonly LoggingService _logger;
    private readonly HttpClient _http;

    public BandwidthAdvisorService(LoggingService logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nerdspace-OBS-Ground-Control", AppVersion.DisplayVersion.TrimStart('v')));
    }

    public async Task<BandwidthTestResult> RunAsync(
        StreamingPlatform platform,
        MotionProfile motion,
        bool twitchEnhancedBroadcasting,
        bool twitchServerSideTranscode,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // A short warm-up keeps DNS/TLS setup out of the measured samples.
        progress?.Report("Warming up the upload test…");
        await UploadOnceAsync(256 * 1024, cancellationToken);

        // ~25 MB total measured upload. Large enough for useful sustained samples without being excessive.
        var sizes = new[] { 1, 4, 8, 12 }.Select(mb => mb * 1024 * 1024).ToArray();
        var samples = new List<BandwidthSample>();
        var uploaded = 0d;

        for (var i = 0; i < sizes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Measuring sustained upload • sample {i + 1} of {sizes.Length}…");
            var seconds = await UploadOnceAsync(sizes[i], cancellationToken);
            var mbps = sizes[i] * 8d / Math.Max(seconds, 0.001) / 1_000_000d;
            samples.Add(new BandwidthSample(i + 1, sizes[i], seconds, mbps));
            uploaded += sizes[i] / 1024d / 1024d;
        }

        var ordered = samples.Select(x => x.Mbps).OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
            return new BandwidthTestResult(false, samples, 0, 0, 0, 0, uploaded, "Bandwidth test failed", "No valid upload samples were produced.", null);

        var average = ordered.Average();
        var peak = ordered.Max();
        // Lower-quartile-ish stable value: deliberately favors reliability without allowing one transient outlier to dominate.
        var stable = ordered.Length >= 4 ? (ordered[0] + ordered[1]) / 2d : ordered[0];
        var variation = average <= 0 ? 0 : (peak - ordered.Min()) / average * 100d;
        var recommendation = Recommend(stable, platform, motion, twitchEnhancedBroadcasting, twitchServerSideTranscode, variation);

        var status = recommendation.SafeBudgetMbps < 1.2
            ? "Upload is too constrained for a reliable mainstream live stream"
            : variation >= 45
                ? "Upload speed is usable but inconsistent"
                : "Bandwidth Advisor completed";

        var detail = "Mission Control uses the conservative stable sample, not the fastest sample. The safe stream budget is stable upload divided by four, leaving substantial headroom for games, voice chat, browser sources, other devices, and normal ISP variation. The Cloudflare upload endpoint receives generated test bytes only; no personal files are uploaded.";
        _logger.Info($"Bandwidth Advisor: stable={stable:F1} Mbps peak={peak:F1} Mbps budget={recommendation.SafeBudgetMbps:F1} Mbps recommendation={recommendation.Headline}");
        return new BandwidthTestResult(true, samples, average, stable, peak, variation, uploaded, status, detail, recommendation);
    }

    public BandwidthRecommendation Recommend(
        double stableUploadMbps,
        StreamingPlatform platform,
        MotionProfile motion,
        bool twitchEnhancedBroadcasting,
        bool twitchServerSideTranscode,
        double variationPercent = 0)
    {
        var safeBudget = Math.Max(0, stableUploadMbps / 4d);
        var audioKbps = platform == StreamingPlatform.YouTube ? 128 : 160;
        var availableVideoKbps = Math.Max(300, (int)Math.Floor((safeBudget * 1000d - audioKbps - 100d) / 100d) * 100);

        var candidates = BuildCandidates(platform, twitchEnhancedBroadcasting, twitchServerSideTranscode)
            .Where(x => x.RequiredBudgetKbps <= availableVideoKbps)
            .ToList();

        StreamTarget target;
        if (candidates.Count == 0)
        {
            target = new StreamTarget("480p", 30, Math.Min(1200, availableVideoKbps), Math.Min(1200, availableVideoKbps), "H.264", 1);
        }
        else
        {
            target = SelectByMotion(candidates, motion);
        }

        var confidence = variationPercent switch
        {
            >= 60 => "LOW — upload varied heavily during the scan",
            >= 35 => "FAIR — keep extra headroom and test before going live",
            _ when stableUploadMbps < 6 => "FAIR — limited upload headroom",
            _ => "GOOD — conservative headroom retained"
        };

        var platformNote = platform switch
        {
            StreamingPlatform.Twitch when twitchEnhancedBroadcasting && target.Resolution == "1440p" =>
                twitchServerSideTranscode
                    ? "Twitch 2K requires Enhanced Broadcasting. Current Twitch guidance lists roughly 9 Mbps upstream when added server-side transcode support is available."
                    : "Twitch 2K requires Enhanced Broadcasting. Without added server-side transcode support, current Twitch guidance lists substantially more upstream bandwidth; Mission Control will avoid 1440p unless the conservative budget supports it.",
            StreamingPlatform.Twitch when twitchEnhancedBroadcasting =>
                "Enhanced Broadcasting can automatically vary the quality ladder and total bandwidth. Mission Control treats its result as a safe maximum budget, not a requirement to manually force every track.",
            StreamingPlatform.Twitch =>
                "Standard Twitch recommendation favors a conservative single-stream H.264 profile. Enable Enhanced Broadcasting in the advisor if you use Twitch multitrack/2K features.",
            StreamingPlatform.YouTube =>
                "YouTube publishes bitrate guidance by resolution and frame rate. Mission Control chooses the highest profile that fits inside the /4 safe upload budget.",
            _ => "Generic recommendation prioritizes stability and leaves substantial upload headroom."
        };

        var rationale = motion switch
        {
            MotionProfile.HighMotion => "High-motion content prioritizes 60 FPS and may choose a lower resolution to preserve motion quality.",
            MotionProfile.LowMotion => "Low-motion content prioritizes image detail and may choose 30 FPS at a higher resolution.",
            _ => "Balanced content weighs resolution and frame rate evenly."
        };

        return new BandwidthRecommendation(platform, motion, stableUploadMbps, safeBudget, target.BitrateKbps, audioKbps, target.Resolution, target.Fps, target.Codec, confidence, platformNote, rationale);
    }

    public void OpenCloudflareSpeedTest() => Process.Start(new ProcessStartInfo { FileName = CloudflareSpeedTestUrl, UseShellExecute = true });

    private async Task<double> UploadOnceAsync(int bytes, CancellationToken cancellationToken)
    {
        var payload = new byte[bytes];
        Random.Shared.NextBytes(payload.AsSpan(0, Math.Min(payload.Length, 4096)));
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var sw = Stopwatch.StartNew();
        using var response = await _http.PostAsync(CloudflareUploadEndpoint, content, cancellationToken);
        sw.Stop();
        response.EnsureSuccessStatusCode();
        return sw.Elapsed.TotalSeconds;
    }

    private static List<StreamTarget> BuildCandidates(StreamingPlatform platform, bool enhanced, bool serverTranscode)
    {
        if (platform == StreamingPlatform.YouTube)
        {
            // YouTube H.264 live recommendations (Mbps) represented in Kbps.
            return new()
            {
                new("480p", 30, 2500, 2500, "H.264", 1),
                new("720p", 30, 4000, 4000, "H.264", 2),
                new("720p", 60, 6000, 6000, "H.264", 2),
                new("1080p", 30, 10000, 10000, "H.264", 3),
                new("1080p", 60, 12000, 12000, "H.264", 3),
                new("1440p", 30, 15000, 15000, "H.264", 4),
                new("1440p", 60, 24000, 24000, "H.264", 4)
            };
        }

        if (platform == StreamingPlatform.Twitch)
        {
            var list = new List<StreamTarget>
            {
                new("480p", 30, 1500, 1500, "H.264", 1),
                new("720p", 30, 3000, 3000, "H.264", 2),
                new("720p", 60, 4500, 4500, "H.264", 2),
                new("1080p", 30, 5000, 5000, "H.264", 3),
                new("1080p", 60, 6000, 6000, "H.264", 3)
            };
            if (enhanced)
            {
                // 2K requires Enhanced Broadcasting; use HEVC recommendation only when safe budget clears Twitch's upstream requirement.
                var requiredBudgetKbps = serverTranscode ? 9000 : 20000;
                list.Add(new("1440p", 60, 9000, requiredBudgetKbps, "HEVC / Enhanced Broadcasting", 4));
            }
            return list;
        }

        return new()
        {
            new("480p", 30, 1500, 1500, "H.264", 1),
            new("720p", 30, 3000, 3000, "H.264", 2),
            new("720p", 60, 4500, 4500, "H.264", 2),
            new("1080p", 30, 5000, 5000, "H.264", 3),
            new("1080p", 60, 6500, 6500, "H.264", 3),
            new("1440p", 30, 9000, 9000, "H.264", 4),
            new("1440p", 60, 12000, 12000, "H.264", 4)
        };
    }

    private static StreamTarget SelectByMotion(List<StreamTarget> candidates, MotionProfile motion)
    {
        if (motion == MotionProfile.HighMotion)
            return candidates.OrderByDescending(x => x.Fps).ThenByDescending(x => x.ResolutionRank).First();
        if (motion == MotionProfile.LowMotion)
            return candidates.OrderByDescending(x => x.ResolutionRank).ThenBy(x => x.Fps).First();
        return candidates.OrderByDescending(x => x.ResolutionRank + (x.Fps >= 60 ? 0.35 : 0)).First();
    }

    private sealed record StreamTarget(string Resolution, int Fps, int BitrateKbps, int RequiredBudgetKbps, string Codec, double ResolutionRank);
}
