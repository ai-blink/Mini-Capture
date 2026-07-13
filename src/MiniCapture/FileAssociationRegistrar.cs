using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MiniCapture;

public static class FileAssociationRegistrar
{
    public static readonly IReadOnlyList<string> SupportedExtensions = [".png", ".jpg", ".jpeg"];

    private static readonly HashSet<string> SupportedExtensionSet = new(
        SupportedExtensions,
        StringComparer.OrdinalIgnoreCase);

    private const string RegisteredApplicationName = "Mini Capture Viewer";
    private const string ApplicationKeyPath = @"Software\Classes\Applications\MiniCapture.exe";
    private const string CapabilitiesRelativePath = ApplicationKeyPath + @"\Capabilities";
    private const string RegisteredApplicationsPath = @"Software\RegisteredApplications";
    private const string ApplicationDisplayName = "Mini Capture Viewer";
    private const string ApplicationDescription = "Mini Capture image viewer for PNG and JPEG screenshots.";
    private const int ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    public static FileAssociationRegistrationResult RegisterViewerCandidates(
        string executablePath,
        IEnumerable<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        var normalizedPath = Path.GetFullPath(executablePath);
        var selectedExtensions = NormalizeExtensions(extensions);
        var supportedExtensions = selectedExtensions
            .Where(SupportedExtensionSet.Contains)
            .ToArray();
        var unselectedSupportedExtensions = SupportedExtensions
            .Except(supportedExtensions, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var skippedExtensions = selectedExtensions
            .Except(supportedExtensions, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (supportedExtensions.Length == 0)
        {
            return new FileAssociationRegistrationResult([], skippedExtensions);
        }

        WriteApplicationRegistration(normalizedPath, supportedExtensions);
        foreach (var extension in supportedExtensions)
        {
            WriteProgIdRegistration(normalizedPath, extension);
            WriteOpenWithRegistration(extension);
        }

        foreach (var extension in unselectedSupportedExtensions)
        {
            RemoveOpenWithRegistration(extension);
        }

        NotifyAssociationChanged();
        return new FileAssociationRegistrationResult(supportedExtensions, skippedExtensions);
    }

    public static bool IsSupportedExtension(string extension)
    {
        return SupportedExtensionSet.Contains(extension);
    }

    private static void WriteApplicationRegistration(string executablePath, IReadOnlyCollection<string> extensions)
    {
        using var appKey = CreateCurrentUserSubKey(ApplicationKeyPath);
        appKey.SetValue(null, ApplicationDisplayName, RegistryValueKind.String);
        appKey.SetValue("FriendlyAppName", ApplicationDisplayName, RegistryValueKind.String);

        using var iconKey = CreateCurrentUserSubKey($@"{ApplicationKeyPath}\DefaultIcon");
        iconKey.SetValue(null, $"{Quote(executablePath)},0", RegistryValueKind.String);

        using var commandKey = CreateCurrentUserSubKey($@"{ApplicationKeyPath}\shell\open\command");
        commandKey.SetValue(null, BuildOpenCommand(executablePath), RegistryValueKind.String);

        using var supportedTypesKey = CreateCurrentUserSubKey($@"{ApplicationKeyPath}\SupportedTypes");
        ClearKnownExtensionValues(supportedTypesKey);
        foreach (var extension in extensions)
        {
            supportedTypesKey.SetValue(extension, string.Empty, RegistryValueKind.String);
        }

        using var capabilitiesKey = CreateCurrentUserSubKey(CapabilitiesRelativePath);
        capabilitiesKey.SetValue("ApplicationName", ApplicationDisplayName, RegistryValueKind.String);
        capabilitiesKey.SetValue("ApplicationDescription", ApplicationDescription, RegistryValueKind.String);

        using var fileAssociationsKey = CreateCurrentUserSubKey($@"{CapabilitiesRelativePath}\FileAssociations");
        ClearKnownExtensionValues(fileAssociationsKey);
        foreach (var extension in extensions)
        {
            fileAssociationsKey.SetValue(extension, GetProgId(extension), RegistryValueKind.String);
        }

        using var registeredApplicationsKey = CreateCurrentUserSubKey(RegisteredApplicationsPath);
        registeredApplicationsKey.SetValue(
            RegisteredApplicationName,
            CapabilitiesRelativePath,
            RegistryValueKind.String);
    }

    private static void WriteProgIdRegistration(string executablePath, string extension)
    {
        var progId = GetProgId(extension);
        using var progIdKey = CreateCurrentUserSubKey($@"Software\Classes\{progId}");
        progIdKey.SetValue(null, GetFriendlyTypeName(extension), RegistryValueKind.String);
        progIdKey.SetValue("FriendlyTypeName", GetFriendlyTypeName(extension), RegistryValueKind.String);

        using var defaultIconKey = CreateCurrentUserSubKey($@"Software\Classes\{progId}\DefaultIcon");
        defaultIconKey.SetValue(null, $"{Quote(executablePath)},0", RegistryValueKind.String);

        using var commandKey = CreateCurrentUserSubKey($@"Software\Classes\{progId}\shell\open\command");
        commandKey.SetValue(null, BuildOpenCommand(executablePath), RegistryValueKind.String);
    }

    private static void WriteOpenWithRegistration(string extension)
    {
        using var openWithKey = CreateCurrentUserSubKey($@"Software\Classes\{extension}\OpenWithProgids");
        openWithKey.SetValue(GetProgId(extension), string.Empty, RegistryValueKind.String);
    }

    private static void RemoveOpenWithRegistration(string extension)
    {
        using var openWithKey = Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\{extension}\OpenWithProgids",
            writable: true);
        openWithKey?.DeleteValue(GetProgId(extension), throwOnMissingValue: false);
    }

    private static void ClearKnownExtensionValues(RegistryKey key)
    {
        foreach (var extension in SupportedExtensions)
        {
            key.DeleteValue(extension, throwOnMissingValue: false);
        }
    }

    private static RegistryKey CreateCurrentUserSubKey(string subKeyName)
    {
        return Registry.CurrentUser.CreateSubKey(subKeyName) ??
            throw new IOException($"Registry key could not be created: HKCU\\{subKeyName}");
    }

    private static string[] NormalizeExtensions(IEnumerable<string> extensions)
    {
        return extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.Trim())
            .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
            .Select(extension => extension.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetProgId(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "MiniCapture.AssocFile.PNG"
            : "MiniCapture.AssocFile.JPEG";
    }

    private static string GetFriendlyTypeName(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "PNG Image"
            : "JPEG Image";
    }

    private static string BuildOpenCommand(string executablePath)
    {
        return $"{Quote(executablePath)} \"%1\"";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static void NotifyAssociationChanged()
    {
        try
        {
            SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}

public sealed record FileAssociationRegistrationResult(
    IReadOnlyList<string> RegisteredExtensions,
    IReadOnlyList<string> SkippedExtensions);
