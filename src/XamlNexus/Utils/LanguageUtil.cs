namespace XamlNexus.Utils {
    public static class LangKeys {
        public const string Welcome = "Welcome";
        public const string SelectFramework = "SelectFramework";
        public const string NeedTray = "NeedTray";
        public const string ProjectName = "ProjectName";
        public const string Generating = "Generating";
        public const string Success = "Success";
        public const string Route = "Route";
    }

    public enum LanguageType {
        Chinese,
        English
    }

    public static class LanguageRegistry {
        public static LanguageType CurrentLanguage { get; set; }

        private static readonly Dictionary<LanguageType, Dictionary<string, string>> _locales = new() {
            [LanguageType.Chinese] = new() {
                [LangKeys.Welcome] = "欢迎使用 XamlNexus —— 您的 XAML 项目脚手架",
                [LangKeys.SelectFramework] = "您想使用哪种 XAML 框架?",
                [LangKeys.NeedTray] = "是否需要托盘?",
                [LangKeys.ProjectName] = "请输入项目名称:",
                [LangKeys.Generating] = "正在构建您的项目...",
                [LangKeys.Success] = "项目已成功初始化！",
                [LangKeys.Route] = "路径",
            },

            [LanguageType.English] = new() {
                [LangKeys.Welcome] = "Welcome to XamlNexus — Your XAML Project Scaffolder",
                [LangKeys.SelectFramework] = "Which XAML framework do you want to use?",
                [LangKeys.NeedTray] = "Do you need a tray?",
                [LangKeys.ProjectName] = "Enter project name:",
                [LangKeys.Generating] = "Building your project...",
                [LangKeys.Success] = "Project initialized successfully!",
                [LangKeys.Route] = "Route",
            }
        };

        public static string GetI18n(string key) {
            if (_locales.TryGetValue(CurrentLanguage, out var langDict)) {
                if (langDict.TryGetValue(key, out var val)) return val;
            }
            return key; 
        }
    }
}
