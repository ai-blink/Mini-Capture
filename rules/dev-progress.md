# Dev Progress

## Current Status

- 2026-07-02: Brainstorming design approved and committed in `03d01aa`.
- 2026-07-02: App-dev workflow initialized from the approved design.
- 2026-07-02: P0 WPF skeleton, floating capture button, four-mode menu, placeholder mode selection, and shutdown flow implemented.
- 2026-07-02: P1 basic drag/full/timer capture, dated PNG auto-save, and result panel actions implemented.

## Current Work

- P1 is complete. Next implementation slice is P2 window picker and overlay exclusion.

## Next Actions

- Implement cursor HWND detection and window highlight overlay.
- Capture selected window or record a fallback path for unsupported windows.
- Apply overlay/floating UI capture exclusion where Windows support allows it.

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
