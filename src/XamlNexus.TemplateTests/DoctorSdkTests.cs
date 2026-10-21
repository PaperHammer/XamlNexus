using XamlNexus.Tooling.Diagnostics;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class DoctorSdkTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DoctorUsesSdkSelectedByAncestorGlobalJson(bool missingSdk) {
        string parent = Directory.CreateTempSubdirectory("xamlnexus-doctor-sdk-").FullName;
        try {
            var installedSelection = ShellExecutor.Run("dotnet", "--version", parent);
            Assert.True(installedSelection.Success, installedSelection.DiagnosticOutput);
            string version = missingSdk ? "99.0.100" : installedSelection.StandardOutput.Trim();
            File.WriteAllText(Path.Combine(parent, "global.json"),
                System.Text.Json.JsonSerializer.Serialize(new { sdk = new { version, rollForward = "disable" } }));
            string root = Directory.CreateDirectory(Path.Combine(parent, "App")).FullName;
            string manifestPath = Path.Combine(root, "xamlnexus.json");
            XamlNexusProjectManifestStore.Save(manifestPath, new() {
                GeneratorVersion = "1.0.0", Modules = [],
                Project = new() { Name = "App", Preset = "winui", Language = "en-US", SolutionFormat = "sln" },
            });
            var report = XamlNexusDoctor.Diagnose(XamlNexusProjectLocator.Locate(root), BuiltInRecipeCatalog.Create());
            var sdk = Assert.Single(report.Checks, check => check.Code == "XD1002");
            Assert.Equal(missingSdk ? XamlNexusDoctorSeverity.Error : XamlNexusDoctorSeverity.Pass, sdk.Severity);
            Assert.Contains(version, sdk.Message);
            if (missingSdk) Assert.Contains("global.json", sdk.Message);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }
}
