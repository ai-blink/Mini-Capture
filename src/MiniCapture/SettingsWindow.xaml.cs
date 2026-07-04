using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace MiniCapture;

internal enum SettingsSection
{
    ExtensionDefaults
}

public partial class SettingsWindow : Window
{
    private readonly DispatcherTimer _associationRefreshTimer;

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

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;

        _associationRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _associationRefreshTimer.Tick += (_, _) => RefreshAssociationStates("10초 자동 갱신");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
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
        if (ExtensionDefaultsPanel is null)
        {
            return;
        }

        ExtensionDefaultsPanel.Visibility = section == SettingsSection.ExtensionDefaults
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

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
