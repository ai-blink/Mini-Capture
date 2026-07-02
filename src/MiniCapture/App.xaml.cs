using System.Windows;

namespace MiniCapture;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EnsureWindowsDirectoryEnvironment();
        SettingsPathHelper.EnsureAppDataDirectory();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
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
