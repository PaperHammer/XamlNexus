using System.Text;

namespace XamlNexus.Common.Projects;

public enum XamlNexusMergeStatus {
    Merged,
    Conflict,
    Unsupported,
}

public sealed record XamlNexusTextMergeResult(
    XamlNexusMergeStatus Status,
    byte[]? Content);

public static class XamlNexusThreeWayTextMerge {
    private const int MaximumContentBytes = 1024 * 1024;
    private const long MaximumDiffCells = 4_000_000;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static XamlNexusTextMergeResult Merge(
        byte[] baseline,
        byte[] local,
        byte[] target) {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(target);

        if (local.AsSpan().SequenceEqual(baseline))
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Merged, target);
        if (target.AsSpan().SequenceEqual(baseline) || local.AsSpan().SequenceEqual(target))
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Merged, local);
        if (!TryDecode(baseline, out string? baselineText) ||
            !TryDecode(local, out string? localText) ||
            !TryDecode(target, out string? targetText)) {
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Unsupported, null);
        }

        IReadOnlyList<string> baselineLines = SplitLines(baselineText);
        IReadOnlyList<string> localLines = SplitLines(localText);
        IReadOnlyList<string> targetLines = SplitLines(targetText);
        if ((long)(baselineLines.Count + 1) * (localLines.Count + 1) > MaximumDiffCells ||
            (long)(baselineLines.Count + 1) * (targetLines.Count + 1) > MaximumDiffCells) {
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Unsupported, null);
        }

        IReadOnlyList<TextEdit> localEdits = CreateEdits(baselineLines, localLines);
        IReadOnlyList<TextEdit> targetEdits = CreateEdits(baselineLines, targetLines);
        var mergedEdits = new List<TextEdit>(localEdits);
        foreach (TextEdit targetEdit in targetEdits) {
            TextEdit? identical = mergedEdits.FirstOrDefault(localEdit => AreIdentical(localEdit, targetEdit));
            if (identical is not null) continue;
            if (mergedEdits.Any(localEdit => Overlaps(localEdit, targetEdit)))
                return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Conflict, null);
            mergedEdits.Add(targetEdit);
        }

        mergedEdits.Sort((left, right) => left.Start.CompareTo(right.Start));
        var merged = new StringBuilder();
        int cursor = 0;
        foreach (TextEdit edit in mergedEdits) {
            for (int index = cursor; index < edit.Start; index++)
                merged.Append(baselineLines[index]);
            foreach (string line in edit.Replacement)
                merged.Append(line);
            cursor = edit.Start + edit.DeleteCount;
        }
        for (int index = cursor; index < baselineLines.Count; index++)
            merged.Append(baselineLines[index]);

        return new XamlNexusTextMergeResult(
            XamlNexusMergeStatus.Merged,
            StrictUtf8.GetBytes(merged.ToString()));
    }

    public static string CreateConflictDocument(
        string relativePath,
        string fromVersion,
        string toVersion,
        byte[] baseline,
        byte[] local,
        byte[] target) {
        string baselineText = DecodeForConflictDocument(baseline);
        string localText = DecodeForConflictDocument(local);
        string targetText = DecodeForConflictDocument(target);
        return $"<<<<<<< LOCAL: {relativePath}{Environment.NewLine}" +
            EnsureTrailingNewline(localText) +
            $"||||||| BASE: XamlNexus {fromVersion}{Environment.NewLine}" +
            EnsureTrailingNewline(baselineText) +
            $"======={Environment.NewLine}" +
            EnsureTrailingNewline(targetText) +
            $">>>>>>> TARGET: XamlNexus {toVersion}{Environment.NewLine}";
    }

    private static bool TryDecode(byte[] content, out string text) {
        text = string.Empty;
        if (content.Length > MaximumContentBytes || content.Contains((byte)0)) return false;
        try {
            text = StrictUtf8.GetString(content);
            return true;
        }
        catch (DecoderFallbackException) {
            return false;
        }
    }

    private static string DecodeForConflictDocument(byte[] content) =>
        TryDecode(content, out string text)
            ? text
            : "[Binary or unsupported text content omitted.]";

    private static string EnsureTrailingNewline(string value) =>
        value.EndsWith('\n') || value.EndsWith('\r')
            ? value
            : value + Environment.NewLine;

    private static IReadOnlyList<string> SplitLines(string text) {
        var lines = new List<string>();
        int start = 0;
        for (int index = 0; index < text.Length; index++) {
            if (text[index] == '\r') {
                int length = index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
                lines.Add(text.Substring(start, index - start + length));
                index += length - 1;
                start = index + 1;
            }
            else if (text[index] == '\n') {
                lines.Add(text.Substring(start, index - start + 1));
                start = index + 1;
            }
        }
        if (start < text.Length) lines.Add(text[start..]);
        return lines;
    }

    private static IReadOnlyList<TextEdit> CreateEdits(
        IReadOnlyList<string> baseline,
        IReadOnlyList<string> variant) {
        var lengths = new int[baseline.Count + 1, variant.Count + 1];
        for (int baselineIndex = baseline.Count - 1; baselineIndex >= 0; baselineIndex--) {
            for (int variantIndex = variant.Count - 1; variantIndex >= 0; variantIndex--) {
                lengths[baselineIndex, variantIndex] = baseline[baselineIndex] == variant[variantIndex]
                    ? lengths[baselineIndex + 1, variantIndex + 1] + 1
                    : Math.Max(
                        lengths[baselineIndex + 1, variantIndex],
                        lengths[baselineIndex, variantIndex + 1]);
            }
        }

        var matches = new List<(int Baseline, int Variant)>();
        int left = 0;
        int right = 0;
        while (left < baseline.Count && right < variant.Count) {
            if (baseline[left] == variant[right]) {
                matches.Add((left++, right++));
            }
            else if (lengths[left + 1, right] >= lengths[left, right + 1]) {
                left++;
            }
            else {
                right++;
            }
        }

        var edits = new List<TextEdit>();
        int baselineCursor = 0;
        int variantCursor = 0;
        foreach ((int baselineMatch, int variantMatch) in matches.Append((baseline.Count, variant.Count))) {
            if (baselineCursor != baselineMatch || variantCursor != variantMatch) {
                edits.Add(new TextEdit(
                    baselineCursor,
                    baselineMatch - baselineCursor,
                    variant.Skip(variantCursor).Take(variantMatch - variantCursor).ToArray()));
            }
            baselineCursor = baselineMatch + 1;
            variantCursor = variantMatch + 1;
        }
        return edits;
    }

    private static bool AreIdentical(TextEdit left, TextEdit right) =>
        left.Start == right.Start &&
        left.DeleteCount == right.DeleteCount &&
        left.Replacement.SequenceEqual(right.Replacement, StringComparer.Ordinal);

    private static bool Overlaps(TextEdit left, TextEdit right) {
        if (left.DeleteCount == 0 && right.DeleteCount == 0)
            return left.Start == right.Start;
        if (left.DeleteCount == 0)
            return left.Start >= right.Start && left.Start <= right.Start + right.DeleteCount;
        if (right.DeleteCount == 0)
            return right.Start >= left.Start && right.Start <= left.Start + left.DeleteCount;
        return left.Start < right.Start + right.DeleteCount &&
            right.Start < left.Start + left.DeleteCount;
    }

    private sealed record TextEdit(
        int Start,
        int DeleteCount,
        IReadOnlyList<string> Replacement);
}
