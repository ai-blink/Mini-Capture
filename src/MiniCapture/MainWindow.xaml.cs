using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfClipboard = System.Windows.Clipboard;
using WpfPoint = System.Windows.Point;

namespace MiniCapture;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _statusTimer;
    private WpfPoint? _buttonDragStartScreen;
    private WpfPoint _buttonDragStartWindowPosition;
    private bool _captureInProgress;
    private bool _isDraggingButton;
    private string? _lastCapturePath;
    private bool _suppressNextCaptureClick;
    private SettingsWindow? _settingsWindow;
    private ViewerWindow? _viewerWindow;
    private CaptureMode _selectedMode = CaptureMode.Drag;
    private int _captureDelaySeconds;

    public MainWindow()
    {
        InitializeComponent();

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.8)
        };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusPopup.IsOpen = false;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceNearWorkAreaCorner();
        UpdatePopupPlacement();
        SelectMode(_selectedMode, showStatus: false);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TryExcludeFromCapture(new WindowInteropHelper(this).Handle);
    }

    private void OnCaptureButtonClick(object sender, RoutedEventArgs e)
    {
        if (_suppressNextCaptureClick)
        {
            _suppressNextCaptureClick = false;
            e.Handled = true;
            return;
        }

        ToggleModePopup();
    }

    private async void OnModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var mode = CaptureModeInfo.FromKey(button.Tag as string);
        SelectMode(mode, showStatus: true);
        ModePopup.IsOpen = false;
        await ExecuteModeAsync(mode);
    }

    private void OnTimerButtonClick(object sender, RoutedEventArgs e)
    {
        ModePopup.IsOpen = false;
        UpdatePopupPlacement();
        TimerPopup.IsOpen = true;
    }

    private void OnOpenViewerEntryClick(object sender, RoutedEventArgs e)
    {
        ClosePopups();
        OpenInternalViewer(CaptureFileIndex.GetLatestImage()?.Path);
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        ClosePopups();
        OpenSettings();
    }

    private void OnTimerDelayClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string secondsText } ||
            !int.TryParse(secondsText, out var seconds))
        {
            return;
        }

        _captureDelaySeconds = seconds;
        ModeTimerButton.Content = $"{seconds}초";
        AutomationProperties.SetName(ModeTimerButton, $"Timer delay {seconds} seconds");
        TimerPopup.IsOpen = false;
        ShowStatus($"{seconds}초 지연이 설정되었습니다. 영역, 창, 전체 캡처에 적용됩니다.");
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ExitApplication();
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { IsExitRequested: false })
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnClosing(e);
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        UpdatePopupPlacement();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;
        StatusPopup.IsOpen = false;
        ResultPopup.IsOpen = false;
        e.Handled = true;
    }

    private void PlaceNearWorkAreaCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 24;
        Top = Math.Max(workArea.Top + 12, workArea.Bottom - Height - 24);
    }

    private void OnCaptureButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Activate();
        _buttonDragStartScreen = GetCursorScreenPosition();
        _buttonDragStartWindowPosition = new WpfPoint(Left, Top);
        _isDraggingButton = false;
        CaptureButton.CaptureMouse();
        e.Handled = true;
    }

    private void OnCaptureButtonPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_buttonDragStartScreen is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = GetCursorScreenPosition();
        var deltaX = current.X - start.X;
        var deltaY = current.Y - start.Y;

        if (!_isDraggingButton &&
            Math.Abs(deltaX) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(deltaY) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _isDraggingButton = true;
        ClosePopups();
        MoveWithinWorkArea(
            _buttonDragStartWindowPosition.X + deltaX,
            _buttonDragStartWindowPosition.Y + deltaY);
        UpdatePopupPlacement();

        e.Handled = true;
    }

    private void OnCaptureButtonPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_buttonDragStartScreen is null)
        {
            return;
        }

        _buttonDragStartScreen = null;
        CaptureButton.ReleaseMouseCapture();

        if (_isDraggingButton)
        {
            _isDraggingButton = false;
            UpdatePopupPlacement();
            e.Handled = true;
            return;
        }

        _suppressNextCaptureClick = true;
        ToggleModePopup();
        e.Handled = true;

        Dispatcher.BeginInvoke(() =>
        {
            _suppressNextCaptureClick = false;
        }, DispatcherPriority.ContextIdle);
    }

    private void MoveWithinWorkArea(double left, double top)
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(left, workArea.Left, workArea.Right - Width);
        Top = Math.Clamp(top, workArea.Top, workArea.Bottom - Height);
    }

    private void ToggleModePopup()
    {
        UpdatePopupPlacement();
        ModePopup.IsOpen = !ModePopup.IsOpen;
    }

    private WpfPoint GetCursorScreenPosition()
    {
        var position = System.Windows.Forms.Cursor.Position;
        var point = new WpfPoint(position.X, position.Y);
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget is null
            ? point
            : source.CompositionTarget.TransformFromDevice.Transform(point);
    }

    private void UpdatePopupPlacement()
    {
        var workArea = SystemParameters.WorkArea;
        var openToRight = Left + (Width / 2) < workArea.Left + (workArea.Width / 2);
        var placement = openToRight ? PlacementMode.Right : PlacementMode.Left;
        var horizontalOffset = openToRight ? 10 : -10;

        StatusPopup.Placement = placement;
        StatusPopup.HorizontalOffset = horizontalOffset;
        ResultPopup.Placement = placement;
        ResultPopup.HorizontalOffset = horizontalOffset;
        TimerPopup.Placement = placement;
        TimerPopup.HorizontalOffset = horizontalOffset;
    }

    private void ClosePopups()
    {
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;
        StatusPopup.IsOpen = false;
        ResultPopup.IsOpen = false;
    }

    private void HideToTray()
    {
        ClosePopups();
        Hide();
    }

    private void SelectMode(CaptureMode mode, bool showStatus)
    {
        _selectedMode = mode;
        CaptureButton.ToolTip = $"{CaptureModeInfo.DisplayName(mode)} 선택됨. 드래그하면 위치를 옮길 수 있습니다. 우클릭하면 이미지 뷰어, 설정, 종료 메뉴를 열 수 있습니다.";
        AutomationProperties.SetName(CaptureButton, $"{CaptureModeInfo.DisplayName(mode)} 모드 선택됨");
        StatusText.Text = CaptureModeInfo.PlaceholderStatus(mode);
        StatusText.Foreground = WpfBrushes.White;
        AutomationProperties.SetName(StatusText, StatusText.Text);

        if (!showStatus)
        {
            return;
        }

        StatusPopup.IsOpen = true;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private async Task ExecuteModeAsync(CaptureMode mode)
    {
        if (_captureInProgress)
        {
            return;
        }

        _captureInProgress = true;
        ResultPopup.IsOpen = false;

        try
        {
            var savedPath = mode switch
            {
                CaptureMode.Window => await CaptureWindowAsync(),
                CaptureMode.FullScreen => await CaptureFullScreenAsync(),
                _ => await CaptureDragRegionAsync()
            };

            if (string.IsNullOrWhiteSpace(savedPath))
            {
                ShowStatus("캡처가 취소되었습니다.");
                return;
            }

            ShowCaptureResult(savedPath);
        }
        catch (Exception ex)
        {
            ShowStatus($"캡처 실패: {ex.Message}");
        }
        finally
        {
            Show();
            Activate();
            _captureInProgress = false;
        }
    }

    private async Task<string?> CaptureFullScreenAsync()
    {
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;
        if (!await RunCountdownIfNeededAsync())
        {
            return null;
        }

        ShowStatus("전체 화면을 캡처합니다.");
        Hide();
        await Task.Delay(180);
        return ScreenCaptureService.CaptureFullScreen();
    }

    private async Task<string?> CaptureDragRegionAsync()
    {
        ShowStatus("캡처할 영역을 드래그하세요. Esc로 취소할 수 있습니다.");
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;

        if (_captureDelaySeconds > 0)
        {
            if (!await RunCountdownIfNeededAsync())
            {
                return null;
            }

            ShowStatus("캡처할 영역을 드래그하세요. Esc로 취소할 수 있습니다.");
        }

        Hide();

        var overlay = new RegionCaptureOverlay
        {
            Owner = null
        };

        var accepted = overlay.ShowDialog() == true;
        if (!accepted || overlay.SelectedRegion is not { } region)
        {
            return null;
        }

        return ScreenCaptureService.CaptureRegion(region);
    }

    private async Task<string?> CaptureWindowAsync()
    {
        ShowStatus("캡처할 창 위에 마우스를 올리고 클릭하세요. Esc로 취소할 수 있습니다.");
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;
        if (!await RunCountdownIfNeededAsync())
        {
            return null;
        }

        ShowStatus("캡처할 창 위에 마우스를 올리고 클릭하세요. Esc로 취소할 수 있습니다.");
        Hide();

        var overlay = new WindowPickerOverlay();
        var accepted = overlay.ShowDialog() == true;
        if (!accepted || overlay.SelectedTarget is not { } target)
        {
            return null;
        }

        await Task.Delay(180);
        return ScreenCaptureService.CaptureWindow(target);
    }

    private async Task<bool> RunCountdownIfNeededAsync()
    {
        if (_captureDelaySeconds <= 0)
        {
            return true;
        }

        _statusTimer.Stop();
        StatusPopup.IsOpen = true;
        StatusText.Foreground = WpfBrushes.Red;

        for (var remaining = _captureDelaySeconds; remaining > 0; remaining--)
        {
            StatusText.Text = $"{remaining}";
            AutomationProperties.SetName(StatusText, $"{remaining}초 후 캡처");
            System.Media.SystemSounds.Beep.Play();
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        StatusText.Foreground = WpfBrushes.White;
        return true;
    }

    private void ShowCaptureResult(string path)
    {
        _lastCapturePath = path;
        ResultPathText.Text = path;
        AutomationProperties.SetName(ResultPathText, path);
        AutomationProperties.SetName(CaptureButton, $"저장됨 {path}");
        StatusPopup.IsOpen = false;
        ResultPopup.IsOpen = true;
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = WpfBrushes.White;
        AutomationProperties.SetName(StatusText, message);
        StatusPopup.IsOpen = true;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapturePath is not null)
        {
            ShellService.OpenFile(_lastCapturePath);
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapturePath is not null)
        {
            ShellService.OpenContainingFolder(_lastCapturePath);
        }
    }

    private void OnOpenViewerClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapturePath is not null)
        {
            OpenInternalViewer(_lastCapturePath);
        }
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapturePath is null)
        {
            return;
        }

        try
        {
            WpfClipboard.SetText(_lastCapturePath);
            ShowStatus("파일 경로를 클립보드에 복사했습니다.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            ShowStatus($"경로 복사 실패: {ex.Message}");
        }
    }

    private void OnCopyImageClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapturePath is null || !File.Exists(_lastCapturePath))
        {
            return;
        }

        try
        {
            using var stream = File.Open(_lastCapturePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            WpfClipboard.SetImage(image);
            ShowStatus("이미지를 클립보드에 복사했습니다.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            ShowStatus($"이미지 복사 실패: {ex.Message}");
        }
    }

    private void OnDismissResultClick(object sender, RoutedEventArgs e)
    {
        ResultPopup.IsOpen = false;
    }

    private void OpenInternalViewer(string? path)
    {
        if (_viewerWindow is null || !_viewerWindow.IsVisible)
        {
            _viewerWindow = new ViewerWindow(path);
            _viewerWindow.Closed += (_, _) => _viewerWindow = null;
            _viewerWindow.Show();
            return;
        }

        _viewerWindow.OpenImage(path);
    }

    public void OpenViewer(string? path)
    {
        OpenInternalViewer(path);
    }

    public void OpenSettings()
    {
        try
        {
            if (_settingsWindow is null || !_settingsWindow.IsVisible)
            {
                _settingsWindow = new SettingsWindow
                {
                    Owner = IsVisible ? this : null
                };
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
                return;
            }

            _settingsWindow.Activate();
        }
        catch (Exception ex) when (ex is InvalidOperationException or XamlParseException)
        {
            ShowStatus($"설정을 열 수 없습니다: {ex.Message}");
        }
    }
}
