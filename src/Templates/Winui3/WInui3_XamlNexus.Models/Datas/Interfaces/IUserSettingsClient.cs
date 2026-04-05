using Winui3_XamlNexus.Models.Cores.Interfaces;

namespace Winui3_XamlNexus.Models.Datas.Interfaces {
    public interface IUserSettingsClient : IDisposable {
        ISettings Settings { get; }
        Task SaveAsync<T>();
        Task LoadAsync<T>();
    }
}
