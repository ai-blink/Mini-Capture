# Dev Architecture

## Preferred Stack

C# WPF for UI plus Win32 P/Invoke and Windows.Graphics.Capture for capture integration.

## Main Modules

- `FloatingButton`: transparent topmost entrypoint, position memory, mode menu.
- `CaptureMenu`: drag/window/full/timer commands.
- `RegionCaptureOverlay`: selection dim layer, rectangle drag, cancel/confirm.
- `WindowPickerOverlay`: cursor HWND detection, DWM bounds highlight, click capture.
- `CaptureEngine`: Windows.Graphics.Capture first, BitBlt/PrintWindow fallback only when needed.
- `SaveService`: PNG path, filename, path validation, save result.
- `ShellService`: open file, open folder, select file in Explorer.
- `MiniViewer`: image loading, zoom, fit, 100%, previous/next.
- `CaptureFileIndex`: folder scan, image filtering, sort, change detection.
- `FolderTreeModel`: quick access, This PC, capture folder, year/month/day nodes.
- `MiniExplorer`: tree, address/search, details and icon views.
- `ThumbnailService`: lazy thumbnails, cache, cancellation.

## Dependency Direction

UI surfaces call services through narrow interfaces. Capture/save/index services should not depend on viewer UI.

## Validation Strategy

Start with manual Windows validation for capture overlays, DPI, multi-monitor behavior, save path, and viewer/browser interactions. Add focused unit tests for filename generation, path validation, sorting, file filtering, and tree model behavior.
