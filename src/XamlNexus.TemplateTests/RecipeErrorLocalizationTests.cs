using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using XamlNexus.Common.Recipes;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class RecipeErrorLocalizationTests {
    [Fact]
    public void EveryScenarioHasBothTranslationsAndMatchingArguments() {
        var resources = new ResourceManager("XamlNexus.Common.Resources.Strings", typeof(RecipeErrorDefinition).Assembly);
        var definitions = typeof(XamlNexusRecipeErrors).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (RecipeErrorDefinition)field.GetValue(null)!).ToArray();
        Assert.Equal(definitions.Length, definitions.Select(d => d.ResourceKey).Distinct().Count());
        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("zh-CN") }) {
            var resourceSet = resources.GetResourceSet(culture, true, false)!;
            foreach (var definition in definitions) {
                string? template = resourceSet.GetString(definition.ResourceKey);
                Assert.False(string.IsNullOrWhiteSpace(template), definition.ResourceKey);
                Assert.Equal(definition.ArgumentCount, CompositeFormat.Parse(template!).MinimumArgumentCount);
                var args = Enumerable.Range(0, definition.ArgumentCount).Select(i => (object?)$"argument-{i}").ToArray();
                string result = definition.Format(culture, args);
                foreach (var arg in args) Assert.Contains((string)arg!, result);
            }
        }
    }

    [Fact]
    public void SharedCodeUsesScenarioSpecificMessage() {
        var xml = new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidSlnxXml, []);
        var root = new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingSolutionRoot, []);
        Assert.Equal("XR1230", xml.Code);
        Assert.Equal(xml.Code, root.Code);
        Assert.NotEqual(xml.ResourceKey, root.ResourceKey);
        Assert.Equal("XR1230: SLNX XML 无效", xml.GetLocalizedMessage(CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Equal("XR1230: SLNX 必须包含 Solution 根元素", root.GetLocalizedMessage(CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Equal("XR1230: Invalid SLNX XML.", xml.Message);
    }

    [Fact]
    public void StructuredExceptionPreservesParametersAndInnerException() {
        var inner = new IOException("disk failure");
        object?[] args = ["Demo", 2];
        var error = new XamlNexusRecipeException(XamlNexusRecipeErrors.TransactionRollbackFailed, args, inner);
        args[0] = "changed";
        Assert.Same(inner, error.InnerException);
        Assert.Equal("Demo", error.Arguments[0]);
        Assert.Equal("XR1303: Recipe 'Demo' failed. Rollback encountered 2 additional error(s).", error.Message);
        Assert.Equal("XR1303: Recipe“Demo”执行失败，回滚时发生 2 个额外错误", error.GetLocalizedMessage(CultureInfo.GetCultureInfo("zh-CN")));
    }

    [Fact]
    public void LegacyConstructorKeepsCustomMessage() {
        var inner = new IOException("original");
        var error = new XamlNexusRecipeException("XR9999", "Custom message", inner);
        Assert.Equal("XR9999: Custom message", error.GetLocalizedMessage(CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Null(error.ResourceKey);
        Assert.Same(inner, error.InnerException);
    }

    [Fact]
    public void WrongArgumentCountIsRejected() => Assert.Throws<ArgumentException>(() =>
        new XamlNexusRecipeException(XamlNexusRecipeErrors.AlreadyInstalled, []));
}
