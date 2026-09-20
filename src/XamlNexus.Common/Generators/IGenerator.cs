namespace XamlNexus.Common.Generators {
    public interface IGenerator {
        IReadOnlyList<string> GetIncludedModuleIds(string profile) => [];
        GenerationResult GenerateProject(ProjectConfig config, Action<GenerationProgress>? progress = null);
        bool Generate(ProjectConfig config) => GenerateProject(config).Success;
    }
}
