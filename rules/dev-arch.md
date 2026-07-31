# Dev Architecture

## Preferred Stack

C# WPF for UI plus Win32 P/Invoke for overlays and window picking. Current capture uses GDI `CopyFromScreen`; Windows.Graphics.Capture remains a future hardening option for protected or accelerated windows.

## Main Modules

- `FloatingButton`: transparent topmost entrypoint, position memory, mode menu.
- `CaptureMenu`: drag/window/full/timer commands.
- `RegionCaptureOverlay`: selection dim layer, rectangle drag, cancel/confirm.
- `WindowPickerOverlay`: cursor HWND detection, DWM bounds highlight, click capture.
- `ScreenCaptureService`: virtual-screen, region, and selected-window PNG capture.
- `NativeWindowApi`: top-level window enumeration, DWM visible bounds, and capture-exclusion hints.
- `WindowPickerService`: topmost visible non-app window selection under cursor.
- `SaveService`: PNG path, filename, path validation, save result. Currently folded into `ScreenCaptureService` until the save flow grows.
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

## Current Implementation Status

- P0 done: WPF app shell, floating topmost button, four-mode menu, clean exit.
- P1 done: drag/full/timer capture, dated PNG auto-save, result panel actions.
- P2 done: window picker overlay, DWM bounds highlight, selected-window PNG capture, Esc cancel.
- P3 done: internal image viewer, capture folder index, Explorer-style folder tree/file pane, details view, and small/medium/large icon views.
- P4 done: DPI/package checks, overlay-free selection, editable raster markup, layout persistence, external-image activation, and extension settings.
- Viewer reinforcement done: single-instance file handoff, cancellable background full-resolution decode, current-folder refresh, and sortable Details columns.
