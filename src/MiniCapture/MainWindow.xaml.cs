using System.IO;
using MiniCapture.Resources;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfToolTip = System.Windows.Controls.ToolTip;

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
    private int _shortcutTimerDelaySeconds;
    private MiniCaptureSettings _settings;
    private CaptureHotkeyManager? _hotkeyManager;

    public MainWindow()
    {
        InitializeComponent();
        _settings = MiniCaptureSettingsStore.Load();
        _captureDelaySeconds = _settings.TimerDelaySeconds;
        _shortcutTimerDelaySeconds = _settings.ShortcutTimerDelaySeconds;
        MiniCaptureSettingsStore.SettingsChanged += OnSettingsChanged;

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
        ApplyStoredPosition();
        ApplyTimerButtonText();
        UpdatePopupPlacement();
        SelectMode(_selectedMode, showStatus: false);
        ApplyQuickButtonVisibility();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        ApplyCaptureExclusion(helper.Handle);

        if (HwndSource.FromHwnd(helper.Handle) is { } source)
        {
            _hotkeyManager = new CaptureHotkeyManager(source, mode => Dispatcher.Invoke(() => ExecuteModeFromHotkey(mode)));
            var hotkeyStatus = _hotkeyManager.Register(_settings);
            if (hotkeyStatus.StartsWith("등록 실패", StringComparison.Ordinal))
            {
                ShowStatus(hotkeyStatus);
            }
        }
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

    private void OnOverlayPopupOpened(object? sender, EventArgs e)
    {
        if (sender is Popup popup)
        {
            ApplyCaptureExclusion(popup);
        }
    }

    private void OnCaptureButtonContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ApplyCaptureExclusion(menu);
        }
    }

    private void OnOverlayToolTipOpened(object sender, RoutedEventArgs e)
    {
        if (sender is WpfToolTip toolTip)
        {
            ApplyCaptureExclusion(toolTip);
        }
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
        _settings.TimerDelaySeconds = seconds;
        MiniCaptureSettingsStore.Save(_settings);
        ApplyTimerButtonText();
        TimerPopup.IsOpen = false;
        ShowStatus(string.Format(Strings.Main_Status_GlobalDelaySet, seconds));
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

    protected override void OnClosed(EventArgs e)
    {
        MiniCaptureSettingsStore.SettingsChanged -= OnSettingsChanged;
        _hotkeyManager?.Dispose();
        base.OnClosed(e);
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
            SaveQuickButtonPosition();
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

    private void ApplyStoredPosition()
    {
        if (_settings.QuickButtonLeft is double left && _settings.QuickButtonTop is double top)
        {
            MoveWithinWorkArea(left, top);
            return;
        }

        PlaceNearWorkAreaCorner();
        SaveQuickButtonPosition();
    }

    private void SaveQuickButtonPosition()
    {
        _settings.QuickButtonLeft = Left;
        _settings.QuickButtonTop = Top;
        MiniCaptureSettingsStore.Save(_settings, notify: false);
    }

    private void ApplyQuickButtonVisibility()
    {
        if (_settings.QuickButtonVisible)
        {
            if (!IsVisible)
            {
                Show();
            }

            return;
        }

        ClosePopups();
        if (_settingsWindow is not null)
        {
            _settingsWindow.Owner = null;
        }

        Hide();
    }

    private void ApplyTimerButtonText()
    {
        ModeTimerButton.Content = _captureDelaySeconds > 0 ? string.Format(Strings.Main_SecondsFormat, _captureDelaySeconds) : Strings.Main_Mode_Timer;
        AutomationProperties.SetName(ModeTimerButton, _captureDelaySeconds > 0
            ? $"Timer delay {_captureDelaySeconds} seconds"
            : "Timer delay off");
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
        CaptureButton.ToolTip = string.Format(Strings.Main_CaptureButtonModeTooltip, CaptureModeInfo.DisplayName(mode));
        AutomationProperties.SetName(CaptureButton, string.Format(Strings.Main_CaptureButtonModeName, CaptureModeInfo.DisplayName(mode)));
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
                CaptureMode.Timer => await CaptureTimerFullScreenAsync(),
                CaptureMode.FullScreen => await CaptureFullScreenAsync(),
                _ => await CaptureDragRegionAsync()
            };

            if (string.IsNullOrWhiteSpace(savedPath))
            {
                ShowStatus(Strings.Main_Status_Cancelled);
                return;
            }

            ShowCaptureResult(savedPath);
        }
        catch (Exception ex)
        {
            ShowStatus(string.Format(Strings.Main_Status_CaptureFailed, ex.Message));
        }
        finally
        {
            if (_settings.QuickButtonVisible)
            {
                Show();
                Activate();
            }
            else
            {
                Hide();
            }

            _captureInProgress = false;
        }
    }

    private async void ExecuteModeFromHotkey(CaptureMode mode)
    {
        SelectMode(mode, showStatus: true);
        ClosePopups();
        await ExecuteModeAsync(mode);
    }

    private void OnSettingsChanged(object? sender, MiniCaptureSettings settings)
    {
        _settings = settings.Clone();
        _captureDelaySeconds = Math.Clamp(_settings.TimerDelaySeconds, 0, 60);
        _shortcutTimerDelaySeconds = Math.Clamp(_settings.ShortcutTimerDelaySeconds, 1, 60);
        ApplyCaptureExclusion(new WindowInteropHelper(this).Handle);
        ApplyCaptureExclusion(ModePopup);
        ApplyCaptureExclusion(TimerPopup);
        ApplyCaptureExclusion(StatusPopup);
        ApplyCaptureExclusion(ResultPopup);
        ApplyTimerButtonText();
        ApplyStoredPosition();
        UpdatePopupPlacement();
        ApplyQuickButtonVisibility();

        if (_hotkeyManager is not null)
        {
            ShowStatus(_hotkeyManager.Register(_settings));
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

        ShowStatus(Strings.Main_Status_CapturingFull);
        Hide();
        await Task.Delay(180);
        return ScreenCaptureService.CaptureFullScreen();
    }

    private async Task<string?> CaptureTimerFullScreenAsync()
    {
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;

        if (!await RunCountdownIfNeededAsync(_shortcutTimerDelaySeconds))
        {
            return null;
        }

        ShowStatus(Strings.Main_Status_CapturingTimerFull);
        Hide();
        await Task.Delay(180);
        return ScreenCaptureService.CaptureFullScreen();
    }

    private async Task<string?> CaptureDragRegionAsync()
    {
        ShowStatus(Strings.Main_Status_DragRegion);
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;

        ScreenCaptureSnapshot? frozenSnapshot = null;
        if (_captureDelaySeconds > 0)
        {
            if (!await RunCountdownIfNeededAsync())
            {
                return null;
            }

            ShowStatus(Strings.Main_Status_FreezingTimer);
            Hide();
            await Task.Delay(180);
            frozenSnapshot = ScreenCaptureService.CaptureSnapshot();
            ShowStatus(Strings.Main_Status_DragRegionFrozen);
        }
        else
        {
            Hide();
        }

        try
        {
            var overlay = frozenSnapshot is null
                ? new RegionCaptureOverlay()
                : new RegionCaptureOverlay(frozenSnapshot.CreatePreviewSource());
            overlay.Owner = null;

            var accepted = overlay.ShowDialog() == true;
            if (!accepted || overlay.SelectedRegion is not { } region)
            {
                return null;
            }

            return frozenSnapshot is null
                ? ScreenCaptureService.CaptureRegion(region)
                : ScreenCaptureService.SaveSnapshotRegion(frozenSnapshot, region);
        }
        finally
        {
            frozenSnapshot?.Dispose();
        }
    }

    private async Task<string?> CaptureWindowAsync()
    {
        ShowStatus(Strings.Main_Status_PickWindow);
        ModePopup.IsOpen = false;
        TimerPopup.IsOpen = false;
        if (!await RunCountdownIfNeededAsync())
        {
            return null;
        }

        ShowStatus(Strings.Main_Status_PickWindow);
        Hide();

        ScreenCaptureSnapshot? frozenSnapshot = null;
        IReadOnlyList<WindowCaptureTarget>? frozenTargets = null;
        if (_captureDelaySeconds > 0)
        {
            await Task.Delay(180);
            frozenTargets = WindowPickerService.GetSelectableTargetsInZOrder(Environment.ProcessId);
            frozenSnapshot = ScreenCaptureService.CaptureSnapshot();
            ShowStatus(Strings.Main_Status_PickWindowFrozen);
        }

        try
        {
            var overlay = frozenSnapshot is null || frozenTargets is null
                ? new WindowPickerOverlay()
                : new WindowPickerOverlay(frozenSnapshot.CreatePreviewSource(), frozenTargets);

            var accepted = overlay.ShowDialog() == true;
            if (!accepted || overlay.SelectedTarget is not { } target)
            {
                return null;
            }

            if (frozenSnapshot is not null)
            {
                return ScreenCaptureService.SaveSnapshotRegion(frozenSnapshot, target.Bounds);
            }

            await Task.Delay(180);
            return ScreenCaptureService.CaptureWindow(target);
        }
        finally
        {
            frozenSnapshot?.Dispose();
        }
    }

    private async Task<bool> RunCountdownIfNeededAsync(int? delaySeconds = null)
    {
        var seconds = delaySeconds ?? _captureDelaySeconds;
        if (seconds <= 0)
        {
            return true;
        }

        _statusTimer.Stop();
        StatusPopup.IsOpen = true;
        StatusText.Foreground = WpfBrushes.Red;

        for (var remaining = seconds; remaining > 0; remaining--)
        {
            StatusText.Text = $"{remaining}";
            AutomationProperties.SetName(StatusText, string.Format(Strings.Main_CountdownName, remaining));
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
        AutomationProperties.SetName(CaptureButton, string.Format(Strings.Main_SavedName, path));
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

    private async void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapturePath is null)
        {
            return;
        }

        try
        {
            await ClipboardService.SetTextAsync(_lastCapturePath);
            ShowStatus(Strings.Main_Status_PathCopied);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            ShowStatus(string.Format(Strings.Main_Status_PathCopyFailed, ex.Message));
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
            ClipboardService.SetImage(image);
            ShowStatus(Strings.Main_Status_ImageCopied);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            ShowStatus(string.Format(Strings.Main_Status_ImageCopyFailed, ex.Message));
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
            _viewerWindow.RestoreAndActivate();
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
                _hotkeyManager?.Suspend();
                _settingsWindow = new SettingsWindow
                {
                    Owner = IsVisible ? this : null
                };
                _settingsWindow.Closed += (_, _) =>
                {
                    _settingsWindow = null;
                    if (_hotkeyManager is not null)
                    {
                        ShowStatus(_hotkeyManager.Register(_settings));
                    }
                };
                _settingsWindow.Show();
                return;
            }

            _settingsWindow.Activate();
        }
        catch (Exception ex) when (ex is InvalidOperationException or XamlParseException)
        {
            ShowStatus(string.Format(Strings.Main_Status_SettingsOpenFailed, ex.Message));
        }
    }

    private void ApplyCaptureExclusion(Popup popup)
    {
        if (popup.Child is not { } child)
        {
            return;
        }

        if (PresentationSource.FromVisual(child) is HwndSource source)
        {
            ApplyCaptureExclusion(source.Handle);
        }
    }

    private void ApplyCaptureExclusion(ContextMenu menu)
    {
        if (PresentationSource.FromVisual(menu) is HwndSource source)
        {
            ApplyCaptureExclusion(source.Handle);
        }
    }

    private void ApplyCaptureExclusion(WpfToolTip toolTip)
    {
        if (PresentationSource.FromVisual(toolTip) is HwndSource source)
        {
            ApplyCaptureExclusion(source.Handle);
        }
    }

    private void ApplyCaptureExclusion(IntPtr hwnd)
    {
        NativeWindowApi.TrySetCaptureExclusion(hwnd, _settings.CaptureUiExcludedFromCapture);
    }
}
