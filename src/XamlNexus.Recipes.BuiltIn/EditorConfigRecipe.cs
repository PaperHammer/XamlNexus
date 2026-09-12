using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public sealed class EditorConfigRecipe : IXamlNexusRecipe {
    public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
        Id = "editorconfig",
        Version = "1.0.0",
        DisplayName = "EditorConfig",
        Description = "Adds consistent C# and XAML editor defaults to the generated repository.",
        SupportedPresets = ["winui", "hybrid"],
    };

    public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) => new() {
        Changes = [XamlNexusRecipeFileChange.CreateText(".editorconfig", Content)],
    };

    private const string Content = """
        root = true

        [*]
        charset = utf-8
        end_of_line = crlf
        insert_final_newline = true
        trim_trailing_whitespace = true

        [*.{cs,csx}]
        indent_style = space
        indent_size = 4
        dotnet_sort_system_directives_first = true
        dotnet_separate_import_directive_groups = false
        csharp_new_line_before_open_brace = all

        [*.{xaml,xml,csproj,props,targets}]
        indent_style = space
        indent_size = 2

        [*.{yml,yaml,json,md}]
        indent_style = space
        indent_size = 2
        """;
}
