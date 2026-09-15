namespace XamlNexus.Common.CommandLine;

/// <summary>命令行名称与持久化 Recipe ID 的映射，避免改名破坏旧清单或与模板 updater 能力冲突。</summary>
public static class RecipeCommandNames {
    public static string ToRecipeId(string name) => name.Trim().ToLowerInvariant() switch {
        "updater" => "app-update",
        "tray" => "system-tray",
        _ => name.Trim(),
    };

    public static string ToCommandName(string id) => id.ToLowerInvariant() switch {
        "app-update" => "updater",
        "system-tray" => "tray",
        _ => id,
    };
}
