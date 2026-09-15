using System.Text;

namespace XamlNexus.Common.Projects;

public enum XamlNexusMergeStatus {
    /// <summary>已得到合并内容，不代表内容通过了编译或业务逻辑验证</summary>
    Merged,
    /// <summary>双方修改无法安全组合，需要人工处理</summary>
    Conflict,
    /// <summary>内容、编码、大小或差异计算量超出当前合并器支持范围</summary>
    Unsupported,
}

public sealed record XamlNexusTextMergeResult(
    XamlNexusMergeStatus Status,
    byte[]? Content);

/// <summary>以旧基线为共同坐标执行行级三方合并，并生成供人工处理的冲突文本</summary>
public static class XamlNexusThreeWayTextMerge {
    // 仅需要解码和计算差异时受这些限制；直接返回某一方原始字节的快捷分支不经过解码
    private const int MaximumContentBytes = 1024 * 1024;
    private const long MaximumDiffWork = 16_000_000;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// 三方文本合并：分别计算用户和新模板相对于旧基线的行级修改，再组合不重叠的修改
    /// 只在内存中生成结果，不写入项目文件，也不理解源代码的语义
    /// </summary>
    /// <param name="baseline">旧版本生成时保存的原始内容快照</param>
    /// <param name="local">用户当前文件内容</param>
    /// <param name="target">新版本模板对应的文件内容</param>
    /// <returns>合并状态；成功时包含完整文件内容，失败时内容为空</returns>
    public static XamlNexusTextMergeResult Merge(
        byte[] baseline,
        byte[] local,
        byte[] target) {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(target);

        // 用户未改动时直接采用新模板；新模板未改动或双方内容相同时保留本地
        // 这些快捷判断不需要文本解码，也不会改写原有字节和格式
        if (local.AsSpan().SequenceEqual(baseline))
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Merged, target);
        if (target.AsSpan().SequenceEqual(baseline) || local.AsSpan().SequenceEqual(target))
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Merged, local);
        if (!TryDecode(baseline, out string? baselineText) ||
            !TryDecode(local, out string? localText) ||
            !TryDecode(target, out string? targetText)) {
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Unsupported, null);
        }

        // 按行拆分并保留换行符两组修改均以同一份基线的行索引定位，避免插入造成位置偏移
        IReadOnlyList<string> baselineLines = SplitLines(baselineText);
        IReadOnlyList<string> localLines = SplitLines(localText);
        IReadOnlyList<string> targetLines = SplitLines(targetText);
        IReadOnlyList<TextEdit>? localEdits = CreateEdits(baselineLines, localLines);
        IReadOnlyList<TextEdit>? targetEdits = CreateEdits(baselineLines, targetLines);
        if (localEdits is null || targetEdits is null)
            return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Unsupported, null);
        var mergedEdits = new List<TextEdit>(localEdits);
        foreach (TextEdit targetEdit in targetEdits) {
            // 相同位置、删除数量和替换内容完全一致时，只保留一份修改
            TextEdit? identical = mergedEdits.FirstOrDefault(localEdit => AreIdentical(localEdit, targetEdit));
            if (identical is not null) continue;
            // 不同修改涉及重叠范围时不猜测取舍或插入顺序，直接报告冲突
            if (mergedEdits.Any(localEdit => Overlaps(localEdit, targetEdit)))
                return new XamlNexusTextMergeResult(XamlNexusMergeStatus.Conflict, null);
            mergedEdits.Add(targetEdit);
        }

        // 按基线位置重建文件：复制未改动段，追加替换内容，再跳过被删除的基线行
        mergedEdits.Sort((left, right) => left.Start.CompareTo(right.Start));
        var merged = new StringBuilder();
        int cursor = 0;
        foreach (TextEdit edit in mergedEdits) {
            for (int index = cursor; index < edit.Start; index++)
                merged.Append(baselineLines[index]);
            foreach (string line in edit.Replacement)
                merged.Append(line);
            // 纯插入的 DeleteCount 为 0，因此原位置的基线行仍会在后续被保留
            cursor = edit.Start + edit.DeleteCount;
        }
        for (int index = cursor; index < baselineLines.Count; index++)
            merged.Append(baselineLines[index]);

        return new XamlNexusTextMergeResult(
            XamlNexusMergeStatus.Merged,
            StrictUtf8.GetBytes(merged.ToString()));
    }

    /// <summary>
    /// 生成带 LOCAL（用户）、BASE（旧基线）、TARGET（新模板）标记的人工合并文本
    /// 默认尝试仅标记冲突区段；无法生成局部冲突或要求 wholeFile 时展示整个文件
    /// 此方法只返回文本，实际导出由升级流程负责
    /// </summary>
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

    /// <summary>将重叠修改归为同一组，正常输出无冲突区域，仅对双方结果不同的区域添加标记</summary>
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
            // 合并传递重叠：A 与 B 重叠、B 与 C 重叠时，三者属于同一组，防止重复输出基线区间
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
        // 未发现行级冲突不代表没有结构冲突；返回 false，让调用方回退为整文件冲突展示
        return hasConflict;
    }

    /// <summary>在基线的指定半开区间应用一侧修改，构造局部冲突中的 LOCAL 或 TARGET 内容</summary>
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

    /// <summary>拒绝超限、含零字节或非法 UTF-8 的内容；失败时交由上层决定如何人工处理</summary>
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

    /// <summary>识别 CRLF、CR 和 LF，并将换行符留在每行内，重建时无需另外拼接换行</summary>
    private static IReadOnlyList<string> SplitLines(string text) {
        // start：当前这一行从哪里开始
        // index：当前扫描到哪里了
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

    // 使用双向 Myers 寻找最短编辑路径，通过匹配的相同行提取插入、删除和替换
    // 计算量超过预算时返回 null，不使用不完整的差异结果进行合并
    // 从两端搜索并切分子问题；复用两个前沿数组，不保存每轮距离的完整轨迹
    // 所有子问题共享同一个计算预算，避免复杂输入导致无界计算
    internal static IReadOnlyList<TextEdit>? CreateEdits(IReadOnlyList<string> baseline, IReadOnlyList<string> variant) {
        var matches = new List<(int Baseline, int Variant)>();
        // Left 区间属于基线，Right 区间属于变体，均为半开区间；用显式栈代替递归调用
        var pending = new Stack<(int Left, int LeftEnd, int Right, int RightEnd)>();
        pending.Push((0, baseline.Count, 0, variant.Count));
        int[]? forward = null;
        int[]? reverse = null;
        long work = 0;
        while (pending.TryPop(out var range)) {
            var (left, leftEnd, right, rightEnd) = range;
            // 先剥离相同前缀和后缀，记录匹配行，只对中间不同部分进行搜索
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
            // 确实需要搜索时才分配数组，后续子问题复用，避免重复分配及递归栈增长
            forward ??= new int[baseline.Count + variant.Count + 5];
            reverse ??= new int[forward.Length];
            var split = FindMiddleSplit(baseline, variant, left, leftEnd, right, rightEnd,
                forward, reverse, ref work);
            if (split is null) return null;
            var (x, y) = split.Value;
            if ((x == left && y == right) || (x == leftEnd && y == rightEnd))
                return null; // 切分必须缩小问题范围，不能接受没有进展或不完整的差异
            pending.Push((x, leftEnd, y, rightEnd));
            pending.Push((left, x, right, y));
        }
        matches.Sort((left, right) => left.Baseline.CompareTo(right.Baseline));

        var edits = new List<TextEdit>();
        int baselineCursor = 0;
        int variantCursor = 0;
        // 相邻匹配点之间就是修改段；追加文件末尾哨兵，使最后一段差异也能被提取
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

    /// <summary>
    /// 双向 Myers 搜索：从两个区间的首尾逐轮增加编辑距离，前沿相遇时返回切分坐标
    /// x/y 为当前子问题内的位置，k = x - y 为对角线；匹配行沿对角线前进，不增加编辑次数
    /// </summary>
    private static (int Left, int Right)? FindMiddleSplit(
        IReadOnlyList<string> baseline, IReadOnlyList<string> variant,
        int left, int leftEnd, int right, int rightEnd,
        int[] forward, int[] reverse, ref long work) {
        int n = leftEnd - left;
        int m = rightEnd - right;
        int limit = (n + m + 1) / 2; // 单边最多搜索多少层
        int offset = limit + 1;
        int length = 2 * limit + 3; // 覆盖 -limit ... 0 ... +limit
        /*
         * limit 同时决定了 k 的最大搜索范围
         * k = x - y
         * 插入 → k - 1
         * 删除 → k + 1
         * 
         * distance = D 可能访问的 k 是：-D, -D+2, ..., D-2, D
         *  D = 0     k =  0
            D = 1     k = -1, 1
            D = 2     k = -2, 0, 2
            D = 3     k = -3, -1, 1, 3
            ...
            D = limit

            => -limit <= k <= +limit

            但是 数组不能使用负数下标，所以需要把整个 k 坐标向右平移

            但 Myers 的递推还要访问相邻对角线：forward[index - 1]、forward[index + 1]
            故意在左右留出了额外的安全空间
         */
        Array.Fill(forward, -1, 0, length); // -1 为初始值
        Array.Fill(reverse, -1, 0, length);
        forward[offset + 1] = reverse[offset + 1] = 0; // 保存每条 k 对角线的状态。对于某条对角线 k，目前能够到达的最远 x
        int delta = n - m;
        // 长度差的奇偶决定在哪一侧扩展后检查前沿相遇
        bool odd = (delta & 1) != 0;
        int forwardStart = 0, forwardEnd = 0, reverseStart = 0, reverseEnd = 0;
        for (int distance = 0; distance <= limit; distance++) { // distance: 当前允许使用多少次 insert/delete.第一次能到终点的 D，就是：最短编辑距离。
            for (int k = -distance + forwardStart; k <= distance - forwardEnd; k += 2) {
                if (++work > MaximumDiffWork) return null;
                int index = offset + k;
                // 前沿相同时统一优先删除，两侧使用相同规则，使重复行场景的选择保持一致
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
            // 反向坐标从区间末尾计算；相遇后转换回基线和变体中的绝对索引
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

    /// <summary>判断两项修改的基线位置是否重叠；相同修改的去重由调用方单独处理</summary>
    private static bool Overlaps(TextEdit left, TextEdit right) {
        // 两次插入不删除基线行，但占据同一个插入点时仍可能存在顺序冲突
        if (left.DeleteCount == 0 && right.DeleteCount == 0)
            return left.Start == right.Start;
        // 插入点落在另一修改的删除/替换范围内，或恰好处于边界，均保守地判为重叠
        if (left.DeleteCount == 0)
            return left.Start >= right.Start && left.Start <= right.Start + right.DeleteCount;
        if (right.DeleteCount == 0)
            return right.Start >= left.Start && right.Start <= left.Start + left.DeleteCount;
        // 两个非空范围按半开区间 [Start, Start + DeleteCount) 判断，相邻范围不算重叠
        return left.Start < right.Start + right.DeleteCount &&
            right.Start < left.Start + left.DeleteCount;
    }

    /// <summary>基于旧基线的一段行级修改，统一表示插入、删除和替换</summary>
    /// <param name="Start">基线中的零起始行索引；插入发生在该行之前，也可指向文件末尾</param>
    /// <param name="DeleteCount">从 Start 开始删除的行数；0 表示纯插入</param>
    /// <param name="Replacement">要写入的新行，包含原有换行符；空集合表示没有插入内容</param>
    internal sealed record TextEdit(
        int Start,
        int DeleteCount,
        IReadOnlyList<string> Replacement);
}
