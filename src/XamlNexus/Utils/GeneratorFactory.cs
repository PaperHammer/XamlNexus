using System.Reflection;
using XamlNexus.Common.Generators;
using XamlNexus.Models;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Utils {
    public static class GeneratorFactory {
        private static readonly Dictionary<FrameworkType, IGenerator> _generators = new();

        static GeneratorFactory() {
            RegisterGenerators();
        }

        private static void RegisterGenerators() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var generatorTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IGenerator).IsAssignableFrom(t)
                            && !t.IsInterface
                            && !t.IsAbstract);

            foreach (var type in generatorTypes) {
                if (Activator.CreateInstance(type) is IGenerator generator) {
                    var attribute = type.GetCustomAttribute<GeneratorAttribute>();
                    if (attribute != null) {
                        _generators[attribute.Framework] = generator;
                    }
                }
            }
        }

        public static IGenerator GetGenerator(FrameworkType type) {
            if (_generators.TryGetValue(type, out var generator))
                return generator;

            throw new NotSupportedException($"未找到框架 {type} 的生成器实现。");
        }
    }
}
