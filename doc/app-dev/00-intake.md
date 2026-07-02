# 00 Intake

## Goal

Build a lightweight Windows capture app for a user who wants a stable PicPick/ShareX alternative focused on always-available capture, automatic PNG saving, quick viewing, and capture-folder browsing.

## Problem

PicPick is useful but unreliable for the user, while ShareX is powerful but larger than the desired workflow. The blocker is friction in frequent capture/save/review loops, not missing advanced features.

## User Context

The user works on Windows and wants a small floating capture button that stays available without interrupting the desktop. Captured images should be saved automatically and inspected immediately.

## Scope

In scope:

- Transparent always-on-top capture button.
- Drag region, target window, full screen, and timer capture modes.
- PNG auto-save to a configured folder.
- Save result panel with file open, folder open, viewer open.
- Mini viewer with zoom, fit, 100%, previous/next.
- Windows Explorer-style mini browser with tree navigation and details/small/medium/large icon views.

Out of scope:

- OCR, upload/cloud sharing, scrolling capture, video/GIF capture.
- Heavy image editing.
- General-purpose file manager behavior beyond the capture workflow.

## Success Criteria

- App shows the floating capture button within a perceived 1 second.
- Capture to PNG feels immediate for drag/full-screen capture.
- Four capture modes survive repeated manual use without app restart.
- Capture result can open file, folder, or viewer in one click.
- Recent capture folder listing is responsive for 500 images.
- Thumbnail view switches between small, medium, and large without disruptive scroll jumps.

## Source Artifacts

- Approved design: `notes/brainstorm/20260702_mini_capture_design.md`
- Visual sample: `notes/brainstorm/mini-capture-interactive.html`
