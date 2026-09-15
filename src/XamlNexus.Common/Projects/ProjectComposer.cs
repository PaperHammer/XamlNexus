using System.Collections.ObjectModel;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Recipes;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Projects;

public sealed record CompositionRequest(ProjectConfig Project, string Profile, IReadOnlyList<string> Features);

public static class CompositionPlanner {
    public static IReadOnlyList<IXamlNexusRecipe> Resolve(
        string preset, string profile, IEnumerable<string> features, IXamlNexusRecipeCatalog catalog,
        IEnumerable<XamlNexusManagedModule>? installedModules = null) =>
        ResolveCore(preset, profile, features, catalog, (installedModules ?? []).Select(module => module.Id), false);

    public static IReadOnlyList<IXamlNexusRecipe> ResolveForCreation(
        string preset, string profile, IEnumerable<string> features, IXamlNexusRecipeCatalog catalog,
        IEnumerable<string> includedModuleIds) =>
        ResolveCore(preset, profile, features, catalog, includedModuleIds, true);

    /// <summary>
    /// 解析所选 Recipe 及其依赖，检查循环依赖、架构兼容性和模块冲突，生成依赖优先的安装计划。
    /// 此方法只规划，不安装模块或修改项目文件。
    /// </summary>
    /// <param name="preset">项目架构：winui 或 hybrid。</param>
    /// <param name="profile">项目配置档：standard 或 basic；此处仅校验取值，不展开配置档模块。</param>
    /// <param name="features">用户明确请求的 Recipe 标识，依赖会自动递归补齐。</param>
    /// <param name="catalog">用于查询 Recipe 实现、支持的架构、依赖和冲突声明的目录。</param>
    /// <param name="existingIds">已有模块标识，包括已安装模块或创建时基础模板已包含的模块。</param>
    /// <param name="reuseRequestedModules">是否允许用户明确请求已有模块；允许时复用，否则报告重复安装。</param>
    /// <returns>排除已有模块、去重且依赖排在使用者之前的只读 Recipe 列表。</returns>
    private static ReadOnlyCollection<IXamlNexusRecipe> ResolveCore(
        string preset, string profile, IEnumerable<string> features, IXamlNexusRecipeCatalog catalog,
        IEnumerable<string> existingIds, bool reuseRequestedModules) {
        // 配置档和架构使用固定的小写标识；后续模块标识比较则忽略大小写。
        if (profile is not ("standard" or "basic")) throw new ArgumentException("Profile must be standard or basic.");
        if (preset is not ("winui" or "hybrid")) throw new ArgumentException("Unsupported architecture.");

        // 当前递归链中尚未完成解析的模块，用于识别 A -> B -> A 这样的循环依赖。
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 保存调用前已有的模块；不会把本次规划的新模块加入这个集合。
        var installed = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 已有模块视为依赖已满足；新模块解析完成后也加入，避免重复规划共享依赖。
        var completed = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
        // 仅收集本次需要安装的 Recipe，顺序由下面的依赖优先遍历确定。
        var result = new List<IXamlNexusRecipe>();

        // 局部递归函数：先处理一个 Recipe 的所有依赖，再将它自身加入安装计划。
        void Visit(string id) {
            // 已有或已完成规划的模块直接复用，不再查询和展开其依赖。
            if (completed.Contains(id)) return;
            // Add 返回 false 表示模块仍在当前递归链中，说明存在循环依赖。
            if (!visiting.Add(id)) throw new InvalidOperationException($"Cyclic Recipe dependency at '{id}'.");
            // 对需要新增的模块检查定义是否存在，以及是否支持当前项目架构。
            var recipe = catalog.Find(id) ?? throw new InvalidOperationException($"Unknown Recipe '{id}'.");
            if (!recipe.Descriptor.SupportedPresets.Contains(preset, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Recipe '{id}' does not support preset '{preset}'.");
            // 对同级依赖排序，使计划顺序稳定；递归保证依赖先于使用者加入结果。
            foreach (string dependency in recipe.Descriptor.Dependencies.Order(StringComparer.OrdinalIgnoreCase)) Visit(dependency);
            // 当前模块依赖已全部处理，退出递归链并标记为规划完成（尚未实际安装）。
            visiting.Remove(id);
            completed.Add(id);
            result.Add(recipe);
        }
        // 用户请求去重并排序后逐个解析；重复安装检查只针对明确请求，不针对递归依赖。
        foreach (string id in features.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) {
            // 创建项目时允许复用模板自带模块；向已有项目添加模块时拒绝明确重复安装。
            if (installed.Contains(id) && !reuseRequestedModules)
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.AlreadyInstalled, [id]);
            Visit(id);
        }
        // 此时 completed 是已有模块与计划新增模块的并集。
        // 检查目录中属于该集合的所有 Recipe，既覆盖新模块之间的冲突，也覆盖与已有模块的冲突。
        foreach (var recipe in catalog.Recipes.Where(recipe => completed.Contains(recipe.Descriptor.Id))) {
            foreach (string conflict in recipe.Descriptor.Conflicts)
                if (completed.Contains(conflict))
                    throw new InvalidOperationException($"Recipe '{recipe.Descriptor.Id}' conflicts with '{conflict}'.");
        }
        // 以只读包装返回计划，避免调用方直接增删列表；实际安装由调用方执行。
        return result.AsReadOnly();
    }
}

public static class ProjectComposer {
    public static string Create(CompositionRequest request, IGenerator generator, IXamlNexusRecipeCatalog catalog) {
        var config = request.Project;
        string preset = config.Framework switch {
            FrameworkType.Winui3 => "winui",
            FrameworkType.Winui3_Wpf => "hybrid",
            _ => throw new ArgumentException("Unsupported architecture."),
        };
        var recipes = CompositionPlanner.ResolveForCreation(preset, request.Profile, request.Features, catalog,
            generator.GetIncludedModuleIds(request.Profile));
        if (string.IsNullOrWhiteSpace(config.SlnName) || config.SlnName is "." or ".."
            || Path.GetFileName(config.SlnName) != config.SlnName)
            throw new ArgumentException("The project name must be a single directory name.");

        string parent = Path.GetFullPath(config.OutputPath);
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, config.SlnName);
        string staging = Directory.CreateTempSubdirectory("xamlnexus-create-").FullName;
        bool ownsDestination = false;
        Exception? failure = null;
        try {
            var stagedConfig = new ProjectConfig {
                Profile = request.Profile,
                SlnName = config.SlnName, OutputPath = staging, Language = config.Language,
                Framework = config.Framework, SlnType = config.SlnType,
            };
            if (!generator.Generate(stagedConfig, reportSuccess: false))
                throw new InvalidOperationException("Application scaffold generation failed.");
            CommandLine.CreationReport.RunFinishing(() => {
                string projectRoot = Path.Combine(stagedConfig.OutputPath, config.SlnName);
                foreach (var recipe in recipes) {
                    // Reload after each installation so dependencies and expected hashes reflect prior changes.
                    var project = XamlNexusProjectLocator.Locate(projectRoot);
                    XamlNexusRecipeTransaction.Apply(project, recipe);
                }
                var report = XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(projectRoot));
                if (!report.IsValid) throw new InvalidOperationException("The composed project failed validation.");
                // Reserve a new destination atomically; never copy into a directory
                // another process created after the initial existence check.
                destination = ProjectOutputReservation.Create(parent, config.SlnName);
                ownsDestination = true;
                // Staging may live on another volume. Copy source files first and solution
                // discovery files last so IDEs only load the completed composition.
                var files = Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => (Source: path, Relative: Path.GetRelativePath(projectRoot, path)))
                    .Where(file => !file.Relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".vs"))
                    .OrderBy(file => Path.GetExtension(file.Source) is ".sln" or ".slnx" ? 2 : Path.GetExtension(file.Source) == ".csproj" ? 1 : 0)
                    .ThenBy(file => file.Relative, StringComparer.Ordinal).ToArray();
                foreach (var (Source, Relative) in files) {
                    string target = Path.Combine(destination, Relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(Source, target, overwrite: false);
                }
                if (!XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(destination)).IsValid)
                    throw new InvalidOperationException("The published project failed validation.");
            });
            return destination;
        }
        catch (Exception exception) {
            failure = exception;
            if (ownsDestination) {
                try { Directory.Delete(destination, recursive: true); }
                catch (Exception cleanupException) {
                    failure.Data["CleanupError"] = $"Incomplete project '{destination}' could not be removed: {cleanupException.Message}";
                }
            }
            throw;
        }
        finally {
            // Only remove the unique temporary directory created by this invocation.
            try {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
            catch (Exception cleanupException) {
                if (failure is not null) failure.Data["CleanupError"] = $"{failure.Data["CleanupError"]} Temporary directory '{staging}' could not be removed: {cleanupException.Message}";
                else System.Diagnostics.Trace.TraceWarning($"Could not remove staging directory '{staging}': {cleanupException.Message}");
            }
        }
    }
}
