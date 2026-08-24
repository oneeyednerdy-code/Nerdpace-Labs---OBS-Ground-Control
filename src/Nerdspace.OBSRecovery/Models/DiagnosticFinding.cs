namespace Nerdspace.OBSRecovery.Models;

public sealed record DiagnosticFinding(
    CheckSeverity Severity,
    string Category,
    string Summary,
    string Detail,
    int Occurrences = 1)
{
    public string Display => $"{Severity.ToString().ToUpperInvariant()} • {Category} • {Summary}" +
                             (Occurrences > 1 ? $" ({Occurrences}x)" : string.Empty);
}
