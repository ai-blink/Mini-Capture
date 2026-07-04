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
