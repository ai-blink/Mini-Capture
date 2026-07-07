# Mini Capture

Mini Capture is a lightweight Windows screenshot utility for fast daily capture work. It keeps a compact always-on-top capture button on screen, saves screenshots automatically, and opens captures in a focused viewer with a small Windows Explorer-style browser.

## Features

- Floating transparent capture button with quick capture modes.
- Four capture modes: region drag, target window, full screen, and timer capture.
- Automatic PNG saving under the user's Pictures MiniCapture folder.
- Result actions for opening the saved image, opening its folder, or viewing it in the app.
- Internal image viewer with zoom, fit, 100%, navigation, and light markup tools.
- Capture library browser with a left folder tree and details/small/medium/large image views.
- Optional capture UI exclusion for the floating button, popups, and selection overlays where Windows supports it.

## Tech Stack

- C# / .NET 8
- WPF and Windows Forms tray integration
- Win32 interop for hotkeys, overlays, window picking, and capture UI exclusion
- GDI screen capture through `CopyFromScreen`

## Build

```powershell
dotnet build MiniCapture.slnx
```

The Debug build output is written to:

```text
src/MiniCapture/bin/Debug/net8.0-windows/
```

## Run

```powershell
dotnet run --project src/MiniCapture/MiniCapture.csproj
```

You can also open an image directly in the viewer:

```powershell
dotnet run --project src/MiniCapture/MiniCapture.csproj -- "C:\path\to\image.png"
```

## Publish

```powershell
dotnet publish src/MiniCapture/MiniCapture.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/MiniCapture-win-x64-framework-dependent
```

## V1 Scope

Mini Capture is intentionally smaller than PicPick or ShareX. V1 focuses on reliable screenshot capture, automatic saving, fast review, and capture-folder browsing. OCR, upload/cloud sharing, scrolling capture, video/GIF recording, and general-purpose file management are out of scope.

