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

## P2 Continuation

### Finish Line

P2 complete: window mode now opens a topmost picker overlay, highlights the target window under the cursor, saves the clicked window as a PNG, and supports `Esc` cancel back to the floating button.

### Changes

- Added Win32 interop for top-level window enumeration, DWM extended frame bounds, titles/classes, and capture exclusion hints.
- Added `WindowPickerService` to find the topmost visible non-app window under the cursor.
- Added `WindowPickerOverlay` with target highlight and label.
- Connected `창 지정` mode to picker selection and selected-window PNG saving.
- Applied `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` best-effort hints to the floating button and capture overlays.

### Verification

- `dotnet build`: passed with 0 warnings and 0 errors.
- UI Automation window capture smoke: opened `창 지정`, clicked a target `cmd` window, saved a non-empty PNG, and exited cleanly.
- Escape cancel smoke: opened window picker, sent `Esc`, confirmed the app stayed alive and the floating capture button returned.

### Decisions

- P2 uses DWM visible bounds plus GDI `CopyFromScreen` for selected-window PNG capture.
- Windows.Graphics.Capture remains a future hardening path if DWM/GDI capture fails for specific accelerated or protected windows.
- DPI and multi-monitor coordinate precision remain P4 validation concerns.

### Follow-ups

- P3: internal image viewer and Windows Explorer-style mini browser.
- P4: repeated manual tests across DPI/multi-monitor setups and protected/accelerated windows.

## Handoff

P3 is complete. Next step: implement P4 hardening and package checks without adding OCR, upload, scrolling capture, video/GIF, heavy editing, or a general-purpose file manager.

## P3 Continuation

### Finish Line

P3 complete: the `보기` result-panel action now opens an internal WPF viewer with image zoom controls, previous/next navigation, and a first Explorer-style capture folder browser.

### Changes

- Added `ViewerWindow` with Fit, 100%, zoom in/out, previous/next, and Ctrl+wheel zoom.
- Added capture folder indexing for PNG/JPG/JPEG files under `Pictures\MiniCapture`.
- Added Explorer-style folder tree, address display, details file list, and small/medium/large icon view switches.
- Added lightweight thumbnail loading for icon views.
- Rewired the result panel `보기` button from the default external image app to the internal viewer.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation smoke: ran full capture, opened the internal viewer from `보기`, exercised Fit/100%/zoom in/out, previous/next navigation, file list selection, and details/small/medium/large view mode switches.
- App shutdown smoke: the same UIA run closed the main app process normally after viewer/browser checks.

### Decisions

- P3 keeps browsing scoped to the capture root instead of becoming a general-purpose file manager.
- Details view is the default for responsiveness; icon views load thumbnails on demand.
- Delete, rename, copy, move, OCR, upload, scrolling capture, video/GIF, heavy editing, and packaging remain out of this slice.

### Follow-ups

- P4: DPI/multi-monitor and repeated capture validation.
- P4: packaging choice and release notes.
- FOLLOW_UP: thumbnail cancellation/cache hardening and explicit large-folder perf sweep.
