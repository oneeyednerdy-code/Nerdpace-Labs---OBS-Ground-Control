namespace Nerdspace.OBSRecovery.Models;

public sealed record SelfUpdateSnapshot(
    bool Configured,
    string Channel,
    string InstalledVersion,
    string LatestVersion,
    string Status,
    string Detail,
    bool UpdateAvailable,
    bool CanInstall,
    string? ReleaseUrl,
    DateTimeOffset? CheckedAt)
{
    public string Display =>
        $"Installed: {InstalledVersion}\n" +
        $"Latest: {LatestVersion}\n" +
        $"Channel: {Channel}\n" +
        $"Status: {Status}\n{Detail}";
}

public sealed record SelfUpdateProgress(
    string Stage,
    int? Percent = null)
{
    public string Display => Percent.HasValue ? $"{Stage} • {Percent.Value}%" : Stage;
}

public sealed record SelfUpdateFeedConfig(
    string Repository,
    string PublicKey,
    string StableFeedUrl,
    string PreviewFeedUrl)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Repository) &&
        !string.IsNullOrWhiteSpace(PublicKey) &&
        !string.IsNullOrWhiteSpace(StableFeedUrl) &&
        !string.IsNullOrWhiteSpace(PreviewFeedUrl);
}
