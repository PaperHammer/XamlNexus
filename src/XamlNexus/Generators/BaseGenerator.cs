using Spectre.Console;
using XamlNexus.Generators.Interfaces;
using XamlNexus.Models;

namespace XamlNexus.Generators {
    public abstract class BaseGenerator : IGenerator {
        public abstract void Generate(ProjectConfig config);

        protected void CreateDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
                AnsiConsole.MarkupLine($"  [grey]创建目录:[/] {path}");
            }
        }
    }
}
