# 04 Execution Tasks

## P0: Skeleton And Floating Entry

Owner: main implementation agent.

Files expected:

- WPF project files.
- Floating button view/model.
- Settings path helper.

Acceptance:

- App launches.
- Transparent topmost capture button appears.
- Button opens four-mode menu.
- App exits cleanly.

## P1: Basic Capture And Save

Owner: main implementation agent.

Scope:

- Drag region capture.
- Full screen capture.
- Timer delay.
- PNG auto-save.
- Result panel open file/folder/viewer actions.

Acceptance:

- Repeated drag/full/timer captures create PNG files.
- Save folder can be opened.
- Capture UI is hidden or excluded from capture as far as supported.

## P2: Window Picker

Owner: capture-focused slice.

Scope:

- Cursor HWND detection.
- Visible frame bounds.
- Highlight overlay.
- Click to capture selected window.

Acceptance:

- Hovering windows updates highlight.
- Clicking captures the highlighted window.
- Escape cancels and returns to idle state.

## P3: Viewer And Explorer

Owner: UI/file-browser slice.

Scope:

- Viewer zoom, fit, 100%, previous/next.
- Capture folder index.
- Windows Explorer-style left tree.
- Details/small/medium/large icon views.
- Lazy thumbnails.

Acceptance:

- Viewer opens the latest capture.
- Left tree can select capture folder path.
- File pane switches views without losing selection.
- 500 image entries remain responsive.

## P4: Hardening And Package

Owner: integration slice.

Scope:

- DPI/multi-monitor checks.
- Repeated capture loops.
- Packaging choice.
- Basic release notes.

Acceptance:

- Manual test matrix passes.
- Known gaps are recorded in run report.
- User can launch a packaged or clearly documented local build.
