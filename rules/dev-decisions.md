# Dev Decisions

## Vision

- D-001: Mini Capture is a lightweight capture-first Windows app, not a PicPick/ShareX clone.
- D-002: The app should optimize for fast capture, automatic PNG save, and quick review.

## UX

- D-003: The persistent entrypoint is a transparent always-on-top capture button.
- D-004: The file browser follows Windows Explorer conventions: tree, address/search, details and icon views.
- D-005: Image icon views support small, medium, and large thumbnail sizes.

## Architecture

- D-006: First implementation target is C# WPF plus Win32 P/Invoke and Windows.Graphics.Capture.
- D-007: Capture, save, file index, thumbnail, viewer, and explorer responsibilities remain separate.

## Scope

- D-008: OCR, upload/cloud sharing, scrolling capture, video/GIF capture, heavy editing, and general-purpose file management are out of V1.

## Validation

- D-009: Manual Windows validation is required for overlay/capture behavior; unit tests should cover deterministic path/index/tree behavior.
