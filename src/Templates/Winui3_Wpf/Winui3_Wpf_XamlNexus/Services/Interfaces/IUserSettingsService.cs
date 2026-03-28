using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;

namespace Winui3_Wpf_XamlNexus.Services.Interfaces {
    public interface IUserSettingsService {
        ISettings Settings { get; }
        void Save<T>();
        void Load<T>();
    }
}
