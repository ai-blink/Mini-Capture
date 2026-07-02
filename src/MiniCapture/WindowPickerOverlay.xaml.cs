using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MiniCapture;

public partial class WindowPickerOverlay : Window
{
    private readonly DispatcherTimer _pollTimer;
    private readonly int _currentProcessId = Environment.ProcessId;
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
        Left = _virtualBounds.Left;
        Top = _virtualBounds.Top;
        Width = _virtualBounds.Width;
        Height = _virtualBounds.Height;

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
        var left = bounds.Left - _virtualBounds.Left;
        var top = bounds.Top - _virtualBounds.Top;

        System.Windows.Controls.Canvas.SetLeft(HighlightBorder, left);
        System.Windows.Controls.Canvas.SetTop(HighlightBorder, top);
        HighlightBorder.Width = bounds.Width;
        HighlightBorder.Height = bounds.Height;
        HighlightBorder.Visibility = Visibility.Visible;

        TargetText.Text = string.IsNullOrWhiteSpace(title) ? "선택된 창" : title;
        System.Windows.Controls.Canvas.SetLeft(TargetText, left + 8);
        System.Windows.Controls.Canvas.SetTop(TargetText, Math.Max(8, top - 34));
        TargetText.Visibility = Visibility.Visible;
    }
}
