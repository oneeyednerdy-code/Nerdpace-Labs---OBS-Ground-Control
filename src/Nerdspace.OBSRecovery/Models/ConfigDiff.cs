namespace Nerdspace.OBSRecovery.Models;

public sealed record ConfigDiff(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Changed)
{
    public string Summary => $"{Added.Count} added • {Removed.Count} removed • {Changed.Count} changed";
}
