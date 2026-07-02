# Dev Progress

## Current Status

- 2026-07-02: Brainstorming design approved and committed in `03d01aa`.
- 2026-07-02: App-dev workflow initialized from the approved design.
- 2026-07-02: P0 WPF skeleton, floating capture button, four-mode menu, placeholder mode selection, and shutdown flow implemented.
- 2026-07-02: P1 basic drag/full/timer capture, dated PNG auto-save, and result panel actions implemented.
- 2026-07-02: P2 window picker, hover highlight overlay, selected-window PNG capture, and Esc cancel implemented.

## Current Work

- P2 is complete. Next implementation slice is P3 viewer and Explorer-style browser.

## Next Actions

- Implement mini viewer with zoom, fit, 100%, and previous/next.
- Add capture folder index and Windows Explorer-style left tree/file pane.
- Keep thumbnails lazy and avoid general-purpose file manager expansion.

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
