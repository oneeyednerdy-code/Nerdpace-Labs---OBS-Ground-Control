using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nerdspace.OBSRecovery.Platform;

public sealed class WindowsObsPlatformService : IObsPlatformService
{
    public string PlatformName => "Windows 10/11 • x64";
    public string MonitoringCapability => "Windows process, window, responsiveness + stuck-shutdown monitoring";
    public bool SupportsAutomaticHangDetection => true;
    public bool SupportsWindowDetection => true;

    public IReadOnlyList<Process> GetObsProcesses() => Process.GetProcessesByName("obs64");

    public bool HasUsableWindow(Process process)
    {
        try { process.Refresh(); return process.MainWindowHandle != IntPtr.Zero; }
        catch { return false; }
    }

    public bool IsResponding(Process process)
    {
        try { process.Refresh(); return process.Responding; }
        catch { return false; }
    }

    public string? FindObsInstall()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "OBS Studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "OBS Studio", "bin", "64bit", "obs64.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public string GetInstalledObsVersion(string? configuredPath)
    {
        try
        {
            var path = !string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath) ? configuredPath : FindObsInstall();
            if (path is null) return "Not detected";
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.ProductVersion?.Split(' ')[0] ?? info.FileVersion ?? "Unknown";
        }
        catch { return "Unknown"; }
    }

    public Task LaunchAsync(string? configuredPath, bool safeMode, CancellationToken cancellationToken = default)
    {
        var path = !string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath)
            ? configuredPath
            : FindObsInstall();
        if (path is null) throw new FileNotFoundException("OBS Studio could not be located.");

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = true,
            Arguments = safeMode ? "--safe-mode" : string.Empty
        });
        return Task.CompletedTask;
    }

    public Task ShowAsync(int? processId, CancellationToken cancellationToken = default)
    {
        Process? process = null;
        try
        {
            process = processId.HasValue ? Process.GetProcessById(processId.Value) : GetObsProcesses().FirstOrDefault(p => HasUsableWindow(p));
            if (process is not null && process.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindowAsync(process.MainWindowHandle, 9);
                SetForegroundWindow(process.MainWindowHandle);
            }
        }
        finally { process?.Dispose(); }
        return Task.CompletedTask;
    }

    public async Task GracefulStopAsync(Process process, CancellationToken cancellationToken = default)
    {
        try
        {
            if (process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow();
            await WaitForExitAsync(process, TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch { }
    }

    public async Task ForceStopAsync(Process process, CancellationToken cancellationToken = default)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            await WaitForExitAsync(process, TimeSpan.FromSeconds(5), cancellationToken);
            return;
        }
        catch (ArgumentException) { return; }
        catch (System.ComponentModel.Win32Exception) { }

        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine Ground Control executable path.");
        using var helper = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"--elevated-kill-obs {process.Id}"
        }) ?? throw new InvalidOperationException("Windows did not start the elevated recovery helper.");

        await helper.WaitForExitAsync(cancellationToken);
        if (helper.ExitCode != 0)
            throw new InvalidOperationException($"Elevated OBS recovery failed with exit code {helper.ExitCode}.");
    }

    public string GetObsLogDirectory() => Path.Combine(GetObsConfigDirectory(), "logs");
    public string GetObsCrashDirectory() => Path.Combine(GetObsConfigDirectory(), "crashes");
    public string GetObsConfigDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio");
    public string GetSettingsDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nerdspace Labs", "OBS Ground Control");

    public IReadOnlyList<string> GetPluginDirectories()
    {
        var list = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "obs-studio", "plugins")
        };

        var customPluginPath = Environment.GetEnvironmentVariable("OBS_PLUGINS_PATH");
        if (!string.IsNullOrWhiteSpace(customPluginPath))
        {
            foreach (var path in customPluginPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                list.Add(path);
        }
        var install = FindObsInstall();
        if (install is not null)
        {
            var obsRoot = Directory.GetParent(Directory.GetParent(Directory.GetParent(install)!.FullName)!.FullName)!.FullName;
            list.Add(Path.Combine(obsRoot, "obs-plugins", "64bit"));
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public Task ConfigureStartupAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null) return Task.CompletedTask;
        if (enabled) key.SetValue("Nerdspace Labs OBS Ground Control", $"\"{Environment.ProcessPath}\" --tray");
        else key.DeleteValue("Nerdspace Labs OBS Ground Control", throwOnMissingValue: false);
        return Task.CompletedTask;
    }

    private static async Task WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try { await process.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
