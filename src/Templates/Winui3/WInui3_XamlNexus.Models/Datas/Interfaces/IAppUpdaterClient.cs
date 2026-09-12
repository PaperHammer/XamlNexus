using Winui3_XamlNexus.Common.Events;
using Winui3_XamlNexus.Common.Updates;

namespace Winui3_XamlNexus.Models.Datas.Interfaces {
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
