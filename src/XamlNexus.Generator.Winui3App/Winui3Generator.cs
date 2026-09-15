using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;

namespace XamlNexus.Generator.Winui3App {
    public class Winui3Generator : BaseGenerator {
        protected override FrameworkType Framework => FrameworkType.Winui3;

        protected override string TemplateRoot =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Winui3");

        protected override string GetTemplatePrefix() => "Winui3_XamlNexus";

        protected override string GetPresetId() => "winui";

        protected override IEnumerable<string> GetManagedModuleIds() =>
            base.GetManagedModuleIds().Where(id => id != "updater");

        protected override Dictionary<string, string> GetCustomTokens(ProjectConfig config) => new() {
            [GetTemplatePrefix()] = config.SlnName,
            // 兼容旧版 Models 模板的大小写，字符串替换区分大小写
            ["WInui3_XamlNexus"] = config.SlnName
        };

        protected override IEnumerable<(string Name, string? Folder)> GetProjects() {
            return [
                ("Winui3_XamlNexus.UI", null),
                ("Winui3_XamlNexus.Common", null),
                ("Winui3_XamlNexus.Models", null),
                ("Winui3_XamlNexus.UIComponent", null),
                ("Winui3_XamlNexus.MainPanel", "Panels"),
                ("Winui3_XamlNexus.AppSettingsPanel", "Panels")
            ];
        }
    }
}
