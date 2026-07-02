# Dev Progress

## Current Status

- 2026-07-02: Brainstorming design approved and committed in `03d01aa`.
- 2026-07-02: App-dev workflow initialized from the approved design.
- 2026-07-02: P0 WPF skeleton, floating capture button, four-mode menu, placeholder mode selection, and shutdown flow implemented.

## Current Work

- P0 is complete. Next implementation slice is P1 capture/save.

## Next Actions

- Add drag/full/timer capture paths.
- Add PNG save service and result panel actions.
- Revisit overlay capture exclusion with Win32 P/Invoke when capture is connected.

## Blockers

- None currently recorded.

## Evidence

- Design doc: `notes/brainstorm/20260702_mini_capture_design.md`
- Planning mockup: `notes/brainstorm/mini-capture-interactive.html`
- App workflow docs: `doc/app-dev/`
- P0 build: `dotnet build MiniCapture.slnx`
- P0 smoke: UI Automation found capture button, four mode buttons, timer placeholder state, and clean process exit.
