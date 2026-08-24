using Microsoft.Win32;
using System.Diagnostics;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class SteelSeriesSonarService
{
    public const string SonarUrl = "https://steelseries.com/gg/sonar-for-streamers";
    public const string GgUrl = "https://steelseries.com/gg";
    private readonly LoggingService _logger;

    public SteelSeriesSonarService(LoggingService logger) => _logger = logger;

    public Task<SteelSeriesSonarSnapshot> InspectAsync(CancellationToken cancellationToken = default)
    {
        var (ggInstalled, version) = FindGgInstallation();
        var running = DetectSonarProcess();
        var endpoints = DetectSonarAudioEndpoints();
        var sonar = running || endpoints;

        if (!ggInstalled && !sonar)
        {
            return Task.FromResult(new SteelSeriesSonarSnapshot(
                true, false, "Not installed", false, false, false,
                "Nothing found — SteelSeries GG / Sonar was not detected. Check skipped.",
                "This is normal if you do not use SteelSeries Sonar. Sonar is installed and managed through SteelSeries GG."));
        }

        var status = sonar ? "SteelSeries Sonar detected" : "SteelSeries GG detected";
        var detail = sonar
            ? "Mission Control found local Sonar process/audio endpoint evidence. Use SteelSeries GG for Sonar configuration and software updates."
            : "SteelSeries GG is installed, but Mission Control did not find an active Sonar process or Sonar virtual audio endpoint. This can be normal when Sonar is disabled or GG is not running.";

        return Task.FromResult(new SteelSeriesSonarSnapshot(
            true, ggInstalled, version, sonar, running, endpoints, status, detail));
    }

    public void OpenSonarPage() => OpenUrl(SonarUrl);
    public void OpenGgPage() => OpenUrl(GgUrl);

    private (bool Installed, string Version) FindGgInstallation()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (root is null) continue;
                foreach (var subName in root.GetSubKeyNames())
                {
                    using var key = root.OpenSubKey(subName);
                    var display = (key?.GetValue("DisplayName") as string)?.Trim() ?? string.Empty;
                    if (!display.Contains("SteelSeries GG", StringComparison.OrdinalIgnoreCase) &&
                        !display.Equals("SteelSeries", StringComparison.OrdinalIgnoreCase)) continue;
                    var version = (key?.GetValue("DisplayVersion") as string)?.Trim();
                    return (true, string.IsNullOrWhiteSpace(version) ? "version unknown" : version);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"SteelSeries GG registry scan warning: {ex.Message}");
            }
        }
        return (false, "Not installed");
    }

    private static bool DetectSonarProcess()
    {
        try
        {
            return Process.GetProcesses().Any(p =>
            {
                try
                {
                    var n = p.ProcessName;
                    return n.Contains("SteelSeriesSonar", StringComparison.OrdinalIgnoreCase) ||
                           n.Contains("SonarSvc", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            });
        }
        catch { return false; }
    }

    private bool DetectSonarAudioEndpoints()
    {
        try
        {
            return RegistryTreeContains(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render", "SteelSeries Sonar") ||
                   RegistryTreeContains(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture", "SteelSeries Sonar");
        }
        catch (Exception ex)
        {
            _logger.Warn($"SteelSeries Sonar audio endpoint scan warning: {ex.Message}");
            return false;
        }
    }

    private static bool RegistryTreeContains(RegistryHive hive, string path, string needle)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var root = baseKey.OpenSubKey(path);
        if (root is null) return false;

        foreach (var deviceName in root.GetSubKeyNames())
        {
            using var device = root.OpenSubKey(deviceName);
            if (device is null) continue;
            if (KeyContains(device, needle)) return true;
            using var props = device.OpenSubKey("Properties");
            if (props is not null && KeyContains(props, needle)) return true;
        }
        return false;
    }

    private static bool KeyContains(RegistryKey key, string needle)
    {
        foreach (var valueName in key.GetValueNames())
        {
            var value = key.GetValue(valueName);
            if (value is string s && s.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
            if (value is string[] a && a.Any(x => x.Contains(needle, StringComparison.OrdinalIgnoreCase))) return true;
        }
        foreach (var childName in key.GetSubKeyNames().Take(64))
        {
            using var child = key.OpenSubKey(childName);
            if (child is not null && KeyContainsShallow(child, needle)) return true;
        }
        return false;
    }

    private static bool KeyContainsShallow(RegistryKey key, string needle)
    {
        foreach (var valueName in key.GetValueNames())
        {
            var value = key.GetValue(valueName);
            if (value is string s && s.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
            if (value is string[] a && a.Any(x => x.Contains(needle, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
}
