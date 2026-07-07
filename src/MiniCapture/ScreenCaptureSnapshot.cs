using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MiniCapture;

public sealed class ScreenCaptureSnapshot(Rectangle bounds, Bitmap bitmap) : IDisposable
{
    private bool _disposed;

    public Rectangle Bounds { get; } = bounds;

    public Bitmap Bitmap { get; } = bitmap;

    public BitmapSource CreatePreviewSource()
    {
        ThrowIfDisposed();

        var handle = Bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            _ = DeleteObject(handle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Bitmap.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);
}
