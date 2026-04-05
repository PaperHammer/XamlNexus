using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Models.Cores;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.Grpc.Service.CommonModels;

namespace Winui3_Wpf_XamlNexus.DataAssistor {
    public static class DataAssist {
        public static Grpc_SettingsData SettingsToGrpc(ISettings settings) {
            var data = new Grpc_SettingsData() {
                ApplicationTheme = (Grpc_AppTheme)settings.ApplicationTheme,
                SystemBackdrop = (Grpc_AppSystemBackdrop)settings.SystemBackdrop,
                AppName = settings.AppName,
                AppVersion = settings.AppVersion,
                Language = settings.Language,
                IsUpdated = settings.IsUpdated,
                IsAutoStart = settings.IsAutoStart,
                IsFirstRun = settings.IsFirstRun,
                DataSaveDir = settings.DataSaveDir,                
            };

            return data;
        }

        public static Settings GrpcToSettings(Grpc_SettingsData settings) {
            var data = new Settings() {
                ApplicationTheme = (AppTheme)settings.ApplicationTheme,
                SystemBackdrop = (AppSystemBackdrop)settings.SystemBackdrop,
                AppName = settings.AppName,
                AppVersion = settings.AppVersion,
                Language = settings.Language,
                IsUpdated = settings.IsUpdated,
                IsAutoStart = settings.IsAutoStart,
                IsFirstRun = settings.IsFirstRun,
                DataSaveDir = settings.DataSaveDir,
            };

            return data;
        }
    }
}
