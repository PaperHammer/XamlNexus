namespace XamlNexus.Models {
    public class ProjectConfig {
        public string ProjectName { get; set; } = "MyXamlNexusApp";
        public string Language { get; set; } = "C#";
        public FrameworkType Framework { get; set; }
        public SolutionType SlnType { get; set; }
        public bool NeedTray { get; set; }
        public string OutputPath => Path.Combine(Environment.CurrentDirectory, ProjectName);
    }

    public enum FrameworkType {
        Wpf, Winui3, Winui3_Wpf
    }

    public enum SolutionType {
        Sln, Slnx
    }
}
