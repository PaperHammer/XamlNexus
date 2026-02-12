namespace XamlNexus.Models {
    public class ProjectConfig {
        public string ProjectName { get; set; } = "MyXamlNexusApp";
        public string Language { get; set; } = "C#";
        public FrameworkType Framework { get; set; }
        public bool NeedTray { get; set; }
        public string OutputPath => Path.Combine(Environment.CurrentDirectory, ProjectName);
    }

    public enum FrameworkType {
        WPF, WinUI3, WinUI3_WPF
    }
}
