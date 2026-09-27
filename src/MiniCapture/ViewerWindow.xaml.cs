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

// 크기 조절 핸들이 움직이는 변. -1 = 왼쪽/위, 0 = 고정, 1 = 오른쪽/아래.
internal readonly record struct ResizeEdges(int Horizontal, int Vertical);

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
    Arrow,
    Mosaic
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
    private static readonly SemaphoreSlim ImageDecodeGate = new(1, 1);
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

    // 편집 동작 직전 상태. 이미지는 frozen이라 참조를 공유하고, 주석은 화면 요소 없이 복제해 둔다.
    private sealed record EditSnapshot(BitmapSource Image, IReadOnlyList<EditableAnnotation> Annotations);

    internal sealed class EditableAnnotation
    {
        public ViewerAnnotationKind Kind { get; init; }

        public WpfColor Color { get; init; }

        public double StrokeThickness { get; init; }

        public WpfRect PixelBounds { get; set; }

        public List<WpfPoint> PixelPoints { get; } = new();

        public string Text { get; set; } = string.Empty;

        public ViewerMosaicType MosaicType { get; set; }

        public int MosaicBlockSize { get; set; }

        // 원본 이미지에서 계산한 모자이크 조각 캐시. 키(영역·종류·크기·원본)가 바뀌면 다시 만든다.
        public BitmapSource? MosaicPatch { get; set; }

        public object? MosaicPatchKey { get; set; }

        public FrameworkElement? Element { get; set; }

        // 화면 요소(Element)는 복제하지 않는다. 복원할 때 다시 그린다. 모자이크 조각은 frozen이라 공유한다.
        public EditableAnnotation Clone()
        {
            var clone = new EditableAnnotation
            {
                Kind = Kind,
                Color = Color,
                StrokeThickness = StrokeThickness,
                PixelBounds = PixelBounds,
                Text = Text,
                MosaicType = MosaicType,
                MosaicBlockSize = MosaicBlockSize,
                MosaicPatch = MosaicPatch,
                MosaicPatchKey = MosaicPatchKey
            };
            clone.PixelPoints.AddRange(PixelPoints);
            return clone;
        }
    }

    private readonly ObservableCollection<CaptureImageFile> _files = new();
    private readonly List<EditableAnnotation> _annotations = new();
    private readonly List<WpfPoint> _penPoints = new();
    private readonly EditHistory<EditSnapshot> _editHistory = new(MaxUndoSnapshots);
    // 연속 입력(텍스트 타이핑·모자이크 크기 슬라이더)을 한 동작으로 묶는 키. 같은 키가 이어지면 새 스냅샷을 쌓지 않는다.
    private object? _undoCoalesceKey;
    // 이동·크기 변경 드래그를 시작할 때 잡은 직전 상태. 실제로 바뀌었을 때만 히스토리에 넣는다.
    private EditSnapshot? _dragStartSnapshot;
    private readonly DispatcherTimer _viewStateSaveTimer;
    private CancellationTokenSource? _imageLoadCancellation;
    private CancellationTokenSource? _loadCancellation;
    private ObservableCollection<FolderTreeNode> _folderNodes = new();
    private BitmapSource? _editableImage;
    private WpfShape? _previewShape;
    private WpfPath? _cropPreviewMask;
    private WpfRectangle? _cropPreviewFrame;
    private WpfRectangle? _pixelSelectionFrame;
    private readonly List<WpfRectangle> _resizeHandles = new();
    private ResizeEdges _activeResizeEdges;
    private WpfRectangle? _selectionFrame;
    private EditableAnnotation? _selectedAnnotation;
    // 그리기 도구에서 누른 객체. 놓을 때 클릭(거의 안 움직임)이면 이 객체를 선택한다.
    private EditableAnnotation? _pressedAnnotation;
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
    private bool _isMovingPixelSelection;
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
    private Int32Rect _pixelSelectionStartRect;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private WpfColor _selectedColor = WpfColor.FromRgb(239, 68, 68);
    private double _strokeThickness = 4.0;
    private int _mosaicBlockSize = 14;
    private ViewerMosaicType _mosaicType = ViewerMosaicType.Block;
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
        if (TryOpenImageFromCurrentFolder(imagePath))
        {
            RestoreAndActivate();
            return;
        }

        _pendingPath = imagePath;
        _ = RefreshIndexAsync();
        RestoreAndActivate();
    }

    public void RestoreAndActivate()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private bool TryOpenImageFromCurrentFolder(string? imagePath)
    {
        if (!CanIncrementallyOpenImage(_currentFolderPath, imagePath) ||
            !CaptureFileIndex.TryCreateOpenCandidate(imagePath, out var imageFile) ||
            imageFile is null)
        {
            return false;
        }

        var file = FindFile(imagePath);
        if (file is null)
        {
            _files.Insert(GetSortedFileInsertIndex(imageFile), imageFile);
            _activeFolderFileCount = _files.Count;
            FileCountText.Text = $"{_files.Count:0}개";
            UpdateNavigationButtons();
            file = imageFile;
        }
        else
        {
            file.RefreshFromDisk();
            MoveFileToSortedPosition(file);
        }

        if (!string.Equals(_currentFile?.Path, file.Path, StringComparison.OrdinalIgnoreCase))
        {
            SelectFile(file);
        }

        return true;
    }

    internal static bool CanIncrementallyOpenImage(string? currentFolderPath, string? imagePath) =>
        CaptureFileIndex.IsOpenCandidatePath(imagePath) &&
        IsSameFolderSelection(currentFolderPath, IOPath.GetDirectoryName(imagePath));

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

}
