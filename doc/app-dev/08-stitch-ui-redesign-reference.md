# Stitch UI Redesign Reference

Date: 2026-07-04

## Source Inventory

Stitch export root:

- `C:/Users/user/Desktop/stitch_mini_capture_ui_redesign`

Read sources:

- `mini_capture/DESIGN.md`
- `mini_capture_pro_dark_variant/screen.png`
- `mini_capture_pro_dark_variant/code.html`
- `mini_capture_dark_ribbon_variant/screen.png`
- `mini_capture_dark_ribbon_variant/code.html`
- `mini_capture_compact_minimalist_variant/screen.png`
- `mini_capture_compact_minimalist_variant/code.html`
- `mini_capture_light_mica_variant/screen.png`
- `mini_capture_light_mica_variant/code.html`
- `mini_capture_glassmorphism_variant/screen.png`
- `mini_capture_glassmorphism_variant/code.html`
- `mini_capture_hybrid_modern_variant/screen.png`
- `mini_capture_hybrid_modern_variant/code.html`
- `doc/app-dev/07-ui-overhaul-reference-plan.md`
- `src/MiniCapture/ViewerWindow.xaml`
- `src/MiniCapture/ViewerWindow.xaml.cs`

Important handling rule: `code.html` is visual and structural reference only. Do not copy generated HTML/CSS into WPF.

## Candidate Verdict Table

| Candidate | Strengths | Risks | Verdict |
| --- | --- | --- | --- |
| `mini_capture_hybrid_modern_variant` | Best overall balance. Dark canvas, restrained top command area, strong selected tool state, useful left library density, bottom zoom/status controls. | Some glass/premium styling is decorative. `Share` wording implies out-of-scope destinations. Left pane includes broad library concepts. | **Primary direction** |
| `mini_capture_pro_dark_variant` | Most complete app-shell composition. Clear File/View/Markup/Properties split, strong status bar, good action visibility. | Too much full-screen ribbon weight. Sidebar includes `Projects`, `Favorites`, `Trash`, which imply a file manager. Bottom blue status bar is too loud. | Borrow command grouping and status pattern |
| `mini_capture_dark_ribbon_variant` | Strongest Paint/PicPick ribbon reference. Dense commands and group labels are easy to map to WPF. | Overcrowded at small widths. Some Korean labels wrap awkwardly. Looks more like a full editor than a capture viewer. | Borrow toolbar hierarchy only |
| `mini_capture_compact_minimalist_variant` | Small-window safety is good. Light toolbar reads clearly and uses simple group separation. | Too plain for the requested Paint/PicPick direction. Left pane still implies file manager features. | Secondary reference for minimum density |
| `mini_capture_light_mica_variant` | Familiar light Windows surface, clear Explorer-like left pane. | Low emotional fit with requested darker Paint/PicPick references. Large empty canvas feels unfinished. | Avoid as primary |
| `mini_capture_glassmorphism_variant` | Visually polished, strong modern mood, good active command highlight. | Too decorative and card-like. Floating glass toolbar is risky in WPF and may feel like a landing-page mockup rather than a utility. | Avoid as implementation base |

## Selected Design Direction

Primary: `mini_capture_hybrid_modern_variant`.

Borrow from:

- `mini_capture_pro_dark_variant`: complete command groups and status bar metadata.
- `mini_capture_dark_ribbon_variant`: Paint/PicPick-style grouped toolbar vocabulary.
- `mini_capture_compact_minimalist_variant`: minimum-width survival and simpler group separators.

Avoid:

- `mini_capture_glassmorphism_variant`: heavy glass/card treatment.
- `mini_capture_light_mica_variant`: light file-manager-heavy direction.
- Any candidate elements that imply delete, rename, copy, move, general project folders, uploads, print, email, or cloud share.

The final product direction is **Dark Screenshot Markup Shell**:

- dark Windows utility frame
- compact command ribbon, not a full Paint ribbon
- collapsible capture-root library pane
- canvas-first center workspace
- bottom status bar with file path, dimensions, saved state, zoom
- first-class result actions: save, open, copy image, copy path

## Product-Safe Translation

Keep:

- top 32px title/menu strip
- 76-92px command band
- grouped toolbar sections
- active tool background tint plus 2px accent mark
- dark dotted canvas workspace
- left capture library pane
- compact bottom status bar
- visible zoom controls
- blue primary action for save or selected tool

Change:

- `Share` becomes hidden or removed for V1. If a label is needed later, use `내보내기` only for local save/copy actions.
- `Projects`, `Favorites`, and `Trash` are removed. They imply general file management.
- `New Capture` stays only if wired to existing capture flow; it must not create a blank canvas.
- top tabs are not full feature tabs. They are command filters or visual grouping only.
- backdrop blur becomes opaque or semi-opaque WPF brushes. Avoid fragile acrylic simulation.

Reject:

- file delete/rename/copy/move
- project/session model
- layers
- upload/cloud share
- print/email destinations
- broad Paint effects
- floating glass cards over the canvas

## Token Summary

Source `DESIGN.md` values to preserve:

- `primary`: `#0078D4`
- `surface`: `#121414`
- `surface-container-low`: `#1A1C1C`
- `surface-container`: `#1E2020`
- `surface-container-high`: `#282A2B`
- `on-surface`: `#E2E2E2`
- `on-surface-variant`: `#C0C7D4`
- `outline-variant`: `#404752`
- `canvas`: `#1E1E1E`
- base spacing: `4px`
- sidebar width: `240px`
- ribbon height target: `84px`
- status height target: `24-28px`
- radius: `4px`

WPF resource names:

- `ViewerWindowBackgroundBrush`
- `ViewerTitleBarBackgroundBrush`
- `ViewerToolbarBackgroundBrush`
- `ViewerToolbarGroupBorderBrush`
- `ViewerCanvasBackgroundBrush`
- `ViewerCanvasDotBrush`
- `ViewerLibraryPaneBackgroundBrush`
- `ViewerStatusBarBackgroundBrush`
- `ViewerAccentBrush`
- `ViewerAccentMutedBrush`
- `ViewerTextBrush`
- `ViewerMutedTextBrush`
- `ViewerCommandButtonStyle`
- `ViewerToolToggleButtonStyle`
- `ViewerRibbonGroupStyle`
- `ViewerSwatchButtonStyle`
- `ViewerStatusBadgeStyle`

## WPF Layout Contract

```text
ViewerWindow
  Grid
    Row 0: Title strip, 32px
      App icon, filename, File/Edit/View labels if kept, window commands
    Row 1: Command ribbon, 84px
      File group
      View group
      Markup group
      Properties group
      trailing Save primary button
    Row 2: Main workspace, *
      Column 0: Capture library pane, 240px default, collapsible
      Column 1: Canvas viewport, *
    Row 3: Status bar, 26px
      Path, dimensions, format, size, saved state, zoom controls
```

Toolbar group contract:

- File: Save, Open, Open Folder, Copy Image, Copy Path.
- View: Previous, Next, Fit, 100%, Zoom In, Zoom Out, Refresh.
- Markup: Pan, Select, Pen, Arrow, Rectangle, Ellipse, Text, Mosaic.
- Properties: color swatches, stroke width, text value.

Use `ToggleButton` for tools. Use `Button` for commands. Do not style commands and tools as identical controls.

## Current Code Fit

Existing implementation already has most required behavior:

- `ViewerWindow.xaml` includes toolbar button styles, color swatches, image browser, and status areas.
- `ViewerWindow.xaml.cs` already supports `Pan`, `Rectangle`, `Ellipse`, `Mosaic`, `Text`, `Pen`, and `Arrow`.
- Save, open, copy path, copy image, previous/next, zoom, fit, actual size, and refresh are already wired.

The redesign should mostly be a XAML composition and style pass, with minimal event-handler changes.

Implementation should:

- keep existing handler names and automation ids where possible
- introduce shared ribbon group styles
- convert tool buttons to selected-state toggle visuals if feasible without rewriting the editing engine
- remove broad file-manager-looking labels from the Stitch candidates
- keep browsing constrained to the configured capture root
- keep tray/close behavior unchanged

## Accessibility And Small Window Rules

- Minimum usable size: `980x580`.
- Every icon-only command needs `ToolTip` and `AutomationProperties.Name`.
- Active tool must be visible without relying on color alone.
- Swatches need visible borders in dark and high-contrast modes.
- Long file names must trim with ellipsis before pushing action buttons off-screen.
- Korean labels must not wrap into broken one-character columns.
- Overflow menu is preferred over overlapping toolbar text.

## Anti-Patterns

- Do not reproduce the Stitch glassmorphism literally in WPF.
- Do not add trash, project folders, favorites management, or general Explorer operations.
- Do not add upload/share/cloud destinations because Stitch uses `Share`.
- Do not add a new-canvas workflow.
- Do not add layer controls.
- Do not create a general drawing editor architecture.
- Do not let the left pane dominate the canvas.
- Do not make the bottom status bar bright blue across the full width.

## Acceptance Checks For The Implementation Slice

- `dotnet build MiniCapture.slnx` passes.
- `git diff --check` passes.
- At `980x580`, toolbar content does not overlap or wrap badly.
- Save, Open, Copy Image, and Copy Path are visible.
- Pan, Select, Pen, Arrow, Rectangle, Ellipse, Text, Mosaic, color, and stroke width are discoverable.
- Capture library stays within capture root and exposes no delete/rename/copy/move actions.
- Existing tray/close behavior still works.

## Next Implementation Prompt

Use `doc/app-dev/08-stitch-ui-redesign-reference.md` as the product-safe translation of the Stitch folder. Implement only the `ViewerWindow` visual/layout redesign toward the `mini_capture_hybrid_modern_variant` direction, borrowing command grouping from `mini_capture_pro_dark_variant` and `mini_capture_dark_ribbon_variant`. Keep existing editing and clipboard behavior. Do not implement general file manager, upload/share, layers, AI, OCR, or heavy editing features.
