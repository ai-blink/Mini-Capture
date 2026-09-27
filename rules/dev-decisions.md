# Dev Decisions

## Vision

- D-001: Mini Capture is a lightweight capture-first Windows app, not a PicPick/ShareX clone.
- D-002: The app should optimize for fast capture, automatic PNG save, and quick review.

## UX

- D-003: The persistent entrypoint is a transparent always-on-top capture button.
- D-004: The file browser follows Windows Explorer conventions: tree, address/search, details and icon views.
- D-005: Image icon views support small, medium, and large thumbnail sizes.
- D-013: The partial mosaic tool offers exactly two types — sharp block averaging (existing) and Gaussian blur — chosen by the user over a wider 3-type option (adding circular blocks); block size is user-adjustable via a slider (6-64px) instead of a fixed constant. (Tool entry and apply model superseded by D-017; the two types and slider range remain.)
- D-017 (revised 2026-09-27 by the capture-editor comparison): Mosaic is drawn with the mosaic tool `▦`(B) — a drag creates a non-destructive `ViewerAnnotationKind.Mosaic` object (all four capture editors surveyed — Greenshot, ShareX, Flameshot, ksnip — keep pixelate/blur as a retained editable object drawn directly by its tool; the interim pixel-selection "apply mosaic" flow was removed). The object can be re-selected after deselect, moved, resized, re-typed/re-sized, or deleted, following the user's expectation that an applied effect stays editable; it is composed into the bitmap only at save/copy (or when crop/cut/paste commits annotations). The patch is computed from the base image, so shapes beneath it are not pixelated. Block size means image pixels, no longer divided by the zoom factor. Its create/move/resize/property changes are in the undo stack (D-019).
- D-018: Markup interaction follows the modal capture-editor convention (Greenshot/Flameshot/ksnip majority over ShareX hit-test-first): drawing tools always draw on drag; a click (<4 display px) in a drawing tool selects the pressed object instead of creating one; body-drag move is the `객체 선택`(V) tool's job; newly drawn objects stay selected without a tool switch (no forced switch, per ksnip #671); 8 resize handles; text re-edit is double-click in Select; Esc steps crop→drawing→selection→Select tool; single-key tool shortcuts V/M/R/E/A/T/P/B/H (no `Ctrl+D`, whose meaning conflicts across products). Toolbar icon buttons show a text label taken from `AutomationProperties.Name`. The user checked phase 1 in the running app and chose (2026-09-27) to keep Select-tool movement rather than hit-test-first.
- D-019: Edit undo/redo stores one `EditSnapshot(image, cloned annotation list)` per gesture — the state just before the action (Greenshot/ShareX/Flameshot/kImageAnnotator all snapshot annotations too). The frozen image is shared by reference; annotations are cloned without their on-screen element and re-cloned on restore. Continuous input (text typing, mosaic-size slider, typing right after creating a text box) coalesces into one step; selecting another object breaks the coalescing. Bitmap operations that commit annotations (rotate/crop/cut/paste) push a single snapshot, so one undo restores both the image and the editable annotations. Capacity stays 20.
- D-015: `창 제외`에서 실행 중인 대상을 선택하면 제한 권한 조회로 확인 가능한 실행 파일 경로와 프로세스명을 함께 저장하고 파일 경로 기준을 기본값으로 사용한다. 이름 전용 항목은 나중에 경로가 확인될 때만 파일 경로 기본값으로 승격한다. 경로를 이미 가진 항목의 파일 경로·프로세스명 적용 기준은 라디오 버튼으로만 전환하며, 전환 중 다른 값을 삭제하거나 자동으로 재선택하지 않는다.

## Architecture

- D-014: `ViewerWindow.xaml.cs` is split into 14 `partial class` files (1 anchor + 13 responsibility-scoped siblings, including the seam-A Annotations split) by responsibility (13-file granularity chosen over 6 coarse or 20+ fine, since editing-family responsibilities alone total ~1900 lines and would defeat a coarse split); all fields/consts/nested-types/enums stay in the anchor file (shared-state ownership is not reassigned by this refactor); the Annotations region uses "seam A" (create+render vs. mouse-interaction+commit as two files) since 505 lines was still too large as one file. `internal static` test-exposed helpers (`SortFiles`, `CompareFiles`, `IsSameFolderSelection`, `RequiresDetailsView`, `CalculateCropRectangle`, `DecodeImageFile`, `ApplyGaussianBlurRegion`, `BuildGaussianKernel`) keep their exact accessibility so `tests/MiniCapture.Tests` needs no changes.
- D-006: First implementation target is C# WPF plus Win32 P/Invoke and Windows.Graphics.Capture.
- D-007: Capture, save, file index, thumbnail, viewer, and explorer responsibilities remain separate.
- D-010: P1/P2 capture uses GDI `CopyFromScreen` with DWM visible bounds for window capture; Windows.Graphics.Capture is deferred to hardening/fallback work.
- D-011: `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` is applied best-effort to floating and overlay windows where Windows supports it.
- D-016: Multilingual UI (matching the README's EN/KO/ZH/JA promise) uses standard WPF `.resx` resources (`MiniCapture.Resources.Strings`, one independent setup per project — not shared with the sibling `pinte` app), with a hand-written `Strings.Designer.cs` since `dotnet build` alone (no Visual Studio) never runs the single-file generator that would normally produce it. Language switch takes effect after a restart (`CurrentUICulture` applied once at startup) rather than live XAML rebinding, and a self-restart must pass the exiting PID (`--restart-wait <pid>`) so the new process waits for the old single-instance mutex to release instead of racing it.

## Scope

- D-008: OCR, upload/cloud sharing, scrolling capture, video/GIF capture, heavy editing, and general-purpose file management are out of V1.

## Validation

- D-009: Manual Windows validation is required for overlay/capture behavior; unit tests should cover deterministic path/index/tree behavior.
- D-012: P0-P2 completion evidence is `dotnet build` plus UI Automation smoke checks for full, timer, window capture, result actions, and Esc cancel.
