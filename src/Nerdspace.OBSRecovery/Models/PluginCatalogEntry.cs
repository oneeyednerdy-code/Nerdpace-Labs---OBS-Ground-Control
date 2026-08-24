namespace Nerdspace.OBSRecovery.Models;

public sealed record PluginCatalogEntry(
    string Id,
    string DisplayName,
    string Author,
    string? Repository,
    string? SourceUrl,
    string SourceVerification,
    string ObsResourceUrl,
    string Description,
    string? ResourceVersion,
    string[] SupportedPlatforms,
    string MinimumObsVersion,
    string[] MatchTokens)
{
    public bool HasVerifiedSource => !string.IsNullOrWhiteSpace(SourceUrl) &&
                                     SourceVerification.Equals("OBS resource page", StringComparison.OrdinalIgnoreCase);
    public bool HasGitHubRepository => !string.IsNullOrWhiteSpace(Repository);
    public string RepositoryUrl => HasGitHubRepository ? $"https://github.com/{Repository}" : SourceUrl ?? string.Empty;
    public string ReleasesUrl => HasGitHubRepository ? $"https://github.com/{Repository}/releases" : SourceUrl ?? string.Empty;
    public string PlatformSummary => SupportedPlatforms.Length == 0 ? "Platform metadata unavailable" : string.Join(", ", SupportedPlatforms);
    public string Display => $"{DisplayName} • {Author} • {SourceVerification}";
}
