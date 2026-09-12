using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Updates;

namespace Winui3_Wpf_XamlNexus.Core.AppUpdate {
    public interface IAppUpdaterService : IDisposable {
        event EventHandler<AppUpdaterEventArgs>? UpdateChecked;
        event EventHandler? InstallerLaunched;

        string LastCheckChangelog { get; }
        DateTime LastCheckTime { get; }
        Uri? LastCheckUri { get; }
        Uri? LastCheckShaUri { get; }
        Version LastCheckVersion { get; }
        AppUpdateStatus Status { get; }

        Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45000);
        Task<(Uri exeUri, Uri shaUri, Version version, string changelog)> GetLatestRelease(bool isBeta);
        Task DownloadAndLaunchUpdateAsync(
            CancellationToken cancellationToken = default,
            IProgress<AppUpdateDownloadProgressEventArgs>? progress = null);
        void Start();
        void Stop();
    }
}
