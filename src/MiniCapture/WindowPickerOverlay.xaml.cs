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
    private readonly ImageSource? _frozenPreview;
    private readonly IReadOnlyList<WindowCaptureTarget>? _frozenTargets;
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

    public WindowPickerOverlay(ImageSource frozenPreview, IReadOnlyList<WindowCaptureTarget> frozenTargets)
        : this()
    {
        _frozenPreview = frozenPreview;
        _frozenTargets = frozenTargets;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _virtualBounds = ScreenCaptureService.GetVirtualScreenBounds();
        _fromDevice = GetFromDeviceMatrix();
        _virtualBoundsDip = DeviceRectangleToDip(_virtualBounds);

        if (UsesFrozenSnapshot)
        {
            Left = _virtualBoundsDip.Left;
            Top = _virtualBoundsDip.Top;
            Width = _virtualBoundsDip.Width;
            Height = _virtualBoundsDip.Height;
            OverlayCanvas.Background = new ImageBrush(_frozenPreview!)
            {
                Stretch = Stretch.Fill
            };
            HintText.Text = "정지 화면에서 캡처할 창을 클릭 / Esc 취소";
            ShowHintNearCursor(System.Windows.Forms.Cursor.Position);
        }
        else
        {
            Width = HintWidth;
            Height = HintHeight;
            ShowHintNearCursor(System.Windows.Forms.Cursor.Position);
        }

        _inputHook = new GlobalInputHook();
        _inputHook.MouseMove += OnHookMouseMove;
        _inputHook.LeftButtonDown += OnHookLeftButtonDown;
        _inputHook.EscapePressed += OnHookEscapePressed;
        _pollTimer.Start();
        UpdateTargetFromCursor();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TrySetCaptureExclusion(
            new WindowInteropHelper(this).Handle,
            MiniCaptureSettingsStore.Load().CaptureUiExcludedFromCapture);
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
            e.Handled = UsesFrozenSnapshot;
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
        _currentTarget = _frozenTargets is null
            ? WindowPickerService.FindTargetAt(new DrawingPoint(cursor.X, cursor.Y), _currentProcessId)
            : WindowPickerService.FindTargetAt(new DrawingPoint(cursor.X, cursor.Y), _frozenTargets);
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
        if (UsesFrozenSnapshot)
        {
            var left = boundsDip.Left - _virtualBoundsDip.Left;
            var top = boundsDip.Top - _virtualBoundsDip.Top;
            System.Windows.Controls.Canvas.SetLeft(HighlightBorder, left);
            System.Windows.Controls.Canvas.SetTop(HighlightBorder, top);
            HighlightBorder.Width = Math.Max(1, boundsDip.Width);
            HighlightBorder.Height = Math.Max(1, boundsDip.Height);
            HighlightBorder.Visibility = Visibility.Visible;

            HintText.Visibility = Visibility.Collapsed;
            TargetText.Text = string.IsNullOrWhiteSpace(title) ? "선택된 창" : title;
            System.Windows.Controls.Canvas.SetLeft(TargetText, Math.Max(0, left + 8));
            System.Windows.Controls.Canvas.SetTop(TargetText, Math.Max(0, top + 8));
            TargetText.MaxWidth = Math.Max(120, Math.Min(420, Width - left - 16));
            TargetText.Visibility = Visibility.Visible;
            return;
        }

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
        if (UsesFrozenSnapshot)
        {
            var left = Math.Clamp(
                cursorDip.X - _virtualBoundsDip.Left + 18,
                0,
                Math.Max(0, _virtualBoundsDip.Width - HintWidth));
            var top = Math.Clamp(
                cursorDip.Y - _virtualBoundsDip.Top + 18,
                0,
                Math.Max(0, _virtualBoundsDip.Height - HintHeight));

            System.Windows.Controls.Canvas.SetLeft(HintText, left);
            System.Windows.Controls.Canvas.SetTop(HintText, top);
            HintText.Visibility = Visibility.Visible;
            return;
        }

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

    private bool UsesFrozenSnapshot => _frozenPreview is not null && _frozenTargets is not null;
}
