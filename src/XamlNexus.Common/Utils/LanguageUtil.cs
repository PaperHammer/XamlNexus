using System.Globalization;
using Spectre.Console;

namespace XamlNexus.Common.Utils {
    public static class LangKeys {
        public static string SelectFramework { get; } = "SelectFramework";
        public static string SelectSolutionFormat { get; } = "SelectSolutionFormat";
        public static string NeedTray { get; } = "NeedTray";
        public static string ProjectName { get; } = "SlnName";
        public static string OutputPath { get; } = "OutputPath";
        public static string GeneratingModules { get; } = "GeneratingModules";
        public static string GeneratingSolution { get; } = "GeneratingSolution";
        public static string TemplateWinui3NotFound { get; } = "TemplateWinui3NotFound";
        public static string SuccessMessage { get; set; } = "SuccessMessage";
        public static string Error { get; set; } = "Error";
        public static string ErrorCreateSolution { get; set; } = "ErrorCreateSolution";
        public static string ErrorAddProject { get; set; } = "ErrorAddProject";
        public static string Start { get; set; } = "Start";
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
                [LangKeys.Start] = "开始生成",
                [LangKeys.SelectFramework] = "您想使用哪种 XAML 框架?",
                [LangKeys.SelectSolutionFormat] = "请选择解决方案格式:",
                [LangKeys.NeedTray] = "是否需要托盘图标?",
                [LangKeys.ProjectName] = "解决方案名称",
                [LangKeys.OutputPath] = "输出目录",
                [LangKeys.GeneratingModules] = "正在构建项目模块...",
                [LangKeys.GeneratingSolution] = "正在构建解决方案...",
                [LangKeys.SuccessMessage] = "项目已成功初始化！",
                [LangKeys.Error] = "错误",
                [LangKeys.ErrorCreateSolution] = "创建解决方案失败。路径: {0}",
                [LangKeys.ErrorAddProject] = "无法将项目添加到解决方案: {0}",
                [LangKeys.TemplateWinui3NotFound] = "未找到 WinUI 3 模板。请确保已安装: dotnet workload install windowsappsdk",
            },

            [LanguageType.English] = new() {
                [LangKeys.Start] = "Start generating",
                [LangKeys.SelectFramework] = "Which XAML framework do you want to use?",
                [LangKeys.SelectSolutionFormat] = "Select solution format:",
                [LangKeys.NeedTray] = "Do you need a system tray?",
                [LangKeys.ProjectName] = "Solution Name",
                [LangKeys.OutputPath] = "Output Path",
                [LangKeys.GeneratingModules] = "Building project modules...",
                [LangKeys.GeneratingSolution] = "Building solution...",
                [LangKeys.SuccessMessage] = "Project instantiated successfully!",
                [LangKeys.Error] = "Error",
                [LangKeys.ErrorCreateSolution] = "Failed to create solution at: {0}",
                [LangKeys.ErrorAddProject] = "Failed to add project to solution: {0}",
                [LangKeys.TemplateWinui3NotFound] = "WinUI 3 template not found. Run: dotnet workload install windowsappsdk",
            }
        };

        public static string GetI18n(string key) {
            if (_locales.TryGetValue(CurrentLanguage, out var langDict)) {
                if (langDict.TryGetValue(key, out var val)) return val.EscapeMarkup();
            }
            return key.EscapeMarkup();
        }

        private static LanguageType AutoDetectLanguage() {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) {
                return LanguageType.Chinese;
            }
            return LanguageType.English;
        }
    }
}
