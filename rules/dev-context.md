# Dev Context

## Resume Point

P0-P4, viewer/browser reinforcement, overlay-free selection, TabPaint-inspired editor workflow, toolbar UI polish, Dark Screenshot Markup Shell, first package checks, and V1 viewer performance/file-entry cleanup are complete. The next slice is manual hardware validation or installer/default-app packaging if distribution needs it.

Recent commits:

- `fb67b43`: P0 WPF skeleton, floating capture button, four-mode menu.
- `246231d`: P1 drag/full/timer capture, PNG auto-save, result panel.
- `e956f19`: P2 window picker, DWM bounds highlight, selected-window PNG capture, Esc cancel.
- `311e693`: P3 internal viewer and first Explorer-style browser slice.
- `0b89e6e`: Viewer/browser V1 reinforcement.
- `72f5ad5`: Overlay-free region/window selection.

Latest polish:

- Floating button starts at the left edge, matches the HTML mock shape more closely, supports click/drag without duplicate menus, and opens a compact radial four-button menu above the main button.
- Viewer icon file views wrap downward with horizontal scrolling disabled.
- Viewer editor now has pen, arrow, stroke thickness, rotate, undo/redo, save, and clipboard workflows while staying raster-only and capture-root scoped.
- Viewer toolbar now uses compact icon action groups, edit glyph buttons, color swatches, and inline stroke/text controls inspired by TabPaint.
- P4 checks confirmed Per-Monitor DPI awareness, repeated PNG capture, drag/window/full/timer smoke coverage, viewer action availability, close-to-tray behavior, and a launchable framework-dependent `win-x64` publish folder.
- ViewerWindow now resolves latest images without sorting the full index, loads folder contents asynchronously in UI batches, decodes thumbnails in the background with limited concurrency, and accepts PNG/JPG/JPEG file paths as startup arguments.
- Windows PNG/JPG/JPEG default-app association is intentionally deferred to an installer/registry slice; V1 code now supports the required quoted `%1`-style file path argument but does not mutate system associations.

## Active Design

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- HTML mockup: `notes/brainstorm/mini-capture-interactive.html`

## Immediate Next Step

Use the latest sections of `doc/app-dev/05-run-report.md` as the release-readiness baseline. True multi-monitor hardware validation and any installer/self-contained/default-app association decision remain follow-ups.

## Current Stack Decision

C# WPF plus Win32 P/Invoke remains the stack. Capture uses GDI `CopyFromScreen` with DWM bounds for window capture; the viewer/browser/editor is WPF-only, indexes PNG/JPG/JPEG files under `Pictures\MiniCapture`, and can preview a PNG/JPG/JPEG path passed on the command line.
