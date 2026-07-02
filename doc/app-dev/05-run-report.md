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

## P1 Continuation

### Finish Line

P1 complete: drag region, full screen, and timer capture paths now save PNG files automatically, then show a result panel with file, folder, and view actions.

### Changes

- Added GDI-based `ScreenCaptureService` for virtual-screen and region PNG capture.
- Added dated capture folders under `Pictures\MiniCapture\yyyy\MM\dd`.
- Added `RegionCaptureOverlay` for drag selection with `Esc` cancel.
- Connected full screen and 3-second timer capture from the floating menu.
- Added result panel actions for file open, folder select, and view/open.
- Kept window picker as a P2 placeholder.

### Verification

- `dotnet build`: passed with 0 warnings and 0 errors.
- UI Automation full capture smoke: saved a non-empty PNG and exited cleanly.
- UI Automation timer smoke: saved a non-empty PNG and exited cleanly.
- Region engine smoke: `ScreenCaptureService.CaptureRegion(...)` saved a non-empty PNG.
- Result panel smoke: file, folder, and view buttons were found after capture.

### Decisions

- The first capture engine uses Windows GDI `CopyFromScreen` for P1. Windows.Graphics.Capture remains the planned path for P2 window capture.
- The `보기` action opens the saved PNG with the default shell viewer until the internal P3 viewer exists.
- Overlay/floating-button exclusion from capture remains a P2 hardening item.

### Follow-ups

- P2: HWND picking, highlight overlay, selected-window capture, and overlay exclusion.
- P3: internal viewer and Windows Explorer-style mini browser.
- DPI/multi-monitor precision needs a focused P4 check after P2.

## Handoff

Next step: implement P2 window picker and overlay exclusion without adding OCR, upload, scrolling capture, video/GIF, heavy editing, or the P3 viewer/browser.
