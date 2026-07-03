using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace MiniCapture;

public partial class WindowPickerOverlay : Window
{
    private const double HintWidth = 340;
    private const double HintHeight = 36;

    private readonly DispatcherTimer _pollTimer;
    private readonly int _currentProcessId = Environment.ProcessId;
    private GlobalInputHook? _inputHook;
    private Matrix _fromDevice = Matrix.Identity;
    private WpfRect _virtualBoundsDip;
    private DrawingRectangle _virtualBounds;
    private WindowCaptureTarget? _currentTarget;

    public WindowCaptureTarget? SelectedTarget { get; private set; }

    public WindowPickerOverlay()
    {
        InitializeComponent();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(70)
        };
        _pollTimer.Tick += (_, _) => UpdateTargetFromCursor();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _virtualBounds = ScreenCaptureService.GetVirtualScreenBounds();
        _fromDevice = GetFromDeviceMatrix();
        _virtualBoundsDip = DeviceRectangleToDip(_virtualBounds);
        Width = HintWidth;
        Height = HintHeight;
        ShowHintNearCursor(System.Windows.Forms.Cursor.Position);

        _inputHook = new GlobalInputHook();
        _inputHook.MouseMove += OnHookMouseMove;
        _inputHook.LeftButtonDown += OnHookLeftButtonDown;
        _inputHook.EscapePressed += OnHookEscapePressed;
        _pollTimer.Start();
        UpdateTargetFromCursor();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TryExcludeFromCapture(new WindowInteropHelper(this).Handle);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _pollTimer.Stop();
        _inputHook?.Dispose();
        _inputHook = null;
    }

    private void OnHookMouseMove(object? sender, GlobalMouseHookEventArgs e)
    {
        UpdateTargetFromCursor();
    }

    private void OnHookLeftButtonDown(object? sender, GlobalMouseHookEventArgs e)
    {
        UpdateTargetFromCursor();
        if (_currentTarget is not { } target)
        {
            return;
        }

        e.Handled = true;
        SelectedTarget = target;
        DialogResult = true;
        Close();
    }

    private void OnHookEscapePressed(object? sender, GlobalKeyHookEventArgs e)
    {
        e.Handled = true;
        DialogResult = false;
        Close();
    }

    private void UpdateTargetFromCursor()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        _currentTarget = WindowPickerService.FindTargetAt(new DrawingPoint(cursor.X, cursor.Y), _currentProcessId);
        if (_currentTarget is not { } target)
        {
            HighlightBorder.Visibility = Visibility.Collapsed;
            TargetText.Visibility = Visibility.Collapsed;
            ShowHintNearCursor(cursor);
            return;
        }

        UpdateHighlight(target.Bounds, target.Title);
    }

    private void UpdateHighlight(DrawingRectangle bounds, string title)
    {
        var boundsDip = DeviceRectangleToDip(bounds);
        Left = boundsDip.Left;
        Top = boundsDip.Top;
        Width = Math.Max(1, boundsDip.Width);
        Height = Math.Max(1, boundsDip.Height);

        HintText.Visibility = Visibility.Collapsed;
        System.Windows.Controls.Canvas.SetLeft(HighlightBorder, 0);
        System.Windows.Controls.Canvas.SetTop(HighlightBorder, 0);
        HighlightBorder.Width = Width;
        HighlightBorder.Height = Height;
        HighlightBorder.Visibility = Visibility.Visible;

        TargetText.Text = string.IsNullOrWhiteSpace(title) ? "선택된 창" : title;
        System.Windows.Controls.Canvas.SetLeft(TargetText, 8);
        System.Windows.Controls.Canvas.SetTop(TargetText, 8);
        TargetText.Visibility = Visibility.Visible;
    }

    private void ShowHintNearCursor(DrawingPoint cursor)
    {
        var cursorDip = _fromDevice.Transform(new WpfPoint(cursor.X, cursor.Y));
        Left = Math.Clamp(cursorDip.X + 18, _virtualBoundsDip.Left, _virtualBoundsDip.Right - HintWidth);
        Top = Math.Clamp(cursorDip.Y + 18, _virtualBoundsDip.Top, _virtualBoundsDip.Bottom - HintHeight);
        Width = HintWidth;
        Height = HintHeight;
        HintText.Visibility = Visibility.Visible;
    }

    private Matrix GetFromDeviceMatrix()
    {
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
    }

    private WpfRect DeviceRectangleToDip(DrawingRectangle rectangle)
    {
        var topLeft = _fromDevice.Transform(new WpfPoint(rectangle.Left, rectangle.Top));
        var bottomRight = _fromDevice.Transform(new WpfPoint(rectangle.Right, rectangle.Bottom));
        return new WpfRect(topLeft, bottomRight);
    }
}
