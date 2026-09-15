using System.Globalization;
using System.Resources;
using System.Text;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using Xunit;

namespace XamlNexus.TemplateTests;

public class SlnxAndResourceTests {
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void XmlMergeAcceptsBomAndPreservesLocalEncoding(bool baselineBom, bool localBom, bool targetBom) {
        static byte[] Encode(string text, bool bom) => (bom ? new byte[] { 0xEF, 0xBB, 0xBF } : [])
            .Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        var result = XamlNexusThreeWayXmlMerge.Merge(
            Encode("<Page><Binding Path='Old' Mode='OneWay'/></Page>", baselineBom),
            Encode("<Page><Binding Path='New' Mode='OneWay'/></Page>", localBom),
            Encode("<Page><Binding Path='Old' Mode='TwoWay'/></Page>", targetBom));
        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        Assert.Equal(localBom, result.Content!.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        using var stream = new MemoryStream(result.Content!);
        var binding = System.Xml.Linq.XDocument.Load(stream).Root!.Element("Binding")!;
        Assert.Equal("New", (string?)binding.Attribute("Path"));
        Assert.Equal("TwoWay", (string?)binding.Attribute("Mode"));
    }

    [Fact]
    public void XamlBindingPathAndModeChangesMergeIndependently() {
        var result = XamlNexusThreeWayXmlMerge.Merge(
            Encoding.UTF8.GetBytes("<Page><Binding Path='Old' Mode='OneWay'/></Page>"),
            Encoding.UTF8.GetBytes("<Page><Binding Path='New' Mode='OneWay'/></Page>"),
            Encoding.UTF8.GetBytes("<Page><Binding Path='Old' Mode='TwoWay'/></Page>"));
        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        var binding = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(result.Content!)).Root!.Element("Binding")!;
        Assert.Equal("New", (string?)binding.Attribute("Path"));
        Assert.Equal("TwoWay", (string?)binding.Attribute("Mode"));
    }

    [Fact]
    public void ResourceSetsContainSameKeysAndPreserveFormatting() {
        var manager = new ResourceManager("XamlNexus.Common.Resources.Strings", typeof(LanguageRegistry).Assembly);
        var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;
        var chinese = manager.GetResourceSet(CultureInfo.GetCultureInfo("zh-CN"), true, false)!;
        var en = english.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
        var zh = chinese.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
        // 帮助统一使用英文；中文资源省略该键，由 ResourceManager 回退到默认资源。
        Assert.Equal(en.Where(key => key != "Cli_Help"), zh);
        Assert.Equal(manager.GetString("Cli_Help", CultureInfo.GetCultureInfo("en")),
            manager.GetString("Cli_Help", CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Equal("Solution Name", manager.GetString("SlnName", CultureInfo.GetCultureInfo("en")));
        Assert.Equal("项目名称", manager.GetString("SlnName", CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Contains("{0}", manager.GetString("Creation_Run", CultureInfo.GetCultureInfo("zh-CN"))!);
    }

    [Fact]
    public void SlnxMergePreservesIndependentProjectAdditions() {
        var result = XamlNexusThreeWayXmlMerge.Merge(
            Encoding.UTF8.GetBytes("<Solution><Project Path='App.csproj'/></Solution>"),
            Encoding.UTF8.GetBytes("<Solution><Project Path='App.csproj'/><Project Path='User.csproj'/></Solution>"),
            Encoding.UTF8.GetBytes("<Solution><Project Path='App.csproj'/><Project Path='New.csproj'/></Solution>"));
        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        Assert.Contains("User.csproj", Encoding.UTF8.GetString(result.Content!));
        Assert.Contains("New.csproj", Encoding.UTF8.GetString(result.Content!));
    }
}
