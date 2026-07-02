namespace MiniCapture;

public enum CaptureMode
{
    Drag,
    Window,
    FullScreen
}

public static class CaptureModeInfo
{
    public static CaptureMode FromKey(string? key)
    {
        return key switch
        {
            "Window" => CaptureMode.Window,
            "FullScreen" => CaptureMode.FullScreen,
            _ => CaptureMode.Drag
        };
    }

    public static string DisplayName(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => "창 지정",
            CaptureMode.FullScreen => "전체",
            _ => "드래그"
        };
    }

    public static string ShortLabel(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => "WIN",
            CaptureMode.FullScreen => "ALL",
            _ => "DRG"
        };
    }

    public static string PlaceholderStatus(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Window => "창 지정 모드 선택됨. 대상 창을 클릭하면 PNG로 저장합니다.",
            CaptureMode.FullScreen => "전체 모드 선택됨. 전체 화면을 PNG로 저장합니다.",
            _ => "드래그 모드 선택됨. 저장할 영역을 드래그하세요."
        };
    }
}
