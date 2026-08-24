using Avalonia.Controls;
using Avalonia.Threading;

namespace Nerdspace.OBSRecovery.UI;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _animationTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private int _dotCount;

    public SplashWindow()
    {
        InitializeComponent();
        _animationTimer.Tick += (_, _) =>
        {
            _dotCount = (_dotCount + 1) % 4;
            LaunchStatusText.Text = "Starting Ground Control" + new string('.', _dotCount);
        };
        Opened += (_, _) => _animationTimer.Start();
        Closed += (_, _) => _animationTimer.Stop();
    }

    public void SetStatus(string text)
    {
        _animationTimer.Stop();
        LaunchStatusText.Text = text;
    }
}
