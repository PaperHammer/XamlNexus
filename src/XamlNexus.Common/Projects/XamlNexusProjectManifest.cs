using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamlNexus.Common.Projects;

/// <summary>
/// 项目根目录 xamlnexus.json 的数据模型，记录项目身份、已包含的模块以及受管理文件的基线
/// 脚手架通过清单识别项目状态，并为组件操作、诊断和升级提供依据
/// </summary>
public sealed class XamlNexusProjectManifest {
    /// <summary>当前支持的清单结构版本，与生成器的软件版本分别管理</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>此清单采用的结构版本；校验时必须与当前支持版本一致</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>清单记录的生成器版本，用于标识项目对应的脚手架版本</summary>
    public required string GeneratorVersion { get; init; }

    /// <summary>项目名称、架构预设、能力组合、语言和解决方案格式</summary>
    public required XamlNexusProjectIdentity Project { get; init; }

    /// <summary>项目已包含的能力，来源可以是模板自带的 template 或后续安装的 recipe</summary>
    public required IReadOnlyList<XamlNexusManagedModule> Modules { get; init; }

    /// <summary>
    /// 脚手架文件的路径、内容哈希及可选基线快照，供升级比较和合并使用
    /// 允许旧清单不包含此集合；为 null 时不写入 JSON
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<XamlNexusManagedFile>? ScaffoldFiles { get; init; }

    /// <summary>
    /// 检查清单字段、重复标识、相对路径、哈希格式，以及内嵌基线与哈希的一致性
    /// 只校验清单数据，不读取项目磁盘文件来确认其存在或内容是否发生变化
    /// </summary>
    /// <returns>发现的问题说明；正常完成且返回空集合表示这些清单检查通过</returns>
    public IReadOnlyList<string> Validate() {
        var errors = new List<string>();

        // 拒绝未知结构版本，避免使用当前规则误读不兼容的清单
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Unsupported schemaVersion '{SchemaVersion}'. Expected '{CurrentSchemaVersion}'.");

        if (string.IsNullOrWhiteSpace(GeneratorVersion))
            errors.Add("generatorVersion is required.");

        // required 不能替代运行时校验：仍需检查 null、空字符串及允许的取值
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
            // 模块 ID 忽略大小写判重，防止把 sqlite 和 SQLite 当作两个模块
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

                // Recipe 必须提供受管理文件集合，供后续更新和移除使用；这里允许空集合
                // 模板模块可以不逐个记录文件，其脚手架基线由 ScaffoldFiles 统一保存
                if (module.Source == "recipe" && module.Files is null) {
                    errors.Add($"modules[{index}].files is required for a Recipe module.");
                }
                else if (module.Files is not null) {
                    // 此集合仅检查当前模块内部的重复路径，不检查不同模块之间的文件归属冲突
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

        // 可选的脚手架文件集合采用同样的路径、哈希和基线校验规则
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

    /// <summary>
    /// 检查路径不是空白或根路径，且不包含独立的 . 或 .. 路径段
    /// 这是字符串层面的检查，不解析磁盘上的符号链接，也不验证所有非法文件名字符
    /// </summary>
    private static bool IsSafeRelativePath(string path) {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        string normalized = path.Replace('\\', '/');

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    /// <summary>检查是否为 64 位十六进制字符串；这里只检查格式，不计算文件哈希</summary>
    private static bool IsSha256(string value) =>
        value is not null &&
        value.Length == 64 &&
        value.All(Uri.IsHexDigit);

    /// <summary>
    /// 有内嵌基线时，将其解码、解压并计算 SHA-256，确认快照与清单记录的哈希一致
    /// propertyPath 用于指出出错的 JSON 字段；可识别的编码或压缩错误追加到 errors
    /// </summary>
    private static void ValidateBaselineContent(XamlNexusManagedFile file, string propertyPath, List<string> errors) {
        // 基线内容是可选字段；仅有哈希而没有快照的记录不会在此处进行内容校验
        if (file.BaselineContentGzipBase64 is null) return;

        try {
            // Decode 同时限制单份基线的解压大小；哈希计算针对原始字节，而非 Base64 文本
            byte[] content = XamlNexusBaselineContent.Decode(file.BaselineContentGzipBase64);
            string contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
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
