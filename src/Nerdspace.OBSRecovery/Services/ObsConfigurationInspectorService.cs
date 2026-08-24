using System.Text.Json;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class ObsConfigurationInspectorService
{
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;

    public ObsConfigurationInspectorService(IObsPlatformService platform, LoggingService logger)
    {
        _platform = platform;
        _logger = logger;
    }

    public ObsOutputSnapshot Inspect()
    {
        try
        {
            var profileDir = ResolveProfileDirectory();
            if (profileDir is null)
                return new ObsOutputSnapshot(false, "Unknown", null, null, null, null, null, "Unknown", "No OBS profile directory could be identified.");

            var profileName = Path.GetFileName(profileDir);
            var ini = ReadIni(Path.Combine(profileDir, "basic.ini"));
            int? width = IntValue(ini, "Video", "OutputCX");
            int? height = IntValue(ini, "Video", "OutputCY");
            int? fps = ParseFps(ini);
            int? video = IntValue(ini, "SimpleOutput", "VBitrate");
            int? audio = IntValue(ini, "SimpleOutput", "ABitrate");
            var encoder = StringValue(ini, "SimpleOutput", "StreamEncoder") ?? "Unknown encoder";

            var streamEncoderJson = Path.Combine(profileDir, "streamEncoder.json");
            if (File.Exists(streamEncoderJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(streamEncoderJson));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("bitrate", out var bitrate) && bitrate.TryGetInt32(out var parsed)) video = parsed;
                    if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) encoder = id.GetString() ?? encoder;
                }
                catch { }
            }

            return new ObsOutputSnapshot(true, profileName, video, audio, width, height, fps, encoder,
                "Read-only snapshot of the most likely active/recent OBS profile. Ground Control does not change these settings automatically.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not inspect OBS output configuration: {ex.Message}");
            return new ObsOutputSnapshot(false, "Unknown", null, null, null, null, null, "Unknown", "OBS output configuration could not be read.");
        }
    }

    private string? ResolveProfileDirectory()
    {
        var profilesRoot = Path.Combine(_platform.GetObsConfigDirectory(), "basic", "profiles");
        if (!Directory.Exists(profilesRoot)) return null;

        var global = ReadIni(Path.Combine(_platform.GetObsConfigDirectory(), "global.ini"));
        var profileDirName = StringValue(global, "Basic", "ProfileDir");
        if (!string.IsNullOrWhiteSpace(profileDirName))
        {
            var direct = Path.Combine(profilesRoot, profileDirName);
            if (Directory.Exists(direct)) return direct;
        }

        var profileName = StringValue(global, "Basic", "Profile");
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var direct = Path.Combine(profilesRoot, profileName);
            if (Directory.Exists(direct)) return direct;
        }

        return Directory.EnumerateDirectories(profilesRoot)
            .Where(d => File.Exists(Path.Combine(d, "basic.ini")))
            .OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, "basic.ini")))
            .FirstOrDefault();
    }

    private static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;
        var section = string.Empty;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (!result.ContainsKey(section)) result[section] = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            if (!result.TryGetValue(section, out var values)) result[section] = values = new(StringComparer.OrdinalIgnoreCase);
            values[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return result;
    }

    private static string? StringValue(Dictionary<string, Dictionary<string, string>> ini, string section, string key)
        => ini.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value.Trim('"') : null;

    private static int? IntValue(Dictionary<string, Dictionary<string, string>> ini, string section, string key)
        => int.TryParse(StringValue(ini, section, key), out var value) ? value : null;

    private static int? ParseFps(Dictionary<string, Dictionary<string, string>> ini)
    {
        var common = StringValue(ini, "Video", "FPSCommon");
        if (!string.IsNullOrWhiteSpace(common))
        {
            var first = common.Split(new[] { ' ', '/', '.' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (int.TryParse(first, out var fps)) return fps;
        }
        var num = IntValue(ini, "Video", "FPSInt");
        if (num.HasValue) return num;
        var numerator = IntValue(ini, "Video", "FPSNum");
        var denominator = IntValue(ini, "Video", "FPSDen");
        if (numerator.HasValue && denominator is > 0) return (int)Math.Round(numerator.Value / (double)denominator.Value);
        return null;
    }
}
