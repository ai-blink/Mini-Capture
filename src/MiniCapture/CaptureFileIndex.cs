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

        return EnumerateImageFiles(folder, recursive: false)
            .Select(file => new CaptureImageFile(file))
            .OrderByDescending(file => file.LastWriteTime)
            .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static CaptureImageFile? GetLatestImage()
    {
        Directory.CreateDirectory(RootDirectory);

        FileInfo? latest = null;
        foreach (var file in EnumerateImageFiles(RootDirectory, recursive: true))
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

    public static ObservableCollection<FolderTreeNode> BuildFolderTree(
        string? targetFolder = null,
        CaptureImageFile? knownLatest = null)
    {
        var root = RootDirectory;
        Directory.CreateDirectory(root);
        var normalizedTargetFolder = NormalizeFolder(targetFolder) ?? root;

        var miniCaptureQuick = BuildDirectoryNode("MiniCapture", root, "캡처 루트");
        miniCaptureQuick.IsExpanded = true;
        var miniCaptureUnderPictures = CloneNode(miniCaptureQuick);
        miniCaptureUnderPictures.IsExpanded = true;

        var latest = knownLatest;
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
        if (!IsUnderRoot(normalizedTargetFolder) && BuildExternalPathNode(normalizedTargetFolder) is { } externalPath)
        {
            thisPc.Children.Add(externalPath);
        }

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
        try
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(RootDirectory));
            var folder = EnsureTrailingSeparator(Path.GetFullPath(folderPath));
            return folder.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static FolderTreeNode? BuildExternalPathNode(string targetFolder)
    {
        var fullPath = Path.GetFullPath(targetFolder);
        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return null;
        }

        var rootName = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(rootName))
        {
            rootName = rootPath;
        }

        var rootNode = new FolderTreeNode(rootName, rootPath, "\uEDA2", rootPath)
        {
            IsExpanded = true
        };

        var currentNode = rootNode;
        var currentPath = rootPath;
        var relativePath = Path.GetRelativePath(rootPath, fullPath);
        if (!string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            foreach (var segment in relativePath.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, segment);
                var child = new FolderTreeNode(segment, currentPath)
                {
                    IsExpanded = true
                };
                currentNode.Children.Add(child);
                currentNode = child;
            }
        }

        AddImmediateDirectories(currentNode, fullPath);
        return rootNode;
    }

    private static void AddImmediateDirectories(FolderTreeNode node, string path)
    {
        foreach (var directory in EnumerateDirectories(path))
        {
            node.Children.Add(new FolderTreeNode(Path.GetFileName(directory), directory));
        }
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

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return root;
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            return Directory.Exists(fullPath) ? fullPath : root;
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

    private static IEnumerable<FileInfo> EnumerateImageFiles(string folder, bool recursive)
    {
        foreach (var file in EnumerateFiles(folder, recursive))
        {
            if (ImageExtensions.Contains(file.Extension))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string folder, bool recursive)
    {
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (recursive)
            {
                foreach (var child in EnumerateDirectories(current).Reverse())
                {
                    pending.Push(child);
                }
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
