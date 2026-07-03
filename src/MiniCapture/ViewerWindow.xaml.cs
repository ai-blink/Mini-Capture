using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IOPath = System.IO.Path;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfClipboard = System.Windows.Clipboard;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPath = System.Windows.Shapes.Path;
using WpfPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfShape = System.Windows.Shapes.Shape;

namespace MiniCapture;

internal enum ViewerEditTool
{
    Pan,
    Select,
    Rectangle,
    Ellipse,
    Mosaic,
    Text,
    Pen,
    Arrow
}

public partial class ViewerWindow : Window
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8.0;
    private const double ZoomStep = 1.25;
    private const int MaxUndoSnapshots = 20;

    private readonly ObservableCollection<CaptureImageFile> _files = new();
    private readonly List<WpfPoint> _penPoints = new();
    private readonly Stack<BitmapSource> _redoImages = new();
    private readonly Stack<BitmapSource> _undoImages = new();
    private ObservableCollection<FolderTreeNode> _folderNodes = new();
    private BitmapSource? _editableImage;
    private WpfShape? _previewShape;
    private string? _pendingPath;
    private string? _currentFolderPath;
    private string? _lastSavedPath;
    private CaptureImageFile? _currentFile;
    private ViewerEditTool _editTool = ViewerEditTool.Pan;
    private bool _fitMode = true;
    private bool _isDirty;
    private bool _isDrawing;
    private bool _isLoadingSelection;
    private bool _isPanning;
    private bool _spacePanActive;
    private WpfPoint _drawStartPoint;
    private WpfPoint _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private WpfColor _selectedColor = WpfColor.FromRgb(239, 68, 68);
    private double _strokeThickness = 4.0;
    private double _zoom = 1.0;
    private ExplorerViewMode _viewMode = ExplorerViewMode.Details;

    public ViewerWindow(string? imagePath)
    {
        InitializeComponent();
        _pendingPath = imagePath;
        FileList.ItemsSource = _files;
        UpdateToolButtons();
        UpdateColorSwatches();
    }

    public void OpenImage(string? imagePath)
    {
        _pendingPath = imagePath;
        RefreshIndex();
        Activate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetViewMode(ExplorerViewMode.Details);
        RefreshIndex();
    }

    private void RefreshIndex()
    {
        _folderNodes = CaptureFileIndex.BuildFolderTree();
        FolderTree.ItemsSource = _folderNodes;

        var targetPath = ResolveTargetPath(_pendingPath);
        var targetFolder = GetTargetFolder(targetPath);
        SelectFolderPath(targetFolder);
        LoadFolder(targetFolder, targetPath);
        _pendingPath = null;
    }

    private string? ResolveTargetPath(string? requestedPath)
    {
        if (CaptureFileIndex.IsImagePath(requestedPath))
        {
            return requestedPath;
        }

        return CaptureFileIndex.GetLatestImage()?.Path;
    }

    private static string GetTargetFolder(string? targetPath)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var folder = IOPath.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return folder;
            }
        }

        return CaptureFileIndex.RootDirectory;
    }

    private void LoadFolder(string folderPath, string? preferredPath)
    {
        _currentFolderPath = folderPath;
        _files.Clear();
        foreach (var file in CaptureFileIndex.GetImages(folderPath))
        {
            _files.Add(file);
        }

        AddressText.Text = folderPath;
        FileCountText.Text = $"{_files.Count:0}개";

        var selected = FindFile(preferredPath) ?? FindFile(_currentFile?.Path) ?? _files.FirstOrDefault();
        SelectFile(selected);

        if (selected is null)
        {
            ClearImage("선택한 폴더에 이미지가 없습니다.");
        }
    }

    private CaptureImageFile? FindFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _files.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectFile(CaptureImageFile? file)
    {
        _isLoadingSelection = true;
        FileList.SelectedItem = file;
        if (file is not null)
        {
            FileList.ScrollIntoView(file);
        }

        _isLoadingSelection = false;
        LoadImage(file);
    }

    private void LoadImage(CaptureImageFile? file)
    {
        if (file is null)
        {
            ClearImage("표시할 이미지가 없습니다.");
            return;
        }

        try
        {
            using var stream = File.Open(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            _currentFile = file;
            _lastSavedPath = file.Path;
            _editableImage = image;
            ClearEditHistory();
            _isDirty = false;
            PreviewImage.Source = _editableImage;
            EmptyMessage.Visibility = Visibility.Collapsed;
            StatusText.Text = file.Path;
            UpdateStatusMetadata();
            UpdateDirtyIndicator();

            if (_fitMode)
            {
                FitToStage();
            }
            else
            {
                ApplyZoom();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ClearImage($"이미지를 열 수 없습니다: {ex.Message}");
        }

        UpdateNavigationButtons();
    }

    private void ClearImage(string message)
    {
        _currentFile = null;
        _lastSavedPath = null;
        _editableImage = null;
        ClearEditHistory();
        _isDirty = false;
        PreviewImage.Source = null;
        ImageSurface.Width = 0;
        ImageSurface.Height = 0;
        AnnotationOverlay.Width = 0;
        AnnotationOverlay.Height = 0;
        CurrentFileText.Text = string.Empty;
        StatusText.Text = message;
        StatusMetaText.Text = string.Empty;
        SaveStateText.Text = "대기";
        EmptyMessage.Text = message;
        EmptyMessage.Visibility = Visibility.Visible;
        ZoomText.Text = "-";
        StatusZoomText.Text = "-";
        UpdateNavigationButtons();
        UpdateEditButtons();
    }

    private void OnFolderTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not FolderTreeNode { Path: { } path })
        {
            return;
        }

        LoadFolder(path, null);
    }

    private void OnFileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSelection)
        {
            return;
        }

        if (FileList.SelectedItem is CaptureImageFile file)
        {
            LoadImage(file);
        }
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        MoveSelection(-1);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        MoveSelection(1);
    }

    private void MoveSelection(int offset)
    {
        if (_files.Count == 0)
        {
            return;
        }

        var currentIndex = FileList.SelectedIndex < 0 ? 0 : FileList.SelectedIndex;
        var targetIndex = Math.Clamp(currentIndex + offset, 0, _files.Count - 1);
        SelectFile(_files[targetIndex]);
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom / ZoomStep);
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom * ZoomStep);
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        _fitMode = true;
        FitToStage();
    }

    private void OnActualSizeClick(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _pendingPath = _currentFile?.Path;
        RefreshIndex();
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ExplorerViewMode>(tag, out var mode))
        {
            return;
        }

        SetViewMode(mode);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus("저장할 이미지가 없습니다.");
            return;
        }

        var targetPath = GetSaveTargetPath();
        if (targetPath is null)
        {
            SetViewerStatus("저장할 파일을 선택할 수 없습니다.");
            return;
        }

        try
        {
            SavePng(_editableImage, targetPath);
            _lastSavedPath = targetPath;
            _isDirty = false;
            UpdateDirtyIndicator();
            UpdateNavigationButtons();
            SetViewerStatus($"저장됨: {targetPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetViewerStatus($"저장 실패: {ex.Message}");
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var path = GetActiveFilePath();
        if (path is null || !File.Exists(path))
        {
            SetViewerStatus("열 파일이 없습니다.");
            return;
        }

        ShellService.OpenFile(path);
        SetViewerStatus($"열기: {path}");
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var path = GetActiveFilePath();
        if (path is null)
        {
            ShellService.OpenFile(CaptureFileIndex.RootDirectory);
            SetViewerStatus($"폴더 열기: {CaptureFileIndex.RootDirectory}");
            return;
        }

        ShellService.OpenContainingFolder(path);
        SetViewerStatus($"폴더 열기: {path}");
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        var path = GetActiveFilePath();
        if (path is null)
        {
            SetViewerStatus("복사할 파일 경로가 없습니다.");
            return;
        }

        try
        {
            WpfClipboard.SetText(path);
            SetViewerStatus("파일 경로를 클립보드에 복사했습니다.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"클립보드 복사 실패: {ex.Message}");
        }
    }

    private void OnCopyImageClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus("복사할 이미지가 없습니다.");
            return;
        }

        try
        {
            WpfClipboard.SetImage(_editableImage);
            SetViewerStatus("이미지를 클립보드에 복사했습니다.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"이미지 복사 실패: {ex.Message}");
        }
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        UndoEdit();
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        RedoEdit();
    }

    private void OnRotateLeftClick(object sender, RoutedEventArgs e)
    {
        RotateImage(-90);
    }

    private void OnRotateRightClick(object sender, RoutedEventArgs e)
    {
        RotateImage(90);
    }

    private void OnStrokeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _strokeThickness = Math.Max(1, e.NewValue);
        if (StrokeText is not null)
        {
            StrokeText.Text = $"{_strokeThickness:0}px";
        }
    }

    private void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ViewerEditTool>(tag, out var tool))
        {
            return;
        }

        _editTool = tool;
        UpdateToolButtons();
        SetViewerStatus(tool switch
        {
            ViewerEditTool.Pan => "핸드툴: 이미지를 끌어 이동합니다.",
            ViewerEditTool.Select => "선택: 보기 상태를 유지하고 편집하지 않습니다.",
            ViewerEditTool.Rectangle => "네모: 이미지 위에서 드래그해 그립니다.",
            ViewerEditTool.Ellipse => "동그라미: 이미지 위에서 드래그해 그립니다.",
            ViewerEditTool.Mosaic => "모자이크: 가릴 영역을 드래그합니다.",
            ViewerEditTool.Pen => "펜: 이미지 위에 자유선을 그립니다.",
            ViewerEditTool.Arrow => "화살표: 드래그해서 방향을 표시합니다.",
            _ => "텍스트: 이미지 위를 클릭해 입력합니다."
        });
    }

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string colorText })
        {
            return;
        }

        if (System.Windows.Media.ColorConverter.ConvertFromString(colorText) is WpfColor color)
        {
            _selectedColor = color;
            UpdateColorSwatches();
            SetViewerStatus("색상을 선택했습니다.");
        }
    }

    private void OnImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editableImage is null)
        {
            return;
        }

        var position = ClampToSurface(e.GetPosition(AnnotationOverlay));
        if (_editTool == ViewerEditTool.Pan || _spacePanActive)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(ImageScrollViewer);
            _panStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = ImageScrollViewer.VerticalOffset;
            AnnotationOverlay.CaptureMouse();
            AnnotationOverlay.Cursor = WpfCursors.SizeAll;
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.Select)
        {
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.Pen)
        {
            _isDrawing = true;
            _penPoints.Clear();
            _penPoints.Add(position);
            _previewShape = CreatePenPreviewPath();
            AnnotationOverlay.Children.Add(_previewShape);
            AnnotationOverlay.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.Text)
        {
            ApplyText(position);
            e.Handled = true;
            return;
        }

        _isDrawing = true;
        _drawStartPoint = position;
        _previewShape = CreatePreviewShape();
        AnnotationOverlay.Children.Add(_previewShape);
        UpdatePreviewShape(position);
        AnnotationOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void OnImageMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPanning)
        {
            var current = e.GetPosition(ImageScrollViewer);
            ImageScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - (current.X - _panStartPoint.X));
            ImageScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - (current.Y - _panStartPoint.Y));
            e.Handled = true;
            return;
        }

        if (!_isDrawing)
        {
            return;
        }

        var point = ClampToSurface(e.GetPosition(AnnotationOverlay));
        if (_editTool == ViewerEditTool.Pen)
        {
            _penPoints.Add(point);
            UpdatePenPreviewPath();
        }
        else
        {
            UpdatePreviewShape(point);
        }

        e.Handled = true;
    }

    private void OnImageMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            AnnotationOverlay.ReleaseMouseCapture();
            AnnotationOverlay.Cursor = _editTool == ViewerEditTool.Pan ? WpfCursors.SizeAll : WpfCursors.Cross;
            e.Handled = true;
            return;
        }

        if (!_isDrawing)
        {
            return;
        }

        var endPoint = ClampToSurface(e.GetPosition(AnnotationOverlay));
        if (_editTool == ViewerEditTool.Pen)
        {
            _penPoints.Add(endPoint);
            RemovePreviewShape();
            ApplyPen(_penPoints);
            _penPoints.Clear();
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        var rect = GetPixelRect(_drawStartPoint, endPoint);
        RemovePreviewShape();

        if (rect.Width >= 2 && rect.Height >= 2)
        {
            if (_editTool == ViewerEditTool.Mosaic)
            {
                ApplyMosaic(rect);
            }
            else if (_editTool == ViewerEditTool.Arrow)
            {
                ApplyArrow(_drawStartPoint, endPoint);
            }
            else
            {
                ApplyShape(rect, _editTool == ViewerEditTool.Ellipse);
            }
        }

        _isDrawing = false;
        AnnotationOverlay.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SetViewMode(ExplorerViewMode mode)
    {
        var selected = FileList.SelectedItem;
        _viewMode = mode;

        if (mode == ExplorerViewMode.Details)
        {
            FileList.View = CreateDetailsView();
            FileList.ItemTemplate = null;
            FileList.ItemsPanel = (ItemsPanelTemplate)Resources["DetailsItemsPanel"];
            FileList.ItemContainerStyle = (Style)Resources["ExplorerListItemStyle"];
        }
        else
        {
            FileList.View = null;
            FileList.ItemsPanel = (ItemsPanelTemplate)Resources["IconItemsPanel"];
            FileList.ItemContainerStyle = (Style)Resources["IconListItemStyle"];
            FileList.ItemTemplate = (DataTemplate)Resources[mode switch
            {
                ExplorerViewMode.SmallIcons => "SmallIconTemplate",
                ExplorerViewMode.LargeIcons => "LargeIconTemplate",
                _ => "MediumIconTemplate"
            }];
        }

        FileList.SelectedItem = selected;
        UpdateViewButtons();
    }

    private static GridView CreateDetailsView()
    {
        return new GridView
        {
            Columns =
            {
                new GridViewColumn
                {
                    Header = "이름",
                    Width = 180,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.FileName))
                },
                new GridViewColumn
                {
                    Header = "수정한 날짜",
                    Width = 116,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.ModifiedText))
                },
                new GridViewColumn
                {
                    Header = "유형",
                    Width = 48,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.Extension))
                },
                new GridViewColumn
                {
                    Header = "크기",
                    Width = 70,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.SizeText))
                }
            }
        };
    }

    private void PushUndoSnapshot()
    {
        if (_editableImage is null)
        {
            return;
        }

        _undoImages.Push(CloneBitmap(_editableImage));
        while (_undoImages.Count > MaxUndoSnapshots)
        {
            TrimUndoStack();
        }

        _redoImages.Clear();
        UpdateEditButtons();
    }

    private void TrimUndoStack()
    {
        if (_undoImages.Count <= MaxUndoSnapshots)
        {
            return;
        }

        var snapshots = _undoImages.Take(MaxUndoSnapshots).Reverse().ToArray();
        _undoImages.Clear();
        foreach (var snapshot in snapshots)
        {
            _undoImages.Push(snapshot);
        }
    }

    private void UndoEdit()
    {
        if (_editableImage is null || _undoImages.Count == 0)
        {
            return;
        }

        _redoImages.Push(CloneBitmap(_editableImage));
        SetEditableImage(_undoImages.Pop(), "되돌렸습니다.", markDirty: true);
        UpdateEditButtons();
    }

    private void RedoEdit()
    {
        if (_editableImage is null || _redoImages.Count == 0)
        {
            return;
        }

        _undoImages.Push(CloneBitmap(_editableImage));
        SetEditableImage(_redoImages.Pop(), "다시 적용했습니다.", markDirty: true);
        UpdateEditButtons();
    }

    private void ClearEditHistory()
    {
        _undoImages.Clear();
        _redoImages.Clear();
        UpdateEditButtons();
    }

    private static BitmapSource CloneBitmap(BitmapSource source)
    {
        var converted = ConvertToPbgra32(source);
        var clone = new WriteableBitmap(converted);
        clone.Freeze();
        return clone;
    }

    private void RotateImage(double angle)
    {
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var source = ConvertToPbgra32(_editableImage);
        var transformed = new TransformedBitmap(source, new RotateTransform(angle));
        transformed.Freeze();
        SetEditableImage(transformed, angle < 0 ? "왼쪽으로 회전했습니다." : "오른쪽으로 회전했습니다.");
    }

    private string? GetSaveTargetPath()
    {
        var sourcePath = _currentFile?.Path ?? _lastSavedPath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        if (string.Equals(IOPath.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        var folder = IOPath.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = CaptureFileIndex.RootDirectory;
        }

        return IOPath.Combine(folder, $"{IOPath.GetFileNameWithoutExtension(sourcePath)}_edited.png");
    }

    private string? GetActiveFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_lastSavedPath) && File.Exists(_lastSavedPath))
        {
            return _lastSavedPath;
        }

        return _currentFile?.Path;
    }

    private static void SavePng(BitmapSource source, string targetPath)
    {
        var folder = IOPath.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var tempPath = IOPath.Combine(folder ?? CaptureFileIndex.RootDirectory, $"{IOPath.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Open(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void ApplyShape(Int32Rect pixelRect, bool ellipse)
    {
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var source = ConvertToPbgra32(_editableImage);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new WpfRect(0, 0, source.PixelWidth, source.PixelHeight));

            var brush = new SolidColorBrush(_selectedColor);
            var pen = new WpfPen(brush, GetPixelStrokeThickness());
            var rect = new WpfRect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height);
            if (ellipse)
            {
                drawing.DrawEllipse(null, pen, new WpfPoint(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2)), rect.Width / 2, rect.Height / 2);
            }
            else
            {
                drawing.DrawRectangle(null, pen, rect);
            }
        }

        SetEditableImage(RenderBitmap(visual, source.PixelWidth, source.PixelHeight), ellipse ? "동그라미를 그렸습니다." : "네모를 그렸습니다.");
    }

    private void ApplyText(WpfPoint displayPoint)
    {
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var source = ConvertToPbgra32(_editableImage);
        var pixelPoint = DisplayToPixel(displayPoint);
        var text = string.IsNullOrWhiteSpace(AnnotationTextBox.Text)
            ? "Text"
            : AnnotationTextBox.Text.Trim();

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new WpfRect(0, 0, source.PixelWidth, source.PixelHeight));
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                Math.Clamp(22 / Math.Max(_zoom, MinZoom), 14, 96),
                new SolidColorBrush(_selectedColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, source.PixelWidth - pixelPoint.X)
            };
            drawing.DrawText(formatted, pixelPoint);
        }

        SetEditableImage(RenderBitmap(visual, source.PixelWidth, source.PixelHeight), "텍스트를 입력했습니다.");
    }

    private void ApplyMosaic(Int32Rect pixelRect)
    {
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var source = ConvertToBgra32(_editableImage);
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var blockSize = Math.Clamp((int)Math.Round(14 / Math.Max(_zoom, MinZoom)), 6, 64);
        var right = Math.Min(width, pixelRect.X + pixelRect.Width);
        var bottom = Math.Min(height, pixelRect.Y + pixelRect.Height);

        for (var y = pixelRect.Y; y < bottom; y += blockSize)
        {
            for (var x = pixelRect.X; x < right; x += blockSize)
            {
                var blockRight = Math.Min(right, x + blockSize);
                var blockBottom = Math.Min(bottom, y + blockSize);
                ApplyMosaicBlock(pixels, stride, x, y, blockRight, blockBottom);
            }
        }

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        bitmap.Freeze();
        SetEditableImage(bitmap, "모자이크를 적용했습니다.");
    }

    private void ApplyPen(IReadOnlyList<WpfPoint> displayPoints)
    {
        if (_editableImage is null || displayPoints.Count < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var source = ConvertToPbgra32(_editableImage);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new WpfRect(0, 0, source.PixelWidth, source.PixelHeight));
            var pen = CreateDrawingPen();
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var first = DisplayToPixel(displayPoints[0]);
                context.BeginFigure(first, isFilled: false, isClosed: false);
                for (var i = 1; i < displayPoints.Count; i++)
                {
                    context.LineTo(DisplayToPixel(displayPoints[i]), isStroked: true, isSmoothJoin: true);
                }
            }

            geometry.Freeze();
            drawing.DrawGeometry(null, pen, geometry);
        }

        SetEditableImage(RenderBitmap(visual, source.PixelWidth, source.PixelHeight), "펜 선을 그렸습니다.");
    }

    private void ApplyArrow(WpfPoint displayStart, WpfPoint displayEnd)
    {
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var source = ConvertToPbgra32(_editableImage);
        var start = DisplayToPixel(displayStart);
        var end = DisplayToPixel(displayEnd);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new WpfRect(0, 0, source.PixelWidth, source.PixelHeight));
            var pen = CreateDrawingPen();
            drawing.DrawLine(pen, start, end);
            DrawArrowHead(drawing, start, end, pen.Brush);
        }

        SetEditableImage(RenderBitmap(visual, source.PixelWidth, source.PixelHeight), "화살표를 그렸습니다.");
    }

    private void DrawArrowHead(DrawingContext drawing, WpfPoint start, WpfPoint end, System.Windows.Media.Brush brush)
    {
        var vector = start - end;
        if (vector.Length < 1)
        {
            return;
        }

        vector.Normalize();
        var headLength = Math.Max(10, GetPixelStrokeThickness() * 4.5);
        var headWidth = Math.Max(7, GetPixelStrokeThickness() * 2.8);
        var perpendicular = new Vector(-vector.Y, vector.X);
        var p1 = end + (vector * headLength) + (perpendicular * headWidth);
        var p2 = end + (vector * headLength) - (perpendicular * headWidth);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(end, isFilled: true, isClosed: true);
            context.LineTo(p1, isStroked: true, isSmoothJoin: true);
            context.LineTo(p2, isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        drawing.DrawGeometry(brush, null, geometry);
    }

    private static void ApplyMosaicBlock(byte[] pixels, int stride, int left, int top, int right, int bottom)
    {
        long blue = 0;
        long green = 0;
        long red = 0;
        long alpha = 0;
        var count = 0;

        for (var y = top; y < bottom; y++)
        {
            var row = y * stride;
            for (var x = left; x < right; x++)
            {
                var offset = row + (x * 4);
                blue += pixels[offset];
                green += pixels[offset + 1];
                red += pixels[offset + 2];
                alpha += pixels[offset + 3];
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        var averageBlue = (byte)(blue / count);
        var averageGreen = (byte)(green / count);
        var averageRed = (byte)(red / count);
        var averageAlpha = (byte)(alpha / count);

        for (var y = top; y < bottom; y++)
        {
            var row = y * stride;
            for (var x = left; x < right; x++)
            {
                var offset = row + (x * 4);
                pixels[offset] = averageBlue;
                pixels[offset + 1] = averageGreen;
                pixels[offset + 2] = averageRed;
                pixels[offset + 3] = averageAlpha;
            }
        }
    }

    private void SetEditableImage(BitmapSource bitmap, string status, bool markDirty = true)
    {
        if (bitmap.CanFreeze && !bitmap.IsFrozen)
        {
            bitmap.Freeze();
        }

        _editableImage = bitmap;
        _isDirty = markDirty || _isDirty;
        PreviewImage.Source = _editableImage;
        EmptyMessage.Visibility = Visibility.Collapsed;

        if (_fitMode)
        {
            FitToStage();
        }
        else
        {
            ApplyZoom();
        }

        SetViewerStatus(status);
        UpdateDirtyIndicator();
        UpdateEditButtons();
    }

    private WpfPen CreateDrawingPen()
    {
        var brush = new SolidColorBrush(_selectedColor);
        var pen = new WpfPen(brush, GetPixelStrokeThickness())
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static BitmapSource ConvertToPbgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Pbgra32)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static BitmapSource ConvertToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static BitmapSource RenderBitmap(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private double GetPixelStrokeThickness()
    {
        return Math.Clamp(_strokeThickness / Math.Max(_zoom, MinZoom), 1.0, 48.0);
    }

    private WpfShape CreatePreviewShape()
    {
        var stroke = new SolidColorBrush(_selectedColor);
        WpfShape shape = _editTool switch
        {
            ViewerEditTool.Ellipse => new WpfEllipse(),
            ViewerEditTool.Arrow => new WpfPath(),
            _ => new WpfRectangle()
        };

        shape.Stroke = stroke;
        shape.StrokeThickness = 2;
        shape.Fill = WpfBrushes.Transparent;

        if (_editTool == ViewerEditTool.Mosaic)
        {
            shape.Fill = new SolidColorBrush(WpfColor.FromArgb(42, _selectedColor.R, _selectedColor.G, _selectedColor.B));
            shape.StrokeDashArray = new DoubleCollection { 4, 3 };
        }

        return shape;
    }

    private void UpdatePreviewShape(WpfPoint currentPoint)
    {
        if (_previewShape is null)
        {
            return;
        }

        if (_editTool == ViewerEditTool.Arrow && _previewShape is WpfPath path)
        {
            path.Data = CreateArrowPreviewGeometry(_drawStartPoint, currentPoint);
            return;
        }

        var rect = new WpfRect(_drawStartPoint, currentPoint);
        Canvas.SetLeft(_previewShape, rect.Left);
        Canvas.SetTop(_previewShape, rect.Top);
        _previewShape.Width = Math.Max(1, rect.Width);
        _previewShape.Height = Math.Max(1, rect.Height);
    }

    private WpfShape CreatePenPreviewPath()
    {
        return new WpfPath
        {
            Stroke = new SolidColorBrush(_selectedColor),
            StrokeThickness = _strokeThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
    }

    private void UpdatePenPreviewPath()
    {
        if (_previewShape is not WpfPath path || _penPoints.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(_penPoints[0], isFilled: false, isClosed: false);
            for (var i = 1; i < _penPoints.Count; i++)
            {
                context.LineTo(_penPoints[i], isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        path.Data = geometry;
    }

    private Geometry CreateArrowPreviewGeometry(WpfPoint start, WpfPoint end)
    {
        var vector = start - end;
        if (vector.Length < 1)
        {
            return Geometry.Empty;
        }

        vector.Normalize();
        var headLength = Math.Max(10, _strokeThickness * 4.5);
        var headWidth = Math.Max(7, _strokeThickness * 2.8);
        var perpendicular = new Vector(-vector.Y, vector.X);
        var p1 = end + (vector * headLength) + (perpendicular * headWidth);
        var p2 = end + (vector * headLength) - (perpendicular * headWidth);
        var group = new GeometryGroup();
        group.Children.Add(new LineGeometry(start, end));

        var head = new StreamGeometry();
        using (var context = head.Open())
        {
            context.BeginFigure(end, isFilled: true, isClosed: true);
            context.LineTo(p1, isStroked: true, isSmoothJoin: true);
            context.LineTo(p2, isStroked: true, isSmoothJoin: true);
        }

        head.Freeze();
        group.Children.Add(head);
        group.Freeze();
        return group;
    }

    private void RemovePreviewShape()
    {
        if (_previewShape is not null)
        {
            AnnotationOverlay.Children.Remove(_previewShape);
            _previewShape = null;
        }
    }

    private WpfPoint ClampToSurface(WpfPoint point)
    {
        var width = Math.Max(1, AnnotationOverlay.ActualWidth);
        var height = Math.Max(1, AnnotationOverlay.ActualHeight);
        return new WpfPoint(Math.Clamp(point.X, 0, width), Math.Clamp(point.Y, 0, height));
    }

    private WpfPoint DisplayToPixel(WpfPoint point)
    {
        if (_editableImage is null)
        {
            return new WpfPoint();
        }

        var displayWidth = Math.Max(1, AnnotationOverlay.ActualWidth);
        var displayHeight = Math.Max(1, AnnotationOverlay.ActualHeight);
        return new WpfPoint(
            Math.Clamp(point.X / displayWidth * _editableImage.PixelWidth, 0, _editableImage.PixelWidth - 1),
            Math.Clamp(point.Y / displayHeight * _editableImage.PixelHeight, 0, _editableImage.PixelHeight - 1));
    }

    private Int32Rect GetPixelRect(WpfPoint startPoint, WpfPoint endPoint)
    {
        if (_editableImage is null)
        {
            return new Int32Rect();
        }

        var start = DisplayToPixel(startPoint);
        var end = DisplayToPixel(endPoint);
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        var x = (int)Math.Floor(left);
        var y = (int)Math.Floor(top);
        var width = Math.Max(1, (int)Math.Ceiling(right) - x);
        var height = Math.Max(1, (int)Math.Ceiling(bottom) - y);

        x = Math.Clamp(x, 0, _editableImage.PixelWidth - 1);
        y = Math.Clamp(y, 0, _editableImage.PixelHeight - 1);
        width = Math.Clamp(width, 1, _editableImage.PixelWidth - x);
        height = Math.Clamp(height, 1, _editableImage.PixelHeight - y);
        return new Int32Rect(x, y, width, height);
    }

    private void SetZoom(double zoom)
    {
        _fitMode = false;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ApplyZoom();
    }

    private void FitToStage()
    {
        if (_editableImage is not BitmapSource bitmap)
        {
            return;
        }

        var viewportWidth = ImageScrollViewer.ViewportWidth;
        var viewportHeight = ImageScrollViewer.ViewportHeight;
        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            viewportWidth = ImageScrollViewer.ActualWidth;
        }

        if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
        {
            viewportHeight = ImageScrollViewer.ActualHeight;
        }

        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            _zoom = 1.0;
        }
        else
        {
            var zoomX = Math.Max(1, viewportWidth - 28) / bitmap.PixelWidth;
            var zoomY = Math.Max(1, viewportHeight - 28) / bitmap.PixelHeight;
            _zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);
        }

        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_editableImage is not BitmapSource bitmap)
        {
            ZoomText.Text = "-";
            return;
        }

        var width = Math.Max(1, bitmap.PixelWidth * _zoom);
        var height = Math.Max(1, bitmap.PixelHeight * _zoom);
        ImageSurface.Width = width;
        ImageSurface.Height = height;
        PreviewImage.Width = width;
        PreviewImage.Height = height;
        AnnotationOverlay.Width = width;
        AnnotationOverlay.Height = height;
        var zoomText = $"{_zoom * 100:0}%";
        ZoomText.Text = zoomText;
        StatusZoomText.Text = zoomText;
    }

    private void OnImageStageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitMode)
        {
            FitToStage();
        }
    }

    private void OnImagePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        SetZoom(e.Delta > 0 ? _zoom * ZoomStep : _zoom / ZoomStep);
        e.Handled = true;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (IsTextInputFocused())
        {
            if (control && key == Key.S)
            {
                OnSaveClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }

            return;
        }

        if (control)
        {
            switch (key)
            {
                case Key.S:
                    OnSaveClick(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                case Key.Z:
                    UndoEdit();
                    e.Handled = true;
                    return;
                case Key.Y:
                    RedoEdit();
                    e.Handled = true;
                    return;
                case Key.L:
                    RotateImage(-90);
                    e.Handled = true;
                    return;
                case Key.R:
                    RotateImage(90);
                    e.Handled = true;
                    return;
            }
        }

        switch (key)
        {
            case Key.Left:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                SetZoom(_zoom * ZoomStep);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                SetZoom(_zoom / ZoomStep);
                e.Handled = true;
                break;
            case Key.D1:
                SetZoom(1.0);
                e.Handled = true;
                break;
            case Key.F:
                _fitMode = true;
                FitToStage();
                e.Handled = true;
                break;
            case Key.Space:
                _spacePanActive = true;
                UpdateToolButtons();
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Space && _spacePanActive)
        {
            _spacePanActive = false;
            UpdateToolButtons();
            e.Handled = true;
        }
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    }

    private void UpdateNavigationButtons()
    {
        var index = FileList.SelectedIndex;
        PreviousButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index >= 0 && index < _files.Count - 1;

        if (_currentFile is null)
        {
            return;
        }

        var position = index >= 0 ? index + 1 : 0;
        StatusText.Text = _currentFile.Path;
        UpdateStatusMetadata(position);
    }

    private void UpdateStatusMetadata(int position = 0)
    {
        if (_currentFile is null || _editableImage is null)
        {
            StatusMetaText.Text = string.Empty;
            return;
        }

        var format = IOPath.GetExtension(_currentFile.Path).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(format))
        {
            format = "IMG";
        }

        var positionText = position > 0 && _files.Count > 0
            ? $" | {position}/{_files.Count}"
            : string.Empty;
        StatusMetaText.Text = $"{_editableImage.PixelWidth:0}x{_editableImage.PixelHeight:0} | {format} | {_currentFile.SizeText}{positionText}";
    }

    private void UpdateDirtyIndicator()
    {
        var fileName = _currentFile?.FileName ?? IOPath.GetFileName(_lastSavedPath) ?? string.Empty;
        var prefix = _isDirty ? "* " : string.Empty;
        CurrentFileText.Text = $"{prefix}{fileName}";
        Title = string.IsNullOrWhiteSpace(fileName)
            ? "Mini Capture Viewer"
            : $"{prefix}{fileName} - Mini Capture Viewer";
        SaveStateText.Text = _isDirty ? "변경됨" : "저장됨";
        SaveStateText.Foreground = _isDirty
            ? new SolidColorBrush(WpfColor.FromRgb(250, 204, 21))
            : new SolidColorBrush(WpfColor.FromRgb(94, 234, 212));
    }

    private void UpdateEditButtons()
    {
        if (UndoButton is not null)
        {
            UndoButton.IsEnabled = _undoImages.Count > 0;
        }

        if (RedoButton is not null)
        {
            RedoButton.IsEnabled = _redoImages.Count > 0;
        }
    }

    private void UpdateViewButtons()
    {
        SetViewButtonState(DetailsViewButton, _viewMode == ExplorerViewMode.Details);
        SetViewButtonState(SmallViewButton, _viewMode == ExplorerViewMode.SmallIcons);
        SetViewButtonState(MediumViewButton, _viewMode == ExplorerViewMode.MediumIcons);
        SetViewButtonState(LargeViewButton, _viewMode == ExplorerViewMode.LargeIcons);
    }

    private void UpdateToolButtons()
    {
        SetViewButtonState(PanToolButton, _editTool == ViewerEditTool.Pan || _spacePanActive);
        SetViewButtonState(SelectToolButton, _editTool == ViewerEditTool.Select && !_spacePanActive);
        SetViewButtonState(RectangleToolButton, _editTool == ViewerEditTool.Rectangle);
        SetViewButtonState(EllipseToolButton, _editTool == ViewerEditTool.Ellipse);
        SetViewButtonState(MosaicToolButton, _editTool == ViewerEditTool.Mosaic);
        SetViewButtonState(TextToolButton, _editTool == ViewerEditTool.Text);
        SetViewButtonState(PenToolButton, _editTool == ViewerEditTool.Pen);
        SetViewButtonState(ArrowToolButton, _editTool == ViewerEditTool.Arrow);
        AnnotationOverlay.Cursor = _editTool switch
        {
            ViewerEditTool.Pan => WpfCursors.SizeAll,
            ViewerEditTool.Select when !_spacePanActive => WpfCursors.Arrow,
            _ when _spacePanActive => WpfCursors.SizeAll,
            _ => WpfCursors.Cross
        };
    }

    private void UpdateColorSwatches()
    {
        if (ColorSwatches is null)
        {
            return;
        }

        foreach (var child in ColorSwatches.Children.OfType<System.Windows.Controls.Button>())
        {
            var selected = child.Tag is string colorText &&
                System.Windows.Media.ColorConverter.ConvertFromString(colorText) is WpfColor color &&
                color.Equals(_selectedColor);
            child.BorderBrush = selected
                ? new SolidColorBrush(WpfColor.FromRgb(0, 120, 212))
                : new SolidColorBrush(WpfColor.FromRgb(64, 71, 82));
            child.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            child.Padding = selected ? new Thickness(1) : new Thickness(2);
        }
    }

    private void SetViewerStatus(string message)
    {
        StatusText.Text = message;
        AutomationProperties.SetName(StatusText, message);
    }

    private static void SetViewButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? new SolidColorBrush(WpfColor.FromRgb(0, 120, 212))
            : new SolidColorBrush(WpfColor.FromRgb(36, 40, 42));
        button.Foreground = selected ? WpfBrushes.White : new SolidColorBrush(WpfColor.FromRgb(226, 226, 226));
        button.BorderBrush = selected
            ? new SolidColorBrush(WpfColor.FromRgb(94, 175, 255))
            : new SolidColorBrush(WpfColor.FromRgb(64, 71, 82));
        button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
    }

    private void SelectFolderPath(string folderPath)
    {
        ClearFolderSelection(_folderNodes);
        _ = SelectFolderPath(_folderNodes, folderPath);
    }

    private static void ClearFolderSelection(IEnumerable<FolderTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            ClearFolderSelection(node.Children);
        }
    }

    private static bool SelectFolderPath(IEnumerable<FolderTreeNode> nodes, string folderPath)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Path) &&
                string.Equals(node.Path, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                node.IsSelected = true;
                return true;
            }

            if (SelectFolderPath(node.Children, folderPath))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }
}
