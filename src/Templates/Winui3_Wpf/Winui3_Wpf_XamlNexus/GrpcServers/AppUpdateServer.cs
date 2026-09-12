using System.Reflection;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Updates;
using Winui3_Wpf_XamlNexus.Core.AppUpdate;
using Winui3_Wpf_XamlNexus.Grpc.Service.CommonModels;
using Winui3_Wpf_XamlNexus.Grpc.Service.Update;

namespace Winui3_Wpf_XamlNexus.GrpcServers {
    public class AppUpdateServer(
        IAppUpdaterService updater) : Grpc_UpdateService.Grpc_UpdateServiceBase {
        public override async Task<Empty> CheckUpdate(Empty _, ServerCallContext context) {
            await _updater.CheckUpdate(0);

            return await Task.FromResult(new Empty());
        }

        public override async Task StartDownload(
            Empty _,
            IServerStreamWriter<Grpc_UpdateDownloadProgress> responseStream,
            ServerCallContext context) {
            if (_updater.Status != AppUpdateStatus.Available
                || _updater.LastCheckUri is null
                || _updater.LastCheckShaUri is null) {
                return;
            }

            var progressChannel = System.Threading.Channels.Channel.CreateUnbounded<AppUpdateDownloadProgressEventArgs>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            var progress = new InlineProgress<AppUpdateDownloadProgressEventArgs>(value =>
                progressChannel.Writer.TryWrite(value));
            var downloadTask = DownloadAsync();

            try {
                await foreach (var value in progressChannel.Reader.ReadAllAsync(context.CancellationToken)) {
                    await responseStream.WriteAsync(ToGrpcProgress(value));
                }

                await downloadTask;
                await responseStream.WriteAsync(new Grpc_UpdateDownloadProgress { Completed = true });
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested) {
                // The requesting UI disconnected or cancelled the download.
                try {
                    await downloadTask;
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested) {
                }
            }
            catch (Exception exception) {
                ArcLog.GetLogger<AppUpdateServer>().Error("Update download failed", exception);
                throw new RpcException(new Status(StatusCode.Internal, "The update could not be downloaded or verified."));
            }

            async Task DownloadAsync() {
                try {
                    await _updater.DownloadAndLaunchUpdateAsync(context.CancellationToken, progress);
                }
                finally {
                    progressChannel.Writer.TryComplete();
                }
            }
        }

        public override Task<Grpc_UpdateResponse> GetUpdateStatus(Empty _, ServerCallContext context) {
            return Task.FromResult(new Grpc_UpdateResponse() {
                Status = (Grpc_UpdateStatus)((int)_updater.Status),
                Changelog = _updater.LastCheckChangelog ?? string.Empty,
                Uri = _updater.LastCheckUri?.OriginalString ?? string.Empty,
                ShaUri = _updater.LastCheckShaUri?.OriginalString ?? string.Empty,
                Version = _updater.LastCheckVersion.ToString() ?? string.Empty,
                Time = Timestamp.FromDateTime(_updater.LastCheckTime.ToUniversalTime()),
            });
        }

        public override async Task SubscribeUpdateChecked(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var tcs = new TaskCompletionSource<bool>();
                    _updater.UpdateChecked += Updater_UpdateChecked;
                    void Updater_UpdateChecked(object? sender, AppUpdaterEventArgs e) {
                        _updater.UpdateChecked -= Updater_UpdateChecked;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _updater.UpdateChecked -= Updater_UpdateChecked;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e) {
                ArcLog.GetLogger<AppUpdateServer>().Error(e);
            }
        }

        public override Task<Grpc_GetCoreStatsResponse> GetCoreStats(Empty _, ServerCallContext context) {
            return Task.FromResult(new Grpc_GetCoreStatsResponse() {
                AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
            });
        }

        private readonly IAppUpdaterService _updater = updater;

        private static Grpc_UpdateDownloadProgress ToGrpcProgress(AppUpdateDownloadProgressEventArgs value) {
            return new Grpc_UpdateDownloadProgress {
                BytesReceived = value.BytesReceived,
                TotalBytes = value.TotalBytes ?? 0,
                HasTotalBytes = value.TotalBytes.HasValue,
                Percentage = value.Percentage ?? 0,
            };
        }

        private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T> {
            public void Report(T value) => callback(value);
        }
    }
}
