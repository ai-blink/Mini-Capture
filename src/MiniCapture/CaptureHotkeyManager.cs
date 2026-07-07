using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace MiniCapture;

internal sealed class CaptureHotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModWin = 0x0008;
    private const int ModNoRepeat = 0x4000;

    private readonly HwndSource _source;
    private readonly Action<CaptureMode> _onHotkey;
    private readonly List<int> _registeredIds = [];
    private readonly Dictionary<int, CaptureMode> _registeredModes = [];

    public CaptureHotkeyManager(HwndSource source, Action<CaptureMode> onHotkey)
    {
        _source = source;
        _onHotkey = onHotkey;
        _source.AddHook(OnWindowMessage);
    }

    public string Register(MiniCaptureSettings settings)
    {
        UnregisterAll();

        var failures = new List<string>();
        var id = 1;
        RegisterGroup(ref id, CaptureMode.Window, settings.GetHotkeySlots(CaptureHotkeyKind.Window), "창 선택", failures);
        RegisterGroup(ref id, CaptureMode.Drag, settings.GetHotkeySlots(CaptureHotkeyKind.Region), "영역 선택", failures);
        RegisterGroup(ref id, CaptureMode.FullScreen, settings.GetHotkeySlots(CaptureHotkeyKind.FullScreen), "전체 화면", failures);
        RegisterGroup(ref id, CaptureMode.Timer, settings.GetHotkeySlots(CaptureHotkeyKind.Timer), "타이머", failures);

        return failures.Count == 0
            ? "단축키가 등록되었습니다."
            : $"등록 실패: {string.Join(", ", failures)}";
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(OnWindowMessage);
    }

    public void Suspend()
    {
        UnregisterAll();
    }

    private void RegisterGroup(ref int nextId, CaptureMode mode, IReadOnlyList<string> hotkeys, string label, List<string> failures)
    {
        for (var index = 0; index < hotkeys.Count; index++)
        {
            RegisterOne(nextId++, mode, hotkeys[index], $"{label} {index + 1}", failures);
        }
    }

    private void RegisterOne(int id, CaptureMode mode, string text, string label, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!CaptureHotkey.TryParse(text, out var hotkey))
        {
            failures.Add($"{label}({text})");
            return;
        }

        var modifiers = ToNativeModifiers(hotkey.Modifiers) | ModNoRepeat;
        var virtualKey = KeyInterop.VirtualKeyFromKey(hotkey.Key);
        if (!RegisterHotKey(_source.Handle, id, modifiers, virtualKey))
        {
            failures.Add($"{label}({hotkey.DisplayText})");
            return;
        }

        _registeredIds.Add(id);
        _registeredModes[id] = mode;
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey)
        {
            return IntPtr.Zero;
        }

        if (!_registeredModes.TryGetValue(wParam.ToInt32(), out var captureMode))
        {
            return IntPtr.Zero;
        }

        handled = true;
        _onHotkey(captureMode);
        return IntPtr.Zero;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registeredIds)
        {
            _ = UnregisterHotKey(_source.Handle, id);
        }

        _registeredIds.Clear();
        _registeredModes.Clear();
    }

    private static int ToNativeModifiers(ModifierKeys modifiers)
    {
        var native = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            native |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            native |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            native |= ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            native |= ModWin;
        }

        return native;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
