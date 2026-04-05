using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Generator.Winui3_Wpf_App;
using XamlNexus.Generator.Winui3App;

namespace XamlNexus.Utils {
    public static class GeneratorFactory {
        public static IGenerator GetGenerator(FrameworkType type) {
            if (_generators.TryGetValue(type, out var generator))
                return generator();

            throw new NotImplementedException();
        }

        private static readonly Dictionary<FrameworkType, Func<IGenerator>> _generators = new() {
            { FrameworkType.Winui3, () => new Winui3Generator() },
            { FrameworkType.Winui3_Wpf, () => new Winui3_WpfGenerator() },
        };
    }
}
