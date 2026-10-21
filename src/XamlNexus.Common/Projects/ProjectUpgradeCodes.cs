namespace XamlNexus.Common.Projects;

/// <summary>稳定错误编号的唯一登记处；保持对外编号兼容。</summary>
public static class ProjectUpgradeCodes {
    public const string ConflictExportStale = "XU2020";
    public const string ConflictResolutionInvalid = "XU2021";
    public const string BaselineVersionMismatch = "XU1006";
    public const string BaselineAlreadyPresent = "XU1007";
    public const string TargetBaselineMissing = "XU1002";
    public const string ScaffoldFileMissing = "XU2001";
    public const string ScaffoldFileChanged = "XU2002";
    public const string BaselineMissing = "XU1001";
    public const string DestinationOccupied = "XU2003";
    public const string ModifiedFileRemoved = "XU2010";
    public const string ConflictsPreventApply = "XU2004";
    public const string PlanVersionMismatch = "XU2007";
    public const string ManifestChanged = "XU2008";
    public const string CreatePathAppeared = "XU2005";
    public const string ManagedFileChanged = "XU2006";
    public const string UpgradeFailed = "XU3001";
    public const string ModuleOwnershipConflict = "XU2014";
    public const string FileOwnershipConflict = "XU2013";
    public const string ProjectIdentityMismatch = "XU1003";
    public const string TargetFileStale = "XU1004";
    public const string PathEscapesProject = "XU1005";
    public const string DowngradeUnsupported = "XU1008";
    public const string UnsupportedMergeContent = "XU2012";
    public const string MergeConflict = "XU2011";
}
