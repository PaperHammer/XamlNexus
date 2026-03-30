using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces;
using Winui3WpfXamlNexus.Grpc.Service.CommonModels;
using Winui3WpfXamlNexus.Grpc.Service.Update;

namespace Winui3_Wpf_XamlNexus.Grpc.Client {
    public partial class AppUpdaterClient : IAppUpdaterClient {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        public Version AssemblyVersion { get; private set; }
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Notchecked;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public string LastCheckChangelog { get; private set; } = string.Empty;
        public Uri LastCheckUri { get; private set; }
        public Uri LastCheckShaUri { get; private set; }

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
        }

        public async Task CheckUpdateAsync() {
            await _client.CheckUpdateAsync(new Empty());
        }

        public async Task StartDownloadAsync() {
            await _client.StartDownloadAsync(new Empty());
        }

        private async Task UpdateStatusRefresh() {
            var resp = await _client.GetUpdateStatusAsync(new Empty());
            Status = (AppUpdateStatus)((int)resp.Status);
            LastCheckTime = resp.Time.ToDateTime().ToLocalTime();
            LastCheckChangelog = resp.Changelog;
            try {
                LastCheckVersion = string.IsNullOrEmpty(resp.Version) ? null : new Version(resp.Version);
                LastCheckUri = string.IsNullOrEmpty(resp.Uri) ? null : new Uri(resp.Uri);
                LastCheckShaUri = string.IsNullOrEmpty(resp.ShaUri) ? null : new Uri(resp.ShaUri);
            }
            catch { }
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
                    UpdateChecked = null;
                    try {
                        _cancellationTokenUpdateChecked?.Cancel();

                        // 不等待任务完成，仅记录异常
                        _updateCheckedChangedTask?.ContinueWith(t => {
                            if (t.Exception != null)
                                ArcLog.GetLogger<AppUpdaterClient>().Error(t.Exception);
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                    catch (AggregateException ex) { ArcLog.GetLogger<AppUpdaterClient>().Error("Task cancelled during Dispose", ex); }
                    catch (OperationCanceledException) { }

                    _cancellationTokenUpdateChecked?.Dispose();
                    _updateCheckedLock.Dispose();
                    _client = null;
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private Grpc_UpdateService.Grpc_UpdateServiceClient _client;
        private readonly SemaphoreSlim _updateCheckedLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenUpdateChecked;
        private readonly Task _updateCheckedChangedTask;
    }
}
