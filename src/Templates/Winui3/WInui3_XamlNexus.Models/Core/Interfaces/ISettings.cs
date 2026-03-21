using Winui3_XamlNexus.Common;

namespace WInui3_XamlNexus.Models.Core.Interfaces {
    public interface ISettings {
        #region for app
        AppWpRunRulesEnum AppFullscreen { get; set; }
        AppWpRunRulesEnum AppFocus { get; set; }
        AppTheme ApplicationTheme { get; set; }
        AppWpRunRulesEnum BatteryPoweredn { get; set; }
        AppWpRunRulesEnum PowerSaving { get; set; }
        AppWpRunRulesEnum RemoteDesktop { get; set; }
        AppSystemBackdrop SystemBackdrop { get; set; }
        string AppName { get; set; }
        string AppVersion { get; set; }
        string FileVersion { get; set; }
        string Language { get; set; }
        bool IsUpdated { get; set; }
        bool IsAutoStart { get; set; }
        bool IsFirstRun { get; set; }
        string DataSaveDir { get; set; }
        #endregion
    }
}
