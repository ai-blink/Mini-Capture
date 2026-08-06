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
    PixelSelect,
    Rectangle,
    Ellipse,
    Mosaic,
    Text,
    Pen,
    Arrow
}

internal enum ViewerMosaicType
{
    Block,
    Blur
}

internal enum ViewerAnnotationKind
{
    Rectangle,
    Ellipse,
    Text,
    Pen,
    Arrow
}

internal enum ViewerFileSortColumn
{
    Name,
    ModifiedDate,
    Type,
    Size
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
    private WpfPath? _cropPreviewMask;
    private WpfRectangle? _cropPreviewFrame;
    private WpfRectangle? _pixelSelectionFrame;
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
    private bool _isCropMode;
    private bool _isSelectingFolder;
    private bool _isUpdatingCropControls;
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
    private Int32Rect? _pixelSelection;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private WpfColor _selectedColor = WpfColor.FromRgb(239, 68, 68);
    private double _strokeThickness = 4.0;
    private int _mosaicBlockSize = 14;
    private ViewerMosaicType _mosaicType = ViewerMosaicType.Block;
    private bool _isApplyingMosaic;
    private double _zoom = 1.0;
    private ExplorerViewMode _viewMode = ExplorerViewMode.Details;
    private ViewerFileSortColumn _fileSortColumn = ViewerFileSortColumn.ModifiedDate;
    private ListSortDirection _fileSortDirection = ListSortDirection.Descending;

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

    private static ViewerIndexSnapshot BuildFolderSnapshot(string folderPath, string? preferredPath)
    {
        var targetFolder = Directory.Exists(folderPath)
            ? IOPath.GetFullPath(folderPath)
            : CaptureFileIndex.RootDirectory;
        CaptureFileIndex.TryCreateImageFile(preferredPath, out var preferredFile);
        var folderNodes = CaptureFileIndex.BuildFolderTree(targetFolder, preferredFile);
        return new ViewerIndexSnapshot(folderNodes, preferredFile?.Path, targetFolder);
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
        var requestedSortColumn = _fileSortColumn;
        var requestedSortDirection = _fileSortDirection;
        try
        {
            files = await Task.Run(
                () => SortFiles(
                    CaptureFileIndex.GetImages(folderPath),
                    requestedSortColumn,
                    requestedSortDirection),
                load.Token);
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

        if (requestedSortColumn != _fileSortColumn || requestedSortDirection != _fileSortDirection)
        {
            ApplyFileSort();
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

    internal static bool IsSameFolderSelection(string? currentFolderPath, string? selectedFolderPath)
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
            if (CompareFiles(file, _files[index], _fileSortColumn, _fileSortDirection) < 0)
            {
                return index;
            }
        }

        return _files.Count;
    }

    internal static IReadOnlyList<CaptureImageFile> SortFiles(
        IEnumerable<CaptureImageFile> files,
        ViewerFileSortColumn sortColumn,
        ListSortDirection sortDirection)
    {
        var sorted = files.ToList();
        sorted.Sort((left, right) => CompareFiles(left, right, sortColumn, sortDirection));
        return sorted;
    }

    internal static int CompareFiles(
        CaptureImageFile left,
        CaptureImageFile right,
        ViewerFileSortColumn sortColumn,
        ListSortDirection sortDirection)
    {
        var comparison = sortColumn switch
        {
            ViewerFileSortColumn.Name => StringComparer.CurrentCultureIgnoreCase.Compare(left.FileName, right.FileName),
            ViewerFileSortColumn.ModifiedDate => left.LastWriteTime.CompareTo(right.LastWriteTime),
            ViewerFileSortColumn.Type => StringComparer.CurrentCultureIgnoreCase.Compare(left.Extension, right.Extension),
            ViewerFileSortColumn.Size => left.Length.CompareTo(right.Length),
            _ => 0
        };

        if (sortDirection == ListSortDirection.Descending)
        {
            comparison = comparison switch
            {
                < 0 => 1,
                > 0 => -1,
                _ => 0
            };
        }

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.FileName, right.FileName);
        return comparison != 0
            ? comparison
            : StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path);
    }

    private void ApplyFileSort()
    {
        var selectedPath = (FileList.SelectedItem as CaptureImageFile)?.Path;
        var sorted = SortFiles(_files, _fileSortColumn, _fileSortDirection);

        if (!sorted.SequenceEqual(_files))
        {
            _isLoadingSelection = true;
            _files.Clear();
            foreach (var file in sorted)
            {
                _files.Add(file);
            }

            FileList.SelectedItem = FindFile(selectedPath);
            if (FileList.SelectedItem is not null)
            {
                FileList.ScrollIntoView(FileList.SelectedItem);
            }

            _isLoadingSelection = false;
        }

        UpdateDetailsSortHeaders();
        UpdateNavigationButtons();
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
        _pixelSelection = null;
        CloseCropMode();
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
        _pixelSelection = null;
        CloseCropMode();
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

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshCurrentFolderAsync();
    }

    private async Task RefreshCurrentFolderAsync()
    {
        var folderPath = _currentFolderPath;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            await RefreshIndexAsync();
            return;
        }

        var load = StartNewLoad();
        var stopwatch = Stopwatch.StartNew();
        var preferredPath = (FileList.SelectedItem as CaptureImageFile)?.Path;
        if (string.IsNullOrWhiteSpace(preferredPath) &&
            IsSameFolderSelection(folderPath, _currentFile?.FolderPath))
        {
            preferredPath = _currentFile?.Path;
        }

        SetViewerStatus("현재 폴더 파일 목록을 새로고침하는 중입니다.");

        try
        {
            var snapshot = await Task.Run(
                () => BuildFolderSnapshot(folderPath, preferredPath),
                load.Token);
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

    private void OnCopySelectionClick(object sender, RoutedEventArgs e)
    {
        CopyPixelSelection();
    }

    private void OnCutSelectionClick(object sender, RoutedEventArgs e)
    {
        CutPixelSelection();
    }

    private void OnPasteSelectionClick(object sender, RoutedEventArgs e)
    {
        PasteIntoPixelSelection();
    }

    private void CopyPixelSelection()
    {
        if (!TryGetPixelSelection(out var selection))
        {
            SetViewerStatus("먼저 복사할 영역을 선택하세요.");
            return;
        }

        try
        {
            CopyPixelSelectionToClipboard(selection);
            SetViewerStatus($"선택 영역을 클립보드에 복사했습니다: {selection.Width} × {selection.Height}px");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"선택 영역 복사 실패: {ex.Message}");
        }
    }

    private void CutPixelSelection()
    {
        if (!TryGetPixelSelection(out var selection))
        {
            SetViewerStatus("먼저 잘라낼 영역을 선택하세요.");
            return;
        }

        try
        {
            CopyPixelSelectionToClipboard(selection);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"선택 영역 복사 실패: {ex.Message}");
            return;
        }

        CommitAnnotationsToBitmap("잘라내기 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        SetEditableImage(ClearPixelRectangle(_editableImage, selection), "선택 영역을 잘라냈습니다.");
    }

    private void PasteIntoPixelSelection()
    {
        if (_editableImage is null)
        {
            SetViewerStatus("붙여넣을 이미지가 없습니다.");
            return;
        }

        BitmapSource? clipboardImage;
        try
        {
            clipboardImage = WpfClipboard.ContainsImage() ? WpfClipboard.GetImage() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"클립보드를 읽을 수 없습니다: {ex.Message}");
            return;
        }

        if (clipboardImage is null || clipboardImage.PixelWidth < 1 || clipboardImage.PixelHeight < 1)
        {
            SetViewerStatus("클립보드에 붙여넣을 이미지가 없습니다.");
            return;
        }

        var target = GetPasteTarget(clipboardImage);
        if (target.Width < 1 || target.Height < 1)
        {
            SetViewerStatus("붙여넣을 수 있는 이미지 영역이 없습니다.");
            return;
        }

        CommitAnnotationsToBitmap("붙여넣기 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        _pixelSelection = target;
        SetEditableImage(PasteBitmap(_editableImage, clipboardImage, target), "선택 영역 위치에 이미지를 붙여넣었습니다.");
    }

    private void CopyPixelSelectionToClipboard(Int32Rect selection)
    {
        var copied = new CroppedBitmap(ComposeImageForExport(), selection);
        copied.Freeze();
        WpfClipboard.SetImage(copied);
    }

    private bool TryGetPixelSelection(out Int32Rect selection)
    {
        if (_editableImage is not null && _pixelSelection is { } current &&
            current.Width > 0 && current.Height > 0)
        {
            selection = current;
            return true;
        }

        selection = new Int32Rect();
        return false;
    }

    private Int32Rect GetPasteTarget(BitmapSource pastedImage)
    {
        if (_editableImage is null)
        {
            return new Int32Rect();
        }

        var sourceWidth = _editableImage.PixelWidth;
        var sourceHeight = _editableImage.PixelHeight;
        var x = _pixelSelection?.X ?? Math.Max(0, (sourceWidth - pastedImage.PixelWidth) / 2);
        var y = _pixelSelection?.Y ?? Math.Max(0, (sourceHeight - pastedImage.PixelHeight) / 2);
        x = Math.Clamp(x, 0, sourceWidth - 1);
        y = Math.Clamp(y, 0, sourceHeight - 1);
        return new Int32Rect(
            x,
            y,
            Math.Min(pastedImage.PixelWidth, sourceWidth - x),
            Math.Min(pastedImage.PixelHeight, sourceHeight - y));
    }

    private static BitmapSource ClearPixelRectangle(BitmapSource source, Int32Rect selection)
    {
        var bitmapSource = ConvertToBgra32(source);
        var width = bitmapSource.PixelWidth;
        var height = bitmapSource.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmapSource.CopyPixels(pixels, stride, 0);

        var right = Math.Min(width, selection.X + selection.Width);
        var bottom = Math.Min(height, selection.Y + selection.Height);
        for (var y = Math.Max(0, selection.Y); y < bottom; y++)
        {
            Array.Clear(pixels, (y * stride) + (Math.Max(0, selection.X) * 4), Math.Max(0, right - selection.X) * 4);
        }

        var cleared = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        cleared.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        cleared.Freeze();
        return cleared;
    }

    private static BitmapSource PasteBitmap(BitmapSource source, BitmapSource pastedImage, Int32Rect target)
    {
        var sourceBitmap = ConvertToPbgra32(source);
        var pastedBitmap = ConvertToPbgra32(pastedImage);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(sourceBitmap, new WpfRect(0, 0, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight));
            drawing.PushClip(new RectangleGeometry(new WpfRect(target.X, target.Y, target.Width, target.Height)));
            drawing.DrawImage(pastedBitmap, new WpfRect(target.X, target.Y, pastedBitmap.PixelWidth, pastedBitmap.PixelHeight));
            drawing.Pop();
        }

        return RenderBitmap(visual, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight);
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

    private void OnMosaicSizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _mosaicBlockSize = Math.Clamp((int)Math.Round(e.NewValue), 6, 64);
        if (MosaicSizeText is not null)
        {
            MosaicSizeText.Text = $"{_mosaicBlockSize}px";
        }
    }

    private void OnMosaicTypeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ViewerMosaicType>(tag, out var mosaicType))
        {
            return;
        }

        _mosaicType = mosaicType;
        UpdateToolContext();
    }

    private void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ViewerEditTool>(tag, out var tool))
        {
            return;
        }

        if (_isCropMode)
        {
            CloseCropMode();
        }

        _editTool = tool;
        if (tool == ViewerEditTool.PixelSelect)
        {
            SelectAnnotation(null);
        }

        UpdateToolButtons();
        SetViewerStatus(tool switch
        {
            ViewerEditTool.Pan => "핸드툴: 이미지를 끌어 이동합니다.",
            ViewerEditTool.Select => "선택: 보기 상태를 유지하고 편집하지 않습니다.",
            ViewerEditTool.PixelSelect => "영역 선택: 드래그한 영역을 복사, 잘라내기, 붙여넣기할 수 있습니다.",
            ViewerEditTool.Rectangle => "네모: 이미지 위에서 드래그해 그립니다.",
            ViewerEditTool.Ellipse => "동그라미: 이미지 위에서 드래그해 그립니다.",
            ViewerEditTool.Mosaic => "모자이크: 가릴 영역을 드래그합니다.",
            ViewerEditTool.Pen => "펜: 이미지 위에 자유선을 그립니다.",
            ViewerEditTool.Arrow => "화살표: 드래그해서 방향을 표시합니다.",
            _ => "텍스트: 이미지 위를 클릭해 입력합니다."
        });
    }

    private void OnCropToolClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus("자를 이미지가 없습니다.");
            return;
        }

        if (_isCropMode)
        {
            CloseCropMode();
            SetViewerStatus("자르기를 취소했습니다.");
            return;
        }

        _isCropMode = true;
        _editTool = ViewerEditTool.Pan;
        SelectAnnotation(null);
        ResetCropControls();
        CropPanel.Visibility = Visibility.Visible;
        UpdateToolButtons();
        RenderAnnotations();
        SetViewerStatus("자르기: 왼쪽 패널에서 값을 조절한 뒤 적용하세요.");
    }

    private void OnCropValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingCropControls || !_isCropMode)
        {
            return;
        }

        SynchronizeCropControls();
        RenderAnnotations();
    }

    private void OnCropTextLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ApplyCropTextValue(sender as System.Windows.Controls.TextBox);
    }

    private void OnCropTextPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyCropTextValue(sender as System.Windows.Controls.TextBox);
        e.Handled = true;
    }

    private void OnCancelCropClick(object sender, RoutedEventArgs e)
    {
        CloseCropMode();
        SetViewerStatus("자르기를 취소했습니다.");
    }

    private void OnApplyCropClick(object sender, RoutedEventArgs e)
    {
        ApplyCrop();
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

        if (_editTool == ViewerEditTool.PixelSelect)
        {
            _isDrawing = true;
            _drawStartPoint = position;
            _previewShape = CreatePixelSelectionPreview();
            AnnotationOverlay.Children.Add(_previewShape);
            UpdatePreviewShape(position);
            AnnotationOverlay.CaptureMouse();
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

        if (_editTool == ViewerEditTool.PixelSelect)
        {
            _pixelSelection = rect.Width >= 2 && rect.Height >= 2 ? rect : null;
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            RenderAnnotations();
            SetViewerStatus(_pixelSelection is null
                ? "영역 선택을 해제했습니다."
                : $"영역 선택: {rect.Width} × {rect.Height}px");
            e.Handled = true;
            return;
        }

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

    private void ResetCropControls()
    {
        _isUpdatingCropControls = true;
        try
        {
            CropLeftSlider.Value = 0;
            CropRightSlider.Value = 0;
            CropTopSlider.Value = 0;
            CropBottomSlider.Value = 0;
        }
        finally
        {
            _isUpdatingCropControls = false;
        }

        SynchronizeCropControls();
    }

    private void SynchronizeCropControls()
    {
        if (_editableImage is null)
        {
            return;
        }

        var width = _editableImage.PixelWidth;
        var height = _editableImage.PixelHeight;
        _isUpdatingCropControls = true;
        try
        {
            var left = Math.Clamp((int)Math.Round(CropLeftSlider.Value), 0, Math.Max(0, width - 1));
            var right = Math.Clamp((int)Math.Round(CropRightSlider.Value), 0, Math.Max(0, width - left - 1));
            var top = Math.Clamp((int)Math.Round(CropTopSlider.Value), 0, Math.Max(0, height - 1));
            var bottom = Math.Clamp((int)Math.Round(CropBottomSlider.Value), 0, Math.Max(0, height - top - 1));

            CropLeftSlider.Maximum = Math.Max(0, width - right - 1);
            CropRightSlider.Maximum = Math.Max(0, width - left - 1);
            CropTopSlider.Maximum = Math.Max(0, height - bottom - 1);
            CropBottomSlider.Maximum = Math.Max(0, height - top - 1);
            CropLeftSlider.Value = Math.Min(left, CropLeftSlider.Maximum);
            CropRightSlider.Value = Math.Min(right, CropRightSlider.Maximum);
            CropTopSlider.Value = Math.Min(top, CropTopSlider.Maximum);
            CropBottomSlider.Value = Math.Min(bottom, CropBottomSlider.Maximum);
            CropLeftText.Text = ((int)Math.Round(CropLeftSlider.Value)).ToString(CultureInfo.CurrentCulture);
            CropRightText.Text = ((int)Math.Round(CropRightSlider.Value)).ToString(CultureInfo.CurrentCulture);
            CropTopText.Text = ((int)Math.Round(CropTopSlider.Value)).ToString(CultureInfo.CurrentCulture);
            CropBottomText.Text = ((int)Math.Round(CropBottomSlider.Value)).ToString(CultureInfo.CurrentCulture);
        }
        finally
        {
            _isUpdatingCropControls = false;
        }
    }

    private void ApplyCropTextValue(System.Windows.Controls.TextBox? textBox)
    {
        if (!_isCropMode || textBox?.Tag is not string side)
        {
            return;
        }

        if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) || value < 0)
        {
            SynchronizeCropControls();
            SetViewerStatus("자르기 값은 0 이상의 정수로 입력하세요.");
            return;
        }

        var slider = side switch
        {
            "Left" => CropLeftSlider,
            "Right" => CropRightSlider,
            "Top" => CropTopSlider,
            "Bottom" => CropBottomSlider,
            _ => null
        };
        if (slider is null)
        {
            return;
        }

        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        SynchronizeCropControls();
        RenderAnnotations();
    }

    private Int32Rect GetCropPixelRect()
    {
        if (_editableImage is null)
        {
            return new Int32Rect();
        }

        return CalculateCropRectangle(
            _editableImage.PixelWidth,
            _editableImage.PixelHeight,
            (int)Math.Round(CropLeftSlider.Value),
            (int)Math.Round(CropRightSlider.Value),
            (int)Math.Round(CropTopSlider.Value),
            (int)Math.Round(CropBottomSlider.Value));
    }

    internal static Int32Rect CalculateCropRectangle(int width, int height, int left, int right, int top, int bottom)
    {
        if (width < 1 || height < 1)
        {
            return new Int32Rect();
        }

        left = Math.Clamp(left, 0, width - 1);
        right = Math.Clamp(right, 0, width - left - 1);
        top = Math.Clamp(top, 0, height - 1);
        bottom = Math.Clamp(bottom, 0, height - top - 1);
        return new Int32Rect(left, top, width - left - right, height - top - bottom);
    }

    private void ApplyCrop()
    {
        if (_editableImage is null)
        {
            return;
        }

        var crop = GetCropPixelRect();
        if (crop.Width == _editableImage.PixelWidth && crop.Height == _editableImage.PixelHeight)
        {
            CloseCropMode();
            SetViewerStatus("자르기 값이 없어 원본을 유지했습니다.");
            return;
        }

        CommitAnnotationsToBitmap("자르기 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var cropped = new CroppedBitmap(ConvertToPbgra32(_editableImage), crop);
        cropped.Freeze();
        _pixelSelection = null;
        CloseCropMode();
        SetEditableImage(cropped, $"이미지를 {crop.Width} × {crop.Height}px로 잘랐습니다.");
    }

    private void CloseCropMode()
    {
        _isCropMode = false;
        CropPanel.Visibility = Visibility.Collapsed;
        UpdateToolButtons();
        RenderAnnotations();
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

        RenderCropPreview();
        RenderSelectionFrame();
        RenderPixelSelectionFrame();
    }

    private void RenderCropPreview()
    {
        _cropPreviewMask = null;
        _cropPreviewFrame = null;
        if (!_isCropMode || _editableImage is null || AnnotationOverlay.ActualWidth <= 0 || AnnotationOverlay.ActualHeight <= 0)
        {
            return;
        }

        var crop = GetCropPixelRect();
        var rect = PixelToDisplayRect(new WpfRect(crop.X, crop.Y, crop.Width, crop.Height));
        var maskGeometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        maskGeometry.Children.Add(new RectangleGeometry(new WpfRect(0, 0, AnnotationOverlay.ActualWidth, AnnotationOverlay.ActualHeight)));
        maskGeometry.Children.Add(new RectangleGeometry(rect));
        _cropPreviewMask = new WpfPath
        {
            Data = maskGeometry,
            Fill = new SolidColorBrush(WpfColor.FromArgb(150, 8, 15, 15)),
            IsHitTestVisible = false
        };
        _cropPreviewFrame = new WpfRectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Stroke = new SolidColorBrush(WpfColor.FromRgb(94, 234, 212)),
            StrokeThickness = 1.5,
            Fill = WpfBrushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_cropPreviewFrame, rect.Left);
        Canvas.SetTop(_cropPreviewFrame, rect.Top);
        AnnotationOverlay.Children.Add(_cropPreviewMask);
        AnnotationOverlay.Children.Add(_cropPreviewFrame);
    }

    private void RenderPixelSelectionFrame()
    {
        _pixelSelectionFrame = null;
        if (_pixelSelection is not { } selection || _editableImage is null)
        {
            return;
        }

        var rect = PixelToDisplayRect(new WpfRect(selection.X, selection.Y, selection.Width, selection.Height));
        _pixelSelectionFrame = new WpfRectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = WpfBrushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_pixelSelectionFrame, rect.Left);
        Canvas.SetTop(_pixelSelectionFrame, rect.Top);
        AnnotationOverlay.Children.Add(_pixelSelectionFrame);
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

    private async void ApplyMosaic(Int32Rect pixelRect)
    {
        if (_editableImage is null || _isApplyingMosaic)
        {
            return;
        }

        _isApplyingMosaic = true;
        try
        {
            CommitAnnotationsToBitmap("모자이크 전 주석을 이미지에 적용했습니다.", pushUndo: true);
            PushUndoSnapshot();
            var source = ConvertToBgra32(_editableImage);
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            var blockSize = Math.Clamp((int)Math.Round(_mosaicBlockSize / Math.Max(_zoom, MinZoom)), 6, 64);
            var right = Math.Min(width, pixelRect.X + pixelRect.Width);
            var bottom = Math.Min(height, pixelRect.Y + pixelRect.Height);
            var mosaicType = _mosaicType;

            await Task.Run(() =>
            {
                if (mosaicType == ViewerMosaicType.Blur)
                {
                    ApplyGaussianBlurRegion(pixels, stride, pixelRect.X, pixelRect.Y, right, bottom, blockSize / 2.0);
                }
                else
                {
                    for (var y = pixelRect.Y; y < bottom; y += blockSize)
                    {
                        for (var x = pixelRect.X; x < right; x += blockSize)
                        {
                            var blockRight = Math.Min(right, x + blockSize);
                            var blockBottom = Math.Min(bottom, y + blockSize);
                            ApplyMosaicBlock(pixels, stride, x, y, blockRight, blockBottom);
                        }
                    }
                }
            });

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            bitmap.Freeze();
            SetEditableImage(bitmap, mosaicType == ViewerMosaicType.Blur ? "흐림 모자이크를 적용했습니다." : "모자이크를 적용했습니다.");
        }
        finally
        {
            _isApplyingMosaic = false;
        }
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

    internal static void ApplyGaussianBlurRegion(byte[] pixels, int stride, int left, int top, int right, int bottom, double radius)
    {
        if (right <= left || bottom <= top)
        {
            return;
        }

        var sigma = Math.Max(1.0, radius / 2.0);
        var kernelRadius = Math.Max(1, (int)Math.Ceiling(radius));
        var kernel = BuildGaussianKernel(kernelRadius, sigma);
        var regionWidth = right - left;
        var regionHeight = bottom - top;
        var horizontal = new byte[regionWidth * regionHeight * 4];

        for (var y = 0; y < regionHeight; y++)
        {
            var sourceRow = (top + y) * stride;
            var destRow = y * regionWidth * 4;
            for (var x = 0; x < regionWidth; x++)
            {
                double blue = 0;
                double green = 0;
                double red = 0;
                double alpha = 0;
                for (var k = -kernelRadius; k <= kernelRadius; k++)
                {
                    var sampleX = Math.Clamp(x + k, 0, regionWidth - 1) + left;
                    var offset = sourceRow + (sampleX * 4);
                    var weight = kernel[k + kernelRadius];
                    blue += pixels[offset] * weight;
                    green += pixels[offset + 1] * weight;
                    red += pixels[offset + 2] * weight;
                    alpha += pixels[offset + 3] * weight;
                }

                var destOffset = destRow + (x * 4);
                horizontal[destOffset] = (byte)Math.Clamp(Math.Round(blue), 0, 255);
                horizontal[destOffset + 1] = (byte)Math.Clamp(Math.Round(green), 0, 255);
                horizontal[destOffset + 2] = (byte)Math.Clamp(Math.Round(red), 0, 255);
                horizontal[destOffset + 3] = (byte)Math.Clamp(Math.Round(alpha), 0, 255);
            }
        }

        for (var y = 0; y < regionHeight; y++)
        {
            var destRow = (top + y) * stride;
            for (var x = 0; x < regionWidth; x++)
            {
                double blue = 0;
                double green = 0;
                double red = 0;
                double alpha = 0;
                for (var k = -kernelRadius; k <= kernelRadius; k++)
                {
                    var sampleY = Math.Clamp(y + k, 0, regionHeight - 1);
                    var offset = (sampleY * regionWidth * 4) + (x * 4);
                    var weight = kernel[k + kernelRadius];
                    blue += horizontal[offset] * weight;
                    green += horizontal[offset + 1] * weight;
                    red += horizontal[offset + 2] * weight;
                    alpha += horizontal[offset + 3] * weight;
                }

                var destOffset = destRow + ((left + x) * 4);
                pixels[destOffset] = (byte)Math.Clamp(Math.Round(blue), 0, 255);
                pixels[destOffset + 1] = (byte)Math.Clamp(Math.Round(green), 0, 255);
                pixels[destOffset + 2] = (byte)Math.Clamp(Math.Round(red), 0, 255);
                pixels[destOffset + 3] = (byte)Math.Clamp(Math.Round(alpha), 0, 255);
            }
        }
    }

    internal static double[] BuildGaussianKernel(int radius, double sigma)
    {
        var kernel = new double[(radius * 2) + 1];
        var sum = 0.0;
        for (var i = -radius; i <= radius; i++)
        {
            var value = Math.Exp(-(i * i) / (2 * sigma * sigma));
            kernel[i + radius] = value;
            sum += value;
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= sum;
        }

        return kernel;
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

    private static WpfRectangle CreatePixelSelectionPreview()
    {
        return new WpfRectangle
        {
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = new SolidColorBrush(WpfColor.FromArgb(30, 37, 99, 235)),
            IsHitTestVisible = false
        };
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
}
