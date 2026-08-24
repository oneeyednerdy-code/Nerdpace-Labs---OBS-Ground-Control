using System.Text.Json;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SceneAssetScannerService
{
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg",
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v", ".ts",
        ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".opus"
    };

    public SceneAssetScannerService(IObsPlatformService platform, LoggingService logger)
    {
        _platform = platform;
        _logger = logger;
    }

    // Diagnostics remains broad: any missing rooted local file reference with an extension.
    public IReadOnlyList<MissingAsset> Scan(int limit = 250)
    {
        var results = new List<MissingAsset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sceneCollection, value) in EnumerateSceneStrings())
        {
            if (results.Count >= limit) break;
            var path = NormalizeLocalPath(value, mediaOnly: false);
            if (path is null || !seen.Add(path) || File.Exists(path) || Directory.Exists(path)) continue;
            results.Add(new MissingAsset(sceneCollection, path));
        }
        return results;
    }

    // Backups are intentionally narrower: only common image/audio/video files are copied.
    public IReadOnlyList<SceneMediaReference> ScanMediaReferences(int limit = 10000)
    {
        var results = new List<SceneMediaReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sceneCollection, value) in EnumerateSceneStrings())
        {
            if (results.Count >= limit) break;
            var path = NormalizeLocalPath(value, mediaOnly: true);
            if (path is null || !seen.Add(path)) continue;
            var exists = File.Exists(path);
            long size = 0;
            if (exists)
            {
                try { size = new FileInfo(path).Length; } catch { }
            }
            results.Add(new SceneMediaReference(sceneCollection, path, exists, size));
        }
        return results;
    }

    public SceneMediaEstimate EstimateMedia()
    {
        var refs = ScanMediaReferences();
        return new SceneMediaEstimate(
            refs.Count(x => x.Exists),
            refs.Where(x => x.Exists).Sum(x => x.SizeBytes),
            refs.Count(x => !x.Exists));
    }

    private IEnumerable<(string SceneCollection, string Value)> EnumerateSceneStrings()
    {
        var sceneDir = Path.Combine(_platform.GetObsConfigDirectory(), "basic", "scenes");
        if (!Directory.Exists(sceneDir)) yield break;
        foreach (var file in Directory.EnumerateFiles(sceneDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(File.ReadAllText(file)); }
            catch (Exception ex)
            {
                _logger.Warn($"Could not inspect scene collection {Path.GetFileName(file)}: {ex.Message}");
            }
            if (doc is null) continue;
            using (doc)
            {
                var sceneCollection = Path.GetFileNameWithoutExtension(file);
                foreach (var value in EnumerateStrings(doc.RootElement))
                    yield return (sceneCollection, value);
            }
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value)) yield return value;
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    foreach (var child in EnumerateStrings(property.Value)) yield return child;
                break;
            case JsonValueKind.Array:
                foreach (var childElement in element.EnumerateArray())
                    foreach (var child in EnumerateStrings(childElement)) yield return child;
                break;
        }
    }

    private static string? NormalizeLocalPath(string value, bool mediaOnly)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile) trimmed = uri.LocalPath;
        if (!(Path.IsPathRooted(trimmed) || trimmed.StartsWith("\\\\"))) return null;
        var extension = Path.GetExtension(trimmed);
        if (string.IsNullOrWhiteSpace(extension)) return null;
        if (mediaOnly && !MediaExtensions.Contains(extension)) return null;
        try { return Path.GetFullPath(trimmed); } catch { return null; }
    }
}
