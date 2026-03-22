namespace XamlNexus.Common.Generators {
    public abstract class BaseGenerator : IGenerator {
        public abstract void Generate(ProjectConfig config);

        protected bool IsTextFile(string path) {
            string ext = Path.GetExtension(path).ToLower();
            string[] textExts = { ".cs", ".xaml", ".csproj", ".sln", ".slnx", ".json", ".xml", ".config", ".txt", ".md" };
            return textExts.Contains(ext);
        }
    }
}
