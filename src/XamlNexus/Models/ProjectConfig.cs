namespace XamlNexus.Models {
    public class ProjectConfig {
        public string ProjectName { get; set; } = "MyXamlApp";
        public string Language { get; set; } = "C#";
        public string Framework { get; set; } = "WPF";
        public string OutputPath => Path.Combine(Environment.CurrentDirectory, ProjectName);
    }
}
