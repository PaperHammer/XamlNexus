using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Utils;

namespace XamlNexus {
    internal class Program {
        static void Main(string[] args) {            
            try {
                BaseConfig.ShowLogo();
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
