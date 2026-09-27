# Dev Context

## Resume Point

- Branch: `main`, v0.2.0 tagged locally; not pushed (GitHub auth failed).
- P0-P4 and V1 viewer/browser/editor reinforcement are code-complete; interactive-desktop validation and the `ViewerWindow` split's manual GUI regression pass remain outstanding (see `rules/dev-progress.md` Next Actions).
- 2026-09-27 viewer editing UX: phase 1 (`00f3383`, D-017/D-018) user-checked in the running app; movement stays in `객체 선택`(V) by user decision. Phase 2 (D-019): `EditHistory<T>` + `EditSnapshot(image, cloned annotations)` — one undo step per gesture for add/move/resize/delete/mosaic property/text, typing and slider drags coalesced; rotate/crop/cut/paste undo in one step and restore editable annotations. Build 0/0, 33/33 tests, user-approved.
- i18n: `.resx` infra + `SettingsWindow` localized with restart-to-apply (`--restart-wait <pid>` race fix). 2026-09-28 slice 2: `MainWindow`, tray, capture-mode texts, capture overlays (+52 keys, reflection-verified). 2026-09-28 slices 3-4: hotkey dialog, capture errors, folder tree, `ViewerWindow` (+233 keys). New UI text must go through `Strings.*` (add to all 4 `.resx` + `Strings.Designer.cs`).

## Immediate Next Step

- v0.2.0 is built, packaged (rtifacts/MiniCapture-v0.2.0-win-x64-portable.zip), deployed to C:\app, committed and tagged locally. Push + GitHub release are blocked on GitHub auth: ask the user to run `! gh auth login` and `! gh auth setup-git`, then git push origin main --follow-tags and gh release create v0.2.0 <zip> with the notes in doc/app-dev/05-run-report.md (v0.2.0 section).
- User UI checks: the single ordered list in ules/dev-progress.md Next Actions.
- Screen verification recipe (works here): launch the Debug exe, find windows with Win32 `EnumWindows` by title (UIA top-level search misses owned windows), capture with `PrintWindow`; change `%LOCALAPPDATA%\MiniCapture\settings.json` Language/Theme temporarily and restore it.

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
