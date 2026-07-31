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

## P3 Usability Polish Continuation

### Finish Line

P3 polish complete: the floating capture button now follows the approved mockup more closely, supports drag repositioning without breaking click-to-menu behavior, and the four mode buttons sit in a compact centered radial cluster above the main button.

### Changes

- Moved the default floating button placement to the left side of the work area.
- Restyled the floating button to use the mockup-sized teal circular button with a simple white inner frame.
- Replaced the stacked text menu with four circular mode buttons arranged around the main button center.
- Split click and drag gestures so dragging moves the floating button while a click opens exactly one mode menu.
- Repositioned status/result popups to open away from the nearest screen edge.
- Disabled horizontal scrolling in icon file views so image tiles wrap downward.

### Verification

- `git diff --check`: passed with CRLF conversion warnings only.
- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation radial-menu check: four mode buttons measured about 91-94px from the main button center.
- Manual visual check: the mode buttons are compact and evenly spaced; closer spacing would overlap the 44px circular buttons.

### Decisions

- The mode menu keeps the compact radial cluster as the final P3 polish shape.
- File browser polish remains scoped to capture image browsing; no delete, rename, copy, move, OCR, upload, scrolling capture, video/GIF, heavy editing, or packaging work was added.

## Handoff

P3 implementation and usability polish are complete. Next step: implement P4 hardening and package checks without expanding V1 scope.

## Timer And Window Capture Fix

### Finish Line

Timer UX and window capture fix complete: the timer button now opens delay choices only, the selected delay applies to drag/window/full-screen captures, countdown feedback is visible before capture, and selected-window capture saves a PNG again.

### Changes

- Removed timer as an executable capture mode.
- Added timer delay choices for 3, 5, 7, and 10 seconds.
- Applied the selected delay before drag, window, and full-screen capture flows.
- Added red countdown text and per-second system beep feedback during countdown.
- Hardened window picking by filtering minimized, cloaked, and tiny candidate windows.
- Switched the window picker click handler to preview mouse input so the overlay accepts the selection before child hit-testing can interfere.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- Timer UI smoke: UI Automation verified `Timer3Button`, `Timer5Button`, `Timer7Button`, and `Timer10Button`; opening timer choices did not save a capture.
- Delayed window capture smoke: selected 3 seconds, saw countdown automation text `3초 후 캡처`, clicked a `cmd` target window, and saved `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_024215_057.png` at 5,945,783 bytes.
- Window capture smoke without delay saved `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_024134_636.png` at 5,898,490 bytes.
- App shutdown smoke: `CloseMainWindow` exited the app normally.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Timer remains a capture setting, not a fourth executable capture path.
- The first fix keeps the existing DWM-bounds plus GDI capture engine and tightens target selection instead of introducing Windows.Graphics.Capture in this slice.
- P4 packaging, broader DPI/multi-monitor sweeps, OCR, upload/cloud sharing, scrolling capture, video/GIF, heavy editing, and file-manager operations remain out of scope.

## Window Picker Full-Screen Candidate Fix

### Finish Line

Window picker regression fixed: hovering a normal window no longer selects screen-covering helper windows as the capture target.

### Changes

- Deferred virtual-screen-sized candidates while scanning window candidates under the cursor.
- Ignored known full-screen helper targets observed in the reproduction path.
- Preserved fallback behavior for truly full-screen/maximized captures when no smaller real candidate is available.

### Verification

- Reproduction probe showed visible full-screen helper windows such as `PicPick`/`GazeScroll` ahead of normal window candidates.
- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- Window capture smoke saved `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_042248_813.png` at `1250x753`, confirming the selected target was not the `3840x2160` virtual screen.
- `git diff --check`: passed with CRLF conversion warnings only.

## DPI Coordinate Fix

### Finish Line

Overlay coordinates now account for DPI scaling: WPF overlay placement/highlight uses DIP coordinates converted from Win32 physical pixels, while capture bounds remain physical pixels for `CopyFromScreen`.

### Changes

- Added a custom startup entry point that enables Per-Monitor DPI awareness before WPF initializes.
- Converted window picker overlay virtual-screen bounds and highlight rectangles from physical pixels to WPF DIP coordinates.
- Converted region overlay window bounds from physical pixels to DIP coordinates, then converts selected drag rectangles back to physical pixels for capture.
- Adjusted picker candidate choice to prefer smaller real windows after full-screen helper candidates are deferred.

### Verification

- DPI awareness probe reports `DpiAwareness=2`.
- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Automated mouse-coordinate smoke remains partially environment-sensitive because the PowerShell driver can itself be DPI virtualized; final visual/manual confirmation should be done in the running app on the target scaled display.

## Close To Tray Fix

### Finish Line

Closing the floating capture window no longer exits Mini Capture; the process stays alive through a system tray icon until the user explicitly chooses exit.

### Changes

- Switched application shutdown mode to explicit shutdown.
- Added a tray icon with `열기` and `종료` commands.
- Added double-click tray restore behavior.
- Changed normal window closing to hide the floating window to tray.
- Kept the existing in-app `종료` command as a real app exit path.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- Close-to-tray smoke: `CloseMainWindow` returned true, the main window hid, and the process remained alive.

## Viewer Browser V1 Reinforcement

### Finish Line

The internal viewer and capture-root browser are strengthened within V1 scope: the tree feels closer to Windows Explorer, simple image annotations can be applied, edited PNGs can be saved, and viewer/result actions cover open plus clipboard workflows.

### Changes

- Expanded the capture-root tree with Explorer-like groups, icons, quick links for today/recent captures, and a `내 PC > 사진 > MiniCapture` path while keeping selectable paths inside `Pictures\MiniCapture`.
- Added viewer tools for hand/pan, rectangle, ellipse, mosaic selection, color choice, and text placement.
- Added in-memory bitmap editing that commits simple annotations directly into the displayed bitmap, without adding a layer system or general editor architecture.
- Added viewer actions for save, open, copy file path, and copy image.
- Added result-panel actions for copy file path and copy image, and renamed the file action to `열기`.
- Added automation IDs for the preview image/edit surface path used by UI smoke checks.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation smoke created `C:\Users\user\Pictures\MiniCapture\2026\07\03\20260703_070610_158.png`, opened the internal viewer, verified richer tree labels, selected a color, used pan/rectangle/ellipse/mosaic/text tools, saved the edited PNG, copied the file path, copied the image to the clipboard, invoked open, and confirmed closing the main window kept the process alive in tray.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Editing remains intentionally raster-only and immediate; no layer list, object selection, delete/rename/copy/move file operations, OCR, upload/cloud sharing, scrolling capture, video/GIF, or heavy editing was added.
- The tree can show Explorer-like grouping nodes outside the capture root, but selectable file paths remain under the capture root.

### Follow-ups

- P4 still owns DPI/multi-monitor sweeps, repeated capture loops, packaging choice, and release notes.
- FOLLOW_UP: if annotation undo or save-as is desired later, treat it as a separate V2 editor slice rather than extending this V1 reinforcement.

## Overlay-Free Region And Window Selection

### Finish Line

Region and window selection no longer depend on a virtual-screen-sized topmost overlay window, so external capture targets are not visually covered by Mini Capture during selection.

### Changes

- Added a low-level global input hook for selection-mode mouse and Escape handling.
- Changed region selection to show only a small hint before drag, then a selection-sized rectangle while dragging.
- Changed window selection to show only a small hint or target-sized highlight window, instead of a full-screen transparent picker.
- Kept the existing region/window capture output paths and `Esc` cancel behavior.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation/global-mouse smoke confirmed region selection showed a small `446x58` hint and saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_033557_384.png`.
- UI Automation/global-mouse smoke confirmed window selection used a target-sized highlight and saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_033559_480.png`.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- The selection windows remain best-effort excluded from capture, but they are no longer virtual-screen-sized.
- The global hook consumes the selection click/drag so choosing a capture target does not also click or drag inside the target app.

## TabPaint-Inspired Editor Workflow

### Finish Line

The internal viewer keeps its V1 raster-only editor but adopts selected TabPaint workflow ideas: faster annotation tools, undo/redo, stroke sizing, rotation, and keyboard shortcuts without becoming a full paint app.

### Changes

- Added pen and arrow tools to the existing viewer toolbar.
- Added stroke thickness control shared by rectangle, ellipse, pen, arrow, and text/mosaic sizing where applicable.
- Added in-memory undo/redo snapshots capped to a small stack.
- Added rotate-left and rotate-right actions.
- Added dirty title/current-file indicator and kept save writing the edited PNG back to the active capture file.
- Added keyboard shortcuts for save, undo, redo, rotate-left/right, plus temporary space-to-pan behavior while preserving text input focus.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation smoke saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_042336_550.png`, opened the internal viewer, verified new toolbar controls, exercised rectangle, ellipse, mosaic, text, pen, arrow, stroke slider, rotate, undo/redo, save, file-path clipboard, and image clipboard.
- `git diff --check`: passed with CRLF conversion warnings only.
- Docs secret scan: `SECRET_SCAN: PASS`.

### Decisions

- TabPaint was used only as a workflow reference. Multi-tab editing, AI/OCR tools, layer/object systems, cloud/upload sharing, delete/rename/copy/move file manager actions, scrolling capture, video/GIF, and heavy editing remain out of V1 scope.
- Editing remains immediate raster compositing inside `ViewerWindow`; no new editor architecture was introduced.

### Follow-ups

- FOLLOW_UP: decide later whether V2 needs save-as, crop, blur, or richer editor history.
- P4 still owns packaging and release checks.

## TabPaint-Inspired Viewer Toolbar UI

### Finish Line

The viewer toolbar is reorganized so editing tools are easier to scan and use, taking TabPaint's dense top toolbar as visual reference while keeping Mini Capture's single-image, capture-root-scoped V1 behavior.

### Changes

- Reduced the toolbar from three text-heavy rows to two compact rows.
- Converted primary viewer actions to icon buttons with accessible names and tooltips.
- Grouped save/open/copy, undo/redo/rotate, navigation, zoom, and refresh controls with visual dividers.
- Converted editor tools to compact glyph buttons.
- Replaced the color combo box with direct color swatches and selected-state feedback.
- Moved stroke thickness and text input inline with the editor tools.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation smoke found 26 toolbar controls, selected the blue color swatch, changed stroke thickness, exercised all edit tool buttons, and verified file-path/image clipboard actions.
- Visual screenshot check: `%TEMP%\mini_capture_toolbar_after_tabpaint.png` showed the compact two-row toolbar with no obvious text overlap in the default viewer window.
- `git diff --check`: passed with CRLF conversion warnings only.
- Docs secret scan: `SECRET_SCAN: PASS`.

### Decisions

- This slice changes toolbar presentation only. It does not add multi-tabs, delete/rename/copy/move file manager operations, AI/OCR, cloud sharing, scrolling capture, video/GIF, or a layer-based editor.

## Dark Screenshot Markup Shell Viewer Redesign

### Finish Line

ViewerWindow now follows the Dark Screenshot Markup Shell direction: a dark screenshot-first app shell with clear File/View/Markup/Properties groups, markup tools as the visual center, capture-root library browsing, a canvas-first workspace, and a stronger status/zoom bar.

### Changes

- Rebuilt `ViewerWindow.xaml` around a dark title strip, two-row command ribbon, left Capture Library panes, dotted dark canvas workspace, and bottom metadata/status bar.
- Grouped file actions as visible Save, Open, Folder, Copy Image, and Copy Path commands.
- Grouped view actions as previous/next, fit, 100%, zoom, and refresh controls.
- Promoted markup tools into a stronger row with pan, select, pen, arrow, rectangle, ellipse, text, mosaic, undo/redo, and rotate controls.
- Kept existing raster editing logic and added only minimal code-behind for the select tool, folder-open command, dark selected-state colors, and status metadata badges.
- Preserved capture-root-scoped browsing; no delete, rename, copy, move, upload, OCR, layer, project, scrolling capture, or video/GIF behavior was added.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- UI Automation smoke opened Mini Capture, made a full-screen capture, opened the internal viewer, resized it to 980x580 DIP, found save/open/copy-image/copy-path actions and pan/select/pen/arrow/rectangle/ellipse/text/mosaic/stroke controls, saved the active PNG, copied the file path, copied image data, and confirmed close-to-tray still kept the process alive.
- Visual check: `PrintWindow` screenshot saved at `%TEMP%\mini_capture_dark_shell_printwindow_980x580.png`; toolbar groups, markup controls, properties, status metadata, and zoom badge did not overlap at the target size.

### Decisions

- The Stitch `mini_capture_hybrid_modern_variant` was translated into WPF structure rather than copied from HTML/CSS.
- `mini_capture_pro_dark_variant` and `mini_capture_dark_ribbon_variant` informed command grouping only; broad editor/file-manager features were excluded.
- The new select tool is intentionally a neutral viewing/edit-safe mode, not a layer/object selection system.

### Follow-ups

- P4 still owns DPI/multi-monitor sweeps, repeated capture loops, packaging choice, and release notes.
- FOLLOW_UP: if the Capture Library tree should visually remove the existing `내 PC > 사진` grouping, treat it as a separate navigation-copy polish slice because selectable paths are already constrained to the capture root.

## P4 Hardening And Package

### Finish Line

P4 complete: Mini Capture has a verified build baseline, DPI/capture smoke evidence, a selected first packaging path, a launchable local publish folder, and recorded release-readiness follow-ups.

### Changes

- Added `artifacts/` to `.gitignore` so generated publish outputs stay out of source control.
- Chose a framework-dependent `win-x64` folder publish as the first V1 package baseline.
- Published the app to `artifacts\publish\MiniCapture-win-x64-framework-dependent`.
- Updated live progress, roadmap, context, and execution-task docs with P4 evidence and known gaps.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors after stopping a stale running `MiniCapture` process that had locked the Debug output.
- `git diff --check`: passed before and after the P4 doc/package updates; the final run emitted CRLF conversion warnings only.
- DPI probe: launched app reported `APP_DPI_AWARENESS=2`; the local environment exposed one active display, and the app saw virtual bounds `0,0,3840,2160`.
- Repeated capture loop: UI Automation created three consecutive full-screen PNGs at `3840x2160`: `20260704_171554_088.png`, `20260704_171555_214.png`, and `20260704_171556_257.png`.
- Drag capture smoke: the drag UI path saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_172455_279.png` at `766x505`.
- Window capture smoke: the window UI picker saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_172602_252.png` at `977x569`.
- Timer capture smoke: timer delay plus full-screen capture saved `C:\Users\user\Pictures\MiniCapture\2026\07\04\20260704_172303_357.png` at `3840x2160` and 6,172,894 bytes.
- Viewer regression smoke: after timer capture, the internal viewer exposed save, open, copy image, copy path, pen, and stroke controls.
- Close-to-tray smoke: `CloseMainWindow` returned true and the process remained alive until the smoke script explicitly cleaned it up.
- Packaging check: `dotnet publish src\MiniCapture\MiniCapture.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish\MiniCapture-win-x64-framework-dependent` passed, and the published `MiniCapture.exe` launched with a visible `Mini Capture` main window.

### Decisions

- V1 package baseline is a framework-dependent folder publish. It is simple, small, and matches the current local-build maturity; it requires the .NET 8 Windows Desktop Runtime on target machines.
- No installer, self-contained single-file package, auto-update, code signing, OCR, upload/cloud sharing, scrolling capture, video/GIF, heavy editing, or general file-manager behavior was added in P4.
- Current local hardware has one active display, so true cross-monitor manual validation remains a follow-up rather than a completed P4 claim.

### Release Notes

- Mini Capture now has verified drag, window, full-screen, and timer capture smoke coverage in the local test environment.
- The internal viewer/browser/editor regression smoke confirms the Dark Screenshot Markup Shell still exposes core save/open/copy and markup controls.
- A first launchable package can be produced with the documented `dotnet publish` command under `artifacts\publish\MiniCapture-win-x64-framework-dependent`.

### Follow-ups

- FOLLOW_UP: run a true multi-monitor hardware pass on a machine with multiple displays and mixed DPI if available.
- FOLLOW_UP: decide whether V1 distribution should stay framework-dependent or move to a self-contained/installer package.
- FOLLOW_UP: keep Windows.Graphics.Capture, protected/accelerated-window coverage, thumbnail cancellation, and large-folder perf sweeps as later hardening unless a release blocker appears.

## V1 Viewer Performance And File Entry

### Finish Line

Viewer performance and Windows image entry cleanup complete: ViewerWindow avoids the main UI-thread bottlenecks found in folder indexing and thumbnails, Mini Capture accepts PNG/JPG/JPEG file paths on startup, and Windows extension association is scoped as an installer/registry follow-up rather than a direct V1 system mutation.

### Changes

- Changed latest-image lookup from full recursive list sort to a single-pass scan.
- Reused the built capture-folder tree instead of reading the same directory tree twice for quick access and `내 PC > 사진`.
- Changed ViewerWindow refresh/folder loading to run index work on a background task and populate the file list in UI batches.
- Changed icon-view thumbnails from synchronous binding-converter decoding to background thumbnail properties with limited concurrency.
- Removed the now-unused thumbnail converter.
- Added startup argument handling so `MiniCapture.exe <image-path>` opens ViewerWindow with a PNG/JPG/JPEG path while keeping the capture library browser scoped to `Pictures\MiniCapture`.
- Preserved the left-click radial menu as four capture controls only and kept image viewer entry in the capture button context menu.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- Large-folder/file-argument smoke: launched `MiniCapture.exe <png path>` against a temporary 420-file folder under `Pictures\MiniCapture`; ViewerWindow became available in 1911ms, reported `폴더 로딩 완료: 420개, 131ms`, invoked medium icon view in 42ms and details view in 864ms, then removed the temporary `_perf_smoke_*` folder after confirming the path stayed under the capture root.
- Capture/viewer regression smoke: UI Automation found the four radial capture buttons, full capture saved `C:\Users\user\Pictures\MiniCapture\2026\07\05\20260705_021751_249.png` at 1,245,184 bytes, result-panel `보기` opened ViewerWindow, save/open/copy-image/copy-path/pen/stroke controls were available, and `CloseMainWindow` returned true while the process stayed alive.
- Context-menu placement check: `MainWindow.xaml` contains `OpenImageViewerMenuItem` only inside `CaptureButton.ContextMenu`; direct UIA mouse-coordinate right-click was unreliable in the scaled desktop harness, so this check was recorded as static placement plus shared handler-path verification.

### Decisions

- V1 should not directly write PNG/JPG/JPEG registry associations from the app. Microsoft documents file associations as Shell/default-app behavior that controls double-click/open behavior, and recommends proper application registration practices such as versioned ProgIDs and quoted command arguments. Treat this as an installer/default-app packaging slice, not a runtime viewer-performance slice.
- Startup file arguments are enough code support for the next installer/Open With step: a future command can pass the selected image path as the first argument.
- File arguments may point outside the capture root for preview, but the browser remains scoped to `Pictures\MiniCapture` so V1 does not become a general file manager.

References:

- https://learn.microsoft.com/en-us/windows/win32/shell/fa-how-work
- https://learn.microsoft.com/en-us/windows/win32/shell/fa-best-practices

### Follow-ups

- FOLLOW_UP: add installer/registry default-app registration for PNG/JPG/JPEG only if V1 distribution requires Windows Settings/Open With integration.
- FOLLOW_UP: decide framework-dependent vs self-contained/installer packaging together with extension association.
- FOLLOW_UP: add thumbnail cancellation/cache eviction only if a concrete release-blocking large-library case appears.

## Extension Settings V1 Reinforcement

### Finish Line

SettingsWindow extension association UI now has a V1-complete image extension settings surface: category-based current status checks, separate request candidate selection, Windows default-app guidance, executable path copy support, and full association status refresh on a 10-second timer or settings-window activation.

### Changes

- Replaced the simple PNG/JPG/JPEG status list with category groups for 기본 이미지 and 추가 이미지.
- Added displayed image extensions `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp`, `.tif`, and `.tiff`.
- Added disabled current-association checkboxes separate from editable request-candidate checkboxes.
- Added 전체 선택, 선택 안 함, 새로고침, Windows 기본 앱 열기, and 실행 파일 경로 복사 controls.
- Added read-only registry status detection for per-extension UserChoice/default ProgID/open command references to Mini Capture.
- Added 10-second `DispatcherTimer` refresh and `Activated` refresh without registry writes or system default-app mutation.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- UI Automation smoke launched `MiniCapture.exe --settings` and verified category labels, all eight image extensions, separate `현재 연결` and `요청 선택` checks, select/clear/refresh/default-app/copy-path controls, executable path text, and status text.
- Timer smoke observed status changing from `설정 창 열림: 전체 확장자 연결 상태를 갱신했습니다. 03:16:25` to `10초 자동 갱신: 전체 확장자 연결 상태를 갱신했습니다. 03:16:35`.
- Regression smoke verified the left-click radial menu still exposes `ModeDragButton`, `ModeWindowButton`, `ModeFullScreenButton`, and `ModeTimerButton`.
- Close-to-tray smoke verified `CloseMainWindow=True` and the process remained alive until the smoke script cleaned it up.

### Decisions

- V1 reads association state but does not write registry keys or force Windows default-app choices.
- The request checkboxes are only candidate selection for the user-guided Windows Default Apps/Open With flow.
- Default-app registration, versioned ProgIDs, and installer/self-contained packaging remain a future packaging slice.

### Follow-ups

- FOLLOW_UP: implement proper installer/default-app registration only if V1 distribution requires Mini Capture to appear directly in Windows default-app choices.
- FOLLOW_UP: run true multi-monitor hardware validation separately when hardware is available.

## Editable Viewer Markup

### Finish Line

Viewer editor annotations are editable before save: new rectangle, ellipse, pen, arrow, and text markup remain selectable on the image surface, can be moved/resized/deleted, and text uses a regular WPF text box editing flow.

### Changes

- Added a lightweight selectable annotation model inside `ViewerWindow`.
- Changed new rectangle, ellipse, pen, arrow, and text tools to create overlay annotations instead of immediately burning them into the bitmap.
- Added selection frame, resize handle, delete action, and `Delete` key handling for selected annotations.
- Changed save and copy-image actions to export a flattened bitmap composed from the base image plus pending annotations.
- Kept mosaic and rotation as immediate bitmap operations; they first flatten pending annotations to avoid mixed edit states.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- UI Automation launched `MiniCapture.exe <png path>` and verified save, copy image, select tool, text tool, delete selected annotation, text input, and stroke controls.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- This is not a layer editor or project format. Pending annotations are editable only during the current viewer session before save/reload.
- Saved and copied output remains a flattened image, preserving the simple PNG-oriented Mini Capture V1 behavior.

## Capture Settings Persistence

### Finish Line

Settings now cover persistent capture hotkeys for window, region, and full-screen capture; persistent timer delay; and persistent transparent quick-button visibility and moved position.

### Changes

- Added `settings.json` storage under the existing Mini Capture local app data directory.
- Added a Capture Settings section to `SettingsWindow` with hotkey fields, timer seconds, quick-button visibility, save, and reset controls.
- Added global `RegisterHotKey` handling for window, region, and full-screen capture.
- Restored timer delay, quick-button position, and quick-button visibility on app startup.
- Saved quick-button position after drag movement and kept tray/settings access available when the quick button is hidden.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

### Follow-ups

- FOLLOW_UP: add a richer key-capture control if text-form hotkey input proves awkward in manual use.
- FOLLOW_UP: run manual hardware validation for global hotkeys and multi-monitor quick-button placement.

## Capture Hotkey Detection UI

### Finish Line

Capture hotkey settings now use a three-row list for window, region, and full-screen capture, with a per-row key detection button that accepts a shortcut pressed within 5 seconds.

### Changes

- Replaced the three free-form hotkey text boxes with a `ListBox` showing action, current shortcut, status, and key-detect button.
- Added a 5-second WPF key detection flow that records Ctrl/Alt/Shift/Win plus a non-modifier key into the selected row.
- Kept Save as the apply point, so detected shortcuts can be reviewed before being persisted and globally registered.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

## Per-Key Shortcut Combos

### Finish Line

Each shortcut slot now uses one combo box per key part instead of storing the whole shortcut combination in one combo box.

### Changes

- Changed every shortcut slot from one editable combo containing values like `Ctrl+Alt+W` to three combo boxes: modifier 1, modifier 2, and main key.
- Kept the compact one-row layout and narrow key-detect button.
- Key detection now fills the three combo boxes instead of writing one combined string into a single combo box.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

## Compact Shortcut Combo Row

### Finish Line

Shortcut settings now stay compact on one row per capture mode, with three editable combo boxes and narrow key-detect buttons.

### Changes

- Removed the large per-slot card layout.
- Changed each slot to a horizontal `ComboBox + 감지` pair.
- Kept `+` separators between shortcut slots and moved active countdown feedback to the settings status text.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

## Shortcut Combo Boxes

### Finish Line

Each shortcut slot is now a combo box with its own key-detect button, arranged as three combo boxes per capture mode row.

### Changes

- Replaced shortcut slot list boxes with editable combo boxes.
- Kept the inline layout: mode label, combo 1 + detect, plus combo 2 + detect, plus combo 3 + detect.
- Added common shortcut presets while still allowing key detection to write custom combinations into the combo box text.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

## Three Shortcut Slots Per Capture Mode

### Finish Line

Each capture mode now supports three shortcut combinations. Every shortcut combination has its own single-item shortcut list box and its own key-detect button, for nine list-box/button pairs total.

### Changes

- Expanded settings storage from one shortcut per mode to three shortcut slots per mode while keeping the old single-shortcut fields as compatibility fallbacks.
- Updated global hotkey registration to register all non-empty shortcut slots for window, region, and full-screen capture.
- Updated the capture settings UI to render three shortcut-list/key-detect pairs under each capture mode.
- Kept empty optional slots valid, while requiring at least one shortcut per capture mode and preventing duplicate shortcut combinations.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

## Inline Shortcut Slot Layout

### Finish Line

Shortcut slots are now displayed inline per capture mode, so each row reads as mode label plus shortcut 1, plus shortcut 2, plus shortcut 3.

### Changes

- Changed the capture hotkey UI from vertically stacked shortcut slots to one row per capture mode.
- Added visible `+` separators between the three shortcut slots.
- Kept one list box and one key-detect button inside each shortcut slot.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.

## Separate Capture Hotkey Lists

### Finish Line

Each capture shortcut now has its own shortcut list box and matching key-detect button: one pair for window capture, one for region capture, and one for full-screen capture.

### Changes

- Split the previous single three-row hotkey list into `WindowHotkeyList`, `RegionHotkeyList`, and `FullScreenHotkeyList`.
- Kept one key-detect button next to each shortcut list box.
- Kept the existing 5-second key detection and explicit Save behavior.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by a running `MiniCapture.exe` process locking `bin\Debug\net8.0-windows\MiniCapture.exe`; the app was not force-closed, and the alternate output build verified compilation.

## Viewer Layout Persistence

### Finish Line

Viewer layout persistence is complete: the internal image viewer restores its previous window size/position/maximized state, Explorer-style file pane view mode, and capture-library/file-list pane widths across viewer reopen and app restart.

### Changes

- Extended `settings.json` storage with viewer window bounds, window state, Explorer view mode, folder-tree width, and file-list width.
- Restored the saved viewer layout during `ViewerWindow` construction so the first shown frame uses the persisted size and pane layout.
- Saved viewer layout on close while preserving current settings written by other app surfaces.
- Guarded window restore against off-screen virtual-screen bounds and clamped saved pane widths to practical limits.
- Named the folder-tree and file-list `ColumnDefinition` entries so resized pane widths can be captured and restored.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Viewer layout state reuses the existing Mini Capture local app data `settings.json` instead of adding a second persistence file.
- This slice does not add a general file-manager layout system, registry/default-app changes, or new viewer/editor features.

## Viewer Image View State Persistence

### Finish Line

Viewer image view state persistence is complete: the internal image viewer has a smaller zoom in/out UI, and each opened image restores its previous zoom/fit mode and scroll position after reopen or app restart.

### Changes

- Added compact zoom button styling for the viewer toolbar while keeping existing automation IDs and handlers.
- Extended `settings.json` storage with recent per-image view states capped at 200 entries.
- Saved zoom, fit mode, and scroll offsets after zoom controls, Ctrl+wheel, scrollbar movement, hand-tool panning, image changes, and viewer close.
- Restored saved image view state after the image surface layout is ready, so scroll offsets apply to the current zoomed surface.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors after stale WPF build intermediates were cleared.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Image view state reuses the existing Mini Capture local app data `settings.json`; no new database or cache file was added.
- The saved state is scoped to image preview position only and does not persist editable annotations or create a broader viewer session format.

## Viewer Bottom Zoom Slider

### Finish Line

Viewer bottom zoom slider is complete: the status bar now exposes a compact zoom slider that stays synchronized with toolbar buttons, Ctrl+wheel zoom, fit restore, and per-image zoom persistence.

### Changes

- Added a bottom status-bar zoom slider with the same 10% to 800% range as the viewer zoom engine.
- Synchronized the slider from `ApplyZoom` so toolbar zoom, fit mode, restored image state, and keyboard/mouse zoom update the bottom control.
- Routed slider changes through `SetZoom`, preserving the existing per-image zoom and scroll-position save behavior.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.

## Capture UI Exclusion Toggle

### Finish Line

The floating capture UI exclusion is now configurable, and the main quick button, its child mode popups, timer/status/result popups, right-click context menu, and selection overlays all use the same setting.

### Changes

- Added `CaptureUiExcludedFromCapture` to the persisted settings, defaulting to enabled.
- Added a capture settings checkbox for excluding the quick icon, child buttons, and right-click menu from captures.
- Changed the Win32 display-affinity helper to set either `WDA_EXCLUDEFROMCAPTURE` or `WDA_NONE`.
- Applied the setting to the main floating window and to WPF `Popup`/`ContextMenu` HWNDs when they open.
- Reused the same setting for region and window picker overlay exclusion.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-build\`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.
- Static check found the setting and handlers in `MainWindow`, `SettingsWindow`, `NativeWindowApi`, `RegionCaptureOverlay`, and `WindowPickerOverlay`.
- UI Automation `--settings` smoke did not find the settings window in this harness; the launched process exposed only the main `Mini Capture` window. Treat manual settings-window validation as a follow-up if needed.

## 3D App Icon

### Finish Line

Mini Capture now has a glossy 3D app icon applied to the executable, main capture window, viewer, settings window, and tray icon.

### Changes

- Generated a strict 2x2 3D icon sprite source and selected the glossy capture-lens candidate.
- Normalized the selected candidate into `Assets/App/MiniCapture.png` and a multi-frame `Assets/App/MiniCapture.ico`.
- Set the WPF executable `ApplicationIcon`, embedded the app icon assets as resources, and loaded the same icon for the Windows Forms tray icon with a default icon fallback.
- Added WPF window icon references for the floating capture window, viewer, and settings window.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- PNG alpha validation: 256x256 RGBA, corner alpha `[0, 0, 0, 0]`, visible bbox `(23, 21, 239, 239)`.
- ICO header validation: 16, 20, 24, 32, 48, 64, 128, and 256px frames are present.
- Dark background previews at 20, 24, 32, 48, and 64px remain recognizable.

### Decisions

- Kept the generated source and split candidates under `artifacts/icon-sprite/20260708-app-icon-3d/` for traceability.
- Did not touch installer/default-app association behavior in this slice.

## Region Capture Mouse Release Recovery

### Finish Line

Region capture is back on the initial working interaction model: a full-screen WPF selection overlay captures mouse input directly, and releasing the left mouse button completes the capture when the selected area is large enough. Esc remains the explicit cancel path.

### Changes

- Replaced the small hint-window/global-hook region selection path with the initial full-screen WPF overlay path.
- Restored `Mouse.Capture`, `MouseLeftButtonDown`, `MouseMove`, and `MouseLeftButtonUp` as the region selection input contract.
- Kept DPI-aware device/DIP conversion and the existing capture UI exclusion call.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-region-initial-path\`: passed with 0 warnings and 0 errors.
- UI Automation/mouse smoke invoked region capture, confirmed the overlay covered the full virtual screen, dragged a region, released the left mouse button, and saved `C:\Users\user\Pictures\MiniCapture\2026\07\08\20260708_042410_255.png`.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Prioritized the initial proven interaction over the overlay-free global-hook path because the global-hook path could leave the hint visible and miss completion on left-button release.
- Kept the fix scoped to region overlay selection handling; capture saving, hotkeys, viewer, and app icon work were not changed.

## Frozen Timer Selection

### Finish Line

Timer-delayed region and window capture now use the timer-expiry screen state as the capture baseline: after countdown, Mini Capture snapshots the virtual screen, shows that frozen image for selection, and saves the selected region/window by cropping the snapshot.

### Changes

- Added `ScreenCaptureSnapshot` and `ScreenCaptureService.SaveSnapshotRegion(...)` for crop-from-frozen-bitmap saving.
- Changed delayed region capture to create a virtual-screen snapshot after countdown and let the user drag on the frozen preview.
- Changed delayed window capture to capture visible top-level window metadata around the timer-expiry point, select against that stored metadata, and save by cropping the frozen bitmap.
- Kept non-delayed region/window capture on the existing live `CopyFromScreen` paths.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Timer expiry is the capture baseline for delayed region/window selection.
- Delayed window selection uses frozen bounds/title metadata instead of live HWND hit-testing after the countdown.
- A separate "select a window first, then capture that HWND after a delay" mode remains out of V1 unless manual use shows it is needed.

### Follow-ups

- FOLLOW_UP: manually validate frozen timer region/window behavior on the target hardware, especially multi-monitor and DPI-scaled setups.
- FOLLOW_UP: consider a separate preselected-window delayed capture mode only if users need delayed capture of a specific live HWND instead of crop-from-frozen-screen behavior.

## Window Picker Frontmost Selection Fix

### Finish Line

Window capture selection now prefers the frontmost selectable window under the cursor instead of choosing the smallest overlapping window behind it.

### Changes

- Replaced the live window picker area-sort path with the same Z-order candidate selection contract used by frozen timer window metadata.
- Removed the generic full-screen deferral heuristic so a real large, maximized, or full-screen foreground window can be selected first.
- Kept existing minimized, cloaked, tiny, app-owned, shell, and known helper-window filters.
- Added a no-package regression harness for the pure target-selection logic.

### Verification

- Initial `dotnet build MiniCapture.slnx` and test run against the default Debug output were blocked by the currently running `MiniCapture` process locking `bin/obj` files.
- Follow-up `dotnet build MiniCapture.slnx` against the default Debug output later passed with 0 warnings and 0 errors after the lock cleared.
- `dotnet build MiniCapture.slnx --artifacts-path artifacts\build-picker-fix -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-picker-fix -p:UseAppHost=false`: passed 4 regression cases, including large front window before smaller covered window.
- `git diff --check`: passed with CRLF conversion warnings only.

### Decisions

- Frontmost visible target wins after explicit filters; unknown full-screen helper overlays are not treated as a generic reason to skip the actual foreground window.
- Kept the fix scoped to window picking. Region selection, capture saving, viewer, settings, and packaging behavior were not changed.

### Follow-ups

- FOLLOW_UP: manually validate frontmost window picking against the specific app/window that previously would not highlight, plus a maximized-window-over-smaller-window setup.

## Default-App Candidate Registration

### Finish Line

Mini Capture registers itself as a Windows default-app candidate for PNG/JPG/JPEG without forcing the user's current defaults.

### Changes

- Added user-scoped file association registration under HKCU for `MiniCapture.Viewer`, PNG/JPG/JPEG ProgIDs, `Applications\MiniCapture.exe`, `Capabilities\FileAssociations`, `RegisteredApplications`, and Open With ProgIDs.
- Added automatic PNG/JPG/JPEG candidate registration on app startup using the current executable path.
- Simplified the Settings extension tab to one PNG/JPG/JPEG list with current default app text, per-row `변경`, `Windows 기본 앱에서 선택하기`, a `설명 보기` modal, and `선택 목록 복구` as a repair path.
- Removed the previous extra-image section and registration checkboxes because they looked editable while Windows owns the actual default-app change.
- Sent a shell association-changed notification after successful registration.

### Verification

- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-association-registration\`: passed with 0 warnings and 0 errors.
- `dotnet build MiniCapture.slnx -p:BaseOutputPath=artifacts\verify-extension-help-ux\ -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-association-registration -p:UseAppHost=false`: passed 4 regression cases.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-extension-help-ux -p:UseAppHost=false`: passed 4 regression cases.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by running `MiniCapture.exe` processes locking `bin\Debug\net8.0-windows\MiniCapture.exe`; alternate output build verified compilation without closing the user's running app.

### Decisions

- This slice registers Mini Capture as a candidate only; it does not write `UserChoice` or force default apps.
- Installer/self-contained packaging remains separate from the in-app user-scoped registration path.

## Viewer Save/Copy And Thumbnail Refresh

### Finish Line

Viewer markup can be completed inside the image viewer through visible save/copy actions, and the file browser thumbnail/metadata updates immediately after saving an edited image.

### Acceptance Checks

- Header-level viewer actions expose save, image copy, and path copy without relying on the wider ribbon being visible.
- `Ctrl+S`, `Ctrl+C`, and `Ctrl+Shift+C` work in the viewer for save, image copy, and path copy when a text box is not focused.
- Saving a marked-up image refreshes the current file entry thumbnail cache, size, modified time, selection, and sorted position.

### Scope Limit

- Changed only the viewer action surface, save-state synchronization, and capture-file thumbnail invalidation.
- Did not add new markup tools, installer behavior, default-app mutation, or broader file-manager actions.

### Review Budget

- One implementation/review loop.

### Stop Rule

- Stop after isolated build, existing regression tests, and diff hygiene pass.

### Changes

- Replaced the decorative viewer header labels with direct `저장`, `이미지 복사`, and `경로` actions.
- Added viewer shortcuts for image copy and path copy while preserving normal text-box copy behavior.
- Made `CaptureImageFile` refreshable so saved files can update their metadata and invalidate stale thumbnails.
- Updated viewer save flow to refresh or insert the saved file entry, keep selection coherent, and preserve list sorting by newest modified time.

### Verification

- `dotnet build src\MiniCapture\MiniCapture.csproj -p:OutputPath="%TEMP%\mini-capture-build-output\"`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj -p:OutputPath="%TEMP%\mini-capture-tests-output\"`: passed 4 regression cases.
- `git diff --check`: passed with CRLF conversion warnings only.
- Default `dotnet build MiniCapture.slnx` was blocked by the currently running `MiniCapture.exe` locking `bin\Debug\net8.0-windows\MiniCapture.exe`; alternate output build verified compilation without closing the user's running app.

## V0.1.0 Portable Release

### Changes

- Rewrote `README.md` in Korean with portable setup, capture/viewer usage, shortcuts, default-app guidance, privacy, build, and V1 scope.
- Set application, assembly, file, and informational versions to `0.1.0`.
- Published a self-contained, single-file `win-x64` portable executable and packaged it as `MiniCapture-v0.1.0-win-x64-portable.zip`.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors after closing the locking Debug app process.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj`: passed 4 regression cases.
- Portable `MiniCapture.exe --settings` launch: passed; process was responsive and reported product version `0.1.0`.
- ZIP SHA-256: `229220E1BC009224905A0424E767D6A3CA9937B30A4CD47C665DD445AC178DE5`.

## External Image Full-Path Browser Integration

### Finish Line

Opening a PNG/JPG/JPEG outside `Pictures\MiniCapture` keeps the viewer, address display, folder tree, file list, and previous/next context synchronized to the image's actual parent folder.

### Acceptance Checks

- External image folders are not coerced back to the capture root.
- The tree contains one expanded drive/share-root-to-target path and the target's immediate child folders.
- The external folder list includes supported images in that folder without recursively scanning the entire drive.
- The existing recursive capture-root index and latest-capture entry remain unchanged.
- Build, focused path/tree regressions, and diff hygiene pass.

### Scope Limit

- No address editing, search, delete, rename, move, Save As, or general-purpose file-manager operations.
- Existing external save behavior remains unchanged: PNG overwrites the selected PNG and JPG/JPEG saves an `_edited.png` sibling.

### Review Budget

- Two implementation/review loops.

### Stop Rule

- Stop after the isolated build, focused regression tests, diff check, and run-report/roadmap update pass.

### Changes

- Allowed validated existing folders outside the capture root to become the active viewer folder.
- Kept capture-root indexing recursive while limiting external folders to direct image files.
- Added an expanded external drive/share path chain and immediate child-folder nodes without duplicating the selected target path.
- Rebuilt the external tree when navigating to an external ancestor or child so deeper traversal stays synchronized.
- Labeled the folder pane as `전체 경로` while browsing outside the capture root.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-external-browser-final -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-external-browser-final -p:UseAppHost=false`: passed 6 cases, including external direct-folder indexing and unique full-path tree construction.
- `git diff --check`: passed with CRLF conversion warnings only.
- The isolated viewer process stayed alive with an external image argument, but this desktop harness repeatedly timed out while enumerating the viewer's descendant UI Automation tree. Address/list/tree UIA assertions remain a manual follow-up rather than claimed evidence.

### Decisions

- Full-path integration means the real filesystem drive/share hierarchy for the opened image, not Windows shell virtual folders or a general file manager.
- External folders use non-recursive image enumeration to avoid accidental drive-wide scans; selecting a child rebuilds the focused path tree for continued navigation.

### Follow-ups

- FOLLOW_UP: manually open an external PNG and JPG/JPEG and confirm the expanded path, selected node, sibling list, previous/next navigation, and save refresh on the interactive desktop.

## Viewer Single Instance And Keyboard Navigation

### Finish Line

Opening image files repeatedly reuses one Mini Capture process and viewer window, while Left/Right reliably changes the selected image within the active folder.

### Acceptance Checks

- A secondary launch forwards its command-line arguments to the primary process through a local named pipe and exits.
- A forwarded image path opens in the existing viewer window.
- Left/Right is handled before file-list controls consume the key, except while editing text.
- Isolated build, focused IPC regression, existing browser regressions, and diff hygiene pass.

### Scope Limit

- No installer, registry, association, image-ordering, or general file-browser behavior changes.

### Review Budget

- One implementation/fix loop.

### Stop Rule

- Stop after the isolated build, seven regression cases, and diff check pass.

### Changes

- Added a mutex-plus-named-pipe single-instance coordinator and routed forwarded image/settings/plain-launch requests onto the existing app dispatcher.
- Moved the viewer's keyboard handler to `PreviewKeyDown`, so the current folder's previous/next selection works even when the file list has focus.
- Added an isolated IPC forwarding regression using unique test mutex/pipe names.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-single-instance-navigation-final -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-single-instance-navigation-final -p:UseAppHost=false`: passed 7 cases, including `SingleInstance_ForwardsArgumentsToPrimary`.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: manually open several PNG/JPG/JPEG files through Windows Explorer with the currently installed app, then confirm only one `MiniCapture.exe` process remains and the existing viewer changes images with Left/Right.

## Viewer Folder-Scoped Image Loading

### Finish Line

Opening an image or selecting a folder no longer recursively loads every image beneath the capture root; the viewer loads only direct image siblings in the active folder.

### Acceptance Checks

- PNG/JPG/JPEG lists are limited to the selected folder's direct files, including folders beneath `Pictures\MiniCapture`.
- An image-path open reuses that image for the `최근 캡처` tree entry instead of triggering another root-wide latest-image scan.
- First opening the viewer with no supplied image may still perform one latest-capture search.
- Build, eight regression cases, and diff hygiene pass.

### Scope Limit

- No changes to image ordering, thumbnail rendering, capture output directories, external-folder browsing, or the existing recursive directory-tree presentation.

### Review Budget

- One implementation/review loop.

### Stop Rule

- Stop after isolated build, focused regression coverage, and diff check pass.

### Changes

- Changed image enumeration to direct-folder-only for both capture-root and external folders.
- Passed the already resolved startup image into folder-tree construction, avoiding its redundant full capture-root latest-image traversal.
- Added a capture-root regression proving nested image files are excluded from the active folder list.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-folder-lazy-loading -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-folder-lazy-loading -p:UseAppHost=false`: passed 8 cases, including capture-root and external non-recursive image listing.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: if capture directory nesting itself becomes large, replace the existing recursive directory-tree construction with expand-on-demand nodes in a separate viewer-tree slice.

## External Folder Loading Failure State

### Finish Line

An external-folder load failure no longer leaves the viewer permanently showing a loading message; the viewer reports the failure while preserving the prior image list until a new folder list is available.

### Acceptance Checks

- Folder enumeration completes before the active list and address are replaced.
- Tree-building, file enumeration, and UI batch failures change the status to `읽기 실패` with the underlying message.
- Cancellation remains silent and does not overwrite a newer navigation request.
- Build, existing regression coverage, and diff hygiene pass.

### Scope Limit

- No retry queue, timeout policy, network-share mounting logic, or folder-tree architecture changes.

### Review Budget

- One implementation/review loop.

### Stop Rule

- Stop after isolated build, regression tests, diff check, and republishing the latest executable.

### Changes

- Deferred clearing the current file list and changing the address until direct-folder enumeration succeeds.
- Added visible failure-state handling for initial index, external-tree, image-enumeration, and UI-population exceptions.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-external-folder-error-state -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-external-folder-error-state -p:UseAppHost=false`: passed 8 cases.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: manually select an inaccessible, disconnected, or transient external folder and confirm the status changes from the loading text to `폴더를 읽을 수 없습니다: …` without blanking the preceding list.

## Large Folder Virtualized View Guard

### Finish Line

Opening a large external image folder keeps the viewer responsive by preventing the non-virtualized icon WrapPanel from creating every icon and thumbnail at once.

### Acceptance Checks

- Folders with more than 300 images switch from an icon view to the existing virtualized Details view before file items are populated.
- Icon view requests remain blocked while such a folder is active, with an explanatory status message.
- Folders at or below the threshold preserve all existing icon view choices.
- Build, nine regression cases, and diff hygiene pass.

### Scope Limit

- No custom virtualizing WrapPanel, paging UI, thumbnail cache redesign, or changes to ordinary-size folder views.

### Review Budget

- One implementation/review loop.

### Stop Rule

- Stop after isolated build, regression coverage, diff check, and republishing the latest executable.

### Changes

- Added a 300-file guard for icon views, which use WPF's non-virtualized WrapPanel and otherwise realize every thumbnail.
- Automatically use the existing Details `VirtualizingStackPanel` for larger folders and prevent switching back to an icon template until a smaller folder is selected.
- Added boundary regression coverage for the view-mode guard.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-large-folder-virtualized-view -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-large-folder-virtualized-view -p:UseAppHost=false`: passed 9 cases.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: manually open the reported large external folder and verify that Details view remains responsive and process memory stabilizes without the icon-mode thumbnail surge.

## External Folder Details-Only Guard

### Finish Line

External image folders never enter the non-virtualized icon/thumbnail layout, including folders below the previous large-folder threshold.

### Acceptance Checks

- Every external folder uses the existing virtualized Details view before its files are displayed.
- Requests to select Small, Medium, or Large icon view remain blocked while an external folder is active.
- Capture-root folders retain icon views through the 300-file threshold policy.
- Build, nine regression cases, and diff hygiene pass.

### Scope Limit

- No custom virtualizing WrapPanel, paging behavior, or capture-root icon UI change.

### Review Budget

- Second and final implementation/review loop for this freeze report.

### Stop Rule

- Stop after isolated build, regression coverage, diff check, and republishing the latest executable.

### Changes

- Used GLM-5.2 for a bounded cross-check of the list/tree/icon rendering paths.
- Confirmed the running executable matched the previous latest publish and that persisted viewer mode was `SmallIcons`.
- Restricted all external folders to Details mode because the existing WrapPanel icon layouts are non-virtualized even below 300 files.
- Extended the view-policy regression to cover external folders with a single image.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-external-folder-details-only -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-external-folder-details-only -p:UseAppHost=false`: passed 9 cases.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: close the existing process, launch this publish, and manually re-open the reported external folder to verify the status shows Details view and memory no longer grows from icon thumbnails.

## Background Full-Resolution Image Decode

### Finish Line

Opening a high-resolution external image keeps the viewer window responsive while preserving the full-resolution raster used by the editor and save path.

### Acceptance Checks

- Full-resolution image decoding runs outside the WPF UI thread.
- A new file selection cancels an earlier pending decode and prevents stale image assignment.
- The decoded bitmap is frozen before it reaches WPF controls.
- Existing full-resolution editing and save behavior remains unchanged.
- Build, ten regression cases, and diff hygiene pass.

### Scope Limit

- No preview downscaling, image-quality reduction, editor export change, or thumbnail cache redesign.

### Review Budget

- Root-cause implementation after two unsuccessful view-policy mitigations; no adjacent performance work follows this slice.

### Stop Rule

- Stop after isolated build, focused decode regression, diff check, and republishing the latest executable.

### Root Cause Evidence

- The current published executable was confirmed to match `C:\app\MiniCapture.exe` by SHA-256, so stale deployment was excluded.
- The active process retained roughly 300 MB while recent external folders had modest file counts.
- Recent images include a 3646×7671 (28 MP) source. `ViewerWindow.LoadImage` synchronously decoded that source at full resolution on the WPF UI thread, creating an approximately 112 MB raw pixel buffer before rendering overhead.

### Changes

- Split image loading into a cancelable `Task.Run` decode and a UI-thread-only assignment phase.
- Reused the existing full-resolution bitmap and `Freeze()` behavior, so save/export fidelity is unchanged.
- Added a regression that decodes a valid PNG on a background worker and verifies dimensions plus frozen UI handoff.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-background-image-decode -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-background-image-decode -p:UseAppHost=false`: passed 10 cases, including background image decoding.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: manually open the reported 28 MP external image and verify the viewer stays interactive during the loading status, then save once to confirm the original-resolution editor workflow remains intact.

## Viewer File Refresh, Details Sorting, And Tooltip Capture Exclusion

### Finish Line

Refresh the viewer's active folder without navigating away, sort the Details file list by clicking Name, Modified Date, Type, or Size with ascending/descending toggles, and apply capture exclusion to floating/radial tooltip windows.

### Scope Limit

- No general-purpose file manager behavior, file watching, search, or persistent sort preference.
- No capture pipeline or overlay architecture change.

### Changes

- Added a current-folder refresh button beside the browser address and F5 support; the existing toolbar refresh now uses the same folder-preserving path.
- Added clickable Details headers with visible ascending/descending arrows.
- Sorts modified dates as `DateTime` and sizes as bytes rather than formatted display text.
- Keeps the active sort order consistent for refreshes, saves, new list entries, and previous/next navigation.
- Applies the existing best-effort `WDA_EXCLUDEFROMCAPTURE` path when floating-button and radial-menu tooltip HWNDs open.

### Verification

- `dotnet build MiniCapture.slnx`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --no-build`: passed 12 cases, including name/date/type/size sorting.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: manually confirm the address-row refresh button, F5 refresh, all four header toggles, and tooltip capture exclusion on the interactive desktop.
