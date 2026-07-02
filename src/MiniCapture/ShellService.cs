using System.Diagnostics;
using System.IO;

namespace MiniCapture;

public static class ShellService
{
    public static void OpenFile(string path)
    {
        StartShellProcess(path);
    }

    public static void OpenContainingFolder(string path)
    {
        if (!File.Exists(path))
        {
            StartShellProcess(SettingsPathHelper.DefaultCaptureDirectory);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }

    private static void StartShellProcess(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
