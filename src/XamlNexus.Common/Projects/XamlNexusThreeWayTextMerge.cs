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
    private const long MaximumDiffWork = 16_000_000;
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
        IReadOnlyList<TextEdit>? localEdits = CreateEdits(baselineLines, localLines);
        IReadOnlyList<TextEdit>? targetEdits = CreateEdits(baselineLines, targetLines);
        if (localEdits is null || targetEdits is null)
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Unsupported, null);
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
        byte[] target,
        bool wholeFile = false) {
        if (!wholeFile && TryCreatePartialConflictDocument(relativePath, fromVersion, toVersion, baseline, local, target, out string? partial))
            return partial;
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

    private static bool TryCreatePartialConflictDocument(
        string path, string fromVersion, string toVersion,
        byte[] baseline, byte[] local, byte[] target, out string document) {
        document = string.Empty;
        if (!TryDecode(baseline, out string baselineText) ||
            !TryDecode(local, out string localText) || !TryDecode(target, out string targetText)) return false;
        var lines = SplitLines(baselineText);
        var localLines = SplitLines(localText);
        var targetLines = SplitLines(targetText);
        var localEdits = CreateEdits(lines, localLines);
        var targetEdits = CreateEdits(lines, targetLines);
        if (localEdits is null || targetEdits is null) return false;

        var edits = localEdits.Select(edit => (Edit: edit, Local: true))
            .Concat(targetEdits.Select(edit => (Edit: edit, Local: false)))
            .OrderBy(item => item.Edit.Start).ToList();
        var output = new StringBuilder();
        int cursor = 0;
        bool hasConflict = false;
        while (edits.Count > 0) {
            var group = new List<(TextEdit Edit, bool Local)> { edits[0] };
            edits.RemoveAt(0);
            // Include transitive overlaps so each baseline range is emitted once.
            bool expanded;
            do {
                expanded = false;
                for (int index = edits.Count - 1; index >= 0; index--) {
                    if (!group.Any(item => Overlaps(item.Edit, edits[index].Edit))) continue;
                    group.Add(edits[index]);
                    edits.RemoveAt(index);
                    expanded = true;
                }
            } while (expanded);
            int start = group.Min(item => item.Edit.Start);
            int end = group.Max(item => item.Edit.Start + item.Edit.DeleteCount);
            for (; cursor < start; cursor++) output.Append(lines[cursor]);
            var left = group.Where(item => item.Local).Select(item => item.Edit).OrderBy(edit => edit.Start).ToArray();
            var right = group.Where(item => !item.Local).Select(item => item.Edit).OrderBy(edit => edit.Start).ToArray();
            string localPart = RenderRange(lines, start, end, left);
            string targetPart = RenderRange(lines, start, end, right);
            if (left.Length == 0) output.Append(targetPart);
            else if (right.Length == 0 || localPart == targetPart) output.Append(localPart);
            else {
                hasConflict = true;
                if (output.Length > 0 && output[^1] is not ('\n' or '\r')) output.AppendLine();
                output.Append(CreateConflictDocument(path, fromVersion, toVersion,
                    StrictUtf8.GetBytes(RenderRange(lines, start, end, [])),
                    StrictUtf8.GetBytes(localPart), StrictUtf8.GetBytes(targetPart), wholeFile: true));
            }
            cursor = end;
        }
        for (; cursor < lines.Count; cursor++) output.Append(lines[cursor]);
        document = output.ToString();
        // A structural conflict can exist even when the line edits agree.
        return hasConflict;
    }

    private static string RenderRange(IReadOnlyList<string> lines, int start, int end, IReadOnlyList<TextEdit> edits) {
        var result = new StringBuilder();
        int cursor = start;
        foreach (var edit in edits) {
            for (; cursor < edit.Start; cursor++) result.Append(lines[cursor]);
            foreach (string line in edit.Replacement) result.Append(line);
            cursor = edit.Start + edit.DeleteCount;
        }
        for (; cursor < end; cursor++) result.Append(lines[cursor]);
        return result.ToString();
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

    // Bidirectional Myers bisects the shortest edit path. The two frontiers
    // are reused across subproblems; no per-distance trace is retained.
    // A single work budget covers the entire diff, including all subproblems.
    internal static IReadOnlyList<TextEdit>? CreateEdits(
        IReadOnlyList<string> baseline,
        IReadOnlyList<string> variant) {
        var matches = new List<(int Baseline, int Variant)>();
        var pending = new Stack<(int Left, int LeftEnd, int Right, int RightEnd)>();
        pending.Push((0, baseline.Count, 0, variant.Count));
        int[]? forward = null;
        int[]? reverse = null;
        long work = 0;
        while (pending.TryPop(out var range)) {
            var (left, leftEnd, right, rightEnd) = range;
            while (left < leftEnd && right < rightEnd) {
                if (++work > MaximumDiffWork) return null;
                if (!StringComparer.Ordinal.Equals(baseline[left], variant[right])) break;
                matches.Add((left++, right++));
            }
            while (left < leftEnd && right < rightEnd) {
                if (++work > MaximumDiffWork) return null;
                if (!StringComparer.Ordinal.Equals(baseline[leftEnd - 1], variant[rightEnd - 1])) break;
                matches.Add((--leftEnd, --rightEnd));
            }
            if (left == leftEnd || right == rightEnd) continue;
            // Allocate once, only if a nontrivial search is needed. Processing
            // ranges iteratively avoids recursive stack growth on uneven splits.
            forward ??= new int[baseline.Count + variant.Count + 5];
            reverse ??= new int[forward.Length];
            var split = FindMiddleSplit(baseline, variant, left, leftEnd, right, rightEnd,
                forward, reverse, ref work);
            if (split is null) return null;
            var (x, y) = split.Value;
            if ((x == left && y == right) || (x == leftEnd && y == rightEnd))
                return null; // Never accept a non-progressing or partial diff.
            pending.Push((x, leftEnd, y, rightEnd));
            pending.Push((left, x, right, y));
        }
        matches.Sort((left, right) => left.Baseline.CompareTo(right.Baseline));

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

    private static (int Left, int Right)? FindMiddleSplit(
        IReadOnlyList<string> baseline, IReadOnlyList<string> variant,
        int left, int leftEnd, int right, int rightEnd,
        int[] forward, int[] reverse, ref long work) {
        int n = leftEnd - left;
        int m = rightEnd - right;
        int limit = (n + m + 1) / 2;
        int offset = limit + 1;
        int length = 2 * limit + 3;
        Array.Fill(forward, -1, 0, length);
        Array.Fill(reverse, -1, 0, length);
        forward[offset + 1] = reverse[offset + 1] = 0;
        int delta = n - m;
        bool odd = (delta & 1) != 0;
        int forwardStart = 0, forwardEnd = 0, reverseStart = 0, reverseEnd = 0;
        for (int distance = 0; distance <= limit; distance++) {
            for (int k = -distance + forwardStart; k <= distance - forwardEnd; k += 2) {
                if (++work > MaximumDiffWork) return null;
                int index = offset + k;
                // On equal frontiers choose deletion, consistently in both scans.
                int x = k == -distance || (k != distance && forward[index - 1] < forward[index + 1])
                    ? forward[index + 1] : forward[index - 1] + 1;
                int y = x - k;
                while (x < n && y < m) {
                    if (++work > MaximumDiffWork) return null;
                    if (!StringComparer.Ordinal.Equals(baseline[left + x], variant[right + y])) break;
                    x++;
                    y++;
                }
                forward[index] = x;
                if (x > n) forwardEnd += 2;
                else if (y > m) forwardStart += 2;
                else if (odd) {
                    int other = offset + delta - k;
                    if (other >= 0 && other < length && reverse[other] >= 0 && x >= n - reverse[other])
                        return (left + x, right + y);
                }
            }
            for (int k = -distance + reverseStart; k <= distance - reverseEnd; k += 2) {
                if (++work > MaximumDiffWork) return null;
                int index = offset + k;
                int x = k == -distance || (k != distance && reverse[index - 1] < reverse[index + 1])
                    ? reverse[index + 1] : reverse[index - 1] + 1;
                int y = x - k;
                while (x < n && y < m) {
                    if (++work > MaximumDiffWork) return null;
                    if (!StringComparer.Ordinal.Equals(baseline[leftEnd - x - 1], variant[rightEnd - y - 1])) break;
                    x++;
                    y++;
                }
                reverse[index] = x;
                if (x > n) reverseEnd += 2;
                else if (y > m) reverseStart += 2;
                else if (!odd) {
                    int other = offset + delta - k;
                    if (other >= 0 && other < length && forward[other] >= 0 && forward[other] >= n - x) {
                        int forwardX = forward[other];
                        return (left + forwardX, right + forwardX - (delta - k));
                    }
                }
            }
        }
        return null;
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

    internal sealed record TextEdit(
        int Start,
        int DeleteCount,
        IReadOnlyList<string> Replacement);
}
