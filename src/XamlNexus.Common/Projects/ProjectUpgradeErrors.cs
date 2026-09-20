using XamlNexus.Common.Resources;

namespace XamlNexus.Common.Projects;

/// <summary>集中绑定错误场景、编号与中英文文案资源。</summary>
public static class ProjectUpgradeErrors {
    public static readonly ErrorDefinition ConflictExportStale =
        new(ProjectUpgradeCodes.ConflictExportStale, "ProjectUpgrade_ConflictExportStale", 0);

    public static readonly ErrorDefinition ManualResolutionRequired =
        new(ProjectUpgradeCodes.ConflictResolutionInvalid, "ProjectUpgrade_ManualResolutionRequired", 2);

    public static readonly ErrorDefinition ResolvedFileMissing =
        new(ProjectUpgradeCodes.ConflictResolutionInvalid, "ProjectUpgrade_ResolvedFileMissing", 1);

    public static readonly ErrorDefinition ConflictMarkersRemain =
        new(ProjectUpgradeCodes.ConflictResolutionInvalid, "ProjectUpgrade_ConflictMarkersRemain", 1);

    public static readonly ErrorDefinition ResolvedXmlInvalid =
        new(ProjectUpgradeCodes.ConflictResolutionInvalid, "ProjectUpgrade_ResolvedXmlInvalid", 1);

    public static readonly ErrorDefinition BaselineVersionMismatch =
        new(ProjectUpgradeCodes.BaselineVersionMismatch, "ProjectUpgrade_BaselineVersionMismatch", 0);

    public static readonly ErrorDefinition BaselineAlreadyPresent =
        new(ProjectUpgradeCodes.BaselineAlreadyPresent, "ProjectUpgrade_BaselineAlreadyPresent", 0);

    public static readonly ErrorDefinition TargetBaselineMissing =
        new(ProjectUpgradeCodes.TargetBaselineMissing, "ProjectUpgrade_TargetBaselineMissing", 0);

    public static readonly ErrorDefinition AdoptionFileMissing =
        new(ProjectUpgradeCodes.ScaffoldFileMissing, "ProjectUpgrade_AdoptionFileMissing", 0);

    public static readonly ErrorDefinition AdoptionFileChanged =
        new(ProjectUpgradeCodes.ScaffoldFileChanged, "ProjectUpgrade_AdoptionFileChanged", 0);

    public static readonly ErrorDefinition BaselineMissing =
        new(ProjectUpgradeCodes.BaselineMissing, "ProjectUpgrade_BaselineMissing", 0);

    public static readonly ErrorDefinition ManagedFileMissing =
        new(ProjectUpgradeCodes.ScaffoldFileMissing, "ProjectUpgrade_ManagedFileMissing", 0);

    public static readonly ErrorDefinition DestinationOccupied =
        new(ProjectUpgradeCodes.DestinationOccupied, "ProjectUpgrade_DestinationOccupied", 0);

    public static readonly ErrorDefinition MergeBaselineMissing =
        new(ProjectUpgradeCodes.ScaffoldFileChanged, "ProjectUpgrade_MergeBaselineMissing", 0);

    public static readonly ErrorDefinition ModifiedFileRemoved =
        new(ProjectUpgradeCodes.ModifiedFileRemoved, "ProjectUpgrade_ModifiedFileRemoved", 0);

    public static readonly ErrorDefinition ConflictsPreventApply =
        new(ProjectUpgradeCodes.ConflictsPreventApply, "ProjectUpgrade_ConflictsPreventApply", 1);

    public static readonly ErrorDefinition PlanVersionMismatch =
        new(ProjectUpgradeCodes.PlanVersionMismatch, "ProjectUpgrade_PlanVersionMismatch", 0);

    public static readonly ErrorDefinition ManifestChanged =
        new(ProjectUpgradeCodes.ManifestChanged, "ProjectUpgrade_ManifestChanged", 0);

    public static readonly ErrorDefinition CreatePathAppeared =
        new(ProjectUpgradeCodes.CreatePathAppeared, "ProjectUpgrade_CreatePathAppeared", 1);

    public static readonly ErrorDefinition ManagedFileChanged =
        new(ProjectUpgradeCodes.ManagedFileChanged, "ProjectUpgrade_ManagedFileChanged", 1);

    public static readonly ErrorDefinition ModuleOwnershipConflict =
        new(ProjectUpgradeCodes.ModuleOwnershipConflict, "ProjectUpgrade_ModuleOwnershipConflict", 1);

    public static readonly ErrorDefinition FileOwnershipConflict =
        new(ProjectUpgradeCodes.FileOwnershipConflict, "ProjectUpgrade_FileOwnershipConflict", 2);

    public static readonly ErrorDefinition ProjectIdentityMismatch =
        new(ProjectUpgradeCodes.ProjectIdentityMismatch, "ProjectUpgrade_ProjectIdentityMismatch", 0);

    public static readonly ErrorDefinition TargetFileStale =
        new(ProjectUpgradeCodes.TargetFileStale, "ProjectUpgrade_TargetFileStale", 1);

    public static readonly ErrorDefinition PathEscapesProject =
        new(ProjectUpgradeCodes.PathEscapesProject, "ProjectUpgrade_PathEscapesProject", 1);

    public static readonly ErrorDefinition DowngradeUnsupported =
        new(ProjectUpgradeCodes.DowngradeUnsupported, "ProjectUpgrade_DowngradeUnsupported", 2);

    public static readonly ErrorDefinition LegacyBaselineMissing =
        new(ProjectUpgradeCodes.BaselineMissing, "ProjectUpgrade_LegacyBaselineMissing", 0);

    public static readonly ErrorDefinition UnsupportedXml =
        new(ProjectUpgradeCodes.UnsupportedMergeContent, "ProjectUpgrade_UnsupportedXml", 0);

    public static readonly ErrorDefinition UnsupportedText =
        new(ProjectUpgradeCodes.UnsupportedMergeContent, "ProjectUpgrade_UnsupportedText", 0);

    public static readonly ErrorDefinition SolutionMergeConflict =
        new(ProjectUpgradeCodes.MergeConflict, "ProjectUpgrade_SolutionMergeConflict", 0);

    public static readonly ErrorDefinition XmlMergeConflict =
        new(ProjectUpgradeCodes.MergeConflict, "ProjectUpgrade_XmlMergeConflict", 0);

    public static readonly ErrorDefinition TextMergeConflict =
        new(ProjectUpgradeCodes.MergeConflict, "ProjectUpgrade_TextMergeConflict", 0);

    public static readonly ErrorDefinition UpgradeRolledBack =
        new(ProjectUpgradeCodes.UpgradeFailed, "ProjectUpgrade_UpgradeRolledBack", 0);

    public static readonly ErrorDefinition UpgradeRollbackFailed =
        new(ProjectUpgradeCodes.UpgradeFailed, "ProjectUpgrade_UpgradeRollbackFailed", 1);

}
