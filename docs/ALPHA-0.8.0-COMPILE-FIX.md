# 0.8.0-alpha.4 Compile Fix

GitHub Actions reported:

```text
CS1061: 'GitHubReleaseInfo' does not contain a definition for 'Url'
```

`GitHubReleaseInfo` defines:

```csharp
public sealed record GitHubReleaseInfo(
    string Version,
    string TagName,
    string ReleaseUrl,
    DateTimeOffset? PublishedAt);
```

The stale call in `CreatorSoftwareUpdateService` was corrected from:

```csharp
release?.Url
```

to:

```csharp
release?.ReleaseUrl
```

This keeps Mix It Up and Firebot update links pointed at the verified GitHub release page.
