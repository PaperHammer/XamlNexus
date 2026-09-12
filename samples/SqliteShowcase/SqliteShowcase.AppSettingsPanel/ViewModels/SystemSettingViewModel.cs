using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using SqliteShowcase.Common.Logging;
using SqliteShowcase.Common.Utils;
using SqliteShowcase.Common.Utils.Storage;
using SqliteShowcase.Models.Mvvm;
using SqliteShowcase.UIComponent;
using SqliteShowcase.UIComponent.Utils;

namespace SqliteShowcase.AppSettingsPanel.ViewModels {
    public partial class SystemSettingViewModel {
        public ICommand? LogCommand { get; set; }

        public SystemSettingViewModel() {
            InitCommand();
        }

        private void InitCommand() {
            LogCommand = new RelayCommand(async () => {
                await ExportLogsAsync();
            });
        }

        private async Task ExportLogsAsync() {
            var saveFile = await WindowsStoragePickers.PickSaveFileAsync(
                WindowConsts.WindowHandle,
                "Winui3XamlNexus_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                new System.Collections.Generic.Dictionary<string, string[]>() {
                    ["Compressed archive"] = [".zip"]
                }
            );

            if (saveFile != null) {
                try {
                    LogUtil.ExportLogFiles(saveFile.Path);
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<SystemSettingViewModel>().Error(ex);
                    GlobalMessageUtil.ShowException(ex);
                }
            }
        }
    }
}
