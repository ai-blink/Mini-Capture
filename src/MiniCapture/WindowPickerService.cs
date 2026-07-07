using System.Drawing;

namespace MiniCapture;

public static class WindowPickerService
{
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
        return FindTargetAt(screenPoint, GetSelectableTargetsInZOrder(currentProcessId));
    }

    public static IReadOnlyList<WindowCaptureTarget> GetSelectableTargetsInZOrder(int currentProcessId)
    {
        var targets = new List<WindowCaptureTarget>();
        foreach (var hwnd in NativeWindowApi.EnumerateTopLevelWindows())
        {
            if (!TryCreateTarget(hwnd, currentProcessId, out var target))
            {
                continue;
            }

            targets.Add(target);
        }

        return targets;
    }

    public static WindowCaptureTarget? FindTargetAt(Point screenPoint, IReadOnlyList<WindowCaptureTarget> targets)
    {
        return SelectTargetAt(screenPoint, targets);
    }

    internal static WindowCaptureTarget? SelectTargetAt(Point screenPoint, IEnumerable<WindowCaptureTarget> targets)
    {
        foreach (var target in targets)
        {
            if (!target.Bounds.Contains(screenPoint))
            {
                continue;
            }

            return target;
        }

        return null;
    }

    private static bool TryCreateTarget(IntPtr hwnd, int currentProcessId, out WindowCaptureTarget target)
    {
        target = default;
        if (!IsCandidate(hwnd, currentProcessId))
        {
            return false;
        }

        if (!NativeWindowApi.TryGetVisibleBounds(hwnd, out var bounds))
        {
            return false;
        }

        if (bounds.Width < 24 || bounds.Height < 24)
        {
            return false;
        }

        var title = NativeWindowApi.GetTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = NativeWindowApi.GetClassName(hwnd);
        }

        target = new WindowCaptureTarget(hwnd, bounds, title);
        return true;
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

}
