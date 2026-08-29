# Dev Context

## Resume Point

- Branch: `main`.
- P0-P4 and V1 viewer/browser/editor reinforcement are code-complete.
- Viewer refresh preserves the active folder and is available beside the address bar, from the toolbar, and through F5.
- Details headers sort Name, Modified Date, Type, and Size with ascending/descending indicators.
- Floating-button and radial-menu tooltip windows receive the existing best-effort capture-exclusion policy.
- Viewer toolbar now includes pixel-area selection, copy/cut/paste, and crop entry; the crop panel expands below the left file list in two rows.
- Normal-width viewer toolbars wrap command groups instead of showing a horizontal scrollbar; the crop command uses the Windows four-corner crop glyph.
- Settings now let users exclude target windows by registered executable path.
- The window-exclusion repair queries executable paths with `PROCESS_QUERY_LIMITED_INFORMATION`/`QueryFullProcessImageName`, so normal processes no longer depend on the overly restrictive `Process.MainModule` query. A formerly name-only target is upgraded to its resolved path and file-path default, while user-chosen radio modes for existing path targets remain intact. The direct-path picker reserves its 10px gap plus 96px button width inside a 106px column so its right border stays inside the card. Build/test evidence is complete; the refreshed process list and border still require user visual confirmation.
- The mosaic edit tool exposes a block-size slider (6-64px) and a block/Gaussian-blur type toggle; `ApplyMosaic` branches on type. `ContextToolbar` height changed from fixed `Height="42"` to `MinHeight="42"` after real-app usage showed the mosaic type buttons clipping (found by launching the built exe, not by code review).
- Viewer image activation now avoids duplicate same-folder indexing/loading, and full-resolution decodes are serialized so canceled synchronous decodes cannot overlap newer requests.
- A new capture in the active viewer folder is inserted into the existing sorted list, while F5 reloads only that folder and preserves the tree; the standard Debug build and 23 regression cases passed.
- Result-popup and viewer path copy share bounded recovery for the observed `CLIPBRD_E_CANT_OPEN` contention error.
- v0.1.4 Release build passes with 0 warnings/errors; 18 regression cases pass; the installed portable executable passed a controlled 180ms clipboard-contention probe with one user action.
- `ViewerWindow.xaml.cs` deep-refactor split is DONE (all 9 stages S0-S8 committed on `main`). Anchor is 203 lines (from 4013), holding only using-header/4 enums/2 nested types/consts/53 fields/constructor/`OpenImage`/`OnLoaded`/`OnClosing`/`OnClosed`. 13 sibling files: Keyboard/ExplorerView/UiState/FileBrowser/Viewport/Commands/EditTools/PixelSelection/EditHistory/Annotations/AnnotationInteraction/Raster/Graphics. Phase 4 (independent regression-risk audit) returned PASS; a multiset line-comparison against the pre-split commit confirmed zero content lines lost. Rollback branch `backup/pre-viewer-split` points at the pre-split commit if ever needed.

## Immediate Next Step

- v0.1.4 is the current portable release; keep only the existing interactive viewer and multi-monitor follow-ups open.
- **Only remaining step for the ViewerWindow split**: run the full manual-GUI regression checklist (design doc `notes/subagents/deep-refactor/20260806_170229_design.md` §6, or Phase 4's consolidated priority order — edit tools first, then annotation select/move/resize/delete, then restart-and-restore-layout, then navigation/toolbar, then keyboard) since the assistant has no interactive-desktop control for this native WPF app and no automated test exercises the actual XAML wiring/click paths.
- Manually verify the mosaic size slider and block/blur type buttons on a real capture.
- Manually add consecutive captures while the viewer displays the same folder and confirm that the list updates without a whole-window freeze.
- Manually validate refresh/F5 and all four sort toggles on the interactive desktop.
- Manually select an image area, verify Ctrl+C/Ctrl+X/Ctrl+V, then crop with left/right and top/bottom slider and numeric values before applying.
- Confirm a normal-width viewer wraps its toolbar without clipping, and verify a registered `창 제외` process cannot be selected.
- NEEDS_USER_UI_CHECK: refresh Mini Capture's running-process list and confirm normal user processes now show executable paths while protected Windows processes remain unavailable; validate the file-picker right border and radio-only rule switching. Then validate `new-alt`'s global-settings right border at its minimum width.
- Re-run the outstanding repeated-association, sibling-navigation, and high-resolution external-image workflow.
- Keep true multi-monitor validation and installer/default-app packaging as follow-ups.

## Constraints

- Preserve the C# WPF plus Win32 direction and raster-only editor.
- Selection paste uses the selected area's top-left as its target; without a selection it is centered on the canvas.
- Do not expand V1 into OCR, cloud sharing, scrolling/video capture, heavy editing, or general file management.
- Treat `doc/app-dev/05-run-report.md` as the detailed verification baseline.

## Blockers

- None.
