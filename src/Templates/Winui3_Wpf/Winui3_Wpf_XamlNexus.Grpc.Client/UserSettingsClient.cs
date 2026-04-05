using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.DataAssistor;
using Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces;
using Winui3_Wpf_XamlNexus.Models.Cores;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.Grpc.Service.UserSettings;

namespace Winui3_Wpf_XamlNexus.Grpc.Client {
    public class UserSettingsClient : IUserSettingsClient {
        public ISettings Settings { get; private set; } = new Settings();

        public UserSettingsClient() {
            _client = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(new NamedPipeChannel(".", Consts.CoreField.GrpcPipeServerName));

            Task.Run(async () => {
                var loadTask = LoadAsync<ISettings>();
            
                await Task.WhenAll(loadTask);
            }).Wait();
        }

        public async Task LoadAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                try {
                    Settings = await GetSettingsAsync().ConfigureAwait(false);
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingsClient>().Error(e);
                    Settings = new Settings();
                    await SaveAsync<T>();
                }
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task SaveAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                await SetSettingsAsync().ConfigureAwait(false);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private async Task SetSettingsAsync() {
            _ = await _client.SetSettingsAsync(DataAssist.SettingsToGrpc(Settings));
        }

        private async Task<ISettings> GetSettingsAsync() {
            var resp = await _client.GetSettingsAsync(new Empty());
            return DataAssist.GrpcToSettings(resp);
        }

        #region Dispose
        private bool _disposed;
        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                _client = null;
            }

            _disposed = true;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly string _settingsPath = Consts.CommonPaths.UserSettingsPath;
        private Grpc_UserSettingsService.Grpc_UserSettingsServiceClient _client;
    }
}
