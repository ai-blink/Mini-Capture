using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace MiniCapture;

public partial class RegionCaptureOverlay : Window
{
    private const int MinimumSelectionSize = 3;

    private readonly ImageSource? _frozenPreview;
    private WpfPoint? _dragStart;
    private Matrix _fromDevice = Matrix.Identity;
    private Matrix _toDevice = Matrix.Identity;
    private WpfRect _virtualBoundsDip;
    private DrawingRectangle _virtualBounds;

    public DrawingRectangle? SelectedRegion { get; private set; }

    public RegionCaptureOverlay()
    {
        InitializeComponent();
    }

    public RegionCaptureOverlay(ImageSource frozenPreview)
        : this()
    {
        _frozenPreview = frozenPreview;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _virtualBounds = ScreenCaptureService.GetVirtualScreenBounds();
        var source = PresentationSource.FromVisual(this);
        _fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        _toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        _virtualBoundsDip = DeviceRectangleToDip(_virtualBounds);

        Left = _virtualBoundsDip.Left;
        Top = _virtualBoundsDip.Top;
        Width = _virtualBoundsDip.Width;
        Height = _virtualBoundsDip.Height;

        if (_frozenPreview is not null)
        {
            OverlayCanvas.Background = new ImageBrush(_frozenPreview)
            {
                Stretch = Stretch.Fill
            };
            HintText.Text = "정지 화면에서 드래그해서 캡처 영역 선택 / Esc 취소";
        }

        Activate();
        Focus();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TrySetCaptureExclusion(
            new WindowInteropHelper(this).Handle,
            MiniCaptureSettingsStore.Load().CaptureUiExcludedFromCapture);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(OverlayCanvas);
        HintText.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(_dragStart.Value, _dragStart.Value);
        Mouse.Capture(this);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelection(start, e.GetPosition(OverlayCanvas));
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        Mouse.Capture(null);
        var selected = ToScreenRectangle(start, e.GetPosition(OverlayCanvas));
        if (selected.Width < MinimumSelectionSize || selected.Height < MinimumSelectionSize)
        {
            Cancel();
            return;
        }

        SelectedRegion = selected;
        DialogResult = true;
        Close();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Cancel();
        e.Handled = true;
    }

    private void UpdateSelection(WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);

        System.Windows.Controls.Canvas.SetLeft(SelectionBorder, left);
        System.Windows.Controls.Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private DrawingRectangle ToScreenRectangle(WpfPoint start, WpfPoint end)
    {
        var leftDip = _virtualBoundsDip.Left + Math.Min(start.X, end.X);
        var topDip = _virtualBoundsDip.Top + Math.Min(start.Y, end.Y);
        var rightDip = _virtualBoundsDip.Left + Math.Max(start.X, end.X);
        var bottomDip = _virtualBoundsDip.Top + Math.Max(start.Y, end.Y);

        var topLeft = _toDevice.Transform(new WpfPoint(leftDip, topDip));
        var bottomRight = _toDevice.Transform(new WpfPoint(rightDip, bottomDip));
        var left = (int)Math.Round(topLeft.X);
        var top = (int)Math.Round(topLeft.Y);
        var right = (int)Math.Round(bottomRight.X);
        var bottom = (int)Math.Round(bottomRight.Y);
        return new DrawingRectangle(left, top, right - left, bottom - top);
    }

    private WpfRect DeviceRectangleToDip(DrawingRectangle rectangle)
    {
        var topLeft = _fromDevice.Transform(new WpfPoint(rectangle.Left, rectangle.Top));
        var bottomRight = _fromDevice.Transform(new WpfPoint(rectangle.Right, rectangle.Bottom));
        return new WpfRect(topLeft, bottomRight);
    }

    private void Cancel()
    {
        Mouse.Capture(null);
        DialogResult = false;
        Close();
    }
}
