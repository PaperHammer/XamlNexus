using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.Common.Utils.Storage;
using Winui3_XamlNexus.Models.Cores;
using Winui3_XamlNexus.Models.Cores.Interfaces;
using Winui3_XamlNexus.Models.Datas.Interfaces;

namespace Winui3_XamlNexus.Models.Datas {
    public class UserSettingsClient : IUserSettingsClient {
        public ISettings Settings { get; private set; } = new Settings();

        public UserSettingsClient() {
            Task.Run(async () => {
                var loadTask = LoadAsync<ISettings>();

                await Task.WhenAll(loadTask);
            }).Wait();
        }

        public async Task LoadAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                try {
                    Settings = await JsonSaver.LoadAsync<Settings>(_settingsPath, SettingsContext.Default);
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
                await JsonSaver.SaveAsync(_settingsPath, Settings, SettingsContext.Default);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        #region Dispose
        private bool _disposed;
        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
            }

            _disposed = true;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly string _settingsPath = Consts.CommonPaths.UserSettingsPath;
    }
}
