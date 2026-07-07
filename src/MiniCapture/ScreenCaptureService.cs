using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FormsScreen = System.Windows.Forms.Screen;

namespace MiniCapture;

public static class ScreenCaptureService
{
    public static string CaptureFullScreen()
    {
        return CaptureRegion(GetVirtualScreenBounds());
    }

    public static string CaptureWindow(WindowCaptureTarget target)
    {
        return CaptureRegion(target.Bounds);
    }

    public static string CaptureRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new InvalidOperationException("캡처 영역이 너무 작습니다.");
        }

        var filePath = CreateCapturePath(DateTime.Now);
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
        bitmap.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    public static ScreenCaptureSnapshot CaptureSnapshot()
    {
        var bounds = GetVirtualScreenBounds();
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return new ScreenCaptureSnapshot(bounds, bitmap);
    }

    public static string SaveSnapshotRegion(ScreenCaptureSnapshot snapshot, Rectangle region)
    {
        var clipped = Rectangle.Intersect(region, snapshot.Bounds);
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            throw new InvalidOperationException("캡처 영역이 정지 화면 밖에 있습니다.");
        }

        var sourceRegion = new Rectangle(
            clipped.Left - snapshot.Bounds.Left,
            clipped.Top - snapshot.Bounds.Top,
            clipped.Width,
            clipped.Height);

        var filePath = CreateCapturePath(DateTime.Now);
        using var bitmap = new Bitmap(clipped.Width, clipped.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(
            snapshot.Bitmap,
            new Rectangle(0, 0, clipped.Width, clipped.Height),
            sourceRegion,
            GraphicsUnit.Pixel);
        bitmap.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    public static Rectangle GetVirtualScreenBounds()
    {
        var screens = FormsScreen.AllScreens;
        if (screens.Length == 0)
        {
            throw new InvalidOperationException("캡처할 화면을 찾을 수 없습니다.");
        }

        var bounds = screens[0].Bounds;
        for (var i = 1; i < screens.Length; i++)
        {
            bounds = Rectangle.Union(bounds, screens[i].Bounds);
        }

        return bounds;
    }

    private static string CreateCapturePath(DateTime timestamp)
    {
        var directory = SettingsPathHelper.CaptureDirectoryFor(timestamp);
        Directory.CreateDirectory(directory);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var suffix = attempt == 0 ? string.Empty : $"_{attempt:00}";
            var fileName = $"{timestamp:yyyyMMdd_HHmmss_fff}{suffix}.png";
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }
        }

        throw new IOException("고유한 캡처 파일 이름을 만들 수 없습니다.");
    }
}
