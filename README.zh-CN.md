# Mini Capture

[English](README.md) | [한국어](README.ko.md) | 简体中文 | [日本語](README.ja.md)

Mini Capture 是一款轻量级 Windows 屏幕截图工具，可快速捕获、查看、标注和保存图像。将小巧的截图按钮固定在屏幕边缘，需要时即可截取区域、窗口、全屏或定时截图。

## 功能预览

### 快速截图菜单

![从悬浮按钮选择区域、窗口、全屏或定时截图](docs/assets/readme/quick-capture-menu.gif)

### 截图库和查看器

![在 Mini Capture 查看器中浏览截图文件并调整图像缩放](docs/assets/readme/viewer-workflow.gif)

## 下载便携版

1. 从[最新发行版](https://github.com/ai-blink/Mini-Capture/releases/latest)下载 `MiniCapture-v0.1.6-win-x64-portable.zip`。
2. 将 ZIP 文件解压到任意文件夹。
3. 运行 `MiniCapture.exe`。

无需安装，也无需单独安装 .NET。如果 Windows SmartScreen 在首次运行时要求确认，请确认文件来源后选择 **更多信息 → 仍要运行**。

> 移动便携版文件夹后，用于注册图像默认应用候选项的路径也会改变。请在新文件夹中重新运行一次 `MiniCapture.exe`，以注册当前位置。

## 主要功能

- 小巧的始终置顶截图按钮
- 区域、窗口、全屏和定时四种截图方式
- 自动保存为 PNG
- 内置 PNG、JPG 和 JPEG 图像查看器，并可播放在设置中注册的 GIF 原始动画
- 用于浏览截图文件夹的 Windows 资源管理器风格迷你文件浏览器
- 详细信息、小图标、中等图标和大图标四种文件视图
- 适合窗口、实际大小、缩放控件和直接输入缩放百分比
- 画笔、箭头、矩形、椭圆、文本和马赛克标注工具
- 标注的选择、移动、调整大小、删除、撤销和重做
- 复制图像、复制文件路径、保存和打开文件夹
- 使用 `F5` 刷新文件列表，并在详细信息视图中按列排序
- 像素区域选择、复制、剪切、粘贴和从图像边缘裁剪
- 管理应从窗口截图中排除的可执行文件
- 记住窗口位置、文件视图方式和资源管理器面板宽度

## 使用方法

### 截取图像

点击屏幕边缘的 Mini Capture 按钮，然后选择截图方式。

- **区域**：拖动鼠标选择要截取的区域。
- **窗口**：将鼠标移到要截取的窗口上，然后选择它。
- **全屏**：立即截取整个屏幕。
- **定时**：在设定延迟后截取所选屏幕。

默认情况下，截图会按日期保存到以下文件夹：

```text
C:\Users\<用户名>\Pictures\MiniCapture\年份\月份\日期
```

### 查看和编辑图像

您可以从截图结果中打开查看器，也可以使用 `MiniCapture.exe` 打开 PNG/JPG/JPEG 文件或在设置中注册的 GIF。GIF 会播放其原始动画，而不会停留在第一帧。

使用查看器顶部工具栏可在文件之间切换、适合窗口显示、查看实际大小、缩放、旋转和使用标注工具。

缩放百分比输入框支持 `125` 或 `125%` 等输入，支持范围为 10%–800%。也可以通过底部状态栏中的滑块调整缩放。

### 选择窗口排除规则

在 **设置 → 窗口排除** 中选择正在运行的进程时，应用会同时保存可确认的可执行文件路径和进程名称，并自动将默认匹配规则设为文件路径。

现有的仅按进程名匹配的项目会在之后能够确认路径时更新为按文件路径匹配。只有 Windows 实际阻止应用查询路径的受保护进程，才会按进程名添加。

之后可使用单选按钮切换匹配规则；切换时会保留两个已保存的值。

### 常用快捷键

| 快捷键 | 操作 |
| --- | --- |
| `Ctrl+S` | 保存当前图像 |
| `Ctrl+C` | 复制选中的像素区域；未选择像素区域时复制包含标注的图像 |
| `Ctrl+X` | 剪切选中的像素区域 |
| `Ctrl+V` | 将图像粘贴到选定区域的位置 |
| `Ctrl+Shift+C` | 复制文件路径 |
| `Ctrl+Z` / `Ctrl+Y` | 撤销 / 重做 |
| `←` / `→` | 上一张 / 下一张图像 |
| `+` / `-` | 放大 / 缩小 |
| `1` | 实际大小 |
| `F` | 适合窗口 |
| `F5` | 刷新当前文件夹 |
| `Delete` | 删除选中的标注 |
| `Space` | 按住时临时使用手形工具 |

### 将 Mini Capture 作为图像默认应用选项

Mini Capture 启动时会为当前 Windows 用户注册为可打开 PNG/JPG/JPEG 的应用候选项。您也可以在设置窗口中添加其他图像扩展名。根据 Windows 政策，应用不会强制更改您已有的默认应用。

1. 在 Mini Capture 中打开 **设置 → 扩展名关联**。
2. 如需其他扩展名，请输入如 `.bmp`，然后选择 **注册扩展名**。
3. 选择 **在 Windows 默认应用中选择**。
4. 为该扩展名选择 **Mini Capture Viewer** 作为默认应用。

## 系统要求

- Windows 10 或 Windows 11
- 64 位 Windows（`win-x64`）
- 用于截图和保存图像的用户 Pictures 文件夹访问权限

## 隐私与网络

Mini Capture 仅将截图图像保存在您的电脑上。V1 不包含帐户、云上传、远程共享或使用情况分析功能。

## 从源代码构建

需要 .NET 8 SDK。

```powershell
dotnet build .\MiniCapture.slnx
dotnet run --project .\src\MiniCapture\MiniCapture.csproj
```

如需直接在查看器中打开图像文件，请将文件路径作为参数传入：

```powershell
dotnet run --project .\src\MiniCapture\MiniCapture.csproj -- "C:\path\to\image.png"
```

## 创建便携版包

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

## V1 范围

V1 专注于可靠的屏幕截图、自动保存、快速查看、简单标注和浏览截图文件夹。不包括 OCR、云上传、滚动截图、视频/GIF 录制、重度图像编辑或通用文件管理功能。

## 版本

- 当前公开的便携版：`v0.1.6`
