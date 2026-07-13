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
    private int _thumbnailVersion;

    public CaptureImageFile(FileInfo file)
    {
        Path = file.FullName;
        RefreshFromFileInfo(file, raiseChanges: false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }

    public string FileName { get; private set; } = string.Empty;

    public string FolderPath { get; private set; } = string.Empty;

    public string Extension { get; private set; } = string.Empty;

    public long Length { get; private set; }

    public DateTime LastWriteTime { get; private set; }

    public string SizeText => FormatSize(Length);

    public string ModifiedText => LastWriteTime.ToString("yyyy-MM-dd HH:mm");

    public ImageSource? SmallThumbnail => GetOrQueueThumbnail(64, nameof(SmallThumbnail));

    public ImageSource? MediumThumbnail => GetOrQueueThumbnail(112, nameof(MediumThumbnail));

    public ImageSource? LargeThumbnail => GetOrQueueThumbnail(168, nameof(LargeThumbnail));

    public bool RefreshFromDisk()
    {
        try
        {
            var file = new FileInfo(Path);
            if (!file.Exists)
            {
                return false;
            }

            RefreshFromFileInfo(file, raiseChanges: true);
            InvalidateThumbnails();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public void InvalidateThumbnails()
    {
        lock (_thumbnailLock)
        {
            _thumbnailVersion++;
            _thumbnails.Clear();
            _loadingThumbnailSizes.Clear();
        }

        RaisePropertyChanged(nameof(SmallThumbnail));
        RaisePropertyChanged(nameof(MediumThumbnail));
        RaisePropertyChanged(nameof(LargeThumbnail));
    }

    private ImageSource? GetOrQueueThumbnail(int pixelSize, string propertyName)
    {
        int thumbnailVersion;
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

            thumbnailVersion = _thumbnailVersion;
        }

        _ = LoadThumbnailAsync(pixelSize, propertyName, thumbnailVersion);
        return null;
    }

    private async Task LoadThumbnailAsync(int pixelSize, string propertyName, int thumbnailVersion)
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
            var shouldNotify = false;
            lock (_thumbnailLock)
            {
                if (thumbnailVersion == _thumbnailVersion)
                {
                    _thumbnails[pixelSize] = thumbnail;
                    shouldNotify = true;
                }

                _loadingThumbnailSizes.Remove(pixelSize);
            }

            if (shouldNotify)
            {
                RaisePropertyChanged(propertyName);
            }
        }
    }

    private void RefreshFromFileInfo(FileInfo file, bool raiseChanges)
    {
        FileName = file.Name;
        FolderPath = file.DirectoryName ?? string.Empty;
        Extension = file.Extension.TrimStart('.').ToUpperInvariant();
        Length = file.Length;
        LastWriteTime = file.LastWriteTime;

        if (!raiseChanges)
        {
            return;
        }

        RaisePropertyChanged(nameof(FileName));
        RaisePropertyChanged(nameof(FolderPath));
        RaisePropertyChanged(nameof(Extension));
        RaisePropertyChanged(nameof(Length));
        RaisePropertyChanged(nameof(LastWriteTime));
        RaisePropertyChanged(nameof(SizeText));
        RaisePropertyChanged(nameof(ModifiedText));
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
