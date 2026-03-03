using WInui3_XamlNexus.Models.Core;
using WInui3_XamlNexus.Models.Core.Interfaces;
using WInui3_XamlNexus.Models.Datas.Interfaces;

namespace WInui3_XamlNexus.Models.Datas {
    public class UserSettingsClient : IUserSettingsClient {
        public ISettings Settings { get; private set; } = new Settings();

        public UserSettingsClient() {
            Task.Run(async () => {
                await LoadAsync<ISettings>().ConfigureAwait(false);
                // add new settings initialization logic here if needed
            }).Wait();
        }

        public void Load<T>() {
            if (typeof(T) == typeof(ISettings)) {

            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task LoadAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {

            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>() {
            if (typeof(T) == typeof(ISettings)) {

            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task SaveAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {

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
    }
}
