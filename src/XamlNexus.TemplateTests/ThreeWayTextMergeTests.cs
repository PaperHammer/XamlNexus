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

    [Fact]
    public void ConflictDocument_PreservesIndependentEditsAroundMultipleConflicts() {
        string document = XamlNexusThreeWayTextMerge.CreateConflictDocument("file.txt", "1", "2",
            Bytes("head\nkeep\nfirst\nmiddle\nsecond\nkeep2\ntail\n"),
            Bytes("HEAD\nkeep\nlocal1\nmiddle\nlocal2\nkeep2\ntail\n"),
            Bytes("head\nkeep\ntarget1\nmiddle\ntarget2\nkeep2\nTAIL\n"));
        Assert.StartsWith("HEAD\nkeep\n<<<<<<< LOCAL", document);
        Assert.EndsWith("keep2\nTAIL\n", document);
        Assert.Equal(2, document.Split("<<<<<<< LOCAL").Length - 1);
        Assert.Contains("middle\n<<<<<<< LOCAL", document);
        Assert.DoesNotContain("head\n", document);
        Assert.DoesNotContain("tail\n", document);
    }

    [Fact]
    public void ConflictDocument_TransitiveOverlapsIncludeEachSideOnce() {
        string document = XamlNexusThreeWayTextMerge.CreateConflictDocument("file.txt", "1", "2",
            Bytes("start\na\nb\nc\nend\n"), Bytes("start\nA\nb\nC\nend\n"),
            Bytes("start\nreplacement\nend\n"));
        Assert.Equal(1, document.Split("<<<<<<< LOCAL").Length - 1);
        Assert.Contains("A\nb\nC\n", document);
        Assert.Contains("a\nb\nc\n", document);
        Assert.StartsWith("start\n", document);
        Assert.EndsWith("end\n", document);
    }

    [Fact]
    public void ConflictDocument_StructuralFallbackIncludesWholeInputs() {
        string document = XamlNexusThreeWayTextMerge.CreateConflictDocument("file.xml", "1", "2",
            Bytes("a\nb\n"), Bytes("A\nb\n"), Bytes("a\nB\n"), wholeFile: true);
        Assert.StartsWith("<<<<<<< LOCAL", document);
        Assert.Contains("A\nb\n", document);
        Assert.Contains("a\nB\n", document);
    }

    [Fact]
    public void MyersEdits_ReconstructVariantsWithOptimalEditCounts() {
        var random = new Random(713);
        for (int sample = 0; sample < 1000; sample++) {
            string[] baseline = Enumerable.Range(0, random.Next(25)).Select(_ => random.Next(4).ToString()).ToArray();
            string[] variant = Enumerable.Range(0, random.Next(25)).Select(_ => random.Next(4).ToString()).ToArray();
            var edits = XamlNexusThreeWayTextMerge.CreateEdits(baseline, variant);
            Assert.NotNull(edits);
            var actual = new List<string>();
            int cursor = 0;
            foreach (var edit in edits) {
                Assert.InRange(edit.Start, cursor, baseline.Length);
                actual.AddRange(baseline.Skip(cursor).Take(edit.Start - cursor));
                actual.AddRange(edit.Replacement);
                cursor = edit.Start + edit.DeleteCount;
                Assert.InRange(cursor, 0, baseline.Length);
            }
            actual.AddRange(baseline.Skip(cursor));
            Assert.Equal(variant, actual);

            // Independent small-input LCS oracle checks minimality, without
            // requiring the same alignment when duplicate lines have ties.
            var lcs = new int[baseline.Length + 1, variant.Length + 1];
            for (int x = 1; x <= baseline.Length; x++)
                for (int y = 1; y <= variant.Length; y++)
                    lcs[x, y] = baseline[x - 1] == variant[y - 1]
                        ? lcs[x - 1, y - 1] + 1 : Math.Max(lcs[x - 1, y], lcs[x, y - 1]);
            Assert.Equal(baseline.Length + variant.Length - 2 * lcs[baseline.Length, variant.Length],
                edits.Sum(edit => edit.DeleteCount + edit.Replacement.Count));
        }
    }

    [Fact]
    public void MyersEdits_LargeReplacementUsesBoundedLinearStorage() {
        string[] baseline = Enumerable.Repeat("old", 1500).ToArray();
        string[] variant = Enumerable.Repeat("new", 1500).ToArray();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var edits = XamlNexusThreeWayTextMerge.CreateEdits(baseline, variant);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var edit = Assert.Single(edits!);
        Assert.Equal(0, edit.Start);
        Assert.Equal(1500, edit.DeleteCount);
        Assert.Equal(variant, edit.Replacement);
        // The old trace needed over four million int cells for this distance.
        Assert.InRange(allocated, 0, 1_000_000);
    }

    [Fact]
    public void MyersEdits_EquivalentSwapPrefersDeletionFirst() {
        var edits = XamlNexusThreeWayTextMerge.CreateEdits(["a", "b"], ["b", "a"]);
        Assert.NotNull(edits);
        Assert.Equal(2, edits.Count);
        Assert.Equal(0, edits[0].Start);
        Assert.Equal(1, edits[0].DeleteCount);
        Assert.Empty(edits[0].Replacement);
        Assert.Equal(2, edits[1].Start);
        Assert.Equal(["a"], edits[1].Replacement);
    }

    [Fact]
    public void MyersEdits_EmptySideNeedsNoSearch() {
        string[] lines = Enumerable.Repeat("line\n", 20_000).ToArray();
        var insertion = Assert.Single(XamlNexusThreeWayTextMerge.CreateEdits([], lines)!);
        Assert.Equal(0, insertion.Start);
        Assert.Equal(0, insertion.DeleteCount);
        Assert.Equal(lines, insertion.Replacement);
        var deletion = Assert.Single(XamlNexusThreeWayTextMerge.CreateEdits(lines, [])!);
        Assert.Equal(0, deletion.Start);
        Assert.Equal(lines.Length, deletion.DeleteCount);
        Assert.Empty(deletion.Replacement);
    }

    [Fact]
    public void Merge_TwentyThousandLinesWithSparseChanges() {
        string[] baseline = Enumerable.Range(0, 20_000).Select(index => $"line {index}\n").ToArray();
        string[] local = (string[])baseline.Clone();
        string[] target = (string[])baseline.Clone();
        local[10] = "local\n";
        target[^10] = "target\n";
        var result = XamlNexusThreeWayTextMerge.Merge(
            Bytes(string.Concat(baseline)), Bytes(string.Concat(local)), Bytes(string.Concat(target)));
        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        local[^10] = target[^10];
        Assert.Equal(string.Concat(local), Encoding.UTF8.GetString(result.Content!));
    }

    [Fact]
    public void Merge_DiffBudgetExhaustionIsUnsupportedAndExportsWholeInputs() {
        string baseline = string.Concat(Enumerable.Repeat("base\n", 3000));
        string local = string.Concat(Enumerable.Repeat("local\n", 3000));
        string target = string.Concat(Enumerable.Repeat("target\n", 3000));
        var result = XamlNexusThreeWayTextMerge.Merge(Bytes(baseline), Bytes(local), Bytes(target));
        Assert.Equal(XamlNexusMergeStatus.Unsupported, result.Status);
        Assert.Null(result.Content);
        string document = XamlNexusThreeWayTextMerge.CreateConflictDocument("file", "1", "2",
            Bytes(baseline), Bytes(local), Bytes(target));
        Assert.Contains(baseline, document);
        Assert.Contains(local, document);
        Assert.Contains(target, document);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Merge_PreservesLineEndingsAndUnterminatedLastLine(string newline) {
        var result = XamlNexusThreeWayTextMerge.Merge(Bytes($"a{newline}b{newline}c"),
            Bytes($"A{newline}b{newline}c"), Bytes($"a{newline}b{newline}C"));
        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        Assert.Equal($"A{newline}b{newline}C", Encoding.UTF8.GetString(result.Content!));
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
