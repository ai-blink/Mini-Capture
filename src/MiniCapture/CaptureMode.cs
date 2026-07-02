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
        return mode switch
        {
            CaptureMode.Window => "창 지정 캡처는 P2에서 연결됩니다.",
            CaptureMode.Timer => "타이머 모드 선택됨. 3초 후 전체 화면을 캡처합니다.",
            CaptureMode.FullScreen => "전체 모드 선택됨. 전체 화면을 PNG로 저장합니다.",
            _ => "드래그 모드 선택됨. 저장할 영역을 드래그하세요."
        };
    }
}
