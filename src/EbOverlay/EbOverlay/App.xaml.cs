using System.Windows;
using Application = System.Windows.Application;

namespace Daemon;

public partial class App : Application
{
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent WPF from shutting down when overlay is hidden
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = new SettingsStore();
        settings.Load();

        var overlay = new OverlayWindow(settings);
        overlay.Show();

        _trayIcon = new TrayIcon(overlay, settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
