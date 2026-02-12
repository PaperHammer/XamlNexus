using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Models;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Template.WpfApp {
    [Generator(FrameworkType.Wpf)]
    public class WpfAppGenerator : BaseGenerator {
        public override void Generate(ProjectConfig config) {
            // 模拟输出，后期这里写具体的文件创建逻辑
            AnsiConsole.MarkupLine($"[grey]DEBUG:[/] 正在为 {config.ProjectName} 创建 Wpf 核心文件...");
        }
    }
}
