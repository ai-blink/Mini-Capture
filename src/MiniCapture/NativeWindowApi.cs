using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniCapture;

internal static class NativeWindowApi
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    public static IReadOnlyList<IntPtr> EnumerateTopLevelWindows()
    {
        var windows = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static bool IsVisible(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero && IsWindowVisible(hwnd);
    }

    public static int GetProcessId(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var processId);
        return unchecked((int)processId);
    }

    public static bool IsMinimized(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero && IsIconic(hwnd);
    }

    public static bool IsCloaked(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        return DwmGetWindowAttributeInt(hwnd, DwmwaCloaked, out var cloaked, Marshal.SizeOf<int>()) == 0 &&
            cloaked != 0;
    }

    public static string GetTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public static string GetClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        _ = GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public static bool TryGetVisibleBounds(IntPtr hwnd, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (DwmGetWindowAttributeRect(hwnd, DwmwaExtendedFrameBounds, out var dwmRect, Marshal.SizeOf<NativeRect>()) == 0 &&
            TryToRectangle(dwmRect, out bounds))
        {
            return true;
        }

        return GetWindowRect(hwnd, out var windowRect) && TryToRectangle(windowRect, out bounds);
    }

    public static bool TryExcludeFromCapture(IntPtr hwnd)
    {
        return TrySetCaptureExclusion(hwnd, exclude: true);
    }

    public static bool TrySetCaptureExclusion(IntPtr hwnd, bool exclude)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return SetWindowDisplayAffinity(hwnd, exclude ? WdaExcludeFromCapture : WdaNone);
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static bool TryToRectangle(NativeRect rect, out Rectangle bounds)
    {
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = new Rectangle(rect.Left, rect.Top, width, height);
        return true;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttributeRect(IntPtr hwnd, int attribute, out NativeRect rect, int attributeSize);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttributeInt(IntPtr hwnd, int attribute, out int value, int attributeSize);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
