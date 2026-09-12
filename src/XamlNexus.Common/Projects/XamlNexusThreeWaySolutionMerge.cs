using System.Text;
using System.Text.RegularExpressions;

namespace XamlNexus.Common.Projects;

public sealed record XamlNexusSolutionMergeResult(
    XamlNexusMergeStatus Status,
    byte[]? Content);

public static partial class XamlNexusThreeWaySolutionMerge {
    private const int MaximumContentBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static XamlNexusSolutionMergeResult Merge(
        byte[] baseline,
        byte[] local,
        byte[] target) {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(target);
        if (local.AsSpan().SequenceEqual(baseline)) return Merged(target);
        if (target.AsSpan().SequenceEqual(baseline) || local.AsSpan().SequenceEqual(target))
            return Merged(local);
        if (!TryParse(baseline, out SolutionDocument? baselineDocument) ||
            !TryParse(local, out SolutionDocument? localDocument) ||
            !TryParse(target, out SolutionDocument? targetDocument)) {
            return Unsupported();
        }

        if (!TryMergeLines(
                baselineDocument!.Preamble,
                localDocument!.Preamble,
                targetDocument!.Preamble,
                out IReadOnlyList<string>? preamble) ||
            !TryMergeScalar(
                baselineDocument.GlobalLine,
                localDocument.GlobalLine,
                targetDocument.GlobalLine,
                out string? globalLine) ||
            !TryMergeScalar(
                baselineDocument.EndGlobalLine,
                localDocument.EndGlobalLine,
                targetDocument.EndGlobalLine,
                out string? endGlobalLine) ||
            !TryMergeEntries(
                baselineDocument.Projects,
                localDocument.Projects,
                targetDocument.Projects,
                MergeExactEntry,
                out IReadOnlyList<SolutionEntry>? projects) ||
            !TryMergeEntries(
                baselineDocument.Sections,
                localDocument.Sections,
                targetDocument.Sections,
                MergeSection,
                out IReadOnlyList<SolutionEntry>? sections)) {
            return Conflict();
        }

        var lines = new List<string>(preamble!);
        foreach (SolutionEntry project in projects!) lines.AddRange(project.Lines);
        lines.Add(globalLine!);
        foreach (SolutionEntry section in sections!) lines.AddRange(section.Lines);
        lines.Add(endGlobalLine!);
        string text = string.Join(localDocument.NewLine, lines);
        if (localDocument.HasTrailingNewLine) text += localDocument.NewLine;
        return Merged(StrictUtf8.GetBytes(text));
    }

    private static bool TryParse(byte[] content, out SolutionDocument? document) {
        document = null;
        if (content.Length > MaximumContentBytes || content.Contains((byte)0)) return false;
        string text;
        try {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException) {
            return false;
        }

        string newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (text.Contains('\r') && newLine == "\n") return false;
        bool trailingNewLine = text.EndsWith(newLine, StringComparison.Ordinal);
        string[] lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        if (trailingNewLine) lines = lines[..^1];
        int globalIndex = Array.FindIndex(lines, line => line.Trim().Equals("Global", StringComparison.Ordinal));
        if (globalIndex < 0 || !lines[^1].Trim().Equals("EndGlobal", StringComparison.Ordinal)) return false;

        var preamble = new List<string>();
        var projects = new List<SolutionEntry>();
        bool projectsStarted = false;
        for (int index = 0; index < globalIndex;) {
            if (!lines[index].TrimStart().StartsWith("Project(\"", StringComparison.Ordinal)) {
                if (projectsStarted) return false;
                preamble.Add(lines[index++]);
                continue;
            }
            projectsStarted = true;
            int end = FindTerminator(lines, index + 1, globalIndex, "EndProject");
            if (end < 0 || !TryProjectKey(lines[index], out string? key)) return false;
            projects.Add(new SolutionEntry(key!, lines[index..(end + 1)]));
            index = end + 1;
        }

        var sections = new List<SolutionEntry>();
        for (int index = globalIndex + 1; index < lines.Length - 1;) {
            Match header = SectionHeaderPattern().Match(lines[index]);
            if (!header.Success) return false;
            int end = FindTerminator(lines, index + 1, lines.Length - 1, "EndGlobalSection");
            if (end < 0) return false;
            sections.Add(new SolutionEntry(
                header.Groups["name"].Value,
                lines[index..(end + 1)]));
            index = end + 1;
        }
        if (HasDuplicateKeys(projects) || HasDuplicateKeys(sections)) return false;
        document = new SolutionDocument(
            preamble,
            projects,
            lines[globalIndex],
            sections,
            lines[^1],
            newLine,
            trailingNewLine);
        return true;
    }

    private static int FindTerminator(
        IReadOnlyList<string> lines,
        int start,
        int limit,
        string terminator) {
        for (int index = start; index < limit; index++) {
            if (lines[index].Trim().Equals(terminator, StringComparison.Ordinal)) return index;
        }
        return -1;
    }

    private static bool TryProjectKey(string line, out string? key) {
        Match match = ProjectLinePattern().Match(line);
        if (!match.Success) {
            key = null;
            return false;
        }
        string type = match.Groups["type"].Value.ToUpperInvariant();
        string path = match.Groups["path"].Value.Replace('/', '\\').ToUpperInvariant();
        key = $"{type}|{path}";
        return true;
    }

    private static bool TryMergeEntries(
        IReadOnlyList<SolutionEntry> baseline,
        IReadOnlyList<SolutionEntry> local,
        IReadOnlyList<SolutionEntry> target,
        EntryMerger merger,
        out IReadOnlyList<SolutionEntry>? merged) {
        var baselineByKey = baseline.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        var localByKey = local.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        var targetByKey = target.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        var keys = local.Select(entry => entry.Key)
            .Concat(target.Select(entry => entry.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var result = new List<SolutionEntry>();
        foreach (string key in keys) {
            baselineByKey.TryGetValue(key, out SolutionEntry? baselineEntry);
            localByKey.TryGetValue(key, out SolutionEntry? localEntry);
            targetByKey.TryGetValue(key, out SolutionEntry? targetEntry);
            if (!TryMergeEntry(baselineEntry, localEntry, targetEntry, merger, out SolutionEntry? entry)) {
                merged = null;
                return false;
            }
            if (entry is not null) result.Add(entry);
        }
        merged = result;
        return true;
    }

    private static bool TryMergeEntry(
        SolutionEntry? baseline,
        SolutionEntry? local,
        SolutionEntry? target,
        EntryMerger merger,
        out SolutionEntry? merged) {
        merged = null;
        if (baseline is null) {
            if (local is null) {
                merged = target;
                return true;
            }
            if (target is null) {
                merged = local;
                return true;
            }
            return merger(null, local, target, out merged);
        }
        if (local is null) {
            if (target is null || EntryEquals(target, baseline)) return true;
            return false;
        }
        if (target is null) {
            if (EntryEquals(local, baseline)) return true;
            return false;
        }
        return merger(baseline, local, target, out merged);
    }

    private static bool MergeExactEntry(
        SolutionEntry? baseline,
        SolutionEntry local,
        SolutionEntry target,
        out SolutionEntry? merged) {
        if (baseline is null) {
            merged = EntryEquals(local, target) ? local : null;
            return merged is not null;
        }
        if (EntryEquals(local, baseline)) merged = target;
        else if (EntryEquals(target, baseline) || EntryEquals(local, target)) merged = local;
        else merged = null;
        return merged is not null;
    }

    private static bool MergeSection(
        SolutionEntry? baseline,
        SolutionEntry local,
        SolutionEntry target,
        out SolutionEntry? merged) {
        if (baseline is null) return MergeExactEntry(null, local, target, out merged);
        if (EntryEquals(local, baseline)) {
            merged = target;
            return true;
        }
        if (EntryEquals(target, baseline) || EntryEquals(local, target)) {
            merged = local;
            return true;
        }
        if (!TryMergeScalar(baseline.Lines[0], local.Lines[0], target.Lines[0], out string? header) ||
            !TryMergeScalar(baseline.Lines[^1], local.Lines[^1], target.Lines[^1], out string? footer) ||
            !TryIndexSectionLines(baseline, out IReadOnlyList<SolutionEntry>? baselineLines) ||
            !TryIndexSectionLines(local, out IReadOnlyList<SolutionEntry>? localLines) ||
            !TryIndexSectionLines(target, out IReadOnlyList<SolutionEntry>? targetLines) ||
            !TryMergeEntries(
                baselineLines!,
                localLines!,
                targetLines!,
                MergeExactEntry,
                out IReadOnlyList<SolutionEntry>? body)) {
            merged = null;
            return false;
        }
        var lines = new List<string> { header! };
        lines.AddRange(body!.Select(entry => entry.Lines[0]));
        lines.Add(footer!);
        merged = new SolutionEntry(local.Key, lines);
        return true;
    }

    private static bool TryIndexSectionLines(
        SolutionEntry section,
        out IReadOnlyList<SolutionEntry>? entries) {
        var result = new List<SolutionEntry>();
        foreach (string line in section.Lines.Skip(1).SkipLast(1)) {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) {
                entries = null;
                return false;
            }
            int separator = trimmed.IndexOf('=');
            string key = (separator < 0 ? trimmed : trimmed[..separator]).Trim();
            result.Add(new SolutionEntry(key, [line]));
        }
        if (HasDuplicateKeys(result)) {
            entries = null;
            return false;
        }
        entries = result;
        return true;
    }

    private static bool TryMergeScalar<T>(T baseline, T local, T target, out T? merged) {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(local, baseline)) merged = target;
        else if (comparer.Equals(target, baseline) || comparer.Equals(local, target)) merged = local;
        else {
            merged = default;
            return false;
        }
        return true;
    }

    private static bool TryMergeLines(
        IReadOnlyList<string> baseline,
        IReadOnlyList<string> local,
        IReadOnlyList<string> target,
        out IReadOnlyList<string>? merged) {
        if (local.SequenceEqual(baseline, StringComparer.Ordinal)) merged = target;
        else if (target.SequenceEqual(baseline, StringComparer.Ordinal) ||
                 local.SequenceEqual(target, StringComparer.Ordinal)) merged = local;
        else {
            merged = null;
            return false;
        }
        return true;
    }

    private static bool EntryEquals(SolutionEntry left, SolutionEntry right) =>
        left.Lines.SequenceEqual(right.Lines, StringComparer.Ordinal);

    private static bool HasDuplicateKeys(IEnumerable<SolutionEntry> entries) =>
        entries.GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1);

    private static XamlNexusSolutionMergeResult Merged(byte[] content) =>
        new(XamlNexusMergeStatus.Merged, content);

    private static XamlNexusSolutionMergeResult Conflict() =>
        new(XamlNexusMergeStatus.Conflict, null);

    private static XamlNexusSolutionMergeResult Unsupported() =>
        new(XamlNexusMergeStatus.Unsupported, null);

    private delegate bool EntryMerger(
        SolutionEntry? baseline,
        SolutionEntry local,
        SolutionEntry target,
        out SolutionEntry? merged);

    private sealed record SolutionEntry(string Key, IReadOnlyList<string> Lines);

    private sealed record SolutionDocument(
        IReadOnlyList<string> Preamble,
        IReadOnlyList<SolutionEntry> Projects,
        string GlobalLine,
        IReadOnlyList<SolutionEntry> Sections,
        string EndGlobalLine,
        string NewLine,
        bool HasTrailingNewLine);

    [GeneratedRegex(
        "^\\s*Project\\(\"(?<type>\\{[0-9A-Fa-f-]{36}\\})\"\\)\\s*=\\s*\"[^\"]+\",\\s*\"(?<path>[^\"]+)\",\\s*\"\\{[0-9A-Fa-f-]{36}\\}\"\\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProjectLinePattern();

    [GeneratedRegex(
        "^\\s*GlobalSection\\((?<name>[^)]+)\\)\\s*=\\s*\\S+\\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SectionHeaderPattern();
}
