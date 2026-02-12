using XamlNexus.Models;

namespace XamlNexus.Common.Generators {
    public interface IGenerator {
        void Generate(ProjectConfig config);
    }
}
