using System.Globalization;
using Spectre.Console;

namespace XamlNexus.Common.Utils {
    public static class LangKeys {
        public const string Welcome = "Welcome";
        public const string SelectFramework = "SelectFramework";
        public const string SelectSolutionFormat = "SelectSolutionFormat";
        public const string NeedTray = "NeedTray";
        public const string ProjectName = "ProjectName";
        public const string Route = "Route";

        // 生成过程 (Status)
        public const string Generating = "Generating";
        public const string SetupStructure = "SetupStructure";
        public const string ApplyingConfig = "ApplyingConfig";
        public const string ConfigServices = "ConfigServices";
        public const string Finalizing = "Finalizing";

        // 结果与异常
        public const string Success = "Success";
        public const string Location = "Location";
        public const string NextSteps = "NextSteps";
        public const string TemplateNotFound = "TemplateNotFound";
    }

    public enum LanguageType {
        Chinese,
        English
    }

    public static class LanguageRegistry {
        /// <summary>
        /// 当前使用的语言，默认通过系统语言初始化
        /// </summary>
        public static LanguageType CurrentLanguage { get; set; } = AutoDetectLanguage();

        private static readonly Dictionary<LanguageType, Dictionary<string, string>> _locales = new() {
            [LanguageType.Chinese] = new() {
                [LangKeys.Welcome] = "欢迎使用 XamlNexus —— 您的 XAML 项目脚手架",
                [LangKeys.SelectFramework] = "您想使用哪种 XAML 框架?",
                [LangKeys.SelectSolutionFormat] = "请选择项目解决方案格式:",
                [LangKeys.NeedTray] = "是否需要托盘图标?",
                [LangKeys.ProjectName] = "项目名称",
                [LangKeys.Route] = "生成路径",

                [LangKeys.Generating] = "正在构建您的项目...",
                [LangKeys.SetupStructure] = "初始化项目骨架...",
                [LangKeys.ApplyingConfig] = "注入专业版构建配置...",
                [LangKeys.ConfigServices] = "配置应用服务与日志系统...",
                [LangKeys.Finalizing] = "同步解决方案依赖关系...",

                [LangKeys.Success] = "项目已成功初始化",
                [LangKeys.Location] = "存储位置",
                [LangKeys.NextSteps] = "后续操作",
                [LangKeys.TemplateNotFound] = "未找到 WinUI 3 模板。请确保已安装: dotnet workload install windowsappsdk",
            },

            [LanguageType.English] = new() {
                [LangKeys.Welcome] = "Welcome to XamlNexus — Your XAML Project Scaffolder",
                [LangKeys.SelectFramework] = "Which XAML framework do you want to use?",
                [LangKeys.SelectSolutionFormat] = "Select project solution format:",
                [LangKeys.NeedTray] = "Do you need a system tray?",
                [LangKeys.ProjectName] = "Project Name",
                [LangKeys.Route] = "Target Route",

                [LangKeys.Generating] = "Building your project...",
                [LangKeys.SetupStructure] = "Initializing project structure...",
                [LangKeys.ApplyingConfig] = "Injecting professional configurations...",
                [LangKeys.ConfigServices] = "Configuring services and logging...",
                [LangKeys.Finalizing] = "Finalizing workspace dependencies...",

                [LangKeys.Success] = "Project initialized successfully",
                [LangKeys.Location] = "Location",
                [LangKeys.NextSteps] = "Next steps",
                [LangKeys.TemplateNotFound] = "WinUI 3 template not found. Run: dotnet workload install windowsappsdk",
            }
        };

        /// <summary>
        /// 获取国际化文本
        /// </summary>
        public static string GetI18n(string key) {
            if (_locales.TryGetValue(CurrentLanguage, out var langDict)) {
                if (langDict.TryGetValue(key, out var val)) return val.EscapeMarkup();
            }
            // 回退逻辑：如果当前语言没找到，尝试在英文中找
            if (CurrentLanguage != LanguageType.English && _locales[LanguageType.English].TryGetValue(key, out var engVal)) {
                return engVal.EscapeMarkup();
            }
            return key.EscapeMarkup(); // 最终回退：直接返回 key 避免界面空白
        }

        /// <summary>
        /// 自动检测系统语言
        /// </summary>
        private static LanguageType AutoDetectLanguage() {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) {
                return LanguageType.Chinese;
            }
            return LanguageType.English;
        }
    }
}
