using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class LogAnalyzerService
{
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;

    private static readonly Rule[] Rules =
    {
        new("Encoder", "encoder overloaded", CheckSeverity.Fail, "Encoder overload detected", "OBS reported encoder overload. Check GPU/CPU headroom, output settings, and scene complexity."),
        new("Network", "dropped frames", CheckSeverity.Warning, "Dropped-frame message detected", "OBS reported dropped frames. Review network stability and ingest selection."),
        new("GPU", "device removed", CheckSeverity.Fail, "Graphics device reset detected", "A graphics-device reset can interrupt capture or crash OBS."),
        new("Browser", "obs-browser", CheckSeverity.Info, "Browser-source activity", "Browser source entries were present in the log."),
        new("Plugin", "failed to load", CheckSeverity.Warning, "Module/plugin failed to load", "A module reported a load failure. Review nearby log lines and plugin compatibility."),
        new("Media", "failed to open", CheckSeverity.Warning, "Resource failed to open", "OBS could not open a resource. Missing or inaccessible media may be involved."),
        new("Audio", "max audio buffering", CheckSeverity.Warning, "Audio buffering warning", "OBS reported high audio buffering."),
        new("General", "error:", CheckSeverity.Warning, "OBS logged an error", "The latest OBS log contains one or more error entries.")
    };

    public LogAnalyzerService(IObsPlatformService platform, LoggingService logger)
    {
        _platform = platform;
        _logger = logger;
    }

    public string? GetLatestLogPath()
    {
        var dir = _platform.GetObsLogDirectory();
        if (!Directory.Exists(dir)) return null;
        return Directory.EnumerateFiles(dir, "*.txt", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public IReadOnlyList<DiagnosticFinding> AnalyzeLatest()
    {
        var path = GetLatestLogPath();
        if (path is null) return new[] { new DiagnosticFinding(CheckSeverity.Info, "Logs", "No OBS log found", "Launch OBS at least once to create a log.") };

        try
        {
            var counts = Rules.ToDictionary(r => r, _ => 0);
            foreach (var line in ReadTail(path, 8000))
            {
                foreach (var rule in Rules)
                    if (line.Contains(rule.Token, StringComparison.OrdinalIgnoreCase)) counts[rule]++;
            }

            var findings = counts.Where(kv => kv.Value > 0)
                .Select(kv => new DiagnosticFinding(kv.Key.Severity, kv.Key.Category, kv.Key.Summary, kv.Key.Detail, kv.Value))
                .OrderByDescending(x => x.Severity)
                .ToList();

            if (findings.Count == 0)
                findings.Add(new DiagnosticFinding(CheckSeverity.Pass, "Logs", "No known warning patterns found", "The latest OBS log did not contain the Mission Control patterns currently monitored."));
            return findings;
        }
        catch (Exception ex)
        {
            _logger.Warn($"OBS log analysis failed: {ex.Message}");
            return new[] { new DiagnosticFinding(CheckSeverity.Warning, "Logs", "Could not analyze the latest log", ex.Message) };
        }
    }

    private static IEnumerable<string> ReadTail(string path, int maxLines)
    {
        var queue = new Queue<string>(maxLines);
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (queue.Count == maxLines) queue.Dequeue();
            queue.Enqueue(line);
        }
        return queue;
    }

    private sealed record Rule(string Category, string Token, CheckSeverity Severity, string Summary, string Detail);
}
