# Dev Progress

## Current Status

- 2026-07-02: Brainstorming design approved and committed in `03d01aa`.
- 2026-07-02: App-dev workflow initialized from the approved design.
- 2026-07-02: P0 WPF skeleton, floating capture button, four-mode menu, placeholder mode selection, and shutdown flow implemented.
- 2026-07-02: P1 basic drag/full/timer capture, dated PNG auto-save, and result panel actions implemented.
- 2026-07-02: P2 window picker, hover highlight overlay, selected-window PNG capture, and Esc cancel implemented.
- 2026-07-02: P3 internal viewer and first Explorer-style browser slice implemented.
- 2026-07-03: P3 usability polish completed for the mock-aligned floating button, drag repositioning, centered radial mode menu, and wrapping icon file views.
- 2026-07-03: Timer UX corrected to delay selection only, countdown feedback added, and window capture target filtering/smoke validation completed.

## Current Work

- Timer/window capture fix slice is complete. Next implementation slice is P4 hardening and package checks.

## Next Actions

- Run DPI/multi-monitor and repeated capture validation.
- Choose the first packaging path and record release notes.
- Keep Windows.Graphics.Capture and thumbnail cancellation/perf work as hardening follow-ups unless a P4 blocker appears.

## Blockers

- None currently recorded.

## Evidence

- Design doc: `notes/brainstorm/20260702_mini_capture_design.md`
- Planning mockup: `notes/brainstorm/mini-capture-interactive.html`
- App workflow docs: `doc/app-dev/`
- P0 build: `dotnet build MiniCapture.slnx`
- P0 smoke: UI Automation found capture button, four mode buttons, timer placeholder state, and clean process exit.
- P1 build: `dotnet build`
- P1 smoke: UI Automation full capture saved PNG, timer capture saved PNG, result action buttons were found, and process exited cleanly.
- P1 region engine: `ScreenCaptureService.CaptureRegion(...)` saved a non-empty PNG.
- P2 build: `dotnet build`
- P2 smoke: UI Automation selected `창 지정`, synthetic click on a target `cmd` window saved a non-empty PNG, and `Esc` cancel returned to the floating button.
- P3 build: `dotnet build MiniCapture.slnx`
- P3 smoke: UI Automation ran full capture, opened the internal viewer from `보기`, exercised Fit/100%/zoom in/out, previous/next, file list selection, and details/small/medium/large view mode switches.
- P3 polish build: `dotnet build MiniCapture.slnx`
- P3 polish smoke: UI Automation measured the four radial mode buttons at roughly 91-94px from the main button center; file icon view wraps vertically with horizontal scrolling disabled.
- Timer/window fix build: `dotnet build MiniCapture.slnx`
- Timer/window fix smoke: UI Automation verified timer choices `3/5/7/10`, confirmed opening timer choices did not save a capture, observed countdown automation text `3초 후 캡처`, saved delayed window capture `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_024215_057.png`, and verified app exit with `CloseMainWindow`.
- Timer/window fix whitespace check: `git diff --check` passed with CRLF conversion warnings only.
