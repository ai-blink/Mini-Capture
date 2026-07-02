# Mini Capture Project Instructions

## Canonical Entrypoint

This file is the single canonical project instruction entrypoint. Do not create a competing authoritative `AGENTS.md`. If a compatibility shim is ever needed, it should point back to this file.

## Product Target

Build a lightweight Windows capture app that keeps a transparent always-on-top capture button available, supports four capture modes, saves PNG files automatically, and provides a fast viewer with a Windows Explorer-style mini file browser.

## Current Approved Design

- Product/UX design: `notes/brainstorm/20260702_mini_capture_design.md`
- Interactive planning mockup: `notes/brainstorm/mini-capture-interactive.html`
- Approved design commit: `03d01aa`

## Completion Discipline

For non-trivial work:

- Define `Finish Line`, `Acceptance Checks`, `Scope Limit`, `Review Budget`, and `Stop Rule` before implementation.
- Fix only `BLOCKER` issues that prevent the finish line; record `FOLLOW_UP` and `IGNORE_FOR_V1` separately.
- Use at most two fix/review loops unless the user explicitly approves more.
- Stop after acceptance checks pass and update the roadmap/run report instead of continuing adjacent hardening.
- Keep implementation slices small and recoverable. Prefer git history and repeated validation over broad safety layers.

## Implementation Guardrails

- Preferred stack for first build: C# WPF plus Win32 P/Invoke and Windows.Graphics.Capture.
- Keep Electron/WebView approaches out of the MVP unless the user explicitly changes the goal.
- Do not add OCR, upload/cloud sharing, scrolling capture, video/GIF recording, heavy image editing, or a general-purpose file manager in V1.
- Preserve the approved Windows Explorer-style file browser direction: left tree, address/search, right file pane, details/small/medium/large image icon views.
- Floating button and overlays must avoid being captured where Windows support allows it.

## Project Docs

- Intent: `doc/INTENT.md`
- App workflow docs: `doc/app-dev/`
- Live development docs: `rules/dev-*.md`
- Roadmap: `rules/dev-roadmap.md`
