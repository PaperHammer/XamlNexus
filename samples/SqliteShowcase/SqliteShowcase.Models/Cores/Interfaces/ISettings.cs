using SqliteShowcase.Common;

namespace SqliteShowcase.Models.Cores.Interfaces {
    public interface ISettings {
        #region for app
        AppTheme ApplicationTheme { get; set; }
        AppSystemBackdrop SystemBackdrop { get; set; }
        string AppName { get; set; }
        string AppVersion { get; set; }
        string Language { get; set; }
        bool IsUpdated { get; set; }
        bool IsAutoStart { get; set; }
        bool IsFirstRun { get; set; }
        bool RecentEntriesFirst { get; set; }
        string DataSaveDir { get; set; }
        #endregion
    }
}
