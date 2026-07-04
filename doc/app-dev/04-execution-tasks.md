# 04 Execution Tasks

## P0: Skeleton And Floating Entry

Status: done in `fb67b43`.

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

Status: done in `246231d`.

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

Status: done in `e956f19`.

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

Status: done in the P3 viewer/browser slice.

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
- First usable slice remains scoped to the capture folder workflow.

Evidence:

- `dotnet build MiniCapture.slnx` passed.
- UI Automation smoke opened the internal viewer from `보기`, exercised Fit/100%/zoom in/out, previous/next, file list selection, and details/small/medium/large view modes.
- FOLLOW_UP: 500-image responsiveness, thumbnail cancellation, and large-folder perf measurement remain P4/follow-up hardening.

## P4: Hardening And Package

Status: done in the P4 hardening/package slice.

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

Evidence:

- `dotnet build MiniCapture.slnx` passed after stopping a stale running app instance that locked the Debug output.
- UI Automation/full capture loop created three consecutive full-screen PNGs.
- Drag UI, window UI, timer+full, viewer controls, and close-to-tray smoke checks passed in the available local environment.
- App DPI awareness reported `2`; the local environment exposed one active display with app virtual bounds `0,0,3840,2160`.
- `dotnet publish src\MiniCapture\MiniCapture.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish\MiniCapture-win-x64-framework-dependent` passed, and the published app launched.

FOLLOW_UP:

- Run a true multi-monitor hardware pass on a multi-display machine.
- Decide later whether V1 needs an installer or self-contained package; current package baseline requires the .NET 8 Windows Desktop Runtime.
