using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Resources;
using XamlNexus.Tooling.CommandLine;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ProjectErrorLocalizationTests {
    [Theory]
    [InlineData(typeof(ProjectValidationErrors), typeof(ProjectValidationCodes))]
    [InlineData(typeof(ProjectUpgradeErrors), typeof(ProjectUpgradeCodes))]
    [InlineData(typeof(CliErrors), typeof(CliCodes))]
    public void EveryScenarioHasBothTranslationsAndRegisteredCode(Type scenarios, Type registry) {
        var definitions = scenarios.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (ErrorDefinition)field.GetValue(null)!).ToArray();
        var codes = registry.GetFields().Select(field => (string)field.GetRawConstantValue()!).ToArray();
        Assert.NotEmpty(definitions);
        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal(definitions.Length, definitions.Select(d => d.ResourceKey).Distinct().Count());
        var resources = new ResourceManager("XamlNexus.Common.Resources.Strings", typeof(ErrorDefinition).Assembly);
        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("zh-CN") }) {
            var set = resources.GetResourceSet(culture, true, false)!;
            foreach (var definition in definitions) {
                Assert.Contains(definition.Code, codes);
                string? template = set.GetString(definition.ResourceKey);
                Assert.False(string.IsNullOrWhiteSpace(template), definition.ResourceKey);
                Assert.Equal(definition.ArgumentCount, CompositeFormat.Parse(template!).MinimumArgumentCount);
                var args = Enumerable.Range(0, definition.ArgumentCount).Select(i => (object?)$"argument-{i}").ToArray();
                var result = definition.Format(culture, args);
                foreach (var argument in args) Assert.Contains((string)argument!, result);
            }
        }
    }

    [Fact]
    public void UpgradeExceptionKeepsEnglishMessageAndLocalizesAtPresentationBoundary() {
        var inner = new IOException("invalid XML");
        object?[] args = ["App.csproj"];
        var error = new XamlNexusProjectUpgradeException(ProjectUpgradeErrors.ResolvedXmlInvalid, args, inner);
        args[0] = "changed";
        Assert.Equal("XU2021", error.Code);
        Assert.Equal("XU2021: Resolved XML is invalid: App.csproj", error.Message);
        Assert.Equal("XU2021: 解决冲突后的 XML 无效：App.csproj", error.GetLocalizedMessage(CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Same(inner, error.InnerException);
        Assert.Throws<ArgumentException>(() => new XamlNexusProjectUpgradeException(ProjectUpgradeErrors.ResolvedXmlInvalid, []));
        var legacy = new XamlNexusProjectUpgradeException("XU9999", "custom");
        Assert.Equal(legacy.Message, legacy.GetLocalizedMessage(CultureInfo.GetCultureInfo("zh-CN")));
    }

    [Fact]
    public void SharedCodesRetainDifferentScenariosAndCliPreservesDetails() {
        Assert.Equal(ProjectValidationErrors.TrayWindowMissing.Code, ProjectValidationErrors.TrayWindowCodeMissing.Code);
        Assert.NotEqual(ProjectValidationErrors.TrayWindowMissing.ResourceKey, ProjectValidationErrors.TrayWindowCodeMissing.ResourceKey);
        Assert.Equal("XU2011", ProjectUpgradeErrors.TextMergeConflict.Code);
        Assert.Equal(ProjectUpgradeErrors.TextMergeConflict.Code, ProjectUpgradeErrors.XmlMergeConflict.Code);
        const string detail = "SDK {version} <unavailable>";
        Assert.Equal(detail, CliErrors.RunFailed.Format(CultureInfo.GetCultureInfo("zh-CN"), [detail]));
        Assert.Equal("XC1201", CliErrors.RunFailed.Code);
    }
}
