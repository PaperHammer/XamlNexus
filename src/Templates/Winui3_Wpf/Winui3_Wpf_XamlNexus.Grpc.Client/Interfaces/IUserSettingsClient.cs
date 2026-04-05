using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;

namespace Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces {
    public interface IUserSettingsClient : IDisposable {
        ISettings Settings { get; }
        Task SaveAsync<T>();
        Task LoadAsync<T>();
    }
}
