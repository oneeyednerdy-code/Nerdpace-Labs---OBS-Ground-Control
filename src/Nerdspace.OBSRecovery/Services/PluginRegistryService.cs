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
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            entries = entries
                .Select(entry => new { Entry = entry, Score = SearchScore(entry, terms) })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Entry);
        }
        else
        {
            entries = entries.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        return entries.ToList();
    }


    private static int SearchScore(PluginCatalogEntry entry, IReadOnlyList<string> terms)
    {
        var score = 0;
        foreach (var term in terms)
        {
            var matched = false;
            if (entry.DisplayName.Equals(term, StringComparison.OrdinalIgnoreCase)) { score += 100; matched = true; }
            else if (entry.DisplayName.StartsWith(term, StringComparison.OrdinalIgnoreCase)) { score += 70; matched = true; }
            else if (entry.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)) { score += 50; matched = true; }

            if (entry.Author.Contains(term, StringComparison.OrdinalIgnoreCase)) { score += 25; matched = true; }
            if (entry.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) { score += 15; matched = true; }
            if (entry.MatchTokens.Any(token => token.Contains(term, StringComparison.OrdinalIgnoreCase))) { score += 35; matched = true; }
            if (entry.Repository?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) { score += 20; matched = true; }
            if (entry.SourceUrl?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) { score += 10; matched = true; }

            // Every search term must match somewhere. This makes multi-word searches
            // useful for intent such as "vertical output" instead of broad OR matching.
            if (!matched) return 0;
        }
        return score;
    }

    public PluginCatalogEntry? Match(string name, string path)
    {
        var haystack = $"{name} {path}";
        var normalizedHaystack = Normalize(haystack);
        return _entries.FirstOrDefault(entry =>
            (!string.IsNullOrWhiteSpace(entry.Id) && Normalize(entry.Id).Length >= 4 && normalizedHaystack.Contains(Normalize(entry.Id), StringComparison.OrdinalIgnoreCase)) ||
            IsMatch(entry, haystack));
    }

    public bool MatchesInstalledPlugin(PluginCatalogEntry entry, PluginInfo plugin)
    {
        var haystack = $"{plugin.Id} {plugin.Name} {plugin.Path}";
        var normalizedHaystack = Normalize(haystack);
        return (!string.IsNullOrWhiteSpace(entry.Id) && Normalize(entry.Id).Length >= 4 && normalizedHaystack.Contains(Normalize(entry.Id), StringComparison.OrdinalIgnoreCase)) ||
               IsMatch(entry, haystack);
    }

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
