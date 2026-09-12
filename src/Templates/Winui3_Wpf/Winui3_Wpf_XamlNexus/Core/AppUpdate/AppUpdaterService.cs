using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Updates;
using System.Reflection;
using System.Net.Http;
using System.IO;
using Timer = System.Timers.Timer;

namespace Winui3_Wpf_XamlNexus.Core.AppUpdate {
    public sealed class AppUpdaterService : IAppUpdaterService {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;
        public event EventHandler? InstallerLaunched;

        public string LastCheckChangelog { get; private set; } = string.Empty;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Uri? LastCheckUri { get; private set; }
        public Uri? LastCheckShaUri { get; private set; }
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Notchecked;

        public AppUpdaterService()
            : this(CreateDefaultUpdateSource(), new VerifiedUpdateDownloader(DownloadHttpClient),
                Assembly.GetEntryAssembly()?.GetName().Version
                ?? new Version(0, 0, 0, 0), Consts.Updates.IncludePreview) { }

        public AppUpdaterService(
            IAppUpdateSource? updateSource,
            Version assemblyVersion,
            bool includePreview = false)
            : this(updateSource, new VerifiedUpdateDownloader(DownloadHttpClient), assemblyVersion, includePreview) { }

        public AppUpdaterService(
            IAppUpdateSource? updateSource,
            IAppUpdateDownloader updateDownloader,
            Version assemblyVersion,
            bool includePreview = false) {
            _updateSource = updateSource;
            _updateDownloader = updateDownloader;
            _assemblyVersion = assemblyVersion;
            _includePreview = includePreview;
            _retryTimer.Elapsed += RetryTimer_Elapsed;
            //giving the retry delay is not reliable since it will reset if system sleeps/suspends.
            _retryTimer.Interval = 5 * 60 * 1000;
        }

        public async Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45000) {
            if (Consts.ApplicationType.IsMSIX) {
                //msix already has built-in _updater.
                Status = AppUpdateStatus.Notchecked;
                NotifyUpdateChecked();
                return AppUpdateStatus.Notchecked;
            }

            if (fetchDelay > 0) {
                await Task.Delay(fetchDelay).ConfigureAwait(false);
            }

            try {
                var release = await GetLatestRelease(_includePreview).ConfigureAwait(false);
                LastCheckVersion = release.version;
                LastCheckUri = release.exeUri;
                LastCheckShaUri = release.shaUri;
                LastCheckChangelog = release.changelog;
                Status = AppUpdateVersionEvaluator.Evaluate(_assemblyVersion, release.version);
            }
            catch (Exception exception) {
                Status = AppUpdateStatus.Error;
                ArcLog.GetLogger<AppUpdaterService>().Error("Update check failed", exception);
            }
            finally {
                NotifyUpdateChecked();
            }

            return Status;
        }

        public async Task<(Uri exeUri, Uri shaUri, Version version, string changelog)> GetLatestRelease(bool isBeta) {
            if (_updateSource is null) {
                throw new InvalidOperationException("Configure Consts.Updates.ManifestUrl before checking for updates.");
            }

            var release = await _updateSource.GetLatestReleaseAsync(isBeta).ConfigureAwait(false);
            return (release.DownloadUri, release.Sha256Uri, release.Version, release.Changelog);
        }

        public async Task DownloadAndLaunchUpdateAsync(
            CancellationToken cancellationToken = default,
            IProgress<AppUpdateDownloadProgressEventArgs>? progress = null) {
            if (Status != AppUpdateStatus.Available
                || LastCheckUri is null
                || LastCheckShaUri is null) {
                return;
            }

            try {
                var release = new AppReleaseInfo(
                    LastCheckVersion,
                    LastCheckUri,
                    LastCheckShaUri,
                    LastCheckChangelog);
                var installerPath = await _updateDownloader.DownloadAndVerifyAsync(
                    release,
                    Path.Combine(Consts.CommonPaths.TempDir, "updates"),
                    cancellationToken,
                    progress).ConfigureAwait(false);
                AppUpdateLifecycle.LaunchInstaller(
                    Path.Combine(Consts.CommonPaths.CommonDataDir, "updates"),
                    LastCheckVersion,
                    installerPath);
                InstallerLaunched?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception exception) {
                Status = AppUpdateStatus.Error;
                ArcLog.GetLogger<AppUpdaterService>().Error("Update download or verification failed", exception);
                NotifyUpdateChecked();
                throw;
            }
        }

        /// <summary>
        /// Check for updates periodically.
        /// </summary>
        public void Start() {
            if (_updateSource is null || _retryTimer.Enabled) return;

            _retryTimer.Start();
            _ = CheckUpdate(0);
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

        public void Dispose() {
            Stop();
            _retryTimer.Dispose();
            UpdateChecked = null;
            InstallerLaunched = null;
        }

        private void NotifyUpdateChecked() {
            LastCheckTime = DateTime.Now;
            UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(
                Status, LastCheckVersion, LastCheckTime, LastCheckUri, LastCheckShaUri, LastCheckChangelog));
        }

        private static IAppUpdateSource? CreateDefaultUpdateSource() {
            return Uri.TryCreate(Consts.Updates.ManifestUrl, UriKind.Absolute, out var manifestUri)
                && manifestUri.Scheme == Uri.UriSchemeHttps
                ? new JsonAppUpdateSource(SharedHttpClient, manifestUri)
                : null;
        }
        #endregion

        private readonly int _fetchDelayError = 30 * 60 * 1000; //30min
        private readonly int _fetchDelayRepeat = 12 * 60 * 60 * 1000; //12hr
        private readonly Timer _retryTimer = new();
        private static readonly HttpClient SharedHttpClient = new() {
            Timeout = TimeSpan.FromSeconds(30),
        };
        private static readonly HttpClient DownloadHttpClient = new() {
            Timeout = TimeSpan.FromMinutes(30),
        };
        private readonly IAppUpdateSource? _updateSource;
        private readonly IAppUpdateDownloader _updateDownloader;
        private readonly Version _assemblyVersion;
        private readonly bool _includePreview;
    }
}
