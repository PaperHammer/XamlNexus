using System.Text;
using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ThreeWayTextMergeTests {
    [Fact]
    public void Merge_NonOverlappingLineChangesCombinesBothSides() {
        XamlNexusTextMergeResult result = XamlNexusThreeWayTextMerge.Merge(
            Bytes("one\ntwo\nthree\n"),
            Bytes("ONE\ntwo\nthree\n"),
            Bytes("one\ntwo\nTHREE\n"));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        Assert.Equal("ONE\ntwo\nTHREE\n", Encoding.UTF8.GetString(result.Content!));
    }

    [Fact]
    public void Merge_OverlappingLineChangesReportsConflict() {
        XamlNexusTextMergeResult result = XamlNexusThreeWayTextMerge.Merge(
            Bytes("one\ntwo\n"),
            Bytes("one\nlocal\n"),
            Bytes("one\ntarget\n"));

        Assert.Equal(XamlNexusMergeStatus.Conflict, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Merge_InvalidUtf8ReportsUnsupported() {
        XamlNexusTextMergeResult result = XamlNexusThreeWayTextMerge.Merge(
            [0, 1, 2],
            [0, 1, 3],
            [0, 1, 4]);

        Assert.Equal(XamlNexusMergeStatus.Unsupported, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public void CreateConflictDocumentIncludesLocalBaseAndTargetSections() {
        string document = XamlNexusThreeWayTextMerge.CreateConflictDocument(
            "eng/sample.txt",
            "1.0.0",
            "2.0.0",
            Bytes("base"),
            Bytes("local"),
            Bytes("target"));

        Assert.Contains("<<<<<<< LOCAL: eng/sample.txt", document);
        Assert.Contains("||||||| BASE: XamlNexus 1.0.0", document);
        Assert.Contains("=======", document);
        Assert.Contains(">>>>>>> TARGET: XamlNexus 2.0.0", document);
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
