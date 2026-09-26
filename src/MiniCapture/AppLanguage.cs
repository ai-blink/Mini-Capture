using System.Globalization;

namespace MiniCapture;

public enum AppLanguage
{
    Korean,
    English,
    ChineseSimplified,
    Japanese
}

public static class LocalizationService
{
    public static CultureInfo GetCultureInfo(AppLanguage language) =>
        language switch
        {
            AppLanguage.English => CultureInfo.GetCultureInfo("en"),
            AppLanguage.ChineseSimplified => CultureInfo.GetCultureInfo("zh-CN"),
            AppLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
            _ => CultureInfo.GetCultureInfo("ko-KR")
        };

    public static string GetNativeName(AppLanguage language) =>
        language switch
        {
            AppLanguage.English => "English",
            AppLanguage.ChineseSimplified => "中文(简体)",
            AppLanguage.Japanese => "日本語",
            _ => "한국어"
        };

    public static void ApplyLanguage(AppLanguage language)
    {
        var culture = GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
