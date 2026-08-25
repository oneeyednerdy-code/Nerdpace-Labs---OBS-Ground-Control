using System.Diagnostics;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SystemHealthService
{
    private readonly AppSettings _settings;
    private readonly IObsPlatformService _platform;
    private readonly UpdateService _updates;
    private readonly LoggingService _logger;
    private DateTimeOffset? _lastCpuTimeAt;
    private TimeSpan _lastCpuTime;
    private string _latestObsVersion = "Not checked";
    private DateTimeOffset _slowSampleExpiresAt = DateTimeOffset.MinValue;
    private string? _cachedRecordingPath;
    private double? _cachedRecordingFreeGb;
    private string _cachedInstalledObsVersion = "Not detected";
    private readonly object _sampleGate = new();

    public SystemHealthService(AppSettings settings, IObsPlatformService platform, UpdateService updates, LoggingService logger)
    {
        _settings = settings;
        _platform = platform;
        _updates = updates;
        _logger = logger;
    }

    public async Task RefreshLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.CheckUpdatesOnline) { _latestObsVersion = "Online checks disabled"; return; }
        _latestObsVersion = await _updates.GetLatestObsVersionAsync(cancellationToken);
    }

    public SystemHealthSnapshot Sample()
    {
        lock (_sampleGate)
        {
            return SampleCore();
        }
    }

    private SystemHealthSnapshot SampleCore()
    {
        var processes = _platform.GetObsProcesses().ToList();
        double memory = 0;
        TimeSpan totalCpu = TimeSpan.Zero;
        foreach (var process in processes)
        {
            try { process.Refresh(); memory += process.WorkingSet64 / 1024d / 1024d; totalCpu += process.TotalProcessorTime; }
            catch { }
            finally { process.Dispose(); }
        }

        double? cpu = null;
        var now = DateTimeOffset.Now;
        if (_lastCpuTimeAt.HasValue)
        {
            var elapsedMs = (now - _lastCpuTimeAt.Value).TotalMilliseconds;
            var cpuMs = (totalCpu - _lastCpuTime).TotalMilliseconds;
            if (elapsedMs > 100 && cpuMs >= 0)
                cpu = Math.Max(0, Math.Min(100, cpuMs / elapsedMs / Environment.ProcessorCount * 100));
        }
        _lastCpuTimeAt = now;
        _lastCpuTime = totalCpu;

        if (now >= _slowSampleExpiresAt)
        {
            _cachedRecordingPath = FindRecordingPath();
            _cachedRecordingFreeGb = GetFreeGb(_cachedRecordingPath);
            _cachedInstalledObsVersion = _platform.GetInstalledObsVersion(_settings.ObsPath);
            _slowSampleExpiresAt = now.AddSeconds(30);
        }

        return new SystemHealthSnapshot(
            processes.Count,
            memory,
            cpu,
            _cachedRecordingPath ?? "Not detected",
            _cachedRecordingFreeGb,
            _cachedInstalledObsVersion,
            _latestObsVersion,
            UpdateService.Compare(_cachedInstalledObsVersion, _latestObsVersion),
            now);
    }

    public string? FindRecordingPath()
    {
        try
        {
            var profiles = Path.Combine(_platform.GetObsConfigDirectory(), "basic", "profiles");
            if (!Directory.Exists(profiles)) return null;
            foreach (var ini in Directory.EnumerateFiles(profiles, "basic.ini", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc))
            {
                foreach (var line in File.ReadLines(ini))
                {
                    var trimmed = line.Trim();
                    foreach (var key in new[] { "RecFilePath=", "FilePath=" })
                    {
                        if (!trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                        var value = trimmed[key.Length..].Trim().Trim('"');
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        value = Environment.ExpandEnvironmentVariables(value);
                        if (Path.IsPathRooted(value)) return value;
                    }
                }
            }
        }
        catch (Exception ex) { _logger.Warn($"Could not read OBS recording path: {ex.Message}"); }
        return null;
    }

    private static double? GetFreeGb(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root)) return null;
            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
        }
        catch { return null; }
    }
}
