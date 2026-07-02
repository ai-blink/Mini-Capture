using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MiniCapture;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _statusTimer;
    private bool _captureInProgress;
    private string? _lastCapturePath;
    private CaptureMode _selectedMode = CaptureMode.Drag;

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
        SelectMode(_selectedMode, showStatus: false);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowApi.TryExcludeFromCapture(new WindowInteropHelper(this).Handle);
    }

    private void OnCaptureButtonClick(object sender, RoutedEventArgs e)
    {
        ModePopup.IsOpen = !ModePopup.IsOpen;
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

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        ModePopup.IsOpen = false;
        StatusPopup.IsOpen = false;
        ResultPopup.IsOpen = false;
        e.Handled = true;
    }

    private void PlaceNearWorkAreaCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 12, workArea.Right - Width - 24);
        Top = Math.Max(workArea.Top + 12, workArea.Bottom - Height - 24);
    }

    private void SelectMode(CaptureMode mode, bool showStatus)
    {
        _selectedMode = mode;
        ModeShortText.Text = CaptureModeInfo.ShortLabel(mode);
        CaptureButton.ToolTip = $"{CaptureModeInfo.DisplayName(mode)} 선택됨. 우클릭하면 종료할 수 있습니다.";
        AutomationProperties.SetName(CaptureButton, $"{CaptureModeInfo.DisplayName(mode)} 모드 선택됨");
        StatusText.Text = CaptureModeInfo.PlaceholderStatus(mode);
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
                CaptureMode.FullScreen => await CaptureFullScreenAsync(TimeSpan.FromMilliseconds(180)),
                CaptureMode.Timer => await CaptureFullScreenAsync(TimeSpan.FromSeconds(3)),
                _ => CaptureDragRegion()
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

    private async Task<string?> CaptureFullScreenAsync(TimeSpan delay)
    {
        ShowStatus(delay.TotalSeconds >= 1
            ? $"{delay.TotalSeconds:0}초 후 전체 화면을 캡처합니다."
            : "전체 화면을 캡처합니다.");

        ModePopup.IsOpen = false;
        await Task.Delay(delay);
        Hide();
        await Task.Delay(180);
        return ScreenCaptureService.CaptureFullScreen();
    }

    private string? CaptureDragRegion()
    {
        ShowStatus("캡처할 영역을 드래그하세요. Esc로 취소할 수 있습니다.");
        ModePopup.IsOpen = false;
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
            ShellService.OpenFile(_lastCapturePath);
        }
    }

    private void OnDismissResultClick(object sender, RoutedEventArgs e)
    {
        ResultPopup.IsOpen = false;
    }
}
