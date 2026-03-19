using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.Common.Utils;
using Winui3_XamlNexus.Common.Utils.Storage;
using Winui3_XamlNexus.Models.Mvvm;
using Winui3_XamlNexus.UIComponent;
using Winui3_XamlNexus.UIComponent.Utils;

namespace Winui3_XamlNexus.AppSettingsPanel.ViewModels {
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
                    GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                }
            }
        }
    }
}
