using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamlNexus.Common.Projects;

/// <summary>文件级操作类型；在文件中插入或删除几行，最终仍属于替换整个文件内容</summary>
public enum XamlNexusUpgradeChangeKind {
    Create,
    Replace,
    Delete,
}

/// <summary>记录变更内容的生成方式：直接更新、按行合并、XML 结构合并或解决方案结构合并</summary>
public enum XamlNexusUpgradeChangeStrategy {
    Direct,
    TextMerge,
    XmlMerge,
    SolutionMerge,
}

/// <summary>升级计划中的一项文件变更，只描述待执行操作，本身不会写入文件</summary>
/// <param name="Kind">创建、替换或删除文件</param>
/// <param name="RelativePath">相对于项目根目录的目标文件路径</param>
/// <param name="ExpectedSha256">预期的修改前文件哈希，用于防止规划后用户又修改文件；创建操作可为空</param>
/// <param name="Content">准备写入的完整文件字节，不是行级补丁；删除操作不需要内容</param>
/// <param name="Strategy">此次变更采用的内容生成策略</param>
public sealed record XamlNexusUpgradeChange(
    XamlNexusUpgradeChangeKind Kind,
    string RelativePath,
    string? ExpectedSha256,
    byte[]? Content,
    XamlNexusUpgradeChangeStrategy Strategy = XamlNexusUpgradeChangeStrategy.Direct);

/// <summary>
/// 无法安全自动应用的升级问题可附带人工合并文档、当前文件哈希和三方输入指纹，
/// 供导出冲突及后续校验人工处理结果使用；冲突不代表已修改项目文件
/// </summary>
public sealed record XamlNexusUpgradeConflict(
    string Code,
    string RelativePath,
    string Message,
    [property: JsonIgnore] string? MergeDocument = null,
    string? ExpectedSha256 = null,
    string? InputFingerprint = null);

public sealed record XamlNexusProjectUpgradePlan(
    string FromVersion,
    string ToVersion,
    IReadOnlyList<XamlNexusUpgradeChange> Changes,
    IReadOnlyList<XamlNexusUpgradeConflict> Conflicts) {
    // 即使部分文件可自动合并，只要还有冲突，整个计划就不能直接应用
    public bool CanApply => Conflicts.Count == 0;
}

public sealed record XamlNexusProjectUpgradeResult(
    string FromVersion,
    string ToVersion,
    IReadOnlyList<string> ChangedFiles);

public sealed class XamlNexusProjectUpgradeException : Exception {
    public XamlNexusProjectUpgradeException(string code, string message)
        : base($"{code}: {message}") => Code = code;

    public XamlNexusProjectUpgradeException(string code, string message, Exception innerException)
        : base($"{code}: {message}", innerException) => Code = code;

    public string Code { get; }
}

public static class XamlNexusProjectUpgrade {
    public static IReadOnlyList<string> WriteConflictArtifacts(XamlNexusProjectUpgradePlan plan, string outputDirectory) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        var artifacts = plan.Conflicts
            .Where(conflict => conflict.MergeDocument is not null)
            .Select(conflict => new {
                Conflict = conflict,
                RelativePath = NormalizePath(conflict.RelativePath) + ".merge",
            })
            .ToArray();
        var createdFiles = new List<string>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try {
            if (artifacts.Length > 0) {
                ProjectPathSafety.EnsureNoLinks(root);
                EnsureParentDirectory(Path.Combine(root, ".xamlnexus-upgrade"),
                    Path.GetPathRoot(root)!, createdDirectories);
            }
            foreach (var artifact in artifacts) {
                string path = ResolvePath(root, artifact.RelativePath);
                EnsureParentDirectory(path, root, createdDirectories);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                createdFiles.Add(path);
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                writer.Write(artifact.Conflict.MergeDocument);
            }
            if (artifacts.Length > 0) {
                string stamp = ResolvePath(root, ".xamlnexus-upgrade");
                using var stream = new FileStream(stamp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                createdFiles.Add(stamp);
                using var writer = new StreamWriter(stream);
                writer.Write(ResolutionFingerprint(plan));
            }
            return artifacts.Select(artifact => artifact.RelativePath).ToArray();
        }
        catch {
            foreach (string path in createdFiles.AsEnumerable().Reverse()) {
                try {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            foreach (string directory in createdDirectories.OrderByDescending(path => path.Length)) {
                try {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    private static string ResolutionFingerprint(XamlNexusProjectUpgradePlan plan) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Plan = plan,
            Documents = plan.Conflicts.Select(conflict => conflict.MergeDocument).ToArray(),
        })));

    public static XamlNexusProjectUpgradePlan ResolveConflicts(XamlNexusProjectUpgradePlan plan, string resolutionDirectory) {
        ArgumentNullException.ThrowIfNull(plan);
        string root = Path.GetFullPath(resolutionDirectory);
        string stamp = ResolvePath(root, ".xamlnexus-upgrade");
        if (!File.Exists(stamp) || File.ReadAllText(stamp) != ResolutionFingerprint(plan))
            throw new XamlNexusProjectUpgradeException("XU2020",
                "The conflict export is missing or stale. Export conflicts again from the current project and target version.");

        var changes = plan.Changes.ToList();
        foreach (var conflict in plan.Conflicts) {
            if (conflict.Code != "XU2011" || conflict.ExpectedSha256 is null)
                throw new XamlNexusProjectUpgradeException("XU2021",
                    $"This conflict requires manual project changes before retrying upgrade: {conflict.RelativePath} ({conflict.Code}).");

            string path = ResolvePath(root, NormalizePath(conflict.RelativePath) + ".merge");
            if (!File.Exists(path))
                throw new XamlNexusProjectUpgradeException("XU2021", $"Missing resolved file: {conflict.RelativePath}.merge");

            byte[] content = File.ReadAllBytes(path);
            string text = new System.Text.UTF8Encoding(false, true).GetString(content);
            if (text.Contains('\0') || text.Split('\n').Any(line => {
                string value = line.TrimStart('\uFEFF', ' ', '\t', '\r');
                return value.StartsWith("<<<<<<<", StringComparison.Ordinal) ||
                    value.StartsWith("|||||||", StringComparison.Ordinal) ||
                    value.StartsWith("=======", StringComparison.Ordinal) ||
                    value.StartsWith(">>>>>>>", StringComparison.Ordinal);
            }))
                throw new XamlNexusProjectUpgradeException("XU2021", $"Unresolved conflict markers or invalid text: {conflict.RelativePath}");

            if (IsSemanticXmlPath(conflict.RelativePath)) {
                try { System.Xml.Linq.XDocument.Parse(text.TrimStart('\uFEFF')); }
                catch (System.Xml.XmlException exception) {
                    throw new XamlNexusProjectUpgradeException("XU2021", $"Resolved XML is invalid: {conflict.RelativePath}", exception);
                }
            }

            changes.Add(new XamlNexusUpgradeChange(XamlNexusUpgradeChangeKind.Replace,
                conflict.RelativePath, conflict.ExpectedSha256, content));
        }

        return plan with { Changes = changes, Conflicts = [] };
    }

    public static XamlNexusProjectUpgradePlan CreateBaselineAdoptionPlan(XamlNexusProjectContext current, XamlNexusProjectContext target) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ValidateIdentity(current.Manifest, target.Manifest);
        if (!current.Manifest.GeneratorVersion.Equals(
                target.Manifest.GeneratorVersion,
                StringComparison.OrdinalIgnoreCase)) {
            throw new XamlNexusProjectUpgradeException(
                "XU1006",
                "A missing scaffold baseline can only be adopted from the same generator version.");
        }
        if (current.Manifest.ScaffoldFiles is not null) {
            throw new XamlNexusProjectUpgradeException(
                "XU1007",
                "This project already has a scaffold file baseline.");
        }

        IReadOnlyList<XamlNexusManagedFile> desired = target.Manifest.ScaffoldFiles
            ?? throw new XamlNexusProjectUpgradeException(
                "XU1002",
                "The target template has no scaffold file baseline.");

        var conflicts = FindOwnershipConflicts(current.Manifest, target.Manifest);
        if (conflicts.Count > 0)
            return new(current.Manifest.GeneratorVersion, target.Manifest.GeneratorVersion, [], conflicts);

        foreach (XamlNexusManagedFile file in desired) {
            string targetPath = ResolvePath(target.RootDirectory, file.Path);
            ValidateTargetFile(file, targetPath);
            string currentPath = ResolvePath(current.RootDirectory, file.Path);
            if (!File.Exists(currentPath)) {
                conflicts.Add(new XamlNexusUpgradeConflict(
                    "XU2001",
                    file.Path,
                    "A scaffold file required for baseline adoption is missing."));
            }
            else if (!ComputeSha256(currentPath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) {
                conflicts.Add(new XamlNexusUpgradeConflict(
                    "XU2002",
                    file.Path,
                    "A scaffold file differs from the same-version template."));
            }
        }

        return new XamlNexusProjectUpgradePlan(
            current.Manifest.GeneratorVersion,
            target.Manifest.GeneratorVersion,
            [],
            conflicts);
    }

    public static XamlNexusProjectUpgradePlan CreatePlan(XamlNexusProjectContext current, XamlNexusProjectContext target) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ValidateIdentity(current.Manifest, target.Manifest);

        IReadOnlyList<XamlNexusManagedFile> baseline = current.Manifest.ScaffoldFiles
            ?? throw new XamlNexusProjectUpgradeException(
                "XU1001",
                "This project has no scaffold file baseline and cannot be upgraded safely.");
        IReadOnlyList<XamlNexusManagedFile> desired = target.Manifest.ScaffoldFiles
            ?? throw new XamlNexusProjectUpgradeException(
                "XU1002",
                "The target template has no scaffold file baseline.");

        var baselineByPath = baseline.ToDictionary(
            file => NormalizePath(file.Path),
            StringComparer.OrdinalIgnoreCase);
        var desiredByPath = desired.ToDictionary(
            file => NormalizePath(file.Path),
            StringComparer.OrdinalIgnoreCase);
        var changes = new List<XamlNexusUpgradeChange>();
        var conflicts = FindOwnershipConflicts(current.Manifest, target.Manifest);
        if (conflicts.Count > 0)
            return new(current.Manifest.GeneratorVersion, target.Manifest.GeneratorVersion, [], conflicts);

        foreach ((string path, XamlNexusManagedFile installed) in baselineByPath) {
            string currentPath = ResolvePath(current.RootDirectory, installed.Path);
            desiredByPath.TryGetValue(path, out XamlNexusManagedFile? targetFile);
            string? targetPath = targetFile is null
                ? null
                : ResolvePath(target.RootDirectory, targetFile.Path);
            if (targetFile is not null) ValidateTargetFile(targetFile, targetPath!);
            if (!File.Exists(currentPath)) {
                conflicts.Add(new XamlNexusUpgradeConflict(
                    "XU2001",
                    installed.Path,
                    "A scaffold-managed file is missing.",
                    CreateMergeDocument(
                        installed,
                        installed.Path,
                        current.Manifest.GeneratorVersion,
                        target.Manifest.GeneratorVersion,
                        [],
                        targetPath is null ? [] : File.ReadAllBytes(targetPath))));
                continue;
            }
            string actualHash = ComputeSha256(currentPath);
            if (!actualHash.Equals(installed.Sha256, StringComparison.OrdinalIgnoreCase)) {
                PlanModifiedFile(
                    current.Manifest.GeneratorVersion,
                    target.Manifest.GeneratorVersion,
                    installed,
                    targetFile,
                    currentPath,
                    targetPath,
                    actualHash,
                    changes,
                    conflicts);
                continue;
            }

            if (targetFile is null) {
                changes.Add(new XamlNexusUpgradeChange(
                    XamlNexusUpgradeChangeKind.Delete,
                    installed.Path,
                    installed.Sha256,
                    null));
                continue;
            }

            if (!installed.Sha256.Equals(targetFile.Sha256, StringComparison.OrdinalIgnoreCase)) {
                changes.Add(new XamlNexusUpgradeChange(
                    XamlNexusUpgradeChangeKind.Replace,
                    targetFile.Path,
                    installed.Sha256,
                    File.ReadAllBytes(targetPath!)));
            }
        }

        foreach ((string path, XamlNexusManagedFile targetFile) in desiredByPath) {
            if (baselineByPath.ContainsKey(path)) continue;
            string targetPath = ResolvePath(target.RootDirectory, targetFile.Path);
            ValidateTargetFile(targetFile, targetPath);
            string destination = ResolvePath(current.RootDirectory, targetFile.Path);
            if (File.Exists(destination)) {
                if (!ComputeSha256(destination).Equals(targetFile.Sha256, StringComparison.OrdinalIgnoreCase)) {
                    conflicts.Add(new XamlNexusUpgradeConflict(
                        "XU2003",
                        targetFile.Path,
                        "A new scaffold path is already occupied by different content."));
                }
                continue;
            }
            changes.Add(new XamlNexusUpgradeChange(
                XamlNexusUpgradeChangeKind.Create,
                targetFile.Path,
                null,
                File.ReadAllBytes(targetPath)));
        }

        return new XamlNexusProjectUpgradePlan(
            current.Manifest.GeneratorVersion,
            target.Manifest.GeneratorVersion,
            changes,
            conflicts);
    }

    private static void PlanModifiedFile(
        string fromVersion,
        string toVersion,
        XamlNexusManagedFile installed,
        XamlNexusManagedFile? targetFile,
        string currentPath,
        string? targetPath,
        string actualHash,
        List<XamlNexusUpgradeChange> changes,
        List<XamlNexusUpgradeConflict> conflicts) {
        byte[] localContent = File.ReadAllBytes(currentPath);
        byte[] targetContent = targetPath is null ? [] : File.ReadAllBytes(targetPath);
        if (installed.BaselineContentGzipBase64 is null) {
            conflicts.Add(new XamlNexusUpgradeConflict(
                "XU2002",
                installed.Path,
                "A scaffold-managed file was modified, but its older manifest has no merge baseline."));
            return;
        }

        byte[] baselineContent = XamlNexusBaselineContent.Decode(installed.BaselineContentGzipBase64);
        if (targetFile is null) {
            conflicts.Add(new XamlNexusUpgradeConflict(
                "XU2010",
                installed.Path,
                "The user modified a scaffold file that the target version removes.",
                XamlNexusThreeWayTextMerge.CreateConflictDocument(
                    installed.Path,
                    fromVersion,
                    toVersion,
                    baselineContent,
                    localContent,
                    targetContent)));
            return;
        }

        // No reconciliation is needed when the template stayed unchanged or
        // both sides already agree. Preserve the local bytes, including comments
        // and formatting that the semantic merger intentionally cannot rewrite.
        if (targetContent.AsSpan().SequenceEqual(baselineContent) ||
            localContent.AsSpan().SequenceEqual(targetContent))
            return;

        XamlNexusTextMergeResult merge = XamlNexusThreeWayTextMerge.Merge(
            baselineContent,
            localContent,
            targetContent);
        byte[]? mergedContent = merge.Content;
        XamlNexusMergeStatus mergeStatus = merge.Status;
        XamlNexusUpgradeChangeStrategy mergeStrategy = XamlNexusUpgradeChangeStrategy.TextMerge;
        bool semanticConflict = false;
        if (mergeStatus != XamlNexusMergeStatus.Merged &&
            Path.GetExtension(installed.Path).Equals(".sln", StringComparison.OrdinalIgnoreCase)) {
            XamlNexusSolutionMergeResult solutionMerge = XamlNexusThreeWaySolutionMerge.Merge(
                baselineContent,
                localContent,
                targetContent);
            if (solutionMerge.Status == XamlNexusMergeStatus.Merged) {
                mergeStatus = solutionMerge.Status;
                mergedContent = solutionMerge.Content;
                mergeStrategy = XamlNexusUpgradeChangeStrategy.SolutionMerge;
            }
            else if (solutionMerge.Status == XamlNexusMergeStatus.Conflict) {
                semanticConflict = true;
            }
        }
        else if (IsSemanticXmlPath(installed.Path)) {
            // Disjoint line edits can still conflict structurally (for example,
            // inserting the same named control at different positions).
            // XML validation must not be bypassed by a successful text merge.
            XamlNexusXmlMergeResult xmlMerge = XamlNexusThreeWayXmlMerge.Merge(
                baselineContent,
                localContent,
                targetContent);
            mergeStatus = xmlMerge.Status;
            mergedContent = xmlMerge.Content;
            if (xmlMerge.Status == XamlNexusMergeStatus.Merged) {
                mergeStrategy = XamlNexusUpgradeChangeStrategy.XmlMerge;
            }
            else if (xmlMerge.Status == XamlNexusMergeStatus.Conflict) {
                semanticConflict = true;
            }
        }
        if (mergeStatus == XamlNexusMergeStatus.Merged) {
            if (!mergedContent!.AsSpan().SequenceEqual(localContent)) {
                changes.Add(new XamlNexusUpgradeChange(
                    XamlNexusUpgradeChangeKind.Replace,
                    targetFile.Path,
                    actualHash,
                    mergedContent,
                    mergeStrategy));
            }
            return;
        }

        string reason = mergeStatus == XamlNexusMergeStatus.Unsupported
            ? IsSemanticXmlPath(installed.Path)
                ? "XML structure could not be checked safely (unsupported content, encoding, or size). Review the conflict document and merge manually."
                : "The modified scaffold file is binary, too large, or not supported UTF-8 text."
            : semanticConflict
                ? Path.GetExtension(installed.Path).Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ? "The user and target version changed the same solution project, section, or configuration."
                    : "XML changes have conflicting values or child order, or nodes cannot be matched reliably. Review the conflict document and merge manually."
                : "The user and target version changed overlapping lines.";
        conflicts.Add(new XamlNexusUpgradeConflict(
            mergeStatus == XamlNexusMergeStatus.Unsupported ? "XU2012" : "XU2011",
            installed.Path,
            reason,
            XamlNexusThreeWayTextMerge.CreateConflictDocument(
                installed.Path,
                fromVersion,
                toVersion,
                baselineContent,
                localContent,
                targetContent,
                wholeFile: semanticConflict || IsSemanticXmlPath(installed.Path)),
            actualHash,
            Convert.ToHexString(SHA256.HashData(baselineContent)) + ":" +
                Convert.ToHexString(SHA256.HashData(localContent)) + ":" +
                Convert.ToHexString(SHA256.HashData(targetContent))));
    }

    private static bool IsSemanticXmlPath(string path) {
        string extension = Path.GetExtension(path);
        return extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".config", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".resw", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".nuspec", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".appxmanifest", StringComparison.OrdinalIgnoreCase);
    }

    private static string? CreateMergeDocument(
        XamlNexusManagedFile installed,
        string relativePath,
        string fromVersion,
        string toVersion,
        byte[] localContent,
        byte[] targetContent) => installed.BaselineContentGzipBase64 is null
            ? null
            : XamlNexusThreeWayTextMerge.CreateConflictDocument(
                relativePath,
                fromVersion,
                toVersion,
                XamlNexusBaselineContent.Decode(installed.BaselineContentGzipBase64),
                localContent,
                targetContent);

    public static XamlNexusProjectUpgradeResult Apply(
        XamlNexusProjectContext current,
        XamlNexusProjectContext target,
        XamlNexusProjectUpgradePlan plan) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanApply) {
            throw new XamlNexusProjectUpgradeException(
                "XU2004",
                $"Upgrade has {plan.Conflicts.Count} conflict(s); no files were changed.");
        }
        if (!current.Manifest.GeneratorVersion.Equals(plan.FromVersion, StringComparison.OrdinalIgnoreCase) ||
            !target.Manifest.GeneratorVersion.Equals(plan.ToVersion, StringComparison.OrdinalIgnoreCase)) {
            throw new XamlNexusProjectUpgradeException(
                "XU2007",
                "The upgrade plan versions do not match the supplied projects.");
        }

        using var lease = ProjectWriteLease.Acquire(current.RootDirectory);
        ProjectPathSafety.EnsureNoLinks(current.ManifestPath);
        XamlNexusProjectManifest freshManifest = XamlNexusProjectManifestStore.Load(current.ManifestPath);
        if (!ManifestFingerprint(freshManifest).Equals(
                ManifestFingerprint(current.Manifest),
                StringComparison.Ordinal)) {
            throw new XamlNexusProjectUpgradeException(
                "XU2008",
                "The project manifest changed after the upgrade context was loaded.");
        }

        var ownershipConflicts = FindOwnershipConflicts(freshManifest, target.Manifest);
        if (ownershipConflicts.Count > 0)
            throw new XamlNexusProjectUpgradeException(ownershipConflicts[0].Code, ownershipConflicts[0].Message);

        var resolved = plan.Changes.Select(change => new {
            Change = change,
            FullPath = ResolvePath(current.RootDirectory, change.RelativePath),
        }).ToArray();
        foreach (var item in resolved) {
            bool exists = File.Exists(item.FullPath);
            if (item.Change.Kind == XamlNexusUpgradeChangeKind.Create && exists) {
                throw new XamlNexusProjectUpgradeException(
                    "XU2005",
                    $"Upgrade plan is stale because a create path now exists: {item.Change.RelativePath}");
            }
            if (item.Change.Kind is XamlNexusUpgradeChangeKind.Replace or XamlNexusUpgradeChangeKind.Delete) {
                if (!exists ||
                    !ComputeSha256(item.FullPath).Equals(
                        item.Change.ExpectedSha256,
                        StringComparison.OrdinalIgnoreCase)) {
                    throw new XamlNexusProjectUpgradeException(
                        "XU2006",
                        $"Upgrade plan is stale because a managed file changed: {item.Change.RelativePath}");
                }
            }
        }
        var snapshots = resolved.ToDictionary(
            item => item.FullPath,
            item => File.Exists(item.FullPath) ? File.ReadAllBytes(item.FullPath) : null,
            StringComparer.OrdinalIgnoreCase);
        byte[] originalManifest = File.ReadAllBytes(current.ManifestPath);
        var applied = new List<string>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try {
            foreach (var item in resolved) {
                EnsureParentDirectory(
                    item.FullPath,
                    current.RootDirectory,
                    createdDirectories);
                switch (item.Change.Kind) {
                    case XamlNexusUpgradeChangeKind.Create:
                    case XamlNexusUpgradeChangeKind.Replace:
                        AtomicWrite(item.FullPath, item.Change.Content!);
                        break;
                    case XamlNexusUpgradeChangeKind.Delete:
                        File.Delete(item.FullPath);
                        break;
                }
                applied.Add(item.FullPath);
            }

            XamlNexusProjectManifestStore.Save(
                current.ManifestPath,
                CreateUpdatedManifest(current.Manifest, target.Manifest));
            RemoveEmptyDirectories(current.RootDirectory, resolved
                .Where(item => item.Change.Kind == XamlNexusUpgradeChangeKind.Delete)
                .Select(item => item.FullPath));
            return new XamlNexusProjectUpgradeResult(
                plan.FromVersion,
                plan.ToVersion,
                plan.Changes.Select(change => change.RelativePath).ToArray());
        }
        catch (Exception exception) {
            var rollbackErrors = new List<Exception>();
            foreach (string path in applied.AsEnumerable().Reverse()) {
                try {
                    byte[]? original = snapshots[path];
                    if (original is null) {
                        if (File.Exists(path)) File.Delete(path);
                    }
                    else {
                        AtomicWrite(path, original);
                    }
                }
                catch (Exception rollbackException) {
                    rollbackErrors.Add(rollbackException);
                }
            }
            try {
                AtomicWrite(current.ManifestPath, originalManifest);
            }
            catch (Exception rollbackException) {
                rollbackErrors.Add(rollbackException);
            }
            foreach (string directory in createdDirectories.OrderByDescending(path => path.Length)) {
                try {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory);
                }
                catch (Exception rollbackException) {
                    rollbackErrors.Add(rollbackException);
                }
            }
            string rollback = rollbackErrors.Count == 0
                ? "All scaffold changes were rolled back."
                : $"Rollback encountered {rollbackErrors.Count} additional error(s).";
            throw new XamlNexusProjectUpgradeException(
                "XU3001",
                $"Project upgrade failed. {rollback}",
                exception);
        }
    }

    private static XamlNexusProjectManifest CreateUpdatedManifest(
        XamlNexusProjectManifest current,
        XamlNexusProjectManifest target) => new() {
            SchemaVersion = current.SchemaVersion,
            GeneratorVersion = target.GeneratorVersion,
            Project = current.Project,
            Modules = target.Modules.Where(module => module.Source == "template")
                .Concat(current.Modules.Where(module => module.Source == "recipe")).ToArray(),
            ScaffoldFiles = target.ScaffoldFiles,
        };

    private static List<XamlNexusUpgradeConflict> FindOwnershipConflicts(
        XamlNexusProjectManifest current, XamlNexusProjectManifest target) {
        var conflicts = new List<XamlNexusUpgradeConflict>();
        // Existing overlaps also block deletion of a file still owned by a Recipe.
        var scaffoldPaths = (current.ScaffoldFiles ?? []).Concat(target.ScaffoldFiles ?? [])
            .Select(file => NormalizePath(file.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var templateIds = target.Modules.Where(module => module.Source == "template")
            .Select(module => module.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in current.Modules.Where(module => module.Source == "recipe")) {
            if (templateIds.Contains(recipe.Id))
                conflicts.Add(new("XU2014", "xamlnexus.json",
                    $"Target template module '{recipe.Id}' conflicts with an installed Recipe. Automatic ownership transfer is not supported."));
            foreach (var file in recipe.Files ?? []) {
                if (scaffoldPaths.Contains(NormalizePath(file.Path)))
                    conflicts.Add(new("XU2013", file.Path,
                        $"Scaffold path '{file.Path}' is owned by Recipe '{recipe.Id}'. Automatic ownership transfer is not supported, even when the contents match."));
            }
        }
        return conflicts;
    }

    private static void ValidateIdentity(
        XamlNexusProjectManifest current,
        XamlNexusProjectManifest target) {
        if (!current.Project.Name.Equals(target.Project.Name, StringComparison.Ordinal) ||
            !string.Equals(current.Project.Profile ?? "standard", target.Project.Profile ?? "standard", StringComparison.Ordinal) ||
            !current.Project.Preset.Equals(target.Project.Preset, StringComparison.OrdinalIgnoreCase) ||
            !current.Project.Language.Equals(target.Project.Language, StringComparison.OrdinalIgnoreCase) ||
            !current.Project.SolutionFormat.Equals(target.Project.SolutionFormat, StringComparison.OrdinalIgnoreCase)) {
            throw new XamlNexusProjectUpgradeException(
                "XU1003",
                "The target scaffold identity does not match the current project.");
        }
    }

    private static void ValidateTargetFile(XamlNexusManagedFile file, string fullPath) {
        if (!File.Exists(fullPath) ||
            !ComputeSha256(fullPath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) {
            throw new XamlNexusProjectUpgradeException(
                "XU1004",
                $"Target scaffold file is missing or stale: {file.Path}");
        }
    }

    private static string ResolvePath(string rootDirectory, string relativePath) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            throw new XamlNexusProjectUpgradeException(
                "XU1005",
                $"Scaffold path escapes the project: {relativePath}");
        }
        ProjectPathSafety.EnsureNoLinks(fullPath);
        return fullPath;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string ManifestFingerprint(XamlNexusProjectManifest manifest) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest)));

    private static void EnsureParentDirectory(
        string filePath,
        string rootDirectory,
        HashSet<string> createdDirectories) {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory) || Directory.Exists(directory)) return;
        var missing = new Stack<string>();
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        while (directory is not null &&
               !directory.Equals(root, StringComparison.OrdinalIgnoreCase) &&
               !Directory.Exists(directory)) {
            missing.Push(directory);
            directory = Path.GetDirectoryName(directory);
        }
        while (missing.Count > 0) {
            string path = missing.Pop();
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }
    }

    private static void AtomicWrite(string path, byte[] content) {
        string temporary = path + $".{Guid.NewGuid():N}.tmp";
        try {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void RemoveEmptyDirectories(string rootDirectory, IEnumerable<string> deletedFiles) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        foreach (string file in deletedFiles) {
            try {
                string? directory = Path.GetDirectoryName(file);
                while (directory is not null &&
                       !directory.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                       Directory.Exists(directory) &&
                       !Directory.EnumerateFileSystemEntries(directory).Any()) {
                    Directory.Delete(directory);
                    directory = Path.GetDirectoryName(directory);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                // Empty-directory cleanup is cosmetic after the transaction commits.
            }
        }
    }
}
