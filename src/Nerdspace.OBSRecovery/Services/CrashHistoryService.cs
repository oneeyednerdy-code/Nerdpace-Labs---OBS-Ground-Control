using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class CrashHistoryService
{
    private readonly IObsPlatformService _platform;

    public CrashHistoryService(IObsPlatformService platform) => _platform = platform;

    public IReadOnlyList<CrashItem> List(int limit = 20)
    {
        var dir = _platform.GetObsCrashDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<CrashItem>();
        return Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .Take(limit)
            .Select(x => new CrashItem(x.FullName, x.LastWriteTime, x.Length))
            .ToList();
    }

    public sealed record CrashItem(string Path, DateTime Timestamp, long SizeBytes)
    {
        public string Display => $"{Timestamp:g} • {System.IO.Path.GetFileName(Path)} • {SizeBytes / 1024d:F0} KB";
    }
}
