using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class UpdateDeferralService
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;

    public UpdateDeferralService(AppSettings settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
    }

    public string Apply(string key, string latestVersion, string baseStatus)
    {
        if (!baseStatus.Equals("Update available", StringComparison.OrdinalIgnoreCase)) return baseStatus;
        if (!_settings.UpdateDeferrals.TryGetValue(key, out var deferral)) return baseStatus;
        if (!VersionMatches(deferral.Version, latestVersion)) return baseStatus;

        if (deferral.SkipVersion) return $"Skipped {latestVersion}";
        if (deferral.RemindAfterUtc is { } when && when > DateTimeOffset.UtcNow)
            return $"Deferred until {when.ToLocalTime():MMM d, yyyy}";

        return baseStatus;
    }

    public UpdateDeferral? Get(string key, string? latestVersion = null)
    {
        if (!_settings.UpdateDeferrals.TryGetValue(key, out var deferral)) return null;
        if (!string.IsNullOrWhiteSpace(latestVersion) && !VersionMatches(deferral.Version, latestVersion)) return null;
        return deferral;
    }

    public async Task SnoozeAsync(string key, string version, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        _settings.UpdateDeferrals[key] = new UpdateDeferral
        {
            Version = version,
            RemindAfterUtc = DateTimeOffset.UtcNow.Add(duration),
            SkipVersion = false
        };
        await _settingsService.SaveAsync(_settings, cancellationToken);
    }

    public async Task SkipVersionAsync(string key, string version, CancellationToken cancellationToken = default)
    {
        _settings.UpdateDeferrals[key] = new UpdateDeferral
        {
            Version = version,
            RemindAfterUtc = null,
            SkipVersion = true
        };
        await _settingsService.SaveAsync(_settings, cancellationToken);
    }

    public async Task ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_settings.UpdateDeferrals.Remove(key))
            await _settingsService.SaveAsync(_settings, cancellationToken);
    }

    private static bool VersionMatches(string a, string b)
        => UpdateService.NormalizeVersion(a).Equals(UpdateService.NormalizeVersion(b), StringComparison.OrdinalIgnoreCase);
}
