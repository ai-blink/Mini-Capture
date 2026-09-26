# Dev Context

## Resume Point

- Branch: `main`.
- P0-P4 and V1 viewer/browser/editor reinforcement are code-complete; only interactive-desktop validation and the `ViewerWindow` split's manual GUI regression pass remain outstanding on that front (see `rules/dev-progress.md` Next Actions for the full pre-existing checklist).
- Multilingual UI work started 2026-09-27 to match the README's EN/KO/ZH/JA promise (previously only the READMEs were translated, the app itself was 100% Korean-hardcoded with no i18n infra at all). Built `.resx` infra (neutral=Korean + `en`/`zh-CN`/`ja` satellites, 123 keys) with a hand-written `Strings.Designer.cs` (plain `dotnet build` never runs the VS single-file generator, so a checked-in generated class is required), and fully localized `SettingsWindow` (XAML + code-behind + a11y AutomationName strings) plus a new "언어/Language" settings section with restart-to-apply. `ViewerWindow`/`MainWindow`/tray menu are still hardcoded Korean — next slices.
- Fixed a real regression the language-restart flow introduced: `Process.Start` + `Shutdown()` raced the app's single-instance mutex, letting a dying old window get reactivated mid-teardown with its transparent layered-window style broken. Fix: the relaunched process passes `--restart-wait <pid>` and waits for the old PID to fully exit before the single-instance check; normal "already running" launches are unaffected.

- 2026-09-27 viewer editing slice: the "missing" mosaic UI was an off-screen restored viewer (`ViewerLeft=-914`), now clamped by `ClampWindowBounds`. The immediate `▦` mosaic tool is removed; mosaic is created from a `▧` selection as a non-destructive `ViewerAnnotationKind.Mosaic` object (re-select, move, resize, change size/type, Delete; composed on save; block size is in image pixels). Pixel selections and shapes drag-move. Build 0/0, 27/27 tests.

## Immediate Next Step

- NEEDS_USER_UI_CHECK (mosaic object): apply, deselect, re-select, move/resize, change size/type, save. Observed once: a cold start with a file argument did not open the viewer (not reproduced, not investigated).

- NEEDS_USER_UI_CHECK (i18n): switch language in Settings → 언어/Language, confirm the restart prompt applies it, and re-confirm the floating capture button is transparent again afterward.
- Localize `MainWindow.xaml`/`.xaml.cs` (23 Korean literals) + the tray context menu (`App.xaml.cs`) next — small but easy to miss.
- Then localize `ViewerWindow.xaml` (141 Korean lines, largest file) plus its 12 sibling partial-class `.cs` files; read the split design doc first (`notes/subagents/deep-refactor/20260806_170229_design.md`) since responsibilities are spread across files, not one.
- After all windows are localized, re-verify the README's EN/ZH/JA promise actually matches the running app end-to-end.
- Separately (pre-existing, unrelated backlog): run the full manual-GUI regression checklist for the `ViewerWindow.xaml.cs` split (same design doc §6, or Phase 4's consolidated priority order: edit tools → annotation select/move/resize/delete → restart-then-layout-restore → navigation/toolbar → keyboard) — no automated coverage exists for the actual WPF UI wiring/click paths.
- Manually verify: mosaic size/blur controls, consecutive-capture list updates, refresh/F5 + sort toggles, pixel-select Ctrl+C/X/V + crop sliders, toolbar wrap + `창 제외` registered-process exclusion, running-process paths + file-picker/radio behavior in both Mini Capture and `new-alt`.
- Re-run the outstanding repeated-association, sibling-navigation, and high-resolution external-image workflow.
- Keep true multi-monitor validation and installer/default-app packaging as later follow-ups.

## Constraints

- Preserve the C# WPF plus Win32 direction and raster-only editor.
- Selection paste uses the selected area's top-left as its target; without a selection it is centered on the canvas.
- Do not expand V1 into OCR, cloud sharing, scrolling/video capture, heavy editing, or general file management.
- i18n: one independent `.resx` setup per project (not shared with the sibling `pinte` app); language switch is restart-required, not live-rebound; `SettingsWindow` was the user-confirmed first localization slice, not the whole app at once.
- Treat `doc/app-dev/05-run-report.md` as the detailed verification baseline.

## Blockers

- None.
