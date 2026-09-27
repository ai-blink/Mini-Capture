using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using MiniCapture.Resources;

namespace MiniCapture;

// 성공 여부는 메시지 문구(언어별로 달라짐)가 아니라 이 값으로 판정한다.
internal readonly record struct HotkeyRegistrationResult(bool Succeeded, string Message);

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

    public HotkeyRegistrationResult Register(MiniCaptureSettings settings)
    {
        UnregisterAll();

        var failures = new List<string>();
        var id = 1;
        RegisterGroup(ref id, CaptureMode.Window, settings.GetHotkeySlots(CaptureHotkeyKind.Window), Strings.Hotkey_Window, failures);
        RegisterGroup(ref id, CaptureMode.Drag, settings.GetHotkeySlots(CaptureHotkeyKind.Region), Strings.Hotkey_Region, failures);
        RegisterGroup(ref id, CaptureMode.FullScreen, settings.GetHotkeySlots(CaptureHotkeyKind.FullScreen), Strings.Hotkey_FullScreen, failures);
        RegisterGroup(ref id, CaptureMode.Timer, settings.GetHotkeySlots(CaptureHotkeyKind.Timer), Strings.Hotkey_Timer, failures);

        return failures.Count == 0
            ? new HotkeyRegistrationResult(true, Strings.Hotkey_Registered)
            : new HotkeyRegistrationResult(false, string.Format(Strings.Hotkey_RegisterFailed, string.Join(", ", failures)));
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
