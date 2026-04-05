using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Generator.Winui3App {
    [Generator(FrameworkType.Winui3)]
    public class Winui3Generator : BaseGenerator {
        protected override string TemplateRoot =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Winui3");

        protected override string GetTemplatePrefix() => "Winui3_XamlNexus";

        protected override Dictionary<string, string> GetCustomTokens(ProjectConfig config) {
            return new Dictionary<string, string> {
                { "Winui3_XamlNexus", config.SlnName }
            };
        }

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
