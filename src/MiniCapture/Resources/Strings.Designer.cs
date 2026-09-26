using System.Globalization;
using System.Resources;

namespace MiniCapture.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager = new("MiniCapture.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string Common_Close => Get(nameof(Common_Close));
    public static string Common_Detect => Get(nameof(Common_Detect));
    public static string Common_Save => Get(nameof(Common_Save));
    public static string Common_ResetDefault => Get(nameof(Common_ResetDefault));
    public static string Common_Refresh => Get(nameof(Common_Refresh));
    public static string Common_BrowseFile => Get(nameof(Common_BrowseFile));
    public static string Common_ShowHelp => Get(nameof(Common_ShowHelp));

    public static string Settings_WindowTitle => Get(nameof(Settings_WindowTitle));
    public static string Settings_HeaderSubtitle => Get(nameof(Settings_HeaderSubtitle));

    public static string Settings_Section_ExtensionDefaults => Get(nameof(Settings_Section_ExtensionDefaults));
    public static string Settings_Section_CaptureSettings => Get(nameof(Settings_Section_CaptureSettings));
    public static string Settings_Section_WindowExclusions => Get(nameof(Settings_Section_WindowExclusions));
    public static string Settings_Section_Language => Get(nameof(Settings_Section_Language));

    public static string Settings_Capture_Subtitle => Get(nameof(Settings_Capture_Subtitle));
    public static string Settings_Capture_HotkeyHelp => Get(nameof(Settings_Capture_HotkeyHelp));
    public static string Settings_Capture_TimerLabel => Get(nameof(Settings_Capture_TimerLabel));
    public static string Settings_Capture_ShortcutTimerLabel => Get(nameof(Settings_Capture_ShortcutTimerLabel));
    public static string Settings_Capture_QuickButtonVisible => Get(nameof(Settings_Capture_QuickButtonVisible));
    public static string Settings_Capture_UiExcluded => Get(nameof(Settings_Capture_UiExcluded));

    public static string Hotkey_Window => Get(nameof(Hotkey_Window));
    public static string Hotkey_Region => Get(nameof(Hotkey_Region));
    public static string Hotkey_FullScreen => Get(nameof(Hotkey_FullScreen));
    public static string Hotkey_Timer => Get(nameof(Hotkey_Timer));
    public static string Hotkey_FirstKeyPart => Get(nameof(Hotkey_FirstKeyPart));
    public static string Hotkey_SecondKeyPart => Get(nameof(Hotkey_SecondKeyPart));
    public static string Hotkey_MainKeyPart => Get(nameof(Hotkey_MainKeyPart));
    public static string Hotkey_Suffix => Get(nameof(Hotkey_Suffix));
    public static string Hotkey_DetectSuffix => Get(nameof(Hotkey_DetectSuffix));
    public static string Hotkey_EmptyPlaceholder => Get(nameof(Hotkey_EmptyPlaceholder));
    public static string Hotkey_StatusWaiting => Get(nameof(Hotkey_StatusWaiting));
    public static string Hotkey_StatusDetected => Get(nameof(Hotkey_StatusDetected));

    public static string Settings_WindowExclusion_Title => Get(nameof(Settings_WindowExclusion_Title));
    public static string Settings_WindowExclusion_Subtitle => Get(nameof(Settings_WindowExclusion_Subtitle));
    public static string Settings_WindowExclusion_RunningProcessLabel => Get(nameof(Settings_WindowExclusion_RunningProcessLabel));
    public static string Settings_WindowExclusion_RunningProcessTooltip => Get(nameof(Settings_WindowExclusion_RunningProcessTooltip));
    public static string Settings_WindowExclusion_RunningProcessHelp => Get(nameof(Settings_WindowExclusion_RunningProcessHelp));
    public static string Settings_WindowExclusion_AddRunningProcess => Get(nameof(Settings_WindowExclusion_AddRunningProcess));
    public static string Settings_WindowExclusion_AddPathLabel => Get(nameof(Settings_WindowExclusion_AddPathLabel));
    public static string Settings_WindowExclusion_PathTooltip => Get(nameof(Settings_WindowExclusion_PathTooltip));
    public static string Settings_WindowExclusion_AddPathButton => Get(nameof(Settings_WindowExclusion_AddPathButton));
    public static string Settings_WindowExclusion_RemoveSelected => Get(nameof(Settings_WindowExclusion_RemoveSelected));
    public static string Settings_WindowExclusion_RegisteredLabel => Get(nameof(Settings_WindowExclusion_RegisteredLabel));
    public static string Settings_WindowExclusion_RuleLabel => Get(nameof(Settings_WindowExclusion_RuleLabel));
    public static string Settings_WindowExclusion_RuleFilePath => Get(nameof(Settings_WindowExclusion_RuleFilePath));
    public static string Settings_WindowExclusion_RuleProcessName => Get(nameof(Settings_WindowExclusion_RuleProcessName));
    public static string Settings_WindowExclusion_AppliedRuleFilePath => Get(nameof(Settings_WindowExclusion_AppliedRuleFilePath));
    public static string Settings_WindowExclusion_AppliedRuleProcessName => Get(nameof(Settings_WindowExclusion_AppliedRuleProcessName));

    public static string Settings_Extension_Subtitle => Get(nameof(Settings_Extension_Subtitle));
    public static string Settings_Extension_StatusTitle => Get(nameof(Settings_Extension_StatusTitle));
    public static string Settings_Extension_StatusHelp => Get(nameof(Settings_Extension_StatusHelp));
    public static string Settings_Extension_StatusPending => Get(nameof(Settings_Extension_StatusPending));
    public static string Settings_Extension_ImageExtensionsTitle => Get(nameof(Settings_Extension_ImageExtensionsTitle));
    public static string Settings_Extension_ImageExtensionsHelp => Get(nameof(Settings_Extension_ImageExtensionsHelp));
    public static string Settings_Extension_ExtensionInputTooltip => Get(nameof(Settings_Extension_ExtensionInputTooltip));
    public static string Settings_Extension_RegisterButton => Get(nameof(Settings_Extension_RegisterButton));
    public static string Settings_Extension_ColumnExtension => Get(nameof(Settings_Extension_ColumnExtension));
    public static string Settings_Extension_ColumnFormat => Get(nameof(Settings_Extension_ColumnFormat));
    public static string Settings_Extension_ColumnCurrentApp => Get(nameof(Settings_Extension_ColumnCurrentApp));
    public static string Settings_Extension_ColumnEdit => Get(nameof(Settings_Extension_ColumnEdit));
    public static string Settings_Extension_OpenDefaultApps => Get(nameof(Settings_Extension_OpenDefaultApps));
    public static string Settings_Extension_RestoreCandidates => Get(nameof(Settings_Extension_RestoreCandidates));
    public static string Settings_Extension_CopyExecutablePath => Get(nameof(Settings_Extension_CopyExecutablePath));
    public static string Settings_Extension_ChangeAutomationName => Get(nameof(Settings_Extension_ChangeAutomationName));
    public static string Settings_Extension_HelpDialogTitle => Get(nameof(Settings_Extension_HelpDialogTitle));
    public static string Settings_Extension_HelpDialogBody => Get(nameof(Settings_Extension_HelpDialogBody));

    public static string Settings_Footer_DefaultAppNotice => Get(nameof(Settings_Footer_DefaultAppNotice));

    public static string Settings_Status_AutoRefresh10s => Get(nameof(Settings_Status_AutoRefresh10s));
    public static string Settings_Status_WindowOpened => Get(nameof(Settings_Status_WindowOpened));
    public static string Settings_Status_WindowActivated => Get(nameof(Settings_Status_WindowActivated));
    public static string Settings_Status_OpenedDefaultApps => Get(nameof(Settings_Status_OpenedDefaultApps));
    public static string Settings_Status_OpenDefaultAppsFailed => Get(nameof(Settings_Status_OpenDefaultAppsFailed));
    public static string Settings_Status_PathCopied => Get(nameof(Settings_Status_PathCopied));
    public static string Settings_Status_PathCopyFailed => Get(nameof(Settings_Status_PathCopyFailed));
    public static string Settings_Status_RestoreCandidatesDone => Get(nameof(Settings_Status_RestoreCandidatesDone));
    public static string Settings_Status_RestoreCandidatesFailed => Get(nameof(Settings_Status_RestoreCandidatesFailed));
    public static string Settings_Status_InvalidExtensionFormat => Get(nameof(Settings_Status_InvalidExtensionFormat));
    public static string Settings_Status_ExtensionIsDefault => Get(nameof(Settings_Status_ExtensionIsDefault));
    public static string Settings_Status_ExtensionAlreadyRegistered => Get(nameof(Settings_Status_ExtensionAlreadyRegistered));
    public static string Settings_Status_ExtensionRegisteredReason => Get(nameof(Settings_Status_ExtensionRegisteredReason));
    public static string Settings_Status_ExtensionRegistered => Get(nameof(Settings_Status_ExtensionRegistered));
    public static string Settings_Status_ExtensionRegisterFailed => Get(nameof(Settings_Status_ExtensionRegisterFailed));
    public static string Settings_Status_CaptureSettingsSaved => Get(nameof(Settings_Status_CaptureSettingsSaved));
    public static string Settings_Status_CaptureSettingsReset => Get(nameof(Settings_Status_CaptureSettingsReset));
    public static string Settings_Status_ProcessesRefreshed => Get(nameof(Settings_Status_ProcessesRefreshed));
    public static string Settings_Status_SelectProcessFirst => Get(nameof(Settings_Status_SelectProcessFirst));
    public static string Settings_Dialog_BrowseExecutableTitle => Get(nameof(Settings_Dialog_BrowseExecutableTitle));
    public static string Settings_Dialog_ExecutableFilterName => Get(nameof(Settings_Dialog_ExecutableFilterName));
    public static string Settings_Dialog_AllFilesFilterName => Get(nameof(Settings_Dialog_AllFilesFilterName));
    public static string Settings_Status_InvalidExecutablePathOrBrowse => Get(nameof(Settings_Status_InvalidExecutablePathOrBrowse));
    public static string Settings_Status_SelectExclusionToRemove => Get(nameof(Settings_Status_SelectExclusionToRemove));
    public static string Settings_Status_ExclusionAlreadyRemoved => Get(nameof(Settings_Status_ExclusionAlreadyRemoved));
    public static string Settings_Status_ExclusionRemoved => Get(nameof(Settings_Status_ExclusionRemoved));
    public static string Settings_Status_ExclusionProcessNameOnly => Get(nameof(Settings_Status_ExclusionProcessNameOnly));
    public static string Settings_Status_ExclusionTargetNotFound => Get(nameof(Settings_Status_ExclusionTargetNotFound));
    public static string Settings_Status_RuleSwitchedToFilePath => Get(nameof(Settings_Status_RuleSwitchedToFilePath));
    public static string Settings_Status_RuleSwitchedToProcessName => Get(nameof(Settings_Status_RuleSwitchedToProcessName));
    public static string Settings_Status_InvalidExecutablePathOrSelectProcess => Get(nameof(Settings_Status_InvalidExecutablePathOrSelectProcess));
    public static string Settings_Status_ProcessNameUnresolved => Get(nameof(Settings_Status_ProcessNameUnresolved));
    public static string Settings_Status_ExclusionAlreadyExists => Get(nameof(Settings_Status_ExclusionAlreadyExists));
    public static string Settings_Status_ExclusionUpgradedToFilePath => Get(nameof(Settings_Status_ExclusionUpgradedToFilePath));
    public static string Settings_Status_ExclusionAddedFilePath => Get(nameof(Settings_Status_ExclusionAddedFilePath));
    public static string Settings_Status_ExclusionAddedProcessName => Get(nameof(Settings_Status_ExclusionAddedProcessName));
    public static string Settings_Status_TimerRangeInvalid => Get(nameof(Settings_Status_TimerRangeInvalid));
    public static string Settings_Status_ShortcutTimerRangeInvalid => Get(nameof(Settings_Status_ShortcutTimerRangeInvalid));
    public static string Settings_Status_HotkeyFormatInvalid => Get(nameof(Settings_Status_HotkeyFormatInvalid));
    public static string Settings_Status_HotkeyRequired => Get(nameof(Settings_Status_HotkeyRequired));
    public static string Settings_Status_HotkeysMustBeDistinct => Get(nameof(Settings_Status_HotkeysMustBeDistinct));
    public static string Settings_Status_DetectCancelled => Get(nameof(Settings_Status_DetectCancelled));
    public static string Settings_Status_HotkeyDetected => Get(nameof(Settings_Status_HotkeyDetected));
    public static string Settings_Status_AssociationSummary => Get(nameof(Settings_Status_AssociationSummary));
    public static string Settings_Status_AssociationsRefreshed => Get(nameof(Settings_Status_AssociationsRefreshed));
    public static string Settings_Status_WindowsSettingsPrompt => Get(nameof(Settings_Status_WindowsSettingsPrompt));

    public static string Settings_Process_DisplayNamePathUnknown => Get(nameof(Settings_Process_DisplayNamePathUnknown));
    public static string Settings_Process_DisplayName => Get(nameof(Settings_Process_DisplayName));

    public static string FileAssoc_Unspecified => Get(nameof(FileAssoc_Unspecified));
    public static string FileAssoc_CheckFailed => Get(nameof(FileAssoc_CheckFailed));
    public static string FileAssoc_None => Get(nameof(FileAssoc_None));
    public static string FileAssoc_DetailNoCommand => Get(nameof(FileAssoc_DetailNoCommand));
    public static string FileAssoc_DetailWithCommand => Get(nameof(FileAssoc_DetailWithCommand));

    public static string Settings_Language_Subtitle => Get(nameof(Settings_Language_Subtitle));
    public static string Settings_Language_Label => Get(nameof(Settings_Language_Label));
    public static string Settings_Language_ApplyButton => Get(nameof(Settings_Language_ApplyButton));
    public static string Settings_Language_RestartPromptTitle => Get(nameof(Settings_Language_RestartPromptTitle));
    public static string Settings_Language_RestartPromptBody => Get(nameof(Settings_Language_RestartPromptBody));
}
