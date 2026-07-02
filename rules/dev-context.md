# Dev Context

## Resume Point

P0-P2 implementation is complete and committed.

Recent commits:

- `fb67b43`: P0 WPF skeleton, floating capture button, four-mode menu.
- `246231d`: P1 drag/full/timer capture, PNG auto-save, result panel.
- `e956f19`: P2 window picker, DWM bounds highlight, selected-window PNG capture, Esc cancel.

## Active Design

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- HTML mockup: `notes/brainstorm/mini-capture-interactive.html`

## Immediate Next Step

Start P3 implementation from `doc/app-dev/04-execution-tasks.md`: mini image viewer plus Windows Explorer-style capture folder browser.

## Current Stack Decision

Use C# WPF plus Win32 P/Invoke. Current capture implementation uses GDI `CopyFromScreen` with DWM bounds for window capture; Windows.Graphics.Capture remains a future hardening option for protected/accelerated windows.
