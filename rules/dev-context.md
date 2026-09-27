# Dev Context

## Resume Point

- Branch: `main` (markup editing UX phase 2 committed; not pushed).
- P0-P4 and V1 viewer/browser/editor reinforcement are code-complete; interactive-desktop validation and the `ViewerWindow` split's manual GUI regression pass remain outstanding (see `rules/dev-progress.md` Next Actions).
- 2026-09-27 viewer editing UX: phase 1 (`00f3383`, D-017/D-018) user-checked in the running app; movement stays in `객체 선택`(V) by user decision. Phase 2 (D-019): `EditHistory<T>` + `EditSnapshot(image, cloned annotations)` — one undo step per gesture for add/move/resize/delete/mosaic property/text, typing and slider drags coalesced; rotate/crop/cut/paste undo in one step and restore editable annotations. Build 0/0, 33/33 tests, user-approved.
- i18n: `.resx` infra + `SettingsWindow` localized with restart-to-apply (`--restart-wait <pid>` race fix). `ViewerWindow`/`MainWindow`/tray still Korean — toolbar labels come from each button's `AutomationProperties.Name`, so localizing those names localizes the labels.

## Immediate Next Step

- i18n next slice: `MainWindow` + tray menu (`App.xaml.cs` 열기/설정/종료), then `ViewerWindow` (12 partial files; read `notes/subagents/deep-refactor/20260806_170229_design.md` first).
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
