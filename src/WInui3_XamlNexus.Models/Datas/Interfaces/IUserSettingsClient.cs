using WInui3_XamlNexus.Models.Core.Interfaces;

namespace WInui3_XamlNexus.Models.Datas.Interfaces {
    public interface IUserSettingsClient : IDisposable {
        ISettings Settings { get; }
        Task SaveAsync<T>();
        void Save<T>();
        Task LoadAsync<T>();
        void Load<T>();
    }
}
