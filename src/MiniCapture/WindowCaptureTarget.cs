using System.Drawing;

namespace MiniCapture;

public readonly record struct WindowCaptureTarget(IntPtr Hwnd, Rectangle Bounds, string Title);
