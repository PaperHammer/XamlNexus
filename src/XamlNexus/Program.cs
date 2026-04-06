using System.Reflection;
using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Utils;

namespace XamlNexus {
    internal class Program {
        static void Main(string[] args) {            
            try {
                var rawVersion = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

                var version = rawVersion?.Split('+')[0] ?? "unknown";

                BaseConfig.ShowLogo(version);
                var config = BaseConfig.BaseComposeConfig();
                var extraConfig = ConfigFactory.GetGenerator(config!.Framework).ExtraComposeConfig();
                config.Merge(extraConfig);

                var generator = GeneratorFactory.GetGenerator(config.Framework);
                generator.Generate(config);
            }
            catch (Exception ex) {
                AnsiConsole.WriteException(ex);
            }
        }
    }
}
