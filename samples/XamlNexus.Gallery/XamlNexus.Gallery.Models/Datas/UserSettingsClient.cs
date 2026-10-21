using XamlNexus.Gallery.Common;
using XamlNexus.Gallery.Common.Utils.Storage;
using XamlNexus.Gallery.Models.Cores;
using XamlNexus.Gallery.Models.Cores.Interfaces;
using XamlNexus.Gallery.Models.Datas.Interfaces;

namespace XamlNexus.Gallery.Models.Datas {
    public class UserSettingsClient : IUserSettingsClient {
        public ISettings Settings { get; private set; } = new Settings();

        public UserSettingsClient() {
            Task.Run(() => LoadAsync<ISettings>()).GetAwaiter().GetResult();
        }

        public async Task LoadAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                await _saveLock.WaitAsync();
                try {
                    Settings = await JsonSaver.LoadOrCreateAsync(_settingsPath, SettingsContext.Default,
                        () => new Settings());
                }
                finally {
                    _saveLock.Release();
                }
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task SaveAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                await _saveLock.WaitAsync();
                try {
                    await JsonSaver.SaveAsync(_settingsPath, Settings, SettingsContext.Default);
                }
                finally {
                    _saveLock.Release();
                }
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
        private readonly SemaphoreSlim _saveLock = new(1, 1);
    }
}
