# 05 Run Report

## Finish Line

P0 complete: the repository now has a C# WPF skeleton with a transparent always-on-top floating capture button, a four-mode menu, placeholder mode selection state, and a clean shutdown path.

## Changes

- Added `MiniCapture.slnx` and `src/MiniCapture/` WPF project targeting `net8.0-windows`.
- Replaced the default main window with a 76px floating capture entry window.
- Added four mode options: drag, window, full screen, and timer.
- Added placeholder mode state feedback through the button label, tooltip/accessibility state, and status popup.
- Added a right-click exit command and normal window close handling.
- Added a minimal settings path helper for the app data directory.

## Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `dotnet run --project src\MiniCapture\MiniCapture.csproj`: app stayed running after WPF startup; initial `windir` environment issue was fixed.
- UI Automation smoke: found the capture button, found 4 mode buttons, selected timer mode, confirmed selected state, and exited cleanly via `CloseMainWindow`.

## Decisions

- P0 does not include the capture engine, PNG save, result panel, viewer, or file browser.
- `windir` is restored process-locally at startup when missing because WPF FontCache fails before window construction without it in this environment.
- Overlay capture exclusion P/Invoke is deferred until P1/P2 when capture surfaces exist.

## Follow-ups

- P1: connect drag/full/timer capture, PNG auto-save, and result panel actions.
- P1/P2: apply overlay/floating UI capture exclusion where Windows support allows it.
- Workflow follow-up: the plan validator module was unavailable in this local environment.

## Handoff

Next step: implement P1 capture/save without adding OCR, upload, scrolling capture, video/GIF, heavy editing, or the P3 viewer/browser.
