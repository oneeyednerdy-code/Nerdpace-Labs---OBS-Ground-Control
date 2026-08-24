using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
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
    private readonly HttpClient _http = new();

    private sealed record ReleaseSource(string Name, string Url);

    private static readonly IReadOnlyDictionary<string, ReleaseSource> ReleaseSources =
        new Dictionary<string, ReleaseSource>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stream Deck"] = new("Stream Deck", "https://help.elgato.com/hc/en-us/sections/5162671529357-Elgato-Stream-Deck-Software-Release-Notes"),
            ["Wave Link"] = new("Wave Link", "https://help.elgato.com/hc/en-us/sections/4913442828941-Wave-Link-Release-Notes"),
            ["Camera Hub"] = new("Camera Hub", "https://help.elgato.com/hc/en-us/sections/4880787756941-Elgato-Camera-Hub-Release-Notes"),
            ["Control Center"] = new("Control Center", "https://help.elgato.com/hc/en-us/sections/4586833941261-Elgato-Control-Center-Release-Notes"),
            ["4K Capture Utility"] = new("4K Capture Utility", "https://help.elgato.com/hc/en-us/sections/5126053814029-Elgato-4K-Capture-Utility-Release-Notes"),
            ["Elgato Studio"] = new("Elgato Studio", "https://help.elgato.com/hc/en-us/sections/36773146296465-Elgato-Studio-Release-Notes")
        };

    private static readonly string[] KnownNames =
    {
        "Stream Deck", "Wave Link", "Camera Hub", "Control Center", "4K Capture Utility",
        "Elgato Studio", "Video Capture", "Game Capture HD"
    };

    public ElgatoHealthService(LoggingService logger)
    {
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(8);
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("NerdSpace-Streamer-Mission-Control", AppVersion.Version));
    }

    public Task<ElgatoHealthSnapshot> InspectAsync(CancellationToken cancellationToken = default)
        => InspectAsync(false, cancellationToken);

    public async Task<ElgatoHealthSnapshot> InspectAsync(
        bool checkOnline,
        CancellationToken cancellationToken = default)
    {
        var software = ScanWindowsSoftware();
        if (checkOnline && software.Count > 0)
            software = await EnrichSoftwareVersionsAsync(software, cancellationToken);

        var hardware = ScanPresentWindowsHardware();
        var attention = software.Any(IsWaveLink2) || software.Any(x => x.UpdateAvailable);
        return BuildSnapshot(true, true, software, hardware, attention, checkOnline);
    }

    public void OpenDownloads()
        => Process.Start(new ProcessStartInfo { FileName = DownloadsUrl, UseShellExecute = true });

    private async Task<List<ElgatoSoftwareInfo>> EnrichSoftwareVersionsAsync(
        IReadOnlyList<ElgatoSoftwareInfo> software,
        CancellationToken cancellationToken)
    {
        var tasks = software.Select(x => EnrichOneAsync(x, cancellationToken));
        return (await Task.WhenAll(tasks)).OrderBy(x => x.Name).ToList();
    }

    private async Task<ElgatoSoftwareInfo> EnrichOneAsync(
        ElgatoSoftwareInfo installed,
        CancellationToken cancellationToken)
    {
        if (!ReleaseSources.TryGetValue(installed.Name, out var source))
        {
            return installed with
            {
                LatestVersion = "Unavailable",
                UpdateStatus = "Latest version unavailable",
                ReleaseNotesUrl = DownloadsUrl
            };
        }

        try
        {
            var html = await _http.GetStringAsync(source.Url, cancellationToken);
            var latest = ExtractLatestWindowsRelease(html, source.Name);

            if (latest is null)
            {
                _logger.Warn($"Elgato {source.Name} release page did not expose a verifiable Windows version.");
                return installed with
                {
                    LatestVersion = "Unavailable",
                    UpdateStatus = "Latest version unavailable",
                    ReleaseNotesUrl = source.Url
                };
            }

            var status = UpdateService.Compare(installed.Version, latest.Value.Version);
            return installed with
            {
                LatestVersion = latest.Value.Version,
                UpdateStatus = status,
                ReleaseNotesUrl = latest.Value.Url
            };
        }
        catch (Exception ex)
        {
            _logger.Warn($"Elgato {source.Name} update check failed: {ex.Message}");
            return installed with
            {
                LatestVersion = "Unavailable",
                UpdateStatus = "Update check unavailable",
                ReleaseNotesUrl = source.Url
            };
        }
    }

    private static (string Version, string Url)? ExtractLatestWindowsRelease(string html, string product)
    {
        var candidates = new List<(Version Parsed, string Display, string Url)>();

        foreach (Match match in Regex.Matches(
                     html,
                     """<a[^>]+href=["'](?<href>[^"']+)["'][^>]*>(?<title>.*?)</a>""",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var title = WebUtility.HtmlDecode(Regex.Replace(match.Groups["title"].Value, "<.*?>", " "))
                .Replace('\u00A0', ' ')
                .Trim();

            if (string.IsNullOrWhiteSpace(title) ||
                !title.Contains(product, StringComparison.OrdinalIgnoreCase) ||
                !title.Contains("Release", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Beta", StringComparison.OrdinalIgnoreCase))
                continue;

            // This is the Windows desktop build. Do not accidentally compare against
            // a newer macOS/iOS/Android-only release.
            if (title.Contains("macOS", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("iOS", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Android", StringComparison.OrdinalIgnoreCase))
                continue;

            var versionMatch = Regex.Match(title, @"\b\d+(?:\.\d+){1,3}\b");
            if (!versionMatch.Success || !TryVersion(versionMatch.Value, out var parsed))
                continue;

            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            var url = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : $"https://help.elgato.com{(href.StartsWith('/') ? string.Empty : "/")}{href}";

            candidates.Add((parsed, versionMatch.Value, url));
        }

        if (candidates.Count == 0) return null;
        var latest = candidates.OrderByDescending(x => x.Parsed).First();
        return (latest.Display, latest.Url);
    }

    private static bool TryVersion(string value, out Version version)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Take(4)
            .ToList();

        while (parts.Count < 4) parts.Add(0);
        version = new Version(parts[0], parts[1], parts[2], parts[3]);
        return true;
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
                    if (error != 259)
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
        bool attention,
        bool checkedOnline)
    {
        var any = software.Count > 0 || hardware.Count > 0;

        var status = !any
            ? "Nothing found — No supported Elgato hardware or software detected."
            : software.Any(x => x.UpdateAvailable)
                ? "Elgato software update available"
                : attention
                    ? "Elgato software needs attention"
                    : "Elgato hardware/software detected";

        string detail;
        if (!any)
        {
            detail = "Scan completed successfully. Nothing matching the supported Elgato software list or currently-present Elgato hardware was found.";
        }
        else if (software.Any(x => x.UpdateAvailable))
        {
            detail = "One or more installed Elgato applications have a newer version listed by Elgato's official release notes. Mission Control never installs the update automatically.";
        }
        else if (software.Any(IsWaveLink2))
        {
            detail = "Wave Link 2 is end-of-life. Elgato's current development focus is Wave Link 3.";
        }
        else if (!checkedOnline && software.Count > 0)
        {
            detail = "Installed versions are shown. Online latest-version checking was skipped; enable online update checks or run the Elgato scan from Update Center to compare against Elgato's official release notes.";
        }
        else
        {
            detail = "Software and currently-present hardware are reported separately. Firmware for connected Elgato hardware is normally managed by the applicable Elgato application.";
        }

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

        // Keep "Elgato Studio" as the canonical public product name because its
        // official release-note catalog uses that full title.
        if (n.Contains("Studio", StringComparison.OrdinalIgnoreCase))
            return "Elgato Studio";

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
