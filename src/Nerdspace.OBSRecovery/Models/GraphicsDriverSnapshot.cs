namespace Nerdspace.OBSRecovery.Models;

public sealed record GraphicsAdapterInfo(
    string Vendor,
    string Name,
    string DriverVersion,
    double? TemperatureC = null,
    double? UtilizationPercent = null,
    double? MemoryUsedMb = null,
    double? MemoryTotalMb = null)
{
    public string Display
    {
        get
        {
            var parts = new List<string> { $"{Vendor} {Name}".Trim(), $"driver {DriverVersion}" };
            if (TemperatureC.HasValue) parts.Add($"{TemperatureC.Value:F0}°C");
            if (UtilizationPercent.HasValue) parts.Add($"{UtilizationPercent.Value:F0}% GPU");
            if (MemoryUsedMb.HasValue && MemoryTotalMb.HasValue)
                parts.Add($"{MemoryUsedMb.Value:F0}/{MemoryTotalMb.Value:F0} MB VRAM");
            return string.Join(" • ", parts);
        }
    }
}

public sealed record GraphicsDriverSnapshot(
    bool Supported,
    IReadOnlyList<GraphicsAdapterInfo> Adapters,
    string Status,
    string Detail)
{
    public bool HasNvidia => Adapters.Any(x => x.Vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase));
    public bool HasAmd => Adapters.Any(x => x.Vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase));
    public string Summary => Adapters.Count == 0 ? Status : string.Join(" | ", Adapters.Select(x => x.Display));
}
