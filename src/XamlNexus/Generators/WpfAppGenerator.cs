using Spectre.Console;
using XamlNexus.Models;

namespace XamlNexus.Generators {
    public class WpfAppGenerator : BaseGenerator {
        public override void Generate(ProjectConfig config) {
            // 模拟输出，后期这里写具体的文件创建逻辑
            AnsiConsole.MarkupLine($"[grey]DEBUG:[/] 正在为 {config.ProjectName} 创建 WPF 核心文件...");
        }
    }
}
