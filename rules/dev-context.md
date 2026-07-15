# Dev Context

## Resume Point

P0-P4, viewer/browser reinforcement, overlay-free selection, TabPaint-inspired editor workflow, toolbar UI polish, Dark Screenshot Markup Shell, first package checks, V1 viewer performance/file-entry cleanup, extension settings, editable viewer markup, and viewer layout persistence are complete. The next slice is manual hardware validation or installer/default-app packaging if distribution needs it.

Recent commits:

- `fb67b43`: P0 WPF skeleton, floating capture button, four-mode menu.
- `246231d`: P1 drag/full/timer capture, PNG auto-save, result panel.
- `e956f19`: P2 window picker, DWM bounds highlight, selected-window PNG capture, Esc cancel.
- `311e693`: P3 internal viewer and first Explorer-style browser slice.
- `0b89e6e`: Viewer/browser V1 reinforcement.
- `72f5ad5`: Overlay-free region/window selection.
- `ff4e190`: Viewer window and Explorer file-list layout persistence.

Latest polish:

- Floating button starts at the left edge, matches the HTML mock shape more closely, supports click/drag without duplicate menus, and opens a compact radial four-button menu above the main button.
- Viewer icon file views wrap downward with horizontal scrolling disabled.
- Viewer editor now has pen, arrow, stroke thickness, rotate, undo/redo, save, and clipboard workflows while staying raster-only and capture-root scoped.
- Viewer toolbar now uses compact icon action groups, edit glyph buttons, color swatches, and inline stroke/text controls inspired by TabPaint.
- P4 checks confirmed Per-Monitor DPI awareness, repeated PNG capture, drag/window/full/timer smoke coverage, viewer action availability, close-to-tray behavior, and a launchable framework-dependent `win-x64` publish folder.
- ViewerWindow now resolves latest images without sorting the full index, loads folder contents asynchronously in UI batches, decodes thumbnails in the background with limited concurrency, and accepts PNG/JPG/JPEG file paths as startup arguments.
- Windows PNG/JPG/JPEG default-app association is intentionally deferred to an installer/registry slice; V1 code now supports the required quoted `%1`-style file path argument but does not mutate system associations.
- Viewer editor annotations are selectable before save: newly added rectangle, ellipse, pen, arrow, and text markup can be moved/resized/deleted, and text markup is edited through regular WPF text boxes. Save/copy image still exports a flattened bitmap.
- ViewerWindow persists its window bounds/state, Explorer view mode, and capture-library/file-list pane widths through the existing local app data `settings.json`.
- ViewerWindow now keeps images opened outside `Pictures\MiniCapture` synchronized with their actual parent address, a unique expanded drive/share path tree, and a non-recursive same-folder image list; capture-root indexing remains recursive.
- File association launches now reuse the existing MiniCapture process through a named mutex/pipe handoff, so repeatedly opening images does not multiply app processes. The viewer handles left/right keys before the file list and moves through the current folder's sibling images.
- Full-resolution viewer decoding now runs off the UI thread with cancellation for stale selections. Build and regression tests pass, but an actual high-resolution external-image desktop pass is still required before calling the reported freeze resolved.

## Active Design

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- HTML mockup: `notes/brainstorm/mini-capture-interactive.html`

## Immediate Next Step

Use the latest sections of `doc/app-dev/05-run-report.md` as the release-readiness baseline. Manually validate repeated association launches, left/right sibling navigation, and a high-resolution external PNG/JPG/JPEG address/tree/list/save workflow on the interactive desktop; true multi-monitor hardware validation remains a follow-up.

## Current Stack Decision

C# WPF plus Win32 P/Invoke remains the stack. Capture uses GDI `CopyFromScreen` with DWM bounds for window capture; the viewer/browser/editor is WPF-only, recursively indexes PNG/JPG/JPEG files under `Pictures\MiniCapture`, browses the direct parent folder when an external image is passed on the command line, and decodes the selected full-resolution image on a background worker.
