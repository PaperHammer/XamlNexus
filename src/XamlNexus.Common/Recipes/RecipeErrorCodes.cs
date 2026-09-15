namespace XamlNexus.Common.Recipes;

/// <summary>Recipe 对外错误码的唯一登记处。场景与文案 key 的绑定见 XamlNexusRecipeErrors；保留编号以兼容调用方。</summary>
public static class XamlNexusRecipeErrorCodes {

    // 组件定义
    public const string InvalidRecipeId = "XR1001";
    public const string InvalidRecipeVersion = "XR1002";
    public const string MissingDisplayName = "XR1003";
    public const string InvalidSupportedPresets = "XR1004";
    public const string DuplicateDependencies = "XR1005";
    public const string DuplicateConflicts = "XR1006";
    public const string SelfDependencyOrConflict = "XR1007";
    public const string DependencyConflictOverlap = "XR1008";
    public const string InvalidRelatedModuleId = "XR1009";

    // 安装兼容性
    public const string UnsupportedPreset = "XR1101";
    public const string AlreadyInstalled = "XR1102";
    public const string MissingDependencies = "XR1103";
    public const string ConflictingModules = "XR1104";

    // 文件计划和项目结构操作
    public const string EmptyFileChange = "XR1201";
    public const string FilePathMustBeRelative = "XR1202";
    public const string FilePathEscapesProject = "XR1203";
    public const string DirectManifestEditForbidden = "XR1204";
    public const string DuplicateFileChange = "XR1205";
    public const string FileAlreadyExists = "XR1206";
    public const string MissingCreateContent = "XR1207";
    public const string ExpectedFileMissing = "XR1208";
    public const string MissingReplaceContent = "XR1209";
    public const string InvalidExpectedHash = "XR1210";
    public const string FileHashMismatch = "XR1211";
    public const string FileTargetIsDirectory = "XR1212";
    public const string ParentPathIsFile = "XR1213";
    public const string EmptyProjectOperation = "XR1220";
    public const string UnsupportedProjectOperation = "XR1221";
    public const string OverlappingFileAndProjectOperations = "XR1222";
    public const string ProjectOperationTargetMissing = "XR1223";
    public const string UnsupportedProjectFileType = "XR1224";
    public const string InvalidProjectXml = "XR1225";
    public const string PackageReferenceAlreadyExists = "XR1226";
    public const string ReferencedProjectMissing = "XR1227";
    public const string ProjectReferenceAlreadyExists = "XR1228";
    public const string InvalidCsprojOperation = "XR1229";
    public const string InvalidSolution = "XR1230";
    public const string InvalidSolutionOperation = "XR1231";
    public const string SolutionProjectMissing = "XR1232";
    public const string InvalidSolutionProjectType = "XR1233";
    public const string ProjectAlreadyInSolution = "XR1234";
    public const string OperationPathMustBeRelative = "XR1235";
    public const string OperationPathEscapesProject = "XR1236";
    public const string InvalidPackageReference = "XR1237";
    public const string InvalidSolutionConfigurations = "XR1238";
    public const string UnsupportedPackageVersion = "XR1239";
    public const string ProtobufSourceMissing = "XR1240";
    public const string InvalidProtobufExtension = "XR1241";
    public const string ProtobufAlreadyIncluded = "XR1242";
    public const string ProjectReferenceMissing = "XR1243";
    public const string ProtobufReferenceMissing = "XR1244";
    public const string ProjectNotInSolution = "XR1245";
    public const string IncompleteSolutionProject = "XR1246";
    public const string ConditionalPackageReference = "XR1247";

    // 事务执行
    public const string MissingInstallationPlan = "XR1301";
    public const string UnsupportedFileChangeKind = "XR1302";
    public const string TransactionFailed = "XR1303";
    public const string StaleProjectContext = "XR1304";

    // 卸载及历史目录错误
    public const string RemovalModuleNotInstalled = "XR1401";
    public const string RemovalRequiresRecipeModule = "XR1402";

    // 更新
    public const string UpdateModuleNotInstalled = "XR1501";
    public const string UpdateRequiresRecipeModule = "XR1502";
    public const string AlreadyAtTargetVersion = "XR1503";
    public const string DowngradeNotSupported = "XR1504";
    public const string UpdateUnsupportedPreset = "XR1505";
    public const string UpdateMissingDependencies = "XR1506";
    public const string UpdateConflictingModules = "XR1507";

    // 批量安装
    public const string InvalidComposedBatch = "XR1801";
    public const string BatchProjectMismatch = "XR1802";
    public const string StaleBatchManifest = "XR1803";

    // 模板支持
    public const string MissingTrayScaffoldSupport = "XR1901";

    /// <summary>历史兼容别名：目录 ID 重复和卸载模块不存在曾共用 XR1401，保留编号以兼容现有调用方。</summary>
    public const string DuplicateCatalogId = RemovalModuleNotInstalled;
}
