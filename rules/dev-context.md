# Dev Context

## Resume Point

- Branch: `main` (HEAD `00f3383` + docs commit; not pushed).
- P0-P4 and V1 viewer/browser/editor reinforcement are code-complete; interactive-desktop validation and the `ViewerWindow` split's manual GUI regression pass remain outstanding (see `rules/dev-progress.md` Next Actions).
- 2026-09-27 viewer editing UX (`05b7bdd` → `00f3383`): the "missing mosaic UI" was an off-screen restored viewer, fixed by `ClampWindowBounds`. After a PicPick/Paint.NET/Greenshot/ShareX/Flameshot/ksnip comparison (D-017/D-018): mosaic tool `▦`(B) drag-creates an editable `ViewerAnnotationKind.Mosaic` object (composed at save); newly drawn objects stay selected without a tool switch; in drawing tools a click selects an existing object and a drag (≥4 display px) draws a new one; body-drag move only in `객체 선택`(V); 8 resize handles; double-click text in Select to edit; Esc steps crop→drawing→selection→Select tool; shortcuts V/M/R/E/A/T/P/B/H; plain-arrow Select icon and text labels under every toolbar icon button. Build 0/0, 30/30 tests.
- i18n: `.resx` infra + `SettingsWindow` localized with restart-to-apply (`--restart-wait <pid>` race fix). `ViewerWindow`/`MainWindow`/tray still Korean — note the new toolbar labels come from each button's `AutomationProperties.Name`, so localizing those names localizes the labels.

## Immediate Next Step

- NEEDS_USER_UI_CHECK (editing UX): mosaic drag-create, click-to-select in drawing tools, 8-handle resize, V-tool move, text double-click edit + Esc/outside-click finish, Esc steps, single-key shortcuts, toolbar labels at normal window width.
- Phase 2 (agreed, not started): undo/redo covering annotation add/move/resize/delete/property change — one snapshot per gesture (all four capture editors do this; ours only snapshots the bitmap).
- Open decision: user has not chosen whether to switch to Snagit/ShareX hit-test-first movement (drag on an existing object moves it, Ctrl+drag draws over). Current rule = Select tool moves.
- Observed once, not reproduced: a cold start with a file argument did not open the viewer.
- i18n next slices: `MainWindow` + tray menu, then `ViewerWindow` (12 partial files; read `notes/subagents/deep-refactor/20260806_170229_design.md` first).
- Pre-existing backlog: `ViewerWindow` split manual GUI regression, consecutive-capture list, F5/sort, crop sliders, `창 제외`, association/sibling navigation, multi-monitor, installer.

## Constraints

- Preserve the C# WPF plus Win32 direction and raster-only export (annotations incl. mosaic are composed only at save/copy or when crop/cut/paste commits them).
- Drawing tools always draw on drag; object move belongs to the Select tool unless the user picks the hit-test-first option.
- Selection paste uses the selected area's top-left as its target; without a selection it is centered on the canvas.
- Do not expand V1 into OCR, cloud sharing, scrolling/video capture, heavy editing, or general file management.
- i18n: one independent `.resx` setup per project; language switch is restart-required.
- Treat `doc/app-dev/05-run-report.md` as the detailed verification baseline.

## Blockers

- None.
