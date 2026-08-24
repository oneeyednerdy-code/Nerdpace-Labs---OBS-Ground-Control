namespace Nerdspace.OBSRecovery.Models;

public sealed class UpdateDeferral
{
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset? RemindAfterUtc { get; set; }
    public bool SkipVersion { get; set; }
}
