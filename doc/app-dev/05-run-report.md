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

## Pixel-Area Clipboard Editing And Sidebar Crop

### Finish Line

Provide a pixel-area selection tool with copy, cut, and paste, plus a toolbar crop entry whose live-preview panel appears below the left file list in two rows.

### Scope Limit

- No new file-manager, vector editor, OCR, cloud, scrolling-capture, or video behavior.
- Existing annotation selection, save, undo/redo, and raster image paths remain in place.

### Changes

- Added a distinct pixel-area selection tool without repurposing the existing annotation-selection tool.
- Added toolbar copy, cut, paste, and crop controls; `Ctrl+C`, `Ctrl+X`, and `Ctrl+V` use the selected pixel area when present.
- Added a left-sidebar crop panel directly below the file list: its two rows are left/right and top/bottom, each pairing a slider with manual pixel input.
- Dims the crop-outside area and outlines the retained rectangle before apply; crop, cut, and paste remain undoable raster edits.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\verify-crop-selection -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-crop-selection -p:UseAppHost=false`: passed 13 regression cases, including crop-margin clamping and a one-pixel minimum retained canvas.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: on the interactive desktop, verify clipboard ownership and transparency after cut, pasting into a selected destination, all four crop controls, crop preview, apply/cancel, and undo/redo.

## Configurable Window-Selection Exclusions

### Finish Line

Allow users to exclude a window-selection target by choosing a running process or adding its executable path directly.

### Changes

- Added a `창 제외` settings section with a refreshable running-process picker, executable-file browser/manual path entry, registered-path list, and deletion action.
- Store normalized executable paths in the settings file and exclude matching top-level windows during window selection.
- Keep the built-in DimScreen process exclusion and normalize paths before matching so differently formatted equivalent paths still match.

### Verification

- `dotnet build MiniCapture.slnx --artifacts-path artifacts\build-window-exclusions-2 -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --artifacts-path artifacts\test-window-exclusions-2 -p:UseAppHost=false`: passed 15 regression cases, including executable-path normalization and exclusion matching.
- `git diff --check`: passed; only existing CRLF-conversion warnings were emitted.

### Follow-ups

- FOLLOW_UP: restart the interactive app, register a visible process from `창 제외`, and confirm its window is no longer highlighted or selectable. Repeat with the same process's direct `.exe` path.
- NO_ROADMAP_CHANGE: this is a bounded extension of the completed P2 window-targeting behavior, not a new milestone.

## 창 제외 기준 토글 및 설정 테두리 복구

### Finish Line

실행 중인 프로세스 선택 결과가 확인 가능한 실행 파일 경로와 프로세스명을 함께 보존하고, 사용자가 라디오 버튼으로만 적용 기준을 전환할 수 있다. Mini Capture와 `C:\ai\projects\new-alt` 설정 화면의 해당 입력 표면은 오른쪽 테두리까지 온전히 표시한다.

### Scope Limit

- 새 캡처 모드, 제외 매칭 규칙, 설정 정보 구조의 범용 재설계는 추가하지 않는다.
- 라디오 전환은 저장된 경로·프로세스명 값을 삭제하거나 자동으로 다른 기준을 선택하지 않는다.

### Acceptance Checks

- Mini Capture: 선택한 프로세스에서 얻은 경로와 프로세스명이 모두 저장되고, 라디오 토글이 적용 기준만 바꾼다.
- Mini Capture 및 `new-alt`: 관련 설정 표면의 오른쪽 테두리가 지원 창 너비에서 잘리지 않는다.
- 각 프로젝트의 표준 빌드와 관련 자동 검사를 통과한다. 공유 데스크톱의 UI 조작이 안전하지 않으면 실제 화면 확인은 `NEEDS_USER_UI_CHECK`로 남긴다.

### Review Budget And Stop Rule

- 구현 뒤 최대 두 번의 수정/검토 루프만 사용한다.
- 위 확인을 통과하면 종료하고, 관련 없는 발견은 `FOLLOW_UP`으로 기록한다.

### Changes

- 기존 `WindowCaptureExcludedExecutablePaths` JSON은 읽을 때 새 대상 레코드로 옮겨 기존 설정을 보존한다. 새 레코드는 실행 파일 경로, 프로세스명, 현재 적용 기준을 함께 저장한다.
- 실행 중인 프로세스에서 경로를 얻으면 파일 경로 기준으로 추가하면서 프로세스명도 저장한다. 경로를 얻지 못한 프로세스는 프로세스명 기준으로 추가하며, 이후 기준 변경은 선택한 항목의 라디오 버튼으로만 수행한다.
- 파일 경로 기준과 프로세스명 기준의 매칭을 분리해, 라디오 전환이 반대 값을 지우거나 암묵적으로 fallback하지 않도록 했다.
- Mini Capture `창 제외`와 `new-alt` 전역 설정의 세로 스크롤 본문은 뷰포트 폭을 유지하고, 내부 입력만 축소 가능하게 바꿨다. 따라서 긴 경로가 카드 외곽의 오른쪽 테두리를 밀어 화면 밖으로 내보내지 않는다.
- `Process.MainModule` 대신 `PROCESS_QUERY_LIMITED_INFORMATION`과 `QueryFullProcessImageName`으로 일반 프로세스의 실행 경로를 조회하고, 같은 이름의 기존 이름 전용 항목은 경로와 파일 경로 기본값으로 승격한다. Windows가 보호하는 프로세스만 이름 기준으로 남는다.
- 직접 경로 추가 행은 `파일 선택` 버튼의 왼쪽 10px 간격과 96px 최소 너비를 포함한 106px 열을 사용해, 버튼 오른쪽 테두리가 열 밖으로 넘치지 않게 했다.

### Verification

- `dotnet build MiniCapture.slnx --nologo`: 경고 0개, 오류 0개.
- `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --no-build`: 20/20 통과. 경로 기준 매칭, 프로세스명 기준 전환, 두 식별자 보존 검사를 포함한다.
- `dotnet build MiniCapture.slnx --nologo` 및 `dotnet run --project tests\MiniCapture.Tests\MiniCapture.Tests.csproj --no-build`: 경고 0개·오류 0개, 20/20 통과. 이름 전용 항목의 경로 기준 승격 검사도 포함한다.
- `dotnet build C:\ai\projects\new-alt\next\AltController.Next.slnx --nologo`: 경고 0개, 오류 0개.
- 두 작업공간의 변경 범위 `git diff --check`: Mini Capture는 오류 없음. `new-alt` 전체 검사에는 보호 대상 사용자 파일 `notes/transfers/new-alt/latest_context.md`의 기존 trailing whitespace만 보고됐다.

### Follow-ups

- NEEDS_USER_UI_CHECK: 공유 데스크톱은 조작하지 않았다. Mini Capture의 최소 창 너비에서 파일 경로 카드의 오른쪽 테두리와 라디오 전환을, `new-alt` 전역 설정 최소 너비에서 카드 오른쪽 테두리를 실제로 확인한다.

## v0.1.2 Release Preparation

### Changes

- Updated the normal-width viewer toolbar to wrap command groups instead of exposing a horizontal scrollbar or clipping commands.
- Replaced the crop scissors with the Windows four-corner crop glyph, while keeping `영역 잘라내기` as an explicit text command.
- Updated the user README for pixel-area clipboard editing, crop, refresh/sorting, executable-path exclusions, and the v0.1.2 portable package name.

### Release Checks

- Version metadata is `0.1.2` for the application, assembly, file, and informational versions.
- The complete build, regression suite, self-contained single-file publish, archive integrity, and GitHub release upload are required before publication.

### Follow-ups

- Interactive checks for toolbar wrapping, crop, and window-exclusion behavior remain part of the hardware validation follow-up.

## 파일 연결 뷰어 전면 활성화

### Finish Line

탐색기에서 이미지 파일을 열어 이미 실행 중인 Mini Capture에 전달해도 뷰어가 최소화 상태에서 복원되어 즉시 활성화된다.

### Scope Limit

- 파일 연결 등록, 캡처 버튼의 항상 위 표시, 뷰어 편집·탐색 기능은 변경하지 않는다.
- Windows의 전면 창 정책을 우회하는 상시 Topmost 처리나 새 단일 인스턴스 구조는 추가하지 않는다.

### Changes

- 보조 인스턴스가 이미지 경로를 named pipe로 전달하기 직전에, 탐색기의 명시적 열기 동작에서 받은 짧은 포그라운드 권한을 기존 주 인스턴스가 사용할 수 있도록 전달했다.
- 뷰어를 열거나 기존 뷰어에 새 이미지를 전달할 때 최소화 상태를 정상 크기로 복원한 뒤 활성화한다.

### Verification

- `dotnet build .\MiniCapture.slnx -c Debug --no-restore`: 경고 0개, 오류 0개.
- `dotnet run --project .\tests\MiniCapture.Tests\MiniCapture.Tests.csproj -c Debug --no-build`: 17/17 회귀 검사 통과. 단일 인스턴스 인수 전달 검사 포함.
- `git diff --check`: 통과. 기존 CRLF 변환 경고만 발생.

### Release Package

- 앱 메타데이터와 README의 포터블 버전을 `v0.1.3`으로 갱신했다.
- `dotnet build .\MiniCapture.slnx -c Release --no-restore`: 경고 0개, 오류 0개.
- `dotnet run --project .\tests\MiniCapture.Tests\MiniCapture.Tests.csproj -c Release --no-build`: 17/17 회귀 검사 통과.
- `dotnet publish .\src\MiniCapture\MiniCapture.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None`: `artifacts/publish/MiniCapture-v0.1.3-win-x64-portable/MiniCapture.exe` 생성.
- `artifacts/MiniCapture-v0.1.3-win-x64-portable.zip`에 `MiniCapture.exe`가 포함됨을 확인했다.

### Follow-ups

- NEEDS_USER_UI_CHECK: 실행 중인 앱이 있을 때 탐색기에서 PNG/JPG/JPEG를 더블클릭해 뷰어가 즉시 앞으로 오는지, 최소화된 뷰어도 복원되는지 확인한다.
- NO_ROADMAP_CHANGE: 기존 `반복 파일 연결 실행` 수동 검증 항목의 범위다.

## 뷰어 이미지 전환 리소스 급증

### Finish Line

이미지가 열린 상태에서 다른 이미지를 열어도 원본 디코딩과 폴더 인덱싱이 중복·동시 실행되어 CPU나 메모리가 급증하지 않는다.

### Root Cause

- 요청 이미지는 파일 목록을 채우기 전과 후에 각각 원본 해상도로 디코딩되어, 한 번의 열기에도 두 개의 백그라운드 디코드가 시작됐다.
- 취소된 WPF 이미지 디코더는 이미 시작된 동기 디코드를 중단하지 못해, 다음 이미지 요청의 디코드와 동시에 실행될 수 있었다.
- 같은 폴더의 다른 이미지도 전체 파일 인덱스와 폴더 트리를 다시 만들었다.

### Changes

- 파일 목록이 준비된 뒤 선택된 요청 이미지만 한 번 로드하도록 중복 호출을 제거했다.
- 현재 파일 목록에 있는 같은 폴더의 이미지는 인덱스 새로고침 없이 바로 선택한다.
- 원본 이미지 디코드는 전역 게이트로 한 번에 하나만 실행한다. 취소된 요청은 대기 중이면 디코드 전에 종료된다.

### Verification

- `dotnet build .\MiniCapture.slnx -c Debug --no-restore`: 경고 0개, 오류 0개.
- `dotnet run --project .\tests\MiniCapture.Tests\MiniCapture.Tests.csproj -c Debug --no-build`: 17/17 회귀 검사 통과.
- `git diff --check`: 통과. 기존 CRLF 변환 경고만 발생.

### Follow-ups

- NEEDS_USER_UI_CHECK: 큰 PNG/JPG를 연 상태에서 같은 폴더와 다른 폴더의 이미지를 연속으로 열어 CPU·메모리 급증 없이 최종 요청 이미지만 표시되는지 확인한다.
- NO_ROADMAP_CHANGE: 기존 뷰어 성능·대용량 외부 이미지 수동 검증의 보완이다.

## v0.1.4 안정성 릴리스

### Finish Line

뷰어 이미지 전환 리소스 급증과 경로 복사 클립보드 충돌을 하나의 검증된 포터블 릴리스로 배포한다.

### Root Cause

- 뷰어는 요청 이미지를 파일 목록 구성 전후에 중복 디코딩하고, 취소된 동기 디코드가 다음 요청과 겹칠 수 있었다.
- 완료 팝업과 뷰어의 경로 복사는 Windows 공용 클립보드를 한 번만 열어 `CLIPBRD_E_CANT_OPEN` 발생 시 즉시 실패했다.
- 실제 환경의 `copy-manager`가 변경 알림과 250ms 폴링으로 클립보드를 읽어 충돌 가능성을 높였지만, Mini Capture는 특정 외부 프로그램에 의존하지 않고 관측된 HRESULT만 제한적으로 재시도한다.

### Changes

- 같은 폴더의 이미지는 기존 파일 인덱스를 재사용하고, 원본 이미지 디코드를 전역 게이트로 직렬화했다.
- 완료 팝업과 뷰어의 경로 복사를 공통 `ClipboardService`로 통합하고 `0x800401D0`에만 최대 5회 비동기 재시도를 적용했다.
- 앱·어셈블리·파일·정보 버전과 README 포터블 경로를 `0.1.4`로 갱신했다.

### Verification

- `dotnet build .\MiniCapture.slnx -c Release`: 경고 0개, 오류 0개.
- `dotnet run --project .\tests\MiniCapture.Tests\MiniCapture.Tests.csproj -c Release --no-build`: 18/18 회귀 검사 통과.
- 실행 중인 v0.1.2를 백업하고 새 단일 파일을 `C:\app\MiniCapture.exe`에 교체한 뒤 게시본과 설치본 SHA-256 일치를 확인했다.
- 설치본 완료 팝업에서 클립보드를 180ms 강제 점유한 상태로 경로 복사를 한 번 호출해 잠금 해제 후 정확한 경로가 복사됨을 확인했다.
- `git diff --check`: 기존 CRLF 변환 경고 외 오류 없음.

### Release Package

- `artifacts/publish/MiniCapture-v0.1.4-win-x64-portable/MiniCapture.exe`
- `artifacts/MiniCapture-v0.1.4-win-x64-portable.zip`
- 실행 파일 SHA-256: `8522D8DA6CC7413815A19BD8F4DF1093F523B15758424A1F5D0954054818D644`
- ZIP SHA-256: `73A5351C69773640C343F4C90133A0FDFDA5FC1ACAAEA927C5F24972FB9E921B`
- GitHub 릴리스: `https://github.com/ai-blink/Mini-Capture/releases/tag/v0.1.4`

### Follow-ups

- FOLLOW_UP: 큰 PNG/JPG를 같은 폴더와 다른 폴더에서 연속으로 여는 실제 CPU·메모리 수동 검증은 기존 대용량 이미지 체크에 포함한다.
- FOLLOW_UP: 전체 ViewerWindow XAML 수동 회귀와 실제 다중 모니터 검증은 기존 로드맵 항목으로 유지한다.
- Reuse capture: `skip` — 이번 결과는 Mini Capture와 로컬 클립보드 관리자 조합에 특화된 수정이다.

### Next Session

- scope_mode: `patch`
- answer_shape: `patch-first`
- active_constraints: 기존 dirty 파일을 되돌리지 말고, 남은 수동 UI·다중 모니터 항목만 후속으로 취급한다.
- stale_constraints: v0.1.4 버전 갱신·패키징·배포 작업은 릴리스 게시 후 이어받지 않는다.

## v0.1.5 이미지 클립보드 호환성 릴리스

### Finish Line

캡처 완료 팝업, 뷰어, 선택 영역에서 이미지 복사를 한 번 실행하면 현재 클립보드 관리자와 함께 사용하는 환경에서도 이미지가 클립보드에 기록된다.

### Root Cause

- 실제 v0.1.4 뷰어에서 WPF `Clipboard.SetImage`가 `0x800401D0 (CLIPBRD_E_CANT_OPEN)`으로 실패했다.
- 같은 PNG에 50/100/150/200ms 간격의 5회 재시도를 적용해도 모두 실패해, 경로 복사와 같은 재시도만으로는 해결되지 않았다.
- 같은 환경에서 WinForms 이미지 클립보드 경로는 재시도 없이 즉시 성공했다. 따라서 이미지 데이터 전달 방식을 WinForms 호환 경로로 통일했다.

### Changes

- WPF `BitmapSource`를 분리된 `System.Drawing.Bitmap`으로 변환해 WinForms 클립보드에 기록하는 공통 경로를 `ClipboardService`에 추가했다.
- 완료 팝업, 뷰어 전체 이미지, 선택 영역 복사가 모두 공통 경로를 사용하도록 변경했다.
- 이미지 변환 크기와 색상 채널을 확인하는 회귀 테스트를 추가했다.
- 앱·어셈블리·파일·정보 버전과 README 포터블 경로를 `0.1.5`로 갱신했다.

### Verification

- `dotnet run --project tests/MiniCapture.Tests/MiniCapture.Tests.csproj -c Release`: 19/19 통과.
- `dotnet build MiniCapture.slnx -c Release --no-restore`: 경고 0개, 오류 0개.
- 설치본 뷰어 단일 클릭: 성공 상태 및 1060×1050 클립보드 이미지 확인.
- 설치본 캡처 완료 팝업 단일 클릭: 성공 상태 및 3840×2160 클립보드 이미지 확인.
- 검증용 전체 화면 캡처 파일은 확인 후 휴지통으로 이동했다.
- 릴리스 마감 검증: `dotnet build MiniCapture.slnx -c Release --no-restore` 경고 0개, 오류 0개 및 최신 Debug 회귀 검사 23/23 통과.
- 생성한 self-contained 단일 파일 `MiniCapture.exe`(`0.1.5.0`)를 로컬에서 기동해 게시 대상 실행 파일임을 확인했다.

### Release Package

- `artifacts/publish/MiniCapture-v0.1.5-win-x64-portable/MiniCapture.exe`
- `artifacts/MiniCapture-v0.1.5-win-x64-portable.zip`
- 실행 파일 SHA-256: `AA8AC63C2493E901896ACC7A16D51F4A863166EA1E6AAFF2515454CC57A18A44`
- ZIP SHA-256: `8B5032AF15ECA33C636B76096FA33E839044D5F0D379FE44A6EEAAD0BCF87326`

## 설정 창 사용자 확장자 등록

### Finish Line

설정 창에서 이미지 확장자를 입력하면 Mini Capture Viewer의 Windows 선택 후보로 등록되고, 다음 실행과 뷰어 파일 목록에서도 유지한다.

### Root Cause

- 확장자 목록과 등록기는 PNG/JPG/JPEG 세 항목으로 고정되어 있었다.
- 따라서 다른 확장자는 설정 창에 추가할 입력 표면이 없었고, 등록기에 전달돼도 후보 등록 대상에서 제외됐다.

### Changes

- 설정 창에 확장자 입력과 등록 동작을 추가했다. 영문·숫자로 이뤄진 1~32자 확장자만 정규화해 저장한다.
- 추가 확장자를 사용자 설정에 보존하고, 시작 시와 수동 복구 시 기본 확장자와 함께 Windows 후보로 등록한다.
- 파일 인자 처리와 뷰어 파일 브라우저도 저장된 확장자 목록을 동일하게 사용한다.

### Verification

- `dotnet build MiniCapture.slnx`: 경고 0개, 오류 0개.
- `dotnet run --project tests/MiniCapture.Tests/MiniCapture.Tests.csproj --no-build`: 21/21 회귀 검사 통과.
- `git diff --check`: 오류 없음. 기존 CRLF 변환 경고만 발생.

### Follow-up

- NEEDS_USER_UI_CHECK: 설정 → 확장자 연결에서 예를 들어 `.bmp`를 등록한 뒤 Windows 기본 앱 선택 화면에 Mini Capture Viewer 후보가 보이는지 확인한다. 기본 앱 지정은 Windows가 사용자 선택으로만 허용한다.

## 뷰어 라이브러리 증분 갱신

### Finish Line

열려 있는 뷰어에서 현재 폴더에 새 캡처를 열거나 F5를 눌러도 전체 캡처 라이브러리와 폴더 트리를 다시 만들지 않아 창이 멈추지 않는다.

### Root Cause

- 새 캡처는 현재 목록에 아직 없어서 같은 폴더여도 전체 인덱스 갱신으로 넘어갔다.
- 이 경로는 재귀 폴더 트리를 다시 만들고 파일 목록을 비운 뒤 다시 채웠다.
- F5도 현재 폴더 파일만 갱신하면 되는데 폴더 트리 스냅샷을 다시 만들었다.

### Changes

- 현재 표시 중인 폴더의 새 이미지는 정렬 위치에 즉시 추가하고 바로 선택한다.
- 이미 목록에 있는 파일은 메타데이터만 갱신해 정렬 위치를 조정한다.
- F5는 현재 폴더 목록만 비동기로 다시 읽고 기존 트리를 유지한다.
- 새 캡처가 현재 폴더에 있으면 증분 경로를 택하는 회귀 테스트를 추가했다.

### Verification

- 실행 중이던 Mini Capture를 종료한 뒤 `dotnet build MiniCapture.slnx -c Debug --no-restore`를 실행해 경고 0개, 오류 0개를 확인했다.
- `dotnet run --project tests/MiniCapture.Tests/MiniCapture.Tests.csproj --no-build`: 23/23 회귀 검사 통과.
- 표준 Debug 출력의 `MiniCapture.exe`를 다시 실행했다.

### Follow-up

- NEEDS_USER_UI_CHECK: 뷰어를 열어 둔 채 같은 날짜 폴더에 새 캡처를 연속으로 추가하고, 목록이 즉시 갱신되며 창이 멈추지 않는지 확인한다.

## v0.1.6 파일 연결 활성화 릴리스

### Finish Line

이미 열려 있는 뷰어가 Windows 파일 연결로 전달받은 기존 파일을 확장자 인덱스와 독립적으로 받아 해당 파일로 전환한다.

### Root Cause

- 파일 연결 인수도 뷰어 라이브러리의 등록 확장자 필터로 판별했다.
- 따라서 Windows가 Mini Capture에 전달한 기존 파일이라도 등록 목록 밖 확장자는 열기 요청 전에 무시됐다.

### Changes

- 파일 연결용 기존 파일 판별을 인덱스용 확장자 필터에서 분리했다.
- 외부 연결로 연 파일은 현재 폴더 목록에 없더라도 선택 가능한 항목으로 포함한다.
- 기존 뷰어의 같은 폴더 전환과 인수 전달을 고정하는 회귀 테스트를 추가했다.

### Verification

- `dotnet run --project tests/MiniCapture.Tests/MiniCapture.Tests.csproj -c Release --no-restore`: 24/24 통과.
- `dotnet build MiniCapture.slnx -c Release --no-restore`: 경고 0개, 오류 0개.
- self-contained `win-x64` 단일 파일 게시와 ZIP 생성 완료.

### Release Package

- `artifacts/publish/MiniCapture-v0.1.6-win-x64-portable/MiniCapture.exe`
- `artifacts/MiniCapture-v0.1.6-win-x64-portable.zip`
- 실행 파일 SHA-256: `B52DD2274CAC49DD02A3026DA8A6D251FEEE0FDE0AC5B3F99A0C27414BAF4150`
- ZIP SHA-256: `B9DADE5C3CEE12CCCF943CB7AD3E6BF060FD837BFEC12B063AE2962D603C2C24`

### Follow-up

- NEEDS_USER_UI_CHECK: 새 릴리스에서 설정 → 확장자 연결 UI를 열고, 등록된 형식과 다른 외부 이미지 파일을 연속으로 열어 현재 뷰어가 전환되는지 확인한다.

## 선택 영역 모자이크 편집 객체화와 뷰어 창 위치 복구

### Problem

- 사용자가 "선택 영역 모자이크 기능이 사라졌다"고 보고했다. git 이력(`-S`/`-G` 검색)에는 모자이크 제거 커밋이 없었고, HEAD 코드 경로도 온전했다.
- 실제 원인은 표시 위치였다. `settings.json`의 `ViewerLeft=-914`가 복원되면서 뷰어가 `-1157,1450`에 떴고, 창 왼쪽에 붙는 모자이크 설정 줄(`x=-954~-561`)이 3840×2160 단일 모니터 밖에 그려졌다. `ApplyStoredWindowBounds`는 화면과 겹치는지만 검사하고 창을 화면 안으로 끌어오지 않았다.
- 사용 중 추가 요구: 즉시 적용 모자이크는 영역을 고칠 수 없다. 선택 영역·도형을 드래그로 옮길 수 없다. 적용한 모자이크를 선택 해제 후 다시 선택해 편집할 수 없다(Photoshop 기준 기대).

### Changes

- `ClampWindowBounds`: 저장된 뷰어 위치를 가상 화면 안으로 끌어오고, 화면보다 크면 줄인다. 화면과 전혀 겹치지 않으면 기존처럼 기본 위치를 쓴다.
- 즉시 적용 모자이크 도구(`▦`, `ViewerEditTool.Mosaic`)와 그 미리보기·분기를 제거했다.
- `▧` 영역 선택 도구에 "선택 영역 모자이크" 설정 줄(크기 6~64px, 블록/흐림, `모자이크 적용`)을 추가했다.
- 모자이크를 `ViewerAnnotationKind.Mosaic` 비파괴 객체로 전환했다. 원본 이미지 영역에서 조각(`CreateMosaicPatch`)을 만들어 캐시하고, 이동·크기 조절·크기/종류 변경 때 다시 계산한다. 저장·복사 시 `ComposeImageForExport`가 합성한다.
- 모자이크 객체를 선택하면 "모자이크 편집" 줄이 나타나고, 슬라이더·종류 버튼 변경이 선택한 객체에 바로 반영된다.
- 블록 크기는 이제 이미지 픽셀 기준이다(예전에는 확대 배율로 나눴다).
- 선택 영역 안을 끌면 이미지 경계 안에서 이동한다(`MovePixelRect`). 펜·화살표·네모·원 도구 중에도 기존 도형을 끌면 이동한다(텍스트 도구는 클릭 편집 보존을 위해 제외).

### Verification

- `dotnet build MiniCapture.slnx`: 경고 0개, 오류 0개.
- `dotnet run --project tests/MiniCapture.Tests/MiniCapture.Tests.csproj --no-build`: 27/27 통과. 새 테스트 `ClampWindowBounds_PullsOffscreenViewerIntoArea`, `MovePixelRect_KeepsSelectionInsideImage`, `CreateMosaicPatch_AveragesBlocksInsideRegionOnly`.
- 실행 중인 Debug exe UIA 확인: 뷰어가 `0,384`에 복원됨, `ViewerMosaicToolButton` 없음, `▧` 선택 시 크기·블록·흐림·적용 버튼이 화면 안에 표시됨, 네모 도구로 바꾸면 모자이크 줄이 숨겨짐, 선택 없이 적용하면 `먼저 모자이크할 영역을 선택하세요.` 표시.
- 사용자 확인: 선택 영역 → 모자이크 적용 동작.
- 커밋: `05b7bdd`.

### Follow-up

- NEEDS_USER_UI_CHECK: 모자이크 객체를 선택 해제 후 다시 선택해 이동·크기 조절·크기/종류 변경·저장한다. 다른 창이 뷰어를 덮고 있어 마우스 드래그 자동화는 실행하지 않았다.
- 모자이크 객체는 도형과 같이 이미지 되돌리기(`Ctrl+Z`) 대상이 아니다. 흐림은 드래그 중 동기 재계산이며 성능은 측정하지 않았다.
- 관측 1회: 빌드 직후 첫 실행에서 파일 인자로 뷰어가 열리지 않았다(같은 파일을 실행 중 인스턴스에 다시 전달하면 열림, 다음 첫 실행은 정상). 재현되면 조사한다.

## 마크업 편집 UX 1차 — 캡처 편집기 관례 정렬

### Problem

- 사용자 피드백: 선택 영역을 어떤 버튼으로 어떻게 옮기고 고치는지, 텍스트 상자 추가 등이 난해하다. 모자이크 도구에서 드래그·클릭할 때마다 새로 생겨 선택할 방법을 몰랐고, 툴바에서 객체 선택 버튼을 찾지 못했다(점선 아이콘이 픽셀 영역처럼 보임).
- Codex 읽기 전용 UX 리뷰(판정 CHANGES_REQUESTED): `선택` 상태 문구가 "편집하지 않습니다"로 실제 동작과 반대, `선택`/`영역 선택` 명칭 충돌, 텍스트 도구로 도형 클릭 시 새 텍스트 상자 생성 버그, 스페이스 팬 후 커서 복귀 버그, 객체 작업이 Undo에 없음.

### Research

- Codex `terra`/`high` 병렬 조사 3건(공식 문서: PicPick·Paint.NET / 코드: Greenshot `Surface`·`DrawableContainer`, ShareX `ShapeManager` / 코드+이슈: Flameshot `CaptureWidget`, kImageAnnotator `AnnotationArea`).
- 캡처 편집기 4종 모두 모자이크·블러를 도구 드래그로 만드는 편집 가능한 객체로 두고 export 시 합성하며, 객체 추가·이동·크기·삭제를 Undo에 포함한다. 이동 규칙은 Greenshot·Flameshot·ksnip이 "그리기 도구는 새로 그림, 이동은 선택 도구", ShareX만 객체 위 누름 우선(Ctrl로 겹쳐 그리기). 핸들은 Greenshot·Flameshot 8개. 텍스트 재편집은 더블클릭(3종). `Ctrl+D`는 Paint.NET·PicPick(선택 해제)과 ShareX(복제)가 충돌.

### Changes (`00f3383`)

- 모자이크 도구 `▦`(B) 복원: 드래그하면 편집 가능한 모자이크 객체. `▧` 픽셀 영역은 복사·잘라내기·붙여넣기 전용.
- 새로 그린 객체는 도구 전환 없이 선택 유지. 그리기 도구에서 4px 미만 클릭은 누른 객체 선택(빈 곳이면 해제), 드래그는 새로 그림.
- 본문 드래그 이동은 `객체 선택`(V)에서만. 핸들 8개(`ResizeBounds`, 방향별 커서). `객체 선택`에서 텍스트 더블클릭 편집, Esc·바깥 클릭으로 종료.
- Esc 단계: 자르기 취소 → 그리기 취소 → 선택 해제 → 객체 선택 도구. 단축키 V/M/R/E/A/T/P/B/H.
- 버그 수정: 텍스트 도구 도형 클릭 시 텍스트 상자 생성, 스페이스 팬 후 커서, 움직이지 않은 클릭의 수정 표시.
- 툴바: 객체 선택 아이콘을 일반 화살표로, 모든 아이콘 버튼 아래 라벨(`AutomationProperties.Name` 단일 출처), 영역 복사·잘라내기·붙여넣기 아이콘화, 툴팁에 단축키.

### Verification

- `dotnet build MiniCapture.slnx`: 경고 0개, 오류 0개. 회귀 테스트 30/30 통과(새 테스트: `ResizeBounds_MovesOnlyGrabbedEdgesAndKeepsMinimum`, `ToolShortcuts_MapConventionalKeys`, `IsClickGesture_SeparatesClickFromDrag`).
- UIA: 모자이크 도구·툴팁 존재, `모자이크 적용` 버튼 제거, 모자이크 도구에서만 모자이크 설정 줄 표시, 도구별 상태 문구.
- `PrintWindow` 툴바 캡처: 모든 아이콘 버튼에 라벨 1개씩 표시. 첫 캡처에서 이미지 콘텐츠 버튼(핸드·객체 선택)에 라벨이 두 번 붙는 문제를 발견해 수정했다(명시한 `ContentTemplate`은 UIElement 콘텐츠에도 적용됨).

### Follow-up

- NEEDS_USER_UI_CHECK: 마우스·키보드 조작 전체(모자이크 드래그, 클릭 선택, 8방향 크기 조절, V 이동, 텍스트 더블클릭, Esc 단계, 단축키).
- 2차: 객체 작업 포함 Undo/Redo(동작 1회당 스냅샷 1개).
- 사용자 결정 대기: Snagit·ShareX식 객체 위 누름 우선 이동으로 바꿀지.
- 2026-09-27 후속: 사용자가 1차 조작을 실제 앱에서 확인(문제 없음)했고, 이동 방식은 현행(`객체 선택` V에서만 이동)을 유지하기로 결정했다.

## 마크업 편집 UX 2차 — 객체 작업 Undo/Redo

### Problem

- 되돌리기가 비트맵 스냅샷만 쌓아 객체 추가·이동·크기·삭제·속성 변경은 `Ctrl+Z`로 돌아가지 않았다. 회전·자르기·잘라내기·붙여넣기는 "주석 합성"과 "작업"이 스냅샷 2개로 쌓여 되돌리기 2번이 필요했다.

### Changes

- `EditHistory<T>`(신규 `src/MiniCapture/EditHistory.cs`): 동작 직전 상태를 쌓는 Undo/Redo 스택, 용량 20, 새 동작이 redo 갈래를 버림. 기존 `Stack<BitmapSource>` 2개와 `TrimUndoStack`을 대체.
- 스냅샷 = `EditSnapshot(이미지, 주석 복제 목록)`. 이미지는 frozen 참조를 공유(주석만 바뀐 동작은 비트맵 복사 없음), 주석은 `EditableAnnotation.Clone()`으로 화면 요소 없이 복제, 복원 때 다시 복제해 히스토리 스냅샷이 오염되지 않게 한다.
- 기록 지점: 추가(`AddAnnotation`), 이동·크기 변경(드래그 시작 때 잡고 실제 변화가 있을 때만 `EndAnnotationDrag`에서 기록), 삭제, 모자이크 종류·크기, 텍스트 입력. 연속 입력은 병합 키로 한 동작: 텍스트 타이핑, 모자이크 크기 슬라이더, 텍스트 상자 생성 직후의 입력. 다른 객체를 선택하면 병합이 끊긴다.
- 회전·자르기·잘라내기·붙여넣기: 주석이 살아 있는 직전 상태를 1개만 쌓고 합성+작업을 한 동작으로 되돌린다(되돌리면 편집 가능한 주석이 복원된다).
- 드래그·텍스트 편집 중 되돌리기는 그 동작을 먼저 확정한 뒤 되돌린다. 텍스트 상자 안 `Ctrl+Z`는 TextBox 자체 되돌리기 유지.

### Verification

- `dotnet build`(앱·테스트): 경고 0개, 오류 0개. 회귀 테스트 33/33 통과(새 테스트: `EditHistory_UndoRedoWalksOneSnapshotPerAction`, `EditHistory_NewActionDropsRedoAndKeepsCapacity`, `EditableAnnotationClone_IsIndependentOfOriginal`).
- 새 빌드를 파일 인자로 콜드 스타트: 뷰어가 바로 열림(이전 1회 관측 미재현).
- 좌표 입력 자동화 시도는 무효: `SetForegroundWindow`는 성공했지만 화면 캡처상 다른 창(터미널·화상 키보드)이 뷰어를 덮어 입력이 뷰어에 닿지 않았다(모자이크 0개). 입력이 다른 창으로 샐 위험이 있어 중단했다.
- 사용자 승인: 실제 앱 확인 체크리스트(그리기→이동→크기→삭제 후 Undo 4회·Redo 4회, 무변화 클릭 무기록, 텍스트 1회 되돌리기, 모자이크 슬라이더, 회전 후 주석 복원)를 전달한 뒤 사용자가 2차를 승인했다.

### Follow-up

- 모자이크 크기 슬라이더를 놓았다가 같은 객체에서 다시 끌면 두 조작이 한 단계로 묶인다(병합 키가 같은 객체·속성이면 유지). 문제가 되면 슬라이더 조작 종료 시 병합을 끊는다.
- 흐림 모자이크 동기 재계산 성능은 여전히 미측정.

## i18n 2차 — MainWindow·트레이·캡처 오버레이

### Changes

- `MainWindow.xaml`/`.xaml.cs`, 트레이 메뉴(`App.xaml.cs`), `CaptureModeInfo` 이름·안내, `RegionCaptureOverlay`·`WindowPickerOverlay` 안내문을 `Strings.*`로 교체(키 52개 × ko/en/zh-CN/ja). 타이머 3/5/7/10초 버튼은 `Content="3"` + `ContentStringFormat=Main_SecondsFormat`.
- 범위 안 유일한 한국어 리터럴은 `MainWindow.xaml.cs`의 `hotkeyStatus.StartsWith("등록 실패")` — `CaptureHotkeyManager` 출력(이번 범위 밖, 한국어 유지)과 짝이라 의도적으로 남김.

### Verification

- `dotnet build`(테스트 프로젝트 경유): 경고 0개, 오류 0개. 회귀 테스트 33/33 통과.
- 리플렉션(빌드된 `MiniCapture.dll`): 새 키 52개가 4개 문화권 모두 값 존재, en/zh-CN/ja 중 한국어와 같은 값 0개. 예: `Main_SecondsFormat` → 5초/5s/5秒/5秒.
- Debug 빌드 실행: MainWindow 로드 정상, UIA 이름 `드래그 모드 선택됨`(새 키 조합) 확인 후 종료, 배포본 `C:\app\MiniCapture.exe` 재실행.
- NEEDS_USER_UI_CHECK: 영어·중국어·일본어로 전환해 플로팅 버튼 툴팁·우클릭 메뉴, 모드·타이머 버튼, 결과 패널, 트레이 메뉴, 캡처 오버레이 안내문 확인.

### Follow-up

- 남은 비뷰어 문자열: `HotkeyDetectDialog`(9), `CaptureHotkeyManager`(6, 이후 `StartsWith` 판정을 구조화된 결과로 교체), `ScreenCaptureService`(4), `CaptureFileIndex`(6). 그다음 `ViewerWindow`.

## i18n 3차 — 비뷰어 잔여 문자열

- `HotkeyDetectDialog`, `CaptureHotkeyManager`(그룹 이름은 기존 `Hotkey_*` 재사용), `ScreenCaptureService` 예외 문구, `CaptureFileIndex` 폴더 트리 이름을 `Strings.*`로 교체(+19키).
- `CaptureHotkeyManager.Register`가 `HotkeyRegistrationResult(Succeeded, Message)`를 반환 — `MainWindow`의 한국어 접두어 판정 `StartsWith("등록 실패")` 제거(번역 후에도 실패 판정 유지).
- 검증: 빌드 경고 0·오류 0, 테스트 33/33, 리플렉션 19/19 키가 4개 문화권에서 번역값. 뷰어 밖 한국어 리터럴은 `AppLanguage`의 자국어 이름 `한국어`(의도)만 남음.
- NEEDS_USER_UI_CHECK: 비한국어로 키 감지 대화상자와 뷰어 폴더 트리 루트 이름 확인.

## i18n 4차 — ViewerWindow

- `ViewerWindow.xaml` 한국어 속성 139개를 한국어 값 기준으로 키에 자동 매핑해 `{x:Static res:Strings.*}`로 교체, 부분 클래스 13개 파일의 리터럴 약 115개를 `Strings.*`/`string.Format`으로 교체(+214 `Viewer_*` 키, 기존 `Main_Status_*`·`Main_Result_Saved`·`Folder_CaptureRoot`·`Common_Cancel` 재사용). 툴바 라벨은 `AutomationProperties.Name`에서 오므로 함께 번역된다.
- 검증: 빌드 경고 0·오류 0, 테스트 33/33, 리플렉션 214/214 키가 4개 문화권에서 번역값, Debug 빌드를 이미지 인자로 실행해 뷰어 창 로드와 새 키로 만든 UIA 이름 107개 확인(기본 한국어) 후 배포본 재실행. 주석과 자국어 이름 `한국어` 외 한국어 UI 리터럴 0건.
- NEEDS_USER_UI_CHECK: 영어·중국어·일본어에서 보통 창 폭의 툴바 라벨(영어·일본어가 길어 줄바꿈·버튼 폭 변화 가능), 툴팁, 왼쪽 설정 패널, 자르기 패널, 세부 정보 열 머리글·정렬 상태, 상태줄 문구.

## 배포본 교체 (2026-09-28)

- 사용자 보고 "언어 설정 사라짐": 시작 메뉴 바로가기 대상 `C:\app\MiniCapture.exe`가 2026-09-12 빌드라 언어 설정(`222a279`, 09-27) 이전 버전이었다. 기능 회귀가 아니라 오래된 배포본.
- `8dbd782` 기준 단일 파일 Release 게시(`--self-contained true -p:PublishSingleFile=true ...`, `artifacts/publish/MiniCapture-win-x64-portable-20260928`) → en/zh-CN/ja 위성 리소스 번들 포함 확인 → 옛 exe를 세션 scratchpad에 복구용 복사 → 교체·SHA-256 일치·재실행.

## 라이트/다크 테마 설정 (2026-09-28)

- 요청: 설정 창(다크)과 뷰어(라이트) 테마 불일치 → 사용자 선택 "테마를 설정할 수 있게 라이트·다크 모드".
- 변경: `Themes/Light.xaml`·`Dark.xaml`(뷰어 14키 + 설정 18키, 같은 키 목록), `AppTheme`(Light 기본/Dark/System)·`ThemeService`(시작 시 병합, System은 `AppsUseLightTheme`, 다크면 DWM 다크 제목 표시줄), 설정 → 언어·테마 섹션에 테마 콤보·적용하고 재시작(공통 `PromptRestart`). 설정 창·키 감지 대화상자·뷰어의 하드코딩 색을 테마 키로 교체, 뷰어 선택 상태 코드 색(`UiState`)도 테마 키로.
- 발견·수정: 다크에서 비활성 툴바 버튼에 Aero 기본 템플릿의 밝은 회색 배경 → 툴바 버튼 전용 템플릿(주 버튼은 hover에도 강조색 유지).
- 검증: 빌드 0/0, 테스트 36/36(테마 해석, Light·Dark 키 동일, 세 창의 테마 키 참조 205개 모두 정의됨). Debug 빌드로 뷰어를 라이트·다크 각각 `PrintWindow` 캡처해 배경·패널·글자 전환 확인(설정값은 임시 변경 후 원복, 해시 일치). 설정 창은 자동으로 열지 못함(`--settings` 시작 인자는 테마 이전 빌드에서도 창을 열지 않음 → FOLLOW_UP, 우클릭 메뉴 자동화는 불안정해 중단). `C:\app` 배포본 교체(해시 일치).
- NEEDS_USER_UI_CHECK: 다크·시스템 테마에서 설정 창, 키 감지 대화상자, 뷰어, 다크 제목 표시줄.

### 테마 후속 검증 (2026-09-28)

- 정정: "`--settings`로 설정 창이 안 열림"은 버그가 아니었다. 설정 창은 MainWindow 소유 창이라 UIA 최상위 검색에 안 잡혔을 뿐, Win32 `EnumWindows`로는 `Mini Capture 설정`이 보인다.
- 설정 창을 라이트·다크 각각 `PrintWindow` 캡처: 배경·패널·버튼·입력·글자 전환, 다크 제목 표시줄 적용 확인.
- 발견·수정: 확장자 연결 "현재 기본 앱" 열이 고정색 `LightGreen`/`LightSteelBlue`라 라이트에서 거의 안 보임 → 테마 키 `SettingsSuccessTextBrush`(#15803D/#90EE90)·`SettingsInfoTextBrush`(#3B6A96/#B0C4DE).
- 빌드 0/0, 테스트 36/36, `C:\app` 교체(해시 일치). 남은 사용자 확인: 키 감지 대화상자.

## v0.2.0 릴리스 (2026-09-28)

### 포함

- 마크업 객체 작업 되돌리기/다시 실행(동작 1회 = 1단계), 회전·자르기·잘라내기·붙여넣기 1단계 되돌리기.
- 전체 UI 한국어·영어·중국어(간체)·일본어, 설정 → 언어·테마에서 선택(재시작 적용).
- 라이트·다크·시스템 테마, 다크 제목 표시줄.
- 단축키 등록 결과 구조화, 확장자 연결 상태색 테마화, 비활성 툴바 버튼 표시 수정.

### 검증

- `dotnet build MiniCapture.slnx -c Release`: 경고 0개, 오류 0개. `MiniCapture.Tests` Release 36/36 통과.
- 영어·일본어로 뷰어(보통 폭·최소 폭 980)와 설정 창을 `PrintWindow` 캡처: 번역 표시, 최소 폭에서 툴바 2줄 줄바꿈·잘림 없음. 설정 창 부제목이 길면 오른쪽이 잘림(한국어도 동일, FOLLOW_UP).
- 게시 exe 파일 버전 `0.2.0.0`, `C:\app` 교체 후 실행 확인.

### Release Package

- `artifacts/publish/MiniCapture-v0.2.0-win-x64-portable/MiniCapture.exe`
- `artifacts/MiniCapture-v0.2.0-win-x64-portable.zip`
- 실행 파일 SHA-256: `7F5D7E1BDC875E0F73A3B7BE0019C76BDDA1E513BE4B0AF20B3629F9402AC061`
- ZIP SHA-256: `D6734598B636BBB75F4888D1B15F408AAA7280500ABE70CB39A1C82FB0F76B6F`
- 푸시·GitHub 릴리스: GitHub 인증 실패(`gh` 401, git push 인증 실패)로 대기. 태그 `v0.2.0`은 로컬에만 있음.
