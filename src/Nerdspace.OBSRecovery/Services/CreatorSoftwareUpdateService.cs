using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

/// <summary>
/// Detects creator-side streaming bots on Windows and compares installed executable
/// versions only with official stable release sources. It never downloads or installs updates.
/// </summary>
public sealed class CreatorSoftwareUpdateService
{
    public const string MixItUpRepository = "MixItUpBot/Desktop";
    public const string MixItUpReleasesUrl = "https://github.com/MixItUpBot/Desktop/releases";
    public const string MixItUpDownloadUrl = "https://mixitup.bot";

    public const string StreamerBotDownloadUrl = "https://streamer.bot/downloads";

    public const string FirebotRepository = "crowbartools/Firebot";
    public const string FirebotReleasesUrl = "https://github.com/crowbartools/Firebot/releases";

    private readonly AppSettings _settings;
    private readonly UpdateService _updates;
    private readonly LoggingService _logger;
    private readonly HttpClient _http = new();

    public CreatorSoftwareUpdateService(AppSettings settings, UpdateService updates, LoggingService logger)
    {
        _settings = settings;
        _updates = updates;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Nerdspace-OBS-Ground-Control", AppVersion.Version));
    }

    public Task<CreatorSoftwareUpdateSnapshot> InspectMixItUpLocalAsync(CancellationToken ct = default)
        => CheckMixItUpAsync(false, ct);

    public Task<CreatorSoftwareUpdateSnapshot> InspectStreamerBotLocalAsync(CancellationToken ct = default)
        => CheckStreamerBotAsync(false, ct);

    public Task<CreatorSoftwareUpdateSnapshot> InspectFirebotLocalAsync(CancellationToken ct = default)
        => CheckFirebotAsync(false, ct);

    public async Task<CreatorSoftwareUpdateSnapshot> CheckMixItUpAsync(bool checkOnline = true, CancellationToken ct = default)
    {
        var executable = FindMixItUpExecutable();
        if (string.IsNullOrWhiteSpace(executable))
            return NotDetected("mixitup", "Mix It Up", MixItUpDownloadUrl,
                "Mix It Up was not found in Windows install records, running processes, standard locations, or common portable folders. If you use the ZIP version in a custom folder, set MixItUp.exe in Settings.");

        return await CheckGitHubBackedAsync(
            "mixitup", "Mix It Up", executable, MixItUpRepository, MixItUpReleasesUrl,
            "Official GitHub Releases: MixItUpBot/Desktop", checkOnline, ct);
    }

    public async Task<CreatorSoftwareUpdateSnapshot> CheckFirebotAsync(bool checkOnline = true, CancellationToken ct = default)
    {
        var executable = FindFirebotExecutable();
        if (string.IsNullOrWhiteSpace(executable))
            return NotDetected("firebot", "Firebot", FirebotReleasesUrl,
                "Firebot was not found in Windows install records, running processes, standard Electron install locations, or common user folders. Set the Firebot executable path in Settings if it lives somewhere custom.");

        return await CheckGitHubBackedAsync(
            "firebot", "Firebot", executable, FirebotRepository, FirebotReleasesUrl,
            "Official GitHub Releases: crowbartools/Firebot", checkOnline, ct);
    }

    public async Task<CreatorSoftwareUpdateSnapshot> CheckStreamerBotAsync(bool checkOnline = true, CancellationToken ct = default)
    {
        var executable = FindStreamerBotExecutable();
        if (string.IsNullOrWhiteSpace(executable))
            return NotDetected("streamerbot", "Streamer.bot", StreamerBotDownloadUrl,
                "Streamer.bot is portable and can be extracted anywhere. Mission Control checked running processes and common user folders but did not find Streamer.bot.exe. Set its executable path in Settings for guaranteed detection.");

        var installed = ReadExecutableVersion(executable);
        if (!checkOnline)
            return LocalOnly("streamerbot", "Streamer.bot", executable, installed, StreamerBotDownloadUrl,
                "Official Streamer.bot stable Downloads service");

        var latest = await GetLatestStreamerBotStableVersionAsync(ct);
        var status = UpdateService.Compare(installed, latest);
        return new(
            "streamerbot", "Streamer.bot", true, installed, latest, status,
            ExplainStatus(status,
                "Latest stable version comes from the official Streamer.bot Downloads service. Streamer.bot also has its own built-in updater; Mission Control only reports status and opens the official destination."),
            executable, StreamerBotDownloadUrl, "Official Streamer.bot stable Downloads service");
    }

    private async Task<CreatorSoftwareUpdateSnapshot> CheckGitHubBackedAsync(
        string id, string name, string executable, string repository, string fallbackUrl,
        string source, bool checkOnline, CancellationToken ct)
    {
        var installed = ReadExecutableVersion(executable);
        if (!checkOnline)
            return LocalOnly(id, name, executable, installed, fallbackUrl, source);

        var release = await _updates.GetLatestGitHubReleaseAsync(repository, ct);
        var latest = release?.Version ?? "Unavailable";
        var status = UpdateService.Compare(installed, latest);

        return new(
            id, name, true, installed, latest, status,
            ExplainStatus(status,
                $"Latest stable version comes from {source}. Mission Control never installs or replaces {name} silently."),
            executable, release?.ReleaseUrl ?? fallbackUrl, source);
    }

    private static CreatorSoftwareUpdateSnapshot NotDetected(
        string id, string name, string url, string detail)
        => new(id, name, false, "Not detected", "Not checked", "Not detected automatically",
            detail, string.Empty, url, "Official source");

    private static CreatorSoftwareUpdateSnapshot LocalOnly(
        string id, string name, string executable, string installed, string url, string source)
        => new(id, name, true, installed, "Not checked", "Installed",
            $"Detected at {SanitizeUserPath(executable)}. Online update lookup was not requested or is disabled.",
            executable, url, source);

    private static string ExplainStatus(string status, string evidence) => status switch
    {
        "Update available" => $"A newer stable release is available. {evidence}",
        "Current" => $"Installed version matches the latest stable release. {evidence}",
        "Newer than catalog" => $"This installation is newer than the public stable release. Mission Control will not recommend a downgrade. {evidence}",
        "Version unknown" => $"Mission Control could not make a reliable version comparison. {evidence}",
        _ => $"Manual verification is recommended. {evidence}"
    };

    private async Task<string> GetLatestStreamerBotStableVersionAsync(CancellationToken ct)
    {
        try
        {
            var html = await _http.GetStringAsync(StreamerBotDownloadUrl, ct);

            // The official page currently renders a stable card such as:
            // "Streamer.bot stable" followed by "v1.0.4".
            var stable = Regex.Match(
                html,
                @"Streamer\.bot\s*(?:</?[^>]+>\s*)*stable.*?v(?<v>\d+\.\d+\.\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                TimeSpan.FromSeconds(2));
            if (stable.Success)
                return UpdateService.NormalizeVersion(stable.Groups["v"].Value);

            // Fallback to official Windows stable filenames in the page source.
            var matches = Regex.Matches(
                html,
                @"Streamer\.bot-x64-(?<v>\d+\.\d+\.\d+(?:\.\d+)?)\.zip",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(2));

            var candidates = matches
                .Select(m => m.Groups["v"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(v => (Text: v, Version: ParseComparableVersion(v)))
                .Where(x => x.Version is not null)
                .OrderByDescending(x => x.Version)
                .ToList();

            return candidates.Count > 0 ? candidates[0].Text : "Unavailable";
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not check Streamer.bot stable version: {ex.Message}");
            return "Unavailable";
        }
    }

    private string? FindMixItUpExecutable()
    {
        var names = new[] { "MixItUp.exe" };
        var candidates = new List<string?>
        {
            _settings.MixItUpPath,
            TryFindRunningProcessPath("MixItUp"),
            FindFromUninstallRegistry(new[] { "Mix It Up", "MixItUp" }, names),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MixItUp", "MixItUp.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mix It Up", "MixItUp.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mix It Up", "MixItUp.exe")
        };
        candidates.AddRange(FindCommonUserCandidates(names, new[] { "mixitup", "mix it up" }));
        return FirstExisting(candidates);
    }

    private string? FindStreamerBotExecutable()
    {
        var names = new[] { "Streamer.bot.exe" };
        var candidates = new List<string?>
        {
            _settings.StreamerBotPath,
            TryFindRunningProcessPath("Streamer.bot"),
            TryFindRunningProcessPath("StreamerBot"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Streamer.bot", "Streamer.bot.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Streamer.bot", "Streamer.bot.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Streamer.bot", "Streamer.bot.exe"),
            Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Streamer.bot", "Streamer.bot.exe")
        };
        candidates.AddRange(FindCommonUserCandidates(names, new[] { "streamer.bot", "streamerbot" }));
        return FirstExisting(candidates);
    }

    private string? FindFirebotExecutable()
    {
        var names = new[] { "Firebot v5.exe", "Firebot.exe", "firebot.exe" };
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string?>
        {
            _settings.FirebotPath,
            TryFindRunningProcessPath("Firebot v5"),
            TryFindRunningProcessPath("Firebot"),
            FindFromUninstallRegistry(new[] { "Firebot", "Firebot v5" }, names),
            Path.Combine(local, "Programs", "firebot-v5", "Firebot v5.exe"),
            Path.Combine(local, "Programs", "Firebot v5", "Firebot v5.exe"),
            Path.Combine(local, "Firebot", "Firebot v5.exe")
        };
        candidates.AddRange(FindCommonUserCandidates(names, new[] { "firebot", "firebot v5" }));
        candidates.AddRange(FindLocalProgramsCandidates(names, "firebot"));
        return FirstExisting(candidates);
    }

    private static IEnumerable<string?> FindCommonUserCandidates(string[] executableNames, string[] directoryHints)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var exe in executableNames)
            {
                var direct = Path.Combine(root, exe);
                if (File.Exists(direct)) yield return direct;
            }

            string[] children;
            try { children = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly).Take(300).ToArray(); }
            catch { continue; }

            foreach (var directory in children)
            {
                var name = Path.GetFileName(directory);
                if (!directoryHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var exe in executableNames)
                    yield return Path.Combine(directory, exe);
            }
        }
    }

    private static IEnumerable<string?> FindLocalProgramsCandidates(string[] executableNames, string directoryHint)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        if (!Directory.Exists(root)) yield break;

        string[] children;
        try { children = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly).Take(300).ToArray(); }
        catch { yield break; }

        foreach (var directory in children)
        {
            if (!Path.GetFileName(directory).Contains(directoryHint, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var exe in executableNames)
                yield return Path.Combine(directory, exe);
        }
    }

    private static string? FindFromUninstallRegistry(string[] displayTerms, string[] executableNames)
    {
        var locations = new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32),
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32)
        };

        foreach (var (hive, view) in locations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;

                foreach (var keyName in uninstall.GetSubKeyNames())
                {
                    using var key = uninstall.OpenSubKey(keyName);
                    var displayName = key?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                    if (!displayTerms.Any(term => displayName.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;

                    var displayIcon = key?.GetValue("DisplayIcon")?.ToString();
                    if (!string.IsNullOrWhiteSpace(displayIcon))
                    {
                        var candidate = NormalizeExecutableCandidate(displayIcon);
                        if (File.Exists(candidate)) return candidate;
                    }

                    var installLocation = key?.GetValue("InstallLocation")?.ToString();
                    if (!string.IsNullOrWhiteSpace(installLocation))
                    {
                        foreach (var exe in executableNames)
                        {
                            var candidate = Path.Combine(installLocation.Trim().Trim('"'), exe);
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private static string? TryFindRunningProcessPath(string processName)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return null;
    }

    private static string? FirstExisting(IEnumerable<string?> candidates)
    {
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            try
            {
                var path = NormalizeExecutableCandidate(raw);
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
            catch { }
        }
        return null;
    }

    private static string NormalizeExecutableCandidate(string raw)
    {
        var value = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
        var comma = value.LastIndexOf(',');
        if (comma > 2 && int.TryParse(value[(comma + 1)..], out _)) value = value[..comma];
        return value.Trim().Trim('"');
    }

    private static string ReadExecutableVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var raw = !string.IsNullOrWhiteSpace(info.ProductVersion) ? info.ProductVersion : info.FileVersion;
            if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
            var match = Regex.Match(raw, @"\d+(?:\.\d+){1,3}");
            return match.Success ? UpdateService.NormalizeVersion(match.Value) : UpdateService.NormalizeVersion(raw);
        }
        catch { return "Unknown"; }
    }

    private static Version? ParseComparableVersion(string value)
    {
        var match = Regex.Match(UpdateService.NormalizeVersion(value), @"\d+(?:\.\d+){1,3}");
        if (!match.Success) return null;
        var parts = match.Value.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).Take(4).ToList();
        while (parts.Count < 4) parts.Add(0);
        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }

    private static string SanitizeUserPath(string path)
    {
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile) && path.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
                return "%USERPROFILE%" + path[profile.Length..];
        }
        catch { }
        return path;
    }

    public static void OpenUrl(string url)
        => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
}
