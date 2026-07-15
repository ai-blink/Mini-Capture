using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

internal enum ViewerAnnotationKind
{
    Rectangle,
    Ellipse,
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
    private const int FilePopulateBatchSize = 160;
    internal const int MaxIconViewFiles = 300;
    private const double MinStoredViewerWidth = 980;
    private const double MinStoredViewerHeight = 580;
    private const double MaxStoredPanelWidth = 1200;
    private const int MaxStoredImageViewStates = 200;

    private sealed record ViewerIndexSnapshot(
        ObservableCollection<FolderTreeNode> FolderNodes,
        string? TargetPath,
        string TargetFolder);

    private sealed class EditableAnnotation
    {
        public ViewerAnnotationKind Kind { get; init; }

        public WpfColor Color { get; init; }

        public double StrokeThickness { get; init; }

        public WpfRect PixelBounds { get; set; }

        public List<WpfPoint> PixelPoints { get; } = new();

        public string Text { get; set; } = string.Empty;

        public FrameworkElement? Element { get; set; }
    }

    private readonly ObservableCollection<CaptureImageFile> _files = new();
    private readonly List<EditableAnnotation> _annotations = new();
    private readonly List<WpfPoint> _penPoints = new();
    private readonly Stack<BitmapSource> _redoImages = new();
    private readonly Stack<BitmapSource> _undoImages = new();
    private readonly DispatcherTimer _viewStateSaveTimer;
    private CancellationTokenSource? _imageLoadCancellation;
    private CancellationTokenSource? _loadCancellation;
    private ObservableCollection<FolderTreeNode> _folderNodes = new();
    private BitmapSource? _editableImage;
    private WpfShape? _previewShape;
    private WpfRectangle? _resizeHandle;
    private WpfRectangle? _selectionFrame;
    private EditableAnnotation? _selectedAnnotation;
    private string? _pendingPath;
    private string? _currentFolderPath;
    private string? _lastSavedPath;
    private CaptureImageFile? _currentFile;
    private ViewerEditTool _editTool = ViewerEditTool.Pan;
    private bool _fitMode = true;
    private bool _isDirty;
    private bool _isDrawing;
    private bool _isExternalFolder;
    private bool _isLoadingSelection;
    private bool _isSelectingFolder;
    private bool _isPanning;
    private bool _isResizingAnnotation;
    private bool _isMovingAnnotation;
    private bool _isRestoringImageViewState;
    private bool _isUpdatingZoomSlider;
    private bool _spacePanActive;
    private int _activeFolderFileCount;
    private WpfPoint _annotationDragStartPoint;
    private WpfRect _annotationStartBounds;
    private List<WpfPoint> _annotationStartPoints = new();
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
        _viewStateSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _viewStateSaveTimer.Tick += OnViewStateSaveTimerTick;
        ApplyStoredViewerLayout();
        UpdateToolButtons();
        UpdateColorSwatches();
    }

    public void OpenImage(string? imagePath)
    {
        _pendingPath = imagePath;
        _ = RefreshIndexAsync();
        Activate();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshIndexAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        FlushCurrentImageViewState();
        SaveViewerLayout();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewStateSaveTimer.Stop();
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _imageLoadCancellation?.Cancel();
        _imageLoadCancellation?.Dispose();
        _imageLoadCancellation = null;
        base.OnClosed(e);
    }

    private async Task RefreshIndexAsync()
    {
        var load = StartNewLoad();
        var stopwatch = Stopwatch.StartNew();
        var requestedPath = _pendingPath;
        _pendingPath = null;
        SetViewerStatus("캡처 라이브러리를 읽는 중입니다.");

        try
        {
            var snapshot = await Task.Run(() => BuildIndexSnapshot(requestedPath), load.Token);
            if (!IsCurrentLoad(load))
            {
                return;
            }

            _folderNodes = snapshot.FolderNodes;
            FolderTree.ItemsSource = _folderNodes;
            SelectFolderPath(snapshot.TargetFolder);
            await LoadFolderAsync(snapshot.TargetFolder, snapshot.TargetPath, load, stopwatch);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
        }
    }

    private static ViewerIndexSnapshot BuildIndexSnapshot(string? requestedPath)
    {
        var targetPath = ResolveTargetPath(requestedPath);
        var targetFolder = GetTargetFolder(targetPath);
        CaptureFileIndex.TryCreateImageFile(targetPath, out var knownLatest);
        var folderNodes = CaptureFileIndex.BuildFolderTree(targetFolder, knownLatest);
        return new ViewerIndexSnapshot(folderNodes, targetPath, targetFolder);
    }

    private static string? ResolveTargetPath(string? requestedPath)
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
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                try
                {
                    return IOPath.GetFullPath(folder);
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        return CaptureFileIndex.RootDirectory;
    }

    private async Task LoadFolderAsync(
        string folderPath,
        string? preferredPath,
        CancellationTokenSource load,
        Stopwatch? existingStopwatch = null)
    {
        var stopwatch = existingStopwatch ?? Stopwatch.StartNew();
        _currentFolderPath = folderPath;
        SetViewerStatus("폴더를 읽는 중입니다.");
        FileCountText.Text = "읽는 중...";

        IReadOnlyList<CaptureImageFile> files;
        try
        {
            files = await Task.Run(() => CaptureFileIndex.GetImages(folderPath), load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
            return;
        }

        if (!IsCurrentLoad(load))
        {
            return;
        }

        _activeFolderFileCount = files.Count;
        _isExternalFolder = !CaptureFileIndex.IsUnderRoot(folderPath);
        _files.Clear();
        AddressText.Text = folderPath;
        FolderTreeScopeText.Text = CaptureFileIndex.IsUnderRoot(folderPath) ? "캡처 루트" : "전체 경로";

        var switchedToDetailsForLargeFolder = RequiresDetailsView(_activeFolderFileCount) &&
            _viewMode != ExplorerViewMode.Details;
        if (switchedToDetailsForLargeFolder)
        {
            SetViewMode(ExplorerViewMode.Details);
        }

        var preferredIsInFolder = false;
        if (CaptureFileIndex.TryCreateImageFile(preferredPath, out var preferredFile) && preferredFile is not null)
        {
            preferredIsInFolder = string.Equals(preferredFile.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase);
            LoadImage(preferredFile);
        }

        try
        {
            await PopulateFilesAsync(files, preferredPath, load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
            return;
        }
        if (!IsCurrentLoad(load))
        {
            return;
        }

        FileCountText.Text = $"{_files.Count:0}개";

        var selected = preferredIsInFolder
            ? FindFile(preferredPath) ?? FindFile(_currentFile?.Path) ?? _files.FirstOrDefault()
            : FindFile(_currentFile?.Path);

        if (selected is not null)
        {
            SelectFile(selected);
        }
        else if (_currentFile is null)
        {
            SelectFile(_files.FirstOrDefault());
        }
        else
        {
            _isLoadingSelection = true;
            FileList.SelectedItem = null;
            _isLoadingSelection = false;
            UpdateNavigationButtons();
        }

        if (_currentFile is null)
        {
            ClearImage("선택한 폴더에 이미지가 없습니다.");
        }

        var largeFolderSuffix = switchedToDetailsForLargeFolder
            ? _isExternalFolder
                ? " · 외부 폴더는 응답성을 위해 세부 정보 보기로 표시합니다."
                : " · 대용량 폴더는 세부 정보 보기로 표시합니다."
            : string.Empty;
        SetViewerStatus($"폴더 로딩 완료: {_files.Count:0}개, {stopwatch.ElapsedMilliseconds:0}ms{largeFolderSuffix}");
    }

    private async Task PopulateFilesAsync(
        IReadOnlyList<CaptureImageFile> files,
        string? preferredPath,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Add(files[index]);

            if ((index + 1) % FilePopulateBatchSize == 0)
            {
                FileCountText.Text = $"{index + 1:0}/{files.Count:0}개";
                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredPath) && FindFile(preferredPath) is { } preferredFile)
        {
            _isLoadingSelection = true;
            FileList.SelectedItem = preferredFile;
            FileList.ScrollIntoView(preferredFile);
            _isLoadingSelection = false;
        }
    }

    private CancellationTokenSource StartNewLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        return _loadCancellation;
    }

    private CancellationTokenSource StartNewImageLoad()
    {
        CancelImageLoad();
        _imageLoadCancellation = new CancellationTokenSource();
        return _imageLoadCancellation;
    }

    private void CancelImageLoad()
    {
        _imageLoadCancellation?.Cancel();
    }

    private bool IsCurrentImageLoad(CancellationTokenSource load)
    {
        return ReferenceEquals(_imageLoadCancellation, load) && !load.IsCancellationRequested;
    }

    private bool IsCurrentLoad(CancellationTokenSource load)
    {
        return ReferenceEquals(_loadCancellation, load) && !load.IsCancellationRequested;
    }

    private CaptureImageFile? FindFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _files.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsSameFolderSelection(string? currentFolderPath, string selectedFolderPath)
    {
        return !string.IsNullOrWhiteSpace(currentFolderPath) &&
            string.Equals(currentFolderPath, selectedFolderPath, StringComparison.OrdinalIgnoreCase);
    }

    private void MoveFileToSortedPosition(CaptureImageFile file)
    {
        var oldIndex = _files.IndexOf(file);
        if (oldIndex < 0)
        {
            return;
        }

        _files.RemoveAt(oldIndex);
        _files.Insert(GetSortedFileInsertIndex(file), file);
    }

    private int GetSortedFileInsertIndex(CaptureImageFile file)
    {
        for (var index = 0; index < _files.Count; index++)
        {
            var current = _files[index];
            if (file.LastWriteTime > current.LastWriteTime)
            {
                return index;
            }

            if (file.LastWriteTime == current.LastWriteTime &&
                string.Compare(file.FileName, current.FileName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return index;
            }
        }

        return _files.Count;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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
        _ = LoadImageAsync(file);
    }

    private async Task LoadImageAsync(CaptureImageFile? file)
    {
        if (file is null)
        {
            CancelImageLoad();
            ClearImage("표시할 이미지가 없습니다.");
            return;
        }

        var load = StartNewImageLoad();

        if (_currentFile is not null &&
            !string.Equals(_currentFile.Path, file.Path, StringComparison.OrdinalIgnoreCase))
        {
            FlushCurrentImageViewState();
        }

        SetViewerStatus("이미지를 불러오는 중입니다.");

        BitmapSource image;
        try
        {
            image = await Task.Run(() => DecodeImageFile(file.Path), load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            if (IsCurrentImageLoad(load))
            {
                ClearImage($"이미지를 열 수 없습니다: {ex.Message}");
            }

            return;
        }

        if (!IsCurrentImageLoad(load))
        {
            return;
        }

        _currentFile = file;
        _lastSavedPath = file.Path;
        _editableImage = image;
        ClearAnnotations();
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

        RestoreImageViewState(file.Path);
        UpdateNavigationButtons();
    }

    internal static BitmapSource DecodeImageFile(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ClearImage(string message)
    {
        FlushCurrentImageViewState();
        _currentFile = null;
        _lastSavedPath = null;
        _editableImage = null;
        ClearAnnotations();
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
        UpdateZoomSlider(null);
        UpdateNavigationButtons();
        UpdateEditButtons();
    }

    private void OnFolderTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_isSelectingFolder)
        {
            return;
        }

        if (e.NewValue is not FolderTreeNode { Path: { } path })
        {
            return;
        }

        if (IsSameFolderSelection(_currentFolderPath, path))
        {
            return;
        }

        if (!CaptureFileIndex.IsUnderRoot(path))
        {
            _ = NavigateToExternalFolderAsync(path);
            return;
        }

        var load = StartNewLoad();
        _ = LoadFolderAsync(path, null, load);
    }

    private async Task NavigateToExternalFolderAsync(string folderPath)
    {
        var load = StartNewLoad();
        var stopwatch = Stopwatch.StartNew();
        SetViewerStatus("외부 이미지 폴더를 읽는 중입니다.");

        try
        {
            var folderNodes = await Task.Run(() => CaptureFileIndex.BuildFolderTree(folderPath), load.Token);
            if (!IsCurrentLoad(load))
            {
                return;
            }

            _folderNodes = folderNodes;
            FolderTree.ItemsSource = _folderNodes;
            SelectFolderPath(folderPath);
            await LoadFolderAsync(folderPath, null, load, stopwatch);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
        }
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
        ScheduleCurrentImageViewStateSave();
    }

    private void OnActualSizeClick(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _pendingPath = _currentFile?.Path;
        _ = RefreshIndexAsync();
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ExplorerViewMode>(tag, out var mode))
        {
            return;
        }

        if (mode != ExplorerViewMode.Details && RequiresDetailsView(_activeFolderFileCount))
        {
            var scope = _isExternalFolder
                ? "외부 폴더"
                : $"{_activeFolderFileCount:0}개 파일";
            SetViewerStatus($"{scope}은(는) 세부 정보 보기에서만 표시합니다. 아이콘 보기는 응답성을 위해 제한됩니다.");
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
            var exportedImage = ComposeImageForExport();
            SavePng(exportedImage, targetPath);
            ApplySavedFileState(targetPath);
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

    private void ApplySavedFileState(string targetPath)
    {
        if (!CaptureFileIndex.TryCreateImageFile(targetPath, out var savedFile) || savedFile is null)
        {
            return;
        }

        var fileToSelect = FindFile(savedFile.Path);
        var matchesCurrentFile = PathsEqual(_currentFile?.Path, savedFile.Path);

        if (fileToSelect is not null)
        {
            fileToSelect.RefreshFromDisk();
            _isLoadingSelection = true;
            MoveFileToSortedPosition(fileToSelect);
            FileList.SelectedItem = fileToSelect;
            FileList.ScrollIntoView(fileToSelect);
            _isLoadingSelection = false;
            _currentFile = fileToSelect;
        }
        else if (IsSameFolderSelection(_currentFolderPath, savedFile.FolderPath))
        {
            fileToSelect = savedFile;
            _isLoadingSelection = true;
            _files.Insert(GetSortedFileInsertIndex(fileToSelect), fileToSelect);
            FileList.SelectedItem = fileToSelect;
            FileList.ScrollIntoView(fileToSelect);
            _isLoadingSelection = false;
            _currentFile = fileToSelect;
            FileCountText.Text = $"{_files.Count:0}개";
        }
        else if (matchesCurrentFile && _currentFile is not null)
        {
            _currentFile.RefreshFromDisk();
        }
        else
        {
            _isLoadingSelection = true;
            FileList.SelectedItem = null;
            _isLoadingSelection = false;
            _currentFile = savedFile;
        }

        _lastSavedPath = savedFile.Path;
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
            WpfClipboard.SetImage(ComposeImageForExport());
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

    private void OnDeleteAnnotationClick(object sender, RoutedEventArgs e)
    {
        DeleteSelectedAnnotation();
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
            SelectAnnotation(null);
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
            AddTextAnnotation(position);
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

        if (_isMovingAnnotation || _isResizingAnnotation)
        {
            UpdateSelectedAnnotationDrag(ClampToSurface(e.GetPosition(AnnotationOverlay)));
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
            ScheduleCurrentImageViewStateSave();
            e.Handled = true;
            return;
        }

        if (_isMovingAnnotation || _isResizingAnnotation)
        {
            _isMovingAnnotation = false;
            _isResizingAnnotation = false;
            AnnotationOverlay.ReleaseMouseCapture();
            MarkAnnotationChanged("선택 항목을 수정했습니다.");
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
            AddPenAnnotation(_penPoints);
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
                AddArrowAnnotation(_drawStartPoint, endPoint);
            }
            else
            {
                AddShapeAnnotation(rect, _editTool == ViewerEditTool.Ellipse);
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

    internal static bool RequiresDetailsView(int fileCount) =>
        fileCount > MaxIconViewFiles;

    private void ApplyStoredViewerLayout()
    {
        var settings = MiniCaptureSettingsStore.Load();
        ApplyStoredWindowBounds(settings);
        ApplyStoredExplorerLayout(settings);
        var viewMode = settings.ViewerExplorerViewMode;
        SetViewMode(IsDefinedViewMode(viewMode)
            ? viewMode.GetValueOrDefault()
            : ExplorerViewMode.Details);
    }

    private void ApplyStoredWindowBounds(MiniCaptureSettings settings)
    {
        if (settings.ViewerWidth is not { } width ||
            settings.ViewerHeight is not { } height ||
            settings.ViewerLeft is not { } left ||
            settings.ViewerTop is not { } top)
        {
            return;
        }

        width = Math.Max(MinStoredViewerWidth, width);
        height = Math.Max(MinStoredViewerHeight, height);
        var bounds = new WpfRect(left, top, width, height);
        if (!IntersectsVirtualScreen(bounds))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        if (string.Equals(settings.ViewerWindowState, nameof(WindowState.Maximized), StringComparison.Ordinal))
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void ApplyStoredExplorerLayout(MiniCaptureSettings settings)
    {
        if (settings.ViewerFolderTreeWidth is { } folderWidth)
        {
            FolderTreeColumn.Width = new GridLength(ClampPanelWidth(folderWidth, FolderTreeColumn.MinWidth));
        }

        if (settings.ViewerFileListWidth is { } fileListWidth)
        {
            FileListColumn.Width = new GridLength(ClampPanelWidth(fileListWidth, FileListColumn.MinWidth));
        }
    }

    private void SaveViewerLayout()
    {
        var settings = MiniCaptureSettingsStore.Load();
        var bounds = WindowState == WindowState.Normal ? new WpfRect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width >= MinStoredViewerWidth && bounds.Height >= MinStoredViewerHeight)
        {
            settings.ViewerLeft = bounds.Left;
            settings.ViewerTop = bounds.Top;
            settings.ViewerWidth = bounds.Width;
            settings.ViewerHeight = bounds.Height;
        }

        settings.ViewerWindowState = WindowState == WindowState.Maximized
            ? nameof(WindowState.Maximized)
            : nameof(WindowState.Normal);
        settings.ViewerExplorerViewMode = _viewMode;
        settings.ViewerFolderTreeWidth = FolderTreeColumn.ActualWidth > 0
            ? FolderTreeColumn.ActualWidth
            : FolderTreeColumn.Width.Value;
        settings.ViewerFileListWidth = FileListColumn.ActualWidth > 0
            ? FileListColumn.ActualWidth
            : FileListColumn.Width.Value;
        MiniCaptureSettingsStore.Save(settings);
    }

    private void OnViewStateSaveTimerTick(object? sender, EventArgs e)
    {
        _viewStateSaveTimer.Stop();
        SaveCurrentImageViewState();
    }

    private void FlushCurrentImageViewState()
    {
        if (_viewStateSaveTimer.IsEnabled)
        {
            _viewStateSaveTimer.Stop();
        }

        SaveCurrentImageViewState();
    }

    private void ScheduleCurrentImageViewStateSave()
    {
        if (_currentFile is null || _editableImage is null || _isRestoringImageViewState)
        {
            return;
        }

        _viewStateSaveTimer.Stop();
        _viewStateSaveTimer.Start();
    }

    private void SaveCurrentImageViewState()
    {
        if (_currentFile is null || _editableImage is null || _isRestoringImageViewState)
        {
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var states = settings.ViewerImageStates
            .Where(state => !string.IsNullOrWhiteSpace(state.Path))
            .Where(state => !string.Equals(state.Path, _currentFile.Path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        states.Insert(0, new ViewerImageViewState
        {
            Path = _currentFile.Path,
            Zoom = Math.Clamp(_zoom, MinZoom, MaxZoom),
            FitMode = _fitMode,
            HorizontalOffset = Math.Max(0, ImageScrollViewer.HorizontalOffset),
            VerticalOffset = Math.Max(0, ImageScrollViewer.VerticalOffset)
        });

        settings.ViewerImageStates = states
            .Take(MaxStoredImageViewStates)
            .ToList();
        MiniCaptureSettingsStore.Save(settings, notify: false);
    }

    private void RestoreImageViewState(string path)
    {
        var state = MiniCaptureSettingsStore.Load().ViewerImageStates
            .FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (state is null)
        {
            _fitMode = true;
            FitToStage();
            return;
        }

        _isRestoringImageViewState = true;
        try
        {
            _fitMode = state.FitMode;
            if (_fitMode)
            {
                FitToStage();
            }
            else
            {
                _zoom = Math.Clamp(state.Zoom, MinZoom, MaxZoom);
                ApplyZoom();
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    try
                    {
                        ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, state.HorizontalOffset));
                        ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, state.VerticalOffset));
                    }
                    finally
                    {
                        _isRestoringImageViewState = false;
                    }
                }));
        }
        catch
        {
            _isRestoringImageViewState = false;
            throw;
        }
    }

    private static bool IsDefinedViewMode(ExplorerViewMode? mode) =>
        mode is { } value && Enum.IsDefined(value);

    private static double ClampPanelWidth(double width, double minWidth) =>
        Math.Clamp(width, minWidth, MaxStoredPanelWidth);

    private static bool IntersectsVirtualScreen(WpfRect bounds)
    {
        var virtualScreen = new WpfRect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        return virtualScreen.IntersectsWith(bounds);
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

        CommitAnnotationsToBitmap("회전 전 주석을 이미지에 적용했습니다.", pushUndo: true);
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

    private void AddShapeAnnotation(Int32Rect pixelRect, bool ellipse)
    {
        var annotation = new EditableAnnotation
        {
            Kind = ellipse ? ViewerAnnotationKind.Ellipse : ViewerAnnotationKind.Rectangle,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness(),
            PixelBounds = new WpfRect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height)
        };

        AddAnnotation(annotation, ellipse ? "동그라미를 추가했습니다." : "네모를 추가했습니다.");
    }

    private void AddTextAnnotation(WpfPoint displayPoint)
    {
        if (_editableImage is null)
        {
            return;
        }

        var pixelPoint = DisplayToPixel(displayPoint);
        var text = string.IsNullOrWhiteSpace(AnnotationTextBox.Text)
            ? "Text"
            : AnnotationTextBox.Text.Trim();
        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Text,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness(),
            PixelBounds = new WpfRect(
                pixelPoint.X,
                pixelPoint.Y,
                Math.Min(320, Math.Max(120, _editableImage.PixelWidth - pixelPoint.X)),
                56),
            Text = text
        };

        AddAnnotation(annotation, "텍스트 상자를 추가했습니다.");
        if (annotation.Element is System.Windows.Controls.TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void AddPenAnnotation(IReadOnlyList<WpfPoint> displayPoints)
    {
        if (_editableImage is null || displayPoints.Count < 2)
        {
            return;
        }

        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Pen,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness()
        };
        foreach (var point in displayPoints)
        {
            annotation.PixelPoints.Add(DisplayToPixel(point));
        }

        annotation.PixelBounds = GetPixelPointsBounds(annotation.PixelPoints);
        AddAnnotation(annotation, "펜 선을 추가했습니다.");
    }

    private void AddArrowAnnotation(WpfPoint displayStart, WpfPoint displayEnd)
    {
        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Arrow,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness()
        };
        annotation.PixelPoints.Add(DisplayToPixel(displayStart));
        annotation.PixelPoints.Add(DisplayToPixel(displayEnd));
        annotation.PixelBounds = GetPixelPointsBounds(annotation.PixelPoints);
        AddAnnotation(annotation, "화살표를 추가했습니다.");
    }

    private void AddAnnotation(EditableAnnotation annotation, string status)
    {
        _annotations.Add(annotation);
        RenderAnnotation(annotation);
        SelectAnnotation(annotation);
        MarkAnnotationChanged(status);
    }

    private void RenderAnnotations()
    {
        AnnotationOverlay.Children.Clear();
        foreach (var annotation in _annotations)
        {
            RenderAnnotation(annotation);
        }

        RenderSelectionFrame();
    }

    private void RenderAnnotation(EditableAnnotation annotation)
    {
        var element = CreateAnnotationElement(annotation);
        annotation.Element = element;
        element.Tag = annotation;
        element.PreviewMouseLeftButtonDown += OnAnnotationElementMouseLeftButtonDown;
        element.PreviewMouseMove += OnAnnotationElementMouseMove;
        element.PreviewMouseLeftButtonUp += OnAnnotationElementMouseLeftButtonUp;
        AnnotationOverlay.Children.Add(element);
        PositionAnnotationElement(annotation);
    }

    private FrameworkElement CreateAnnotationElement(EditableAnnotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        return annotation.Kind switch
        {
            ViewerAnnotationKind.Ellipse => new WpfEllipse
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                Fill = WpfBrushes.Transparent
            },
            ViewerAnnotationKind.Text => CreateAnnotationTextBox(annotation, brush),
            ViewerAnnotationKind.Pen => new WpfPath
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = WpfBrushes.Transparent,
                Data = CreateDisplayPenGeometry(annotation)
            },
            ViewerAnnotationKind.Arrow => new WpfPath
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                Fill = brush,
                Data = CreateDisplayArrowGeometry(annotation)
            },
            _ => new WpfRectangle
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                Fill = WpfBrushes.Transparent
            }
        };
    }

    private System.Windows.Controls.TextBox CreateAnnotationTextBox(EditableAnnotation annotation, System.Windows.Media.Brush brush)
    {
        var textBox = new System.Windows.Controls.TextBox
        {
            AcceptsReturn = true,
            Background = new SolidColorBrush(WpfColor.FromArgb(42, 0, 0, 0)),
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            Foreground = brush,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = Math.Clamp(annotation.StrokeThickness * 5 * _zoom, 12, 96),
            MinWidth = 48,
            MinHeight = 28,
            Padding = new Thickness(6, 3, 6, 3),
            Text = annotation.Text,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetAutomationId(textBox, "ViewerEditableTextBox");
        textBox.TextChanged += (_, _) =>
        {
            annotation.Text = textBox.Text;
            MarkAnnotationChanged("텍스트를 수정했습니다.");
        };
        textBox.GotKeyboardFocus += (_, _) => SelectAnnotation(annotation);
        return textBox;
    }

    private void PositionAnnotationElement(EditableAnnotation annotation)
    {
        if (annotation.Element is null)
        {
            return;
        }

        if (annotation.Kind is ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow)
        {
            Canvas.SetLeft(annotation.Element, 0);
            Canvas.SetTop(annotation.Element, 0);
            annotation.Element.Width = AnnotationOverlay.Width;
            annotation.Element.Height = AnnotationOverlay.Height;
            return;
        }

        var rect = PixelToDisplayRect(annotation.PixelBounds);
        Canvas.SetLeft(annotation.Element, rect.Left);
        Canvas.SetTop(annotation.Element, rect.Top);
        annotation.Element.Width = Math.Max(1, rect.Width);
        annotation.Element.Height = Math.Max(1, rect.Height);
    }

    private void SelectAnnotation(EditableAnnotation? annotation)
    {
        _selectedAnnotation = annotation;
        RenderSelectionFrame();
        UpdateEditButtons();
    }

    private void RenderSelectionFrame()
    {
        if (_selectionFrame is not null)
        {
            AnnotationOverlay.Children.Remove(_selectionFrame);
            _selectionFrame = null;
        }

        if (_resizeHandle is not null)
        {
            AnnotationOverlay.Children.Remove(_resizeHandle);
            _resizeHandle = null;
        }

        if (_selectedAnnotation is null)
        {
            return;
        }

        var rect = PixelToDisplayRect(GetAnnotationBounds(_selectedAnnotation));
        _selectionFrame = new WpfRectangle
        {
            Stroke = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = WpfBrushes.Transparent,
            IsHitTestVisible = false,
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height)
        };
        Canvas.SetLeft(_selectionFrame, rect.Left);
        Canvas.SetTop(_selectionFrame, rect.Top);
        AnnotationOverlay.Children.Add(_selectionFrame);

        _resizeHandle = new WpfRectangle
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212)),
            Stroke = WpfBrushes.White,
            StrokeThickness = 1,
            Cursor = WpfCursors.SizeNWSE
        };
        _resizeHandle.PreviewMouseLeftButtonDown += OnResizeHandleMouseLeftButtonDown;
        Canvas.SetLeft(_resizeHandle, rect.Right - 6);
        Canvas.SetTop(_resizeHandle, rect.Bottom - 6);
        AnnotationOverlay.Children.Add(_resizeHandle);
    }

    private void OnAnnotationElementMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: EditableAnnotation annotation })
        {
            return;
        }

        SelectAnnotation(annotation);
        if (_editTool != ViewerEditTool.Select || Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            return;
        }

        _isMovingAnnotation = true;
        _annotationDragStartPoint = ClampToSurface(e.GetPosition(AnnotationOverlay));
        _annotationStartBounds = GetAnnotationBounds(annotation);
        _annotationStartPoints = annotation.PixelPoints.ToList();
        AnnotationOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void OnAnnotationElementMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isMovingAnnotation && !_isResizingAnnotation)
        {
            return;
        }

        UpdateSelectedAnnotationDrag(ClampToSurface(e.GetPosition(AnnotationOverlay)));
        e.Handled = true;
    }

    private void OnAnnotationElementMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMovingAnnotation && !_isResizingAnnotation)
        {
            return;
        }

        _isMovingAnnotation = false;
        _isResizingAnnotation = false;
        AnnotationOverlay.ReleaseMouseCapture();
        MarkAnnotationChanged("선택 항목을 수정했습니다.");
        e.Handled = true;
    }

    private void OnResizeHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedAnnotation is null)
        {
            return;
        }

        _isResizingAnnotation = true;
        _annotationDragStartPoint = ClampToSurface(e.GetPosition(AnnotationOverlay));
        _annotationStartBounds = GetAnnotationBounds(_selectedAnnotation);
        _annotationStartPoints = _selectedAnnotation.PixelPoints.ToList();
        AnnotationOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void UpdateSelectedAnnotationDrag(WpfPoint displayPoint)
    {
        if (_selectedAnnotation is null || _editableImage is null)
        {
            return;
        }

        var startPixel = DisplayToPixel(_annotationDragStartPoint);
        var currentPixel = DisplayToPixel(displayPoint);
        var delta = currentPixel - startPixel;

        if (_isResizingAnnotation)
        {
            var newWidth = Math.Max(8, _annotationStartBounds.Width + delta.X);
            var newHeight = Math.Max(8, _annotationStartBounds.Height + delta.Y);
            _selectedAnnotation.PixelBounds = ClampPixelRect(new WpfRect(_annotationStartBounds.X, _annotationStartBounds.Y, newWidth, newHeight));
            ResizeAnnotationPoints(_selectedAnnotation, newWidth, newHeight);
        }
        else if (_isMovingAnnotation)
        {
            MoveAnnotation(_selectedAnnotation, delta);
        }

        RefreshAnnotationVisual(_selectedAnnotation);
        RenderSelectionFrame();
    }

    private void MoveAnnotation(EditableAnnotation annotation, Vector delta)
    {
        annotation.PixelBounds = ClampPixelRect(new WpfRect(
            _annotationStartBounds.X + delta.X,
            _annotationStartBounds.Y + delta.Y,
            _annotationStartBounds.Width,
            _annotationStartBounds.Height));

        if (annotation.Kind is ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow)
        {
            for (var i = 0; i < annotation.PixelPoints.Count; i++)
            {
                var startPoint = i < _annotationStartPoints.Count ? _annotationStartPoints[i] : annotation.PixelPoints[i];
                annotation.PixelPoints[i] = new WpfPoint(
                    Math.Clamp(startPoint.X + delta.X, 0, _editableImage?.PixelWidth ?? 1),
                    Math.Clamp(startPoint.Y + delta.Y, 0, _editableImage?.PixelHeight ?? 1));
            }
        }
    }

    private void ResizeAnnotationPoints(EditableAnnotation annotation, double newWidth, double newHeight)
    {
        if (annotation.Kind is not (ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow) || _annotationStartPoints.Count == 0)
        {
            return;
        }

        var scaleX = newWidth / Math.Max(1, _annotationStartBounds.Width);
        var scaleY = newHeight / Math.Max(1, _annotationStartBounds.Height);
        annotation.PixelPoints.Clear();
        foreach (var point in _annotationStartPoints)
        {
            annotation.PixelPoints.Add(new WpfPoint(
                _annotationStartBounds.X + ((point.X - _annotationStartBounds.X) * scaleX),
                _annotationStartBounds.Y + ((point.Y - _annotationStartBounds.Y) * scaleY)));
        }

        annotation.PixelBounds = GetPixelPointsBounds(annotation.PixelPoints);
    }

    private void RefreshAnnotationVisual(EditableAnnotation annotation)
    {
        if (annotation.Element is WpfPath path)
        {
            path.Data = annotation.Kind == ViewerAnnotationKind.Arrow
                ? CreateDisplayArrowGeometry(annotation)
                : CreateDisplayPenGeometry(annotation);
        }

        PositionAnnotationElement(annotation);
    }

    private void DeleteSelectedAnnotation()
    {
        if (_selectedAnnotation is null)
        {
            return;
        }

        if (_selectedAnnotation.Element is not null)
        {
            AnnotationOverlay.Children.Remove(_selectedAnnotation.Element);
        }

        _annotations.Remove(_selectedAnnotation);
        SelectAnnotation(null);
        MarkAnnotationChanged("선택 항목을 삭제했습니다.");
    }

    private void ClearAnnotations()
    {
        _annotations.Clear();
        SelectAnnotation(null);
        AnnotationOverlay.Children.Clear();
    }

    private void MarkAnnotationChanged(string status)
    {
        _isDirty = true;
        UpdateDirtyIndicator();
        UpdateEditButtons();
        SetViewerStatus(status);
    }

    private void CommitAnnotationsToBitmap(string status, bool pushUndo)
    {
        if (_editableImage is null || _annotations.Count == 0)
        {
            return;
        }

        if (pushUndo)
        {
            PushUndoSnapshot();
        }

        SetEditableImage(ComposeImageForExport(), status);
        ClearAnnotations();
    }

    private BitmapSource ComposeImageForExport()
    {
        if (_editableImage is null)
        {
            throw new InvalidOperationException("No image is loaded.");
        }

        var source = ConvertToPbgra32(_editableImage);
        if (_annotations.Count == 0)
        {
            return source;
        }

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new WpfRect(0, 0, source.PixelWidth, source.PixelHeight));
            foreach (var annotation in _annotations)
            {
                DrawAnnotation(drawing, annotation);
            }
        }

        return RenderBitmap(visual, source.PixelWidth, source.PixelHeight);
    }

    private void DrawAnnotation(DrawingContext drawing, EditableAnnotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        var pen = new WpfPen(brush, Math.Max(1, annotation.StrokeThickness))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        switch (annotation.Kind)
        {
            case ViewerAnnotationKind.Ellipse:
                drawing.DrawEllipse(null, pen, new WpfPoint(annotation.PixelBounds.X + annotation.PixelBounds.Width / 2, annotation.PixelBounds.Y + annotation.PixelBounds.Height / 2), annotation.PixelBounds.Width / 2, annotation.PixelBounds.Height / 2);
                break;
            case ViewerAnnotationKind.Text:
                var formatted = new FormattedText(
                    annotation.Text,
                    CultureInfo.CurrentCulture,
                    WpfFlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    Math.Clamp(annotation.StrokeThickness * 5, 12, 96),
                    brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = Math.Max(1, annotation.PixelBounds.Width)
                };
                drawing.DrawText(formatted, annotation.PixelBounds.TopLeft);
                break;
            case ViewerAnnotationKind.Pen:
                drawing.DrawGeometry(null, pen, CreatePixelPenGeometry(annotation));
                break;
            case ViewerAnnotationKind.Arrow:
                if (annotation.PixelPoints.Count >= 2)
                {
                    drawing.DrawLine(pen, annotation.PixelPoints[0], annotation.PixelPoints[1]);
                    DrawArrowHead(drawing, annotation.PixelPoints[0], annotation.PixelPoints[1], brush, annotation.StrokeThickness);
                }
                break;
            default:
                drawing.DrawRectangle(null, pen, annotation.PixelBounds);
                break;
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

        CommitAnnotationsToBitmap("모자이크 전 주석을 이미지에 적용했습니다.", pushUndo: true);
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
        DrawArrowHead(drawing, start, end, brush, GetPixelStrokeThickness());
    }

    private static void DrawArrowHead(DrawingContext drawing, WpfPoint start, WpfPoint end, System.Windows.Media.Brush brush, double strokeThickness)
    {
        var vector = start - end;
        if (vector.Length < 1)
        {
            return;
        }

        vector.Normalize();
        var headLength = Math.Max(10, strokeThickness * 4.5);
        var headWidth = Math.Max(7, strokeThickness * 2.8);
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

    private Geometry CreateDisplayArrowGeometry(EditableAnnotation annotation)
    {
        if (annotation.PixelPoints.Count < 2)
        {
            return Geometry.Empty;
        }

        return CreateArrowPreviewGeometry(PixelToDisplayPoint(annotation.PixelPoints[0]), PixelToDisplayPoint(annotation.PixelPoints[1]));
    }

    private Geometry CreateDisplayPenGeometry(EditableAnnotation annotation)
    {
        var geometry = new StreamGeometry();
        if (annotation.PixelPoints.Count < 2)
        {
            return geometry;
        }

        using (var context = geometry.Open())
        {
            context.BeginFigure(PixelToDisplayPoint(annotation.PixelPoints[0]), isFilled: false, isClosed: false);
            for (var i = 1; i < annotation.PixelPoints.Count; i++)
            {
                context.LineTo(PixelToDisplayPoint(annotation.PixelPoints[i]), isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Geometry CreatePixelPenGeometry(EditableAnnotation annotation)
    {
        var geometry = new StreamGeometry();
        if (annotation.PixelPoints.Count < 2)
        {
            return geometry;
        }

        using (var context = geometry.Open())
        {
            context.BeginFigure(annotation.PixelPoints[0], isFilled: false, isClosed: false);
            for (var i = 1; i < annotation.PixelPoints.Count; i++)
            {
                context.LineTo(annotation.PixelPoints[i], isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
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

    private WpfPoint PixelToDisplayPoint(WpfPoint point)
    {
        if (_editableImage is null)
        {
            return new WpfPoint();
        }

        var displayWidth = Math.Max(1, AnnotationOverlay.ActualWidth);
        var displayHeight = Math.Max(1, AnnotationOverlay.ActualHeight);
        return new WpfPoint(
            point.X / Math.Max(1, _editableImage.PixelWidth) * displayWidth,
            point.Y / Math.Max(1, _editableImage.PixelHeight) * displayHeight);
    }

    private WpfRect PixelToDisplayRect(WpfRect rect)
    {
        var topLeft = PixelToDisplayPoint(rect.TopLeft);
        var bottomRight = PixelToDisplayPoint(rect.BottomRight);
        return new WpfRect(topLeft, bottomRight);
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

    private WpfRect ClampPixelRect(WpfRect rect)
    {
        if (_editableImage is null)
        {
            return rect;
        }

        var x = Math.Clamp(rect.X, 0, Math.Max(0, _editableImage.PixelWidth - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, _editableImage.PixelHeight - 1));
        var width = Math.Clamp(rect.Width, 1, Math.Max(1, _editableImage.PixelWidth - x));
        var height = Math.Clamp(rect.Height, 1, Math.Max(1, _editableImage.PixelHeight - y));
        return new WpfRect(x, y, width, height);
    }

    private static WpfRect GetPixelPointsBounds(IReadOnlyList<WpfPoint> points)
    {
        if (points.Count == 0)
        {
            return WpfRect.Empty;
        }

        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new WpfRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static WpfRect GetAnnotationBounds(EditableAnnotation annotation)
    {
        return annotation.Kind is ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow
            ? GetPixelPointsBounds(annotation.PixelPoints)
            : annotation.PixelBounds;
    }

    private void SetZoom(double zoom)
    {
        _fitMode = false;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ApplyZoom();
        ScheduleCurrentImageViewStateSave();
    }

    private void OnZoomTextGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ZoomText.SelectAll();
    }

    private void OnZoomTextLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ApplyZoomTextInput();
    }

    private void OnZoomTextPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Enter)
        {
            ApplyZoomTextInput();
            ImageScrollViewer.Focus();
            e.Handled = true;
        }
        else if (key == Key.Escape)
        {
            RestoreZoomText();
            ImageScrollViewer.Focus();
            e.Handled = true;
        }
    }

    private bool ApplyZoomTextInput()
    {
        if (_editableImage is null)
        {
            ZoomText.Text = "-";
            return false;
        }

        var valueText = ZoomText.Text.Trim().TrimEnd('%').Trim();
        var parsed = double.TryParse(valueText, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) ||
            double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
        if (!parsed || !double.IsFinite(percent))
        {
            RestoreZoomText();
            SetViewerStatus("확대/축소 값은 10%~800% 사이의 숫자로 입력하세요.");
            return false;
        }

        SetZoom(percent / 100.0);
        return true;
    }

    private void RestoreZoomText()
    {
        ZoomText.Text = _editableImage is null
            ? "-"
            : $"{_zoom * 100:0}%";
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
        RenderAnnotations();
        var zoomText = $"{_zoom * 100:0}%";
        ZoomText.Text = zoomText;
        StatusZoomText.Text = zoomText;
        UpdateZoomSlider(_zoom);
    }

    private void UpdateZoomSlider(double? zoom)
    {
        if (StatusZoomSlider is null)
        {
            return;
        }

        _isUpdatingZoomSlider = true;
        try
        {
            StatusZoomSlider.IsEnabled = zoom.HasValue;
            StatusZoomSlider.Value = zoom.HasValue
                ? Math.Clamp(zoom.Value * 100, MinZoom * 100, MaxZoom * 100)
                : MinZoom * 100;
        }
        finally
        {
            _isUpdatingZoomSlider = false;
        }
    }

    private void OnImageStageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitMode)
        {
            FitToStage();
        }
    }

    private void OnImageScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.HorizontalChange) < 0.1 && Math.Abs(e.VerticalChange) < 0.1)
        {
            return;
        }

        ScheduleCurrentImageViewStateSave();
    }

    private void OnStatusZoomSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingZoomSlider || _editableImage is null)
        {
            return;
        }

        SetZoom(e.NewValue / 100.0);
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
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

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
                case Key.C:
                    if (shift)
                    {
                        OnCopyPathClick(sender, new RoutedEventArgs());
                    }
                    else
                    {
                        OnCopyImageClick(sender, new RoutedEventArgs());
                    }

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
            case Key.Delete:
                DeleteSelectedAnnotation();
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

        if (DeleteAnnotationButton is not null)
        {
            DeleteAnnotationButton.IsEnabled = _selectedAnnotation is not null;
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
        UpdateToolContext();
        AnnotationOverlay.Cursor = _editTool switch
        {
            ViewerEditTool.Pan => WpfCursors.SizeAll,
            ViewerEditTool.Select when !_spacePanActive => WpfCursors.Arrow,
            _ when _spacePanActive => WpfCursors.SizeAll,
            _ => WpfCursors.Cross
        };
    }

    private void UpdateToolContext()
    {
        var showsStroke = _editTool is ViewerEditTool.Pen or
            ViewerEditTool.Arrow or
            ViewerEditTool.Rectangle or
            ViewerEditTool.Ellipse;
        var showsText = _editTool == ViewerEditTool.Text;

        ContextToolbar.Visibility = showsStroke || showsText
            ? Visibility.Visible
            : Visibility.Collapsed;
        ColorContextGroup.Visibility = showsStroke || showsText
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrokeContextGroup.Visibility = showsStroke
            ? Visibility.Visible
            : Visibility.Collapsed;
        TextContextGroup.Visibility = showsText
            ? Visibility.Visible
            : Visibility.Collapsed;
        ContextToolName.Text = _editTool switch
        {
            ViewerEditTool.Pen => "펜 설정",
            ViewerEditTool.Arrow => "화살표 설정",
            ViewerEditTool.Rectangle => "네모 설정",
            ViewerEditTool.Ellipse => "원 설정",
            ViewerEditTool.Text => "텍스트 설정",
            _ => string.Empty
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
                ? new SolidColorBrush(WpfColor.FromRgb(25, 127, 120))
                : new SolidColorBrush(WpfColor.FromRgb(185, 196, 197));
            child.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            child.Padding = selected ? new Thickness(1) : new Thickness(2);
        }
    }

    private void SetViewerStatus(string message)
    {
        StatusText.Text = message;
        AutomationProperties.SetName(StatusText, message);
    }

    private void ShowFolderLoadFailure(Exception ex)
    {
        FileCountText.Text = "읽기 실패";
        SetViewerStatus($"폴더를 읽을 수 없습니다: {ex.Message}");
    }

    private static void SetViewButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? new SolidColorBrush(WpfColor.FromRgb(216, 238, 235))
            : WpfBrushes.Transparent;
        button.Foreground = selected
            ? new SolidColorBrush(WpfColor.FromRgb(25, 127, 120))
            : new SolidColorBrush(WpfColor.FromRgb(89, 104, 106));
        button.BorderBrush = selected
            ? new SolidColorBrush(WpfColor.FromRgb(25, 127, 120))
            : WpfBrushes.Transparent;
        button.BorderThickness = selected ? new Thickness(1, 1, 1, 2) : new Thickness(1);
    }

    private void SelectFolderPath(string folderPath)
    {
        _isSelectingFolder = true;
        try
        {
            ClearFolderSelection(_folderNodes);
            _ = SelectFolderPath(_folderNodes, folderPath);
        }
        finally
        {
            _isSelectingFolder = false;
        }
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
