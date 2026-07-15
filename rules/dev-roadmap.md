# Dev Roadmap

| Status | Milestone | Evidence |
|---|---|---|
| done | Approve product/UX design | `03d01aa`, `notes/brainstorm/20260702_mini_capture_design.md` |
| done | Initialize app-dev workflow docs | `doc/app-dev/`, `rules/dev-*` |
| done | P0 WPF skeleton, floating button, four-mode menu | `src/MiniCapture/`, `dotnet build MiniCapture.slnx`, UIA smoke |
| done | P1 drag/full/timer capture, PNG save, result panel | `dotnet build`, UIA full/timer smoke, region engine smoke |
| done | P2 window picker and overlay exclusion | `dotnet build`, UIA window capture smoke, Esc cancel smoke |
| done | P3 viewer and Explorer-style browser | `dotnet build MiniCapture.slnx`, UIA viewer/browser smoke |
| done | P3 usability polish | mock-aligned floating button, drag repositioning, radial mode menu, wrapping icon views, `dotnet build MiniCapture.slnx` |
| done | Timer UX and window capture fix | delay choices 3/5/7/10, countdown text/sound path, frozen timer region/window selection, delayed window capture smoke, `dotnet build MiniCapture.slnx`, `git diff --check` |
| done | Viewer/browser V1 reinforcement | richer capture-root tree, annotation tools, PNG save/open/copy actions, `dotnet build MiniCapture.slnx`, UIA viewer smoke |
| done | Overlay-free region/window selection | global input hook, small region hint, target-sized window highlight, `dotnet build MiniCapture.slnx`, UIA selection smoke |
| done | TabPaint-inspired viewer editor workflow | pen, arrow, stroke slider, rotate, undo/redo, UIA editor smoke, `dotnet build MiniCapture.slnx` |
| done | TabPaint-inspired viewer toolbar UI | compact icon actions, grouped edit tools, color swatches, inline stroke/text controls, UIA toolbar smoke |
| done | Dark Screenshot Markup Shell viewer redesign | dark File/View/Markup/Properties ribbon, stronger markup row, capture-root library panes, status/zoom bar, 980x580 UIA/PrintWindow check |
| done | P4 packaging and release checks | `dotnet build MiniCapture.slnx`, repeated capture smoke, DPI check, framework-dependent `win-x64` publish launch |
| done | V1 viewer performance and file entry | async folder loading, background thumbnails, PNG/JPG/JPEG file argument opening, 420-file smoke, extension association deferred to installer/registry |
| done | Extension settings UI | separate settings window with extension-association section, floating-button/tray `설정` entries, `--settings` smoke, no direct registry mutation |
| done | Extension settings V1 reinforcement | app-start PNG/JPG/JPEG default-app candidate registration, simplified extension list, Windows default-app selection UI, explanation modal, repair/refresh actions, isolated `dotnet build` |
| done | Viewer layout persistence | viewer window bounds/state, Explorer view mode, and browser pane widths persist through existing `settings.json`; `dotnet build MiniCapture.slnx` |
| done | Window picker frontmost selection fix | Z-order first matching window selection, large-front-window overlap regression, isolated artifacts build/test |
| done | Viewer save/copy and thumbnail refresh | header save/image/path actions, `Ctrl+S`/`Ctrl+C`/`Ctrl+Shift+C`, save-time thumbnail/metadata invalidation, isolated build/test |
| done | V0.1.0 portable release | Korean README, self-contained single-file `win-x64` ZIP, portable launch smoke, GitHub release |
| done | External image full-path browser integration | actual external parent address, unique drive/share path tree, direct-folder image list, isolated build and 6 regression tests |
| done | Viewer single instance and keyboard navigation | mutex/named-pipe launch forwarding, existing viewer reuse, `PreviewKeyDown` previous/next selection, isolated build and 7 regression tests |
| done | Viewer folder-scoped image loading | direct-folder PNG/JPG/JPEG listing for capture and external folders, known latest-image tree reuse, isolated build and 8 regression tests |
| done | External folder loading failure state | retain prior list until direct-folder enumeration succeeds and surface async loading errors, isolated build and 8 regression tests |
| done | Large folder virtualized view guard | force virtualized Details view above 300 images and block non-virtualized icon thumbnails, isolated build and 9 regression tests |
| done | External folder Details-only guard | external folders always use virtualized Details view, with GLM-5.2 cross-check and 9 regression tests |
| done | Background full-resolution image decode | cancelable worker-thread image decode with frozen WPF handoff, preserved raster save path, isolated build and 10 regression tests |
