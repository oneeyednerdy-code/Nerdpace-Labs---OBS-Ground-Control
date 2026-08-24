namespace Nerdspace.OBSRecovery.Models;

public sealed record PluginCatalogEntry(string Id, string DisplayName, string Repository, string[] MatchTokens);
