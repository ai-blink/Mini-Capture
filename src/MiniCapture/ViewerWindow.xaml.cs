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
        if (!CaptureFileIndex.IsImagePath(imagePath) ||
            !IsSameFolderSelection(_currentFolderPath, IOPath.GetDirectoryName(imagePath)) ||
            FindFile(imagePath) is not { } file)
        {
            return false;
        }

        if (!string.Equals(_currentFile?.Path, file.Path, StringComparison.OrdinalIgnoreCase))
        {
            SelectFile(file);
        }

        return true;
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

}
