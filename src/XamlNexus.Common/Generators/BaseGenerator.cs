using Spectre.Console;
using XamlNexus.Models;

namespace XamlNexus.Common.Generators {
    public abstract class BaseGenerator : IGenerator {
        public abstract void Generate(ProjectConfig config);

        protected void WriteFile(string path, string content, Dictionary<string, string> tokens) {
            // 替换占位符，例如把 {{PROJECT_NAME}} 替换为真实名称
            foreach (var token in tokens) {
                content = content.Replace($"{{{{{token.Key}}}}}", token.Value);
            }

            string dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, content);
        }

        protected void GenerateSolution(string path, string name) {
            // 这里可以放一个简化的 .sln 模板字符串
            // ... (逻辑同之前)
        }

        protected void CreateDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
                AnsiConsole.MarkupLine($"  [grey]创建目录:[/] {path}");
            }
        }
    }
}
