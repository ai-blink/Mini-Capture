using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MiniCapture;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _statusTimer;
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

    private void OnCaptureButtonClick(object sender, RoutedEventArgs e)
    {
        ModePopup.IsOpen = !ModePopup.IsOpen;
    }

    private void OnModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        SelectMode(CaptureModeInfo.FromKey(button.Tag as string), showStatus: true);
        ModePopup.IsOpen = false;
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        ModePopup.IsOpen = false;
        StatusPopup.IsOpen = false;
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
}
