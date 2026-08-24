using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class GraphicsDriverService
{
    public const string NvidiaDriversUrl = "https://www.nvidia.com/en-us/drivers/";
    public const string AmdDriversUrl = "https://www.amd.com/en/support/download/drivers.html";
    private readonly LoggingService _logger;

    public GraphicsDriverService(LoggingService logger) => _logger = logger;

    public async Task<GraphicsDriverSnapshot> InspectAsync(CancellationToken cancellationToken = default)
    {
        var adapters = new List<GraphicsAdapterInfo>();
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classKey is not null)
            {
                foreach (var subName in classKey.GetSubKeyNames().Where(x => x.Length == 4 && x.All(char.IsDigit)))
                {
                    using var key = classKey.OpenSubKey(subName);
                    var name = (key?.GetValue("DriverDesc") as string)?.Trim();
                    var provider = (key?.GetValue("ProviderName") as string)?.Trim() ?? string.Empty;
                    var version = (key?.GetValue("DriverVersion") as string)?.Trim() ?? "Unknown";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var vendor = DetectVendor($"{provider} {name}");
                    if (vendor is "NVIDIA" or "AMD")
                        adapters.Add(new GraphicsAdapterInfo(vendor, name, version));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Windows graphics registry inspection failed: {ex.Message}");
        }

        var nvidia = await TryNvidiaSmiAsync(cancellationToken);
        if (nvidia is not null)
        {
            adapters.RemoveAll(x => x.Vendor == "NVIDIA");
            adapters.AddRange(nvidia);
        }

        adapters = adapters
            .GroupBy(x => $"{x.Vendor}|{x.Name}|{x.DriverVersion}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Vendor)
            .ThenBy(x => x.Name)
            .ToList();

        var status = adapters.Count == 0
            ? "No NVIDIA or AMD display driver detected"
            : "Installed graphics driver detected";

        var detail = adapters.Count == 0
            ? "Ground Control did not identify an NVIDIA or AMD display adapter. This is informational if the PC uses Intel graphics or another vendor."
            : "Ground Control reports the locally installed Windows driver. Use the official vendor page to verify the newest compatible release for the exact GPU.";

        return new GraphicsDriverSnapshot(true, adapters, status, detail);
    }

    public void OpenVendorDriverPage(string vendor)
    {
        var url = vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase) ? AmdDriversUrl : NvidiaDriversUrl;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private async Task<IReadOnlyList<GraphicsAdapterInfo>?> TryNvidiaSmiAsync(CancellationToken cancellationToken)
    {
        var command = FindNvidiaSmi();
        if (command is null) return null;

        try
        {
            var result = await RunAsync(command, new[]
            {
                "--query-gpu=name,driver_version,temperature.gpu,utilization.gpu,memory.used,memory.total",
                "--format=csv,noheader,nounits"
            }, cancellationToken);

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output)) return null;

            var list = new List<GraphicsAdapterInfo>();
            foreach (var row in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = row.Split(',').Select(x => x.Trim()).ToArray();
                if (f.Length < 6) continue;

                static double? D(string value) =>
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : null;

                list.Add(new GraphicsAdapterInfo("NVIDIA", f[0], f[1], D(f[2]), D(f[3]), D(f[4]), D(f[5])));
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.Warn($"nvidia-smi inspection failed: {ex.Message}");
            return null;
        }
    }

    private static string? FindNvidiaSmi()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var p in new[]
        {
            Path.Combine(pf, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "nvidia-smi.exe")
        })
        {
            if (File.Exists(p)) return p;
        }

        return CommandExists("nvidia-smi") ? "nvidia-smi" : null;
    }

    private static string DetectVendor(string value)
    {
        if (value.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) return "NVIDIA";
        if (value.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("ATI", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase)) return "AMD";
        return "Other";
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (p is null) return false;
            if (!p.WaitForExit(1200)) { try { p.Kill(); } catch { } }
            return true;
        }
        catch { return false; }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string fileName, IEnumerable<string> args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errorTask);
    }
}
