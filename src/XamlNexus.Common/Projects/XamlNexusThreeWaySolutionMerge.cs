using System.Text;
using System.Text.RegularExpressions;

namespace XamlNexus.Common.Projects;

public sealed record XamlNexusSolutionMergeResult(
    XamlNexusMergeStatus Status,
    byte[]? Content);

/// <summary>按项目块和全局配置节合并传统 .sln 文件；不用于 XML 格式的 .slnx</summary>
public static partial class XamlNexusThreeWaySolutionMerge {
    private const int MaximumContentBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>将三份解决方案解析成结构后合并，只返回结果字节，不写入磁盘</summary>
    /// <param name="baseline">旧版本解决方案基线</param>
    /// <param name="local">用户当前解决方案</param>
    /// <param name="target">目标版本生成的解决方案</param>
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

        // 文件头和 Global 边界整体比较；项目按身份匹配，配置节还可继续细分到配置项
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

        // 重组解决方案，沿用本地换行风格及文件末尾是否换行
        var lines = new List<string>(preamble!);
        foreach (SolutionEntry project in projects!) lines.AddRange(project.Lines);
        lines.Add(globalLine!);
        foreach (SolutionEntry section in sections!) lines.AddRange(section.Lines);
        lines.Add(endGlobalLine!);
        string text = string.Join(localDocument.NewLine, lines);
        if (localDocument.HasTrailingNewLine) text += localDocument.NewLine;
        return Merged(StrictUtf8.GetBytes(text));
    }

    /// <summary>只接受大小受限的 UTF-8 内容及可识别的 SLN 结构；解析失败不尝试猜测修复</summary>
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

        // 第一段项目之前的行作为文件头；每个 Project 到 EndProject 保留为完整项目块
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

        // Global 内按 GlobalSection 名称索引；重复身份会让合并对象不明确，因此拒绝解析
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

    /// <summary>在指定范围内查找块结束标记，找不到时返回 -1</summary>
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

    /// <summary>以项目类型 GUID 和规范化路径作为身份，避免依赖可能变化的项目实例 GUID</summary>
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

    /// <summary>按键匹配三方条目；保留本地键顺序，再追加仅目标侧出现的键</summary>
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

    /// <summary>
    /// 处理新增和删除：单边新增可保留，删除遇到另一边未改动可接受，删除与修改并存则冲突
    /// 两边都存在时交给具体条目合并器；成功且 merged 为空表示该条目应删除
    /// </summary>
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

    /// <summary>把条目视作整体；双方新增同一键必须内容相同，双方不同修改则无法自动选择</summary>
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

    /// <summary>已有配置节发生双边修改时，分别合并节头、节尾和按键索引的配置行</summary>
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

    /// <summary>以等号左侧作为配置键，无等号时用整行；空行或重复键无法可靠索引</summary>
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

    /// <summary>单值三方规则：一方未改则采用另一方，双方相同则保留，否则冲突</summary>
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

    /// <summary>将整组行作为一个值比较，不在此处计算逐行差异</summary>
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

    // 一个可按身份匹配的结构单元，可以是项目块、配置节或配置行
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
