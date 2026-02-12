namespace XamlNexus.Models.Attributes {
    [AttributeUsage(AttributeTargets.Class)]
    public class GeneratorAttribute : Attribute {
        public FrameworkType Framework { get; }

        public GeneratorAttribute(FrameworkType framework) => Framework = framework;
    }
}
