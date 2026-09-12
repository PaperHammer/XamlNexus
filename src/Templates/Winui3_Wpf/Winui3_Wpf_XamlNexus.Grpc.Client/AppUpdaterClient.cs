using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Updates;
using Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces;
using Winui3_Wpf_XamlNexus.Grpc.Service.CommonModels;
using Winui3_Wpf_XamlNexus.Grpc.Service.Update;

namespace Winui3_Wpf_XamlNexus.Grpc.Client {
    public partial class AppUpdaterClient : IAppUpdaterClient {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;
        public event EventHandler<AppUpdateDownloadProgressEventArgs>? DownloadProgressChanged;

        public Version AssemblyVersion { get; private set; } = new(0, 0, 0, 0);
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Notchecked;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public string LastCheckChangelog { get; private set; } = string.Empty;
        public Uri? LastCheckUri { get; private set; }
        public Uri? LastCheckShaUri { get; private set; }

        public AppUpdaterClient() {
            _client = new Grpc_UpdateService.Grpc_UpdateServiceClient(new NamedPipeChannel(".", Consts.CoreField.GrpcPipeServerName));            
            _cancellationTokenUpdateChecked = new CancellationTokenSource();

            Task.Run(async () => {
                var coreTask = GetCoreStatsAsync();
                var refreshTask = UpdateStatusRefresh();
                await Task.WhenAll(coreTask, refreshTask);

                var status = await coreTask;
                AssemblyVersion = new Version(status.AssemblyVersion);
            }).Wait();

            _updateCheckedChangedTask = Task.Run(
                () => SubscribeUpdateCheckedStream(_cancellationTokenUpdateChecked.Token));
        }

        public async Task CheckUpdateAsync() {
            await _client.CheckUpdateAsync(new Empty());
        }

        public async Task StartDownloadAsync() {
            var cancellationSource = new CancellationTokenSource();
            if (Interlocked.CompareExchange(ref _downloadCancellationSource, cancellationSource, null) is not null) {
                cancellationSource.Dispose();
                return;
            }

            try {
                using var call = _client.StartDownload(new Empty(), cancellationToken: cancellationSource.Token);
                while (await call.ResponseStream.MoveNext(cancellationSource.Token)) {
                    var value = call.ResponseStream.Current;
                    if (!value.Completed) {
                        DownloadProgressChanged?.Invoke(this, new AppUpdateDownloadProgressEventArgs(
                            value.BytesReceived,
                            value.HasTotalBytes ? value.TotalBytes : null));
                    }
                }
            }
            catch (RpcException exception) when
                (exception.StatusCode == StatusCode.Cancelled && cancellationSource.IsCancellationRequested) {
                throw new OperationCanceledException(cancellationSource.Token);
            }
            finally {
                Interlocked.CompareExchange(ref _downloadCancellationSource, null, cancellationSource);
                cancellationSource.Dispose();
            }
        }

        public void CancelDownload() {
            try {
                Volatile.Read(ref _downloadCancellationSource)?.Cancel();
            }
            catch (ObjectDisposedException) {
                // The download completed while cancellation was being requested.
            }
        }

        private async Task UpdateStatusRefresh() {
            var resp = await _client.GetUpdateStatusAsync(new Empty());
            Status = (AppUpdateStatus)((int)resp.Status);
            LastCheckTime = resp.Time.ToDateTime().ToLocalTime();
            LastCheckChangelog = resp.Changelog;
            LastCheckVersion = Version.TryParse(resp.Version, out var version)
                ? version
                : new Version(0, 0, 0, 0);
            LastCheckUri = Uri.TryCreate(resp.Uri, UriKind.Absolute, out var uri) ? uri : null;
            LastCheckShaUri = Uri.TryCreate(resp.ShaUri, UriKind.Absolute, out var shaUri) ? shaUri : null;
        }

        private async Task SubscribeUpdateCheckedStream(CancellationToken token) {
            try {
                using var call = _client.SubscribeUpdateChecked(new Empty(), cancellationToken: token);
                while (await call.ResponseStream.MoveNext(token)) {
                    await _updateCheckedLock.WaitAsync(token);
                    try {
                        var resp = call.ResponseStream.Current;
                        await UpdateStatusRefresh();
                        UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastCheckVersion, LastCheckTime, LastCheckUri, LastCheckShaUri, LastCheckChangelog));
                    }
                    finally {
                        _updateCheckedLock.Release();
                    }
                }
            }
            catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                return;
            }
            catch (Exception e) {
                ArcLog.GetLogger<AppUpdaterClient>().Error(e);
            }
        }

        private async Task<Grpc_GetCoreStatsResponse> GetCoreStatsAsync() {
            Grpc_GetCoreStatsResponse response = await _client.GetCoreStatsAsync(new Empty());

            return response;
        }

        #region Dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    CancelDownload();
                    UpdateChecked = null;
                    DownloadProgressChanged = null;
                    try {
                        _cancellationTokenUpdateChecked.Cancel();

                        // 不等待任务完成，仅记录异常
                        _updateCheckedChangedTask.ContinueWith(t => {
                            if (t.Exception != null)
                                ArcLog.GetLogger<AppUpdaterClient>().Error(t.Exception);
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                    catch (AggregateException ex) { ArcLog.GetLogger<AppUpdaterClient>().Error("Task cancelled during Dispose", ex); }
                    catch (OperationCanceledException) { }

                    _cancellationTokenUpdateChecked.Dispose();
                    _updateCheckedLock.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly Grpc_UpdateService.Grpc_UpdateServiceClient _client;
        private readonly SemaphoreSlim _updateCheckedLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenUpdateChecked;
        private readonly Task _updateCheckedChangedTask;
        private CancellationTokenSource? _downloadCancellationSource;
    }
}
