# Mini Capture

English | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

Mini Capture is a lightweight Windows screen-capture tool for quickly capturing, viewing, annotating, and saving images. Keep its compact capture button at the edge of your screen, then capture a region, window, full screen, or timed screenshot whenever you need it.

## Preview

### Quick Capture Menu

![Selecting region, window, full-screen, or timed capture from the floating button](docs/assets/readme/quick-capture-menu.gif)

### Capture Library and Viewer

![Browsing captured files and adjusting image zoom in the Mini Capture viewer](docs/assets/readme/viewer-workflow.gif)

## Download the Portable Version

1. Download `MiniCapture-v0.1.6-win-x64-portable.zip` from the [latest release](https://github.com/ai-blink/Mini-Capture/releases/latest).
2. Extract the ZIP file to any folder you prefer.
3. Run `MiniCapture.exe`.

No installation or separate .NET installation is required. If Windows SmartScreen asks for confirmation on first launch, verify the file source and select **More info → Run anyway**.

> Moving the portable folder changes the path used for the image-app candidate registration. Run `MiniCapture.exe` once from the new folder to register its current location.

## Key Features

- Compact always-on-top capture button
- Four capture modes: region, window, full screen, and timer
- Automatic PNG saving
- Built-in viewer for PNG, JPG, and JPEG files, plus original animated GIF playback for GIF files registered in Settings
- Windows Explorer-style mini file browser for capture folders
- Details, small, medium, and large file views
- Fit, actual-size, zoom controls, and direct zoom-percentage input
- Pen, arrow, rectangle, ellipse, text, and mosaic markup tools
- Select, move, resize, delete, undo, and redo markup
- Copy image, copy file path, save, and open-folder actions
- File-list refresh with `F5` and sortable columns in Details view
- Pixel-area selection, copy, cut, paste, and edge cropping
- Manage executable files whose windows should be excluded from window capture
- Remembers window position, file-view mode, and Explorer-panel width

## How to Use

### Capture an Image

Click the Mini Capture button at the edge of the screen, then choose a capture mode.

- **Region**: Drag to select the area you want to capture.
- **Window**: Move over the window you want to capture, then select it.
- **Full Screen**: Capture the entire screen immediately.
- **Timer**: Capture the selected screen after the configured delay.

By default, captures are saved by date in the following folder:

```text
C:\Users\<user name>\Pictures\MiniCapture\year\month\day
```

### View and Edit Images

Open the viewer from a capture result, or open PNG/JPG/JPEG files and GIF files registered in Settings with `MiniCapture.exe`. GIF files play as their original animation instead of stopping on the first frame.

Use the viewer toolbar to move between files, fit the image, view it at actual size, zoom, rotate, and use markup tools.

For zoom percentage, enter a value such as `125` or `125%`. The supported range is 10%–800%. You can also adjust zoom with the slider in the bottom status bar.

### Choose How Windows Are Excluded

In **Settings → Window Exclusions**, select a running process to store both its verified executable path and process name. The default matching method is automatically set to the executable path.

Existing process-name-only entries are upgraded to path matching when their path becomes available later. Only protected processes whose path Windows does not allow the app to query are added by process name.

Afterward, use the radio buttons to switch the matching method. Switching methods preserves both stored values.

### Keyboard Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+S` | Save the current image |
| `Ctrl+C` | Copy the selected pixel area; copy the image with markup if no pixel area is selected |
| `Ctrl+X` | Cut the selected pixel area |
| `Ctrl+V` | Paste an image at the selected area |
| `Ctrl+Shift+C` | Copy the file path |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `←` / `→` | Previous / next image |
| `+` / `-` | Zoom in / out |
| `1` | Actual size |
| `F` | Fit to window |
| `F5` | Refresh the current folder |
| `Delete` | Delete selected markup |
| `Space` | Temporarily use the hand tool while held |

### Set Mini Capture as an Image-App Option

When launched, Mini Capture registers itself as an available app for opening PNG/JPG/JPEG files for the current Windows user. You can add other image extensions in Settings. Windows policies prevent Mini Capture from forcibly changing your existing default app.

1. Open **Settings → File Associations** in Mini Capture.
2. To add another extension, enter one such as `.bmp`, then select **Register Extension**.
3. Select **Choose in Windows Default Apps**.
4. Choose **Mini Capture Viewer** as the default app for that extension.

## System Requirements

- Windows 10 or Windows 11
- 64-bit Windows (`win-x64`)
- Access to the user’s Pictures folder for capturing and saving images

## Privacy and Network Use

Mini Capture saves captured images only on your PC. V1 includes no accounts, cloud uploads, remote sharing, or usage analytics.

## Build from Source

The .NET 8 SDK is required.

```powershell
dotnet build .\MiniCapture.slnx
dotnet run --project .\src\MiniCapture\MiniCapture.csproj
```

To open an image file directly in the viewer, pass its path as an argument:

```powershell
dotnet run --project .\src\MiniCapture\MiniCapture.csproj -- "C:\path\to\image.png"
```

## Create a Portable Package

```powershell
dotnet publish .\src\MiniCapture\MiniCapture.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -o .\artifacts\publish\MiniCapture-v0.1.6-win-x64-portable
```

## V1 Scope

V1 focuses on reliable screen capture, automatic saving, fast viewing, simple markup, and browsing capture folders. It does not include OCR, cloud uploads, scrolling capture, video/GIF recording, heavy image editing, or a general-purpose file manager.

## Version

- Current portable public release: `v0.1.6`
