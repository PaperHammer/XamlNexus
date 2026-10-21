using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using XamlNexus.Common.Resources;
using XamlNexus.Tooling.Diagnostics;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class DoctorLocalizationTests {
    [Fact]
    public void EveryScenarioHasExplicitTranslationsAndMatchingArguments() {
        var resources = new ResourceManager("XamlNexus.Common.Resources.Strings", typeof(LocalizedMessageFormatter).Assembly);
        var definitions = typeof(DoctorChecks).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (DoctorCheckDefinition)field.GetValue(null)!).ToArray();
        Assert.NotEmpty(definitions);
        Assert.Equal(definitions.Length, definitions.Select(d => d.ResourceKey).Distinct().Count());
        var codes = typeof(DoctorCodes).GetFields().Select(field => (string)field.GetRawConstantValue()!).ToArray();
        Assert.Equal(codes.Length, codes.Distinct().Count());
        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("zh-CN") }) {
            var resourceSet = resources.GetResourceSet(culture, true, false)!;
            foreach (var definition in definitions) {
                Assert.Contains(definition.Code, codes);
                string? template = resourceSet.GetString(definition.ResourceKey);
                Assert.False(string.IsNullOrWhiteSpace(template), definition.ResourceKey);
                Assert.Equal(definition.ArgumentCount, CompositeFormat.Parse(template!).MinimumArgumentCount);
                var args = Enumerable.Range(0, definition.ArgumentCount).Select(i => (object?)$"argument-{i}").ToArray();
                string result = definition.Format(culture, args);
                foreach (var argument in args) Assert.Contains((string)argument!, result);
            }
        }
    }

    [Fact]
    public void SharedAppSdkCodeKeepsDistinctOutcomesAndTranslations() {
        var missing = DoctorChecks.AppSdkMissing.Create();
        var present = DoctorChecks.AppSdkReferenced.Create(["1.8"]);
        var multiple = DoctorChecks.AppSdkVersionsDiffer.Create(["1.7, 1.8"]);
        Assert.All(new[] { missing, present, multiple }, check => {
            Assert.Equal("XD3003", check.Code);
            Assert.Equal("Windows App SDK", check.Category);
        });
        Assert.Equal(XamlNexusDoctorSeverity.Error, missing.Severity);
        Assert.Equal(XamlNexusDoctorSeverity.Pass, present.Severity);
        Assert.Equal(XamlNexusDoctorSeverity.Warning, multiple.Severity);
        Assert.Equal("已引用 Microsoft.WindowsAppSDK 1.8。",
            DoctorChecks.AppSdkReferenced.Format(CultureInfo.GetCultureInfo("zh-CN"), ["1.8"]));
        Assert.Equal("Microsoft.WindowsAppSDK 1.8 is referenced.",
            DoctorChecks.AppSdkReferenced.Format(CultureInfo.GetCultureInfo("en-US"), ["1.8"]));
    }

    [Fact]
    public void FormattingPreservesPathAndExceptionTextAndRejectsWrongArity() {
        const string detail = "invalid <xml> {value}";
        var check = DoctorChecks.ProjectXmlUnreadable.Create([detail], path: "App/App.csproj");
        Assert.Equal("XD3001", check.Code);
        Assert.Equal(XamlNexusDoctorSeverity.Warning, check.Severity);
        Assert.Equal("App/App.csproj", check.RelativePath);
        Assert.Contains(detail, check.Message);
        Assert.Throws<ArgumentException>(() => DoctorChecks.AppSdkReferenced.Create());
    }
}
