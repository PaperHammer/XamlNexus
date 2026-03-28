using Winui3_Wpf_XamlNexus.Common.Events;

namespace Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces {
    public interface IAppUpdaterClient : IDisposable {
        Version AssemblyVersion { get; }
        string LastCheckChangelog { get; }
        DateTime LastCheckTime { get; }
        Uri LastCheckUri { get; }
        Version LastCheckVersion { get; }
        AppUpdateStatus Status { get; }

        event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        Task CheckUpdateAsync();
        Task StartDownloadAsync();
    }
}
