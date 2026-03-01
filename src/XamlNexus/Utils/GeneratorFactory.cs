using XamlNexus.Common.Generators;
using XamlNexus.Models;
using XamlNexus.Generator.Winui3App;

namespace XamlNexus.Utils {
    public static class GeneratorFactory {        
        public static IGenerator GetGenerator(FrameworkType type) {
            if (_generators.TryGetValue(type, out var generator))
                return generator();

            throw new NotImplementedException();
        }

        private static readonly Dictionary<FrameworkType, Func<IGenerator>> _generators = new() {
            { FrameworkType.Winui3, () => new Winui3AppGenerator() },
            //{ FrameworkType.Wpf, () => new WpfAppGenerator() },
            //{ FrameworkType.Winui3_Wpf, () => new WpfWinui3CompositeGenerator() },
        };
    }
}
