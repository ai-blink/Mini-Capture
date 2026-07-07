using System.IO;
using System.Windows;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace MiniCapture;

public partial class App : System.Windows.Application
{
    private readonly bool _openSettingsOnStartup;
    private readonly string? _startupImagePath;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public App()
        : this(Environment.GetCommandLineArgs().Skip(1).ToArray())
    {
    }

    public App(string[] args)
    {
        _openSettingsOnStartup = args.Any(arg => string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase));
        _startupImagePath = args.FirstOrDefault(CaptureFileIndex.IsImagePath);
    }

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

        if (_openSettingsOnStartup)
        {
            window.Loaded += (_, _) => Dispatcher.BeginInvoke(
                window.OpenSettings,
                DispatcherPriority.ApplicationIdle);
        }

        window.Show();

        if (_startupImagePath is not null)
        {
            Dispatcher.BeginInvoke(() => window.OpenViewer(_startupImagePath));
        }
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
            Icon = LoadTrayIcon(),
            Text = "Mini Capture",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowCaptureWindow);
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/App/MiniCapture.ico", UriKind.Absolute));
            if (resource?.Stream is not null)
            {
                using var icon = new Drawing.Icon(resource.Stream);
                return (Drawing.Icon)icon.Clone();
            }
        }
        catch (ArgumentException)
        {
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return Drawing.SystemIcons.Application;
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("열기", null, (_, _) => Dispatcher.Invoke(ShowCaptureWindow));
        menu.Items.Add("설정", null, (_, _) => Dispatcher.Invoke(ShowSettingsWindow));
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        return menu;
    }

    private void ShowSettingsWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
        }

        _mainWindow.OpenSettings();
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
