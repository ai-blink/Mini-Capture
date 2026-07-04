# Dev Context

## Resume Point

P0-P4, viewer/browser reinforcement, overlay-free selection, TabPaint-inspired editor workflow, toolbar UI polish, Dark Screenshot Markup Shell, and first package checks are complete. The next slice is V1 release-candidate cleanup or manual hardware validation.

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

## Active Design

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- HTML mockup: `notes/brainstorm/mini-capture-interactive.html`

## Immediate Next Step

Use the P4 run report in `doc/app-dev/05-run-report.md` as the latest release-readiness baseline. True multi-monitor hardware validation and any installer/self-contained package decision remain follow-ups.

## Current Stack Decision

C# WPF plus Win32 P/Invoke remains the stack. Capture uses GDI `CopyFromScreen` with DWM bounds for window capture; the viewer/browser/editor is WPF-only and indexes PNG/JPG/JPEG files under `Pictures\MiniCapture`.
