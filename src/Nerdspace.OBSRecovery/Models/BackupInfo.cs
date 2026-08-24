namespace Nerdspace.OBSRecovery.Models;

public sealed record BackupInfo(
    string Path,
    string Name,
    DateTimeOffset Created,
    long SizeBytes,
    int FileCount,
    int MediaFileCount = 0,
    long MediaSizeBytes = 0)
{
    public string Display
    {
        get
        {
            var media = MediaFileCount > 0
                ? $" • {MediaFileCount} media • {MediaSizeBytes / 1024d / 1024d:F1} MB media"
                : string.Empty;
            return $"{Created.LocalDateTime:g} • {Name} • {FileCount} config files{media} • {SizeBytes / 1024d / 1024d:F1} MB";
        }
    }
}

public sealed record BackupManifest(
    string AppVersion,
    string Platform,
    DateTimeOffset Created,
    IReadOnlyList<BackupManifestFile> Files,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<BackupMediaFile>? MediaFiles = null,
    IReadOnlyList<string>? MissingMedia = null);

public sealed record BackupManifestFile(string RelativePath, long SizeBytes, string Sha256);

public sealed record BackupMediaFile(
    string OriginalPath,
    string ArchivePath,
    long SizeBytes,
    string Sha256,
    string SceneCollection);

public sealed record SceneMediaReference(
    string SceneCollection,
    string Path,
    bool Exists,
    long SizeBytes);

public sealed record SceneMediaEstimate(
    int ExistingFileCount,
    long ExistingSizeBytes,
    int MissingFileCount)
{
    public string Display => ExistingFileCount == 0 && MissingFileCount == 0
        ? "Nothing found — No local image/audio/video files are referenced by the current OBS scene collections."
        : $"{ExistingFileCount} local media file(s) • {ExistingSizeBytes / 1024d / 1024d:F1} MB" +
          (MissingFileCount > 0 ? $" • {MissingFileCount} missing reference(s) will be recorded but cannot be backed up." : string.Empty);
}
