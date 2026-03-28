using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Events;
using Timer = System.Timers.Timer;

namespace Winui3_Wpf_XamlNexus.Core.AppUpdate {
    public sealed class AppUpdaterService : IAppUpdaterService {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        public string LastCheckChangelog { get; private set; } = string.Empty;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Uri LastCheckUri { get; private set; } = null!;
        public Uri LastCheckShaUri { get; private set; } = null!;
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Notchecked;

        public AppUpdaterService() {
            _retryTimer.Elapsed += RetryTimer_Elapsed;
            //giving the retry delay is not reliable since it will reset if system sleeps/suspends.
            _retryTimer.Interval = 5 * 60 * 1000;
        }

        public async Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45000) {
            if (Consts.ApplicationType.IsMSIX) {
                //msix already has built-in _updater.
                return AppUpdateStatus.Notchecked;
            }

            // update "Status, LastCheckUri, LastCheckShaUri, LastCheckVersion, LastCheckChangelog" at hereatus = AppUpdateStatus.Error;

            LastCheckTime = DateTime.Now;

            UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastCheckVersion, LastCheckTime, LastCheckUri, LastCheckShaUri, LastCheckChangelog));
            return Status;
        }

        public async Task<(Uri exeUri, Uri shaUri, Version version, string changelog)> GetLatestRelease(bool isBeta) {
            return default;
        }

        /// <summary>
        /// Check for updates periodically.
        /// </summary>
        public void Start() {
            _retryTimer.Start();
        }

        /// <summary>
        /// Stops periodic updates check.
        /// </summary>
        public void Stop() {
            if (_retryTimer.Enabled) {
                _retryTimer.Stop();
            }
        }

        #region private
        private void RetryTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e) {
            if ((DateTime.Now - LastCheckTime).TotalMilliseconds > (Status != AppUpdateStatus.Error ? _fetchDelayRepeat : _fetchDelayError)) {
                _ = CheckUpdate(0);
            }
        }
        #endregion

        private readonly int _fetchDelayError = 30 * 60 * 1000; //30min
        private readonly int _fetchDelayRepeat = 12 * 60 * 60 * 1000; //12hr
        private readonly Timer _retryTimer = new();
    }
}
