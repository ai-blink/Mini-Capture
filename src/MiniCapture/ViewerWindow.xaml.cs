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
}
