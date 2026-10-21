using System.Text.RegularExpressions;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using XamlNexus.Generator.Winui3App;
using XamlNexus.Generator.Winui3_Wpf_App;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class AiGuidanceTests {
    [Theory]
    [InlineData(false, "basic")]
    [InlineData(false, "standard")]
    [InlineData(true, "basic")]
    [InlineData(true, "standard")]
    public void Generation_EmbedsVariantGuidanceAndEditableBaselines(bool hybrid, string profile) {
        string parent = Directory.CreateTempSubdirectory("xamlnexus-ai-generation-").FullName;
        try {
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "src/Templates"))) repository = repository.Parent;
            Assert.NotNull(repository);
            string templates = Path.Combine(repository.FullName, "src/Templates");
            BaseGenerator generator = hybrid ? new HybridGenerator(Path.Combine(templates, "Winui3_Wpf"))
                : new PureGenerator(Path.Combine(templates, "Winui3"));
            Assert.True(generator.Generate(new ProjectConfig {
                SlnName = "AiDemo", OutputPath = parent, Profile = profile, Language = "en-US",
                Framework = hybrid ? FrameworkType.Winui3_Wpf : FrameworkType.Winui3, SlnType = SolutionType.Sln,
            }));
            string root = Path.Combine(parent, "AiDemo");
            string manifestPath = Path.Combine(root, "xamlnexus.json");
            var manifest = XamlNexusProjectManifestStore.Load(manifestPath);
            string[] guidanceFiles = new[] { "AGENTS.md", "CLAUDE.md", "QWEN.md" }
                .Concat(new[] { ".agents/skills", ".claude/skills", ".qwen/skills" }
                    .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*.md", SearchOption.AllDirectories))
                    .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))).ToArray();
            Assert.Equal(18, guidanceFiles.Length);
            AssertReferencesAndMirrors(root);
            foreach (string relative in guidanceFiles) {
                var baseline = Assert.Single(manifest.ScaffoldFiles!, file => file.Path == relative);
                Assert.True(baseline.UserEditable);
                Assert.Equal(File.ReadAllBytes(Path.Combine(root, relative)),
                    XamlNexusBaselineContent.Decode(baseline.BaselineContentGzipBase64!));
                Assert.DoesNotContain("{{", File.ReadAllText(Path.Combine(root, relative)));
            }
            string instructions = File.ReadAllText(Path.Combine(root, "AGENTS.md"));
            string startup = hybrid ? "AiDemo/AiDemo.csproj" : "AiDemo.UI/AiDemo.UI.csproj";
            Assert.Contains(startup, instructions);
            Assert.True(File.Exists(Path.Combine(root, startup)));
            Assert.Contains($"initial profile **{profile}**", instructions);
            Assert.Equal("@AGENTS.md\n", File.ReadAllText(Path.Combine(root, "CLAUDE.md")));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    private static void AssertReferencesAndMirrors(string root) {
        string[] skills = ["xamlnexus-cli", "xamlnexus-project"];
        foreach (string skill in skills) {
            string canonical = Path.Combine(root, ".agents/skills", skill);
            foreach (string file in Directory.EnumerateFiles(canonical, "*.md", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(canonical, file);
                foreach (string directory in new[] { ".claude/skills", ".qwen/skills" })
                    Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(root, directory, skill, relative)));
                foreach (Match link in Regex.Matches(File.ReadAllText(file), @"\]\((references/[^)]+)\)"))
                    Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(file)!, link.Groups[1].Value)), link.Value);
            }
        }
    }

    private sealed class PureGenerator(string templateRoot) : Winui3Generator { protected override string TemplateRoot => templateRoot; }
    private sealed class HybridGenerator(string templateRoot) : Winui3_WpfGenerator { protected override string TemplateRoot => templateRoot; }
}
