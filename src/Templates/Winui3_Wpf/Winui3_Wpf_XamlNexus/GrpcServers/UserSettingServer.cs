using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.Services.Interfaces;
using Winui3_Wpf_XamlNexus.Utils;
using Winui3_Wpf_XamlNexus.Grpc.Service.CommonModels;
using Winui3_Wpf_XamlNexus.Grpc.Service.UserSettings;

namespace Winui3_Wpf_XamlNexus.GrpcServers {
    public class UserSettingServer(
        IUserSettingsService userSetting,
        IUIRunnerService uiRunner) : Grpc_UserSettingsService.Grpc_UserSettingsServiceBase {
        public override Task<Grpc_SettingsData> GetSettings(Empty request, ServerCallContext context) {
            var resp = new Grpc_SettingsData() {
                AppName = _userSetting.Settings.AppName,
                AppVersion = _userSetting.Settings.AppVersion,
                IsFirstRun = _userSetting.Settings.IsFirstRun,
                DataSaveDir = _userSetting.Settings.DataSaveDir,
                IsAutoStart = _userSetting.Settings.IsAutoStart,
                ApplicationTheme = (Grpc_AppTheme)_userSetting.Settings.ApplicationTheme,
                Language = _userSetting.Settings.Language,
                IsUpdated = _userSetting.Settings.IsUpdated,
                SystemBackdrop = (Grpc_AppSystemBackdrop)_userSetting.Settings.SystemBackdrop,
            };

            return Task.FromResult(resp);
        }

        public override Task<Empty> SetSettings(Grpc_SettingsData request, ServerCallContext context) {
            bool restartRequired =
                request.Language != _userSetting.Settings.Language
                || (Common.AppSystemBackdrop)request.SystemBackdrop != _userSetting.Settings.SystemBackdrop;

            if (request.IsAutoStart != _userSetting.Settings.IsAutoStart) {
                _userSetting.Settings.IsAutoStart = request.IsAutoStart;
                try {
                    _ = WindowsAutoStart.SetAutoStart(_userSetting.Settings.IsAutoStart);
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingServer>().Error(e);
                }
            }

            if ((Common.AppTheme)request.ApplicationTheme != _userSetting.Settings.ApplicationTheme) {
                App.ChangeTheme((Common.AppTheme)request.ApplicationTheme);
            }

            if (request.Language != _userSetting.Settings.Language) {
                App.ChangeLanguage(request.Language);
            }

            _userSetting.Settings.AppName = request.AppName;
            _userSetting.Settings.AppVersion = request.AppVersion;
            _userSetting.Settings.IsFirstRun = request.IsFirstRun;
            _userSetting.Settings.DataSaveDir = request.DataSaveDir;
            _userSetting.Settings.IsAutoStart = request.IsAutoStart;
            _userSetting.Settings.ApplicationTheme = (Common.AppTheme)request.ApplicationTheme;
            _userSetting.Settings.Language = request.Language;
            _userSetting.Settings.IsUpdated = request.IsUpdated;
            _userSetting.Settings.SystemBackdrop = (Common.AppSystemBackdrop)request.SystemBackdrop;

            try {
                return Task.FromResult(new Empty());
            }
            finally {
                lock (settingsWriteLock) {
                    _userSetting.Save<ISettings>();
                    if (restartRequired) {
                        _uiRunner.RestartUI();
                    }
                }
            }
        }

        private readonly IUserSettingsService _userSetting = userSetting;
        private readonly IUIRunnerService _uiRunner = uiRunner;
        private readonly object settingsWriteLock = new();
    }
}
