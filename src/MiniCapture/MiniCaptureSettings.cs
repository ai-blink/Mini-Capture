using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MiniCapture;

public sealed class MiniCaptureSettings
{
    public int TimerDelaySeconds { get; set; }

    public bool QuickButtonVisible { get; set; } = true;

    public double? QuickButtonLeft { get; set; }

    public double? QuickButtonTop { get; set; }

    public string WindowCaptureHotkey { get; set; } = "Ctrl+Alt+W";

    public string RegionCaptureHotkey { get; set; } = "Ctrl+Alt+R";

    public string FullScreenCaptureHotkey { get; set; } = "Ctrl+Alt+F";

    public List<string> WindowCaptureHotkeys { get; set; } = ["Ctrl+Alt+W", "", ""];

    public List<string> RegionCaptureHotkeys { get; set; } = ["Ctrl+Alt+R", "", ""];

    public List<string> FullScreenCaptureHotkeys { get; set; } = ["Ctrl+Alt+F", "", ""];

    public double? ViewerLeft { get; set; }

    public double? ViewerTop { get; set; }

    public double? ViewerWidth { get; set; }

    public double? ViewerHeight { get; set; }

    public string? ViewerWindowState { get; set; }

    public ExplorerViewMode? ViewerExplorerViewMode { get; set; }

    public double? ViewerFolderTreeWidth { get; set; }

    public double? ViewerFileListWidth { get; set; }

    public MiniCaptureSettings Clone() =>
        new()
        {
            TimerDelaySeconds = TimerDelaySeconds,
            QuickButtonVisible = QuickButtonVisible,
            QuickButtonLeft = QuickButtonLeft,
            QuickButtonTop = QuickButtonTop,
            WindowCaptureHotkey = WindowCaptureHotkey,
            RegionCaptureHotkey = RegionCaptureHotkey,
            FullScreenCaptureHotkey = FullScreenCaptureHotkey,
            WindowCaptureHotkeys = NormalizeHotkeySlots(WindowCaptureHotkeys, WindowCaptureHotkey).ToList(),
            RegionCaptureHotkeys = NormalizeHotkeySlots(RegionCaptureHotkeys, RegionCaptureHotkey).ToList(),
            FullScreenCaptureHotkeys = NormalizeHotkeySlots(FullScreenCaptureHotkeys, FullScreenCaptureHotkey).ToList(),
            ViewerLeft = ViewerLeft,
            ViewerTop = ViewerTop,
            ViewerWidth = ViewerWidth,
            ViewerHeight = ViewerHeight,
            ViewerWindowState = ViewerWindowState,
            ViewerExplorerViewMode = ViewerExplorerViewMode,
            ViewerFolderTreeWidth = ViewerFolderTreeWidth,
            ViewerFileListWidth = ViewerFileListWidth
        };

    public IReadOnlyList<string> GetHotkeySlots(CaptureHotkeyKind kind) =>
        kind switch
        {
            CaptureHotkeyKind.Window => NormalizeHotkeySlots(WindowCaptureHotkeys, WindowCaptureHotkey),
            CaptureHotkeyKind.Region => NormalizeHotkeySlots(RegionCaptureHotkeys, RegionCaptureHotkey),
            CaptureHotkeyKind.FullScreen => NormalizeHotkeySlots(FullScreenCaptureHotkeys, FullScreenCaptureHotkey),
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
        }
    }

    private static IReadOnlyList<string> NormalizeHotkeySlots(IReadOnlyList<string>? hotkeys, string fallback)
    {
        var slots = new List<string>();
        if (hotkeys is not null)
        {
            slots.AddRange(hotkeys.Take(3).Select(value => value ?? string.Empty));
        }

        if (slots.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
        {
            slots.Add(fallback);
        }

        while (slots.Count < 3)
        {
            slots.Add(string.Empty);
        }

        return slots;
    }
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
            return JsonSerializer.Deserialize<MiniCaptureSettings>(stream, JsonOptions) ?? new MiniCaptureSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new MiniCaptureSettings();
        }
    }

    public static void Save(MiniCaptureSettings settings)
    {
        SettingsPathHelper.EnsureAppDataDirectory();
        using var stream = File.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, JsonOptions);
        SettingsChanged?.Invoke(null, settings.Clone());
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
