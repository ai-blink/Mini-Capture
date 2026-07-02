using System.Drawing;

namespace MiniCapture;

public static class WindowPickerService
{
    private const double FullScreenCoverageThreshold = 0.9;

    private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "TfrmFullScreen"
    };

    private static readonly HashSet<string> IgnoredWindowTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "GazeScroll",
        "Shell Handwriting Canvas"
    };

    public static WindowCaptureTarget? FindTargetAt(Point screenPoint, int currentProcessId)
    {
        var virtualBounds = ScreenCaptureService.GetVirtualScreenBounds();
        var fullScreenCandidates = new List<WindowCaptureTarget>();

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

            if (bounds.Width < 24 || bounds.Height < 24)
            {
                continue;
            }

            var title = NativeWindowApi.GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = NativeWindowApi.GetClassName(hwnd);
            }

            var target = new WindowCaptureTarget(hwnd, bounds, title);
            if (!CoversMostOfVirtualScreen(bounds, virtualBounds))
            {
                return target;
            }

            fullScreenCandidates.Add(target);
        }

        return fullScreenCandidates.FirstOrDefault();
    }

    private static bool IsCandidate(IntPtr hwnd, int currentProcessId)
    {
        if (!NativeWindowApi.IsVisible(hwnd))
        {
            return false;
        }

        if (NativeWindowApi.IsMinimized(hwnd) || NativeWindowApi.IsCloaked(hwnd))
        {
            return false;
        }

        if (NativeWindowApi.GetProcessId(hwnd) == currentProcessId)
        {
            return false;
        }

        var className = NativeWindowApi.GetClassName(hwnd);
        if (IgnoredWindowClasses.Contains(className))
        {
            return false;
        }

        return !IgnoredWindowTitles.Contains(NativeWindowApi.GetTitle(hwnd));
    }

    private static bool CoversMostOfVirtualScreen(Rectangle bounds, Rectangle virtualBounds)
    {
        if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
        {
            return false;
        }

        return bounds.Width >= virtualBounds.Width * FullScreenCoverageThreshold &&
            bounds.Height >= virtualBounds.Height * FullScreenCoverageThreshold;
    }
}
