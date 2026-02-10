namespace XamlNexus.Utils {
    public static class LanguageUtil {
    }

    public static class LanguageRegistry {
        public static readonly LocaleResources Chinese = new(
            WelcomeMessage: "欢迎使用 XamlNexus —— 您的 XAML 项目脚手架",
            SelectLanguage: "请选择控制台输出语言:",
            SelectFramework: "您想使用哪种 XAML 框架?",
            ProjectNamePrompt: "请输入项目名称:",
            GeneratingStatus: "正在构建您的项目...",
            SuccessMessage: "项目已成功初始化！"
        );

        public static readonly LocaleResources English = new(
            WelcomeMessage: "Welcome to XamlNexus — Your XAML Project Scaffolder",
            SelectLanguage: "Select CLI output language:",
            SelectFramework: "Which XAML framework do you want to use?",
            ProjectNamePrompt: "Enter project name:",
            GeneratingStatus: "Building your project...",
            SuccessMessage: "Project initialized successfully!"
        );
    }

    public record LocaleResources(
        string WelcomeMessage,
        string SelectLanguage,
        string SelectFramework,
        string ProjectNamePrompt,
        string GeneratingStatus,
        string SuccessMessage
    );
}
