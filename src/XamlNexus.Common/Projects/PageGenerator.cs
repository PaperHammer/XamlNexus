using System.Text.RegularExpressions;
using XamlNexus.Common.Recipes;

namespace XamlNexus.Common.Projects;

public static partial class PageGenerator {
    public static IReadOnlyList<string> Add(XamlNexusProjectContext project, string name, bool dryRun = false, bool skipNavigation = false, string kind = "blank") {
        if (kind is not ("blank" or "list")) throw new ArgumentException("Page kind must be blank or list.", nameof(kind));
        if (project.Manifest.Project.Preset is not ("winui" or "hybrid"))
            throw new InvalidOperationException("Page generation supports WinUI and hybrid projects.");
        if (!Regex.IsMatch(name, "^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Use a page name such as Orders: start with A-Z, followed by letters or digits.");
        // A custom shell may replace MainWindow entirely; page-only generation still works.
        if (XamlNexusProjectValidator.Validate(project).Issues.Any(issue =>
            issue.Severity == ProjectValidationSeverity.Error && issue.Code != "XN1101"))
            throw new InvalidOperationException("Fix project validation errors before adding a page.");
        string app = project.Manifest.Project.Name;
        string page = name + "Page";
        string vm = name + "ViewModel";
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project.RootDirectory));
        string SafePath(string path) {
            string full = Path.GetFullPath(Path.Combine(root, path));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Page path escapes the project.");
            for (string? current = full; current is not null; current = Path.GetDirectoryName(current)) {
                try {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Page generation does not support linked files or directories.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
            }
            return full;
        }
        if (!File.Exists(SafePath($"{app}.MainPanel/{app}.MainPanel.csproj")))
            throw new InvalidOperationException("The standard MainPanel project is required.");
        SafePath("xamlnexus.json");
        bool supportsInjection = File.Exists(SafePath($"{app}.Common/Utils/DI/AppObjectFactory.cs"));
        string viewModelCreation = supportsInjection ? $"AppObjectFactory.Create<{vm}>()" : "new()";
        var navigationChanges = new List<XamlNexusRecipeFileChange>();
        if (!skipNavigation && File.Exists(SafePath($"{app}.UIComponent/Navigation/INavigationRegistry.cs"))) {
            navigationChanges.Add(XamlNexusRecipeFileChange.CreateText($"{app}.UI/Navigation/{name}Navigation.cs", $$"""
                using System.Runtime.CompilerServices;
                using {{app}}.UIComponent.Navigation;

                namespace {{app}}.UI.Navigation;

                internal static class {{name}}Navigation {
                    [ModuleInitializer]
                    internal static void Register() => NavigationRegistry.Default.Register(new NavigationEntry(
                        "{{name}}", typeof(global::{{app}}.MainPanel.{{page}}), "{{name}}"));
                }
                """));
        }
        var plan = new XamlNexusRecipePlan { Changes = [
            XamlNexusRecipeFileChange.CreateText($"{app}.MainPanel/{page}.xaml", $$"""
                <arc:ArcPage
                    x:Class="{{app}}.MainPanel.{{page}}"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="using:{{app}}.UIComponent.Templates">
                    <StackPanel Padding="24" Spacing="12">
                        <TextBlock Text="{{name}}" FontSize="28" />
                        <!-- Add page content here. -->
                    </StackPanel>
                </arc:ArcPage>
                """),
            XamlNexusRecipeFileChange.CreateText($"{app}.MainPanel/{page}.xaml.cs", $$"""
                using System;
                {{(supportsInjection ? $"using {app}.Common.Utils.DI;" : "")}}
                using {{app}}.MainPanel.ViewModels;
                using {{app}}.UIComponent.Templates;

                namespace {{app}}.MainPanel;

                public sealed partial class {{page}} : ArcPage {
                    public override Type ArcType => typeof({{page}});
                    public {{vm}} ViewModel { get; } = {{viewModelCreation}};
                    public {{page}}() => InitializeComponent();
                }
                """),
            XamlNexusRecipeFileChange.CreateText($"{app}.MainPanel/ViewModels/{vm}.cs", $$"""
                using {{app}}.Models.Mvvm;

                namespace {{app}}.MainPanel.ViewModels;

                public sealed class {{vm}} : ObservableObject {
                    // Add page state here. Shared services can be injected through the constructor.
                }
                """),
            .. navigationChanges,
        ] };
        if (kind == "list") plan = new XamlNexusRecipePlan { Changes = [.. CreateListPage(app, name), .. navigationChanges] };
        foreach (var change in plan.Changes) SafePath(change.RelativePath);
        return XamlNexusRecipeTransaction.ApplyPageChanges(project, plan, dryRun);
    }


}
