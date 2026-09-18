using XamlNexus.Common.Recipes;

namespace XamlNexus.Common.Projects;

public static partial class PageGenerator {
    private static IEnumerable<XamlNexusRecipeFileChange> CreateListPage(string app, string name) {
        foreach (var (asset, relative) in new[] {
            ("Page.xaml", $"{app}.MainPanel/{name}Page.xaml"),
            ("Page.xaml.cs", $"{app}.MainPanel/{name}Page.xaml.cs"),
            ("ViewModel.cs", $"{app}.MainPanel/ViewModels/{name}ViewModel.cs"),
            ("DataSource.cs", $"{app}.MainPanel/Services/{name}DataSource.cs"),
        }) {
            using var stream = typeof(PageGenerator).Assembly.GetManifestResourceStream(
                $"XamlNexus.Common.Assets.ListPage.{asset}.txt")
                ?? throw new InvalidOperationException($"Missing list page asset: {asset}");
            using var reader = new StreamReader(stream);
            yield return XamlNexusRecipeFileChange.CreateText(relative, reader.ReadToEnd()
                .Replace("__APP__", app).Replace("__NAME__", name));
        }
    }
}
