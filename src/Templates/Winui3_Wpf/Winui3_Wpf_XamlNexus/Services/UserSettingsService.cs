using System.Reflection;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils.Storage;
using Winui3_Wpf_XamlNexus.Models.Cores;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.Services.Interfaces;
using Winui3_Wpf_XamlNexus.Utils;

namespace Winui3_Wpf_XamlNexus.Services {
    public class UserSettingsService : IUserSettingsService {
        public ISettings Settings { get; private set; }

        public UserSettingsService() {
            Load<ISettings>();

            //previous installed appversion is different from current instance..    
            if (!Settings.AppVersion.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString(), StringComparison.OrdinalIgnoreCase)) {
                Settings.AppName = Consts.CoreField.AppName;
                Settings.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Settings.IsUpdated = true;
            }

            var lang = SupportedLanguages.GetLanguage(Settings.Language);
            if (lang.Codes.FirstOrDefault(x => x == Settings.Language) == null) {
                Settings.Language = lang.Codes[0];
            }

            try {
                _ = WindowsAutoStart.SetAutoStart(Settings.IsAutoStart);
            }
            catch (Exception e) {
                ArcLog.GetLogger<UserSettingsService>().Error(e);
            }
        }
        public void Load<T>() {
            if (typeof(T) == typeof(ISettings)) {
                try {
                    Settings = JsonSaver.Load<Settings>(_settingsPath, SettingsContext.Default);
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingsService>().Error(e);
                    Settings = new Settings();
                    Save<ISettings>();
                }
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>() {
            if (typeof(T) == typeof(ISettings)) {
                JsonSaver.Save(_settingsPath, Settings, SettingsContext.Default);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private readonly string _settingsPath = Consts.CommonPaths.UserSettingsPath;
        private readonly string _appRulesPath = Consts.CommonPaths.AppRulesPath;
    }
}
