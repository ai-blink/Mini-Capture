using MiniCapture.Resources;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MiniCapture;

public partial class HotkeyDetectDialog : Window
{
    private readonly CaptureHotkeyPart _part;
    private readonly DispatcherTimer _timer;
    private DateTime _deadline;

    public HotkeyDetectDialog(string label, CaptureHotkeyPart part)
    {
        InitializeComponent();
        _part = part;
        TitleText.Text = string.Format(Strings.HotkeyDialog_TitleFormat, label, DisplayName(part));
        InstructionText.Text = part == CaptureHotkeyPart.Main
            ? Strings.HotkeyDialog_TimedMainKey
            : Strings.HotkeyDialog_TimedModifier;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _timer.Tick += (_, _) => UpdateCountdown();
    }

    public string? DetectedValue { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _deadline = DateTime.UtcNow.AddSeconds(5);
        UpdateCountdown();
        _timer.Start();
        Activate();
        Focus();
        Keyboard.Focus(this);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_part is CaptureHotkeyPart.First or CaptureHotkeyPart.Second)
        {
            if (!TryGetModifierName(key, out var modifierName))
            {
                InstructionText.Text = Strings.HotkeyDialog_NeedModifier;
                e.Handled = true;
                return;
            }

            DetectedValue = modifierName;
            DialogResult = true;
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            InstructionText.Text = Strings.HotkeyDialog_NeedMainKey;
            e.Handled = true;
            return;
        }

        DetectedValue = key.ToString();
        DialogResult = true;
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdateCountdown()
    {
        var remaining = (int)Math.Ceiling((_deadline - DateTime.UtcNow).TotalSeconds);
        if (remaining <= 0)
        {
            DialogResult = false;
            return;
        }

        CountdownText.Text = string.Format(Strings.HotkeyDialog_Remaining, remaining);
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static bool TryGetModifierName(Key key, out string modifierName)
    {
        modifierName = key switch
        {
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LWin or Key.RWin => "Win",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(modifierName);
    }

    private static string DisplayName(CaptureHotkeyPart part) =>
        part switch
        {
            CaptureHotkeyPart.First => Strings.Hotkey_FirstKeyPart,
            CaptureHotkeyPart.Second => Strings.Hotkey_SecondKeyPart,
            _ => Strings.Hotkey_MainKeyPart
        };
}
