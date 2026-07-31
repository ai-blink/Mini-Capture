# Dev Roadmap

| Status | Milestone | Evidence |
|---|---|---|
| done | Product/UX design and app-dev docs | `03d01aa`, `notes/brainstorm/`, `doc/app-dev/`, `rules/dev-*` |
| done | P0-P1 app shell and core capture/save flow | floating button, four modes, dated PNG save, result panel, build/UIA smoke |
| done | P2 Windows capture targeting and exclusion | DWM bounds, DPI handling, overlay-free selection, frozen timer targets, tooltip HWND exclusion |
| done | P3 Explorer-style viewer/browser | folder tree, address, details/icon modes, layout persistence, external paths, current-folder refresh, sortable columns |
| done | Viewer raster editor and shell | selectable markup, undo/redo, rotate, save/copy, compact toolbar, dark markup shell |
| done | Viewer performance and activation | async folders/thumbnails, virtualized guards, single-instance handoff, sibling navigation, background full-resolution decode |
| done | Settings and image-extension entry | extension status/request UI, Windows default-app handoff, PNG/JPG/JPEG launch arguments |
| done | P4 validation and portable release | DPI/capture/viewer/tray smoke, framework-dependent publish, V0.1.x portable release |
| follow_up | Interactive viewer/capture validation | refresh/F5, sort headers, tooltip exclusion, repeated association launch, high-resolution external image |
| follow_up | Multi-monitor hardware validation | true multi-display region/window/full/timer coverage |
| follow_up | Installer/default-app packaging if needed | versioned ProgIDs and quoted `%1` registration outside direct app mutation |
