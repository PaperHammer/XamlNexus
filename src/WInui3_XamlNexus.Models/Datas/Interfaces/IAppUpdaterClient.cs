using Winui3_XamlNexus.Common.Events;

namespace WInui3_XamlNexus.Models.Datas.Interfaces {
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
