using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamlNexus.Common.Projects;

public sealed class XamlNexusProjectManifest {
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string GeneratorVersion { get; init; }

    public required XamlNexusProjectIdentity Project { get; init; }

    public required IReadOnlyList<XamlNexusManagedModule> Modules { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<XamlNexusManagedFile>? ScaffoldFiles { get; init; }

    public IReadOnlyList<string> Validate() {
        var errors = new List<string>();

        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Unsupported schemaVersion '{SchemaVersion}'. Expected '{CurrentSchemaVersion}'.");

        if (string.IsNullOrWhiteSpace(GeneratorVersion))
            errors.Add("generatorVersion is required.");

        if (Project is null) {
            errors.Add("project is required.");
        }
        else {
            if (string.IsNullOrWhiteSpace(Project.Name))
                errors.Add("project.name is required.");
            if (Project.Preset is not ("winui" or "hybrid"))
                errors.Add("project.preset must be 'winui' or 'hybrid'.");
            if (Project.Profile is not (null or "standard" or "basic"))
                errors.Add("project.profile must be standard or basic when present.");
            if (Project.Language is not ("zh-CN" or "en-US"))
                errors.Add("project.language must be 'zh-CN' or 'en-US'.");
            if (Project.SolutionFormat is not ("sln" or "slnx"))
                errors.Add("project.solutionFormat must be 'sln' or 'slnx'.");
        }

        if (Modules is null) {
            errors.Add("modules is required.");
        }
        else {
            var moduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < Modules.Count; index++) {
                XamlNexusManagedModule? module = Modules[index];
                if (module is null) {
                    errors.Add($"modules[{index}] is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(module.Id))
                    errors.Add($"modules[{index}].id is required.");
                else if (!moduleIds.Add(module.Id))
                    errors.Add($"Duplicate module id '{module.Id}'.");

                if (string.IsNullOrWhiteSpace(module.Version))
                    errors.Add($"modules[{index}].version is required.");
                if (module.Source is not ("template" or "recipe"))
                    errors.Add($"modules[{index}].source must be 'template' or 'recipe'.");

                if (module.Source == "recipe" && module.Files is null) {
                    errors.Add($"modules[{index}].files is required for a Recipe module.");
                }
                else if (module.Files is not null) {
                    var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int fileIndex = 0; fileIndex < module.Files.Count; fileIndex++) {
                        XamlNexusManagedFile? file = module.Files[fileIndex];
                        if (file is null) {
                            errors.Add($"modules[{index}].files[{fileIndex}] is required.");
                            continue;
                        }
                        if (!IsSafeRelativePath(file.Path))
                            errors.Add($"modules[{index}].files[{fileIndex}].path must stay inside the project.");
                        else if (!filePaths.Add(file.Path))
                            errors.Add($"Duplicate managed file path '{file.Path}' in module '{module.Id}'.");
                        if (!IsSha256(file.Sha256))
                            errors.Add($"modules[{index}].files[{fileIndex}].sha256 must be a SHA-256 hash.");
                        ValidateBaselineContent(
                            file,
                            $"modules[{index}].files[{fileIndex}]",
                            errors);
                    }
                }
            }
        }


        if (ScaffoldFiles is not null) {
            var scaffoldPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < ScaffoldFiles.Count; index++) {
                XamlNexusManagedFile? file = ScaffoldFiles[index];
                if (file is null) {
                    errors.Add($"scaffoldFiles[{index}] is required.");
                    continue;
                }
                if (!IsSafeRelativePath(file.Path))
                    errors.Add($"scaffoldFiles[{index}].path must stay inside the project.");
                else if (!scaffoldPaths.Add(file.Path))
                    errors.Add($"Duplicate scaffold file path '{file.Path}'.");
                if (!IsSha256(file.Sha256))
                    errors.Add($"scaffoldFiles[{index}].sha256 must be a SHA-256 hash.");
                ValidateBaselineContent(file, $"scaffoldFiles[{index}]", errors);
            }
        }

        return errors;
    }

    private static bool IsSafeRelativePath(string path) {
        if (string.IsNullOrWhiteSpace(path) || System.IO.Path.IsPathRooted(path)) return false;
        string normalized = path.Replace('\\', '/');
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    private static bool IsSha256(string value) =>
        value is not null &&
        value.Length == 64 &&
        value.All(character => Uri.IsHexDigit(character));

    private static void ValidateBaselineContent(
        XamlNexusManagedFile file,
        string propertyPath,
        ICollection<string> errors) {
        if (file.BaselineContentGzipBase64 is null) return;
        try {
            byte[] content = XamlNexusBaselineContent.Decode(file.BaselineContentGzipBase64);
            string contentHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
            if (!contentHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                errors.Add($"{propertyPath}.baselineContentGzipBase64 does not match its SHA-256 hash.");
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or IOException) {
            errors.Add($"{propertyPath}.baselineContentGzipBase64 is not valid GZip Base64 content.");
        }
    }
}

public sealed class XamlNexusProjectIdentity {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Profile { get; init; }

    public required string Name { get; init; }

    public required string Preset { get; init; }

    public required string Language { get; init; }

    public required string SolutionFormat { get; init; }
}

public sealed class XamlNexusManagedModule {
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Source { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<XamlNexusManagedFile>? Files { get; init; }
}

public sealed class XamlNexusManagedFile {
    public required string Path { get; init; }

    public required string Sha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaselineContentGzipBase64 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UserEditable { get; init; }
}

public static class XamlNexusProjectManifestStore {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static XamlNexusProjectManifest Load(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        XamlNexusProjectManifest manifest = JsonSerializer.Deserialize<XamlNexusProjectManifest>(
            stream,
            SerializerOptions) ?? throw new InvalidDataException("The XamlNexus project manifest is empty.");

        ThrowIfInvalid(manifest, path);
        return manifest;
    }

    public static void Save(string path, XamlNexusProjectManifest manifest) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(manifest);
        ThrowIfInvalid(manifest, path);

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + ".tmp";
        try {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None)) {
                JsonSerializer.Serialize(stream, manifest, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void ThrowIfInvalid(XamlNexusProjectManifest manifest, string path) {
        IReadOnlyList<string> errors = manifest.Validate();
        if (errors.Count > 0) {
            throw new InvalidDataException(
                $"Invalid XamlNexus project manifest '{path}':{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
        }
    }
}
