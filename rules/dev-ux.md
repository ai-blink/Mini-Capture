# Dev UX

## Core Screens

- Floating capture button: persistent transparent topmost button; its tooltips and radial-menu tooltips use best-effort capture exclusion.
- Capture mode menu: drag, window, full screen, timer.
- Region overlay: drag rectangle, cancel with Escape.
- Window picker overlay: highlight cursor target window and click to capture.
- Save result panel: file open, folder open, viewer open.
- Mini viewer: zoom, fit, 100%, previous/next.
- Mini explorer: Windows Explorer-style tree and file pane with current-folder refresh and sortable Details columns.

## Explorer Direction

- Left slide panel uses a tree: quick access, This PC, Pictures, MiniCapture, year/month/day.
- Top area has navigation buttons, address path, and search.
- Right file pane supports details, small icon, medium icon, and large icon views.
- Details headers toggle ascending/descending order for name, modified date, type, and byte size.
- Address-row refresh and F5 reload the active folder without navigating away.
- Thumbnail generation must be lazy enough to keep folder navigation responsive.
