using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MiniCapture;

public sealed class CaptureImageFile : INotifyPropertyChanged
{
    private static readonly SemaphoreSlim ThumbnailGate = new(2);

    private readonly Dictionary<int, ImageSource?> _thumbnails = new();
    private readonly HashSet<int> _loadingThumbnailSizes = new();
    private readonly object _thumbnailLock = new();

    public CaptureImageFile(FileInfo file)
    {
        Path = file.FullName;
        FileName = file.Name;
        FolderPath = file.DirectoryName ?? string.Empty;
        Extension = file.Extension.TrimStart('.').ToUpperInvariant();
        Length = file.Length;
        LastWriteTime = file.LastWriteTime;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }

    public string FileName { get; }

    public string FolderPath { get; }

    public string Extension { get; }

    public long Length { get; }

    public DateTime LastWriteTime { get; }

    public string SizeText => FormatSize(Length);

    public string ModifiedText => LastWriteTime.ToString("yyyy-MM-dd HH:mm");

    public ImageSource? SmallThumbnail => GetOrQueueThumbnail(64, nameof(SmallThumbnail));

    public ImageSource? MediumThumbnail => GetOrQueueThumbnail(112, nameof(MediumThumbnail));

    public ImageSource? LargeThumbnail => GetOrQueueThumbnail(168, nameof(LargeThumbnail));

    private ImageSource? GetOrQueueThumbnail(int pixelSize, string propertyName)
    {
        lock (_thumbnailLock)
        {
            if (_thumbnails.TryGetValue(pixelSize, out var thumbnail))
            {
                return thumbnail;
            }

            if (!_loadingThumbnailSizes.Add(pixelSize))
            {
                return null;
            }
        }

        _ = LoadThumbnailAsync(pixelSize, propertyName);
        return null;
    }

    private async Task LoadThumbnailAsync(int pixelSize, string propertyName)
    {
        ImageSource? thumbnail = null;
        try
        {
            await ThumbnailGate.WaitAsync().ConfigureAwait(false);
            try
            {
                thumbnail = LoadThumbnail(pixelSize);
            }
            finally
            {
                ThumbnailGate.Release();
            }
        }
        finally
        {
            lock (_thumbnailLock)
            {
                _thumbnails[pixelSize] = thumbnail;
                _loadingThumbnailSizes.Remove(pixelSize);
            }

            RaisePropertyChanged(propertyName);
        }
    }

    private ImageSource? LoadThumbnail(int pixelSize)
    {
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
            return image;
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

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return;
        }

        _ = dispatcher.BeginInvoke(
            () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)),
            DispatcherPriority.Background);
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
