namespace Nerdspace.OBSRecovery.Models;

public sealed record GitHubReleaseInfo(
    string Version,
    string TagName,
    string ReleaseUrl,
    DateTimeOffset? PublishedAt);
