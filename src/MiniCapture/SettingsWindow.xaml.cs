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
    CaptureSettings
}

public partial class SettingsWindow : Window
{
    private readonly DispatcherTimer _associationRefreshTimer;
    private readonly DispatcherTimer _hotkeyDetectTimer;
    private MiniCaptureSettings _captureSettings;
    private CaptureHotkeySettingItem? _detectingHotkey;
    private DateTime _hotkeyDetectDeadline;

    public ObservableCollection<ExtensionCategory> ExtensionGroups { get; } =
    [
        new(
            "기본 이미지",
            "V1에서 파일 인자 진입을 검증한 기본 대상",
            [
                new ExtensionAssociationItem(".png", "Portable Network Graphics"),
                new ExtensionAssociationItem(".jpg", "JPEG Image"),
                new ExtensionAssociationItem(".jpeg", "JPEG Image")
            ]),
        new(
            "추가 이미지",
            "Windows 기본 앱 화면에서 함께 확인할 이미지 포맷",
            [
                new ExtensionAssociationItem(".bmp", "Bitmap Image"),
                new ExtensionAssociationItem(".gif", "Graphics Interchange Format"),
                new ExtensionAssociationItem(".webp", "WebP Image"),
                new ExtensionAssociationItem(".tif", "TIFF Image"),
                new ExtensionAssociationItem(".tiff", "TIFF Image")
            ])
    ];

    public ObservableCollection<CaptureHotkeyGroup> HotkeyGroups { get; }

    private IEnumerable<CaptureHotkeySettingItem> HotkeySettings =>
        HotkeyGroups.SelectMany(group => group.Slots);

    public SettingsWindow()
    {
        InitializeComponent();
        HotkeyGroups =
        [
            new(CaptureHotkeyKind.Window, "창 선택"),
            new(CaptureHotkeyKind.Region, "영역 선택"),
            new(CaptureHotkeyKind.FullScreen, "전체 화면 캡처")
        ];
        DataContext = this;
        _captureSettings = MiniCaptureSettingsStore.Load();

        _associationRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _associationRefreshTimer.Tick += (_, _) => RefreshAssociationStates("10초 자동 갱신");

        _hotkeyDetectTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _hotkeyDetectTimer.Tick += (_, _) => UpdateHotkeyDetectCountdown();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadCaptureSettingsFields();
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
        CancelHotkeyDetection("키 감지를 취소했습니다.");
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

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in GetExtensionItems())
        {
            item.IsSelected = true;
        }

        SetStatus("모든 확장자를 연결 요청 후보로 선택했습니다.");
    }

    private void OnClearSelectionClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in GetExtensionItems())
        {
            item.IsSelected = false;
        }

        SetStatus("연결 요청 후보 선택을 비웠습니다.");
    }

    private void OnRefreshAssociationsClick(object sender, RoutedEventArgs e)
    {
        RefreshAssociationStates("수동 새로고침");
    }

    private void ShowSection(SettingsSection section)
    {
        if (ExtensionDefaultsPanel is null || CaptureSettingsPanel is null)
        {
            return;
        }

        ExtensionDefaultsPanel.Visibility = section == SettingsSection.ExtensionDefaults
            ? Visibility.Visible
            : Visibility.Collapsed;
        CaptureSettingsPanel.Visibility = section == SettingsSection.CaptureSettings
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
        TimerDelayBox.Text = _captureSettings.TimerDelaySeconds.ToString();
        QuickButtonVisibleCheck.IsChecked = _captureSettings.QuickButtonVisible;
    }

    private bool TryBuildCaptureSettings(out MiniCaptureSettings settings, out string error)
    {
        settings = _captureSettings.Clone();
        error = string.Empty;

        if (!int.TryParse(TimerDelayBox.Text, out var seconds) || seconds < 0 || seconds > 60)
        {
            error = "타이머 시간은 0부터 60 사이의 초 단위 숫자로 입력하세요.";
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
        settings.TimerDelaySeconds = seconds;
        settings.QuickButtonVisible = QuickButtonVisibleCheck.IsChecked == true;
        return true;
    }

    private void OnDetectHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: CaptureHotkeySettingItem item })
        {
            return;
        }

        CancelHotkeyDetection(null);
        _detectingHotkey = item;
        _hotkeyDetectDeadline = DateTime.UtcNow.AddSeconds(5);
        item.StatusText = "5초 안에 단축키를 누르세요.";
        SelectHotkeyList(item);
        Focus();
        Keyboard.Focus(this);
        _hotkeyDetectTimer.Start();
        SetStatus($"{item.Label} 단축키 감지 중입니다.");
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_detectingHotkey is not { } item)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            item.StatusText = "Ctrl, Alt, Shift 중 하나와 함께 누르세요.";
            e.Handled = true;
            return;
        }

        var hotkey = new CaptureHotkey(modifiers, key);
        item.HotkeyText = hotkey.DisplayText;
        item.StatusText = "감지 완료";
        _detectingHotkey = null;
        _hotkeyDetectTimer.Stop();
        SetStatus($"{item.Label} 단축키를 {hotkey.DisplayText}(으)로 선택했습니다. 저장을 누르면 적용됩니다.");
        e.Handled = true;
    }

    private void UpdateHotkeyDetectCountdown()
    {
        if (_detectingHotkey is not { } item)
        {
            _hotkeyDetectTimer.Stop();
            return;
        }

        var remaining = (int)Math.Ceiling((_hotkeyDetectDeadline - DateTime.UtcNow).TotalSeconds);
        if (remaining <= 0)
        {
            CancelHotkeyDetection("키 감지 시간이 초과되었습니다.");
            return;
        }

        item.StatusText = $"{remaining}초 안에 단축키를 누르세요.";
        SetStatus($"{item.Label} 단축키 감지 중: {remaining}초 남음");
    }

    private void CancelHotkeyDetection(string? status)
    {
        _hotkeyDetectTimer.Stop();
        if (_detectingHotkey is not { } item)
        {
            return;
        }

        item.StatusText = "대기";
        _detectingHotkey = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            SetStatus(status);
        }
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

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

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
            totalCount++;
            var state = FileAssociationReader.Read(item.Extension, executablePath);
            item.ApplyState(state);

            if (item.IsAssociated)
            {
                associatedCount++;
            }
        }

        AssociationSummaryText.Text = $"{associatedCount}/{totalCount}개 연결됨";
        SetStatus($"{reason}: 전체 확장자 연결 상태를 갱신했습니다. {DateTime.Now:HH:mm:ss}");
    }

    private string BuildWindowsSettingsStatus(string prefix)
    {
        var selected = GetExtensionItems()
            .Where(item => item.IsSelected)
            .Select(item => item.Extension)
            .ToArray();

        return selected.Length == 0
            ? $"{prefix} 연결할 확장자를 선택한 뒤 MiniCapture.exe를 기본 앱/Open With 대상으로 지정하세요."
            : $"{prefix} 선택 후보: {string.Join(", ", selected)}. MiniCapture.exe 경로를 기본 앱/Open With 대상에 사용하세요.";
    }

    private IEnumerable<ExtensionAssociationItem> GetExtensionItems()
    {
        return ExtensionGroups.SelectMany(group => group.Extensions);
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ??
            Process.GetCurrentProcess().MainModule?.FileName ??
            Path.Combine(AppContext.BaseDirectory, "MiniCapture.exe");
    }
}

public sealed class ExtensionCategory(
    string name,
    string description,
    IEnumerable<ExtensionAssociationItem> extensions)
{
    public string Name { get; } = name;

    public string Description { get; } = description;

    public ObservableCollection<ExtensionAssociationItem> Extensions { get; } = new(extensions);
}

public enum CaptureHotkeyKind
{
    Window,
    Region,
    FullScreen
}

public sealed class CaptureHotkeyGroup
{
    public CaptureHotkeyGroup(CaptureHotkeyKind kind, string label)
    {
        Kind = kind;
        Label = label;
        Slots =
        [
            new(kind, $"{label} 조합 1", 1),
            new(kind, $"{label} 조합 2", 2),
            new(kind, $"{label} 조합 3", 3)
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

    public string SlotLabel => $"단축키 {SlotNumber}";

    public string PlusText => SlotNumber < 3 ? "+" : string.Empty;

    public string ListAutomationId => $"{Kind}HotkeyList{SlotNumber}";

    public string DetectAutomationId => $"Detect{Kind}HotkeyButton{SlotNumber}";

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

public sealed class ExtensionAssociationItem(string extension, string displayName) : INotifyPropertyChanged
{
    private bool _isAssociated;
    private bool _isSelected;
    private string _statusText = "확인 대기";
    private string _statusDetail = string.Empty;
    private WpfBrush _statusBrush = WpfBrushes.LightGray;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Extension { get; } = extension;

    public string DisplayName { get; } = displayName;

    public string CurrentStateAutomationName => $"{Extension} 현재 Mini Capture 연결 상태";

    public string CandidateAutomationName => $"{Extension} 연결 요청 후보";

    public bool IsAssociated
    {
        get => _isAssociated;
        private set => SetField(ref _isAssociated, value, nameof(IsAssociated));
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value, nameof(IsSelected));
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
