namespace XamlNexus.Tooling.Diagnostics;

/// <summary>集中绑定诊断场景、稳定编号及中英文资源 key。</summary>
public static class DoctorChecks {
    public static readonly DoctorCheckDefinition Windows =
        new(DoctorCodes.OperatingSystem, DoctorCategories.Environment, XamlNexusDoctorSeverity.Pass, "DoctorWindows", 1);

    public static readonly DoctorCheckDefinition RequiresWindows =
        new(DoctorCodes.OperatingSystem, DoctorCategories.Environment, XamlNexusDoctorSeverity.Error, "DoctorRequiresWindows", 0);

    public static readonly DoctorCheckDefinition SdkResolveFailed =
        new(DoctorCodes.DotnetSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Error, "DoctorSdkResolveFailed", 1);

    public static readonly DoctorCheckDefinition SdkSelected =
        new(DoctorCodes.DotnetSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Pass, "DoctorSdkSelected", 1);

    public static readonly DoctorCheckDefinition SdkUnsupported =
        new(DoctorCodes.DotnetSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Error, "DoctorSdkUnsupported", 1);

    public static readonly DoctorCheckDefinition DotnetFailed =
        new(DoctorCodes.DotnetSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Error, "DoctorDotnetFailed", 1);

    public static readonly DoctorCheckDefinition NugetFailed =
        new(DoctorCodes.NugetSources, DoctorCategories.Environment, XamlNexusDoctorSeverity.Warning, "DoctorNugetFailed", 1);

    public static readonly DoctorCheckDefinition NugetSources =
        new(DoctorCodes.NugetSources, DoctorCategories.Environment, XamlNexusDoctorSeverity.Pass, "DoctorNugetSources", 1);

    public static readonly DoctorCheckDefinition NugetMissing =
        new(DoctorCodes.NugetSources, DoctorCategories.Environment, XamlNexusDoctorSeverity.Warning, "DoctorNugetMissing", 0);

    /// <summary>清单与受管理的项目结构一致。</summary>
    public static readonly DoctorCheckDefinition ProjectConsistent =
        new(DoctorCodes.ProjectStructure, DoctorCategories.Project, XamlNexusDoctorSeverity.Pass, "Doctor_ProjectConsistent", 0);

    /// <summary>无法检查项目 XML：{0}</summary>
    public static readonly DoctorCheckDefinition ProjectXmlUnreadable =
        new(DoctorCodes.ProjectXml, DoctorCategories.WindowsAppSdk, XamlNexusDoctorSeverity.Warning, "Doctor_ProjectXmlUnreadable", 1);

    /// <summary>检测到 {0} 个面向 Windows 的项目。</summary>
    public static readonly DoctorCheckDefinition WindowsProjectsFound =
        new(DoctorCodes.WindowsTargets, DoctorCategories.WindowsAppSdk, XamlNexusDoctorSeverity.Pass, "Doctor_WindowsProjectsFound", 1);

    /// <summary>未检测到面向 Windows 的项目。</summary>
    public static readonly DoctorCheckDefinition WindowsProjectsMissing =
        new(DoctorCodes.WindowsTargets, DoctorCategories.WindowsAppSdk, XamlNexusDoctorSeverity.Error, "Doctor_WindowsProjectsMissing", 0);

    /// <summary>未引用 Microsoft.WindowsAppSDK。</summary>
    public static readonly DoctorCheckDefinition AppSdkMissing =
        new(DoctorCodes.WindowsAppSdk, DoctorCategories.WindowsAppSdk, XamlNexusDoctorSeverity.Error, "Doctor_AppSdkMissing", 0);

    /// <summary>已引用 Microsoft.WindowsAppSDK {0}。</summary>
    public static readonly DoctorCheckDefinition AppSdkReferenced =
        new(DoctorCodes.WindowsAppSdk, DoctorCategories.WindowsAppSdk, XamlNexusDoctorSeverity.Pass, "Doctor_AppSdkReferenced", 1);

    /// <summary>引用了多个 Microsoft.WindowsAppSDK 版本：{0}。</summary>
    public static readonly DoctorCheckDefinition AppSdkVersionsDiffer =
        new(DoctorCodes.WindowsAppSdk, DoctorCategories.WindowsAppSdk, XamlNexusDoctorSeverity.Warning, "Doctor_AppSdkVersionsDiffer", 1);

    /// <summary>未安装 Recipe 模块。</summary>
    public static readonly DoctorCheckDefinition RecipesEmpty =
        new(DoctorCodes.InstalledRecipes, DoctorCategories.Recipes, XamlNexusDoctorSeverity.Pass, "Doctor_RecipesEmpty", 0);

    /// <summary>当前目录中没有已安装的 Recipe“{0}”的定义。</summary>
    public static readonly DoctorCheckDefinition RecipeUnavailable =
        new(DoctorCodes.RecipeAvailability, DoctorCategories.Recipes, XamlNexusDoctorSeverity.Warning, "Doctor_RecipeUnavailable", 1);

    /// <summary>Recipe“{0}”的版本为 {1}，与目录一致。</summary>
    public static readonly DoctorCheckDefinition RecipeVersionMatches =
        new(DoctorCodes.RecipeVersionMatch, DoctorCategories.Recipes, XamlNexusDoctorSeverity.Pass, "Doctor_RecipeVersionMatches", 2);

    /// <summary>Recipe“{0}”的已安装版本为 {1}，目录提供的版本为 {2}。</summary>
    public static readonly DoctorCheckDefinition RecipeVersionDiffers =
        new(DoctorCodes.RecipeVersionMismatch, DoctorCategories.Recipes, XamlNexusDoctorSeverity.Warning, "Doctor_RecipeVersionDiffers", 3);

    /// <summary>拥有数据库的进程已引用 Data 项目。</summary>
    public static readonly DoctorCheckDefinition DataReferencePresent =
        new(DoctorCodes.DataProjectReference, DoctorCategories.Sqlite, XamlNexusDoctorSeverity.Pass, "Doctor_DataReferencePresent", 0);

    /// <summary>拥有数据库的进程未引用 Data 项目。</summary>
    public static readonly DoctorCheckDefinition DataReferenceMissing =
        new(DoctorCodes.DataProjectReference, DoctorCategories.Sqlite, XamlNexusDoctorSeverity.Error, "Doctor_DataReferenceMissing", 0);

    /// <summary>数据库服务源码中存在 WAL 相关文本；尚未验证运行时日志模式。</summary>
    public static readonly DoctorCheckDefinition WalTextFound =
        new(DoctorCodes.WalInitialization, DoctorCategories.Sqlite, XamlNexusDoctorSeverity.Pass, "Doctor_WalTextFound", 0);

    /// <summary>无法从数据库服务源码确认 WAL 初始化。允许自定义初始化或其他日志模式；请按需验证运行时配置。</summary>
    public static readonly DoctorCheckDefinition WalTextMissing =
        new(DoctorCodes.WalInitialization, DoctorCategories.Sqlite, XamlNexusDoctorSeverity.Warning, "Doctor_WalTextMissing", 0);

    /// <summary>已包含混合架构的应用状态 Protobuf 集成。</summary>
    public static readonly DoctorCheckDefinition ProtobufPresent =
        new(DoctorCodes.ProtobufIntegration, DoctorCategories.Sqlite, XamlNexusDoctorSeverity.Pass, "Doctor_ProtobufPresent", 0);

    /// <summary>缺少混合架构的应用状态 Protobuf 集成。</summary>
    public static readonly DoctorCheckDefinition ProtobufMissing =
        new(DoctorCodes.ProtobufIntegration, DoctorCategories.Sqlite, XamlNexusDoctorSeverity.Error, "Doctor_ProtobufMissing", 0);

    /// <summary>未声明 GitHub 发布模块。</summary>
    public static readonly DoctorCheckDefinition ReleaseModuleMissing =
        new(DoctorCodes.ReleaseModule, DoctorCategories.Publishing, XamlNexusDoctorSeverity.Warning, "Doctor_ReleaseModuleMissing", 0);

    /// <summary>发布元数据包含所有必填字段。</summary>
    public static readonly DoctorCheckDefinition ReleaseMetadataComplete =
        new(DoctorCodes.ReleaseMetadata, DoctorCategories.Publishing, XamlNexusDoctorSeverity.Pass, "Doctor_ReleaseMetadataComplete", 0);

    /// <summary>发布元数据缺少：{0}。</summary>
    public static readonly DoctorCheckDefinition ReleaseMetadataMissing =
        new(DoctorCodes.ReleaseMetadata, DoctorCategories.Publishing, XamlNexusDoctorSeverity.Error, "Doctor_ReleaseMetadataMissing", 1);

    /// <summary>发布元数据无效：{0}</summary>
    public static readonly DoctorCheckDefinition ReleaseMetadataInvalid =
        new(DoctorCodes.ReleaseMetadata, DoctorCategories.Publishing, XamlNexusDoctorSeverity.Error, "Doctor_ReleaseMetadataInvalid", 1);

    public static readonly DoctorCheckDefinition Architecture =
        new(DoctorCodes.Architecture, DoctorCategories.Environment, XamlNexusDoctorSeverity.Pass, "DoctorArchitecture", 2);

    public static readonly DoctorCheckDefinition SdkInspectionFailed =
        new(DoctorCodes.WindowsSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Warning, "DoctorSdkInspectionFailed", 1);

    public static readonly DoctorCheckDefinition EnvironmentBoundary =
        new(DoctorCodes.EnvironmentBoundary, DoctorCategories.Environment, XamlNexusDoctorSeverity.Warning, "DoctorEnvironmentBoundary", 0);

    public static readonly DoctorCheckDefinition SdkFound =
        new(DoctorCodes.WindowsSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Pass, "DoctorSdkFound", 2);

    public static readonly DoctorCheckDefinition SdkUnknown =
        new(DoctorCodes.WindowsSdk, DoctorCategories.Environment, XamlNexusDoctorSeverity.Warning, "DoctorSdkUnknown", 0);

}
