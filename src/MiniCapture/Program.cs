using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiniCapture;

public static class Program
{
    internal const string RestartWaitArgument = "--restart-wait";

    [STAThread]
    public static void Main(string[] args)
    {
        DpiAwarenessApi.TryEnablePerMonitorV2();
        LocalizationService.ApplyLanguage(MiniCaptureSettingsStore.Load().Language);
        WaitForRestartSourceExit(args);

        if (!SingleInstanceCoordinator.TryCreatePrimary(out var instance))
        {
            SingleInstanceCoordinator.GrantForegroundActivationToPrimary();
            SingleInstanceCoordinator.NotifyPrimary(args);
            return;
        }

        using (instance!)
        {
            var app = new App(args);
            instance!.StartListening(app.HandleActivationArguments);
            app.InitializeComponent();
            app.Run();
        }
    }

    private static void WaitForRestartSourceExit(string[] args)
    {
        var index = Array.IndexOf(args, RestartWaitArgument);
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out var processId))
        {
            return;
        }

        try
        {
            using var sourceProcess = Process.GetProcessById(processId);
            sourceProcess.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
            // The source process already exited before this instance started.
        }
    }
}

internal static class DpiAwarenessApi
{
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    public static void TryEnablePerMonitorV2()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2))
            {
                return;
            }
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }

        try
        {
            _ = SetProcessDpiAwareness(ProcessPerMonitorDpiAware);
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    private const int ProcessPerMonitorDpiAware = 2;

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int awareness);
}
