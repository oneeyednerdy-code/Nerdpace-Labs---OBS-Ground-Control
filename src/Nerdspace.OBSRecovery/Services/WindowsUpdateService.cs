using System.Diagnostics;
using Nerdspace.OBSRecovery.Models;

namespace Nerdspace.OBSRecovery.Services;

public sealed class WindowsUpdateService
{
    private readonly LoggingService _logger;
    public WindowsUpdateService(LoggingService logger) => _logger = logger;

    public Task<WindowsUpdateSnapshot> CheckMainUpdatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CheckWindowsUpdates(cancellationToken), cancellationToken);
    }

    public void OpenWindowsUpdate()
    {
        Process.Start(new ProcessStartInfo { FileName = "ms-settings:windowsupdate", UseShellExecute = true });
    }

    private WindowsUpdateSnapshot CheckWindowsUpdates(CancellationToken cancellationToken)
    {
        try
        {
            var type = Type.GetTypeFromProgID("Microsoft.Update.Session") ?? throw new InvalidOperationException("Windows Update Agent is unavailable.");
            dynamic session = Activator.CreateInstance(type)!;
            session.ClientApplicationID = "Nerdspace Labs OBS Ground Control";
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online = true;

            // BrowseOnly=0 excludes optional/browse-only updates; Type='Software' excludes driver updates.
            dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software' and BrowseOnly=0");
            var items = new List<WindowsUpdateItem>();
            bool reboot = false;
            int count = (int)result.Updates.Count;
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic update = result.Updates.Item(i);
                string title = ((string)update.Title).Trim();
                if (IsExcluded(title)) continue;

                bool assigned = false, autoSelected = false, rebootRequired = false;
                try { assigned = (bool)update.IsAssigned; } catch { }
                try { autoSelected = (bool)update.AutoSelectOnWebSites; } catch { }
                try { rebootRequired = (bool)update.RebootRequired; } catch { }

                if (!assigned && !autoSelected && !LooksLikeMainUpdate(title, update)) continue;
                items.Add(new WindowsUpdateItem(title, rebootRequired));
                reboot |= rebootRequired;
            }

            return new WindowsUpdateSnapshot(true, items, reboot,
                items.Count == 0 ? "Windows main updates look current" : "Main Windows updates available",
                "Ground Control checks non-optional software updates and filters out drivers, preview releases, optional/browse-only updates, and routine Defender definition updates. Installation stays in Windows Update.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Windows Update check failed: {ex.Message}");
            return new WindowsUpdateSnapshot(true, Array.Empty<WindowsUpdateItem>(), false, "Windows Update check unavailable", ex.Message);
        }
    }

    private static bool IsExcluded(string title)
    {
        return title.Contains("Preview", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Driver", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Definition Update for Microsoft Defender", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Security Intelligence Update", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Malicious Software Removal Tool", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMainUpdate(string title, dynamic update)
    {
        if (title.Contains("Cumulative Update", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Security Update", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Servicing Stack", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Feature update", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Update for Windows", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            int count = (int)update.Categories.Count;
            for (int i = 0; i < count; i++)
            {
                string name = (string)update.Categories.Item(i).Name;
                if (name.Contains("Critical Updates", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Security Updates", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Update Rollups", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Updates", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { }
        return false;
    }
}
