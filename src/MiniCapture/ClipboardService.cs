using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using FormsClipboard = System.Windows.Forms.Clipboard;
using WpfClipboard = System.Windows.Clipboard;

namespace MiniCapture;

internal static class ClipboardService
{
    private const int ClipboardCannotOpenHResult = unchecked((int)0x800401D0);

    public static Task SetTextAsync(string text) =>
        SetTextAsync(text, WpfClipboard.SetText, Task.Delay, maxAttempts: 5);

    public static void SetImage(BitmapSource image)
    {
        using var bitmap = CreateClipboardBitmap(image);
        FormsClipboard.SetImage(bitmap);
    }

    internal static DrawingBitmap CreateClipboardBitmap(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        using var decoded = new DrawingBitmap(stream);
        return new DrawingBitmap(decoded);
    }

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
