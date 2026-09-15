using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

/// <summary>把声明式项目结构操作转换为共享文件替换计划，在内存中编辑，不直接写磁盘</summary>
internal static partial class XamlNexusRecipeProjectEditor {
    /// <summary>按目标文件归并操作，拒绝与普通文件操作重叠，生成带原始哈希的共享文件变更</summary>
    public static IReadOnlyList<ResolvedRecipeFileChange> ResolveAndValidate(
        string rootDirectory,
        IReadOnlyList<XamlNexusRecipeProjectOperation> operations,
        IReadOnlyList<ResolvedRecipeFileChange> fileChanges) {
        ArgumentNullException.ThrowIfNull(operations);
        var docs = fileChanges.Where(change =>
            Path.GetDirectoryName(change.FullPath) == Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar)
            && change.FullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (operations.Count == 0 && docs.Length == 0) return [];

        var stagedCreates = fileChanges
            .Where(change => change.Change.Kind == XamlNexusRecipeFileChangeKind.Create)
            .ToDictionary(change => change.FullPath, change => change.Change.Content!, StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, List<XamlNexusRecipeProjectOperation>>(StringComparer.OrdinalIgnoreCase);

        foreach (XamlNexusRecipeProjectOperation operation in operations) {
            if (operation is null)
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.EmptyProjectOperation, []);
            string relativeTarget = operation switch {
                AddPackageReferenceOperation value => value.ProjectPath,
                EnsurePackageReferenceOperation value => value.ProjectPath,
                AddProjectReferenceOperation value => value.ProjectPath,
                RemoveProjectReferenceOperation value => value.ProjectPath,
                AddProjectToSolutionOperation value => value.SolutionPath,
                RemoveProjectFromSolutionOperation value => value.SolutionPath,
                AddProtobufOperation value => value.ProjectPath,
                RemoveProtobufOperation value => value.ProjectPath,
                _ => throw new XamlNexusRecipeException(
                    XamlNexusRecipeErrors.UnsupportedProjectOperation, [operation.GetType().Name]),
            };
            string target = ResolveSafePath(rootDirectory, relativeTarget, "project operation target");
            if (fileChanges.Any(change => change.FullPath.Equals(target, StringComparison.OrdinalIgnoreCase))) {
                throw new XamlNexusRecipeException(
                    XamlNexusRecipeErrors.OverlappingFileAndProjectOperations, [relativeTarget]);
            }
            if (!File.Exists(target))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProjectOperationTargetMissing, [relativeTarget]);

            if (!targets.TryGetValue(target, out List<XamlNexusRecipeProjectOperation>? list)) {
                list = [];
                targets.Add(target, list);
            }
            list.Add(operation);
        }

        if (docs.Length > 0) {
            foreach (string solution in Directory.EnumerateFiles(rootDirectory).Where(path =>
                path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))) {
                string target = ResolveSafePath(rootDirectory, Path.GetFileName(solution), "solution documents");
                if (fileChanges.Any(change => change.FullPath.Equals(target, StringComparison.OrdinalIgnoreCase)))
                    continue; // 已解析的完整替换在事务提交前会再次校验，不重复生成结构操作。
                targets.TryAdd(target, []);
            }
        }
        var result = new List<ResolvedRecipeFileChange>();
        foreach ((string target, List<XamlNexusRecipeProjectOperation> targetOperations) in targets) {
            byte[] original = File.ReadAllBytes(target);
            byte[] updated = targetOperations.Count == 0 ? original : target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                ? EditProject(rootDirectory, target, original, targetOperations, stagedCreates)
                : target.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                    ? EditXmlSolution(rootDirectory, target, original, targetOperations, stagedCreates)
                : target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                    ? EditSolution(rootDirectory, target, original, targetOperations, stagedCreates)
                    : throw new XamlNexusRecipeException(
                        XamlNexusRecipeErrors.UnsupportedProjectFileType, [Path.GetRelativePath(rootDirectory, target)]);

            if (docs.Length > 0 && (target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || target.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
                updated = Encoding.UTF8.GetBytes(SolutionDocuments.Update(DecodeUtf8(updated), target.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase),
                    docs.Where(d => d.Change.Kind != XamlNexusRecipeFileChangeKind.Delete).Select(d => Path.GetFileName(d.FullPath)),
                    docs.Where(d => d.Change.Kind == XamlNexusRecipeFileChangeKind.Delete).Select(d => Path.GetFileName(d.FullPath))));
            if (updated.AsSpan().SequenceEqual(original)) continue;

            var change = new XamlNexusRecipeFileChange {
                Kind = XamlNexusRecipeFileChangeKind.Replace,
                RelativePath = Path.GetRelativePath(rootDirectory, target).Replace('\\', '/'),
                Content = updated,
                ExpectedSha256 = XamlNexusRecipeHash.Compute(original),
            };
            result.Add(new ResolvedRecipeFileChange(change, target, IsSharedProjectFile: true));
        }
        return result;
    }

    /// <summary>解析 csproj XML 并按操作增删包、项目或 Protobuf 引用；不调用 MSBuild 和 NuGet 还原</summary>
    private static byte[] EditProject(
        string rootDirectory,
        string projectPath,
        byte[] original,
        IReadOnlyList<XamlNexusRecipeProjectOperation> operations,
        IReadOnlyDictionary<string, byte[]> stagedCreates) {
        XDocument document;
        try {
            document = XDocument.Parse(DecodeUtf8(original), LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.InvalidProjectXml, [Path.GetRelativePath(rootDirectory, projectPath)],
                exception);
        }
        XElement root = document.Root
            ?? throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingProjectRoot, []);

        foreach (XamlNexusRecipeProjectOperation operation in operations) {
            switch (operation) {
                case AddPackageReferenceOperation package:
                    ValidatePackage(package);
                    if (root.Descendants().Any(element =>
                            element.Name.LocalName == "PackageReference" &&
                            string.Equals((string?)element.Attribute("Include"), package.PackageId, StringComparison.OrdinalIgnoreCase))) {
                        throw new XamlNexusRecipeException(XamlNexusRecipeErrors.PackageReferenceAlreadyExists, [package.PackageId]);
                    }
                    AddItem(root, new XElement(root.Name.Namespace + "PackageReference",
                        new XAttribute("Include", package.PackageId),
                        new XAttribute("Version", package.Version)));
                    break;

                case EnsurePackageReferenceOperation package:
                    EnsurePackageReference(root, package);
                    break;

                case AddProjectReferenceOperation reference:
                    string referencedPath = ResolveSafePath(rootDirectory, reference.ReferencedProjectPath, "referenced project");
                    if (!File.Exists(referencedPath) && !stagedCreates.ContainsKey(referencedPath))
                        throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ReferencedProjectMissing, [reference.ReferencedProjectPath]);
                    string relativeReference = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, referencedPath);
                    if (root.Descendants().Any(element =>
                            element.Name.LocalName == "ProjectReference" &&
                            PathsEqual(projectPath, (string?)element.Attribute("Include"), referencedPath))) {
                        throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProjectReferenceAlreadyExists, [reference.ReferencedProjectPath]);
                    }
                    AddItem(root, new XElement(root.Name.Namespace + "ProjectReference",
                        new XAttribute("Include", relativeReference)));
                    break;

                case RemoveProjectReferenceOperation reference:
                    string removedReferencePath = ResolveSafePath(
                        rootDirectory,
                        reference.ReferencedProjectPath,
                        "referenced project");
                    RemoveItem(
                        root,
                        "ProjectReference",
                        element => PathsEqual(
                            projectPath,
                            (string?)element.Attribute("Include"),
                            removedReferencePath),
                        XamlNexusRecipeErrors.ProjectReferenceMissing, [reference.ReferencedProjectPath]);
                    break;

                case AddProtobufOperation protobuf:
                    string protoPath = ResolveSafePath(rootDirectory, protobuf.ProtoPath, "Protobuf source");
                    if (!File.Exists(protoPath) && !stagedCreates.ContainsKey(protoPath))
                        throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProtobufSourceMissing, [protobuf.ProtoPath]);
                    if (!protoPath.EndsWith(".proto", StringComparison.OrdinalIgnoreCase))
                        throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidProtobufExtension, [protobuf.ProtoPath]);
                    string relativeProto = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, protoPath);
                    if (root.Descendants().Any(element =>
                            element.Name.LocalName == "Protobuf" &&
                            PathsEqual(projectPath, (string?)element.Attribute("Include"), protoPath))) {
                        throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProtobufAlreadyIncluded, [protobuf.ProtoPath]);
                    }
                    AddItem(root, new XElement(root.Name.Namespace + "Protobuf",
                        new XAttribute("Include", relativeProto)));
                    break;

                case RemoveProtobufOperation protobuf:
                    string removedProtoPath = ResolveSafePath(
                        rootDirectory,
                        protobuf.ProtoPath,
                        "Protobuf source");
                    RemoveItem(
                        root,
                        "Protobuf",
                        element => PathsEqual(
                            projectPath,
                            (string?)element.Attribute("Include"),
                            removedProtoPath),
                        XamlNexusRecipeErrors.ProtobufReferenceMissing, [protobuf.ProtoPath]);
                    break;

                default:
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidCsprojOperation, []);
            }
        }

        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>编辑 SLNX 的 Project 节点，新增时允许引用同一计划准备创建的项目</summary>
    private static byte[] EditXmlSolution(string rootDirectory, string solutionPath, byte[] original,
        IReadOnlyList<XamlNexusRecipeProjectOperation> operations,
        IReadOnlyDictionary<string, byte[]> stagedCreates) {
        XDocument document;
        try { document = XDocument.Parse(DecodeUtf8(original), LoadOptions.PreserveWhitespace); }
        catch (System.Xml.XmlException exception) {
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidSlnxXml, [], exception);
        }
        if (document.Root is not { Name.LocalName: "Solution" } root)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingSolutionRoot, []);
        foreach (var operation in operations) {
            string relative = operation switch {
                AddProjectToSolutionOperation add => add.ProjectPath,
                RemoveProjectFromSolutionOperation remove => remove.ProjectPath,
                _ => throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidSlnxOperation, []),
            };
            string project = ResolveSafePath(rootDirectory, relative, "solution project");
            var existing = root.Descendants().Where(e => e.Name.LocalName == "Project"
                && PathsEqual(solutionPath, (string?)e.Attribute("Path"), project)).ToArray();
            if (operation is RemoveProjectFromSolutionOperation) {
                if (existing.Length == 0)
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProjectNotInSolution, [relative]);
                foreach (var element in existing) element.Remove();
            }
            else {
                if (!File.Exists(project) && !stagedCreates.ContainsKey(project))
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.SolutionProjectMissing, [relative]);
                if (!project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidSolutionProjectType, []);
                if (existing.Length != 0)
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProjectAlreadyInSolution, [relative]);
                XElement parent = root;
                if (GetSolutionFolder((AddProjectToSolutionOperation)operation) is { } folder) {
                    ValidateSolutionFolder(folder);
                    string folderPath = $"/{folder}/";
                    parent = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Folder"
                        && string.Equals((string?)e.Attribute("Name"), folderPath, StringComparison.OrdinalIgnoreCase))!;
                    if (parent is null) {
                        parent = new XElement(root.Name.Namespace + "Folder", new XAttribute("Name", folderPath));
                        root.Add(parent);
                    }
                }
                parent.Add(new XElement(root.Name.Namespace + "Project", new XAttribute("Path",
                    Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, project).Replace('\\', '/'))));
            }
        }
        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>编辑传统 SLN：为新增项目生成稳定 GUID 并补配置映射，删除时清理关联记录</summary>
    private static byte[] EditSolution(
        string rootDirectory,
        string solutionPath,
        byte[] original,
        IReadOnlyList<XamlNexusRecipeProjectOperation> operations,
        IReadOnlyDictionary<string, byte[]> stagedCreates) {
        string text = Encoding.UTF8.GetString(original);
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        int globalIndex = text.IndexOf($"Global{newline}", StringComparison.Ordinal);
        if (globalIndex < 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingGlobalSection, []);

        var entries = new StringBuilder();
        var projectGuids = new List<string>();
        var addedProjectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nesting = new StringBuilder();
        foreach (XamlNexusRecipeProjectOperation operation in operations) {
            if (operation is RemoveProjectFromSolutionOperation remove) {
                text = RemoveSolutionProject(rootDirectory, solutionPath, text, remove);
                continue;
            }
            if (operation is not AddProjectToSolutionOperation add)
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidSlnOperation, []);
            string projectPath = ResolveSafePath(rootDirectory, add.ProjectPath, "solution project");
            if (!File.Exists(projectPath) && !stagedCreates.ContainsKey(projectPath))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.SolutionProjectMissing, [add.ProjectPath]);
            if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidSolutionProjectPath, [add.ProjectPath]);

            string relative = Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, projectPath).Replace('/', '\\');
            if (ContainsSolutionProject(text, relative) || !addedProjectPaths.Add(relative))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProjectAlreadyInSolution, [add.ProjectPath]);
            string name = Path.GetFileNameWithoutExtension(projectPath);
            string guid = XamlNexusSolutionGuid.CreateDeterministic(relative).ToString("B").ToUpperInvariant();
            projectGuids.Add(guid);
            if (GetSolutionFolder(add) is { } folder) {
                ValidateSolutionFolder(folder);
                if (!folders.TryGetValue(folder, out string? folderGuid)) {
                    // 复用已有解决方案文件夹；缺少时创建，不改变项目的磁盘路径。
                    var match = Regex.Match(text,
                        "Project\\(\"\\{(?:2150E333-8FDC-42A3-9474-1A3956D46DE8|66A26720-8FB5-11D2-AA7E-00C04F688DDE)\\}\"\\) = \"" +
                        Regex.Escape(folder) + "\", \"[^\"]*\", \"(?<guid>\\{[^}]+\\})\"", RegexOptions.IgnoreCase);
                    folderGuid = match.Success ? match.Groups["guid"].Value
                        : XamlNexusSolutionGuid.CreateDeterministic("solution-folder/" + folder).ToString("B").ToUpperInvariant();
                    folders.Add(folder, folderGuid);
                    if (!match.Success)
                        entries.Append("Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"")
                            .Append(folder).Append("\", \"").Append(folder).Append("\", \"").Append(folderGuid)
                            .Append('\"').Append(newline).Append("EndProject").Append(newline);
                }
                nesting.Append("\t\t").Append(guid).Append(" = ").Append(folderGuid).Append(newline);
            }
            entries.Append("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"")
                .Append(name).Append("\", \"").Append(relative).Append("\", \"").Append(guid).Append('\"').Append(newline)
                .Append("EndProject").Append(newline);
        }
        globalIndex = text.IndexOf($"Global{newline}", StringComparison.Ordinal);
        text = text.Insert(globalIndex, entries.ToString());
        text = AddSolutionBuildConfigurations(text, newline, projectGuids);
        if (nesting.Length > 0) {
            var section = Regex.Match(text, @"(?m)^[ \t]*GlobalSection\(NestedProjects\)[^\r\n]*\r?\n");
            text = section.Success ? text.Insert(section.Index + section.Length, nesting.ToString())
                : text.Insert(text.LastIndexOf("EndGlobal", StringComparison.Ordinal),
                    "\tGlobalSection(NestedProjects) = preSolution" + newline + nesting + "\tEndGlobalSection" + newline);
        }
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>所有以 Panel 结尾的项目统一归入 Panels，其他项目沿用显式指定的分组。</summary>
    private static string? GetSolutionFolder(AddProjectToSolutionOperation operation) =>
        Path.GetFileNameWithoutExtension(operation.ProjectPath.Replace('\\', '/'))
            .EndsWith("Panel", StringComparison.OrdinalIgnoreCase) ? "Panels" : operation.SolutionFolder;

    /// <summary>解决方案虚拟文件夹采用单层名称，避免向 SLN 注入结构字符。</summary>
    private static void ValidateSolutionFolder(string folder) {
        if (string.IsNullOrWhiteSpace(folder) || folder is "." or ".." || folder.IndexOfAny(['/', '\\', '"', '\r', '\n']) >= 0)
            throw new ArgumentException("Solution folder must be a single folder name.", nameof(folder));
    }

    /// <summary>按元素类型及谓词删除匹配项，缺少预期引用时抛出明确错误</summary>
    private static void RemoveItem(
        XElement projectRoot,
        string itemName,
        Func<XElement, bool> predicate,
        RecipeErrorDefinition error,
        object?[] arguments) {
        XElement? item = projectRoot.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == itemName && predicate(element));
        if (item is null)
            throw new XamlNexusRecipeException(error, arguments);

        XElement? itemGroup = item.Parent;
        item.Remove();
        if (itemGroup is not null &&
            itemGroup.Name.LocalName == "ItemGroup" &&
            itemGroup.Nodes().All(node => node is XText text && node is not XCData && string.IsNullOrWhiteSpace(text.Value))) {
            itemGroup.Remove();
        }
    }

    /// <summary>移除项目块、以项目 GUID 开头的配置映射及相关文件夹嵌套记录</summary>
    private static string RemoveSolutionProject(
        string rootDirectory,
        string solutionPath,
        string solution,
        RemoveProjectFromSolutionOperation remove) {
        string projectPath = ResolveSafePath(rootDirectory, remove.ProjectPath, "solution project");
        string relative = Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, projectPath)
            .Replace('/', '\\');
        MatchCollection lines = SolutionLineRegex().Matches(solution);
        Match? header = lines.Cast<Match>().FirstOrDefault(line => {
            string value = line.Value.TrimEnd('\r', '\n');
            if (!value.StartsWith("Project(\"", StringComparison.Ordinal)) return false;
            string[] parts = value.Split(',');
            return parts.Length >= 2 &&
                parts[1].Trim().Trim('\"').Equals(relative, StringComparison.OrdinalIgnoreCase);
        });
        if (header is null)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ProjectNotInSolution, [remove.ProjectPath]);

        string headerText = header.Value.TrimEnd('\r', '\n');
        string[] headerParts = headerText.Split(',');
        string guid = headerParts[^1].Trim().Trim('\"');
        int end = -1;
        foreach (Match line in lines) {
            if (line.Index < header.Index) continue;
            if (line.Value.TrimEnd('\r', '\n').Equals("EndProject", StringComparison.Ordinal)) {
                end = line.Index + line.Length;
                break;
            }
        }
        if (end < 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.IncompleteSolutionProject, [remove.ProjectPath]);

        solution = solution.Remove(header.Index, end - header.Index);
        solution = Regex.Replace(
            solution,
            $@"(?m)^[ \t]*{Regex.Escape(guid)}\..*(?:\r?\n|$)",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        bool inNestedProjects = false;
        return SolutionLineRegex().Replace(solution, line => {
            string value = line.Value.Trim();
            if (value.StartsWith("GlobalSection(", StringComparison.OrdinalIgnoreCase))
                inNestedProjects = value.StartsWith("GlobalSection(NestedProjects)", StringComparison.OrdinalIgnoreCase);
            else if (value.Equals("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
                inNestedProjects = false;
            if (inNestedProjects) {
                int separator = value.IndexOf('=');
                if (separator >= 0 && value[..separator].Trim().Equals(guid, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
            }
            return line.Value;
        });
    }

    /// <summary>把新项目项加入适合的 ItemGroup，必要时创建分组</summary>
    private static void AddItem(XElement projectRoot, XElement item) {
        XNamespace ns = projectRoot.Name.Namespace;
        XElement? itemGroup = projectRoot.Elements(ns + "ItemGroup")
            .LastOrDefault(group => group.Attribute("Condition") is null);
        if (itemGroup is null) {
            itemGroup = new XElement(ns + "ItemGroup");
            projectRoot.Add(itemGroup);
        }
        itemGroup.Add(item);
    }

    /// <summary>将相对路径解析到项目内部，并拒绝不安全的目标路径</summary>
    private static string ResolveSafePath(string rootDirectory, string relativePath, string description) {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.OperationPathMustBeRelative, [description]);
        string rootPrefix = Path.TrimEndingDirectorySeparator(rootDirectory) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.OperationPathEscapesProject, [description, relativePath]);
        ProjectPathSafety.EnsureNoLinks(fullPath);
        return fullPath;
    }

    /// <summary>把 Include 按所属项目目录解析成绝对路径后比较，避免相对路径写法差异</summary>
    private static bool PathsEqual(string containingProject, string? include, string expectedPath) =>
        !string.IsNullOrWhiteSpace(include) &&
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(containingProject)!, include))
            .Equals(expectedPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>检查解决方案是否已声明指定项目，避免重复加入</summary>
    private static bool ContainsSolutionProject(string solution, string relativeProjectPath) =>
        solution.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("Project(\"", StringComparison.Ordinal))
            .Select(line => line.Split(','))
            .Any(parts => parts.Length >= 2 &&
                parts[1].Trim().Trim('\"').Equals(relativeProjectPath, StringComparison.OrdinalIgnoreCase));

    /// <summary>为新增类库补齐各解决方案配置的 ActiveCfg 和 Build.0，项目平台映射到 Any CPU</summary>
    private static string AddSolutionBuildConfigurations(
        string solution,
        string newline,
        IReadOnlyList<string> projectGuids) {
        const string solutionSection = "GlobalSection(SolutionConfigurationPlatforms)";
        const string projectSection = "GlobalSection(ProjectConfigurationPlatforms)";
        int solutionStart = solution.IndexOf(solutionSection, StringComparison.Ordinal);
        int projectStart = solution.IndexOf(projectSection, StringComparison.Ordinal);
        if (solutionStart < 0 || projectStart < 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingConfigurationSections, []);

        int solutionEnd = solution.IndexOf("EndGlobalSection", solutionStart, StringComparison.Ordinal);
        int projectEnd = solution.IndexOf("EndGlobalSection", projectStart, StringComparison.Ordinal);
        if (solutionEnd < 0 || projectEnd < 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.IncompleteConfigurationSection, []);

        string[] configurations = solution[solutionStart..solutionEnd]
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Skip(1) // The section header is not a configuration entry.
            .Select(line => line.Trim())
            .Where(line => line.Contains(" = ", StringComparison.Ordinal))
            .Select(line => line[..line.IndexOf(" = ", StringComparison.Ordinal)])
            .ToArray();
        if (configurations.Length == 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingBuildConfigurations, []);

        var mappings = new StringBuilder();
        foreach (string guid in projectGuids) {
            foreach (string solutionConfiguration in configurations) {
                string configuration = solutionConfiguration.Split('|')[0];
                string projectConfiguration = $"{configuration}|Any CPU";
                mappings.Append("\t\t").Append(guid).Append('.').Append(solutionConfiguration)
                    .Append(".ActiveCfg = ").Append(projectConfiguration).Append(newline);
                mappings.Append("\t\t").Append(guid).Append('.').Append(solutionConfiguration)
                    .Append(".Build.0 = ").Append(projectConfiguration).Append(newline);
            }
        }
        // Insert before the closing line's indentation, not before its text.
        int insertionPoint = solution.LastIndexOf('\n', projectEnd) + 1;
        return solution.Insert(insertionPoint, mappings.ToString());
    }

    /// <summary>检查包 ID 字符和非空版本声明；不验证远程包是否存在</summary>
    private static void ValidatePackage(AddPackageReferenceOperation package) {
        if (!PackageIdRegex().IsMatch(package.PackageId) || string.IsNullOrWhiteSpace(package.Version))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidPackageReference, []);
    }

    /// <summary>确保无条件包引用达到最低数字版本；已满足则保留，复杂条件和版本表达式需要人工处理</summary>
    private static void EnsurePackageReference(
        XElement projectRoot,
        EnsurePackageReferenceOperation package) {
        if (!PackageIdRegex().IsMatch(package.PackageId) ||
            !Version.TryParse(package.MinimumVersion, out Version? minimumVersion)) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.InvalidMinimumPackageVersion, []);
        }

        XElement[] references = projectRoot.Descendants().Where(element =>
            element.Name.LocalName == "PackageReference" &&
            (string.Equals((string?)element.Attribute("Include"), package.PackageId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals((string?)element.Attribute("Update"), package.PackageId, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (references.Any(reference =>
                reference.AncestorsAndSelf().Any(element => element.Attribute("Condition") is not null ||
                    element.Name.LocalName is "When" or "Otherwise") ||
                reference.Descendants().Any(element => element.Attribute("Condition") is not null))) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.ConditionalPackageReference, [package.PackageId, package.ProjectPath]);
        }
        XElement? reference = references.SingleOrDefault(element => element.Attribute("Include") is not null);
        if (reference is null) {
            AddItem(projectRoot, new XElement(projectRoot.Name.Namespace + "PackageReference",
                new XAttribute("Include", package.PackageId),
                new XAttribute("Version", package.MinimumVersion)));
            return;
        }

        XAttribute? versionAttribute = reference.Attribute("Version");
        XElement? versionElement = reference.Elements().SingleOrDefault(element => element.Name.LocalName == "Version");
        string? currentText = versionAttribute?.Value ?? versionElement?.Value;
        if (!Version.TryParse(currentText, out Version? currentVersion)) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.UnsupportedPackageVersion, [package.PackageId, currentText]);
        }
        if (currentVersion >= minimumVersion) return;

        if (versionAttribute is not null)
            versionAttribute.Value = package.MinimumVersion;
        else
            versionElement!.Value = package.MinimumVersion;
    }

    /// <summary>移除可选 UTF-8 BOM 后读取 XML 文本，避免 BOM 被当作 XML 正文字符</summary>
    private static string DecodeUtf8(byte[] content) {
        ReadOnlySpan<byte> bytes = content;
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
            bytes = bytes[Encoding.UTF8.Preamble.Length..];
        return Encoding.UTF8.GetString(bytes);
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdRegex();

    [GeneratedRegex(".*(?:\\r\\n|\\n|$)", RegexOptions.CultureInvariant)]
    private static partial Regex SolutionLineRegex();
}
