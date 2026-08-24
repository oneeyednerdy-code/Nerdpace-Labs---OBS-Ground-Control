namespace Nerdspace.OBSRecovery.Models;

public enum CheckSeverity
{
    Pass,
    Info,
    Warning,
    Fail
}

public sealed record PreflightResult(
    string Name,
    CheckSeverity Severity,
    string Summary,
    string Detail);
