using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils.IPC;
using Winui3_Wpf_XamlNexus.Services.Interfaces;
using Winui3_Wpf_XamlNexus.Grpc.Service.Commands;
using Winui3_Wpf_XamlNexus.Grpc.Service.CommonModels;

namespace Winui3_Wpf_XamlNexus.GrpcServers {
    public class CommandsServer(
            IUIRunnerService runner) : Grpc_CommandsService.Grpc_CommandsServiceBase {
        public override Task<Empty> ShowUI(Empty _, ServerCallContext context) {
            _runner.ShowUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CloseUI(Empty _, ServerCallContext context) {
            _runner.CloseUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RestartUI(Empty _, ServerCallContext context) {
            _runner.RestartUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ShutDown(Empty _, ServerCallContext context) {
            try {
                return Task.FromResult(new Empty());
            }
            finally {
                App.ShutDown();
            }
        }

        public override async Task SubscribeUIRecievedCmd(Empty request, IServerStreamWriter<Grpc_UIRecievedCmd> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var tcs = new TaskCompletionSource<bool>();
                    MessageType message = default;
                    _runner.UISendCmd += UIRecievedCmd;
                    void UIRecievedCmd(object? s, MessageType e) {
                        _runner.UISendCmd -= UIRecievedCmd;
                        message = e;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _runner.UISendCmd -= UIRecievedCmd;
                        break;
                    }

                    await responseStream.WriteAsync(new() {
                        IpcMsg = (int)message,
                    });
                }
            }
            catch (Exception e) {
                ArcLog.GetLogger<CommandsServer>().Error(e);
            }
        }

        private readonly IUIRunnerService _runner = runner;
    }
}
