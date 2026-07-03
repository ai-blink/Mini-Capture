using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MiniCapture;

internal sealed class GlobalInputHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int VkEscape = 0x1B;

    private readonly LowLevelHookProc _keyboardProc;
    private readonly LowLevelHookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;

    public GlobalInputHook()
    {
        _keyboardProc = OnKeyboardHook;
        _mouseProc = OnMouseHook;

        using var process = Process.GetCurrentProcess();
        var module = process.MainModule;
        var moduleHandle = module?.ModuleName is { } moduleName
            ? GetModuleHandle(moduleName)
            : IntPtr.Zero;

        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
        if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Dispose();
            throw new Win32Exception(error, "Global capture input hook could not be installed.");
        }
    }

    public event EventHandler<GlobalMouseHookEventArgs>? MouseMove;

    public event EventHandler<GlobalMouseHookEventArgs>? LeftButtonDown;

    public event EventHandler<GlobalMouseHookEventArgs>? LeftButtonUp;

    public event EventHandler<GlobalKeyHookEventArgs>? EscapePressed;

    public void Dispose()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private IntPtr OnMouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        var hookInfo = Marshal.PtrToStructure<MouseHookStruct>(lParam);
        var args = new GlobalMouseHookEventArgs(new Point(hookInfo.Point.X, hookInfo.Point.Y));
        var message = wParam.ToInt32();
        switch (message)
        {
            case WmMouseMove:
                MouseMove?.Invoke(this, args);
                break;
            case WmLButtonDown:
                LeftButtonDown?.Invoke(this, args);
                break;
            case WmLButtonUp:
                LeftButtonUp?.Invoke(this, args);
                break;
        }

        return args.Handled
            ? new IntPtr(1)
            : CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private IntPtr OnKeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(_keyboardHook, code, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message is WmKeyDown or WmSysKeyDown)
        {
            var hookInfo = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);
            if (hookInfo.VirtualKeyCode == VkEscape)
            {
                var args = new GlobalKeyHookEventArgs();
                EscapePressed?.Invoke(this, args);
                if (args.Handled)
                {
                    return new IntPtr(1);
                }
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private delegate IntPtr LowLevelHookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc callback, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MouseHookStruct
    {
        public readonly NativePoint Point;
        public readonly uint MouseData;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardHookStruct
    {
        public readonly int VirtualKeyCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }
}

internal sealed class GlobalMouseHookEventArgs(Point screenPoint) : EventArgs
{
    public Point ScreenPoint { get; } = screenPoint;

    public bool Handled { get; set; }
}

internal sealed class GlobalKeyHookEventArgs : EventArgs
{
    public bool Handled { get; set; }
}
