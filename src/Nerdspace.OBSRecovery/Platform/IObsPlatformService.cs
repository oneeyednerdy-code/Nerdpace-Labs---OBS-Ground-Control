using System.Diagnostics;

namespace Nerdspace.OBSRecovery.Platform;

public interface IObsPlatformService
{
    string PlatformName { get; }
    string MonitoringCapability { get; }
    bool SupportsAutomaticHangDetection { get; }
    bool SupportsWindowDetection { get; }

    IReadOnlyList<Process> GetObsProcesses();
    bool HasUsableWindow(Process process);
    bool IsResponding(Process process);
    string? FindObsInstall();
    string GetInstalledObsVersion(string? configuredPath);
    Task LaunchAsync(string? configuredPath, bool safeMode, CancellationToken cancellationToken = default);
    Task ShowAsync(int? processId, CancellationToken cancellationToken = default);
    Task GracefulStopAsync(Process process, CancellationToken cancellationToken = default);
    Task ForceStopAsync(Process process, CancellationToken cancellationToken = default);

    string GetObsLogDirectory();
    string GetObsConfigDirectory();
    string GetObsCrashDirectory();
    string GetSettingsDirectory();
    IReadOnlyList<string> GetPluginDirectories();
    Task ConfigureStartupAsync(bool enabled, CancellationToken cancellationToken = default);
}
