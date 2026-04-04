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
        public static string SuccessMessage { get; } = "SuccessMessage";
        public static string Error { get; } = "Error";
        public static string ErrorCreateSolution { get; } = "ErrorCreateSolution";
        public static string ErrorAddProject { get; } = "ErrorAddProject";
        public static string Start { get; } = "Start";
        public static string Text_Frontend { get; } = "Text_Frontend";
        public static string Text_Backend { get; } = "Text_Backend";
        public static string Text_Start { get; } = "Text_Start";
        public static string Text_Generating_Module { get; } = "Text_Generating_Module";
        public static string Text_Generating { get; } = "Text_Generating";
        public static string Text_Internal_Error { get; } = "Text_Internal_Error";
        public static string Text_Modules_Generated { get; } = "Text_Modules_Generated";
        public static string Text_Generating_Solution { get; } = "Text_Generating_Solution";
        public static string Text_Fail_To_Create_Sln { get; } = "Text_Fail_To_Create_Sln";
        public static string Text_Linking { get; } = "Text_Linking";
        public static string Text_Fail_To_Link_Project { get; } = "Text_Fail_To_Link_Project";
        public static string Text_Soluton_Created { get; } = "Text_Soluton_Created";
        public static string Text_Property { get; } = "Text_Property";
        public static string Text_Value { get; } = "Text_Value";
        public static string Text_Project { get; } = "Text_Project";
        public static string Text_Framework { get; } = "Text_Framework";
        public static string Text_Format { get; } = "Text_Format";
        public static string Text_OutputPath { get; } = "Text_OutputPath";
        public static string Text_Success { get; } = "Text_Success";
        public static string Text_Error { get; } = "Text_Error";
        public static string Text_Warning { get; } = "Text_Warning";
        public static string Text_OutputFolder_Exists_Using { get; } = "Text_OutputFolder_Exists_Using";
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
                [LangKeys.SelectFramework] = "您想使用哪种基础框架?",
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
                [LangKeys.Text_Frontend] = "前台",
                [LangKeys.Text_Backend] = "后台",

                [LangKeys.Text_Start] = "开始",
                [LangKeys.Text_Generating_Module] = "生成模块",
                [LangKeys.Text_Generating] = "正在生成...",
                [LangKeys.Text_Internal_Error] = "内部异常，尝试更新或重新安装",
                [LangKeys.Text_Modules_Generated] = "模块生成完成",
                [LangKeys.Text_Generating_Solution] = "生成解决方案",
                [LangKeys.Text_Fail_To_Create_Sln] = "解决方案生成失败",
                [LangKeys.Text_Linking] = "链接模块到解决方案...",
                [LangKeys.Text_Fail_To_Link_Project] = "模块链接到解决方案失败",
                [LangKeys.Text_Soluton_Created] = "解决方案生成完成",
                [LangKeys.Text_Property] = "属性",
                [LangKeys.Text_Value] = "值",
                [LangKeys.Text_Project] = "项目名称",
                [LangKeys.Text_Framework] = "基础框架",
                [LangKeys.Text_Format] = "解决方案类型",
                [LangKeys.Text_OutputPath] = "输出位置",
                [LangKeys.Text_Success] = "成功!",
                [LangKeys.Text_Error] = "发生错误: ",
                [LangKeys.Text_Warning] = "警告: ",
                [LangKeys.Text_OutputFolder_Exists_Using] = "输出目录已存在，目录文件夹将使用 {0}",
            },

            [LanguageType.English] = new() {
                [LangKeys.Start] = "Start generating",
                [LangKeys.SelectFramework] = "Which basic framework do you want to use?",
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
                [LangKeys.Text_Frontend] = "Frontend",
                [LangKeys.Text_Backend] = "Backend",

                [LangKeys.Text_Start] = "Start",
                [LangKeys.Text_Generating_Module] = "Generat Modules",
                [LangKeys.Text_Generating] = "Generating...",
                [LangKeys.Text_Internal_Error] = "Internal error, try updating or reinstalling",
                [LangKeys.Text_Modules_Generated] = "All modules generated",
                [LangKeys.Text_Generating_Solution] = "Generat Solution",
                [LangKeys.Text_Fail_To_Create_Sln] = "Solution generation failed",
                [LangKeys.Text_Linking] = "Link module to solution...",
                [LangKeys.Text_Fail_To_Link_Project] = "Module failed to link to the solution",
                [LangKeys.Text_Soluton_Created] = "Solution generation completed",
                [LangKeys.Text_Property] = "Property",
                [LangKeys.Text_Value] = "Value",
                [LangKeys.Text_Project] = "Project Name",
                [LangKeys.Text_Framework] = "Basic framework",
                [LangKeys.Text_Format] = "Sln Type",
                [LangKeys.Text_OutputPath] = "Output path",
                [LangKeys.Text_Success] = "Success!",
                [LangKeys.Text_Error] = "An error occurred: ",
                [LangKeys.Text_Warning] = "Warning: ",
                [LangKeys.Text_OutputFolder_Exists_Using] = "The output directory already exists, the directory folder will use {0}",
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
