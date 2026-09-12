using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace XamlNexus.Common.Projects;

public sealed record XamlNexusXmlMergeResult(
    XamlNexusMergeStatus Status,
    byte[]? Content);

public static class XamlNexusThreeWayXmlMerge {
    private const int MaximumContentBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] IdentityAttributes = [
        "Include",
        "Update",
        "Remove",
        "Key",
        "Name",
        "Class",
        "TargetType",
        "Condition",
        "Label",
    ];

    public static XamlNexusXmlMergeResult Merge(
        byte[] baseline,
        byte[] local,
        byte[] target) {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(target);
        if (baseline.Length > MaximumContentBytes ||
            local.Length > MaximumContentBytes ||
            target.Length > MaximumContentBytes) {
            return Unsupported();
        }
        if (!TryParse(baseline, out XDocument? baselineDocument) ||
            !TryParse(local, out XDocument? localDocument) ||
            !TryParse(target, out XDocument? targetDocument) ||
            baselineDocument!.Root is null ||
            localDocument!.Root is null ||
            targetDocument!.Root is null ||
            HasUnsupportedNodes(baselineDocument!) ||
            HasUnsupportedNodes(localDocument!) ||
            HasUnsupportedNodes(targetDocument!)) {
            return Unsupported();
        }
        XDocument verifiedBaseline = baselineDocument!;
        XDocument verifiedLocal = localDocument!;
        XDocument verifiedTarget = targetDocument!;

        XamlNexusMergeStatus declarationStatus = MergeScalar(
            DeclarationValue(verifiedBaseline.Declaration),
            DeclarationValue(verifiedLocal.Declaration),
            DeclarationValue(verifiedTarget.Declaration),
            out string? declaration);
        if (declarationStatus != XamlNexusMergeStatus.Merged)
            return new XamlNexusXmlMergeResult(declarationStatus, null);

        XamlNexusMergeStatus rootStatus = MergeElement(
            verifiedBaseline.Root!,
            verifiedLocal.Root!,
            verifiedTarget.Root!,
            out XElement? mergedRoot);
        if (rootStatus != XamlNexusMergeStatus.Merged)
            return new XamlNexusXmlMergeResult(rootStatus, null);

        var mergedDocument = new XDocument(ParseDeclaration(declaration), mergedRoot);
        return new XamlNexusXmlMergeResult(
            XamlNexusMergeStatus.Merged,
            Serialize(mergedDocument, DetectNewLine(local), HasUtf8Bom(local)));
    }

    private static XamlNexusMergeStatus MergeElement(
        XElement baseline,
        XElement local,
        XElement target,
        out XElement? merged) {
        merged = null;
        if (XNode.DeepEquals(local, baseline)) {
            merged = new XElement(target);
            return XamlNexusMergeStatus.Merged;
        }
        if (XNode.DeepEquals(target, baseline) || XNode.DeepEquals(local, target)) {
            merged = new XElement(local);
            return XamlNexusMergeStatus.Merged;
        }
        if (baseline.Name != local.Name || baseline.Name != target.Name)
            return XamlNexusMergeStatus.Conflict;

        XamlNexusMergeStatus attributeStatus = MergeAttributes(
            baseline,
            local,
            target,
            out IReadOnlyList<XAttribute>? attributes);
        if (attributeStatus != XamlNexusMergeStatus.Merged)
            return attributeStatus;

        bool baselineHasElements = baseline.Elements().Any();
        bool localHasElements = local.Elements().Any();
        bool targetHasElements = target.Elements().Any();
        if (!baselineHasElements && !localHasElements && !targetHasElements) {
            XamlNexusMergeStatus valueStatus = MergeScalar(
                baseline.Value,
                local.Value,
                target.Value,
                out string? value);
            if (valueStatus != XamlNexusMergeStatus.Merged) return valueStatus;
            merged = new XElement(local.Name, attributes, value);
            return XamlNexusMergeStatus.Merged;
        }
        if ((!baselineHasElements && !string.IsNullOrWhiteSpace(baseline.Value)) ||
            (!localHasElements && !string.IsNullOrWhiteSpace(local.Value)) ||
            (!targetHasElements && !string.IsNullOrWhiteSpace(target.Value)))
            return XamlNexusMergeStatus.Conflict;

        XamlNexusMergeStatus childrenStatus = MergeChildren(
            baseline,
            local,
            target,
            out IReadOnlyList<XElement>? children);
        if (childrenStatus != XamlNexusMergeStatus.Merged)
            return childrenStatus;
        merged = new XElement(local.Name, attributes, children);
        return XamlNexusMergeStatus.Merged;
    }

    private static XamlNexusMergeStatus MergeAttributes(
        XElement baseline,
        XElement local,
        XElement target,
        out IReadOnlyList<XAttribute>? merged) {
        var result = new List<XAttribute>();
        XName[] names = local.Attributes().Select(attribute => attribute.Name)
            .Concat(target.Attributes().Select(attribute => attribute.Name))
            .Concat(baseline.Attributes().Select(attribute => attribute.Name))
            .Distinct()
            .ToArray();
        foreach (XName name in names) {
            XamlNexusMergeStatus status = MergeScalar(
                AttributeValue(baseline, name),
                AttributeValue(local, name),
                AttributeValue(target, name),
                out string? value);
            if (status != XamlNexusMergeStatus.Merged) {
                merged = null;
                return status;
            }
            if (value is not null) result.Add(new XAttribute(name, value));
        }
        merged = result;
        return XamlNexusMergeStatus.Merged;
    }

    private static XamlNexusMergeStatus MergeChildren(
        XElement baseline,
        XElement local,
        XElement target,
        out IReadOnlyList<XElement>? merged) {
        XElement[] baselineElements = baseline.Elements().ToArray();
        XElement[] localElements = local.Elements().ToArray();
        XElement[] targetElements = target.Elements().ToArray();
        // Whole-sequence changes need no individual node matching. This also allows
        // unnamed controls when only one side changes the children.
        if (baselineElements.SequenceEqual(localElements, XNode.EqualityComparer)) {
            merged = targetElements.Select(element => new XElement(element)).ToArray();
            return XamlNexusMergeStatus.Merged;
        }
        if (baselineElements.SequenceEqual(targetElements, XNode.EqualityComparer) ||
            localElements.SequenceEqual(targetElements, XNode.EqualityComparer)) {
            merged = localElements.Select(element => new XElement(element)).ToArray();
            return XamlNexusMergeStatus.Merged;
        }
        if (HasAmbiguousChildMatches(baselineElements, localElements, targetElements)) {
            merged = null;
            return XamlNexusMergeStatus.Conflict;
        }
        IReadOnlyDictionary<string, XElement> baselineChildren = IndexChildren(baseline);
        IReadOnlyDictionary<string, XElement> localChildren = IndexChildren(local);
        IReadOnlyDictionary<string, XElement> targetChildren = IndexChildren(target);
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var processed = new HashSet<string>(StringComparer.Ordinal);

        foreach ((string id, XElement localChild) in EnumerateIndexedChildren(local)) {
            processed.Add(id);
            bool hasBaseline = baselineChildren.TryGetValue(id, out XElement? baselineChild);
            bool hasTarget = targetChildren.TryGetValue(id, out XElement? targetChild);
            if (!hasBaseline) {
                if (hasTarget && !XNode.DeepEquals(localChild, targetChild)) {
                    merged = null;
                    return XamlNexusMergeStatus.Conflict;
                }
                result.Add(id, new XElement(localChild));
                continue;
            }
            if (!hasTarget) {
                if (!XNode.DeepEquals(localChild, baselineChild)) {
                    merged = null;
                    return XamlNexusMergeStatus.Conflict;
                }
                continue;
            }
            XamlNexusMergeStatus status = MergeElement(
                baselineChild!,
                localChild,
                targetChild!,
                out XElement? child);
            if (status != XamlNexusMergeStatus.Merged) {
                merged = null;
                return status;
            }
            result.Add(id, child!);
        }

        foreach ((string id, XElement targetChild) in EnumerateIndexedChildren(target)) {
            if (!processed.Add(id)) continue;
            if (baselineChildren.TryGetValue(id, out XElement? baselineChild)) {
                if (!XNode.DeepEquals(targetChild, baselineChild)) {
                    merged = null;
                    return XamlNexusMergeStatus.Conflict;
                }
                continue;
            }
            result.Add(id, new XElement(targetChild));
        }
        return MergeChildOrder(baselineChildren, localChildren, targetChildren, result, out merged);
    }

    private static XamlNexusMergeStatus MergeChildOrder(
        IReadOnlyDictionary<string, XElement> baseline,
        IReadOnlyDictionary<string, XElement> local,
        IReadOnlyDictionary<string, XElement> target,
        IReadOnlyDictionary<string, XElement> children,
        out IReadOnlyList<XElement>? merged) {
        merged = null;
        string[] baselineOrder = baseline.Keys.Where(children.ContainsKey).ToArray();
        string[] localOrder = local.Keys.Where(id => children.ContainsKey(id) && baseline.ContainsKey(id)).ToArray();
        string[] targetOrder = target.Keys.Where(id => children.ContainsKey(id) && baseline.ContainsKey(id)).ToArray();
        bool localReordered = !localOrder.SequenceEqual(baselineOrder);
        bool targetReordered = !targetOrder.SequenceEqual(baselineOrder);
        if (localReordered && targetReordered && !localOrder.SequenceEqual(targetOrder))
            return XamlNexusMergeStatus.Conflict;

        // Select the surviving baseline order separately from inserted nodes.
        // An unchanged side must not veto the other side's reorder.
        string[] order = localReordered ? localOrder : targetOrder;
        var edges = children.Keys.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal));
        var incoming = children.Keys.ToDictionary(id => id, _ => 0);
        void AddEdge(string before, string after) {
            if (edges[before].Add(after)) incoming[after]++;
        }
        for (int index = 1; index < order.Length; index++)
            AddEdge(order[index - 1], order[index]);

        // Preserve each insertion's neighbors, including consecutive new nodes.
        // Incompatible insertion anchors and reorders form a cycle and require review.
        foreach (var side in new[] { local, target }) {
            string? previous = null;
            foreach (string id in side.Keys.Where(children.ContainsKey)) {
                if (previous is not null && (!baseline.ContainsKey(previous) || !baseline.ContainsKey(id)))
                    AddEdge(previous, id);
                previous = id;
            }
        }

        // Stable tie-breaking keeps independent local/target additions deterministic.
        var priority = children.Keys.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
        var ready = new PriorityQueue<string, int>();
        foreach (string id in children.Keys)
            if (incoming[id] == 0) ready.Enqueue(id, priority[id]);
        var result = new List<XElement>(children.Count);
        while (ready.TryDequeue(out string? id, out _)) {
            result.Add(children[id]);
            foreach (string next in edges[id])
                if (--incoming[next] == 0) ready.Enqueue(next, priority[next]);
        }
        if (result.Count != children.Count) return XamlNexusMergeStatus.Conflict;
        merged = result;
        return XamlNexusMergeStatus.Merged;
    }

    private static bool HasAmbiguousChildMatches(XElement[] baseline, XElement[] local, XElement[] target) {
        var baselineGroups = baseline.ToLookup(ElementIdentity, StringComparer.Ordinal);
        var localGroups = local.ToLookup(ElementIdentity, StringComparer.Ordinal);
        var targetGroups = target.ToLookup(ElementIdentity, StringComparer.Ordinal);
        var identities = baselineGroups.Select(group => group.Key)
            .Concat(localGroups.Select(group => group.Key))
            .Concat(targetGroups.Select(group => group.Key)).Distinct(StringComparer.Ordinal);
        foreach (string identity in identities) {
            XElement[] original = baselineGroups[identity].ToArray();
            XElement[] current = localGroups[identity].ToArray();
            XElement[] updated = targetGroups[identity].ToArray();
            if (original.Length <= 1 && current.Length <= 1 && updated.Length <= 1) continue;
            // Occurrence numbers are not stable identities after an edit, insertion,
            // deletion or reorder. Only unchanged duplicate groups can use them.
            if (!original.SequenceEqual(current, XNode.EqualityComparer) ||
                !original.SequenceEqual(updated, XNode.EqualityComparer)) return true;
        }
        return false;
    }

    private static IReadOnlyDictionary<string, XElement> IndexChildren(XElement parent) =>
        EnumerateIndexedChildren(parent).ToDictionary(item => item.Id, item => item.Element);

    private static IEnumerable<(string Id, XElement Element)> EnumerateIndexedChildren(XElement parent) {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (XElement element in parent.Elements()) {
            string identity = ElementIdentity(element);
            occurrences.TryGetValue(identity, out int occurrence);
            occurrences[identity] = occurrence + 1;
            yield return ($"{identity}#{occurrence}", element);
        }
    }

    private static string ElementIdentity(XElement element) {
        // Path identifies a solution project, but is an editable value on XAML bindings.
        if (element.Name == "Project" && element.Document?.Root?.Name == "Solution"
            && element.Attribute("Path") is { } path)
            return $"{element.Name}|{path.Name}={path.Value}";
        foreach (string candidate in IdentityAttributes) {
            XAttribute? attribute = element.Attributes().FirstOrDefault(value =>
                value.Name.LocalName.Equals(candidate, StringComparison.Ordinal));
            if (attribute is not null)
                return $"{element.Name}|{attribute.Name}={attribute.Value}";
        }
        return element.Name.ToString();
    }

    private static XamlNexusMergeStatus MergeScalar(
        string? baseline,
        string? local,
        string? target,
        out string? merged) {
        if (local == baseline) {
            merged = target;
            return XamlNexusMergeStatus.Merged;
        }
        if (target == baseline || local == target) {
            merged = local;
            return XamlNexusMergeStatus.Merged;
        }
        merged = null;
        return XamlNexusMergeStatus.Conflict;
    }

    private static bool HasUtf8Bom(byte[] content) => content.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });

    private static bool TryParse(byte[] content, out XDocument? document) {
        document = null;
        if (content.Contains((byte)0)) return false;
        try {
            string text = StrictUtf8.GetString(HasUtf8Bom(content) ? content.AsSpan(3) : content.AsSpan());
            using var reader = XmlReader.Create(
                new StringReader(text),
                new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
            document = XDocument.Load(reader, LoadOptions.None);
            return true;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or XmlException) {
            return false;
        }
    }

    private static bool HasUnsupportedNodes(XDocument document) =>
        document.Nodes().Any(node => node is not XElement) ||
        document.Root!.DescendantsAndSelf().Any(element => {
            bool hasElements = element.Elements().Any();
            return element.Nodes().Any(node =>
                node is not XElement &&
                (node is XCData ||
                 node is not XText text ||
                 (hasElements && !string.IsNullOrWhiteSpace(text.Value))));
        });

    private static string? AttributeValue(XElement element, XName name) =>
        element.Attribute(name)?.Value;

    private static string? DeclarationValue(XDeclaration? declaration) => declaration is null
        ? null
        : $"{declaration.Version}|{declaration.Encoding}|{declaration.Standalone}";

    private static XDeclaration? ParseDeclaration(string? value) {
        if (value is null) return null;
        string[] parts = value.Split('|');
        return new XDeclaration(parts[0], EmptyToNull(parts[1]), EmptyToNull(parts[2]));
    }

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static byte[] Serialize(XDocument document, string newLine, bool emitBom) {
        using var output = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings {
            Encoding = new UTF8Encoding(emitBom),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = newLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = document.Declaration is null,
        })) {
            document.Save(writer);
        }
        return output.ToArray();
    }

    private static string DetectNewLine(byte[] content) {
        string text = StrictUtf8.GetString(HasUtf8Bom(content) ? content.AsSpan(3) : content.AsSpan());
        return text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private static XamlNexusXmlMergeResult Unsupported() =>
        new(XamlNexusMergeStatus.Unsupported, null);
}
