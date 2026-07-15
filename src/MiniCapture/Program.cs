using System.Runtime.InteropServices;

namespace MiniCapture;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        DpiAwarenessApi.TryEnablePerMonitorV2();

        if (!SingleInstanceCoordinator.TryCreatePrimary(out var instance))
        {
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
