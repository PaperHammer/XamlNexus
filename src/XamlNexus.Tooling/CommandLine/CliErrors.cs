using XamlNexus.Common.Resources;

namespace XamlNexus.Tooling.CommandLine;

/// <summary>集中绑定错误场景、编号与中英文文案资源。</summary>
public static class CliErrors {
    public static readonly ErrorDefinition RunFailed =
        new(CliCodes.RunFailed, "Cli_RunFailed", 1);

    public static readonly ErrorDefinition ListFailed =
        new(CliCodes.ListFailed, "Cli_ListFailed", 1);

    public static readonly ErrorDefinition ValidationFailed =
        new(CliCodes.ValidationFailed, "Cli_ValidationFailed", 1);

    public static readonly ErrorDefinition RecipeCatalogFailed =
        new(CliCodes.RecipeCatalogFailed, "Cli_RecipeCatalogFailed", 1);

    public static readonly ErrorDefinition InvalidArguments =
        new(CliCodes.InvalidArguments, "Cli_InvalidArguments", 1);

}
