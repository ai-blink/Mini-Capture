using System.Windows;
using Forms = System.Windows.Forms;

namespace MiniCapture;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public bool IsExitRequested { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EnsureWindowsDirectoryEnvironment();
        SettingsPathHelper.EnsureAppDataDirectory();
        CreateTrayIcon();

        var window = new MainWindow();
        _mainWindow = window;
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }

    public void ShowCaptureWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
        }

        _mainWindow.ShowFromTray();
    }

    public void ExitApplication()
    {
        IsExitRequested = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Mini Capture",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowCaptureWindow);
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("열기", null, (_, _) => Dispatcher.Invoke(ShowCaptureWindow));
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        return menu;
    }

    private static void EnsureWindowsDirectoryEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            return;
        }

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            Environment.SetEnvironmentVariable("windir", windowsDirectory);
        }
    }
}
