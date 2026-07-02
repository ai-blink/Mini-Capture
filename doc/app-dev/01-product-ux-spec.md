# 01 Product UX Spec

## Core Journey

1. App starts and shows a small transparent always-on-top capture button.
2. User clicks the button and sees four capture modes.
3. User captures a region, target window, full screen, or delayed shot.
4. App saves a PNG to the configured folder.
5. Result panel offers open file, open folder, and open viewer.
6. Viewer opens the image and supports zoom, fit, 100%, previous/next.
7. Mini explorer slides from the left with a Windows Explorer-style tree and file pane.

## Screen Intent

| Screen | Core action | Primary output |
|---|---|---|
| Floating button | Open capture menu | Always-available entrypoint |
| Capture menu | Choose capture mode | Transition into capture state |
| Region overlay | Drag rectangle | Selected area PNG |
| Window picker | Hover and click target window | Selected HWND/window image |
| Result panel | Open file/folder/viewer | Confirm saved PNG |
| Mini viewer | Zoom and navigate images | Fast image inspection |
| Mini explorer | Pick folder/file and view mode | Capture folder browsing |

## Mini Explorer UX

- Left panel slides into the viewer area.
- Top row uses navigation controls, address path, and search.
- Left tree shows quick access, This PC, Pictures, MiniCapture, year/month/day folders.
- Right pane supports details view and icon views.
- Icon views support small, medium, and large thumbnail sizes.
- Selection remains synchronized with the viewer.

## Failure And Re-entry

- `Esc` cancels capture overlays and returns to the floating button.
- Save failure shows path/permission guidance and folder selection.
- Missing/deleted file refreshes the list and moves to the next available image.
- Slow thumbnail generation must not block file list display.

## Acceptance Notes

The product can ship V1 without advanced editing, upload, OCR, scrolling capture, or video features if the capture/save/view/browse loop is reliable.
