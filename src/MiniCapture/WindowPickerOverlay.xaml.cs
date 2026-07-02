using System.Windows;
using System.Windows.Input;
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
    private readonly DispatcherTimer _pollTimer;
    private readonly int _currentProcessId = Environment.ProcessId;
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
        Left = _virtualBoundsDip.Left;
        Top = _virtualBoundsDip.Top;
        Width = _virtualBoundsDip.Width;
        Height = _virtualBoundsDip.Height;

        Activate();
        Focus();
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
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateTargetFromCursor();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        UpdateTargetFromCursor();
        if (_currentTarget is not { } target)
        {
            return;
        }

        SelectedTarget = target;
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void UpdateTargetFromCursor()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        _currentTarget = WindowPickerService.FindTargetAt(new DrawingPoint(cursor.X, cursor.Y), _currentProcessId);
        if (_currentTarget is not { } target)
        {
            HighlightBorder.Visibility = Visibility.Collapsed;
            TargetText.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateHighlight(target.Bounds, target.Title);
    }

    private void UpdateHighlight(DrawingRectangle bounds, string title)
    {
        var boundsDip = DeviceRectangleToDip(bounds);
        var left = boundsDip.Left - _virtualBoundsDip.Left;
        var top = boundsDip.Top - _virtualBoundsDip.Top;

        System.Windows.Controls.Canvas.SetLeft(HighlightBorder, left);
        System.Windows.Controls.Canvas.SetTop(HighlightBorder, top);
        HighlightBorder.Width = boundsDip.Width;
        HighlightBorder.Height = boundsDip.Height;
        HighlightBorder.Visibility = Visibility.Visible;

        TargetText.Text = string.IsNullOrWhiteSpace(title) ? "선택된 창" : title;
        System.Windows.Controls.Canvas.SetLeft(TargetText, left + 8);
        System.Windows.Controls.Canvas.SetTop(TargetText, Math.Max(8, top - 34));
        TargetText.Visibility = Visibility.Visible;
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
