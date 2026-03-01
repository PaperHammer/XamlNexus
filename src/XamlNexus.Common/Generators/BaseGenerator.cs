using Spectre.Console;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Generators {
    public abstract class BaseGenerator : IGenerator {
        public abstract void Generate(ProjectConfig config);

        protected void WriteFile(string path, string content, Dictionary<string, string> tokens) {
            // 替换占位符，例如把 {{PROJECT_NAME}} 替换为真实名称
            foreach (var token in tokens) {
                content = content.Replace($"{{{{{token.Key}}}}}", token.Value);
            }

            string? dir = Path.GetDirectoryName(path)!;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content);
        }

        // todo：适配 slnx
        protected void GenerateSolution(string rootPath, string projectName) {
            string projectGuid = Guid.NewGuid().ToString("B").ToUpper();
            string slnContent = $$"""
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{projectName}}", "{{projectName}}\{{projectName}}.csproj", "{{projectGuid}}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|x64 = Debug|x64
		Release|x64 = Release|x64
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{{projectGuid}}.Debug|x64.ActiveCfg = Debug|x64
		{{projectGuid}}.Debug|x64.Build.0 = Debug|x64
		{{projectGuid}}.Debug|x64.Deploy.0 = Debug|x64
		{{projectGuid}}.Release|x64.ActiveCfg = Release|x64
		{{projectGuid}}.Release|x64.Build.0 = Release|x64
		{{projectGuid}}.Release|x64.Deploy.0 = Release|x64
	EndGlobalSection
EndGlobal
""";

            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            File.WriteAllText(Path.Combine(rootPath, $"{projectName}.sln"), slnContent);
        }

        protected void CreateDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
                AnsiConsole.MarkupLine($"  [grey]创建目录:[/] {path}");
            }
        }
    }

    public class ProjectConfig {
        public string ProjectName { get; set; } = "MyXamlNexusApp";
        public string Language { get; set; } = "C#";
        public FrameworkType Framework { get; set; }
        public SolutionType SlnType { get; set; }
        public bool NeedTray { get; set; }
        public string OutputPath {
            get {
#if DEBUG
                DebugUtil.RestoreOutputDir();
                return DebugUtil.OutputPath;
#else
        return Path.Combine(Environment.CurrentDirectory, ProjectName);
#endif
            }
        }
    }

    public enum FrameworkType {
        Wpf, Winui3, Winui3_Wpf
    }

    public enum SolutionType {
        Sln, Slnx
    }
}
