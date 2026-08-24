using System.Reflection;

namespace Nerdspace.OBSRecovery.Services;

public static class AppVersion
{
    public static string Version
        => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] ?? "0.0.0";

    public static string DisplayVersion => $"v{Version}";
}
