using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XamlNexus.Common.Projects;

/// <summary>将根目录 Markdown 注册为解决方案项；Docs 是虚拟分组，不移动磁盘文件。</summary>
internal static class SolutionDocuments {
    public static string Update(string text, bool xml, IEnumerable<string> add, IEnumerable<string> remove) {
        var additions = add.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var removals = remove.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (xml) {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var root = document.Root!;
            foreach (var item in root.Descendants().Where(e => e.Name.LocalName == "File").ToArray())
                if (removals.Contains((string?)item.Attribute("Path") ?? "")) item.Remove();
            var existing = root.Descendants().Where(e => e.Name.LocalName == "File")
                .Select(e => (string?)e.Attribute("Path")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = additions.Where(name => !existing.Contains(name)).ToArray();
            if (missing.Length > 0) {
                var folder = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Folder"
                    && string.Equals((string?)e.Attribute("Name"), "/Docs/", StringComparison.OrdinalIgnoreCase));
                if (folder is null) {
                    folder = new XElement(root.Name.Namespace + "Folder", new XAttribute("Name", "/Docs/"));
                    root.Add(folder);
                }
                foreach (string name in missing)
                    folder.Add(new XElement(root.Name.Namespace + "File", new XAttribute("Path", name)));
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        // 只修改 SolutionItems，保留项目配置、启动顺序及其他用户内容。
        text = Regex.Replace(text, @"(?ms)(^[ \t]*ProjectSection\(SolutionItems\)[^\r\n]*\r?\n)(.*?)(^[ \t]*EndProjectSection)", match => {
            string body = Regex.Replace(match.Groups[2].Value, @"(?m)^.*(?:\r?\n|$)", line => {
                string name = line.Value.Split('=')[0].Trim();
                return removals.Contains(name) ? "" : line.Value;
            });
            return match.Groups[1].Value + body + match.Groups[3].Value;
        });
        var registered = Regex.Matches(text, @"(?ms)^[ \t]*ProjectSection\(SolutionItems\)[^\r\n]*\r?\n(.*?)^[ \t]*EndProjectSection")
            .SelectMany(m => m.Groups[1].Value.Split('\n')).Select(line => line.Split('=')[0].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string lines = string.Concat(additions.Where(name => !registered.Contains(name))
            .Select(name => "\t\t" + name + " = " + name + newline));
        if (lines.Length == 0) return text;
        var docs = Regex.Match(text, "(?m)^Project\\([^\\r\\n]+ = \"Docs\", \"Docs\", [^\\r\\n]+\\r?\\n[\\s\\S]*?^EndProject");
        if (docs.Success) {
            string block = docs.Value;
            int section = block.IndexOf("EndProjectSection", StringComparison.Ordinal);
            if (section >= 0) block = block.Insert(block.LastIndexOf('\n', section) + 1, lines);
            else block = block.Replace("EndProject", "\tProjectSection(SolutionItems) = preProject" + newline + lines + "\tEndProjectSection" + newline + "EndProject");
            return text.Remove(docs.Index, docs.Length).Insert(docs.Index, block);
        }
        string guid = XamlNexusSolutionGuid.CreateDeterministic("solution-folder/Docs").ToString("B").ToUpperInvariant();
        string entry = $"Project(\"{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}\") = \"Docs\", \"Docs\", \"{guid}\"" + newline
            + "\tProjectSection(SolutionItems) = preProject" + newline + lines + "\tEndProjectSection" + newline + "EndProject" + newline;
        var global = Regex.Match(text, @"(?m)^Global\r?$");
        if (!global.Success) throw new InvalidDataException("Solution has no Global section.");
        return text.Insert(global.Index, entry);
    }
}
