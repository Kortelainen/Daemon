using System.Windows;
using Application = System.Windows.Application;

namespace EbOverlay;

public partial class App : Application
{
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent WPF from shutting down when overlay is hidden
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var overlay = new OverlayWindow();
        overlay.Show();

        _trayIcon = new TrayIcon(overlay);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
