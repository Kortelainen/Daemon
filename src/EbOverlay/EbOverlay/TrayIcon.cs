using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace EbOverlay;

/// <summary>
/// System tray icon with right-click menu.
/// Provides pause/resume and exit — the primary user control surface for the overlay.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly OverlayWindow _overlay;
    private bool _paused;

    public TrayIcon(OverlayWindow overlay)
    {
        _overlay = overlay;

        _notify = new NotifyIcon
        {
            Text    = "EbOverlay",
            Icon    = CreateFallbackIcon(),
            Visible = true,
        };

        _notify.ContextMenuStrip = BuildMenu();

        // Double-click tray icon = toggle pause, same as the menu item
        _notify.DoubleClick += (_, _) => TogglePause();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var header = new ToolStripLabel("EbOverlay")
        {
            Font = new Font("Courier New", 9f, System.Drawing.FontStyle.Bold),
            ForeColor = Color.FromArgb(0x66, 0xFF, 0x99),
        };

        var pauseItem = new ToolStripMenuItem("Pause overlay");
        pauseItem.Click += (_, _) => TogglePause();

        var fullscreenItem = new ToolStripMenuItem("Hide on fullscreen")
        {
            CheckOnClick = true,
            Checked      = _overlay.FullscreenHideEnabled,
        };
        fullscreenItem.CheckedChanged += (_, _) =>
            _overlay.FullscreenHideEnabled = fullscreenItem.Checked;

        menu.Opening += (_, _) =>
        {
            pauseItem.Text          = _paused ? "Resume overlay" : "Pause overlay";
            fullscreenItem.Checked  = _overlay.FullscreenHideEnabled;
        };

        var separator = new ToolStripSeparator();

#if DEBUG
        var testIconsItem = new ToolStripMenuItem("Test status icons")
        {
            CheckOnClick = true,
            Checked      = false,
        };
        testIconsItem.CheckedChanged += (_, _) =>
            _overlay.StatusIconZone?.SetTestMode(testIconsItem.Checked);
#endif

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _notify.Visible = false;
            System.Windows.Application.Current.Shutdown();
        };

        menu.Items.Add(header);
        menu.Items.Add(separator);
        menu.Items.Add(pauseItem);
        menu.Items.Add(fullscreenItem);
#if DEBUG
        menu.Items.Add(testIconsItem);
#endif
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _overlay.Dispatcher.Invoke(() =>
        {
            _overlay.Visibility = _paused ? Visibility.Hidden : Visibility.Visible;
        });
        _notify.Text = _paused ? "EbOverlay — paused" : "EbOverlay";
    }

    // Generates a minimal colored icon at runtime so we don't need an .ico file yet.
    // Replace with a real icon file in M6/M8 when assets are ready.
    private static Icon CreateFallbackIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.FillEllipse(new SolidBrush(Color.FromArgb(0x66, 0xFF, 0x99)), 2, 2, 12, 12);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }
}
