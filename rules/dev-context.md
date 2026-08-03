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
- Build passes with 0 warnings/errors; 15 regression cases pass.

## Immediate Next Step

- Manually validate refresh/F5 and all four sort toggles on the interactive desktop.
- Manually select an image area, verify Ctrl+C/Ctrl+X/Ctrl+V, then crop with left/right and top/bottom slider and numeric values before applying.
- Confirm a normal-width viewer wraps its toolbar without clipping, and verify a registered `창 제외` process cannot be selected.
- Confirm capture UI tooltips do not appear in screenshots where Windows capture exclusion is supported.
- Re-run the outstanding repeated-association, sibling-navigation, and high-resolution external-image workflow.
- Keep true multi-monitor validation and installer/default-app packaging as follow-ups.

## Constraints

- Preserve the C# WPF plus Win32 direction and raster-only editor.
- Selection paste uses the selected area's top-left as its target; without a selection it is centered on the canvas.
- Do not expand V1 into OCR, cloud sharing, scrolling/video capture, heavy editing, or general file management.
- Treat `doc/app-dev/05-run-report.md` as the detailed verification baseline.

## Blockers

- None.
