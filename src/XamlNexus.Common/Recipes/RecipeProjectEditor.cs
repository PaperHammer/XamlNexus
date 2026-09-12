using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

internal static partial class XamlNexusRecipeProjectEditor {
    public static IReadOnlyList<ResolvedRecipeFileChange> ResolveAndValidate(
        string rootDirectory,
        IReadOnlyList<XamlNexusRecipeProjectOperation> operations,
        IReadOnlyList<ResolvedRecipeFileChange> fileChanges) {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0) return [];

        var stagedCreates = fileChanges
            .Where(change => change.Change.Kind == XamlNexusRecipeFileChangeKind.Create)
            .ToDictionary(change => change.FullPath, change => change.Change.Content!, StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, List<XamlNexusRecipeProjectOperation>>(StringComparer.OrdinalIgnoreCase);

        foreach (XamlNexusRecipeProjectOperation operation in operations) {
            if (operation is null)
                throw new XamlNexusRecipeException("XR1220", "Recipe plan contains an empty project operation.");
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
                    "XR1221",
                    $"Unsupported project operation: {operation.GetType().Name}"),
            };
            string target = ResolveSafePath(rootDirectory, relativeTarget, "project operation target");
            if (fileChanges.Any(change => change.FullPath.Equals(target, StringComparison.OrdinalIgnoreCase))) {
                throw new XamlNexusRecipeException(
                    "XR1222",
                    $"A project operation and file operation target the same file: {relativeTarget}");
            }
            if (!File.Exists(target))
                throw new XamlNexusRecipeException("XR1223", $"Project operation target does not exist: {relativeTarget}");

            if (!targets.TryGetValue(target, out List<XamlNexusRecipeProjectOperation>? list)) {
                list = [];
                targets.Add(target, list);
            }
            list.Add(operation);
        }

        var result = new List<ResolvedRecipeFileChange>();
        foreach ((string target, List<XamlNexusRecipeProjectOperation> targetOperations) in targets) {
            byte[] original = File.ReadAllBytes(target);
            byte[] updated = target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                ? EditProject(rootDirectory, target, original, targetOperations, stagedCreates)
                : target.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                    ? EditXmlSolution(rootDirectory, target, original, targetOperations, stagedCreates)
                : target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                    ? EditSolution(rootDirectory, target, original, targetOperations, stagedCreates)
                    : throw new XamlNexusRecipeException(
                        "XR1224",
                        $"Unsupported project operation file type: {Path.GetRelativePath(rootDirectory, target)}");

            if (updated.AsSpan().SequenceEqual(original)) continue;

            var change = new XamlNexusRecipeFileChange {
                Kind = XamlNexusRecipeFileChangeKind.Replace,
                RelativePath = Path.GetRelativePath(rootDirectory, target),
                Content = updated,
                ExpectedSha256 = XamlNexusRecipeHash.Compute(original),
            };
            result.Add(new ResolvedRecipeFileChange(change, target, IsSharedProjectFile: true));
        }
        return result;
    }

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
                "XR1225",
                $"Invalid MSBuild project XML: {Path.GetRelativePath(rootDirectory, projectPath)}",
                exception);
        }
        XElement root = document.Root
            ?? throw new XamlNexusRecipeException("XR1225", "MSBuild project has no root element.");

        foreach (XamlNexusRecipeProjectOperation operation in operations) {
            switch (operation) {
                case AddPackageReferenceOperation package:
                    ValidatePackage(package);
                    if (root.Descendants().Any(element =>
                            element.Name.LocalName == "PackageReference" &&
                            string.Equals((string?)element.Attribute("Include"), package.PackageId, StringComparison.OrdinalIgnoreCase))) {
                        throw new XamlNexusRecipeException("XR1226", $"PackageReference '{package.PackageId}' already exists.");
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
                        throw new XamlNexusRecipeException("XR1227", $"Referenced project does not exist: {reference.ReferencedProjectPath}");
                    string relativeReference = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, referencedPath);
                    if (root.Descendants().Any(element =>
                            element.Name.LocalName == "ProjectReference" &&
                            PathsEqual(projectPath, (string?)element.Attribute("Include"), referencedPath))) {
                        throw new XamlNexusRecipeException("XR1228", $"ProjectReference '{reference.ReferencedProjectPath}' already exists.");
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
                        "XR1243",
                        $"ProjectReference '{reference.ReferencedProjectPath}' does not exist.");
                    break;

                case AddProtobufOperation protobuf:
                    string protoPath = ResolveSafePath(rootDirectory, protobuf.ProtoPath, "Protobuf source");
                    if (!File.Exists(protoPath) && !stagedCreates.ContainsKey(protoPath))
                        throw new XamlNexusRecipeException("XR1240", $"Protobuf source does not exist: {protobuf.ProtoPath}");
                    if (!protoPath.EndsWith(".proto", StringComparison.OrdinalIgnoreCase))
                        throw new XamlNexusRecipeException("XR1241", $"Protobuf source must be a .proto file: {protobuf.ProtoPath}");
                    string relativeProto = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, protoPath);
                    if (root.Descendants().Any(element =>
                            element.Name.LocalName == "Protobuf" &&
                            PathsEqual(projectPath, (string?)element.Attribute("Include"), protoPath))) {
                        throw new XamlNexusRecipeException("XR1242", $"Protobuf source is already included: {protobuf.ProtoPath}");
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
                        "XR1244",
                        $"Protobuf source is not included: {protobuf.ProtoPath}");
                    break;

                default:
                    throw new XamlNexusRecipeException("XR1229", "Only reference operations can target a .csproj file.");
            }
        }

        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static byte[] EditXmlSolution(string rootDirectory, string solutionPath, byte[] original,
        IReadOnlyList<XamlNexusRecipeProjectOperation> operations,
        IReadOnlyDictionary<string, byte[]> stagedCreates) {
        XDocument document;
        try { document = XDocument.Parse(DecodeUtf8(original), LoadOptions.PreserveWhitespace); }
        catch (System.Xml.XmlException exception) {
            throw new XamlNexusRecipeException("XR1230", "Invalid SLNX XML.", exception);
        }
        if (document.Root is not { Name.LocalName: "Solution" } root)
            throw new XamlNexusRecipeException("XR1230", "SLNX requires a Solution root.");
        foreach (var operation in operations) {
            string relative = operation switch {
                AddProjectToSolutionOperation add => add.ProjectPath,
                RemoveProjectFromSolutionOperation remove => remove.ProjectPath,
                _ => throw new XamlNexusRecipeException("XR1231", "Only solution operations can target a .slnx file."),
            };
            string project = ResolveSafePath(rootDirectory, relative, "solution project");
            var existing = root.Descendants().Where(e => e.Name.LocalName == "Project"
                && PathsEqual(solutionPath, (string?)e.Attribute("Path"), project)).ToArray();
            if (operation is RemoveProjectFromSolutionOperation) {
                if (existing.Length == 0)
                    throw new XamlNexusRecipeException("XR1245", $"Project is not in the solution: {relative}");
                foreach (var element in existing) element.Remove();
            }
            else {
                if (!File.Exists(project) && !stagedCreates.ContainsKey(project))
                    throw new XamlNexusRecipeException("XR1232", $"Solution project does not exist: {relative}");
                if (!project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    throw new XamlNexusRecipeException("XR1233", "Solution project must be a .csproj file.");
                if (existing.Length != 0)
                    throw new XamlNexusRecipeException("XR1234", $"Project is already in the solution: {relative}");
                root.Add(new XElement(root.Name.Namespace + "Project", new XAttribute("Path",
                    Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, project).Replace('\\', '/'))));
            }
        }
        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

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
            throw new XamlNexusRecipeException("XR1230", "Solution file does not contain a Global section.");

        var entries = new StringBuilder();
        var projectGuids = new List<string>();
        var addedProjectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (XamlNexusRecipeProjectOperation operation in operations) {
            if (operation is RemoveProjectFromSolutionOperation remove) {
                text = RemoveSolutionProject(rootDirectory, solutionPath, text, remove);
                continue;
            }
            if (operation is not AddProjectToSolutionOperation add)
                throw new XamlNexusRecipeException("XR1231", "Only solution operations can target a .sln file.");
            string projectPath = ResolveSafePath(rootDirectory, add.ProjectPath, "solution project");
            if (!File.Exists(projectPath) && !stagedCreates.ContainsKey(projectPath))
                throw new XamlNexusRecipeException("XR1232", $"Solution project does not exist: {add.ProjectPath}");
            if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                throw new XamlNexusRecipeException("XR1233", $"Solution project must be a .csproj file: {add.ProjectPath}");

            string relative = Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, projectPath).Replace('/', '\\');
            if (ContainsSolutionProject(text, relative) || !addedProjectPaths.Add(relative))
                throw new XamlNexusRecipeException("XR1234", $"Project is already in the solution: {add.ProjectPath}");
            string name = Path.GetFileNameWithoutExtension(projectPath);
            string guid = XamlNexusSolutionGuid.CreateDeterministic(relative).ToString("B").ToUpperInvariant();
            projectGuids.Add(guid);
            entries.Append("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"")
                .Append(name).Append("\", \"").Append(relative).Append("\", \"").Append(guid).Append('\"').Append(newline)
                .Append("EndProject").Append(newline);
        }
        globalIndex = text.IndexOf($"Global{newline}", StringComparison.Ordinal);
        text = text.Insert(globalIndex, entries.ToString());
        text = AddSolutionBuildConfigurations(text, newline, projectGuids);
        return Encoding.UTF8.GetBytes(text);
    }

    private static void RemoveItem(
        XElement projectRoot,
        string itemName,
        Func<XElement, bool> predicate,
        string errorCode,
        string errorMessage) {
        XElement? item = projectRoot.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == itemName && predicate(element));
        if (item is null)
            throw new XamlNexusRecipeException(errorCode, errorMessage);

        XElement? itemGroup = item.Parent;
        item.Remove();
        if (itemGroup is not null &&
            itemGroup.Name.LocalName == "ItemGroup" &&
            itemGroup.Nodes().All(node => node is XText text && node is not XCData && string.IsNullOrWhiteSpace(text.Value))) {
            itemGroup.Remove();
        }
    }

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
            throw new XamlNexusRecipeException("XR1245", $"Project is not in the solution: {remove.ProjectPath}");

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
            throw new XamlNexusRecipeException("XR1246", $"Solution project entry is incomplete: {remove.ProjectPath}");

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

    private static string ResolveSafePath(string rootDirectory, string relativePath, string description) {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new XamlNexusRecipeException("XR1235", $"The {description} path must be relative.");
        string rootPrefix = Path.TrimEndingDirectorySeparator(rootDirectory) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException("XR1236", $"The {description} path escapes the project: {relativePath}");
        ProjectPathSafety.EnsureNoLinks(fullPath);
        return fullPath;
    }

    private static bool PathsEqual(string containingProject, string? include, string expectedPath) =>
        !string.IsNullOrWhiteSpace(include) &&
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(containingProject)!, include))
            .Equals(expectedPath, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsSolutionProject(string solution, string relativeProjectPath) =>
        solution.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("Project(\"", StringComparison.Ordinal))
            .Select(line => line.Split(','))
            .Any(parts => parts.Length >= 2 &&
                parts[1].Trim().Trim('\"').Equals(relativeProjectPath, StringComparison.OrdinalIgnoreCase));

    private static string AddSolutionBuildConfigurations(
        string solution,
        string newline,
        IReadOnlyList<string> projectGuids) {
        const string solutionSection = "GlobalSection(SolutionConfigurationPlatforms)";
        const string projectSection = "GlobalSection(ProjectConfigurationPlatforms)";
        int solutionStart = solution.IndexOf(solutionSection, StringComparison.Ordinal);
        int projectStart = solution.IndexOf(projectSection, StringComparison.Ordinal);
        if (solutionStart < 0 || projectStart < 0)
            throw new XamlNexusRecipeException("XR1238", "Solution file is missing configuration sections.");

        int solutionEnd = solution.IndexOf("EndGlobalSection", solutionStart, StringComparison.Ordinal);
        int projectEnd = solution.IndexOf("EndGlobalSection", projectStart, StringComparison.Ordinal);
        if (solutionEnd < 0 || projectEnd < 0)
            throw new XamlNexusRecipeException("XR1238", "Solution configuration section is incomplete.");

        string[] configurations = solution[solutionStart..solutionEnd]
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Contains(" = ", StringComparison.Ordinal))
            .Select(line => line[..line.IndexOf(" = ", StringComparison.Ordinal)])
            .ToArray();
        if (configurations.Length == 0)
            throw new XamlNexusRecipeException("XR1238", "Solution has no build configurations.");

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
        return solution.Insert(projectEnd, mappings.ToString());
    }

    private static void ValidatePackage(AddPackageReferenceOperation package) {
        if (!PackageIdRegex().IsMatch(package.PackageId) || string.IsNullOrWhiteSpace(package.Version))
            throw new XamlNexusRecipeException("XR1237", "PackageReference requires a valid package id and version.");
    }

    private static void EnsurePackageReference(
        XElement projectRoot,
        EnsurePackageReferenceOperation package) {
        if (!PackageIdRegex().IsMatch(package.PackageId) ||
            !Version.TryParse(package.MinimumVersion, out Version? minimumVersion)) {
            throw new XamlNexusRecipeException(
                "XR1237",
                "PackageReference requires a valid package id and numeric minimum version.");
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
                "XR1247",
                $"PackageReference '{package.PackageId}' in '{package.ProjectPath}' is conditional. " +
                "XamlNexus cannot guarantee the required dependency for every build configuration and will not modify it automatically. " +
                "Review the conditions and provide an unconditional reference meeting the minimum version before retrying.");
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
                "XR1239",
                $"PackageReference '{package.PackageId}' uses an unsupported version expression '{currentText}'.");
        }
        if (currentVersion >= minimumVersion) return;

        if (versionAttribute is not null)
            versionAttribute.Value = package.MinimumVersion;
        else
            versionElement!.Value = package.MinimumVersion;
    }

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
