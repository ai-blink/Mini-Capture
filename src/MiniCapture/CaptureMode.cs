using MiniCapture.Resources;

namespace MiniCapture;

public enum CaptureMode
{
    Drag,
    Window,
    FullScreen,
    Timer
}

public static class CaptureModeInfo
{
    public static CaptureMode FromKey(string? key)
    {
        return key switch
        {
            "Window" => CaptureMode.Window,
            "FullScreen" => CaptureMode.FullScreen,
            "Timer" => CaptureMode.Timer,
            _ => CaptureMode.Drag
        };
    }

    public static string DisplayName(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => Strings.Mode_Name_Window,
            CaptureMode.FullScreen => Strings.Mode_Name_FullScreen,
            CaptureMode.Timer => Strings.Mode_Name_Timer,
            _ => Strings.Mode_Name_Drag
        };
    }

    public static string ShortLabel(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => "WIN",
            CaptureMode.FullScreen => "ALL",
            CaptureMode.Timer => "TMR",
            _ => "DRG"
        };
    }

    public static string PlaceholderStatus(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => Strings.Mode_Placeholder_Window,
            CaptureMode.FullScreen => Strings.Mode_Placeholder_FullScreen,
            CaptureMode.Timer => Strings.Mode_Placeholder_Timer,
            _ => Strings.Mode_Placeholder_Drag
        };
    }
}
