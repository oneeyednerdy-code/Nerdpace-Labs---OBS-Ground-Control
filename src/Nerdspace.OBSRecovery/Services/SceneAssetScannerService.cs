using System.Text.Json;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SceneAssetScannerService
{
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;

    public SceneAssetScannerService(IObsPlatformService platform, LoggingService logger)
    {
        _platform = platform;
        _logger = logger;
    }

    public IReadOnlyList<MissingAsset> Scan(int limit = 250)
    {
        var sceneDir = Path.Combine(_platform.GetObsConfigDirectory(), "basic", "scenes");
        if (!Directory.Exists(sceneDir)) return Array.Empty<MissingAsset>();
        var results = new List<MissingAsset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(sceneDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var value in EnumerateStrings(doc.RootElement))
                {
                    if (results.Count >= limit) return results;
                    var path = NormalizeLocalPath(value);
                    if (path is null || !seen.Add(path) || File.Exists(path) || Directory.Exists(path)) continue;
                    results.Add(new MissingAsset(Path.GetFileNameWithoutExtension(file), path));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not inspect scene collection {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return results;
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

    private static string? NormalizeLocalPath(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
            trimmed = uri.LocalPath;

        var rooted = Path.IsPathRooted(trimmed) || trimmed.StartsWith("\\\\");
        if (!rooted) return null;

        var extension = Path.GetExtension(trimmed);
        if (string.IsNullOrWhiteSpace(extension)) return null;
        try { return Path.GetFullPath(trimmed); }
        catch { return null; }
    }
}
