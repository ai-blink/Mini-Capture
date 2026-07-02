using System.Drawing;

namespace MiniCapture;

public static class WindowPickerService
{
    private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd"
    };

    public static WindowCaptureTarget? FindTargetAt(Point screenPoint, int currentProcessId)
    {
        foreach (var hwnd in NativeWindowApi.EnumerateTopLevelWindows())
        {
            if (!IsCandidate(hwnd, currentProcessId))
            {
                continue;
            }

            if (!NativeWindowApi.TryGetVisibleBounds(hwnd, out var bounds) || !bounds.Contains(screenPoint))
            {
                continue;
            }

            var title = NativeWindowApi.GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = NativeWindowApi.GetClassName(hwnd);
            }

            return new WindowCaptureTarget(hwnd, bounds, title);
        }

        return null;
    }

    private static bool IsCandidate(IntPtr hwnd, int currentProcessId)
    {
        if (!NativeWindowApi.IsVisible(hwnd))
        {
            return false;
        }

        if (NativeWindowApi.GetProcessId(hwnd) == currentProcessId)
        {
            return false;
        }

        return !IgnoredWindowClasses.Contains(NativeWindowApi.GetClassName(hwnd));
    }
}
