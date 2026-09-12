using SqliteShowcase.Models.Cores.Interfaces;

namespace SqliteShowcase.Models.Datas.Interfaces {
    public interface IUserSettingsClient : IDisposable {
        ISettings Settings { get; }
        Task SaveAsync<T>();
        Task LoadAsync<T>();
    }
}
