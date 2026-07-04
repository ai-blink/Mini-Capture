using System.Collections.ObjectModel;
using System.IO;

namespace MiniCapture;

public static class CaptureFileIndex
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    public static string RootDirectory => SettingsPathHelper.DefaultCaptureDirectory;

    public static IReadOnlyList<CaptureImageFile> GetImages(string? folderPath)
    {
        var folder = NormalizeFolder(folderPath);
        if (folder is null)
        {
            return Array.Empty<CaptureImageFile>();
        }

        return EnumerateImageFiles(folder)
            .Select(file => new CaptureImageFile(file))
            .OrderByDescending(file => file.LastWriteTime)
            .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static CaptureImageFile? GetLatestImage()
    {
        Directory.CreateDirectory(RootDirectory);

        FileInfo? latest = null;
        foreach (var file in EnumerateImageFiles(RootDirectory))
        {
            if (latest is null ||
                file.LastWriteTime > latest.LastWriteTime ||
                (file.LastWriteTime == latest.LastWriteTime &&
                    string.Compare(file.Name, latest.Name, StringComparison.OrdinalIgnoreCase) < 0))
            {
                latest = file;
            }
        }

        return latest is null ? null : new CaptureImageFile(latest);
    }

    public static ObservableCollection<FolderTreeNode> BuildFolderTree()
    {
        var root = RootDirectory;
        Directory.CreateDirectory(root);

        var miniCaptureQuick = BuildDirectoryNode("MiniCapture", root, "캡처 루트");
        miniCaptureQuick.IsExpanded = true;
        var miniCaptureUnderPictures = CloneNode(miniCaptureQuick);
        miniCaptureUnderPictures.IsExpanded = true;

        var latest = GetLatestImage();
        if (latest is not null && IsUnderRoot(latest.FolderPath))
        {
            miniCaptureQuick.Children.Insert(0, new FolderTreeNode("최근 캡처", latest.FolderPath, "\uE81C", latest.ModifiedText));
        }

        var todayFolder = Path.Combine(root, DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"), DateTime.Now.ToString("dd"));
        if (Directory.Exists(todayFolder))
        {
            miniCaptureQuick.Children.Insert(0, new FolderTreeNode("오늘", todayFolder, "\uE787", DateTime.Now.ToString("yyyy-MM-dd")));
        }

        var pictures = new FolderTreeNode("사진", null, "\uEB9F");
        pictures.Children.Add(miniCaptureUnderPictures);
        pictures.IsExpanded = true;

        var quickAccess = new FolderTreeNode("빠른 실행", null, "\uE734");
        quickAccess.Children.Add(miniCaptureQuick);
        quickAccess.IsExpanded = true;

        var thisPc = new FolderTreeNode("내 PC", null, "\uEC4E");
        thisPc.Children.Add(pictures);
        thisPc.IsExpanded = true;

        return new ObservableCollection<FolderTreeNode>
        {
            quickAccess,
            thisPc
        };
    }

    public static bool IsImagePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
            ImageExtensions.Contains(Path.GetExtension(path)) &&
            File.Exists(path);
    }

    public static bool TryCreateImageFile(string? path, out CaptureImageFile? imageFile)
    {
        imageFile = null;
        if (!IsImagePath(path))
        {
            return false;
        }

        try
        {
            imageFile = new CaptureImageFile(new FileInfo(path!));
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

    public static bool IsUnderRoot(string folderPath)
    {
        var root = EnsureTrailingSeparator(Path.GetFullPath(RootDirectory));
        var folder = EnsureTrailingSeparator(Path.GetFullPath(folderPath));
        return folder.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static FolderTreeNode BuildDirectoryNode(string name, string path, string? detail = null)
    {
        var node = new FolderTreeNode(name, path, "\uE8B7", detail);
        foreach (var directory in EnumerateDirectories(path))
        {
            node.Children.Add(BuildDirectoryNode(System.IO.Path.GetFileName(directory), directory));
        }

        return node;
    }

    private static FolderTreeNode CloneNode(FolderTreeNode source)
    {
        var clone = new FolderTreeNode(source.Name, source.Path, source.IconGlyph, source.Detail)
        {
            IsExpanded = source.IsExpanded
        };

        foreach (var child in source.Children)
        {
            clone.Children.Add(CloneNode(child));
        }

        return clone;
    }

    private static string? NormalizeFolder(string? folderPath)
    {
        var root = RootDirectory;
        Directory.CreateDirectory(root);

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return root;
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            return IsUnderRoot(fullPath) ? fullPath : root;
        }
        catch (ArgumentException)
        {
            return root;
        }
        catch (NotSupportedException)
        {
            return root;
        }
    }

    private static IEnumerable<FileInfo> EnumerateImageFiles(string folder)
    {
        foreach (var file in EnumerateFiles(folder))
        {
            if (ImageExtensions.Contains(file.Extension))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string folder)
    {
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var child in EnumerateDirectories(current).Reverse())
            {
                pending.Push(child);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                FileInfo info;
                try
                {
                    info = new FileInfo(file);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                yield return info;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string folder)
    {
        try
        {
            return Directory.EnumerateDirectories(folder)
                .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
