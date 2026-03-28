using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.System;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Common.Events;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.Common.Utils.Files;
using Winui3_XamlNexus.Common.Utils.Localization;
using Winui3_XamlNexus.Common.Utils.Storage;
using Winui3_XamlNexus.Common.Utils.ThreadContext;
using Winui3_XamlNexus.Models.Mvvm;
using Winui3_XamlNexus.UIComponent;
using Winui3_XamlNexus.UIComponent.Utils;
using Winui3_XamlNexus.Models.Datas.Interfaces;
using Winui3_XamlNexus.Models.Cores.Interfaces;

namespace Winui3_XamlNexus.AppSettingsPanel.ViewModels {
    public partial class GeneralSettingViewModel : ObservableObject, IDisposable {
        public event EventHandler? WallpaperInstallDirChanged;

        public bool IsWinStore => Consts.ApplicationType.IsMSIX;

        public string AppVersionText {
            get {
                var ver = "v" + _appUpdater.AssemblyVersion;
                if (Consts.ApplicationType.IsTestBuild)
                    ver += "b";
                else if (Consts.ApplicationType.IsMSIX)
                    ver += $" {LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_Version_MsStore))}";
                return ver;
            }
        }

        public List<string> SystemBackdrops { get; set; } = [];
        public List<LanguagesModel> Languages { get; set; } = [];

        private string _autoStartStatu = string.Empty;
        public string AutoStartStatu {
            get => _autoStartStatu;
            set { _autoStartStatu = value; OnPropertyChanged(); }
        }

        private VersionState _currentVersionState = VersionState.None;
        public VersionState CurrentVersionState {
            get => _currentVersionState;
            set { _currentVersionState = value; OnPropertyChanged(); }
        }

        private string _version_LastCheckDate = string.Empty;
        public string Version_LastCheckDate {
            get => _version_LastCheckDate;
            private set { _version_LastCheckDate = value; OnPropertyChanged(); }
        }

        private string _version = string.Empty;
        public string Version {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private bool _isUpdateBtnEnable = true;
        public bool IsUpdateBtnEnable {
            get => _isUpdateBtnEnable;
            set { _isUpdateBtnEnable = value; OnPropertyChanged(); }
        }

        private bool _isUpdateRingActive = false;
        public bool IsUpdateRingActive {
            get => _isUpdateRingActive;
            set { _isUpdateRingActive = value; OnPropertyChanged(); }
        }

        private float _downloadProgress = 0;
        public float DownloadProgress {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        private string _downloadProgressText = string.Empty;
        public string DownloadProgressText {
            get => _downloadProgressText;
            set { _downloadProgressText = value; OnPropertyChanged(); }
        }

        private bool _isAutoStart;
        public bool IsAutoStart {
            get => _isAutoStart;
            set {
                _isAutoStart = value;
                ChangeAutoShartStatu(value);
                if (_userSettingsClient.Settings.IsAutoStart == value) return;

                _userSettingsClient.Settings.IsAutoStart = value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _seletedSystemBackdropIndx;
        public int SeletedSystemBackdropIndx {
            get => _seletedSystemBackdropIndx;
            set {
                _seletedSystemBackdropIndx = value;
                if (_userSettingsClient.Settings.SystemBackdrop == (AppSystemBackdrop)value) return;

                _userSettingsClient.Settings.SystemBackdrop = (AppSystemBackdrop)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private LanguagesModel _selectedLanguage = null!;
        public LanguagesModel SelectedLanguage {
            get => _selectedLanguage;
            set {
                _selectedLanguage = value;
                if (_userSettingsClient.Settings.Language == value.Language) return;

                if (value.Codes.FirstOrDefault(x => x == _userSettingsClient.Settings.Language) == null) {
                    _userSettingsClient.Settings.Language = value.Codes[0];
                    UpdateSettingsConfigFile();
                    LanguageUtil.LanguageChanged(value.Codes[0]);
                    OnPropertyChanged();
                }
            }
        }

        private string _saveDir = string.Empty;
        public string SaveDir {
            get { return _saveDir; }
            set { _saveDir = value; OnPropertyChanged(); }
        }

        private bool _directoryChangeOngoing;
        public bool DirectoryChangeOngoing {
            get { return _directoryChangeOngoing; }
            set {
                _directoryChangeOngoing = value;
                OnPropertyChanged();
                IsDirectoryChangeEnable = !value;
            }
        }

        private bool _isDirectoryChangeEnable = true;
        public bool IsDirectoryChangeEnable {
            get { return _isDirectoryChangeEnable; }
            set { _isDirectoryChangeEnable = value; OnPropertyChanged(); }
        }

        public ICommand? ChangeFileStorageCommand { get; private set; }
        public ICommand? OpenFileStorageCommand { get; private set; }
        public ICommand? CheckUpdateCommand { get; private set; }
        public ICommand? StartDownloadComand { get; private set; }

        public GeneralSettingViewModel(
            IAppUpdaterClient appUpdater,
            IUserSettingsClient userSettingsClient) {
            _appUpdater = appUpdater;
            _userSettingsClient = userSettingsClient;

            InitText();
            InitCollections();
            InitContent();
            InitCommand();
        }

        private void InitCommand() {
            ChangeFileStorageCommand = new RelayCommand(() => {
                SaveDirectoryChange();
            });
            OpenFileStorageCommand = new RelayCommand(() => {
                OpenFolder();
            });
            CheckUpdateCommand = new RelayCommand(async () => {
                await CheckUpdateAsync();
            });
            StartDownloadComand = new RelayCommand(async () => {
                await StartDownloadAsync();
            });
        }

        private void InitContent() {
            _appUpdater.UpdateChecked += AppUpdater_UpdateChecked;
            _seletedSystemBackdropIndx = (int)_userSettingsClient.Settings.SystemBackdrop;
            _selectedLanguage = SupportedLanguages.GetLanguage(_userSettingsClient.Settings.Language);

            IsAutoStart = _userSettingsClient.Settings.IsAutoStart;
            SaveDir = _userSettingsClient.Settings.DataSaveDir;
        }

        private void InitText() {
            Version_LastCheckDate = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_Version_LastCheckDate));

            _sysbdDefault = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_AppearanceAndAction__sysbdDefault));
            _sysbdMica = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_AppearanceAndAction__sysbdMica));
            _sysbdAcrylic = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_AppearanceAndAction__sysbdAcrylic));
        }

        private void InitCollections() {
            Languages = [.. SupportedLanguages.Languages];
            SystemBackdrops = [_sysbdDefault, _sysbdMica, _sysbdAcrylic];
        }

        private void ChangeAutoShartStatu(bool isAutoStart) {
            if (isAutoStart) {
                AutoStartStatu = LanguageUtil.GetI18n(nameof(Consts.I18n.Text_On));
            }
            else {
                AutoStartStatu = LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Off));
            }
        }

        private async Task CheckUpdateAsync() {
            IsUpdateBtnEnable = false;
            IsUpdateRingActive = true;
            InfoBarVisibilityRestore();

            await _appUpdater.CheckUpdateAsync();

            IsUpdateBtnEnable = true;
            IsUpdateRingActive = false;
        }

        private void InfoBarVisibilityRestore() {
            CurrentVersionState = VersionState.None;
        }

        private void AppUpdater_UpdateChecked(object? sender, AppUpdaterEventArgs e) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                MenuUpdate(e.UpdateStatus, e.UpdateDate, e.UpdateVersion);
            });
        }

        private void MenuUpdate(AppUpdateStatus status, DateTime date, Version version) {
            Version = $"v{version}";
            switch (status) {
                case AppUpdateStatus.Uptodate:
                    CurrentVersionState = VersionState.UptoNewest;
                    break;
                case AppUpdateStatus.Available:
                    Version = $"v{version}";
                    CurrentVersionState = VersionState.FindNew;
                    break;
                case AppUpdateStatus.Invalid or AppUpdateStatus.Error:
                    CurrentVersionState = VersionState.UpdateErr;
                    break;
                default:
                    break;
            }
            Version_LastCheckDate = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_Version_LastCheckDate));
            Version_LastCheckDate += status == AppUpdateStatus.Notchecked ? "" : $" {date}";
        }

        private async Task StartDownloadAsync() {
            IsUpdateBtnEnable = false;

            await _appUpdater.StartDownloadAsync();

            IsUpdateBtnEnable = true;
        }

        private async void SaveDirectoryChange() {
            string? destDir = null;
            DirectoryChangeOngoing = true;

            try {
                destDir = (await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle))?.Path;
                if (string.IsNullOrEmpty(destDir)) return;

                if (destDir == Consts.CommonPaths.AppDataDir) {
                    GlobalMessageUtil.ShowError(nameof(Consts.I18n.Dialog_Content_WallpaperDirectoryChangePathInvalid), isNeedLocalizer: true);
                    return;
                }

                var sourceDir = _userSettingsClient.Settings.DataSaveDir;
                _userSettingsClient.Settings.DataSaveDir = destDir;
                if (!string.Equals(destDir, _userSettingsClient.Settings.DataSaveDir, StringComparison.OrdinalIgnoreCase)) {
                    if (Directory.Exists(sourceDir)) {
                        FileUtil.CopyDirectory(sourceDir, destDir, true);
                        Directory.Delete(sourceDir, true);
                        await _userSettingsClient.SaveAsync<ISettings>();
                    }
                }
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex);
                ArcLog.GetLogger<GeneralSettingViewModel>().Error(ex.Message);
                if (!string.IsNullOrEmpty(destDir)) {
                    FileUtil.EmptyDirectory(destDir);
                }
            }
            finally {
                SaveDir = _userSettingsClient.Settings.DataSaveDir;
                DirectoryChangeOngoing = false;
            }
        }

        private async void OpenFolder() {
            var folder = await StorageFolder.GetFolderFromPathAsync(SaveDir);
            await Launcher.LaunchFolderAsync(folder);
        }

        private async void UpdateSettingsConfigFile() {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        #region dispose
        private bool _disposed = false;
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                _appUpdater.UpdateChecked -= AppUpdater_UpdateChecked;
            }

            _disposed = true;
        }
        #endregion

        private string _sysbdDefault = string.Empty;
        private string _sysbdMica = string.Empty;
        private string _sysbdAcrylic = string.Empty;
        private readonly IAppUpdaterClient _appUpdater;
        private readonly IUserSettingsClient _userSettingsClient;
    }

    public enum VersionState {
        None,              // 无状态
        UptoNewest,        // 已是最新
        FindNew,           // 发现新版本
        Downloading,       // 正在下载
        DownloadFailed,    // 下载失败
        VerifyFailed,      // 校验失败
        Downloaded,        // 下载完成
        UpdateErr          // 网络或更新错误
    }
}
