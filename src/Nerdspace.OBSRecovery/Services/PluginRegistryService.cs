using System.Reflection;
using System.Text.Json;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

/// <summary>
/// Loads the prebuilt OBS plugin catalog embedded at build time. Every entry comes from
/// the official OBS Studio Plugins resource directory. Source repositories are only
/// marked verified when the OBS resource page itself publishes the source URL.
/// </summary>
public sealed class PluginRegistryService
{
    private const string ResourceName = "Nerdspace.OBSRecovery.Data.plugin-catalog.json";
    private readonly IReadOnlyList<PluginCatalogEntry> _entries;

    public PluginRegistryService()
    {
        Catalog = LoadCatalog();
        _entries = Catalog.Entries;
    }

    public PluginCatalogDocument Catalog { get; }
    public IReadOnlyList<PluginCatalogEntry> All => _entries;
    public int ResourceCount => Catalog.ResourceCount > 0 ? Catalog.ResourceCount : _entries.Count;
    public int SourceVerifiedCount => Catalog.SourceVerifiedCount > 0
        ? Catalog.SourceVerifiedCount
        : _entries.Count(x => x.HasVerifiedSource);
    public DateTimeOffset? GeneratedAtUtc => DateTimeOffset.TryParse(Catalog.GeneratedAtUtc, out var value) ? value : null;

    public IReadOnlyList<PluginCatalogEntry> Search(string? query)
    {
        IEnumerable<PluginCatalogEntry> entries = _entries;

        // Mission Control is Windows-only right now. Resources with explicit platform
        // metadata that excludes Windows are hidden from normal discovery. Entries with
        // no platform metadata remain visible rather than being guessed incompatible.
        entries = entries.Where(entry => entry.SupportedPlatforms.Length == 0 ||
                                         entry.SupportedPlatforms.Any(p => p.Equals("Windows", StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            entries = entries.Where(entry =>
                entry.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                entry.Author.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                entry.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (entry.Repository?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.SourceUrl?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                entry.MatchTokens.Any(token => token.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return entries.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public PluginCatalogEntry? Match(string name, string path)
    {
        var haystack = $"{name} {path}";
        return _entries.FirstOrDefault(entry => IsMatch(entry, haystack));
    }

    public bool MatchesInstalledPlugin(PluginCatalogEntry entry, PluginInfo plugin)
        => IsMatch(entry, $"{plugin.Name} {plugin.Path}");

    private static bool IsMatch(PluginCatalogEntry entry, string haystack)
    {
        if (entry.MatchTokens.Length == 0) return false;
        return entry.MatchTokens.Any(token =>
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var normalizedToken = Normalize(token);
            var normalizedHaystack = Normalize(haystack);
            return normalizedHaystack.Contains(normalizedToken, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string Normalize(string value)
        => new(value.Where(c => char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray());

    private static PluginCatalogDocument LoadCatalog()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return PluginCatalogDocument.Empty;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PluginCatalogDocument>(stream, options) ?? PluginCatalogDocument.Empty;
        }
        catch
        {
            return PluginCatalogDocument.Empty;
        }
    }
}

public sealed class PluginCatalogDocument
{
    public static PluginCatalogDocument Empty { get; } = new();
    public string GeneratedAtUtc { get; set; } = string.Empty;
    public string Source { get; set; } = PluginDiscoveryService.OfficialObsPluginDirectoryUrl;
    public int ResourceCount { get; set; }
    public int SourceVerifiedCount { get; set; }
    public int FailedResourceCount { get; set; }
    public List<PluginCatalogEntry> Entries { get; set; } = new();
}
