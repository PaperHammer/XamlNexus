using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Updates;

namespace Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces {
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

        Task CheckUpdateAsync();
        Task StartDownloadAsync();
        void CancelDownload();
    }
}
