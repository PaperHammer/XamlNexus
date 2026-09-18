using XamlNexus.Common.CommandLine;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class EnvironmentDoctorTests {
    [Fact]
    public void EnvironmentParserDoesNotRequireProject() {
        var result = CliParser.Parse(["doctor", "--environment", "--json"], Path.GetTempPath());
        Assert.True(result.Success);
        Assert.True(result.Options!.EnvironmentOnly);
        Assert.True(result.Options.JsonOutput);
        Assert.False(CliParser.Parse(["doctor", "--json"], Path.GetTempPath()).Options!.EnvironmentOnly);
    }

    [Theory]
    [InlineData("--environment", "--environment")]
    [InlineData("--environment", "App")]
    [InlineData("--environment", "--project", "App")]
    [InlineData("--environment", "--json", "--json")]
    [InlineData("--environment", "--dry-run")]
    public void InvalidEnvironmentOptionsAreRejected(params string[] args) {
        Assert.False(CliParser.Parse(["doctor", ..args], Path.GetTempPath()).Success);
    }

    [Fact]
    public void StandaloneProbeHonorsGlobalJsonWithoutWritingFiles() {
        string root = Directory.CreateTempSubdirectory("xn-environment-").FullName;
        try {
            File.WriteAllText(Path.Combine(root, "global.json"), """{"sdk":{"version":"99.0.100","rollForward":"disable"}}""");
            var before = Directory.GetFiles(root);
            var report = XamlNexusDoctor.DiagnoseEnvironment(root);
            Assert.Contains(report.Checks, c => c.Code == "XD1002" && c.Severity == XamlNexusDoctorSeverity.Error);
            Assert.DoesNotContain(report.Checks, c => c.Category == "Project");
            Assert.Equal(before, Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SdkProbeRequiresBothHeadersAndX64Libraries() {
        string root = Directory.CreateTempSubdirectory("xn-sdk-").FullName;
        try {
            Assert.Equal(XamlNexusDoctorSeverity.Warning, XamlNexusDoctor.InspectWindowsSdk(root).Severity);
            string include = Directory.CreateDirectory(Path.Combine(root, "Include", "10.0.26100.0", "um")).FullName;
            File.WriteAllText(Path.Combine(include, "Windows.h"), "");
            Assert.Equal(XamlNexusDoctorSeverity.Warning, XamlNexusDoctor.InspectWindowsSdk(root).Severity);
            string lib = Directory.CreateDirectory(Path.Combine(root, "Lib", "10.0.26100.0", "um", "x64")).FullName;
            File.WriteAllText(Path.Combine(lib, "kernel32.lib"), "");
            Assert.Equal(XamlNexusDoctorSeverity.Pass, XamlNexusDoctor.InspectWindowsSdk(root).Severity);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ProbeProcessTimeoutIsReported() {
        if (!OperatingSystem.IsWindows()) return;
        var result = ShellExecutor.Run("powershell.exe", "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30", Path.GetTempPath(), 150);
        Assert.False(result.Success);
        Assert.Contains("timed out", result.StandardError);
    }
}
