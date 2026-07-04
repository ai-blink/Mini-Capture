using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfClipboard = System.Windows.Clipboard;

namespace MiniCapture;

internal enum SettingsSection
{
    ExtensionDefaults
}

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ExecutablePathText.Text = GetExecutablePath();
        ShowSection(SettingsSection.ExtensionDefaults);
        SetStatus("확장자 연결 설정");
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
            SetStatus("Windows 기본 앱 설정을 열었습니다.");
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

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ??
            Process.GetCurrentProcess().MainModule?.FileName ??
            Path.Combine(AppContext.BaseDirectory, "MiniCapture.exe");
    }
}
