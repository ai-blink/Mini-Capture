using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MiniCapture;

public static class FileAssociationRegistrar
{
    public static readonly IReadOnlyList<string> DefaultExtensions = [".png", ".jpg", ".jpeg"];

    private const string RegisteredApplicationName = "Mini Capture Viewer";
    private const string ApplicationKeyPath = @"Software\Classes\Applications\MiniCapture.exe";
    private const string CapabilitiesRelativePath = ApplicationKeyPath + @"\Capabilities";
    private const string RegisteredApplicationsPath = @"Software\RegisteredApplications";
    private const string ApplicationDisplayName = "Mini Capture Viewer";
    private const string ApplicationDescription = "Mini Capture image viewer.";
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

        ArgumentNullException.ThrowIfNull(extensions);

        var normalizedPath = Path.GetFullPath(executablePath);
        var requestedExtensions = extensions.ToArray();
        var registeredExtensions = new List<string>();
        var skippedExtensions = new List<string>();
        foreach (var requestedExtension in requestedExtensions)
        {
            if (!TryNormalizeExtension(requestedExtension, out var extension))
            {
                if (!string.IsNullOrWhiteSpace(requestedExtension))
                {
                    skippedExtensions.Add(requestedExtension.Trim());
                }

                continue;
            }

            if (!registeredExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                registeredExtensions.Add(extension);
            }
        }

        if (registeredExtensions.Count == 0)
        {
            return new FileAssociationRegistrationResult([], skippedExtensions);
        }

        WriteApplicationRegistration(normalizedPath, registeredExtensions);
        foreach (var extension in registeredExtensions)
        {
            WriteProgIdRegistration(normalizedPath, extension);
            WriteOpenWithRegistration(extension);
        }

        NotifyAssociationChanged();
        return new FileAssociationRegistrationResult(registeredExtensions, skippedExtensions);
    }

    public static IReadOnlyList<string> GetRegistrationExtensions(IEnumerable<string>? additionalExtensions)
    {
        var extensions = new List<string>(DefaultExtensions);
        if (additionalExtensions is not null)
        {
            extensions.AddRange(additionalExtensions);
        }

        return extensions
            .Select(extension => TryNormalizeExtension(extension, out var normalized) ? normalized : null)
            .Where(extension => extension is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeAdditionalExtensions(IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return [];
        }

        return extensions
            .Select(extension => TryNormalizeExtension(extension, out var normalized) ? normalized : null)
            .Where(extension => extension is not null)
            .Cast<string>()
            .Where(extension => !DefaultExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryNormalizeExtension(string? extension, out string normalizedExtension)
    {
        normalizedExtension = string.Empty;
        var trimmed = extension?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }

        var suffix = trimmed[1..];
        if (suffix.Length is < 1 or > 32 || suffix.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            return false;
        }

        normalizedExtension = "." + suffix.ToLowerInvariant();
        return true;
    }

    public static string GetDisplayName(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "Portable Network Graphics",
            ".jpg" or ".jpeg" => "JPEG Image",
            _ => $"{extension[1..].ToUpperInvariant()} Image"
        };
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
        foreach (var extension in extensions)
        {
            supportedTypesKey.SetValue(extension, string.Empty, RegistryValueKind.String);
        }

        using var capabilitiesKey = CreateCurrentUserSubKey(CapabilitiesRelativePath);
        capabilitiesKey.SetValue("ApplicationName", ApplicationDisplayName, RegistryValueKind.String);
        capabilitiesKey.SetValue("ApplicationDescription", ApplicationDescription, RegistryValueKind.String);

        using var fileAssociationsKey = CreateCurrentUserSubKey($@"{CapabilitiesRelativePath}\FileAssociations");
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

    private static RegistryKey CreateCurrentUserSubKey(string subKeyName)
    {
        return Registry.CurrentUser.CreateSubKey(subKeyName) ??
            throw new IOException($"Registry key could not be created: HKCU\\{subKeyName}");
    }

    private static string GetProgId(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "MiniCapture.AssocFile.PNG",
            ".jpg" or ".jpeg" => "MiniCapture.AssocFile.JPEG",
            _ => $"MiniCapture.AssocFile.{extension[1..].ToUpperInvariant()}"
        };
    }

    private static string GetFriendlyTypeName(string extension)
    {
        return GetDisplayName(extension);
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
