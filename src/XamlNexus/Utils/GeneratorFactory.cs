using XamlNexus.Generators;
using XamlNexus.Generators.Interfaces;
using XamlNexus.Models;

namespace XamlNexus.Utils {
    public static class GeneratorFactory {
        public static IGenerator GetGenerator(FrameworkType type) => type switch {
            FrameworkType.WPF => new WpfAppGenerator(),
            FrameworkType.WinUI3 => new WinUI3AppGenerator(),
            FrameworkType.WinUI3_WPF => new WpfWinUICompositeGenerator(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"不支持的框架类型: {type}")
        };
    }
}
