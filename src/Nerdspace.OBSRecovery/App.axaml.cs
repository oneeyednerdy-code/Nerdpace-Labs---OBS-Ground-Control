using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nerdspace.OBSRecovery.Services;
using Nerdspace.OBSRecovery.UI;

namespace Nerdspace.OBSRecovery;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var splash = new SplashWindow();
            desktop.MainWindow = splash;

            splash.Opened += async (_, _) =>
            {
                try
                {
                    await Task.Delay(180);
                    splash.SetStatus("Loading Ground Control services…");
                    _mainWindow = AppServices.CreateMainWindow();
                    await Task.Delay(180);
                    splash.SetStatus("Opening mission control…");
                    desktop.MainWindow = _mainWindow;
                    _mainWindow.Show();
                    if (Environment.GetCommandLineArgs().Any(x => x.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
                        _mainWindow.Hide();
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    await Task.Delay(120);
                    splash.Close();
                }
                catch
                {
                    splash.Close();
                    desktop.Shutdown();
                    throw;
                }
            };

            splash.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMain()
    {
        if (_mainWindow is null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e) => ShowMain();
    private void TrayOpen_OnClick(object? sender, EventArgs e) => ShowMain();
    private void TrayLaunch_OnClick(object? sender, EventArgs e) => _mainWindow?.LaunchObsFromTray();
    private async void TrayRestart_OnClick(object? sender, EventArgs e)
    {
        if (_mainWindow is not null) await _mainWindow.RestartObsFromTrayAsync();
    }

    private void TrayExit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow?.PrepareForShutdown();
            desktop.Shutdown();
        }
    }

    private async void About_OnClick(object? sender, EventArgs e)
    {
        if (_mainWindow is null) return;
        var dialog = new Window
        {
            Title = "About OBS Ground Control",
            Width = 440,
            Height = 240,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(28),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "NERDSPACE LABS", Foreground = Avalonia.Media.Brush.Parse("#FF7900"), FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = "OBS Ground Control", FontSize = 28, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = $"Nerdspace Labs by OneEyedNerdy • {AppVersion.DisplayVersion}", Foreground = Avalonia.Media.Brush.Parse("#A9AFBC") },
                    new TextBlock { Text = "A local, privacy-first pre-flight, maintenance, backup, recovery, diagnostics, and workstation health utility for OBS Studio.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0,8,0,0) }
                }
            }
        };
        await dialog.ShowDialog(_mainWindow);
    }
}
