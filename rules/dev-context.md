# Dev Context

## Resume Point

P0-P3 implementation is complete.

Recent commits:

- `fb67b43`: P0 WPF skeleton, floating capture button, four-mode menu.
- `246231d`: P1 drag/full/timer capture, PNG auto-save, result panel.
- `e956f19`: P2 window picker, DWM bounds highlight, selected-window PNG capture, Esc cancel.
- P3 current slice: internal viewer, zoom controls, previous/next navigation, capture folder image index, Explorer-style folder tree, details view, and small/medium/large icon views.

## Active Design

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- HTML mockup: `notes/brainstorm/mini-capture-interactive.html`

## Immediate Next Step

Start P4 implementation from `doc/app-dev/04-execution-tasks.md`: DPI/multi-monitor checks, repeated capture loops, packaging choice, and release notes.

## Current Stack Decision

Use C# WPF plus Win32 P/Invoke. Current capture implementation uses GDI `CopyFromScreen` with DWM bounds for window capture; the P3 viewer/browser is WPF-only and indexes PNG/JPG/JPEG files under `Pictures\MiniCapture`. Windows.Graphics.Capture remains a future hardening option for protected/accelerated windows.
