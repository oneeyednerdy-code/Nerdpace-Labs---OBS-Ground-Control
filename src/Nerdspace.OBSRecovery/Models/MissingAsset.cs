namespace Nerdspace.OBSRecovery.Models;

public sealed record MissingAsset(string SceneCollection, string Path)
{
    public string Display => $"{SceneCollection} • {Path}";
}
