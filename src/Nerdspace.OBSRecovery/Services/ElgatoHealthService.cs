using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class ElgatoHealthService
{
    public const string DownloadsUrl = "https://www.elgato.com/us/en/s/downloads";

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpHardwareId = 0x00000001;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint SpdrpFriendlyName = 0x0000000C;

    private readonly LoggingService _logger;

    private static readonly string[] KnownNames =
    {
        "Stream Deck", "Wave Link", "Camera Hub", "Control Center", "4K Capture Utility",
        "Elgato Studio", "Video Capture", "Game Capture HD"
    };

    public ElgatoHealthService(LoggingService logger) => _logger = logger;

    public Task<ElgatoHealthSnapshot> InspectAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(InspectWindows());

    public void OpenDownloads()
        => Process.Start(new ProcessStartInfo { FileName = DownloadsUrl, UseShellExecute = true });

    private ElgatoHealthSnapshot InspectWindows()
    {
        var software = ScanWindowsSoftware();
        var hardware = ScanPresentWindowsHardware();
        var attention = software.Any(IsWaveLink2);
        return BuildSnapshot(true, true, software, hardware, attention);
    }

    private List<ElgatoSoftwareInfo> ScanWindowsSoftware()
    {
        var list = new List<ElgatoSoftwareInfo>();

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (root is null) continue;

                foreach (var name in root.GetSubKeyNames())
                {
                    using var key = root.OpenSubKey(name);
                    var display = (key?.GetValue("DisplayName") as string)?.Trim();
                    if (display is null) continue;

                    var isElgato =
                        display.Contains("Elgato", StringComparison.OrdinalIgnoreCase) ||
                        KnownNames.Any(k => display.StartsWith(k, StringComparison.OrdinalIgnoreCase));

                    if (!isElgato) continue;

                    var version = (key?.GetValue("DisplayVersion") as string)?.Trim() ?? "Unknown";
                    var location = (key?.GetValue("InstallLocation") as string)?.Trim();
                    list.Add(new ElgatoSoftwareInfo(NormalizeName(display), version, location));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Elgato registry software scan warning: {ex.Message}");
            }
        }

        return list
            .GroupBy(x => $"{x.Name}|{x.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    private List<ElgatoHardwareInfo> ScanPresentWindowsHardware()
    {
        var devices = new List<ElgatoHardwareInfo>();
        var handle = SetupDiGetClassDevs(
            IntPtr.Zero,
            null,
            IntPtr.Zero,
            DigcfPresent | DigcfAllClasses);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            _logger.Warn("Windows SetupAPI could not create a present-device list for Elgato detection.");
            return devices;
        }

        try
        {
            uint index = 0;
            while (true)
            {
                var data = new SpDevInfoData
                {
                    CbSize = (uint)Marshal.SizeOf<SpDevInfoData>()
                };

                if (!SetupDiEnumDeviceInfo(handle, index, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 259) // ERROR_NO_MORE_ITEMS
                        _logger.Warn($"Windows SetupAPI Elgato enumeration ended with error {error}.");
                    break;
                }

                index++;

                var friendly = GetDeviceProperty(handle, ref data, SpdrpFriendlyName);
                var desc = GetDeviceProperty(handle, ref data, SpdrpDeviceDesc);
                var mfg = GetDeviceProperty(handle, ref data, SpdrpMfg);
                var hardwareIds = GetDeviceProperty(handle, ref data, SpdrpHardwareId);

                var evidence = $"{friendly} {desc} {mfg} {hardwareIds}";
                var isElgato =
                    evidence.Contains("Elgato", StringComparison.OrdinalIgnoreCase) ||
                    evidence.Contains("VID_0FD9", StringComparison.OrdinalIgnoreCase);

                if (!isElgato) continue;

                var name = FirstUseful(
                    CleanDeviceString(friendly),
                    CleanDeviceString(desc),
                    "Elgato USB device");

                devices.Add(new ElgatoHardwareInfo(name, "Present Windows device"));
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Elgato present-device scan warning: {ex.Message}");
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(handle);
        }

        return devices
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static string GetDeviceProperty(IntPtr handle, ref SpDevInfoData data, uint property)
    {
        var buffer = new byte[8192];
        if (!SetupDiGetDeviceRegistryProperty(
                handle,
                ref data,
                property,
                out _,
                buffer,
                (uint)buffer.Length,
                out var required))
            return string.Empty;

        var count = (int)Math.Min(required, (uint)buffer.Length);
        if (count <= 2) return string.Empty;

        return Encoding.Unicode
            .GetString(buffer, 0, count)
            .Replace('\0', ' ')
            .Trim();
    }

    private static ElgatoHealthSnapshot BuildSnapshot(
        bool softwareSupported,
        bool hardwareSupported,
        IReadOnlyList<ElgatoSoftwareInfo> software,
        IReadOnlyList<ElgatoHardwareInfo> hardware,
        bool attention)
    {
        var any = software.Count > 0 || hardware.Count > 0;

        var status = !any
            ? "No supported Elgato hardware or software detected"
            : attention
                ? "Elgato software needs attention"
                : "Elgato hardware/software detected";

        var detail = attention
            ? "Wave Link 2 is end-of-life; use Elgato's official Downloads page for current Wave Link software."
            : "Software and currently-present Windows hardware are reported separately. Installed software does not imply a device is connected, and detected hardware does not imply every Elgato app is required.";

        return new ElgatoHealthSnapshot(
            softwareSupported,
            hardwareSupported,
            software,
            hardware,
            attention,
            status,
            detail);
    }

    private static bool IsWaveLink2(ElgatoSoftwareInfo x)
    {
        if (!x.Name.Contains("Wave Link", StringComparison.OrdinalIgnoreCase)) return false;

        var match = Regex.Match(x.Version, @"^(\d+)");
        return match.Success &&
               int.TryParse(match.Groups[1].Value, out var major) &&
               major < 3;
    }

    private static string NormalizeName(string name)
    {
        var n = name.Replace("Elgato ", "", StringComparison.OrdinalIgnoreCase).Trim();
        return KnownNames.FirstOrDefault(k => n.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? n;
    }

    private static string CleanDeviceString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var i = value.LastIndexOf(';');
        return (i >= 0 ? value[(i + 1)..] : value).Trim();
    }

    private static string FirstUseful(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Elgato device";

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint CbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        IntPtr classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SpDevInfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        byte[] propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
}
