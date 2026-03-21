using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Generator.Winui3App;

namespace XamlNexus.Utils {
    public static class ConfigFactory {
        public static IConfig GetGenerator(FrameworkType type) {
            if (_configs.TryGetValue(type, out var config))
                return config();

            throw new NotImplementedException();
        }

        private static readonly Dictionary<FrameworkType, Func<IConfig>> _configs = new() {
            { FrameworkType.Winui3, () => new Winui3Config() },
        };
    }
}
