using System.Globalization;
using Spectre.Console;

namespace XamlNexus.Common.Utils {
    public static class LangKeys {
        public static string SelectFramework { get; } = "SelectFramework";
        public static string ProjectName { get; } = "SlnName";
        public static string OutputPath { get; } = "OutputPath";
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
        public static string Text_ProjectNameEmpty { get; } = "Text_ProjectNameEmpty";
        public static string Text_ProjectNameInvalidChars { get; } = "Text_ProjectNameInvalidChars";
        public static string Text_ProjectNameStartChar { get; } = "Text_ProjectNameStartChar";
        public static string Text_PathEmpty { get; } = "Text_PathEmpty";
        public static string Text_PathInvalidChars { get; } = "Text_PathInvalidChars";
        public static string Text_PathNotAbsolute { get; } = "Text_PathNotAbsolute";
        public static string Text_ProjectNameCapitalized { get; } = "Text_ProjectNameCapitalized";
        public static string Text_Notice { get; } = "Text_Notice";
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

        private static readonly System.Resources.ResourceManager Resources =
            new("XamlNexus.Common.Resources.Strings", typeof(LanguageRegistry).Assembly);

        public static string GetText(string key) => Resources.GetString(key,
            CultureInfo.GetCultureInfo(CurrentLanguage == LanguageType.Chinese ? "zh-CN" : "en")) ?? key;

        public static string GetI18n(string key) => GetText(key).EscapeMarkup();

        /// <summary>在展示边界本地化 Recipe 错误；普通异常及旧式自定义消息保持原样。</summary>
        public static string GetExceptionMessage(Exception exception) => exception is Recipes.XamlNexusRecipeException recipe
            ? recipe.GetLocalizedMessage(CultureInfo.GetCultureInfo(CurrentLanguage == LanguageType.Chinese ? "zh-CN" : "en"))
            : exception is Projects.XamlNexusProjectUpgradeException upgrade
                ? upgrade.GetLocalizedMessage(CultureInfo.GetCultureInfo(CurrentLanguage == LanguageType.Chinese ? "zh-CN" : "en"))
                : exception.Message;

        private static LanguageType AutoDetectLanguage() {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) {
                return LanguageType.Chinese;
            }
            return LanguageType.English;
        }
    }
}
