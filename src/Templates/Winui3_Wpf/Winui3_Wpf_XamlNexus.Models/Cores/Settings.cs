using System.Globalization;
using System.Text.Json.Serialization;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;

namespace Winui3_Wpf_XamlNexus.Models.Cores {
    [JsonSerializable(typeof(Settings))]
    [JsonSerializable(typeof(ISettings))]
    public partial class SettingsContext : JsonSerializerContext { }

    public class Settings : ISettings {
        #region for app
        public AppTheme ApplicationTheme { get; set; }
        public AppSystemBackdrop SystemBackdrop { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public bool IsUpdated { get; set; }
        public bool IsAutoStart { get; set; }
        public bool IsFirstRun { get; set; }
        public string DataSaveDir { get; set; } = string.Empty;
        #endregion

        public Settings() {
            AppName = Consts.CoreField.AppName;
            AppVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "";
            IsFirstRun = true;

            ApplicationTheme = AppTheme.Auto;
            IsUpdated = false;
            SystemBackdrop = AppSystemBackdrop.Default;

            DataSaveDir = Consts.CommonPaths.CommonDataDir;

            try {
                Language = CultureInfo.CurrentUICulture.Name;
            }
            catch (ArgumentNullException) {
                Language = "zh-CN";
            }
        }
    }
}
