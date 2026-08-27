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

    private static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DimScreen"
    };

    public static WindowCaptureTarget? FindTargetAt(Point screenPoint, int currentProcessId)
    {
        return FindTargetAt(screenPoint, GetSelectableTargetsInZOrder(currentProcessId));
    }

    public static IReadOnlyList<WindowCaptureTarget> GetSelectableTargetsInZOrder(int currentProcessId)
    {
        var targets = new List<WindowCaptureTarget>();
        var excludedTargets = MiniCaptureSettingsStore.Load()
            .WindowCaptureExclusionTargets;
        foreach (var hwnd in NativeWindowApi.EnumerateTopLevelWindows())
        {
            if (!TryCreateTarget(hwnd, currentProcessId, excludedTargets, out var target))
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

    private static bool TryCreateTarget(
        IntPtr hwnd,
        int currentProcessId,
        IReadOnlyList<WindowCaptureExclusionTarget> excludedTargets,
        out WindowCaptureTarget target)
    {
        target = default;
        if (!IsCandidate(hwnd, currentProcessId, excludedTargets))
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

    private static bool IsCandidate(
        IntPtr hwnd,
        int currentProcessId,
        IReadOnlyList<WindowCaptureExclusionTarget> excludedTargets)
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

        var process = NativeWindowApi.GetProcessInfo(hwnd);
        if (IsIgnoredProcessName(process.Name) ||
            IsExcludedWindowTarget(process.ExecutablePath, process.Name, excludedTargets))
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

    internal static bool IsIgnoredProcessName(string? processName)
    {
        return !string.IsNullOrWhiteSpace(processName) && IgnoredProcessNames.Contains(processName);
    }

    internal static bool IsExcludedExecutablePath(string? executablePath, ISet<string> excludedExecutablePaths)
    {
        return MiniCaptureSettings.TryNormalizeWindowCaptureExcludedExecutablePath(executablePath, out var normalizedPath) &&
            excludedExecutablePaths.Contains(normalizedPath);
    }

    internal static bool IsExcludedWindowTarget(
        string? executablePath,
        string? processName,
        IReadOnlyList<WindowCaptureExclusionTarget> excludedTargets)
    {
        foreach (var target in excludedTargets)
        {
            if (target.MatchMode == WindowCaptureExclusionMatchMode.FilePath &&
                MiniCaptureSettings.TryNormalizeWindowCaptureExcludedExecutablePath(executablePath, out var normalizedPath) &&
                string.Equals(target.ExecutablePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (target.MatchMode == WindowCaptureExclusionMatchMode.ProcessName &&
                MiniCaptureSettings.TryNormalizeWindowCaptureExcludedProcessName(processName, out var normalizedProcessName) &&
                string.Equals(target.ProcessName, normalizedProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
