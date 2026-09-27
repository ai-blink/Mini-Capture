using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MiniCapture.Resources;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace MiniCapture;

internal enum SettingsSection
{
    ExtensionDefaults,
    CaptureSettings,
    WindowExclusions,
    Language
}

public partial class SettingsWindow : Window
{
    private readonly DispatcherTimer _associationRefreshTimer;
    private MiniCaptureSettings _captureSettings;
    private bool _isUpdatingWindowExclusionRule;

    public ObservableCollection<ExtensionAssociationItem> ExtensionItems { get; } = [];

    public ObservableCollection<CaptureHotkeyGroup> HotkeyGroups { get; }

    public ObservableCollection<RunningProcessItem> RunningProcessItems { get; } = [];

    public ObservableCollection<WindowExclusionItem> WindowExclusionItems { get; } = [];

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
        Enum.GetValues<AppLanguage>().Select(language => new LanguageOption(language)).ToList();

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
        Enum.GetValues<AppTheme>().Select(theme => new ThemeOption(theme)).ToList();

    private IEnumerable<CaptureHotkeySettingItem> HotkeySettings =>
        HotkeyGroups.SelectMany(group => group.Slots);

    public SettingsWindow()
    {
        InitializeComponent();
        HotkeyGroups =
        [
            new(CaptureHotkeyKind.Window, Strings.Hotkey_Window),
            new(CaptureHotkeyKind.Region, Strings.Hotkey_Region),
            new(CaptureHotkeyKind.FullScreen, Strings.Hotkey_FullScreen),
            new(CaptureHotkeyKind.Timer, Strings.Hotkey_Timer)
        ];
        _captureSettings = MiniCaptureSettingsStore.Load();
        LoadExtensionItems();
        DataContext = this;

        _associationRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _associationRefreshTimer.Tick += (_, _) => RefreshAssociationStates(Strings.Settings_Status_AutoRefresh10s);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadCaptureSettingsFields();
        LoadExtensionItems();
        LoadWindowExclusionFields();
        LoadLanguageFields();
        RefreshRunningProcesses();
        ExecutablePathText.Text = GetExecutablePath();
        ShowSection(SettingsSection.ExtensionDefaults);
        RefreshAssociationStates(Strings.Settings_Status_WindowOpened);
        _associationRefreshTimer.Start();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshAssociationStates(Strings.Settings_Status_WindowActivated);
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
            SetStatus(BuildWindowsSettingsStatus(Strings.Settings_Status_OpenedDefaultApps));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            SetStatus(string.Format(Strings.Settings_Status_OpenDefaultAppsFailed, ex.Message));
        }
    }

    private void OnShowExtensionHelpClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            this,
            Strings.Settings_Extension_HelpDialogBody,
            Strings.Settings_Extension_HelpDialogTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void OnCopyExecutablePathClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ClipboardService.SetTextAsync(GetExecutablePath());
            SetStatus(Strings.Settings_Status_PathCopied);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetStatus(string.Format(Strings.Settings_Status_PathCopyFailed, ex.Message));
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnRefreshAssociationsClick(object sender, RoutedEventArgs e)
    {
        RefreshAssociationStates(Strings.Common_Refresh);
    }

    private void OnRegisterAssociationCandidatesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            FileAssociationRegistrar.RegisterViewerCandidates(
                GetExecutablePath(),
                FileAssociationRegistrar.GetRegistrationExtensions(_captureSettings.AdditionalImageExtensions));
            RefreshAssociationStates(Strings.Settings_Extension_RestoreCandidates);
            SetStatus(Strings.Settings_Status_RestoreCandidatesDone);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            SetStatus(string.Format(Strings.Settings_Status_RestoreCandidatesFailed, ex.Message));
        }
    }

    private void OnAddImageExtensionClick(object sender, RoutedEventArgs e)
    {
        if (!FileAssociationRegistrar.TryNormalizeExtension(AdditionalImageExtensionBox.Text, out var extension))
        {
            SetStatus(Strings.Settings_Status_InvalidExtensionFormat);
            return;
        }

        if (FileAssociationRegistrar.DefaultExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            SetStatus(string.Format(Strings.Settings_Status_ExtensionIsDefault, extension));
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var additionalExtensions = FileAssociationRegistrar.NormalizeAdditionalExtensions(
            settings.AdditionalImageExtensions.Append(extension));
        if (settings.AdditionalImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            SetStatus(string.Format(Strings.Settings_Status_ExtensionAlreadyRegistered, extension));
            return;
        }

        try
        {
            FileAssociationRegistrar.RegisterViewerCandidates(
                GetExecutablePath(),
                FileAssociationRegistrar.GetRegistrationExtensions(additionalExtensions));
            settings.AdditionalImageExtensions = additionalExtensions.ToList();
            _captureSettings = settings;
            MiniCaptureSettingsStore.Save(settings);
            AdditionalImageExtensionBox.Clear();
            LoadExtensionItems();
            RefreshAssociationStates(string.Format(Strings.Settings_Status_ExtensionRegisteredReason, extension));
            SetStatus(string.Format(Strings.Settings_Status_ExtensionRegistered, extension));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            SetStatus(string.Format(Strings.Settings_Status_ExtensionRegisterFailed, extension, ex.Message));
        }
    }

    private void ShowSection(SettingsSection section)
    {
        if (ExtensionDefaultsPanel is null || CaptureSettingsPanel is null ||
            WindowExclusionsPanel is null || LanguagePanel is null)
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
        LanguagePanel.Visibility = section == SettingsSection.Language
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
        SetStatus(Strings.Settings_Status_CaptureSettingsSaved);
    }

    private void OnResetCaptureSettingsClick(object sender, RoutedEventArgs e)
    {
        _captureSettings = new MiniCaptureSettings
        {
            QuickButtonLeft = _captureSettings.QuickButtonLeft,
            QuickButtonTop = _captureSettings.QuickButtonTop,
            Language = _captureSettings.Language,
            Theme = _captureSettings.Theme
        };
        MiniCaptureSettingsStore.Save(_captureSettings);
        LoadCaptureSettingsFields();
        SetStatus(Strings.Settings_Status_CaptureSettingsReset);
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
        SetStatus(string.Format(Strings.Settings_Status_ProcessesRefreshed, RunningProcessItems.Count));
    }

    private void OnAddRunningProcessExclusionClick(object sender, RoutedEventArgs e)
    {
        if (RunningProcessBox.SelectedItem is not RunningProcessItem process)
        {
            SetStatus(Strings.Settings_Status_SelectProcessFirst);
            return;
        }

        AddWindowExclusion(process.ExecutablePath, process.ProcessName);
    }

    private void OnBrowseWindowExclusionPathClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Settings_Dialog_BrowseExecutableTitle,
            CheckFileExists = true,
            Filter = string.Format(
                "{0} (*.exe)|*.exe|{1} (*.*)|*.*",
                Strings.Settings_Dialog_ExecutableFilterName,
                Strings.Settings_Dialog_AllFilesFilterName)
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
            SetStatus(Strings.Settings_Status_InvalidExecutablePathOrBrowse);
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
            SetStatus(Strings.Settings_Status_SelectExclusionToRemove);
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var removed = settings.WindowCaptureExclusionTargets.RemoveAll(target =>
            target.HasSameIdentity(selected.Target));
        if (removed == 0)
        {
            SetStatus(Strings.Settings_Status_ExclusionAlreadyRemoved);
            LoadWindowExclusionFields();
            return;
        }

        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        LoadWindowExclusionFields();
        SetStatus(string.Format(Strings.Settings_Status_ExclusionRemoved, selected.DisplayName));
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
            SetStatus(Strings.Settings_Status_ExclusionProcessNameOnly);
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
            SetStatus(Strings.Settings_Status_ExclusionTargetNotFound);
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
            ? Strings.Settings_Status_RuleSwitchedToFilePath
            : Strings.Settings_Status_RuleSwitchedToProcessName);
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
                if (NativeWindowApi.TryGetProcessExecutablePath(process.Id, out var processImagePath))
                {
                    MiniCaptureSettings.TryNormalizeWindowCaptureExcludedExecutablePath(
                        processImagePath,
                        out executablePath);
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
            SetStatus(Strings.Settings_Status_InvalidExecutablePathOrSelectProcess);
            return;
        }

        if (!MiniCaptureSettings.TryNormalizeWindowCaptureExcludedProcessName(processName, out var normalizedProcessName))
        {
            SetStatus(Strings.Settings_Status_ProcessNameUnresolved);
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
        var existing = settings.WindowCaptureExclusionTargets.FirstOrDefault(candidate =>
            candidate.HasSameIdentity(target));
        if (existing is not null)
        {
            if (hasExecutablePath && string.IsNullOrWhiteSpace(existing.ExecutablePath))
            {
                existing.ExecutablePath = normalizedPath;
                existing.ProcessName = normalizedProcessName;
                existing.MatchMode = WindowCaptureExclusionMatchMode.FilePath;
                _captureSettings = settings;
                MiniCaptureSettingsStore.Save(settings);
                WindowExclusionPathBox.Clear();
                LoadWindowExclusionFields();
                WindowExclusionList.SelectedItem = WindowExclusionItems.FirstOrDefault(item =>
                    item.Target.HasSameIdentity(target));
                SetStatus(string.Format(Strings.Settings_Status_ExclusionUpgradedToFilePath, Path.GetFileName(normalizedPath)));
                return;
            }

            SetStatus(Strings.Settings_Status_ExclusionAlreadyExists);
            return;
        }

        settings.WindowCaptureExclusionTargets.Add(target);
        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        WindowExclusionPathBox.Clear();
        LoadWindowExclusionFields();
        WindowExclusionList.SelectedItem = WindowExclusionItems.FirstOrDefault(item => item.Target.HasSameIdentity(target));
        SetStatus(hasExecutablePath
            ? string.Format(Strings.Settings_Status_ExclusionAddedFilePath, Path.GetFileName(normalizedPath))
            : string.Format(Strings.Settings_Status_ExclusionAddedProcessName, normalizedProcessName));
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
            error = Strings.Settings_Status_TimerRangeInvalid;
            return false;
        }

        if (!int.TryParse(ShortcutTimerDelayBox.Text, out var shortcutTimerSeconds) ||
            shortcutTimerSeconds < 1 ||
            shortcutTimerSeconds > 60)
        {
            error = Strings.Settings_Status_ShortcutTimerRangeInvalid;
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
                    error = string.Format(Strings.Settings_Status_HotkeyFormatInvalid, item.Label);
                    return false;
                }

                normalizedHotkeys.Add(hotkey.DisplayText);
                allNonEmptyHotkeys.Add((item.Label, hotkey.DisplayText));
            }

            if (normalizedHotkeys.All(string.IsNullOrWhiteSpace))
            {
                error = string.Format(Strings.Settings_Status_HotkeyRequired, group.Label);
                return false;
            }

            normalizedByKind[group.Kind] = normalizedHotkeys;
        }

        if (allNonEmptyHotkeys.Select(item => item.Hotkey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != allNonEmptyHotkeys.Count)
        {
            error = Strings.Settings_Status_HotkeysMustBeDistinct;
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
            item.StatusText = Strings.Hotkey_StatusWaiting;
            SetStatus(Strings.Settings_Status_DetectCancelled);
            e.Handled = true;
            return;
        }

        ApplyDetectedHotkeyPart(item, part, dialog.DetectedValue);
        item.StatusText = Strings.Hotkey_StatusDetected;
        SetStatus(string.Format(
            Strings.Settings_Status_HotkeyDetected,
            item.Label,
            CaptureHotkeyPartDisplayName(part),
            dialog.DetectedValue));
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
            group.Slots[index].StatusText = Strings.Hotkey_StatusWaiting;
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
            CaptureHotkeyPart.First => Strings.Hotkey_FirstKeyPart,
            CaptureHotkeyPart.Second => Strings.Hotkey_SecondKeyPart,
            _ => Strings.Hotkey_MainKeyPart
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

        AssociationSummaryText.Text = string.Format(Strings.Settings_Status_AssociationSummary, associatedCount, totalCount);
        SetStatus(string.Format(Strings.Settings_Status_AssociationsRefreshed, reason, $"{DateTime.Now:HH:mm:ss}"));
    }

    private string BuildWindowsSettingsStatus(string prefix)
    {
        var extensions = string.Join(", ", GetExtensionItems().Select(item => item.Extension));
        return string.Format(Strings.Settings_Status_WindowsSettingsPrompt, prefix, extensions);
    }

    private IEnumerable<ExtensionAssociationItem> GetExtensionItems()
    {
        return ExtensionItems;
    }

    private void LoadExtensionItems()
    {
        ExtensionItems.Clear();
        foreach (var extension in FileAssociationRegistrar.GetRegistrationExtensions(
                     _captureSettings.AdditionalImageExtensions))
        {
            ExtensionItems.Add(new ExtensionAssociationItem(
                extension,
                FileAssociationRegistrar.GetDisplayName(extension)));
        }
    }

    private void LoadLanguageFields()
    {
        var settings = MiniCaptureSettingsStore.Load();
        LanguageComboBox.SelectedItem = LanguageOptions.FirstOrDefault(option => option.Language == settings.Language);
        ThemeComboBox.SelectedItem = ThemeOptions.FirstOrDefault(option => option.Theme == settings.Theme);
    }

    private void OnApplyLanguageClick(object sender, RoutedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is not LanguageOption option || option.Language == _captureSettings.Language)
        {
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        settings.Language = option.Language;
        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        PromptRestart(Strings.Settings_Language_RestartPromptBody);
    }

    private void OnApplyThemeClick(object sender, RoutedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is not ThemeOption option || option.Theme == _captureSettings.Theme)
        {
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        settings.Theme = option.Theme;
        _captureSettings = settings;
        MiniCaptureSettingsStore.Save(settings);
        PromptRestart(Strings.Settings_Theme_RestartPromptBody);
    }

    // 언어·테마는 시작할 때 한 번 적용되므로 바꾼 뒤 재시작을 권한다. 새 프로세스는 이 PID가 끝날 때까지 기다린다.
    private void PromptRestart(string message)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            message,
            Strings.Settings_Language_RestartPromptTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo(GetExecutablePath())
            {
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(Program.RestartWaitArgument);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            System.Diagnostics.Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
        }
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
        ? string.Format(Strings.Settings_Process_DisplayNamePathUnknown, ProcessName, ProcessId)
        : string.Format(Strings.Settings_Process_DisplayName, ProcessName, ProcessId);
}

public sealed class WindowExclusionItem(WindowCaptureExclusionTarget target)
{
    public WindowCaptureExclusionTarget Target { get; } = target.Clone();

    public string ExecutablePath => Target.ExecutablePath;

    public WindowCaptureExclusionMatchMode MatchMode => Target.MatchMode;

    public string DisplayName => Target.ProcessName;

    public string AppliedRuleText => MatchMode == WindowCaptureExclusionMatchMode.FilePath
        ? Strings.Settings_WindowExclusion_AppliedRuleFilePath
        : Strings.Settings_WindowExclusion_AppliedRuleProcessName;
}

public sealed class LanguageOption(AppLanguage language)
{
    public AppLanguage Language { get; } = language;

    public string DisplayName => LocalizationService.GetNativeName(Language);
}

public sealed class ThemeOption(AppTheme theme)
{
    public AppTheme Theme { get; } = theme;

    public string DisplayName => Theme switch
    {
        AppTheme.Dark => Strings.Theme_Dark,
        AppTheme.System => Strings.Theme_System,
        _ => Strings.Theme_Light
    };
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
    private string _statusText = Strings.Hotkey_StatusWaiting;
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CaptureHotkeyKind Kind { get; } = kind;

    public string Label { get; } = label;

    public int SlotNumber { get; } = slotNumber;

    public string HotkeyAutomationName => $"{Label} {Strings.Hotkey_Suffix}";

    public string DetectAutomationName => $"{Label} {Strings.Hotkey_DetectSuffix}";

    public string FirstKeyAutomationId => $"{Kind}Hotkey{SlotNumber}FirstKeyCombo";

    public string SecondKeyAutomationId => $"{Kind}Hotkey{SlotNumber}SecondKeyCombo";

    public string MainKeyAutomationId => $"{Kind}Hotkey{SlotNumber}MainKeyCombo";

    public string FirstKeyAutomationName => $"{Label} {Strings.Hotkey_FirstKeyPart}";

    public string SecondKeyAutomationName => $"{Label} {Strings.Hotkey_SecondKeyPart}";

    public string MainKeyAutomationName => $"{Label} {Strings.Hotkey_MainKeyPart}";

    public string SlotLabel => Strings.Hotkey_Suffix;

    public string PlusText => string.Empty;

    public string ListAutomationId => $"{Kind}HotkeyList{SlotNumber}";

    public string DetectAutomationId => $"Detect{Kind}HotkeyButton{SlotNumber}";

    public string DetectFirstKeyAutomationId => $"Detect{Kind}Hotkey{SlotNumber}FirstKeyButton";

    public string DetectSecondKeyAutomationId => $"Detect{Kind}Hotkey{SlotNumber}SecondKeyButton";

    public string DetectMainKeyAutomationId => $"Detect{Kind}Hotkey{SlotNumber}MainKeyButton";

    public string DetectFirstKeyAutomationName => $"{Label} {Strings.Hotkey_FirstKeyPart} {Strings.Hotkey_DetectSuffix}";

    public string DetectSecondKeyAutomationName => $"{Label} {Strings.Hotkey_SecondKeyPart} {Strings.Hotkey_DetectSuffix}";

    public string DetectMainKeyAutomationName => $"{Label} {Strings.Hotkey_MainKeyPart} {Strings.Hotkey_DetectSuffix}";

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

    public string DisplayHotkeyText => string.IsNullOrWhiteSpace(HotkeyText) ? Strings.Hotkey_EmptyPlaceholder : HotkeyText;

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
    private string _statusText = Strings.Settings_Extension_StatusPending;
    private string _statusDetail = string.Empty;
    private WpfBrush _statusBrush = ThemeBrush("SettingsMutedTextBrush");

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Extension { get; } = extension;

    public string DisplayName { get; } = displayName;

    public string ChangeAutomationName => string.Format(Strings.Settings_Extension_ChangeAutomationName, Extension);

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
        StatusBrush = ThemeBrush(state.IsAssociated ? "SettingsSuccessTextBrush" : "SettingsInfoTextBrush");
    }

    // 현재 테마(Themes/*.xaml)의 색. 앱 리소스가 없으면(테스트 등) 회색으로 둔다.
    private static WpfBrush ThemeBrush(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as WpfBrush ?? WpfBrushes.Gray;

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
                : string.IsNullOrWhiteSpace(progId) ? Strings.FileAssoc_Unspecified : progId;
            var none = Strings.FileAssoc_None;
            var detail = command is null
                ? string.Format(Strings.FileAssoc_DetailNoCommand, userChoiceProgId ?? none, defaultProgId ?? none)
                : string.Format(Strings.FileAssoc_DetailWithCommand, userChoiceProgId ?? none, defaultProgId ?? none, command);

            return new FileAssociationState(associated, status, detail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new FileAssociationState(false, Strings.FileAssoc_CheckFailed, ex.Message);
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
