using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Nerdspace.OBSRecovery.Models;
using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class BackupService
{
    private const string ManifestEntryName = "mission-control-manifest.json";
    private const string LegacyManifestEntryName = "ground-control-manifest.json";
    private readonly AppSettings _settings;
    private readonly IObsPlatformService _platform;
    private readonly LoggingService _logger;
    private readonly SceneAssetScannerService _assets;

    public BackupService(AppSettings settings, IObsPlatformService platform, LoggingService logger, SceneAssetScannerService assets)
    {
        _settings = settings; _platform = platform; _logger = logger; _assets = assets;
    }

    public string BackupDirectory
    {
        get
        {
            var path = string.IsNullOrWhiteSpace(_settings.BackupDirectory)
                ? Path.Combine(_platform.GetSettingsDirectory(), "Backups") : _settings.BackupDirectory;
            Directory.CreateDirectory(path); return path;
        }
    }

    public SceneMediaEstimate EstimateSceneMedia() => _assets.EstimateMedia();

    public Task<BackupInfo> CreateBackupAsync(string reason = "Manual", CancellationToken cancellationToken = default)
        => CreateBackupAsync(reason, false, cancellationToken);

    public async Task<BackupInfo> CreateBackupAsync(string reason, bool includeSceneMedia, CancellationToken cancellationToken = default)
    {
        var config = _platform.GetObsConfigDirectory();
        if (!Directory.Exists(config)) throw new DirectoryNotFoundException("OBS configuration directory was not found.");
        var safeReason = string.Concat(reason.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(safeReason)) safeReason = "Backup";
        var name = $"Streamer-Mission-Control-{DateTime.Now:yyyyMMdd-HHmmss}-{safeReason}.zip";
        var path = Path.Combine(BackupDirectory, name);
        var manifestFiles = new List<BackupManifestFile>();
        var mediaFiles = new List<BackupMediaFile>();
        var missingMedia = new List<string>();
        var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(config, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(config, file).Replace('\\', '/');
            if (ShouldExclude(relative)) { exclusions.Add(relative); continue; }
            try
            {
                archive.CreateEntryFromFile(file, $"obs-config/{relative}", CompressionLevel.Optimal);
                var info = new FileInfo(file);
                manifestFiles.Add(new BackupManifestFile(relative, info.Length, await Sha256Async(file, cancellationToken)));
            }
            catch (Exception ex) { _logger.Warn($"Skipped backup file {relative}: {ex.Message}"); }
        }

        if (includeSceneMedia)
        {
            var refs = _assets.ScanMediaReferences();
            var mediaIndex = 0;
            foreach (var media in refs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!media.Exists) { missingMedia.Add(media.Path); continue; }
                try
                {
                    var fileName = SanitizeArchiveFileName(Path.GetFileName(media.Path));
                    var archivePath = $"scene-media/{mediaIndex:D5}-{fileName}"; mediaIndex++;
                    archive.CreateEntryFromFile(media.Path, archivePath, CompressionLevel.Optimal);
                    mediaFiles.Add(new BackupMediaFile(media.Path, archivePath, media.SizeBytes,
                        await Sha256Async(media.Path, cancellationToken), media.SceneCollection));
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Skipped scene media {Path.GetFileName(media.Path)}: {ex.Message}");
                    missingMedia.Add(media.Path);
                }
            }
        }

        var manifest = new BackupManifest(AppVersion.Version, _platform.PlatformName, DateTimeOffset.Now,
            manifestFiles, exclusions.OrderBy(x => x).ToList(), mediaFiles,
            missingMedia.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList());
        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using (var stream = manifestEntry.Open())
            await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);

        var fileInfo = new FileInfo(path);
        var backup = new BackupInfo(path, Path.GetFileNameWithoutExtension(path), manifest.Created, fileInfo.Length,
            manifestFiles.Count, mediaFiles.Count, mediaFiles.Sum(x => x.SizeBytes));
        _logger.Success($"OBS backup created: {path}. {mediaFiles.Count} scene media file(s) included; {missingMedia.Count} missing media reference(s) recorded.");
        return backup;
    }

    public IReadOnlyList<BackupInfo> ListBackups()
    {
        var results = new List<BackupInfo>();
        foreach (var path in Directory.EnumerateFiles(BackupDirectory, "*.zip", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                using var archive = ZipFile.OpenRead(path); var manifest = ReadManifest(archive); var info = new FileInfo(path);
                var media = manifest?.MediaFiles ?? Array.Empty<BackupMediaFile>();
                results.Add(new BackupInfo(path, Path.GetFileNameWithoutExtension(path), manifest?.Created ?? info.CreationTimeUtc,
                    info.Length, manifest?.Files.Count ?? 0, media.Count, media.Sum(x => x.SizeBytes)));
            }
            catch { }
        }
        return results;
    }

    public Task RestoreAsync(BackupInfo backup, BackupRestoreScope scope, Func<bool> isObsRunning, CancellationToken cancellationToken = default)
        => RestoreAsync(backup, scope, isObsRunning, false, false, cancellationToken);

    public async Task RestoreAsync(BackupInfo backup, BackupRestoreScope scope, Func<bool> isObsRunning,
        bool restoreSceneMedia, bool overwriteExistingMedia, CancellationToken cancellationToken = default)
    {
        if (isObsRunning()) throw new InvalidOperationException("Close OBS before restoring a backup.");
        if (!File.Exists(backup.Path)) throw new FileNotFoundException("The selected backup no longer exists.", backup.Path);
        if (_settings.AutoBackupBeforeRestore) await CreateBackupAsync("Pre-Restore-Safety", false, cancellationToken);
        var config = _platform.GetObsConfigDirectory(); Directory.CreateDirectory(config);
        var configRoot = Path.GetFullPath(config).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(backup.Path);
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("obs-config/", StringComparison.Ordinal) && !string.IsNullOrEmpty(e.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeSlash = entry.FullName["obs-config/".Length..];
            if (!MatchesScope(relativeSlash, scope)) continue;
            var relative = relativeSlash.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(config, relative));
            if (!destination.StartsWith(configRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup contains an unsafe configuration path and was not restored.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        var mediaRestored = 0; var mediaSkipped = 0;
        if (restoreSceneMedia)
        {
            var manifest = ReadManifest(archive) ?? throw new InvalidDataException("This backup does not contain a Mission Control manifest.");
            foreach (var media in manifest.MediaFiles ?? Array.Empty<BackupMediaFile>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Path.IsPathRooted(media.OriginalPath)) { mediaSkipped++; continue; }
                var entry = archive.GetEntry(media.ArchivePath);
                if (entry is null) { mediaSkipped++; continue; }
                var destination = Path.GetFullPath(media.OriginalPath);
                if (File.Exists(destination) && !overwriteExistingMedia) { mediaSkipped++; continue; }
                try
                {
                    var directory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                    entry.ExtractToFile(destination, overwrite: overwriteExistingMedia); mediaRestored++;
                }
                catch (Exception ex)
                {
                    mediaSkipped++; _logger.Warn($"Could not restore scene media {Path.GetFileName(destination)}: {ex.Message}");
                }
            }
        }
        _logger.Success($"Restored {scope} from {backup.Name}. " +
            (restoreSceneMedia ? $"Scene media restored: {mediaRestored}; skipped: {mediaSkipped}. " : string.Empty) +
            "Stream credentials were not included in the backup by design.");
    }

    public async Task<ConfigDiff> CompareToCurrentAsync(BackupInfo backup, CancellationToken cancellationToken = default)
    {
        using var archive = ZipFile.OpenRead(backup.Path);
        var manifest = ReadManifest(archive) ?? throw new InvalidDataException("This backup does not contain a Mission Control manifest.");
        var oldMap = manifest.Files.ToDictionary(x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<string, BackupManifestFile>(StringComparer.OrdinalIgnoreCase);
        var config = _platform.GetObsConfigDirectory();
        if (Directory.Exists(config))
        {
            foreach (var file in Directory.EnumerateFiles(config, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(config, file).Replace('\\', '/');
                if (ShouldExclude(relative)) continue;
                var info = new FileInfo(file);
                current[relative] = new BackupManifestFile(relative, info.Length, await Sha256Async(file, cancellationToken));
            }
        }
        var added = current.Keys.Except(oldMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var removed = oldMap.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var changed = current.Keys.Intersect(oldMap.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(k => !current[k].Sha256.Equals(oldMap[k].Sha256, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x).ToList();
        return new ConfigDiff(added, removed, changed);
    }

    private static bool MatchesScope(string relative, BackupRestoreScope scope)
    {
        var p = relative.Replace('\\','/');
        return scope switch
        {
            BackupRestoreScope.Everything => true,
            BackupRestoreScope.SceneCollections => p.StartsWith("basic/scenes/", StringComparison.OrdinalIgnoreCase),
            BackupRestoreScope.Profiles => p.StartsWith("basic/profiles/", StringComparison.OrdinalIgnoreCase),
            BackupRestoreScope.PluginSettings => p.StartsWith("plugin_config/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static BackupManifest? ReadManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry(ManifestEntryName) ?? archive.GetEntry(LegacyManifestEntryName);
        if (entry is null) return null;
        using var stream = entry.Open(); return JsonSerializer.Deserialize<BackupManifest>(stream);
    }

    private static bool ShouldExclude(string relative)
    {
        var p = relative.Replace('\\','/').ToLowerInvariant(); var file = Path.GetFileName(p);
        if (file is "service.json" or "cookies" or "cookies-journal") return true;
        if (p.StartsWith("logs/") || p.StartsWith("crashes/")) return true;
        if (p.Contains("/cache/") || p.Contains("/code cache/") || p.Contains("/gpucache/") || p.Contains("/blob_storage/")) return true;
        if (p.Contains("obs-browser") && (p.Contains("cookies") || p.Contains("cache") || p.Contains("local storage"))) return true;
        return false;
    }

    private static string SanitizeArchiveFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "media.bin" : safe;
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create(); var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
