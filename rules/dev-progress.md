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
- 2026-07-03: Window picker target selection fixed so screen-covering helper windows no longer win over real window candidates.
- 2026-07-03: DPI coordinate handling added for capture overlays with Per-Monitor DPI awareness and physical-pixel/DIP conversion.
- 2026-07-03: Close-to-tray behavior added so closing the floating window hides it while keeping the app alive in the system tray.
- 2026-07-03: Viewer/browser V1 reinforcement completed with richer capture-root tree, simple annotation tools, PNG save, open, path-copy, and image-copy actions.
- 2026-07-04: Region/window selection no longer uses a virtual-screen-sized overlay; global input hooks drive a small hint or target-sized highlight window.

## Current Work

- Overlay-free region/window selection slice is complete. Next implementation slice is P4 hardening and package checks.

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
- Window picker full-screen-candidate regression: probe showed visible full-screen helper windows (`PicPick`/`GazeScroll`) ahead of real candidates; after the fix, window capture saved `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_042248_813.png` at `1250x753` instead of virtual-screen size.
- DPI coordinate fix: process DPI awareness probe reports `DpiAwareness=2`; `dotnet build MiniCapture.slnx` passes with 0 warnings and 0 errors; `git diff --check` passes with CRLF conversion warnings only.
- Tray behavior smoke: `CloseMainWindow` returned true, the main window hid, and the `MiniCapture` process remained alive.
- Viewer/browser reinforcement build: `dotnet build MiniCapture.slnx` passed with 0 warnings and 0 errors.
- Viewer/browser reinforcement UIA smoke: created `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_070610_158.png`, opened internal viewer, verified richer tree labels, selected color, used pan/rectangle/ellipse/mosaic/text tools, saved PNG edits, copied file path and image to clipboard, invoked open, and confirmed close-to-tray kept the process alive.
- Viewer/browser reinforcement whitespace check: `git diff --check` passed with CRLF conversion warnings only.
- Overlay-free selection build: `dotnet build MiniCapture.slnx` passed with 0 warnings and 0 errors.
- Overlay-free selection smoke: region selection showed a small `446x58` hint and saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_033557_384.png`; window selection showed target-sized highlight and saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_033559_480.png`.
- Overlay-free selection whitespace check: `git diff --check` passed with CRLF conversion warnings only.
