using SqliteShowcase.Common.Events;
using SqliteShowcase.Common.Updates;

namespace SqliteShowcase.Models.Datas.Interfaces {
    public interface IAppUpdaterClient : IDisposable {
        Version AssemblyVersion { get; }
        string LastCheckChangelog { get; }
        DateTime LastCheckTime { get; }
        Uri? LastCheckUri { get; }
        Uri? LastCheckShaUri { get; }
        Version LastCheckVersion { get; }
        AppUpdateStatus Status { get; }

        event EventHandler<AppUpdaterEventArgs>? UpdateChecked;
        event EventHandler<AppUpdateDownloadProgressEventArgs>? DownloadProgressChanged;
        event EventHandler? InstallerLaunched;

        Task CheckUpdateAsync();
        Task StartDownloadAsync();
        void CancelDownload();
    }
}
