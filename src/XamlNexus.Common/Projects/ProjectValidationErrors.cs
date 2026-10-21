using XamlNexus.Common.Resources;

namespace XamlNexus.Common.Projects;

/// <summary>集中绑定错误场景、编号与中英文文案资源。</summary>
public static class ProjectValidationErrors {
    public static readonly ErrorDefinition SolutionMissing =
        new(ProjectValidationCodes.SolutionMissing, "ProjectValidation_SolutionMissing", 0);

    public static readonly ErrorDefinition VersionPropertiesMissing =
        new(ProjectValidationCodes.VersionPropertiesMissing, "ProjectValidation_VersionPropertiesMissing", 0);

    public static readonly ErrorDefinition WinuiProjectMissing =
        new(ProjectValidationCodes.WinuiProjectMissing, "ProjectValidation_WinuiProjectMissing", 0);

    public static readonly ErrorDefinition CommonProjectMissing =
        new(ProjectValidationCodes.CommonProjectMissing, "ProjectValidation_CommonProjectMissing", 0);

    public static readonly ErrorDefinition HybridHostMissing =
        new(ProjectValidationCodes.HybridHostMissing, "ProjectValidation_HybridHostMissing", 0);

    public static readonly ErrorDefinition ShellMissing =
        new(ProjectValidationCodes.ShellMissing, "ProjectValidation_ShellMissing", 0);

    public static readonly ErrorDefinition UiComponentsMissing =
        new(ProjectValidationCodes.UiComponentsMissing, "ProjectValidation_UiComponentsMissing", 0);

    public static readonly ErrorDefinition EnglishResourcesMissing =
        new(ProjectValidationCodes.EnglishResourcesMissing, "ProjectValidation_EnglishResourcesMissing", 0);

    public static readonly ErrorDefinition ChineseResourcesMissing =
        new(ProjectValidationCodes.ChineseResourcesMissing, "ProjectValidation_ChineseResourcesMissing", 0);

    public static readonly ErrorDefinition LoggingMissing =
        new(ProjectValidationCodes.LoggingMissing, "ProjectValidation_LoggingMissing", 0);

    public static readonly ErrorDefinition SettingsMissing =
        new(ProjectValidationCodes.SettingsMissing, "ProjectValidation_SettingsMissing", 0);

    public static readonly ErrorDefinition SingleInstanceMissing =
        new(ProjectValidationCodes.SingleInstanceMissing, "ProjectValidation_SingleInstanceMissing", 0);

    public static readonly ErrorDefinition UpdateLifecycleMissing =
        new(ProjectValidationCodes.UpdateLifecycleMissing, "ProjectValidation_UpdateLifecycleMissing", 0);

    public static readonly ErrorDefinition UpdateDownloaderMissing =
        new(ProjectValidationCodes.UpdateDownloaderMissing, "ProjectValidation_UpdateDownloaderMissing", 0);

    public static readonly ErrorDefinition ReleaseConfigMissing =
        new(ProjectValidationCodes.ReleaseConfigMissing, "ProjectValidation_ReleaseConfigMissing", 0);

    public static readonly ErrorDefinition InstallerScriptMissing =
        new(ProjectValidationCodes.InstallerScriptMissing, "ProjectValidation_InstallerScriptMissing", 0);

    public static readonly ErrorDefinition AutostartMissing =
        new(ProjectValidationCodes.AutostartMissing, "ProjectValidation_AutostartMissing", 0);

    public static readonly ErrorDefinition BackgroundHostMissing =
        new(ProjectValidationCodes.BackgroundHostMissing, "ProjectValidation_BackgroundHostMissing", 0);

    public static readonly ErrorDefinition GrpcClientMissing =
        new(ProjectValidationCodes.GrpcClientMissing, "ProjectValidation_GrpcClientMissing", 0);

    public static readonly ErrorDefinition GrpcServiceMissing =
        new(ProjectValidationCodes.GrpcServiceMissing, "ProjectValidation_GrpcServiceMissing", 0);

    public static readonly ErrorDefinition TrayWindowMissing =
        new(ProjectValidationCodes.TrayWindowMissing, "ProjectValidation_TrayWindowMissing", 0);

    public static readonly ErrorDefinition TrayWindowCodeMissing =
        new(ProjectValidationCodes.TrayWindowMissing, "ProjectValidation_TrayWindowCodeMissing", 0);

    public static readonly ErrorDefinition ValidatorUnavailable =
        new(ProjectValidationCodes.ValidatorUnavailable, "ProjectValidation_ValidatorUnavailable", 1);

    public static readonly ErrorDefinition ScaffoldFileMissing =
        new(ProjectValidationCodes.ScaffoldFileMissing, "ProjectValidation_ScaffoldFileMissing", 0);

    public static readonly ErrorDefinition ScaffoldFileChanged =
        new(ProjectValidationCodes.ScaffoldFileChanged, "ProjectValidation_ScaffoldFileChanged", 0);

    public static readonly ErrorDefinition RecipeFileMissing =
        new(ProjectValidationCodes.RecipeFileMissing, "ProjectValidation_RecipeFileMissing", 1);

    public static readonly ErrorDefinition RecipeFileChanged =
        new(ProjectValidationCodes.RecipeFileChanged, "ProjectValidation_RecipeFileChanged", 1);

}
