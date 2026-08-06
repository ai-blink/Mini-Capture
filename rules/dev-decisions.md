# Dev Decisions

## Vision

- D-001: Mini Capture is a lightweight capture-first Windows app, not a PicPick/ShareX clone.
- D-002: The app should optimize for fast capture, automatic PNG save, and quick review.

## UX

- D-003: The persistent entrypoint is a transparent always-on-top capture button.
- D-004: The file browser follows Windows Explorer conventions: tree, address/search, details and icon views.
- D-005: Image icon views support small, medium, and large thumbnail sizes.
- D-013: The partial mosaic tool offers exactly two types — sharp block averaging (existing) and Gaussian blur — chosen by the user over a wider 3-type option (adding circular blocks); block size is user-adjustable via a slider (6-64px) instead of a fixed constant.

## Architecture

- D-014: `ViewerWindow.xaml.cs` is split into 14 `partial class` files (1 anchor + 13 responsibility-scoped siblings, including the seam-A Annotations split) by responsibility (13-file granularity chosen over 6 coarse or 20+ fine, since editing-family responsibilities alone total ~1900 lines and would defeat a coarse split); all fields/consts/nested-types/enums stay in the anchor file (shared-state ownership is not reassigned by this refactor); the Annotations region uses "seam A" (create+render vs. mouse-interaction+commit as two files) since 505 lines was still too large as one file. `internal static` test-exposed helpers (`SortFiles`, `CompareFiles`, `IsSameFolderSelection`, `RequiresDetailsView`, `CalculateCropRectangle`, `DecodeImageFile`, `ApplyGaussianBlurRegion`, `BuildGaussianKernel`) keep their exact accessibility so `tests/MiniCapture.Tests` needs no changes.
- D-006: First implementation target is C# WPF plus Win32 P/Invoke and Windows.Graphics.Capture.
- D-007: Capture, save, file index, thumbnail, viewer, and explorer responsibilities remain separate.
- D-010: P1/P2 capture uses GDI `CopyFromScreen` with DWM visible bounds for window capture; Windows.Graphics.Capture is deferred to hardening/fallback work.
- D-011: `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` is applied best-effort to floating and overlay windows where Windows supports it.

## Scope

- D-008: OCR, upload/cloud sharing, scrolling capture, video/GIF capture, heavy editing, and general-purpose file management are out of V1.

## Validation

- D-009: Manual Windows validation is required for overlay/capture behavior; unit tests should cover deterministic path/index/tree behavior.
- D-012: P0-P2 completion evidence is `dotnet build` plus UI Automation smoke checks for full, timer, window capture, result actions, and Esc cancel.
