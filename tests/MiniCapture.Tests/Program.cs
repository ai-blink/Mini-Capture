using System.Drawing;
using System.ComponentModel;
using MiniCapture;

var tests = new (string Name, Action Run)[]
{
    ("SelectTargetAt_ReturnsFrontMostMatchingCandidate", SelectTargetAt_ReturnsFrontMostMatchingCandidate),
    ("SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow", SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow),
    ("SelectTargetAt_SkipsCandidatesOutsideCursorPoint", SelectTargetAt_SkipsCandidatesOutsideCursorPoint),
    ("SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint", SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint),
    ("IsIgnoredProcessName_RecognizesDimScreenOverlay", IsIgnoredProcessName_RecognizesDimScreenOverlay),
    ("WindowExclusions_NormalizeAndMatchExecutablePaths", WindowExclusions_NormalizeAndMatchExecutablePaths),
    ("WindowExclusions_RetainProcessNameAndSwitchOnlyByMode", WindowExclusions_RetainProcessNameAndSwitchOnlyByMode),
    ("ImageExtensions_NormalizeAndPreserveCustomRegistration", ImageExtensions_NormalizeAndPreserveCustomRegistration),
    ("GetImages_AllowsExternalFolderWithoutRecursiveScan", GetImages_AllowsExternalFolderWithoutRecursiveScan),
    ("GetImages_DoesNotRecursivelyScanCaptureSubfolders", GetImages_DoesNotRecursivelyScanCaptureSubfolders),
    ("BuildFolderTree_IncludesExternalPathAndChildFolders", BuildFolderTree_IncludesExternalPathAndChildFolders),
    ("FolderSelection_IgnoresCurrentExternalFolder", FolderSelection_IgnoresCurrentExternalFolder),
    ("RequiresDetailsView_ForLargeFolder", RequiresDetailsView_ForLargeFolder),
    ("SortFiles_UsesRequestedColumnAndDirection", SortFiles_UsesRequestedColumnAndDirection),
    ("CalculateCropRectangle_ClampsMarginsAndPreservesOnePixel", CalculateCropRectangle_ClampsMarginsAndPreservesOnePixel),
    ("BuildGaussianKernel_NormalizesAndIsSymmetric", BuildGaussianKernel_NormalizesAndIsSymmetric),
    ("ApplyGaussianBlurRegion_PreservesUniformColorAndSoftensSharpEdge", ApplyGaussianBlurRegion_PreservesUniformColorAndSoftensSharpEdge),
    ("DecodeImageFile_AllowsBackgroundDecode", DecodeImageFile_AllowsBackgroundDecode),
    ("ClipboardService_RetriesClipboardCannotOpen", ClipboardService_RetriesClipboardCannotOpen),
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

static void IsIgnoredProcessName_RecognizesDimScreenOverlay()
{
    AssertTrue(WindowPickerService.IsIgnoredProcessName("DimScreen"), "DimScreen overlay process should not be selectable");
    AssertTrue(!WindowPickerService.IsIgnoredProcessName("notepad"), "ordinary processes should remain selectable");
}

static void WindowExclusions_NormalizeAndMatchExecutablePaths()
{
    var paths = MiniCaptureSettings.NormalizeWindowCaptureExcludedExecutablePaths(
        [@"C:\\Apps\\DimScreen.exe", @"c:\\apps\\dimscreen.exe", "", "DimScreen"]);

    AssertIntEqual(1, paths.Count, "normalized exclusion count");
    AssertTrue(
        WindowPickerService.IsExcludedExecutablePath(@"C:\\Apps\\DimScreen.exe", paths.ToHashSet(StringComparer.OrdinalIgnoreCase)),
        "configured executable path should be excluded");
}

static void WindowExclusions_RetainProcessNameAndSwitchOnlyByMode()
{
    var targets = MiniCaptureSettings.NormalizeWindowCaptureExclusionTargets(
        [
            new WindowCaptureExclusionTarget
            {
                ExecutablePath = @"C:\ai\projects\key-demo-osk\KeyDemoOsk.exe",
                ProcessName = "KeyDemoOsk",
                MatchMode = WindowCaptureExclusionMatchMode.FilePath
            }
        ],
        legacyExecutablePaths: []);

    AssertIntEqual(1, targets.Count, "normalized exclusion target count");
    AssertTrue(
        string.Equals("KeyDemoOsk", targets[0].ProcessName, StringComparison.Ordinal),
        "process name should be retained beside the executable path");
    AssertTrue(
        targets[0].MatchMode == WindowCaptureExclusionMatchMode.FilePath,
        "a target with an executable path should default to file-path matching");
    AssertTrue(
        WindowPickerService.IsExcludedWindowTarget(
            @"C:\ai\projects\key-demo-osk\KeyDemoOsk.exe",
            "OtherProcess",
            targets),
        "file-path mode should use the stored executable path");
    AssertTrue(
        !WindowPickerService.IsExcludedWindowTarget(
            @"C:\other\KeyDemoOsk.exe",
            "KeyDemoOsk",
            targets),
        "file-path mode should not fall through to the stored process name");

    targets[0].MatchMode = WindowCaptureExclusionMatchMode.ProcessName;
    AssertTrue(
        WindowPickerService.IsExcludedWindowTarget(
            @"C:\other\KeyDemoOsk.exe",
            "KeyDemoOsk",
            targets),
        "process-name mode should use the retained process name after the radio-mode change");
    AssertTrue(
        string.Equals(@"C:\ai\projects\key-demo-osk\KeyDemoOsk.exe", targets[0].ExecutablePath, StringComparison.Ordinal),
        "changing match mode must retain the executable path");

    var nameOnlyTargets = MiniCaptureSettings.NormalizeWindowCaptureExclusionTargets(
        [
            new WindowCaptureExclusionTarget
            {
                ProcessName = "PathUnavailableProcess",
                MatchMode = WindowCaptureExclusionMatchMode.FilePath
            }
        ],
        legacyExecutablePaths: []);

    AssertIntEqual(1, nameOnlyTargets.Count, "path-unavailable exclusion target count");
    AssertTrue(
        nameOnlyTargets[0].MatchMode == WindowCaptureExclusionMatchMode.ProcessName,
        "a target without an executable path should fall back to process-name matching");
    AssertTrue(
        WindowPickerService.IsExcludedWindowTarget(
            executablePath: null,
            processName: "PathUnavailableProcess",
            nameOnlyTargets),
        "path-unavailable target should match by process name");

    var upgradedTargets = MiniCaptureSettings.NormalizeWindowCaptureExclusionTargets(
        [
            new WindowCaptureExclusionTarget
            {
                ProcessName = "KeyDemoOsk",
                MatchMode = WindowCaptureExclusionMatchMode.ProcessName
            },
            new WindowCaptureExclusionTarget
            {
                ExecutablePath = @"C:\ai\projects\key-demo-osk\KeyDemoOsk.exe",
                ProcessName = "KeyDemoOsk",
                MatchMode = WindowCaptureExclusionMatchMode.FilePath
            }
        ],
        legacyExecutablePaths: []);

    AssertIntEqual(1, upgradedTargets.Count, "path-upgraded exclusion target count");
    AssertTrue(
        string.Equals(@"C:\ai\projects\key-demo-osk\KeyDemoOsk.exe", upgradedTargets[0].ExecutablePath, StringComparison.Ordinal),
        "a later resolved executable path should upgrade the name-only target");
    AssertTrue(
        upgradedTargets[0].MatchMode == WindowCaptureExclusionMatchMode.FilePath,
        "a later resolved executable path should restore the file-path default");
}

static void ImageExtensions_NormalizeAndPreserveCustomRegistration()
{
    AssertTrue(
        FileAssociationRegistrar.TryNormalizeExtension("BMP", out var bmp) && bmp == ".bmp",
        "extension input should be normalized with a leading lowercase period");
    AssertTrue(
        !FileAssociationRegistrar.TryNormalizeExtension(".image-format", out _),
        "unsafe extension characters should be rejected");

    var additional = FileAssociationRegistrar.NormalizeAdditionalExtensions(
        [".bmp", "BMP", ".png", ".tiff", ".bad-extension"]);
    AssertTrue(
        additional.SequenceEqual([".bmp", ".tiff"], StringComparer.OrdinalIgnoreCase),
        "custom extensions should be deduplicated and exclude built-in or invalid values");

    var registrations = FileAssociationRegistrar.GetRegistrationExtensions(additional);
    AssertTrue(
        registrations.SequenceEqual([".png", ".jpg", ".jpeg", ".bmp", ".tiff"], StringComparer.OrdinalIgnoreCase),
        "default and custom extensions should be registered together");

    var settings = new MiniCaptureSettings { AdditionalImageExtensions = ["GIF", ".png", ".invalid-extension"] };
    var clone = settings.Clone();
    AssertTrue(
        clone.AdditionalImageExtensions.SequenceEqual([".gif"], StringComparer.OrdinalIgnoreCase),
        "settings copies should retain only valid custom extensions");
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
    AssertTrue(!ViewerWindow.RequiresDetailsView(ViewerWindow.MaxIconViewFiles), "icon views should remain available at the threshold");
    AssertTrue(ViewerWindow.RequiresDetailsView(ViewerWindow.MaxIconViewFiles + 1), "large folders should use the virtualized details view");
    AssertTrue(!ViewerWindow.RequiresDetailsView(1), "small folders should allow icon views");
}

static void SortFiles_UsesRequestedColumnAndDirection()
{
    var folder = CreateTemporaryFolder();
    try
    {
        var alphaPath = Path.Combine(folder, "alpha.png");
        var betaPath = Path.Combine(folder, "beta.jpg");
        var gammaPath = Path.Combine(folder, "gamma.jpeg");
        File.WriteAllBytes(alphaPath, new byte[30]);
        File.WriteAllBytes(betaPath, new byte[10]);
        File.WriteAllBytes(gammaPath, new byte[20]);
        File.SetLastWriteTime(alphaPath, new DateTime(2026, 1, 1, 10, 0, 0));
        File.SetLastWriteTime(betaPath, new DateTime(2026, 1, 2, 10, 0, 0));
        File.SetLastWriteTime(gammaPath, new DateTime(2026, 1, 3, 10, 0, 0));

        var files = new[]
        {
            new CaptureImageFile(new FileInfo(betaPath)),
            new CaptureImageFile(new FileInfo(gammaPath)),
            new CaptureImageFile(new FileInfo(alphaPath))
        };

        AssertFileOrder(
            ["alpha.png", "beta.jpg", "gamma.jpeg"],
            ViewerWindow.SortFiles(files, ViewerFileSortColumn.Name, ListSortDirection.Ascending),
            "name ascending");
        AssertFileOrder(
            ["gamma.jpeg", "beta.jpg", "alpha.png"],
            ViewerWindow.SortFiles(files, ViewerFileSortColumn.ModifiedDate, ListSortDirection.Descending),
            "modified date descending");
        AssertFileOrder(
            ["beta.jpg", "gamma.jpeg", "alpha.png"],
            ViewerWindow.SortFiles(files, ViewerFileSortColumn.Size, ListSortDirection.Ascending),
            "size ascending");
        AssertFileOrder(
            ["alpha.png", "beta.jpg", "gamma.jpeg"],
            ViewerWindow.SortFiles(files, ViewerFileSortColumn.Type, ListSortDirection.Descending),
            "type descending");
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
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

static void CalculateCropRectangle_ClampsMarginsAndPreservesOnePixel()
{
    var regularCrop = ViewerWindow.CalculateCropRectangle(100, 80, 10, 20, 5, 15);
    AssertIntEqual(10, regularCrop.X, "crop left");
    AssertIntEqual(5, regularCrop.Y, "crop top");
    AssertIntEqual(70, regularCrop.Width, "crop width");
    AssertIntEqual(60, regularCrop.Height, "crop height");

    var constrainedCrop = ViewerWindow.CalculateCropRectangle(10, 8, 99, 99, 99, 99);
    AssertIntEqual(9, constrainedCrop.X, "constrained crop x");
    AssertIntEqual(7, constrainedCrop.Y, "constrained crop y");
    AssertIntEqual(1, constrainedCrop.Width, "constrained crop width");
    AssertIntEqual(1, constrainedCrop.Height, "constrained crop height");
}

static void BuildGaussianKernel_NormalizesAndIsSymmetric()
{
    var kernel = ViewerWindow.BuildGaussianKernel(5, 2.5);
    AssertIntEqual(11, kernel.Length, "kernel length for radius 5");

    var sum = 0.0;
    foreach (var weight in kernel)
    {
        sum += weight;
    }

    AssertTrue(Math.Abs(sum - 1.0) < 0.0001, $"kernel weights should normalize to 1.0, got {sum}");

    for (var i = 0; i < kernel.Length / 2; i++)
    {
        AssertTrue(
            Math.Abs(kernel[i] - kernel[kernel.Length - 1 - i]) < 0.0000001,
            "kernel should be symmetric around its center");
    }
}

static void ApplyGaussianBlurRegion_PreservesUniformColorAndSoftensSharpEdge()
{
    const int width = 20;
    const int height = 20;
    const int stride = width * 4;

    var uniformPixels = new byte[stride * height];
    for (var i = 0; i < uniformPixels.Length; i += 4)
    {
        uniformPixels[i] = 100;
        uniformPixels[i + 1] = 150;
        uniformPixels[i + 2] = 200;
        uniformPixels[i + 3] = 255;
    }

    ViewerWindow.ApplyGaussianBlurRegion(uniformPixels, stride, 2, 2, 18, 18, 4.0);

    var centerOffset = (10 * stride) + (10 * 4);
    AssertIntEqual(100, uniformPixels[centerOffset], "uniform region blue after blur");
    AssertIntEqual(150, uniformPixels[centerOffset + 1], "uniform region green after blur");
    AssertIntEqual(200, uniformPixels[centerOffset + 2], "uniform region red after blur");
    AssertIntEqual(255, uniformPixels[centerOffset + 3], "uniform region alpha after blur");

    var splitPixels = new byte[stride * height];
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var offset = (y * stride) + (x * 4);
            var value = (byte)(x < width / 2 ? 0 : 255);
            splitPixels[offset] = value;
            splitPixels[offset + 1] = value;
            splitPixels[offset + 2] = value;
            splitPixels[offset + 3] = 255;
        }
    }

    ViewerWindow.ApplyGaussianBlurRegion(splitPixels, stride, 0, 0, width, height, 4.0);

    var edgeOffset = (10 * stride) + ((width / 2) * 4);
    AssertTrue(
        splitPixels[edgeOffset] > 0 && splitPixels[edgeOffset] < 255,
        $"blurred edge pixel should sit strictly between 0 and 255, got {splitPixels[edgeOffset]}");
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

static void ClipboardService_RetriesClipboardCannotOpen()
{
    const string expectedText = @"C:\captures\sample.png";
    var attempts = 0;
    var delays = new List<TimeSpan>();
    string? copiedText = null;

    ClipboardService.SetTextAsync(
        expectedText,
        text =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new System.Runtime.InteropServices.COMException(
                    "The clipboard is temporarily unavailable.",
                    unchecked((int)0x800401D0));
            }

            copiedText = text;
        },
        delay =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        },
        maxAttempts: 5).GetAwaiter().GetResult();

    AssertIntEqual(3, attempts, "clipboard write attempts");
    AssertIntEqual(2, delays.Count, "clipboard retry delays");
    AssertTrue(copiedText == expectedText, "clipboard retry should eventually copy the requested path");
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

static void AssertFileOrder(
    IReadOnlyList<string> expectedNames,
    IReadOnlyList<CaptureImageFile> actual,
    string label)
{
    var actualNames = actual.Select(file => file.FileName).ToArray();
    if (!expectedNames.SequenceEqual(actualNames, StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Expected {label} [{string.Join(", ", expectedNames)}], got [{string.Join(", ", actualNames)}].");
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
