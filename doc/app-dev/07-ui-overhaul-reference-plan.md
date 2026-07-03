# Mini Capture UI Overhaul Reference Plan

Date: 2026-07-04

## Finish Line

Mini Capture viewer/editor is redesigned as a screenshot-first review and markup surface, not a general Paint clone. The toolbar should make capture review, light annotation, save, open, copy image, and copy path feel immediate.

## Sources Read

Local visual references:

- `C:/Users/user/AppData/Local/Temp/codex-clipboard-b7ce2ca3-3486-4685-b734-b07fc68591be.png`
- `C:/Users/user/AppData/Local/Temp/codex-clipboard-b8f00ec9-70a1-44ac-9569-8c5e1cd39b03.png`
- `C:/Users/user/AppData/Local/Temp/codex-clipboard-1aa66748-3c0e-4b01-a9c1-b98e5fe9173c.png`

Web and repo references:

- [AmirAbdollahi/paint](https://github.com/AmirAbdollahi/paint)
- [PicPick readme](https://picpick.app/en/readme/)
- [PicPick editor help](https://picpick.app/en/help/editor/)
- [Microsoft Paint](https://www.microsoft.com/en-us/windows/paint)
- [ShareX image editor](https://getsharex.com/docs/image-editor)
- [Greenshot help](https://getgreenshot.org/help/)
- [Microsoft Snipping Tool](https://support.microsoft.com/en-us/windows/apps/use-snipping-tool-to-capture-screenshots)
- [Microsoft Photos editor](https://support.microsoft.com/en-us/windows/apps/photos/edit-photos-and-videos-in-windows)

Process references:

- `C:/Users/user/.codex/skills/stitch-reference-translator/SKILL.md`
- `C:/Users/user/.codex/skills/ollama-brief/SKILL.md`
- Subagents:
  - `019f29bd-3576-7e50-b838-cbf4c390580f`: AmirAbdollahi/paint repo analysis
  - `019f29bd-7a8e-7512-8cb5-33d45e2a0b43`: comparable app research
  - `019f29bd-c820-7451-b6a2-3dd40e6bd684`: toolbar IA and WPF handoff planning

## Reference Verdict Table

| Reference | What is useful | What to avoid for V1 | Verdict |
| --- | --- | --- | --- |
| User Image 1: dark Paint/PicPick-like ribbon | Clear groups, large selected tool, color swatches, rulers, canvas/status structure | Full ribbon height, full palette, broad image editing affordances | Borrow grouping and selected state only |
| User Image 2: PicPick classic | Compact ribbon with capture-editor vocabulary, visible canvas, ruler/status bar | Effects, stamps, resize/crop breadth, share/upload | Strong visual reference, reduced scope |
| User Image 3: modern Paint dark | Dark command surface, File menu, quick save/open, palette and zoom/status | Layers, format submenu breadth, new-canvas workflow | Borrow density and contrast, not feature set |
| AmirAbdollahi/paint | Simple tool state machine: shape/freehand/color/thickness/save | WinForms architecture, clear canvas, broad Paint clone behavior | Use as primitive drawing reference |
| PicPick | Capture opens editor automatically; annotations then copy/save; editor has tabs, QAT, ribbon groups, rulers, status bar | Upload/cloud/share, effects, scrolling capture, delete file, heavy format/export menus | Best toolbar inspiration, heavily trimmed |
| Microsoft Paint | Familiar tools: pencil/brush/fill/shapes/text/eraser/select/zoom | AI, layers, background removal, full drawing app expectations | Familiar labels/icons only |
| ShareX | Capture pipeline, annotate/redact/crop, bottom final actions: continue/copy/save/upload | Upload, OCR, automation, wide keybind/tool surface | Best workflow reference for after-capture actions |
| Greenshot | Left/tool menu annotations, obfuscate via pixelize/blur, export and status-bar path actions | Reusable object files, broad editor object management | Strong reference for redaction and result actions |
| Snipping Tool | Mode-first capture, capture copied into an editor surface, simple save/share | Delegating advanced editing to Paint | Reference for simplicity and capture mode clarity |
| Photos | Viewer-first editing: crop/rotate/filter/write/draw on photo | Photo library management, filters/looks | Reference for viewer/editor boundary |

## Selected Direction

Primary direction: **Screenshot Markup Ribbon**.

Mini Capture should feel like a capture viewer that happens to have a small annotation toolbar. It should not feel like a full drawing app. The toolbar becomes a compact two-row command band with four fixed groups:

- File: Save, Open, Open Folder, Copy Image, Copy Path
- View: Previous, Next, Fit, 100%, Zoom In, Zoom Out, Refresh
- Markup: Pan, Select, Pen, Arrow, Rectangle, Ellipse, Text, Mosaic
- Properties: Color swatches, Stroke width, Text input

The annotation group is the visual center. Save and Copy Image are prominent enough to end the workflow quickly. Copy Path and Open Folder are present but visually secondary.

## Toolbar Layout Contract

Target window minimum: `980x580`.

Layout:

```text
ViewerWindow
  Grid
    Row 0: title/action strip, 36-40px
      current filename, save/open/copy actions, undo/redo
    Row 1: compact editor toolbar, 76-92px
      grouped commands: File | View | Markup | Properties
    Row 2: content
      collapsible capture library pane, splitter, canvas viewport
    Row 3: status bar, 26-30px
      file path, dimensions, zoom, cursor/selection state, save state
```

Toolbar behavior:

- Tools are mutually exclusive toggle buttons.
- The active tool has a strong selected state; hover and pressed states remain distinct.
- File/result actions are ordinary command buttons.
- Color swatches are direct buttons, not a large full palette.
- Stroke width uses a compact selector or small slider.
- Text entry appears only when Text tool is selected, or as a compact always-visible input that does not dominate the bar.
- At small widths, lower-priority commands move to an overflow menu before text overlaps.

## Token Summary

Suggested WPF resource names:

- `ViewerToolbarBackgroundBrush`
- `ViewerToolbarBorderBrush`
- `ViewerToolbarGroupSeparatorBrush`
- `ViewerCanvasBackgroundBrush`
- `ViewerStatusBarBackgroundBrush`
- `ViewerAccentBrush`
- `ViewerAccentSubtleBrush`
- `ViewerDangerBrush`
- `ViewerToolButtonStyle`
- `ViewerToolToggleButtonStyle`
- `ViewerSwatchButtonStyle`
- `ViewerToolbarGroupHeaderStyle`
- `ViewerStatusTextStyle`

Suggested sizing:

- Tool button: `32x32`
- Primary command button min width: `72`
- Group gap: `10-14`
- Toolbar row padding: `8`
- Group separator width: `1`
- Swatch: `18x18` or `20x20`
- Status row height: `28`

## WPF Implementation Contract

Primary files:

- `src/MiniCapture/ViewerWindow.xaml`
- `src/MiniCapture/ViewerWindow.xaml.cs`

Implementation notes:

- Reuse the existing in-memory bitmap editing path.
- Keep the existing editor tool enum and command handlers where possible.
- Replace ad hoc button coloring with shared `ToggleButton`/`Button` styles.
- Add `ToolTip` and `AutomationProperties.Name` to every icon-only command.
- Do not introduce a layer model, project format, or object persistence.
- Keep capture library navigation inside the configured capture root.
- Keep tray/close behavior untouched.

Keyboard and accessibility:

- `Ctrl+S`: save edited PNG
- `Ctrl+C`: copy image when canvas has focus
- `Ctrl+Shift+C`: copy file path
- `Space` or `H`: pan tool if this does not conflict with existing shortcuts
- `Esc`: cancel active drawing/selection
- Arrow keys should not break existing file navigation focus
- High contrast mode must still show selected tool, focus rectangle, and color swatch boundary

## Product Scope

Include:

- Pan/hand tool
- Select
- Pen/freehand
- Arrow
- Rectangle
- Ellipse
- Text
- Mosaic/pixelate selected area
- Color select
- Stroke width
- Save PNG
- Open current file
- Open containing folder
- Copy file path
- Copy image
- Previous/next captured image
- Fit/100%/zoom controls

Exclude:

- OCR
- Upload/cloud sharing
- Scrolling capture
- Video/GIF
- AI features
- Layers
- Brushes library
- Stamps library
- Heavy effects menu
- Full palette management
- File delete/rename/copy/move
- General file manager behavior
- New canvas/project/session document model
- Print/email/share destinations

## Similar App Findings

PicPick is the closest toolbar reference. Its docs describe a ribbon toolbar, image tabs, rulers, canvas, and status bar, with Home split into Clipboard, Image, Tools, Size, Colors, and Palette. For Mini Capture, keep the grouped command idea but remove Share, Effects, Stamps, upload destinations, and file deletion.

ShareX is the closest workflow reference. Its image editor flow is capture, add annotations, hide private information with blur or pixelate, then copy/save/upload/continue. Mini Capture should keep capture, annotate, redact, copy/save, and drop upload/automation.

Greenshot is the closest redaction/result-action reference. It supports annotation, shapes, highlight, obfuscate, and after saving exposes path copy/open folder through the status bar. Mini Capture should make copy path and open folder first-class result actions.

Windows Paint is only a familiarity reference. Its simple tools are recognizable, but AI, layers, file format submenus, and broad drawing features should not shape V1.

Snipping Tool and Photos are restraint references. They show that a viewer/editor can stay light if capture/view/edit actions are obvious and advanced editing is out of scope.

## Ollama Brief Note

The auxiliary brief converged on one point: comparable tools standardize the `capture -> quick markup/redaction -> copy/save` path. Mini Capture should reduce the Paint/PicPick visual language to a four-group toolbar, not expand into a complete image editor.

## Acceptance Checks

- `dotnet build MiniCapture.slnx` passes after implementation.
- At `980x580`, toolbar text and icons do not overlap.
- Save, Open, Copy Image, and Copy Path are visible without hunting.
- Pan, Select, Pen, Arrow, Rectangle, Ellipse, Text, Mosaic, Color, and Stroke Width have clear selected/disabled states.
- Editing a capture and saving produces a PNG.
- Copy Image places bitmap data on the clipboard.
- Copy Path places the current file path text on the clipboard.
- Capture library pane never navigates outside the capture root.
- App close/tray behavior remains unchanged.

## Next Implementation Prompt

Read `doc/app-dev/07-ui-overhaul-reference-plan.md` and redesign only `ViewerWindow` toolbar/layout for the Screenshot Markup Ribbon direction. Keep existing editor behavior and bitmap pipeline. Do not add Paint/PicPick-only features. Preserve capture-root browsing and tray behavior. Validate with `dotnet build MiniCapture.slnx` and `git diff --check`.
