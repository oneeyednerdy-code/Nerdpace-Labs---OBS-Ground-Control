using Nerdspace.OBSRecovery.Platform;

namespace Nerdspace.OBSRecovery.Services;

public sealed class LoggingService
{
    private readonly object _gate = new();

    public LoggingService(IObsPlatformService platform)
    {
        LogDirectory = Path.Combine(platform.GetSettingsDirectory(), "Logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public string LogDirectory { get; }
    public event Action<string>? EntryWritten;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);
    public void Success(string message) => Write("SUCCESS", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {level} - {message}";
        var file = Path.Combine(LogDirectory, $"obs-recovery-{DateTime.Now:yyyy-MM-dd}.log");
        lock (_gate) File.AppendAllText(file, line + Environment.NewLine);
        EntryWritten?.Invoke(line);
    }
}
