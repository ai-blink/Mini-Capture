# Dev Context

## Resume Point

- Branch: `main` (markup editing UX phase 2 committed; not pushed).
- P0-P4 and V1 viewer/browser/editor reinforcement are code-complete; interactive-desktop validation and the `ViewerWindow` split's manual GUI regression pass remain outstanding (see `rules/dev-progress.md` Next Actions).
- 2026-09-27 viewer editing UX: phase 1 (`00f3383`, D-017/D-018) user-checked in the running app; movement stays in `객체 선택`(V) by user decision. Phase 2 (D-019): `EditHistory<T>` + `EditSnapshot(image, cloned annotations)` — one undo step per gesture for add/move/resize/delete/mosaic property/text, typing and slider drags coalesced; rotate/crop/cut/paste undo in one step and restore editable annotations. Build 0/0, 33/33 tests, user-approved.
- i18n: `.resx` infra + `SettingsWindow` localized with restart-to-apply (`--restart-wait <pid>` race fix). 2026-09-28 slice 2: `MainWindow`, tray, capture-mode texts, capture overlays (+52 keys, reflection-verified). 2026-09-28 slices 3-4: hotkey dialog, capture errors, folder tree, `ViewerWindow` (+233 keys). New UI text must go through `Strings.*` (add to all 4 `.resx` + `Strings.Designer.cs`).

## Immediate Next Step

- NEEDS_USER_UI_CHECK (theme, D-020): Settings → 언어·테마 → 테마 Dark/System → restart; check the settings window, hotkey dialog, viewer, and the dark title bar (PrintWindow could not show it).
- FOLLOW_UP: --settings on startup does not open the settings window (also on the pre-theme build).

- NEEDS_USER_UI_CHECK: i18n slice 2 screens in EN/ZH/JA (floating button tooltip/menu, mode/timer, result panel, tray, capture overlay hints).
- i18n code-complete (slices 3-4 on 2026-09-28: hotkey dialog/registration result, capture errors, folder tree, full `ViewerWindow`). NEEDS_USER_UI_CHECK in EN/ZH/JA — watch toolbar label width in the viewer; then re-check the README language promise end-to-end.
- FOLLOW_UP (editing): re-dragging the mosaic-size slider on the same object merges into the previous undo step; blur recompute cost during drag is unmeasured.
- Observed once, not reproduced (phase-2 cold start worked): a cold start with a file argument did not open the viewer.
- Pre-existing backlog: `ViewerWindow` split manual GUI regression, consecutive-capture list, F5/sort, crop sliders, `창 제외`, association/sibling navigation, multi-monitor, installer.

## Constraints

- Preserve the C# WPF plus Win32 direction and raster-only export (annotations incl. mosaic are composed only at save/copy or when crop/cut/paste commits them).
- Drawing tools always draw on drag; object move belongs to the Select tool (user decision 2026-09-27).
- Every state-changing edit calls `PushUndoSnapshot()` before mutating (or records a drag-start snapshot); new edit actions must follow this.
- Selection paste uses the selected area's top-left as its target; without a selection it is centered on the canvas.
- Do not expand V1 into OCR, cloud sharing, scrolling/video capture, heavy editing, or general file management.
- i18n: one independent `.resx` setup per project; language switch is restart-required.
- Coordinate-based desktop automation is unreliable here (other windows cover the viewer and input leaks to them); hand real-app checks to the user.
- Treat `doc/app-dev/05-run-report.md` as the detailed verification baseline.

## Blockers

- None.
