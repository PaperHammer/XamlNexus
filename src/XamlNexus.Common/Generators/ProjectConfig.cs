using XamlNexus.Common.Utils;
namespace XamlNexus.Common.Generators {
    public partial class ProjectConfig {
        /// <summary>校验完整输入；不读取终端、不修改配置，也不创建输出目录。</summary>
        public void Validate() {
            if (string.IsNullOrWhiteSpace(SlnName))
                throw new ArgumentException(LanguageRegistry.GetText(LangKeys.Text_ProjectNameEmpty), nameof(SlnName));
            if (!SlnName.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException(LanguageRegistry.GetText(LangKeys.Text_ProjectNameInvalidChars), nameof(SlnName));
            if (!(char.IsLetter(SlnName[0]) || SlnName[0] == '_'))
                throw new ArgumentException(LanguageRegistry.GetText(LangKeys.Text_ProjectNameStartChar), nameof(SlnName));
            if (string.IsNullOrWhiteSpace(OutputPath))
                throw new ArgumentException(LanguageRegistry.GetText(LangKeys.Text_PathEmpty), nameof(OutputPath));
            if (OutputPath.Any(c => Path.GetInvalidPathChars().Contains(c)))
                throw new ArgumentException(LanguageRegistry.GetText(LangKeys.Text_PathInvalidChars), nameof(OutputPath));
            if (!Path.IsPathFullyQualified(OutputPath))
                throw new ArgumentException(LanguageRegistry.GetText(LangKeys.Text_PathNotAbsolute), nameof(OutputPath));
            if (Profile is not ("standard" or "basic"))
                throw new ArgumentException(LanguageRegistry.GetText("Generation_InvalidProfile"), nameof(Profile));
            if (!Enum.IsDefined(Framework) || !Enum.IsDefined(SlnType))
                throw new ArgumentException(LanguageRegistry.GetText("Generation_InvalidFrameworkOrSolution"));
            if (Language is not ("zh-CN" or "en-US"))
                throw new ArgumentException(LanguageRegistry.GetText("Generation_InvalidLanguage"), nameof(Language));
        }

        public string Profile { get; set; } = "standard";
        public string SlnName { get; set; } = GetDefaultProjectName();
        public string Language { get; set; } = "zh-CN";
        public FrameworkType Framework { get; set; }
        public SolutionType SlnType { get; set; }
        public string OutputPath { get; set; } = GetDefaultOutputPath();

        public static string GetDefaultProjectName() => "MyXamlNexusApp";
        public static string GetDefaultOutputPath() {
#if DEBUG
            return Path.Combine(Environment.CurrentDirectory, "debug");
#else
            // 使用专用项目目录，降低生成或清理操作影响用户日常文件的风险。
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "XamlNexus", "Projects");
#endif
        }
    }
}
