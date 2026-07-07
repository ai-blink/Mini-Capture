using System.Drawing;
using MiniCapture;

var tests = new (string Name, Action Run)[]
{
    ("SelectTargetAt_ReturnsFrontMostMatchingCandidate", SelectTargetAt_ReturnsFrontMostMatchingCandidate),
    ("SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow", SelectTargetAt_ReturnsLargeFrontWindowBeforeSmallerCoveredWindow),
    ("SelectTargetAt_SkipsCandidatesOutsideCursorPoint", SelectTargetAt_SkipsCandidatesOutsideCursorPoint),
    ("SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint", SelectTargetAt_ReturnsNullWhenNoCandidateContainsPoint)
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

static WindowCaptureTarget Target(int hwnd, Rectangle bounds, string title)
{
    return new WindowCaptureTarget(new IntPtr(hwnd), bounds, title);
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
