using System.Runtime.InteropServices;
using WpfClipboard = System.Windows.Clipboard;

namespace MiniCapture;

internal static class ClipboardService
{
    private const int ClipboardCannotOpenHResult = unchecked((int)0x800401D0);

    public static Task SetTextAsync(string text) =>
        SetTextAsync(text, WpfClipboard.SetText, Task.Delay, maxAttempts: 5);

    internal static async Task SetTextAsync(
        string text,
        Action<string> setText,
        Func<TimeSpan, Task> delay,
        int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                setText(text);
                return;
            }
            catch (COMException ex) when (
                ex.HResult == ClipboardCannotOpenHResult &&
                attempt < maxAttempts)
            {
                await delay(TimeSpan.FromMilliseconds(50 * attempt));
            }
        }
    }
}
