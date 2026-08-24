using System.Diagnostics;
using Avalonia;

namespace Nerdspace.OBSRecovery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (TryRunWindowsElevatedHelper(args))
            return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static bool TryRunWindowsElevatedHelper(string[] args)
    {
        if (args.Length != 2 ||
            !args[0].Equals("--elevated-kill-obs", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(args[1], out var pid))
            return false;

        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.ProcessName.Equals("obs64", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = 4;
                return true;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            Environment.ExitCode = process.HasExited ? 0 : 5;
        }
        catch (ArgumentException)
        {
            Environment.ExitCode = 0; // OBS already exited.
        }
        catch
        {
            Environment.ExitCode = 1;
        }

        return true;
    }
}
