using System.Globalization;
using System.Text.Json.Serialization;
using SqliteShowcase.Common;
using SqliteShowcase.Models.Cores.Interfaces;

namespace SqliteShowcase.Models.Cores {
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
        public bool RecentEntriesFirst { get; set; }
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
            Language = "zh-CN";
        }
    }
}
