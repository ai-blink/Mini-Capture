using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace MiniCapture;

internal enum SettingsSection
{
    ExtensionDefaults,
    CaptureSettings,
    WindowExclusions
}

public partial class SettingsWindow : Window
{
    private readonly DispatcherTimer _associationRefreshTimer;
    private MiniCaptureSettings _captureSettings;
    private bool _isUpdatingWindowExclusionRule;

    public ObservableCollection<ExtensionAssociationItem> ExtensionItems { get; } =
    [
        new(".png", "Portable Network Graphics"),
        new(".jpg", "JPEG Image"),
        new(".jpeg", "JPEG Image")
    ];

    public ObservableCollection<CaptureHotkeyGroup> HotkeyGroups { get; }

    public ObservableCollection<RunningProcessItem> RunningProcessItems { get; } = [];

    public ObservableCollection<WindowExclusionItem> WindowExclusionItems { get; } = [];

    private IEnumerable<CaptureHotkeySettingItem> HotkeySettings =>
        HotkeyGroups.SelectMany(group => group.Slots);

    public SettingsWindow()
    {
        InitializeComponent();
        HotkeyGroups =
        [
            new(CaptureHotkeyKind.Window, "창 선택"),
            new(CaptureHotkeyKind.Region, "영역 선택"),
            new(CaptureHotkeyKind.FullScreen, "전체 화면 캡처"),
            new(CaptureHotkeyKind.Timer, "타이머 캡처")
        ];
        DataContext = this;
        _captureSettings = MiniCaptureSettingsStore.Load();

        _associationRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _associationRefreshTimer.Tick += (_, _) => RefreshAssociationStates("10초 자동 갱신");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadCaptureSettingsFields();
        LoadWindowExclusionFields();
        RefreshRunningProcesses();
        ExecutablePathText.Text = GetExecutablePath();
        ShowSection(SettingsSection.ExtensionDefaults);
        RefreshAssociationStates("설정 창 열림");
        _associationRefreshTimer.Start();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshAssociationStates("설정 창 활성화");
    }

    protected override void OnClosed(EventArgs e)
    {
        _associationRefreshTimer.Stop();
        base.OnClosed(e);
    }

    private void OnSectionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionList.SelectedItem is not ListBoxItem { Tag: string tag } ||
            !Enum.TryParse<SettingsSection>(tag, out var section))
        {
            return;
        }

        ShowSection(section);
    }

    private void OnOpenDefaultAppsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellService.OpenDefaultAppsSettings();
            SetStatus(BuildWindowsSettingsStatus("Windows 기본 앱 설정을 열었습니다."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            SetStatus($"Windows 기본 앱 설정을 열 수 없습니다: {ex.Message}");
        }
    }

    private void OnShowExtensionHelpClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            this,
            "Mini Capture는 PNG, JPG, JPEG 파일을 열 수 있는 앱으로 자동 등록됩니다.\n\n" +
            "Windows 보안 정책 때문에 앱이 기존 기본 앱을 몰래 바꿀 수는 없습니다. " +
            "마지막 선택은 Windows 설정에서 사용자가 직접 해야 합니다.\n\n" +
            "1. Windows 기본 앱에서 선택하기를 누릅니다.\n" +
            "2. .png, .jpg, .jpeg를 각각 검색합니다.\n" +
            "3. 현재 앱을 Mini Capture Viewer로 바꿉니다.\n\n" +
            "목록에 Mini Capture Viewer가 보이지 않으면 선택 목록 복구를 누른 뒤 다시 시도하세요.",
            "확장자 연결 설명",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnCopyExecutablePathClick(object sender, RoutedEventArgs e)
    {
        try
        {
            WpfClipboard.SetText(GetExecutablePath());
            SetStatus("실행 파일 경로를 복사했습니다.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetStatus($"경로를 복사할 수 없습니다: {ex.Message}");
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnRefreshAssociationsClick(object sender, RoutedEventArgs e)
    {
        RefreshAssociationStates("수동 새로고침");
    }

    private void OnRegisterAssociationCandidatesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            FileAssociationRegistrar.RegisterViewerCandidates(
                GetExecutablePath(),
                FileAssociationRegistrar.SupportedExtensions);
            RefreshAssociationStates("선택 목록 복구 완료");
            SetStatus("PNG/JPG/JPEG 선택 목록을 복구했습니다. Windows 기본 앱에서 Mini Capture Viewer를 선택하세요.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            SetStatus($"선택 목록 복구 실패: {ex.Message}");
        }
    }

    private void ShowSection(SettingsSection section)
    {
        if (ExtensionDefaultsPanel is null || CaptureSettingsPanel is null || WindowExclusionsPanel is null)
        {
            return;
        }

        ExtensionDefaultsPanel.Visibility = section == SettingsSection.ExtensionDefaults
            ? Visibility.Visible
            : Visibility.Collapsed;
        CaptureSettingsPanel.Visibility = section == SettingsSection.CaptureSettings
            ? Visibility.Visible
            : Visibility.Collapsed;
        WindowExclusionsPanel.Visibility = section == SettingsSection.WindowExclusions
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnSaveCaptureSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuildCaptureSettings(out var settings, out var error))
        {
            SetStatus(error);
            return;
        }

        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        LoadCaptureSettingsFields();
        SetStatus("캡처 설정을 저장했습니다. 단축키와 퀵 아이콘 설정이 즉시 반영됩니다.");
    }

    private void OnResetCaptureSettingsClick(object sender, RoutedEventArgs e)
    {
        _captureSettings = new MiniCaptureSettings
        {
            QuickButtonLeft = _captureSettings.QuickButtonLeft,
            QuickButtonTop = _captureSettings.QuickButtonTop
        };
        MiniCaptureSettingsStore.Save(_captureSettings);
        LoadCaptureSettingsFields();
        SetStatus("캡처 설정을 기본값으로 되돌렸습니다.");
    }

    private void LoadCaptureSettingsFields()
    {
        _captureSettings = MiniCaptureSettingsStore.Load();
        SetHotkeyTexts(CaptureHotkeyKind.Window, _captureSettings.GetHotkeySlots(CaptureHotkeyKind.Window));
        SetHotkeyTexts(CaptureHotkeyKind.Region, _captureSettings.GetHotkeySlots(CaptureHotkeyKind.Region));
        SetHotkeyTexts(CaptureHotkeyKind.FullScreen, _captureSettings.GetHotkeySlots(CaptureHotkeyKind.FullScreen));
        SetHotkeyTexts(CaptureHotkeyKind.Timer, _captureSettings.GetHotkeySlots(CaptureHotkeyKind.Timer));
        TimerDelayBox.Text = _captureSettings.TimerDelaySeconds.ToString();
        ShortcutTimerDelayBox.Text = _captureSettings.ShortcutTimerDelaySeconds.ToString();
        QuickButtonVisibleCheck.IsChecked = _captureSettings.QuickButtonVisible;
        CaptureUiExcludedCheck.IsChecked = _captureSettings.CaptureUiExcludedFromCapture;
    }

    private void LoadWindowExclusionFields()
    {
        _captureSettings = MiniCaptureSettingsStore.Load();
        WindowExclusionItems.Clear();
        foreach (var target in _captureSettings.WindowCaptureExclusionTargets)
        {
            WindowExclusionItems.Add(new WindowExclusionItem(target));
        }

        WindowExclusionList.SelectedIndex = WindowExclusionItems.Count > 0 ? 0 : -1;
        SyncWindowExclusionRuleSelector();
    }

    private void OnRefreshWindowExclusionProcessesClick(object sender, RoutedEventArgs e)
    {
        RefreshRunningProcesses();
        SetStatus($"실행 중인 프로세스 {RunningProcessItems.Count}개를 새로고침했습니다.");
    }

    private void OnAddRunningProcessExclusionClick(object sender, RoutedEventArgs e)
    {
        if (RunningProcessBox.SelectedItem is not RunningProcessItem process)
        {
            SetStatus("제외할 실행 중인 프로세스를 먼저 선택하세요.");
            return;
        }

        AddWindowExclusion(process.ExecutablePath, process.ProcessName);
    }

    private void OnBrowseWindowExclusionPathClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "창 지정에서 제외할 실행 파일 선택",
            CheckFileExists = true,
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            WindowExclusionPathBox.Text = dialog.FileName;
        }
    }

    private void OnAddWindowExclusionPathClick(object sender, RoutedEventArgs e)
    {
        if (!MiniCaptureSettings.TryNormalizeWindowCaptureExcludedExecutablePath(
                WindowExclusionPathBox.Text,
                out _))
        {
            SetStatus("존재하는 .exe 파일의 절대 경로를 입력하거나 파일 선택을 사용하세요.");
            return;
        }

        AddWindowExclusion(
            WindowExclusionPathBox.Text,
            Path.GetFileNameWithoutExtension(WindowExclusionPathBox.Text.Trim()));
    }

    private void OnRemoveWindowExclusionClick(object sender, RoutedEventArgs e)
    {
        if (WindowExclusionList.SelectedItem is not WindowExclusionItem selected)
        {
            SetStatus("삭제할 실행 파일을 목록에서 선택하세요.");
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var removed = settings.WindowCaptureExclusionTargets.RemoveAll(target =>
            target.HasSameIdentity(selected.Target));
        if (removed == 0)
        {
            SetStatus("선택한 제외 대상은 이미 목록에서 제거되었습니다.");
            LoadWindowExclusionFields();
            return;
        }

        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        LoadWindowExclusionFields();
        SetStatus($"{selected.DisplayName}을(를) 창 지정 제외 목록에서 제거했습니다.");
    }

    private void OnWindowExclusionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncWindowExclusionRuleSelector();
    }

    private void OnWindowExclusionRuleChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingWindowExclusionRule ||
            WindowExclusionList.SelectedItem is not WindowExclusionItem selected ||
            sender is not System.Windows.Controls.RadioButton { IsChecked: true })
        {
            return;
        }

        var matchMode = ReferenceEquals(sender, WindowExclusionFilePathRuleRadio)
            ? WindowCaptureExclusionMatchMode.FilePath
            : WindowCaptureExclusionMatchMode.ProcessName;
        if (matchMode == WindowCaptureExclusionMatchMode.FilePath && string.IsNullOrWhiteSpace(selected.ExecutablePath))
        {
            SetStatus("실행 파일 경로를 확인할 수 없는 대상은 프로세스명 기준으로만 제외할 수 있습니다.");
            SyncWindowExclusionRuleSelector();
            return;
        }

        if (selected.MatchMode == matchMode)
        {
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var target = settings.WindowCaptureExclusionTargets.FirstOrDefault(candidate =>
            candidate.HasSameIdentity(selected.Target));
        if (target is null)
        {
            SetStatus("선택한 제외 대상을 찾을 수 없어 목록을 새로고침했습니다.");
            LoadWindowExclusionFields();
            return;
        }

        target.MatchMode = matchMode;
        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        LoadWindowExclusionFields();
        WindowExclusionList.SelectedItem = WindowExclusionItems.FirstOrDefault(item =>
            item.Target.HasSameIdentity(selected.Target));
        SetStatus(matchMode == WindowCaptureExclusionMatchMode.FilePath
            ? "파일 경로 기준으로 전환했습니다. 프로세스명도 계속 저장됩니다."
            : "프로세스명 기준으로 전환했습니다. 실행 파일 경로도 계속 저장됩니다.");
    }

    private void RefreshRunningProcesses()
    {
        var selectedProcessId = (RunningProcessBox.SelectedItem as RunningProcessItem)?.ProcessId;
        var items = new List<RunningProcessItem>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                var executablePath = string.Empty;
                try
                {
                    MiniCaptureSettings.TryNormalizeWindowCaptureExcludedExecutablePath(
                        process.MainModule?.FileName,
                        out executablePath);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // The process remains selectable by its process name when Windows withholds the path.
                }

                items.Add(new RunningProcessItem(process.ProcessName, process.Id, executablePath));
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                // Some protected system processes do not expose their executable path.
            }
            finally
            {
                process.Dispose();
            }
        }

        RunningProcessItems.Clear();
        foreach (var item in items
                     .OrderBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.ProcessId))
        {
            RunningProcessItems.Add(item);
        }

        if (selectedProcessId is int processId)
        {
            RunningProcessBox.SelectedItem = RunningProcessItems.FirstOrDefault(item =>
                item.ProcessId == processId);
        }
    }

    private void AddWindowExclusion(string? executablePath, string? processName)
    {
        var hasExecutablePath = MiniCaptureSettings.TryNormalizeWindowCaptureExcludedExecutablePath(
            executablePath,
            out var normalizedPath);
        if (hasExecutablePath && !File.Exists(normalizedPath))
        {
            SetStatus("존재하는 .exe 파일의 절대 경로를 입력하거나 실행 중인 프로세스를 선택하세요.");
            return;
        }

        if (!MiniCaptureSettings.TryNormalizeWindowCaptureExcludedProcessName(processName, out var normalizedProcessName))
        {
            SetStatus("프로세스명을 확인할 수 없어 제외 대상을 저장하지 못했습니다.");
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var target = new WindowCaptureExclusionTarget
        {
            ExecutablePath = hasExecutablePath ? normalizedPath : string.Empty,
            ProcessName = normalizedProcessName,
            MatchMode = hasExecutablePath
                ? WindowCaptureExclusionMatchMode.FilePath
                : WindowCaptureExclusionMatchMode.ProcessName
        };
        if (settings.WindowCaptureExclusionTargets.Any(existing => existing.HasSameIdentity(target)))
        {
            SetStatus("해당 제외 대상은 이미 창 지정 제외 목록에 있습니다.");
            return;
        }

        settings.WindowCaptureExclusionTargets.Add(target);
        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        WindowExclusionPathBox.Clear();
        LoadWindowExclusionFields();
        WindowExclusionList.SelectedItem = WindowExclusionItems.FirstOrDefault(item => item.Target.HasSameIdentity(target));
        SetStatus(hasExecutablePath
            ? $"{Path.GetFileName(normalizedPath)}을(를) 파일 경로 기준으로 제외합니다. 프로세스명도 저장했습니다."
            : $"{normalizedProcessName}을(를) 프로세스명 기준으로 제외합니다.");
    }

    private void SyncWindowExclusionRuleSelector()
    {
        if (WindowExclusionFilePathRuleRadio is null || WindowExclusionProcessNameRuleRadio is null)
        {
            return;
        }

        _isUpdatingWindowExclusionRule = true;
        try
        {
            if (WindowExclusionList.SelectedItem is not WindowExclusionItem selected)
            {
                WindowExclusionFilePathRuleRadio.IsEnabled = false;
                WindowExclusionProcessNameRuleRadio.IsEnabled = false;
                WindowExclusionFilePathRuleRadio.IsChecked = false;
                WindowExclusionProcessNameRuleRadio.IsChecked = false;
                return;
            }

            WindowExclusionFilePathRuleRadio.IsEnabled = !string.IsNullOrWhiteSpace(selected.ExecutablePath);
            WindowExclusionProcessNameRuleRadio.IsEnabled = true;
            WindowExclusionFilePathRuleRadio.IsChecked = selected.MatchMode == WindowCaptureExclusionMatchMode.FilePath;
            WindowExclusionProcessNameRuleRadio.IsChecked = selected.MatchMode == WindowCaptureExclusionMatchMode.ProcessName;
        }
        finally
        {
            _isUpdatingWindowExclusionRule = false;
        }
    }

    private bool TryBuildCaptureSettings(out MiniCaptureSettings settings, out string error)
    {
        settings = _captureSettings.Clone();
        error = string.Empty;

        if (!int.TryParse(TimerDelayBox.Text, out var seconds) || seconds < 0 || seconds > 60)
        {
            error = "전역 타이머 시간은 0부터 60 사이의 초 단위 숫자로 입력하세요.";
            return false;
        }

        if (!int.TryParse(ShortcutTimerDelayBox.Text, out var shortcutTimerSeconds) ||
            shortcutTimerSeconds < 1 ||
            shortcutTimerSeconds > 60)
        {
            error = "타이머 단축키 시간은 1부터 60 사이의 초 단위 숫자로 입력하세요.";
            return false;
        }

        var normalizedByKind = new Dictionary<CaptureHotkeyKind, List<string>>();
        var allNonEmptyHotkeys = new List<(string Label, string Hotkey)>();
        foreach (var group in HotkeyGroups)
        {
            var normalizedHotkeys = new List<string>();
            foreach (var item in group.Slots)
            {
                if (string.IsNullOrWhiteSpace(item.HotkeyText))
                {
                    normalizedHotkeys.Add(string.Empty);
                    continue;
                }

                if (!CaptureHotkey.TryParse(item.HotkeyText, out var hotkey))
                {
                    error = $"{item.Label} 단축키를 Ctrl+Alt+R 같은 형식으로 입력하세요.";
                    return false;
                }

                normalizedHotkeys.Add(hotkey.DisplayText);
                allNonEmptyHotkeys.Add((item.Label, hotkey.DisplayText));
            }

            if (normalizedHotkeys.All(string.IsNullOrWhiteSpace))
            {
                error = $"{group.Label} 단축키를 하나 이상 지정하세요.";
                return false;
            }

            normalizedByKind[group.Kind] = normalizedHotkeys;
        }

        if (allNonEmptyHotkeys.Select(item => item.Hotkey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != allNonEmptyHotkeys.Count)
        {
            error = "모든 단축키 조합은 서로 달라야 합니다.";
            return false;
        }

        settings.SetHotkeySlots(CaptureHotkeyKind.Window, normalizedByKind[CaptureHotkeyKind.Window]);
        settings.SetHotkeySlots(CaptureHotkeyKind.Region, normalizedByKind[CaptureHotkeyKind.Region]);
        settings.SetHotkeySlots(CaptureHotkeyKind.FullScreen, normalizedByKind[CaptureHotkeyKind.FullScreen]);
        settings.SetHotkeySlots(CaptureHotkeyKind.Timer, normalizedByKind[CaptureHotkeyKind.Timer]);
        settings.TimerDelaySeconds = seconds;
        settings.ShortcutTimerDelaySeconds = shortcutTimerSeconds;
        settings.QuickButtonVisible = QuickButtonVisibleCheck.IsChecked == true;
        settings.CaptureUiExcludedFromCapture = CaptureUiExcludedCheck.IsChecked == true;
        return true;
    }

    private void OnDetectKeyPartClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: CaptureHotkeySettingItem item, Tag: string partText } ||
            !Enum.TryParse<CaptureHotkeyPart>(partText, out var part))
        {
            return;
        }

        SelectHotkeyList(item);
        var dialog = new HotkeyDetectDialog(item.Label, part)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.DetectedValue))
        {
            item.StatusText = "대기";
            SetStatus("키 감지를 취소했습니다.");
            e.Handled = true;
            return;
        }

        ApplyDetectedHotkeyPart(item, part, dialog.DetectedValue);
        item.StatusText = "감지 완료";
        SetStatus($"{item.Label} {CaptureHotkeyPartDisplayName(part)}을 {dialog.DetectedValue}(으)로 선택했습니다. 저장을 누르면 적용됩니다.");
        e.Handled = true;
    }

    private void SetHotkeyTexts(CaptureHotkeyKind kind, IReadOnlyList<string> hotkeys)
    {
        var group = HotkeyGroups.FirstOrDefault(setting => setting.Kind == kind);
        if (group is null)
        {
            return;
        }

        for (var index = 0; index < group.Slots.Count; index++)
        {
            group.Slots[index].HotkeyText = index < hotkeys.Count ? hotkeys[index] : string.Empty;
            group.Slots[index].StatusText = "대기";
        }
    }

    private void SelectHotkeyList(CaptureHotkeySettingItem item)
    {
        foreach (var setting in HotkeySettings)
        {
            setting.IsSelected = setting == item;
        }
    }

    private static void ApplyDetectedHotkeyPart(CaptureHotkeySettingItem item, CaptureHotkeyPart part, string value)
    {
        switch (part)
        {
            case CaptureHotkeyPart.First:
                item.FirstKey = value;
                break;
            case CaptureHotkeyPart.Second:
                item.SecondKey = value;
                break;
            case CaptureHotkeyPart.Main:
                item.MainKey = value;
                break;
        }
    }

    private static string CaptureHotkeyPartDisplayName(CaptureHotkeyPart part) =>
        part switch
        {
            CaptureHotkeyPart.First => "첫 번째 키",
            CaptureHotkeyPart.Second => "두 번째 키",
            _ => "세 번째 키"
        };

    private void SetStatus(string message)
    {
        SettingsStatusText.Text = message;
    }

    private void RefreshAssociationStates(string reason)
    {
        var executablePath = GetExecutablePath();
        var associatedCount = 0;
        var totalCount = 0;

        foreach (var item in GetExtensionItems())
        {
            var state = FileAssociationReader.Read(item.Extension, executablePath);
            item.ApplyState(state);
            totalCount++;
            if (item.IsAssociated)
            {
                associatedCount++;
            }
        }

        AssociationSummaryText.Text = $"PNG/JPG/JPEG {associatedCount}/{totalCount}개 연결됨";
        SetStatus($"{reason}: 전체 확장자 연결 상태를 갱신했습니다. {DateTime.Now:HH:mm:ss}");
    }

    private string BuildWindowsSettingsStatus(string prefix)
    {
        return $"{prefix} .png, .jpg, .jpeg의 현재 앱을 Mini Capture Viewer로 바꾸세요.";
    }

    private IEnumerable<ExtensionAssociationItem> GetExtensionItems()
    {
        return ExtensionItems;
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ??
            Process.GetCurrentProcess().MainModule?.FileName ??
            Path.Combine(AppContext.BaseDirectory, "MiniCapture.exe");
    }
}

public sealed class RunningProcessItem(string processName, int processId, string executablePath)
{
    public string ProcessName { get; } = processName;

    public int ProcessId { get; } = processId;

    public string ExecutablePath { get; } = executablePath;

    public string DisplayName => string.IsNullOrWhiteSpace(ExecutablePath)
        ? $"{ProcessName} (PID {ProcessId}, 경로 확인 불가)"
        : $"{ProcessName} (PID {ProcessId})";
}

public sealed class WindowExclusionItem(WindowCaptureExclusionTarget target)
{
    public WindowCaptureExclusionTarget Target { get; } = target.Clone();

    public string ExecutablePath => Target.ExecutablePath;

    public WindowCaptureExclusionMatchMode MatchMode => Target.MatchMode;

    public string DisplayName => Target.ProcessName;

    public string AppliedRuleText => MatchMode == WindowCaptureExclusionMatchMode.FilePath
        ? "적용 기준: 파일 경로"
        : "적용 기준: 프로세스명";
}

public enum CaptureHotkeyKind
{
    Window,
    Region,
    FullScreen,
    Timer
}

public enum CaptureHotkeyPart
{
    First,
    Second,
    Main
}

public sealed class CaptureHotkeyGroup
{
    public CaptureHotkeyGroup(CaptureHotkeyKind kind, string label)
    {
        Kind = kind;
        Label = label;
        Slots =
        [
            new(kind, label, 1)
        ];
    }

    public CaptureHotkeyKind Kind { get; }

    public string Label { get; }

    public ObservableCollection<CaptureHotkeySettingItem> Slots { get; }
}

public sealed class CaptureHotkeySettingItem(
    CaptureHotkeyKind kind,
    string label,
    int slotNumber) : INotifyPropertyChanged
{
    private string _firstKey = string.Empty;
    private string _secondKey = string.Empty;
    private string _mainKey = string.Empty;
    private string _statusText = "대기";
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CaptureHotkeyKind Kind { get; } = kind;

    public string Label { get; } = label;

    public int SlotNumber { get; } = slotNumber;

    public string HotkeyAutomationName => $"{Label} 단축키";

    public string DetectAutomationName => $"{Label} 키 감지";

    public string FirstKeyAutomationId => $"{Kind}Hotkey{SlotNumber}FirstKeyCombo";

    public string SecondKeyAutomationId => $"{Kind}Hotkey{SlotNumber}SecondKeyCombo";

    public string MainKeyAutomationId => $"{Kind}Hotkey{SlotNumber}MainKeyCombo";

    public string FirstKeyAutomationName => $"{Label} 첫 번째 키";

    public string SecondKeyAutomationName => $"{Label} 두 번째 키";

    public string MainKeyAutomationName => $"{Label} 세 번째 키";

    public string SlotLabel => "단축키";

    public string PlusText => string.Empty;

    public string ListAutomationId => $"{Kind}HotkeyList{SlotNumber}";

    public string DetectAutomationId => $"Detect{Kind}HotkeyButton{SlotNumber}";

    public string DetectFirstKeyAutomationId => $"Detect{Kind}Hotkey{SlotNumber}FirstKeyButton";

    public string DetectSecondKeyAutomationId => $"Detect{Kind}Hotkey{SlotNumber}SecondKeyButton";

    public string DetectMainKeyAutomationId => $"Detect{Kind}Hotkey{SlotNumber}MainKeyButton";

    public string DetectFirstKeyAutomationName => $"{Label} 첫 번째 키 감지";

    public string DetectSecondKeyAutomationName => $"{Label} 두 번째 키 감지";

    public string DetectMainKeyAutomationName => $"{Label} 세 번째 키 감지";

    public IReadOnlyList<string> ModifierOptions { get; } =
    [
        "",
        "Ctrl",
        "Alt",
        "Shift",
        "Win"
    ];

    public IReadOnlyList<string> MainKeyOptions { get; } =
    [
        "",
        "A",
        "B",
        "C",
        "D",
        "E",
        "F",
        "G",
        "H",
        "I",
        "J",
        "K",
        "L",
        "M",
        "N",
        "O",
        "P",
        "Q",
        "R",
        "S",
        "T",
        "U",
        "V",
        "W",
        "X",
        "Y",
        "Z",
        "D0",
        "D1",
        "D2",
        "D3",
        "D4",
        "D5",
        "D6",
        "D7",
        "D8",
        "D9"
    ];

    public IEnumerable<CaptureHotkeySettingItem> SingleItem
    {
        get
        {
            yield return this;
        }
    }

    public string DisplayHotkeyText => string.IsNullOrWhiteSpace(HotkeyText) ? "(비어 있음)" : HotkeyText;

    public string HotkeyText
    {
        get
        {
            var parts = new[] { FirstKey, SecondKey, MainKey }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            return parts.Length == 0 ? string.Empty : string.Join("+", parts);
        }
        set
        {
            ApplyHotkeyText(value);
        }
    }

    public string FirstKey
    {
        get => _firstKey;
        set => SetKeyPart(ref _firstKey, value, nameof(FirstKey));
    }

    public string SecondKey
    {
        get => _secondKey;
        set => SetKeyPart(ref _secondKey, value, nameof(SecondKey));
    }

    public string MainKey
    {
        get => _mainKey;
        set => SetKeyPart(ref _mainKey, value, nameof(MainKey));
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value, nameof(StatusText));
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value, nameof(IsSelected));
    }

    private void ApplyHotkeyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FirstKey = string.Empty;
            SecondKey = string.Empty;
            MainKey = string.Empty;
            return;
        }

        if (!CaptureHotkey.TryParse(text, out var hotkey))
        {
            MainKey = text.Trim();
            return;
        }

        var modifiers = new List<string>();
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers.Add("Ctrl");
        }

        if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers.Add("Alt");
        }

        if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers.Add("Shift");
        }

        if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers.Add("Win");
        }

        FirstKey = modifiers.ElementAtOrDefault(0) ?? string.Empty;
        SecondKey = modifiers.ElementAtOrDefault(1) ?? string.Empty;
        MainKey = hotkey.Key.ToString();
    }

    private void SetKeyPart(ref string field, string? value, string propertyName)
    {
        if (SetField(ref field, value ?? string.Empty, propertyName))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HotkeyText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayHotkeyText)));
        }
    }

    private bool SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class ExtensionAssociationItem(
    string extension,
    string displayName) : INotifyPropertyChanged
{
    private bool _isAssociated;
    private string _statusText = "확인 대기";
    private string _statusDetail = string.Empty;
    private WpfBrush _statusBrush = WpfBrushes.LightGray;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Extension { get; } = extension;

    public string DisplayName { get; } = displayName;

    public string ChangeAutomationName => $"{Extension} Windows 기본 앱 변경";

    public bool IsAssociated
    {
        get => _isAssociated;
        private set => SetField(ref _isAssociated, value, nameof(IsAssociated));
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value, nameof(StatusText));
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetField(ref _statusDetail, value, nameof(StatusDetail));
    }

    public WpfBrush StatusBrush
    {
        get => _statusBrush;
        private set => SetField(ref _statusBrush, value, nameof(StatusBrush));
    }

    public void ApplyState(FileAssociationState state)
    {
        IsAssociated = state.IsAssociated;
        StatusText = state.StatusText;
        StatusDetail = state.Detail;
        StatusBrush = state.IsAssociated
            ? WpfBrushes.LightGreen
            : WpfBrushes.LightSteelBlue;
    }

    private void SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record FileAssociationState(bool IsAssociated, string StatusText, string Detail);

public static class FileAssociationReader
{
    public static FileAssociationState Read(string extension, string executablePath)
    {
        try
        {
            var userChoiceProgId = ReadString(
                Registry.CurrentUser,
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice",
                "ProgId");
            var defaultProgId = ReadString(Registry.ClassesRoot, extension, null);
            var progId = FirstNonEmpty(userChoiceProgId, defaultProgId);
            var command = string.IsNullOrWhiteSpace(progId)
                ? null
                : ReadString(Registry.ClassesRoot, $@"{progId}\shell\open\command", null);

            var associated = IsMiniCaptureReference(userChoiceProgId, executablePath) ||
                IsMiniCaptureReference(defaultProgId, executablePath) ||
                IsMiniCaptureReference(command, executablePath);
            var status = associated
                ? "Mini Capture"
                : string.IsNullOrWhiteSpace(progId) ? "미지정" : progId;
            var detail = command is null
                ? $"UserChoice={userChoiceProgId ?? "(없음)"}, 기본={defaultProgId ?? "(없음)"}"
                : $"UserChoice={userChoiceProgId ?? "(없음)"}, 기본={defaultProgId ?? "(없음)"}, 명령={command}";

            return new FileAssociationState(associated, status, detail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new FileAssociationState(false, "확인 실패", ex.Message);
        }
    }

    private static string? ReadString(RegistryKey root, string subKeyName, string? valueName)
    {
        using var key = root.OpenSubKey(subKeyName);
        return key?.GetValue(valueName) as string;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool IsMiniCaptureReference(string? value, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var executableName = Path.GetFileName(executablePath);
        return value.Contains("MiniCapture", StringComparison.OrdinalIgnoreCase) ||
            value.Contains(executablePath, StringComparison.OrdinalIgnoreCase) ||
            value.Contains(executableName, StringComparison.OrdinalIgnoreCase);
    }
}
