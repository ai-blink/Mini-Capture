using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiniCapture;

public sealed class CaptureImageFile
{
    private ImageSource? _thumbnail;
    private int _thumbnailPixelSize;

    public CaptureImageFile(FileInfo file)
    {
        Path = file.FullName;
        FileName = file.Name;
        FolderPath = file.DirectoryName ?? string.Empty;
        Extension = file.Extension.TrimStart('.').ToUpperInvariant();
        Length = file.Length;
        LastWriteTime = file.LastWriteTime;
    }

    public string Path { get; }

    public string FileName { get; }

    public string FolderPath { get; }

    public string Extension { get; }

    public long Length { get; }

    public DateTime LastWriteTime { get; }

    public string SizeText => FormatSize(Length);

    public string ModifiedText => LastWriteTime.ToString("yyyy-MM-dd HH:mm");

    public ImageSource? GetThumbnail(int pixelSize)
    {
        if (_thumbnail is not null && _thumbnailPixelSize == pixelSize)
        {
            return _thumbnail;
        }

        try
        {
            using var stream = File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = pixelSize;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            _thumbnail = image;
            _thumbnailPixelSize = pixelSize;
            return _thumbnail;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string FormatSize(long length)
    {
        if (length < 1024)
        {
            return $"{length} B";
        }

        var size = length / 1024d;
        if (size < 1024)
        {
            return $"{size:0.#} KB";
        }

        size /= 1024d;
        return $"{size:0.#} MB";
    }
}
