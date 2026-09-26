# Dev Progress Archive — 2026-08 (early)

Archived from `rules/dev-progress.md` Current Status (Step 7 archive threshold, >10 entries).

- 2026-07-31: Current-folder refresh and sortable Details columns completed with build and 12 regression cases.
- 2026-07-31: Floating/radial tooltip windows now receive best-effort capture exclusion through their HWND.
- 2026-07-31: Viewer pixel-area selection, clipboard copy/cut/paste, and left-sidebar crop preview/apply controls completed with build and 13 regression cases.
- 2026-08-03: Configurable executable-path exclusions for window targeting completed with build and 15 regression cases.
- 2026-08-03: Viewer toolbar now wraps command groups at normal window widths; the crop command uses a four-corner crop icon.
- 2026-08-06: Partial mosaic tool now exposes an adjustable block-size slider (6-64px) and a block/Gaussian-blur type toggle in the mosaic context group; `ApplyMosaic` branches on the selected type.
- 2026-08-06: `ViewerWindow.xaml.cs` split refactor S0-S8 complete (all 9 stages). Anchor is now 203 lines (from 4013), holding only using-header/enums/nested-types/consts/fields/constructor/lifecycle. 13 new sibling partial-class files: Keyboard, ExplorerView, UiState, FileBrowser, Viewport, Commands, EditTools, PixelSelection, EditHistory, Annotations, AnnotationInteraction (seam A), Raster, Graphics. Each stage is an independent commit on `main`; `backup/pre-viewer-split` branch preserves the pre-split state. Phase 4 (regression risk analysis) returned PASS; a full multiset line-comparison between the pre-split anchor and the 14 post-split files confirms zero content lines were lost or altered (only expected header/brace scaffolding was added).
- 2026-08-07: Fixed a real-app visual bug found after launching the built exe — `ContextToolbar` (the per-tool settings bar shown for Pen/Text/Mosaic/etc.) had a hardcoded `Height="42"` with vertical scrolling disabled, so the new mosaic type buttons (32px tall, taller than the other tools' content) clipped at the bottom. Changed to `MinHeight="42"` so the bar grows to fit its tallest visible content instead of hard-clipping it. The "3-row toolbar" look the user flagged is not new machinery — it reuses the same conditional context-row pattern already used for Pen/Text/Shape tools, now also triggered by Mosaic.
- 2026-08-25: Viewer image switching no longer starts duplicate same-folder indexing/loading, and full-resolution decodes are serialized to prevent canceled synchronous decodes from overlapping newer requests.
- 2026-08-25: Result-popup and viewer path copy now recover from the observed `CLIPBRD_E_CANT_OPEN` contention path; Release build and 18/18 tests pass, and the installed app copied successfully after a controlled 180ms clipboard lock.
- 2026-08-25: v0.1.4 self-contained `win-x64` portable release prepared from the original `main` workspace.
