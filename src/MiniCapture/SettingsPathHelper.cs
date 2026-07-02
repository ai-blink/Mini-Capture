using System.IO;

namespace MiniCapture;

public static class SettingsPathHelper
{
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniCapture");

    public static string DefaultCaptureDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "MiniCapture");

    public static string CaptureDirectoryFor(DateTime timestamp) =>
        Path.Combine(DefaultCaptureDirectory, timestamp.ToString("yyyy"), timestamp.ToString("MM"), timestamp.ToString("dd"));

    public static void EnsureAppDataDirectory()
    {
        Directory.CreateDirectory(AppDataDirectory);
    }
}
