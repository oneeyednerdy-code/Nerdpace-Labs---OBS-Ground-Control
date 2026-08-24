namespace Nerdspace.OBSRecovery.Models;

public sealed record WindowsUpdateItem(string Title, bool RebootRequired)
{
    public string Display => RebootRequired ? $"{Title} • restart may be required" : Title;
}

public sealed record WindowsUpdateSnapshot(
    bool Supported,
    IReadOnlyList<WindowsUpdateItem> Updates,
    bool RebootPending,
    string Status,
    string Detail)
{
    public int Count => Updates.Count;
    public string Summary => !Supported || Status.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ? Status : Count == 0 ? "No main Windows updates pending" : $"{Count} main Windows update(s) pending";
}
