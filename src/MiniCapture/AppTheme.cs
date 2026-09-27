using Microsoft.Win32;

namespace MiniCapture;

public enum AppTheme
{
    Light,
    Dark,
    System
}

public static class ThemeService
{
    // 설정값을 실제로 쓸 테마(Light/Dark)로 바꾼다. System은 Windows 앱 모드를 따르고, 읽지 못하면 라이트.
    internal static AppTheme Resolve(AppTheme setting, bool? systemPrefersLight) =>
        setting switch
        {
            AppTheme.Dark => AppTheme.Dark,
            AppTheme.System => systemPrefersLight == false ? AppTheme.Dark : AppTheme.Light,
            _ => AppTheme.Light
        };

    internal static bool? ReadSystemPrefersLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value ? value != 0 : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return null;
        }
    }

    // 창을 만들기 전에 한 번 부른다. 창들은 StaticResource로 색을 읽으므로 테마 변경은 재시작 후 적용된다.
    public static void Apply(System.Windows.Application application, AppTheme setting)
    {
        var theme = Resolve(setting, setting == AppTheme.System ? ReadSystemPrefersLight() : null);
        application.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri($"/MiniCapture;component/Themes/{theme}.xaml", UriKind.Relative)
        });

        if (theme == AppTheme.Dark)
        {
            // 제목 표시줄은 Windows가 그리므로 리소스로는 바뀌지 않는다. 모든 창이 뜰 때 DWM 다크 모드를 켠다.
            System.Windows.EventManager.RegisterClassHandler(
                typeof(System.Windows.Window),
                System.Windows.FrameworkElement.LoadedEvent,
                new System.Windows.RoutedEventHandler((sender, _) => UseDarkTitleBar((System.Windows.Window)sender)));
        }
    }

    private static void UseDarkTitleBar(System.Windows.Window window)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        // Windows 10 20H1 이후 20, 그 이전 빌드는 19. 지원하지 않는 OS에서는 실패만 하고 무시한다.
        if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
