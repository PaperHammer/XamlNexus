namespace XamlNexus.Common.Generators {
    public interface IGenerator {
        IReadOnlyList<string> GetIncludedModuleIds(string profile) => [];
        bool Generate(ProjectConfig config);
        bool Generate(ProjectConfig config, bool reportSuccess) => Generate(config);
    }
}
