namespace XamlNexus.Recipes.BuiltIn;

/// <summary>读取随程序集分发的独立 Markdown 模板，替换项目名称等生成参数。</summary>
internal static class RecipeReadmeResources {
    /// <summary>将独立的中英文说明作为组件自有文件安装，与组件一起受事务管理。</summary>
    public static IEnumerable<XamlNexus.Common.Recipes.XamlNexusRecipeFileChange> CreatePair(
        string folder, string stem, string projectName) {
        foreach (string suffix in new[] { ".md", ".zh-CN.md" }) {
            string name = stem + ".README" + suffix;
            yield return XamlNexus.Common.Recipes.XamlNexusRecipeFileChange.CreateText(name,
                Load(folder + "/" + name, new Dictionary<string, string> { ["ProjectName"] = projectName }));
        }
    }

    public static string Load(string name, IReadOnlyDictionary<string, string> tokens) {
        using Stream stream = typeof(RecipeReadmeResources).Assembly.GetManifestResourceStream("Readmes/" + name)
            ?? throw new InvalidOperationException($"Missing Recipe README resource: {name}");
        using var reader = new StreamReader(stream);
        string text = reader.ReadToEnd();
        foreach (var (key, value) in tokens)
            text = text.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        return text;
    }
}
