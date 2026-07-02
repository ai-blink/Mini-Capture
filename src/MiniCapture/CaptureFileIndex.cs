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
        return GetImages(RootDirectory).FirstOrDefault();
    }

    public static ObservableCollection<FolderTreeNode> BuildFolderTree()
    {
        var root = RootDirectory;
        Directory.CreateDirectory(root);

        var miniCaptureQuick = BuildDirectoryNode("MiniCapture", root);
        miniCaptureQuick.IsExpanded = true;

        var miniCaptureUnderPictures = BuildDirectoryNode("MiniCapture", root);
        miniCaptureUnderPictures.IsExpanded = true;

        var pictures = new FolderTreeNode("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        pictures.Children.Add(miniCaptureUnderPictures);
        pictures.IsExpanded = true;

        var quickAccess = new FolderTreeNode("Quick access", null);
        quickAccess.Children.Add(miniCaptureQuick);
        quickAccess.IsExpanded = true;

        var thisPc = new FolderTreeNode("This PC", null);
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

    public static bool IsUnderRoot(string folderPath)
    {
        var root = EnsureTrailingSeparator(Path.GetFullPath(RootDirectory));
        var folder = EnsureTrailingSeparator(Path.GetFullPath(folderPath));
        return folder.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static FolderTreeNode BuildDirectoryNode(string name, string path)
    {
        var node = new FolderTreeNode(name, path);
        foreach (var directory in EnumerateDirectories(path))
        {
            node.Children.Add(BuildDirectoryNode(System.IO.Path.GetFileName(directory), directory));
        }

        return node;
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
