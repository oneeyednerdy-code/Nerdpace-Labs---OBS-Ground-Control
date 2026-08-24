using System.Net.Http.Headers;
using System.Text.Json;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class UpdateService
{
    private readonly HttpClient _http = new();
    private readonly LoggingService _logger;

    public UpdateService(LoggingService logger)
    {
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nerdspace-OBS-Ground-Control", AppVersion.Version));
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    public async Task<string> GetLatestObsVersionAsync(CancellationToken cancellationToken = default)
    {
        var release = await GetLatestGitHubReleaseAsync("obsproject/obs-studio", cancellationToken);
        return release?.Version ?? "Unavailable";
    }

    public async Task<string> GetLatestGitHubVersionAsync(string repository, CancellationToken cancellationToken = default)
    {
        var release = await GetLatestGitHubReleaseAsync(repository, cancellationToken);
        return release?.Version ?? "Unavailable";
    }

    public async Task<GitHubReleaseInfo?> GetLatestGitHubReleaseAsync(string repository, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{repository}/releases/latest";
            using var stream = await _http.GetStreamAsync(url, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "Unknown" : "Unknown";
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;
            DateTimeOffset? publishedAt = null;
            if (root.TryGetProperty("published_at", out var published) && DateTimeOffset.TryParse(published.GetString(), out var parsed))
                publishedAt = parsed;

            if (string.IsNullOrWhiteSpace(releaseUrl))
            {
                _logger.Warn($"Latest release metadata for {repository} did not contain an html_url.");
                return null;
            }

            return new GitHubReleaseInfo(NormalizeVersion(tag), tag, releaseUrl, publishedAt);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not check {repository} for updates: {ex.Message}");
            return null;
        }
    }

    public static string Compare(string installed, string latest)
    {
        if (installed is "Unknown" or "Not detected" || latest is "Unknown" or "Unavailable") return "Version unknown";
        var a = TryParseLooseVersion(installed);
        var b = TryParseLooseVersion(latest);
        if (a is null || b is null) return installed.Equals(latest, StringComparison.OrdinalIgnoreCase) ? "Current" : "Check manually";
        return a < b ? "Update available" : a == b ? "Current" : "Newer than catalog";
    }

    private static Version? TryParseLooseVersion(string value)
    {
        var clean = NormalizeVersion(value);
        var core = new string(clean.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
        if (core.Count(c => c == '.') == 1) core += ".0";
        return Version.TryParse(core, out var version) ? version : null;
    }

    public static string NormalizeVersion(string value)
    {
        var clean = value.Trim();
        if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)) clean = clean[1..];
        if (clean.StartsWith("Version ", StringComparison.OrdinalIgnoreCase)) clean = clean[8..];
        return clean.Trim();
    }
}
