namespace Nerdspace.OBSRecovery.Models;

public sealed record BackupInfo(
    string Path,
    string Name,
    DateTimeOffset Created,
    long SizeBytes,
    int FileCount)
{
    public string Display => $"{Created.LocalDateTime:g} • {Name} • {FileCount} files • {SizeBytes / 1024d / 1024d:F1} MB";
}

public sealed record BackupManifest(
    string AppVersion,
    string Platform,
    DateTimeOffset Created,
    IReadOnlyList<BackupManifestFile> Files,
    IReadOnlyList<string> Exclusions);

public sealed record BackupManifestFile(string RelativePath, long SizeBytes, string Sha256);
