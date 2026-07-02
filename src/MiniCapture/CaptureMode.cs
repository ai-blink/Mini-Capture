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
            CaptureMode.Window => "창 지정",
            CaptureMode.FullScreen => "전체",
            CaptureMode.Timer => "타이머",
            _ => "드래그"
        };
    }

    public static string ShortLabel(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => "WIN",
            CaptureMode.FullScreen => "ALL",
            CaptureMode.Timer => "3S",
            _ => "DRG"
        };
    }

    public static string PlaceholderStatus(CaptureMode mode)
    {
        return $"{DisplayName(mode)} 모드 선택됨. 실제 캡처 엔진은 P1에서 연결됩니다.";
    }
}
