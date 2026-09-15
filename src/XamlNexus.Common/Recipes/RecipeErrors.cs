namespace XamlNexus.Common.Recipes;

/// <summary>按错误场景集中绑定稳定错误码和文案资源 key；不同场景允许共用错误码。</summary>
public static class XamlNexusRecipeErrors {
    /// <summary>模块“{0}”已安装</summary>
    public static readonly RecipeErrorDefinition AlreadyInstalled =
        new(XamlNexusRecipeErrorCodes.AlreadyInstalled, "RecipeError_AlreadyInstalled", 1);

    /// <summary>Recipe 未返回安装计划</summary>
    public static readonly RecipeErrorDefinition MissingInstallationPlan =
        new(XamlNexusRecipeErrorCodes.MissingInstallationPlan, "RecipeError_MissingInstallationPlan", 0);

    /// <summary>组合后的批量计划未通过项目校验</summary>
    public static readonly RecipeErrorDefinition InvalidComposedBatch =
        new(XamlNexusRecipeErrorCodes.InvalidComposedBatch, "RecipeError_InvalidComposedBatch", 0);

    /// <summary>此批量计划属于另一个项目</summary>
    public static readonly RecipeErrorDefinition BatchProjectMismatch =
        new(XamlNexusRecipeErrorCodes.BatchProjectMismatch, "RecipeError_BatchProjectMismatch", 0);

    /// <summary>批量规划后项目清单已改变，请重新预览操作</summary>
    public static readonly RecipeErrorDefinition StaleBatchManifest =
        new(XamlNexusRecipeErrorCodes.StaleBatchManifest, "RecipeError_StaleBatchManifest", 0);

    /// <summary>Recipe 目录包含重复 ID“{0}”</summary>
    public static readonly RecipeErrorDefinition DuplicateCatalogId =
        new(XamlNexusRecipeErrorCodes.DuplicateCatalogId, "RecipeError_DuplicateCatalogId", 1);

    /// <summary>Recipe ID 必须使用小写 kebab-case 格式</summary>
    public static readonly RecipeErrorDefinition InvalidRecipeId =
        new(XamlNexusRecipeErrorCodes.InvalidRecipeId, "RecipeError_InvalidRecipeId", 0);

    /// <summary>Recipe 版本必须是语义化版本，例如 1.0.0</summary>
    public static readonly RecipeErrorDefinition InvalidRecipeVersion =
        new(XamlNexusRecipeErrorCodes.InvalidRecipeVersion, "RecipeError_InvalidRecipeVersion", 0);

    /// <summary>必须提供 Recipe 显示名称</summary>
    public static readonly RecipeErrorDefinition MissingDisplayName =
        new(XamlNexusRecipeErrorCodes.MissingDisplayName, "RecipeError_MissingDisplayName", 0);

    /// <summary>支持的预设必须包含 winui、hybrid 中的至少一项</summary>
    public static readonly RecipeErrorDefinition InvalidSupportedPresets =
        new(XamlNexusRecipeErrorCodes.InvalidSupportedPresets, "RecipeError_InvalidSupportedPresets", 0);

    /// <summary>Recipe 依赖项不能重复</summary>
    public static readonly RecipeErrorDefinition DuplicateDependencies =
        new(XamlNexusRecipeErrorCodes.DuplicateDependencies, "RecipeError_DuplicateDependencies", 0);

    /// <summary>Recipe 冲突项不能重复</summary>
    public static readonly RecipeErrorDefinition DuplicateConflicts =
        new(XamlNexusRecipeErrorCodes.DuplicateConflicts, "RecipeError_DuplicateConflicts", 0);

    /// <summary>Recipe 不能依赖自身或与自身冲突</summary>
    public static readonly RecipeErrorDefinition SelfDependencyOrConflict =
        new(XamlNexusRecipeErrorCodes.SelfDependencyOrConflict, "RecipeError_SelfDependencyOrConflict", 0);

    /// <summary>同一模块不能同时是依赖项和冲突项</summary>
    public static readonly RecipeErrorDefinition DependencyConflictOverlap =
        new(XamlNexusRecipeErrorCodes.DependencyConflictOverlap, "RecipeError_DependencyConflictOverlap", 0);

    /// <summary>Recipe“{0}”不支持预设“{1}”</summary>
    public static readonly RecipeErrorDefinition UnsupportedPreset =
        new(XamlNexusRecipeErrorCodes.UnsupportedPreset, "RecipeError_UnsupportedPreset", 2);

    /// <summary>缺少依赖项：{0}</summary>
    public static readonly RecipeErrorDefinition MissingDependencies =
        new(XamlNexusRecipeErrorCodes.MissingDependencies, "RecipeError_MissingDependencies", 1);

    /// <summary>已安装冲突模块：{0}</summary>
    public static readonly RecipeErrorDefinition ConflictingModules =
        new(XamlNexusRecipeErrorCodes.ConflictingModules, "RecipeError_ConflictingModules", 1);

    /// <summary>Recipe 计划包含空的文件变更</summary>
    public static readonly RecipeErrorDefinition EmptyFileChange =
        new(XamlNexusRecipeErrorCodes.EmptyFileChange, "RecipeError_EmptyFileChange", 0);

    /// <summary>Recipe 文件路径必须相对于项目根目录</summary>
    public static readonly RecipeErrorDefinition FilePathMustBeRelative =
        new(XamlNexusRecipeErrorCodes.FilePathMustBeRelative, "RecipeError_FilePathMustBeRelative", 0);

    /// <summary>Recipe 路径超出项目目录：{0}</summary>
    public static readonly RecipeErrorDefinition FilePathEscapesProject =
        new(XamlNexusRecipeErrorCodes.FilePathEscapesProject, "RecipeError_FilePathEscapesProject", 1);

    /// <summary>Recipe 不能直接编辑 xamlnexus.json</summary>
    public static readonly RecipeErrorDefinition DirectManifestEditForbidden =
        new(XamlNexusRecipeErrorCodes.DirectManifestEditForbidden, "RecipeError_DirectManifestEditForbidden", 0);

    /// <summary>Recipe 多次修改同一路径：{0}</summary>
    public static readonly RecipeErrorDefinition DuplicateFileChange =
        new(XamlNexusRecipeErrorCodes.DuplicateFileChange, "RecipeError_DuplicateFileChange", 1);

    /// <summary>Recipe 文件目标是已有目录：{0}</summary>
    public static readonly RecipeErrorDefinition FileTargetIsDirectory =
        new(XamlNexusRecipeErrorCodes.FileTargetIsDirectory, "RecipeError_FileTargetIsDirectory", 1);

    /// <summary>父路径是已有文件：{0}</summary>
    public static readonly RecipeErrorDefinition ParentPathIsFile =
        new(XamlNexusRecipeErrorCodes.ParentPathIsFile, "RecipeError_ParentPathIsFile", 1);

    /// <summary>Recipe 将覆盖已有文件：{0}</summary>
    public static readonly RecipeErrorDefinition FileAlreadyExists =
        new(XamlNexusRecipeErrorCodes.FileAlreadyExists, "RecipeError_FileAlreadyExists", 1);

    /// <summary>创建操作缺少内容：{0}</summary>
    public static readonly RecipeErrorDefinition MissingCreateContent =
        new(XamlNexusRecipeErrorCodes.MissingCreateContent, "RecipeError_MissingCreateContent", 1);

    /// <summary>Recipe 所需文件不存在：{0}</summary>
    public static readonly RecipeErrorDefinition ExpectedFileMissing =
        new(XamlNexusRecipeErrorCodes.ExpectedFileMissing, "RecipeError_ExpectedFileMissing", 1);

    /// <summary>替换操作缺少内容：{0}</summary>
    public static readonly RecipeErrorDefinition MissingReplaceContent =
        new(XamlNexusRecipeErrorCodes.MissingReplaceContent, "RecipeError_MissingReplaceContent", 1);

    /// <summary>必须提供有效的预期 SHA-256：{0}</summary>
    public static readonly RecipeErrorDefinition InvalidExpectedHash =
        new(XamlNexusRecipeErrorCodes.InvalidExpectedHash, "RecipeError_InvalidExpectedHash", 1);

    /// <summary>文件已修改，不符合 Recipe 前置条件：{0}</summary>
    public static readonly RecipeErrorDefinition FileHashMismatch =
        new(XamlNexusRecipeErrorCodes.FileHashMismatch, "RecipeError_FileHashMismatch", 1);

    /// <summary>无效的 {0} 模块 ID“{1}”</summary>
    public static readonly RecipeErrorDefinition InvalidRelatedModuleId =
        new(XamlNexusRecipeErrorCodes.InvalidRelatedModuleId, "RecipeError_InvalidRelatedModuleId", 2);

    /// <summary>Recipe 计划包含空的项目操作</summary>
    public static readonly RecipeErrorDefinition EmptyProjectOperation =
        new(XamlNexusRecipeErrorCodes.EmptyProjectOperation, "RecipeError_EmptyProjectOperation", 0);

    /// <summary>不支持的项目操作：{0}</summary>
    public static readonly RecipeErrorDefinition UnsupportedProjectOperation =
        new(XamlNexusRecipeErrorCodes.UnsupportedProjectOperation, "RecipeError_UnsupportedProjectOperation", 1);

    /// <summary>项目操作与文件操作指向同一文件：{0}</summary>
    public static readonly RecipeErrorDefinition OverlappingFileAndProjectOperations =
        new(XamlNexusRecipeErrorCodes.OverlappingFileAndProjectOperations, "RecipeError_OverlappingFileAndProjectOperations", 1);

    /// <summary>项目操作目标不存在：{0}</summary>
    public static readonly RecipeErrorDefinition ProjectOperationTargetMissing =
        new(XamlNexusRecipeErrorCodes.ProjectOperationTargetMissing, "RecipeError_ProjectOperationTargetMissing", 1);

    /// <summary>不支持的项目操作文件类型：{0}</summary>
    public static readonly RecipeErrorDefinition UnsupportedProjectFileType =
        new(XamlNexusRecipeErrorCodes.UnsupportedProjectFileType, "RecipeError_UnsupportedProjectFileType", 1);

    /// <summary>无效的 MSBuild 项目 XML：{0}</summary>
    public static readonly RecipeErrorDefinition InvalidProjectXml =
        new(XamlNexusRecipeErrorCodes.InvalidProjectXml, "RecipeError_InvalidProjectXml", 1);

    /// <summary>MSBuild 项目缺少根元素</summary>
    public static readonly RecipeErrorDefinition MissingProjectRoot =
        new(XamlNexusRecipeErrorCodes.InvalidProjectXml, "RecipeError_MissingProjectRoot", 0);

    /// <summary>PackageReference“{0}”已存在</summary>
    public static readonly RecipeErrorDefinition PackageReferenceAlreadyExists =
        new(XamlNexusRecipeErrorCodes.PackageReferenceAlreadyExists, "RecipeError_PackageReferenceAlreadyExists", 1);

    /// <summary>引用的项目不存在：{0}</summary>
    public static readonly RecipeErrorDefinition ReferencedProjectMissing =
        new(XamlNexusRecipeErrorCodes.ReferencedProjectMissing, "RecipeError_ReferencedProjectMissing", 1);

    /// <summary>ProjectReference“{0}”已存在</summary>
    public static readonly RecipeErrorDefinition ProjectReferenceAlreadyExists =
        new(XamlNexusRecipeErrorCodes.ProjectReferenceAlreadyExists, "RecipeError_ProjectReferenceAlreadyExists", 1);

    /// <summary>ProjectReference“{0}”不存在</summary>
    public static readonly RecipeErrorDefinition ProjectReferenceMissing =
        new(XamlNexusRecipeErrorCodes.ProjectReferenceMissing, "RecipeError_ProjectReferenceMissing", 1);

    /// <summary>Protobuf 源文件不存在：{0}</summary>
    public static readonly RecipeErrorDefinition ProtobufSourceMissing =
        new(XamlNexusRecipeErrorCodes.ProtobufSourceMissing, "RecipeError_ProtobufSourceMissing", 1);

    /// <summary>Protobuf 源文件必须是 .proto 文件：{0}</summary>
    public static readonly RecipeErrorDefinition InvalidProtobufExtension =
        new(XamlNexusRecipeErrorCodes.InvalidProtobufExtension, "RecipeError_InvalidProtobufExtension", 1);

    /// <summary>Protobuf 源文件已包含：{0}</summary>
    public static readonly RecipeErrorDefinition ProtobufAlreadyIncluded =
        new(XamlNexusRecipeErrorCodes.ProtobufAlreadyIncluded, "RecipeError_ProtobufAlreadyIncluded", 1);

    /// <summary>未包含 Protobuf 源文件：{0}</summary>
    public static readonly RecipeErrorDefinition ProtobufReferenceMissing =
        new(XamlNexusRecipeErrorCodes.ProtobufReferenceMissing, "RecipeError_ProtobufReferenceMissing", 1);

    /// <summary>只有引用操作可以应用于 .csproj 文件</summary>
    public static readonly RecipeErrorDefinition InvalidCsprojOperation =
        new(XamlNexusRecipeErrorCodes.InvalidCsprojOperation, "RecipeError_InvalidCsprojOperation", 0);

    /// <summary>SLNX XML 无效</summary>
    public static readonly RecipeErrorDefinition InvalidSlnxXml =
        new(XamlNexusRecipeErrorCodes.InvalidSolution, "RecipeError_InvalidSlnxXml", 0);

    /// <summary>SLNX 必须包含 Solution 根元素</summary>
    public static readonly RecipeErrorDefinition MissingSolutionRoot =
        new(XamlNexusRecipeErrorCodes.InvalidSolution, "RecipeError_MissingSolutionRoot", 0);

    /// <summary>只有解决方案操作可以应用于 .slnx 文件</summary>
    public static readonly RecipeErrorDefinition InvalidSlnxOperation =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionOperation, "RecipeError_InvalidSlnxOperation", 0);

    /// <summary>项目不在解决方案中：{0}</summary>
    public static readonly RecipeErrorDefinition ProjectNotInSolution =
        new(XamlNexusRecipeErrorCodes.ProjectNotInSolution, "RecipeError_ProjectNotInSolution", 1);

    /// <summary>解决方案项目不存在：{0}</summary>
    public static readonly RecipeErrorDefinition SolutionProjectMissing =
        new(XamlNexusRecipeErrorCodes.SolutionProjectMissing, "RecipeError_SolutionProjectMissing", 1);

    /// <summary>解决方案项目必须是 .csproj 文件</summary>
    public static readonly RecipeErrorDefinition InvalidSolutionProjectType =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionProjectType, "RecipeError_InvalidSolutionProjectType", 0);

    /// <summary>项目已存在于解决方案中：{0}</summary>
    public static readonly RecipeErrorDefinition ProjectAlreadyInSolution =
        new(XamlNexusRecipeErrorCodes.ProjectAlreadyInSolution, "RecipeError_ProjectAlreadyInSolution", 1);

    /// <summary>解决方案文件缺少 Global 段</summary>
    public static readonly RecipeErrorDefinition MissingGlobalSection =
        new(XamlNexusRecipeErrorCodes.InvalidSolution, "RecipeError_MissingGlobalSection", 0);

    /// <summary>只有解决方案操作可以应用于 .sln 文件</summary>
    public static readonly RecipeErrorDefinition InvalidSlnOperation =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionOperation, "RecipeError_InvalidSlnOperation", 0);

    /// <summary>解决方案项目必须是 .csproj 文件：{0}</summary>
    public static readonly RecipeErrorDefinition InvalidSolutionProjectPath =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionProjectType, "RecipeError_InvalidSolutionProjectPath", 1);

    /// <summary>解决方案项目条目不完整：{0}</summary>
    public static readonly RecipeErrorDefinition IncompleteSolutionProject =
        new(XamlNexusRecipeErrorCodes.IncompleteSolutionProject, "RecipeError_IncompleteSolutionProject", 1);

    /// <summary>{0} 路径必须是相对路径</summary>
    public static readonly RecipeErrorDefinition OperationPathMustBeRelative =
        new(XamlNexusRecipeErrorCodes.OperationPathMustBeRelative, "RecipeError_OperationPathMustBeRelative", 1);

    /// <summary>{0} 路径超出项目目录：{1}</summary>
    public static readonly RecipeErrorDefinition OperationPathEscapesProject =
        new(XamlNexusRecipeErrorCodes.OperationPathEscapesProject, "RecipeError_OperationPathEscapesProject", 2);

    /// <summary>解决方案文件缺少配置段</summary>
    public static readonly RecipeErrorDefinition MissingConfigurationSections =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionConfigurations, "RecipeError_MissingConfigurationSections", 0);

    /// <summary>解决方案配置段不完整</summary>
    public static readonly RecipeErrorDefinition IncompleteConfigurationSection =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionConfigurations, "RecipeError_IncompleteConfigurationSection", 0);

    /// <summary>解决方案没有构建配置</summary>
    public static readonly RecipeErrorDefinition MissingBuildConfigurations =
        new(XamlNexusRecipeErrorCodes.InvalidSolutionConfigurations, "RecipeError_MissingBuildConfigurations", 0);

    /// <summary>PackageReference 必须提供有效的包 ID 和版本</summary>
    public static readonly RecipeErrorDefinition InvalidPackageReference =
        new(XamlNexusRecipeErrorCodes.InvalidPackageReference, "RecipeError_InvalidPackageReference", 0);

    /// <summary>PackageReference 必须提供有效的包 ID 和数字形式的最低版本</summary>
    public static readonly RecipeErrorDefinition InvalidMinimumPackageVersion =
        new(XamlNexusRecipeErrorCodes.InvalidPackageReference, "RecipeError_InvalidMinimumPackageVersion", 0);

    /// <summary>“{1}”中的 PackageReference“{0}”带有条件。XamlNexus 无法保证所有构建配置都满足依赖要求，因此不会自动修改。请检查条件，提供满足最低版本要求的无条件引用后重试</summary>
    public static readonly RecipeErrorDefinition ConditionalPackageReference =
        new(XamlNexusRecipeErrorCodes.ConditionalPackageReference, "RecipeError_ConditionalPackageReference", 2);

    /// <summary>PackageReference“{0}”使用不支持的版本表达式“{1}”</summary>
    public static readonly RecipeErrorDefinition UnsupportedPackageVersion =
        new(XamlNexusRecipeErrorCodes.UnsupportedPackageVersion, "RecipeError_UnsupportedPackageVersion", 2);

    /// <summary>模块“{0}”未安装</summary>
    public static readonly RecipeErrorDefinition RemovalModuleNotInstalled =
        new(XamlNexusRecipeErrorCodes.RemovalModuleNotInstalled, "RecipeError_RemovalModuleNotInstalled", 1);

    /// <summary>模块“{0}”不是通过 Recipe 安装的，不能以此方式移除</summary>
    public static readonly RecipeErrorDefinition RemovalRequiresRecipeModule =
        new(XamlNexusRecipeErrorCodes.RemovalRequiresRecipeModule, "RecipeError_RemovalRequiresRecipeModule", 1);

    /// <summary>模块“{0}”未安装</summary>
    public static readonly RecipeErrorDefinition UpdateModuleNotInstalled =
        new(XamlNexusRecipeErrorCodes.UpdateModuleNotInstalled, "RecipeError_UpdateModuleNotInstalled", 1);

    /// <summary>模块“{0}”不是通过 Recipe 安装的，不能以此方式更新</summary>
    public static readonly RecipeErrorDefinition UpdateRequiresRecipeModule =
        new(XamlNexusRecipeErrorCodes.UpdateRequiresRecipeModule, "RecipeError_UpdateRequiresRecipeModule", 1);

    /// <summary>Recipe“{0}”已是版本 {1}</summary>
    public static readonly RecipeErrorDefinition AlreadyAtTargetVersion =
        new(XamlNexusRecipeErrorCodes.AlreadyAtTargetVersion, "RecipeError_AlreadyAtTargetVersion", 2);

    /// <summary>不支持将 Recipe 从 {0} 降级到 {1}</summary>
    public static readonly RecipeErrorDefinition DowngradeNotSupported =
        new(XamlNexusRecipeErrorCodes.DowngradeNotSupported, "RecipeError_DowngradeNotSupported", 2);

    /// <summary>Recipe“{0}”不支持预设“{1}”</summary>
    public static readonly RecipeErrorDefinition UpdateUnsupportedPreset =
        new(XamlNexusRecipeErrorCodes.UpdateUnsupportedPreset, "RecipeError_UpdateUnsupportedPreset", 2);

    /// <summary>缺少依赖项：{0}</summary>
    public static readonly RecipeErrorDefinition UpdateMissingDependencies =
        new(XamlNexusRecipeErrorCodes.UpdateMissingDependencies, "RecipeError_UpdateMissingDependencies", 1);

    /// <summary>已安装冲突模块：{0}</summary>
    public static readonly RecipeErrorDefinition UpdateConflictingModules =
        new(XamlNexusRecipeErrorCodes.UpdateConflictingModules, "RecipeError_UpdateConflictingModules", 1);

    /// <summary>项目上下文加载后清单已改变，请重新加载项目并预览操作</summary>
    public static readonly RecipeErrorDefinition StaleProjectContext =
        new(XamlNexusRecipeErrorCodes.StaleProjectContext, "RecipeError_StaleProjectContext", 0);

    /// <summary>不支持的文件变更类型：{0}</summary>
    public static readonly RecipeErrorDefinition UnsupportedFileChangeKind =
        new(XamlNexusRecipeErrorCodes.UnsupportedFileChangeKind, "RecipeError_UnsupportedFileChangeKind", 1);

    /// <summary>事务失败，但所有文件均已回滚。</summary>
    public static readonly RecipeErrorDefinition TransactionRolledBack =
        new(XamlNexusRecipeErrorCodes.TransactionFailed, "RecipeError_TransactionRolledBack", 1);

    /// <summary>事务失败且回滚发生额外错误。</summary>
    public static readonly RecipeErrorDefinition TransactionRollbackFailed =
        new(XamlNexusRecipeErrorCodes.TransactionFailed, "RecipeError_TransactionRollbackFailed", 2);

    /// <summary>此 WinUI 脚手架不支持窗口关闭设置。请使用最新 XamlNexus 重新生成项目，或先迁移脚手架再安装 settings/tray</summary>
    public static readonly RecipeErrorDefinition MissingTrayScaffoldSupport =
        new(XamlNexusRecipeErrorCodes.MissingTrayScaffoldSupport, "RecipeError_MissingTrayScaffoldSupport", 0);

}
