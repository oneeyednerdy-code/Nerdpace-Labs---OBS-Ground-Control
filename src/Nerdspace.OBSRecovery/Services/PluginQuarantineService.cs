using System.Text.Json;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class PluginQuarantineService
{
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;
    private readonly string _root;

    public PluginQuarantineService(IObsPlatformService platform, LoggingService logger)
    {
        _platform = platform;
        _logger = logger;
        _root = Path.Combine(platform.GetSettingsDirectory(), "Plugin Quarantine");
        Directory.CreateDirectory(_root);
    }

    public async Task QuarantineAsync(PluginInfo plugin, CancellationToken cancellationToken = default)
    {
        if (_platform.GetObsProcesses().Count > 0) throw new InvalidOperationException("Close OBS before quarantining a plugin.");
        if (!plugin.CanQuarantine || !Directory.Exists(plugin.Path))
            throw new InvalidOperationException("This plugin cannot be safely quarantined automatically. Mission Control only quarantines complete plugin bundles/directories it can restore.");

        var id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{plugin.Id}";
        var destination = Path.Combine(_root, id, "plugin");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(plugin.Path, destination);
        var manifest = new QuarantineManifest(plugin.Name, plugin.Path, destination, DateTimeOffset.Now);
        await File.WriteAllTextAsync(Path.Combine(_root, id, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        _logger.Warn($"Plugin quarantined: {plugin.Name}. Original path: {plugin.Path}");
    }

    public IReadOnlyList<QuarantineItem> List()
    {
        var list = new List<QuarantineItem>();
        foreach (var manifestPath in Directory.EnumerateFiles(_root, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<QuarantineManifest>(File.ReadAllText(manifestPath));
                if (manifest is not null) list.Add(new QuarantineItem(manifestPath, manifest));
            }
            catch { }
        }
        return list.OrderByDescending(x => x.Manifest.Created).ToList();
    }

    public Task RestoreAsync(QuarantineItem item)
    {
        if (_platform.GetObsProcesses().Count > 0) throw new InvalidOperationException("Close OBS before restoring a quarantined plugin.");
        var m = item.Manifest;
        if (!Directory.Exists(m.QuarantinedPath)) throw new DirectoryNotFoundException("The quarantined plugin directory is missing.");
        if (Directory.Exists(m.OriginalPath)) throw new IOException("A plugin already exists at the original location. Mission Control will not overwrite it.");
        Directory.CreateDirectory(Path.GetDirectoryName(m.OriginalPath)!);
        Directory.Move(m.QuarantinedPath, m.OriginalPath);
        try { Directory.Delete(Path.GetDirectoryName(item.ManifestPath)!, recursive: true); } catch { }
        _logger.Success($"Plugin restored: {m.Name}");
        return Task.CompletedTask;
    }

    public sealed record QuarantineManifest(string Name, string OriginalPath, string QuarantinedPath, DateTimeOffset Created);
    public sealed record QuarantineItem(string ManifestPath, QuarantineManifest Manifest)
    {
        public string Display => $"{Manifest.Created.LocalDateTime:g} • {Manifest.Name}";
    }
}
