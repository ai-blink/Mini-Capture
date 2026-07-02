# Dev Context

## Resume Point

P0-P3 and P3 usability polish are complete. The next slice is P4 hardening and package checks.

Recent commits:

- `fb67b43`: P0 WPF skeleton, floating capture button, four-mode menu.
- `246231d`: P1 drag/full/timer capture, PNG auto-save, result panel.
- `e956f19`: P2 window picker, DWM bounds highlight, selected-window PNG capture, Esc cancel.
- `311e693`: P3 internal viewer and first Explorer-style browser slice.

Latest polish:

- Floating button starts at the left edge, matches the HTML mock shape more closely, supports click/drag without duplicate menus, and opens a compact radial four-button menu above the main button.
- Viewer icon file views wrap downward with horizontal scrolling disabled.

## Active Design

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- HTML mockup: `notes/brainstorm/mini-capture-interactive.html`

## Immediate Next Step

Start P4 from `doc/app-dev/04-execution-tasks.md`: DPI/multi-monitor checks, repeated capture loops, packaging choice, and release notes.

## Current Stack Decision

C# WPF plus Win32 P/Invoke remains the stack. Capture uses GDI `CopyFromScreen` with DWM bounds for window capture; the viewer/browser is WPF-only and indexes PNG/JPG/JPEG files under `Pictures\MiniCapture`.
