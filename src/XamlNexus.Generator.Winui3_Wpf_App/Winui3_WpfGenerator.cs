using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Generator.Winui3_Wpf_App {
    [Generator(FrameworkType.Winui3_Wpf)]
    public class Winui3_WpfGenerator : BaseGenerator {
        protected override string TemplateRoot =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Winui3_Wpf");

        protected override string GetTemplatePrefix() => "Winui3_Wpf_XamlNexus";

        protected override Dictionary<string, string> GetCustomTokens(ProjectConfig config) {
            return new Dictionary<string, string> {
                { "Winui3_Wpf_XamlNexus", config.SlnName },
                { "Winui3WpfXamlNexus", config.SlnName }
            };
        }

        protected override string[] GetSkipCopyDirs() {
            return ["Plugins"];
        }

        protected override IEnumerable<(string Name, string? Folder)> GetProjects() {
            return [
                ("Winui3_Wpf_XamlNexus", null),
                ("Winui3_Wpf_XamlNexus.UI", null),
                ("Winui3_Wpf_XamlNexus.Common", null),
                ("Winui3_Wpf_XamlNexus.DataAssistor", null),
                ("Winui3_Wpf_XamlNexus.Grpc.Client", null),
                ("Winui3_Wpf_XamlNexus.Grpc.Service", null),
                ("Winui3_Wpf_XamlNexus.Models", null),
                ("Winui3_Wpf_XamlNexus.UIComponent", null),
                ("Winui3_Wpf_XamlNexus.MainPanel", "Panels"),
                ("Winui3_Wpf_XamlNexus.AppSettingsPanel", "Panels")
            ];
        }
    }
}
