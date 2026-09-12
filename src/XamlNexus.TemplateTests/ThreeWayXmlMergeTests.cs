using System.Text;
using System.Xml.Linq;
using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ThreeWayXmlMergeTests {
    [Fact]
    public void Merge_IndependentAttributesOnSameElementCombinesBothSides() {
        XamlNexusXmlMergeResult result = XamlNexusThreeWayXmlMerge.Merge(
            Bytes("<Project><ItemGroup><PackageReference Include=\"Sample\" Version=\"1.0\" /></ItemGroup></Project>"),
            Bytes("<Project><ItemGroup><PackageReference Include=\"Sample\" Version=\"1.0\" PrivateAssets=\"all\" /></ItemGroup></Project>"),
            Bytes("<Project><ItemGroup><PackageReference Include=\"Sample\" Version=\"2.0\" /></ItemGroup></Project>"));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        XElement package = Assert.Single(Parse(result.Content!).Descendants("PackageReference"));
        Assert.Equal("2.0", (string?)package.Attribute("Version"));
        Assert.Equal("all", (string?)package.Attribute("PrivateAssets"));
    }

    [Fact]
    public void Merge_IndependentKeyedChildAdditionsPreservesBothSides() {
        XamlNexusXmlMergeResult result = XamlNexusThreeWayXmlMerge.Merge(
            Bytes("<Project><ItemGroup /></Project>"),
            Bytes("<Project><ItemGroup><PackageReference Include=\"Local\" /></ItemGroup></Project>"),
            Bytes("<Project><ItemGroup><PackageReference Include=\"Target\" /></ItemGroup></Project>"));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        string[] packages = Parse(result.Content!)
            .Descendants("PackageReference")
            .Select(element => (string)element.Attribute("Include")!)
            .ToArray();
        Assert.Equal(["Local", "Target"], packages);
    }

    [Fact]
    public void Merge_ConflictingAttributeValueReportsConflict() {
        XamlNexusXmlMergeResult result = XamlNexusThreeWayXmlMerge.Merge(
            Bytes("<Project Version=\"1\" />"),
            Bytes("<Project Version=\"local\" />"),
            Bytes("<Project Version=\"target\" />"));

        Assert.Equal(XamlNexusMergeStatus.Conflict, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Merge_XamlAttributesPreservesNamespaceAndBothChanges() {
        const string xmlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XamlNexusXmlMergeResult result = XamlNexusThreeWayXmlMerge.Merge(
            Bytes($"<Page xmlns=\"{xmlNamespace}\"><Grid Background=\"White\" /></Page>"),
            Bytes($"<Page xmlns=\"{xmlNamespace}\"><Grid Background=\"White\" Opacity=\"0.8\" /></Page>"),
            Bytes($"<Page xmlns=\"{xmlNamespace}\"><Grid Background=\"Black\" /></Page>"));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        XDocument document = Parse(result.Content!);
        XElement grid = Assert.Single(document.Descendants(XName.Get("Grid", xmlNamespace)));
        Assert.Equal("Black", (string?)grid.Attribute("Background"));
        Assert.Equal("0.8", (string?)grid.Attribute("Opacity"));
    }

    [Fact]
    public void Merge_CommentedDocumentRemainsUnsupportedRatherThanDroppingComment() {
        XamlNexusXmlMergeResult result = XamlNexusThreeWayXmlMerge.Merge(
            Bytes("<Project><!-- keep --><Value>one</Value></Project>"),
            Bytes("<Project><!-- local --><Value>one</Value></Project>"),
            Bytes("<Project><!-- keep --><Value>two</Value></Project>"));

        Assert.Equal(XamlNexusMergeStatus.Unsupported, result.Status);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData("AB", "AB", "BA", "BA")]
    [InlineData("AB", "BA", "AB", "BA")]
    [InlineData("ABC", "CAB", "CAB", "CAB")]
    [InlineData("AB", "AB", "XAB", "XAB")]
    [InlineData("AB", "AB", "AXB", "AXB")]
    [InlineData("AB", "AB", "ABX", "ABX")]
    [InlineData("AB", "AXB", "AB", "AXB")]
    [InlineData("AB", "AXB", "AYB", "AXYB")]
    [InlineData("AB", "AXYB", "AB", "AXYB")]
    [InlineData("ABC", "AC", "AXBC", "AXC")]
    [InlineData("ABC", "AC", "CA", "CA")]
    public void Merge_ChildOrderPreservesReordersAndInsertionPositions(
        string baseline, string local, string target, string expected) {
        XamlNexusXmlMergeResult result = MergeOrderedChildren(baseline, local, target);

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        XElement root = Parse(result.Content!).Root!;
        Assert.Equal(expected, string.Concat(root.Elements().Select(child => (string?)child.Attribute("Name"))));
        Assert.Equal("Custom", (string?)root.Elements().Single(child => (string?)child.Attribute("Name") == "A").Attribute("Text"));
        Assert.Equal("8", (string?)root.Attribute("Spacing"));
    }

    [Theory]
    [InlineData("ABC", "BAC", "ACB")]
    [InlineData("AB", "AXB", "BA")]
    [InlineData("AB", "XAB", "AXB")]
    [InlineData("AB", "AXYB", "AYXB")]
    public void Merge_IncompatibleChildOrdersReportConflict(string baseline, string local, string target) {
        XamlNexusXmlMergeResult result = MergeOrderedChildren(baseline, local, target);

        Assert.Equal(XamlNexusMergeStatus.Conflict, result.Status);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData("BA", false)]
    [InlineData("BA", true)]
    [InlineData("XAB", false)]
    [InlineData("XAB", true)]
    [InlineData("B", false)]
    [InlineData("B", true)]
    public void Merge_UnnamedRepeatedChildrenDoNotMatchByPosition(string targetOrder, bool reverseSides) {
        string Document(string order, bool customized = false) => new XElement("StackPanel",
            order.Select(text => new XElement("TextBlock", new XAttribute("Text", text),
                customized && text == 'A' ? new XAttribute("FontSize", "24") : null)))
            .ToString(SaveOptions.DisableFormatting);
        byte[] local = Bytes(Document("AB", customized: true));
        byte[] target = Bytes(Document(targetOrder));
        var result = XamlNexusThreeWayXmlMerge.Merge(Bytes(Document("AB")),
            reverseSides ? target : local, reverseSides ? local : target);

        Assert.Equal(XamlNexusMergeStatus.Conflict, result.Status);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_UnnamedChildrenAllowWholeSequenceChangesWithParentEdits(bool reverseSides) {
        byte[] baseline = Bytes("<StackPanel><TextBlock Text=\"A\"/><TextBlock Text=\"B\"/></StackPanel>");
        byte[] local = Bytes("<StackPanel Spacing=\"8\"><TextBlock Text=\"A\"/><TextBlock Text=\"B\"/></StackPanel>");
        byte[] target = Bytes("<StackPanel><TextBlock Text=\"B\"/><TextBlock Text=\"A\"/></StackPanel>");
        var result = XamlNexusThreeWayXmlMerge.Merge(baseline,
            reverseSides ? target : local, reverseSides ? local : target);

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        XElement root = Parse(result.Content!).Root!;
        Assert.Equal("8", (string?)root.Attribute("Spacing"));
        Assert.Equal(new[] { "B", "A" }, root.Elements().Select(child => (string?)child.Attribute("Text")));
    }

    [Fact]
    public void Merge_UnchangedRepeatedChildrenAllowIndependentSiblingEdits() {
        string Document(string width, string height) => $"<Grid><TextBlock Text=\"A\"/><TextBlock Text=\"B\"/><Border Width=\"{width}\" Height=\"{height}\"/></Grid>";
        var result = XamlNexusThreeWayXmlMerge.Merge(Bytes(Document("1", "1")),
            Bytes(Document("2", "1")), Bytes(Document("1", "2")));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        XElement root = Parse(result.Content!).Root!;
        Assert.Equal(new[] { "A", "B" }, root.Elements("TextBlock").Select(child => (string?)child.Attribute("Text")));
        Assert.Equal("2", (string?)root.Element("Border")!.Attribute("Width"));
        Assert.Equal("2", (string?)root.Element("Border")!.Attribute("Height"));
    }

    private static XamlNexusXmlMergeResult MergeOrderedChildren(string baseline, string local, string target) {
        byte[] Document(string order, bool customized = false, bool updated = false) => Bytes(
            new XElement("StackPanel",
                new XAttribute("Spacing", updated ? "8" : "0"),
                order.Select(name => new XElement("TextBlock",
                    new XAttribute("Name", name),
                    new XAttribute("Text", customized && name == 'A' ? "Custom" : name.ToString()))))
                .ToString(SaveOptions.DisableFormatting));
        return XamlNexusThreeWayXmlMerge.Merge(Document(baseline), Document(local, customized: true), Document(target, updated: true));
    }

    private static XDocument Parse(byte[] content) =>
        XDocument.Parse(Encoding.UTF8.GetString(content));

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
