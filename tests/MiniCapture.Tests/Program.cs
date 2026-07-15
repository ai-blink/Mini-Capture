using System.Drawing;
using MiniCapture;

var tests = new (string Name, Action Run)[]
{
    ("SelectTargetAt_ReturnsFrontMostMatchingCandidate", SelectTargetAt_ReturnsFrontMostMatchingCandidate),
    ("SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow", SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow),
    ("SelectTargetAt_SkipsCandidatesOutsideCursorPoint", SelectTargetAt_SkipsCandidatesOutsideCursorPoint),
    ("SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint", SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint),
    ("GetImages_AllowsExternalFolderWithoutRecursiveScan", GetImages_AllowsExternalFolderWithoutRecursiveScan),
    ("GetImages_DoesNotRecursivelyScanCaptureSubfolders", GetImages_DoesNotRecursivelyScanCaptureSubfolders),
    ("BuildFolderTree_IncludesExternalPathAndChildFolders", BuildFolderTree_IncludesExternalPathAndChildFolders),
    ("FolderSelection_IgnoresCurrentExternalFolder", FolderSelection_IgnoresCurrentExternalFolder),
    ("RequiresDetailsView_ForLargeFolder", RequiresDetailsView_ForLargeFolder),
    ("DecodeImageFile_AllowsBackgroundDecode", DecodeImageFile_AllowsBackgroundDecode),
    ("SingleInstance_ForwardsArgumentsToPrimary", SingleInstance_ForwardsArgumentsToPrimary)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

static void SelectTargetAt_ReturnsFrontMostMatchingCandidate()
{
    var front = Target(1, new Rectangle(0, 0, 240, 200), "front");
    var behind = Target(2, new Rectangle(10, 10, 80, 70), "behind");

    var selected = WindowPickerService.SelectTargetAt(new Point(20, 20), [front, behind]);

    AssertEqual(front, selected);
}

static void SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow()
{
    var largeFront = Target(3, new Rectangle(0, 0, 1920, 1080), "large front");
    var smallBehind = Target(4, new Rectangle(300, 240, 420, 320), "small behind");

    var selected = WindowPickerService.SelectTargetAt(new Point(360, 300), [largeFront, smallBehind]);

    AssertEqual(largeFront, selected);
}

static void SelectTargetAt_SkipsCandidatesOutsideCursorPoint()
{
    var outside = Target(5, new Rectangle(0, 0, 100, 100), "outside");
    var inside = Target(6, new Rectangle(200, 200, 100, 100), "inside");

    var selected = WindowPickerService.SelectTargetAt(new Point(240, 240), [outside, inside]);

    AssertEqual(inside, selected);
}

static void SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint()
{
    var selected = WindowPickerService.SelectTargetAt(
        new Point(500, 500),
        [Target(7, new Rectangle(0, 0, 100, 100), "outside")]);

    if (selected is not null)
    {
        throw new InvalidOperationException($"Expected no target, got {selected.Value.Title}.");
    }
}

static void GetImages_AllowsExternalFolderWithoutRecursiveScan()
{
    var folder = CreateTemporaryFolder();
    try
    {
        var childFolder = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName;
        var pngPath = Path.Combine(folder, "current.png");
        var jpgPath = Path.Combine(folder, "current.jpg");
        var nestedPath = Path.Combine(childFolder, "nested.png");
        File.WriteAllBytes(pngPath, [1]);
        File.WriteAllBytes(jpgPath, [2]);
        File.WriteAllBytes(nestedPath, [3]);
        File.WriteAllText(Path.Combine(folder, "ignored.txt"), "not an image");

        var images = CaptureFileIndex.GetImages(folder);

        AssertIntEqual(2, images.Count, "external image count");
        AssertTrue(images.Any(image => PathsEqual(image.Path, pngPath)), "external PNG should be indexed");
        AssertTrue(images.Any(image => PathsEqual(image.Path, jpgPath)), "external JPG should be indexed");
        AssertTrue(images.All(image => !PathsEqual(image.Path, nestedPath)), "external child folders should not be scanned recursively");
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
}

static void BuildFolderTree_IncludesExternalPathAndChildFolders()
{
    var folder = CreateTemporaryFolder();
    try
    {
        var childFolder = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName;

        var nodes = CaptureFileIndex.BuildFolderTree(folder);
        var flattened = Flatten(nodes).ToList();

        AssertTrue(flattened.Any(node => PathsEqual(node.Path, folder)), "external target folder should appear in the tree");
        AssertTrue(flattened.Any(node => PathsEqual(node.Path, childFolder)), "external child folder should appear in the tree");
        AssertIntEqual(1, flattened.Count(node => PathsEqual(node.Path, folder)), "external target tree node count");
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
}

static void GetImages_DoesNotRecursivelyScanCaptureSubfolders()
{
    var folder = Path.Combine(CaptureFileIndex.RootDirectory, $"MiniCapture.Tests.{Guid.NewGuid():N}");
    Directory.CreateDirectory(folder);

    try
    {
        var childFolder = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName;
        var directPath = Path.Combine(folder, "direct.png");
        var nestedPath = Path.Combine(childFolder, "nested.png");
        File.WriteAllBytes(directPath, [1]);
        File.WriteAllBytes(nestedPath, [2]);

        var images = CaptureFileIndex.GetImages(folder);

        AssertIntEqual(1, images.Count, "capture-folder direct image count");
        AssertTrue(PathsEqual(images[0].Path, directPath), "capture-folder child images should not be indexed");
        AssertTrue(images.All(image => !PathsEqual(image.Path, nestedPath)), "capture-folder child folders should not be scanned recursively");
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
}

static void RequiresDetailsView_ForLargeFolder()
{
    AssertTrue(!ViewerWindow.RequiresDetailsView(false, ViewerWindow.MaxIconViewFiles), "capture-root icon views should remain available at the threshold");
    AssertTrue(ViewerWindow.RequiresDetailsView(false, ViewerWindow.MaxIconViewFiles + 1), "large capture-root folders should use the virtualized details view");
    AssertTrue(ViewerWindow.RequiresDetailsView(true, 1), "external folders should use the virtualized details view regardless of file count");
}

static void FolderSelection_IgnoresCurrentExternalFolder()
{
    const string activeFolder = @"C:\images\external";

    AssertTrue(
        ViewerWindow.IsSameFolderSelection(activeFolder, @"c:\IMAGES\EXTERNAL"),
        "selecting the active external folder should be recognized as the same navigation target");
    AssertTrue(
        !ViewerWindow.IsSameFolderSelection(activeFolder, @"C:\images\other"),
        "selecting another external folder should still navigate");
}

static void DecodeImageFile_AllowsBackgroundDecode()
{
    var folder = CreateTemporaryFolder();
    try
    {
        var path = Path.Combine(folder, "image.png");
        using (var bitmap = new System.Drawing.Bitmap(7, 5))
        {
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        var image = Task.Run(() => ViewerWindow.DecodeImageFile(path)).GetAwaiter().GetResult();

        AssertIntEqual(7, image.PixelWidth, "decoded image width");
        AssertIntEqual(5, image.PixelHeight, "decoded image height");
        AssertTrue(image.IsFrozen, "background-decoded image should be frozen before UI use");
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
}

static void SingleInstance_ForwardsArgumentsToPrimary()
{
    var id = Guid.NewGuid().ToString("N");
    var mutexName = $"Local\\MiniCapture.Tests.{id}";
    var pipeName = $"MiniCapture.Tests.{id}";
    using var primary = CreatePrimary(mutexName, pipeName);
    using var received = new ManualResetEventSlim();
    string[]? deliveredArguments = null;
    primary.StartListening(arguments =>
    {
        deliveredArguments = arguments;
        received.Set();
    });

    var expectedArguments = new[] { @"C:\\images\\sample.png" };
    AssertTrue(SingleInstanceCoordinator.NotifyPrimary(pipeName, expectedArguments), "secondary instance should reach the primary");
    AssertTrue(received.Wait(TimeSpan.FromSeconds(2)), "primary should receive forwarded arguments");
    AssertTrue(deliveredArguments is not null && deliveredArguments.SequenceEqual(expectedArguments), "forwarded arguments should be unchanged");
}

static SingleInstanceCoordinator CreatePrimary(string mutexName, string pipeName)
{
    if (!SingleInstanceCoordinator.TryCreate(mutexName, pipeName, out var primary) || primary is null)
    {
        throw new InvalidOperationException("Expected the isolated test instance to acquire its mutex.");
    }

    return primary;
}

static WindowCaptureTarget Target(int hwnd, Rectangle bounds, string title)
{
    return new WindowCaptureTarget(new IntPtr(hwnd), bounds, title);
}

static string CreateTemporaryFolder()
{
    var folder = Path.Combine(Path.GetTempPath(), $"MiniCapture.Tests.{Guid.NewGuid():N}");
    Directory.CreateDirectory(folder);
    return folder;
}

static IEnumerable<FolderTreeNode> Flatten(IEnumerable<FolderTreeNode> nodes)
{
    foreach (var node in nodes)
    {
        yield return node;
        foreach (var child in Flatten(node.Children))
        {
            yield return child;
        }
    }
}

static bool PathsEqual(string? left, string? right)
{
    return !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertIntEqual(int expected, int actual, string label)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Expected {label} {expected}, got {actual}.");
    }
}

static void AssertEqual(WindowCaptureTarget expected, WindowCaptureTarget? actual)
{
    if (actual is null)
    {
        throw new InvalidOperationException($"Expected {expected.Title}, got no target.");
    }

    if (actual.Value.Hwnd != expected.Hwnd || actual.Value.Bounds != expected.Bounds || actual.Value.Title != expected.Title)
    {
        throw new InvalidOperationException($"Expected {expected.Title}, got {actual.Value.Title}.");
    }
}
