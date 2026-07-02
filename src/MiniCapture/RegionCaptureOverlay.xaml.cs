using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace MiniCapture;

public partial class RegionCaptureOverlay : Window
{
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

        Activate();
        Focus();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TryExcludeFromCapture(new WindowInteropHelper(this).Handle);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(OverlayCanvas);
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(_dragStart.Value, _dragStart.Value);
        Mouse.Capture(this);
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelection(start, e.GetPosition(OverlayCanvas));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        Mouse.Capture(null);
        var end = e.GetPosition(OverlayCanvas);
        var selected = ToScreenRectangle(start, end);
        if (selected.Width < 3 || selected.Height < 3)
        {
            Cancel();
            return;
        }

        SelectedRegion = selected;
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
        }
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
        var width = (int)Math.Round(bottomRight.X - topLeft.X);
        var height = (int)Math.Round(bottomRight.Y - topLeft.Y);
        return new DrawingRectangle(left, top, width, height);
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
