using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingPoint = System.Drawing.Point;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace MiniCapture;

public partial class RegionCaptureOverlay : Window
{
    private const double HintWidth = 278;
    private const double HintHeight = 36;

    private GlobalInputHook? _inputHook;
    private DrawingPoint? _dragStart;
    private Matrix _fromDevice = Matrix.Identity;
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
        _virtualBoundsDip = DeviceRectangleToDip(_virtualBounds);
        Width = HintWidth;
        Height = HintHeight;
        ShowHintNearCursor(System.Windows.Forms.Cursor.Position);

        _inputHook = new GlobalInputHook();
        _inputHook.LeftButtonDown += OnHookLeftButtonDown;
        _inputHook.MouseMove += OnHookMouseMove;
        _inputHook.LeftButtonUp += OnHookLeftButtonUp;
        _inputHook.EscapePressed += OnHookEscapePressed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TryExcludeFromCapture(new WindowInteropHelper(this).Handle);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _inputHook?.Dispose();
        _inputHook = null;
    }

    private void OnHookLeftButtonDown(object? sender, GlobalMouseHookEventArgs e)
    {
        _dragStart = e.ScreenPoint;
        HintText.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(e.ScreenPoint, e.ScreenPoint);
        e.Handled = true;
    }

    private void OnHookMouseMove(object? sender, GlobalMouseHookEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            ShowHintNearCursor(e.ScreenPoint);
            return;
        }

        UpdateSelection(start, e.ScreenPoint);
        e.Handled = true;
    }

    private void OnHookLeftButtonUp(object? sender, GlobalMouseHookEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        e.Handled = true;
        var end = e.ScreenPoint;
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

    private void OnHookEscapePressed(object? sender, GlobalKeyHookEventArgs e)
    {
        e.Handled = true;
        Cancel();
    }

    private void UpdateSelection(DrawingPoint start, DrawingPoint end)
    {
        var deviceRect = NormalizeDeviceRectangle(start, end);
        var dipRect = DeviceRectangleToDip(deviceRect);
        Left = dipRect.Left;
        Top = dipRect.Top;
        Width = Math.Max(1, dipRect.Width);
        Height = Math.Max(1, dipRect.Height);

        System.Windows.Controls.Canvas.SetLeft(SelectionBorder, 0);
        System.Windows.Controls.Canvas.SetTop(SelectionBorder, 0);
        SelectionBorder.Width = Width;
        SelectionBorder.Height = Height;
    }

    private DrawingRectangle ToScreenRectangle(DrawingPoint start, DrawingPoint end)
    {
        return NormalizeDeviceRectangle(start, end);
    }

    private DrawingRectangle NormalizeDeviceRectangle(DrawingPoint start, DrawingPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);
        return new DrawingRectangle(left, top, right - left, bottom - top);
    }

    private void ShowHintNearCursor(DrawingPoint cursor)
    {
        HintText.Visibility = Visibility.Visible;
        SelectionBorder.Visibility = Visibility.Collapsed;

        var cursorDip = _fromDevice.Transform(new WpfPoint(cursor.X, cursor.Y));
        Left = Math.Clamp(cursorDip.X + 18, _virtualBoundsDip.Left, _virtualBoundsDip.Right - HintWidth);
        Top = Math.Clamp(cursorDip.Y + 18, _virtualBoundsDip.Top, _virtualBoundsDip.Bottom - HintHeight);
        Width = HintWidth;
        Height = HintHeight;
    }

    private WpfRect DeviceRectangleToDip(DrawingRectangle rectangle)
    {
        var topLeft = _fromDevice.Transform(new WpfPoint(rectangle.Left, rectangle.Top));
        var bottomRight = _fromDevice.Transform(new WpfPoint(rectangle.Right, rectangle.Bottom));
        return new WpfRect(topLeft, bottomRight);
    }

    private void Cancel()
    {
        DialogResult = false;
        Close();
    }
}
