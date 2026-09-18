using XamlNexus.Gallery.Models.Cores.Interfaces;

namespace XamlNexus.Gallery.Models.Datas.Interfaces {
    public interface IUserSettingsClient : IDisposable {
        ISettings Settings { get; }
        Task SaveAsync<T>();
        Task LoadAsync<T>();
    }
}
