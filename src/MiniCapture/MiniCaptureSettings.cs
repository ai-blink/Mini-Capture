using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MiniCapture;

public sealed class MiniCaptureSettings
{
    public int TimerDelaySeconds { get; set; }

    public int ShortcutTimerDelaySeconds { get; set; } = 5;

    public bool QuickButtonVisible { get; set; } = true;

    public bool CaptureUiExcludedFromCapture { get; set; } = true;

    public List<WindowCaptureExclusionTarget> WindowCaptureExclusionTargets { get; set; } = [];

    [JsonPropertyName("WindowCaptureExcludedExecutablePaths")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LegacyWindowCaptureExcludedExecutablePaths { get; set; }

    public double? QuickButtonLeft { get; set; }

    public double? QuickButtonTop { get; set; }

    public string WindowCaptureHotkey { get; set; } = "Ctrl+Alt+W";

    public string RegionCaptureHotkey { get; set; } = "Ctrl+Alt+R";

    public string FullScreenCaptureHotkey { get; set; } = "Ctrl+Alt+F";

    public string TimerCaptureHotkey { get; set; } = "Ctrl+Alt+T";

    public List<string> WindowCaptureHotkeys { get; set; } = ["Ctrl+Alt+W", "", ""];

    public List<string> RegionCaptureHotkeys { get; set; } = ["Ctrl+Alt+R", "", ""];

    public List<string> FullScreenCaptureHotkeys { get; set; } = ["Ctrl+Alt+F", "", ""];

    public List<string> TimerCaptureHotkeys { get; set; } = ["Ctrl+Alt+T", "", ""];

    public double? ViewerLeft { get; set; }

    public double? ViewerTop { get; set; }

    public double? ViewerWidth { get; set; }

    public double? ViewerHeight { get; set; }

    public string? ViewerWindowState { get; set; }

    public ExplorerViewMode? ViewerExplorerViewMode { get; set; }

    public double? ViewerFolderTreeWidth { get; set; }

    public double? ViewerFileListWidth { get; set; }

    public List<ViewerImageViewState> ViewerImageStates { get; set; } = [];

    public MiniCaptureSettings Clone() =>
        new()
        {
            TimerDelaySeconds = TimerDelaySeconds,
            ShortcutTimerDelaySeconds = ShortcutTimerDelaySeconds,
            QuickButtonVisible = QuickButtonVisible,
            CaptureUiExcludedFromCapture = CaptureUiExcludedFromCapture,
            WindowCaptureExclusionTargets = NormalizeWindowCaptureExclusionTargets(
                    WindowCaptureExclusionTargets,
                    LegacyWindowCaptureExcludedExecutablePaths)
                .Select(target => target.Clone())
                .ToList(),
            LegacyWindowCaptureExcludedExecutablePaths = null,
            QuickButtonLeft = QuickButtonLeft,
            QuickButtonTop = QuickButtonTop,
            WindowCaptureHotkey = WindowCaptureHotkey,
            RegionCaptureHotkey = RegionCaptureHotkey,
            FullScreenCaptureHotkey = FullScreenCaptureHotkey,
            TimerCaptureHotkey = TimerCaptureHotkey,
            WindowCaptureHotkeys = NormalizeHotkeySlots(WindowCaptureHotkeys, WindowCaptureHotkey).ToList(),
            RegionCaptureHotkeys = NormalizeHotkeySlots(RegionCaptureHotkeys, RegionCaptureHotkey).ToList(),
            FullScreenCaptureHotkeys = NormalizeHotkeySlots(FullScreenCaptureHotkeys, FullScreenCaptureHotkey).ToList(),
            TimerCaptureHotkeys = NormalizeHotkeySlots(TimerCaptureHotkeys, TimerCaptureHotkey).ToList(),
            ViewerLeft = ViewerLeft,
            ViewerTop = ViewerTop,
            ViewerWidth = ViewerWidth,
            ViewerHeight = ViewerHeight,
            ViewerWindowState = ViewerWindowState,
            ViewerExplorerViewMode = ViewerExplorerViewMode,
            ViewerFolderTreeWidth = ViewerFolderTreeWidth,
            ViewerFileListWidth = ViewerFileListWidth,
            ViewerImageStates = ViewerImageStates
                .Select(state => state.Clone())
                .ToList()
        };

    public IReadOnlyList<string> GetHotkeySlots(CaptureHotkeyKind kind) =>
        kind switch
        {
            CaptureHotkeyKind.Window => NormalizeHotkeySlots(WindowCaptureHotkeys, WindowCaptureHotkey),
            CaptureHotkeyKind.Region => NormalizeHotkeySlots(RegionCaptureHotkeys, RegionCaptureHotkey),
            CaptureHotkeyKind.FullScreen => NormalizeHotkeySlots(FullScreenCaptureHotkeys, FullScreenCaptureHotkey),
            CaptureHotkeyKind.Timer => NormalizeHotkeySlots(TimerCaptureHotkeys, TimerCaptureHotkey),
            _ => []
        };

    public void SetHotkeySlots(CaptureHotkeyKind kind, IReadOnlyList<string> hotkeys)
    {
        var normalized = NormalizeHotkeySlots(hotkeys, string.Empty).ToList();
        switch (kind)
        {
            case CaptureHotkeyKind.Window:
                WindowCaptureHotkeys = normalized;
                WindowCaptureHotkey = normalized.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                break;
            case CaptureHotkeyKind.Region:
                RegionCaptureHotkeys = normalized;
                RegionCaptureHotkey = normalized.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                break;
            case CaptureHotkeyKind.FullScreen:
                FullScreenCaptureHotkeys = normalized;
                FullScreenCaptureHotkey = normalized.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                break;
            case CaptureHotkeyKind.Timer:
                TimerCaptureHotkeys = normalized;
                TimerCaptureHotkey = normalized.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                break;
        }
    }

    internal static IReadOnlyList<string> NormalizeWindowCaptureExcludedExecutablePaths(IReadOnlyList<string>? executablePaths)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (executablePaths is null)
        {
            return [];
        }

        foreach (var executablePath in executablePaths)
        {
            if (TryNormalizeWindowCaptureExcludedExecutablePath(executablePath, out var normalizedPath))
            {
                paths.Add(normalizedPath);
            }
        }

        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static IReadOnlyList<WindowCaptureExclusionTarget> NormalizeWindowCaptureExclusionTargets(
        IReadOnlyList<WindowCaptureExclusionTarget>? targets,
        IReadOnlyList<string>? legacyExecutablePaths)
    {
        var normalizedTargets = new List<WindowCaptureExclusionTarget>();

        void AddTarget(WindowCaptureExclusionTarget candidate)
        {
            var hasExecutablePath = TryNormalizeWindowCaptureExcludedExecutablePath(
                candidate.ExecutablePath,
                out var executablePath);
            var hasProcessName = TryNormalizeWindowCaptureExcludedProcessName(
                candidate.ProcessName,
                out var processName);
            if (!hasProcessName && hasExecutablePath)
            {
                hasProcessName = TryNormalizeWindowCaptureExcludedProcessName(
                    Path.GetFileNameWithoutExtension(executablePath),
                    out processName);
            }

            if (!hasProcessName)
            {
                return;
            }

            var normalized = new WindowCaptureExclusionTarget
            {
                ExecutablePath = hasExecutablePath ? executablePath : string.Empty,
                ProcessName = processName,
                MatchMode = hasExecutablePath && candidate.MatchMode == WindowCaptureExclusionMatchMode.FilePath
                    ? WindowCaptureExclusionMatchMode.FilePath
                    : WindowCaptureExclusionMatchMode.ProcessName
            };
            var existing = normalizedTargets.FirstOrDefault(target => target.HasSameIdentity(normalized));
            if (existing is null)
            {
                normalizedTargets.Add(normalized);
                return;
            }

            if (string.IsNullOrWhiteSpace(existing.ExecutablePath) && !string.IsNullOrWhiteSpace(normalized.ExecutablePath))
            {
                existing.ExecutablePath = normalized.ExecutablePath;
                existing.ProcessName = normalized.ProcessName;
                existing.MatchMode = normalized.MatchMode;
            }
        }

        foreach (var target in targets ?? [])
        {
            AddTarget(target);
        }

        foreach (var executablePath in NormalizeWindowCaptureExcludedExecutablePaths(legacyExecutablePaths))
        {
            AddTarget(new WindowCaptureExclusionTarget
            {
                ExecutablePath = executablePath,
                ProcessName = Path.GetFileNameWithoutExtension(executablePath),
                MatchMode = WindowCaptureExclusionMatchMode.FilePath
            });
        }

        return normalizedTargets
            .OrderBy(target => target.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static bool TryNormalizeWindowCaptureExcludedExecutablePath(string? executablePath, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        var trimmedPath = executablePath?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath) ||
            !Path.IsPathFullyQualified(trimmedPath) ||
            !string.Equals(Path.GetExtension(trimmedPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(trimmedPath);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    internal static bool TryNormalizeWindowCaptureExcludedProcessName(string? processName, out string normalizedProcessName)
    {
        normalizedProcessName = processName?.Trim() ?? string.Empty;
        if (normalizedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalizedProcessName = normalizedProcessName[..^4];
        }

        return !string.IsNullOrWhiteSpace(normalizedProcessName);
    }

    private static IReadOnlyList<string> NormalizeHotkeySlots(IReadOnlyList<string>? hotkeys, string fallback)
    {
        var slots = new List<string>();
        if (hotkeys is not null)
        {
            slots.AddRange(hotkeys.Take(1).Select(value => value ?? string.Empty));
        }

        if (slots.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
        {
            slots.Add(fallback);
        }

        while (slots.Count < 1)
        {
            slots.Add(string.Empty);
        }

        return slots;
    }
}

public enum WindowCaptureExclusionMatchMode
{
    FilePath,
    ProcessName
}

public sealed class WindowCaptureExclusionTarget
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public WindowCaptureExclusionMatchMode MatchMode { get; set; } = WindowCaptureExclusionMatchMode.FilePath;

    public WindowCaptureExclusionTarget Clone() =>
        new()
        {
            ExecutablePath = ExecutablePath,
            ProcessName = ProcessName,
            MatchMode = MatchMode
        };

    public bool HasSameIdentity(WindowCaptureExclusionTarget other)
    {
        if (!string.IsNullOrWhiteSpace(ExecutablePath) && !string.IsNullOrWhiteSpace(other.ExecutablePath))
        {
            return string.Equals(ExecutablePath, other.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(ProcessName, other.ProcessName, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ViewerImageViewState
{
    public string Path { get; set; } = string.Empty;

    public double Zoom { get; set; } = 1.0;

    public bool FitMode { get; set; } = true;

    public double HorizontalOffset { get; set; }

    public double VerticalOffset { get; set; }

    public ViewerImageViewState Clone() =>
        new()
        {
            Path = Path,
            Zoom = Zoom,
            FitMode = FitMode,
            HorizontalOffset = HorizontalOffset,
            VerticalOffset = VerticalOffset
        };
}

public static class MiniCaptureSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static event EventHandler<MiniCaptureSettings>? SettingsChanged;

    public static string SettingsPath =>
        Path.Combine(SettingsPathHelper.AppDataDirectory, "settings.json");

    public static MiniCaptureSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new MiniCaptureSettings();
            }

            using var stream = File.OpenRead(SettingsPath);
            return NormalizeLoadedSettings(JsonSerializer.Deserialize<MiniCaptureSettings>(stream, JsonOptions) ?? new MiniCaptureSettings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new MiniCaptureSettings();
        }
    }

    public static void Save(MiniCaptureSettings settings, bool notify = true)
    {
        SettingsPathHelper.EnsureAppDataDirectory();
        using var stream = File.Create(SettingsPath);
        var normalized = NormalizeLoadedSettings(settings);
        JsonSerializer.Serialize(stream, normalized, JsonOptions);
        if (notify)
        {
            SettingsChanged?.Invoke(null, normalized.Clone());
        }
    }

    private static MiniCaptureSettings NormalizeLoadedSettings(MiniCaptureSettings settings)
    {
        if (settings.TimerDelaySeconds < 0)
        {
            settings.TimerDelaySeconds = 0;
        }
        else if (settings.TimerDelaySeconds > 60)
        {
            settings.TimerDelaySeconds = 60;
        }

        if (settings.ShortcutTimerDelaySeconds <= 0)
        {
            settings.ShortcutTimerDelaySeconds = 5;
        }
        else if (settings.ShortcutTimerDelaySeconds > 60)
        {
            settings.ShortcutTimerDelaySeconds = 60;
        }

        settings.SetHotkeySlots(CaptureHotkeyKind.Window, settings.GetHotkeySlots(CaptureHotkeyKind.Window));
        settings.SetHotkeySlots(CaptureHotkeyKind.Region, settings.GetHotkeySlots(CaptureHotkeyKind.Region));
        settings.SetHotkeySlots(CaptureHotkeyKind.FullScreen, settings.GetHotkeySlots(CaptureHotkeyKind.FullScreen));
        settings.SetHotkeySlots(CaptureHotkeyKind.Timer, settings.GetHotkeySlots(CaptureHotkeyKind.Timer));
        settings.WindowCaptureExclusionTargets = MiniCaptureSettings.NormalizeWindowCaptureExclusionTargets(
                settings.WindowCaptureExclusionTargets,
                settings.LegacyWindowCaptureExcludedExecutablePaths)
            .Select(target => target.Clone())
            .ToList();
        settings.LegacyWindowCaptureExcludedExecutablePaths = null;
        return settings;
    }
}

public readonly record struct CaptureHotkey(ModifierKeys Modifiers, Key Key)
{
    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(ModifierKeys.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(ModifierKeys.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(ModifierKeys.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }

    public static bool TryParse(string? text, out CaptureHotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        Key? key = null;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierKeys.Control;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierKeys.Alt;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierKeys.Shift;
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierKeys.Windows;
                continue;
            }

            if (Enum.TryParse<Key>(part, ignoreCase: true, out var parsedKey) &&
                KeyInterop.VirtualKeyFromKey(parsedKey) != 0)
            {
                key = parsedKey;
            }
        }

        if (modifiers == ModifierKeys.None || key is not { } finalKey)
        {
            return false;
        }

        hotkey = new CaptureHotkey(modifiers, finalKey);
        return true;
    }
}
