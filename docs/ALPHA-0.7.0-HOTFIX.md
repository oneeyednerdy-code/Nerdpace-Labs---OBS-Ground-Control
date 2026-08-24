# v0.7.0-alpha.11 Build Hotfix

This alpha hotfix fixes two C# compiler errors that appeared after moving the project to .NET 10.

## Fixed

`ObsConfigurationInspectorService.cs`

```csharp
common.Split(new[] { ' ', '/', '.' }, StringSplitOptions.RemoveEmptyEntries)
```

`PluginInventoryService.cs`

```csharp
pluginName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
```

The previous form passed several individual `char` arguments followed by `StringSplitOptions`, which does not match a valid `string.Split` overload.
