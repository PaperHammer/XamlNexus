using XamlNexus.Tooling.Diagnostics;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class DoctorSqliteTests {
    [Theory]
    [InlineData("InitializeCustomDatabase();", XamlNexusDoctorSeverity.Warning)]
    [InlineData("Execute(\"PRAGMA journal_mode=WAL;\");", XamlNexusDoctorSeverity.Pass)]
    [InlineData("// journal_mode=WAL is intentionally not executed", XamlNexusDoctorSeverity.Pass)]
    public void SourceHeuristicDoesNotClaimRuntimeConfigurationOrBlockCustomization(string source, XamlNexusDoctorSeverity expected) {
        string root = Directory.CreateTempSubdirectory("xamlnexus-doctor-sqlite-").FullName;
        try {
            void Write(string relative, string content) {
                string path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }
            Write("App.sln", "");
            Write("Directory.Build.props", "<Project/>");
            Write("App.Common/App.Common.csproj", "<Project/>");
            Write("App.Data/App.Data.csproj", "<Project/>");
            Write("App.UI/App.UI.csproj", "<Project><PropertyGroup><TargetFramework>net8.0-windows10.0.19041.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include='Microsoft.WindowsAppSDK' Version='1.6.0'/><ProjectReference Include='../App.Data/App.Data.csproj'/></ItemGroup></Project>");
            Write("App.Data/Persistence/SqliteDatabase.cs", source);
            string manifest = Path.Combine(root, "xamlnexus.json");
            XamlNexusProjectManifestStore.Save(manifest, new() {
                GeneratorVersion = "1.0.0",
                Project = new() { Name = "App", Preset = "winui", Language = "en-US", SolutionFormat = "sln" },
                Modules = [new() { Id = "sqlite", Source = "recipe", Version = "1.0.0", Files = [] }],
            });
            var report = XamlNexusDoctor.Diagnose(XamlNexusProjectLocator.Locate(root), BuiltInRecipeCatalog.Create(), probeEnvironment: false);
            var check = Assert.Single(report.Checks, item => item.Code == "XD5002");
            Assert.Equal(expected, check.Severity);
            Assert.True(report.IsHealthy);
            if (expected == XamlNexusDoctorSeverity.Pass)
                Assert.Contains(LanguageRegistry.CurrentLanguage == LanguageType.Chinese
                    ? "尚未验证运行时日志模式" : "runtime journal mode has not been verified", check.Message);
            else Assert.Contains(LanguageRegistry.CurrentLanguage == LanguageType.Chinese
                ? "允许自定义初始化或其他日志模式" : "Custom initialization or another journal mode is allowed", check.Message);
            Assert.Equal(source, File.ReadAllText(Path.Combine(root, "App.Data/Persistence/SqliteDatabase.cs")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
