using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.System;
using SqliteShowcase.Common;
using SqliteShowcase.Common.Events;
using SqliteShowcase.Common.Logging;
using SqliteShowcase.Common.Utils.Files;
using SqliteShowcase.Common.Utils.Localization;
using SqliteShowcase.Common.Utils.Storage;
using SqliteShowcase.Common.Utils.ThreadContext;
using SqliteShowcase.Common.Utils;
using SqliteShowcase.Common.Updates;
using SqliteShowcase.Models.Mvvm;
using SqliteShowcase.UIComponent;
using SqliteShowcase.UIComponent.Utils;
using SqliteShowcase.Models.Datas.Interfaces;
using SqliteShowcase.Models.Cores.Interfaces;

namespace SqliteShowcase.AppSettingsPanel.ViewModels {
    public partial class GeneralSettingViewModel : ObservableObject, IDisposable {
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

        private bool _isDownloadProgressIndeterminate = true;
        public bool IsDownloadProgressIndeterminate {
            get => _isDownloadProgressIndeterminate;
            set { _isDownloadProgressIndeterminate = value; OnPropertyChanged(); }
        }

        private bool _isAutoStart;
        public bool IsAutoStart {
            get => _isAutoStart;
            set {
                if (!IsAutoStartChangeEnabled || _isAutoStart == value) return;
                _isAutoStart = value;
                ChangeAutoShartStatu(value);
                OnPropertyChanged();
                _ = UpdateAutoStartAsync(value);
            }
        }

        private int _seletedSystemBackdropIndx;
        public int SeletedSystemBackdropIndx {
            get => _seletedSystemBackdropIndx;
            set {
                if (_isRefreshingLanguage || !IsBackdropChangeEnabled || value < 0 || value >= SystemBackdrops.Count) return;
                if (_userSettingsClient.Settings.SystemBackdrop == (AppSystemBackdrop)value) return;
                _ = ChangeBackdropAsync(value);
            }
        }

        private bool _isBackdropChangeEnabled = true;
        public bool IsBackdropChangeEnabled {
            get => _isBackdropChangeEnabled;
            private set { _isBackdropChangeEnabled = value; OnPropertyChanged(); }
        }

        private async Task ChangeBackdropAsync(int index) {
            AppSystemBackdrop previous = _userSettingsClient.Settings.SystemBackdrop;
            IsBackdropChangeEnabled = false;
            _seletedSystemBackdropIndx = index;
            _userSettingsClient.Settings.SystemBackdrop = (AppSystemBackdrop)index;
            try {
                await Task.Yield();
                await _userSettingsClient.SaveAsync<ISettings>();
            }
            catch (Exception exception) {
                _userSettingsClient.Settings.SystemBackdrop = previous;
                _seletedSystemBackdropIndx = (int)previous;
                OnPropertyChanged(nameof(SeletedSystemBackdropIndx));
                ArcLog.GetLogger<GeneralSettingViewModel>().Error(exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally {
                IsBackdropChangeEnabled = true;
            }
        }

        private LanguagesModel _selectedLanguage = null!;
        public LanguagesModel SelectedLanguage {
            get => _selectedLanguage;
            set {
                if (value is null || !IsLanguageChangeEnabled || value.Codes.Length == 0) return;
                if (value.Codes.Contains(_userSettingsClient.Settings.Language, StringComparer.OrdinalIgnoreCase)) return;
                _ = ChangeLanguageAsync(value);
            }
        }

        private bool _isLanguageChangeEnabled = true;
        public bool IsLanguageChangeEnabled {
            get => _isLanguageChangeEnabled;
            private set { _isLanguageChangeEnabled = value; OnPropertyChanged(); }
        }

        private async Task ChangeLanguageAsync(LanguagesModel language) {
            LanguagesModel previousSelection = _selectedLanguage;
            string previousLanguage = _userSettingsClient.Settings.Language;
            IsLanguageChangeEnabled = false;
            _selectedLanguage = language;
            _userSettingsClient.Settings.Language = language.Codes[0];
            OnPropertyChanged(nameof(SelectedLanguage));
            try {
                // Let the two-way binding finish before a synchronous save failure can restore its selection.
                await Task.Yield();
                await LanguageUtil.SetLanguageAsync(language.Codes[0]);
                await _userSettingsClient.SaveAsync<ISettings>();
            }
            catch (Exception exception) {
                _selectedLanguage = previousSelection;
                _userSettingsClient.Settings.Language = previousLanguage;
                try {
                    await LanguageUtil.SetLanguageAsync(previousLanguage);
                }
                catch (Exception rollbackException) {
                    ArcLog.GetLogger<GeneralSettingViewModel>().Error("Language rollback failed", rollbackException);
                }
                OnPropertyChanged(nameof(SelectedLanguage));
                ArcLog.GetLogger<GeneralSettingViewModel>().Error("Language settings could not be saved", exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally {
                IsLanguageChangeEnabled = true;
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
        public ICommand? CancelDownloadCommand { get; private set; }

        public GeneralSettingViewModel(
            IAppUpdaterClient appUpdater,
            IUserSettingsClient userSettingsClient) {
            _appUpdater = appUpdater;
            _userSettingsClient = userSettingsClient;

            InitText();
            InitCollections();
            InitContent();
            InitCommand();
            LanguageUtil.LanguageUpdated += OnLanguageUpdated;
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
            CancelDownloadCommand = new RelayCommand(() => _appUpdater.CancelDownload());
        }

        private void InitContent() {
            _appUpdater.UpdateChecked += AppUpdater_UpdateChecked;
            _appUpdater.DownloadProgressChanged += AppUpdater_DownloadProgressChanged;
            _seletedSystemBackdropIndx = (int)_userSettingsClient.Settings.SystemBackdrop;
            _selectedLanguage = SupportedLanguages.GetLanguage(_userSettingsClient.Settings.Language);

            _isAutoStart = _userSettingsClient.Settings.IsAutoStart;
            ChangeAutoShartStatu(_isAutoStart);
            _ = RefreshAutoStartAsync();
            SaveDir = _userSettingsClient.Settings.DataSaveDir;
        }

        private void InitText() {
            Version_LastCheckDate = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_Version_LastCheckDate));
            if (_lastUpdateCheckDate is DateTime lastCheck) Version_LastCheckDate += $" {lastCheck}";

            _sysbdDefault = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_AppearanceAndAction__sysbdDefault));
            _sysbdMica = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_AppearanceAndAction__sysbdMica));
            _sysbdAcrylic = LanguageUtil.GetI18n(nameof(Consts.I18n.Settings_General_AppearanceAndAction__sysbdAcrylic));
        }

        private bool _isRefreshingLanguage;
        private DateTime? _lastUpdateCheckDate;

        private void OnLanguageUpdated(object? sender, EventArgs args) {
            _isRefreshingLanguage = true;
            try {
                InitText();
                SystemBackdrops = [_sysbdDefault, _sysbdMica, _sysbdAcrylic];
                OnPropertyChanged(nameof(SystemBackdrops));
                OnPropertyChanged(nameof(SeletedSystemBackdropIndx));
                ChangeAutoShartStatu(_isAutoStart);
                OnPropertyChanged(nameof(AppVersionText));
            }
            finally {
                _isRefreshingLanguage = false;
            }
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

            try {
                await _appUpdater.CheckUpdateAsync();
            }
            catch (Exception exception) {
                CurrentVersionState = VersionState.UpdateErr;
                ArcLog.GetLogger<GeneralSettingViewModel>().Error("Update check failed", exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally {
                IsUpdateBtnEnable = true;
                IsUpdateRingActive = false;
            }
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
            _lastUpdateCheckDate = status == AppUpdateStatus.Notchecked ? null : date;
            Version_LastCheckDate += _lastUpdateCheckDate is null ? "" : $" {date}";
        }

        private async Task StartDownloadAsync() {
            IsUpdateBtnEnable = false;
            DownloadProgress = 0;
            DownloadProgressText = string.Empty;
            IsDownloadProgressIndeterminate = true;
            CurrentVersionState = VersionState.Downloading;

            try {
                await _appUpdater.StartDownloadAsync();
                CurrentVersionState = VersionState.None;
            }
            catch (OperationCanceledException) {
                CurrentVersionState = VersionState.FindNew;
            }
            catch (Exception exception) {
                CurrentVersionState = VersionState.DownloadFailed;
                ArcLog.GetLogger<GeneralSettingViewModel>().Error("Update download failed", exception);
            }
            finally {
                IsUpdateBtnEnable = true;
            }
        }

        private void AppUpdater_DownloadProgressChanged(
            object? sender,
            AppUpdateDownloadProgressEventArgs e) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                IsDownloadProgressIndeterminate = !e.Percentage.HasValue;
                DownloadProgress = (float)(e.Percentage ?? 0);
                DownloadProgressText = e.TotalBytes is > 0
                    ? $"{FormatBytes(e.BytesReceived)} / {FormatBytes(e.TotalBytes.Value)} ({e.Percentage:0}%)"
                    : FormatBytes(e.BytesReceived);
            });
        }

        private static string FormatBytes(long value) {
            const double megabyte = 1024d * 1024d;
            return $"{value / megabyte:0.0} MB";
        }

        private async void SaveDirectoryChange() {
            if (DirectoryChangeOngoing) return;
            DirectoryChangeOngoing = true;
            string previousDirectory = _userSettingsClient.Settings.DataSaveDir;
            try {
                string? destination = (await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle))?.Path;
                if (string.IsNullOrEmpty(destination)) return;
                if (Path.GetFullPath(destination).Equals(Path.GetFullPath(previousDirectory), StringComparison.OrdinalIgnoreCase)) return;

                await DataDirectoryCopy.CopyAsync(previousDirectory, destination);
                _userSettingsClient.Settings.DataSaveDir = destination;
                await _userSettingsClient.SaveAsync<ISettings>();
            }
            catch (Exception exception) {
                _userSettingsClient.Settings.DataSaveDir = previousDirectory;
                ArcLog.GetLogger<GeneralSettingViewModel>().Error("The file storage directory could not be changed", exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally {
                SaveDir = _userSettingsClient.Settings.DataSaveDir;
                DirectoryChangeOngoing = false;
            }
        }

        private async void OpenFolder() {
            try {
                var folder = await StorageFolder.GetFolderFromPathAsync(SaveDir);
                await Launcher.LaunchFolderAsync(folder);
            }
            catch (Exception exception) {
                ArcLog.GetLogger<GeneralSettingViewModel>().Error(exception);
                GlobalMessageUtil.ShowException(exception);
            }
        }

        private bool _isAutoStartChangeEnabled;
        public bool IsAutoStartChangeEnabled {
            get => _isAutoStartChangeEnabled;
            private set { _isAutoStartChangeEnabled = value; OnPropertyChanged(); }
        }

        private async Task RefreshAutoStartAsync() {
            try {
                StartupRegistration registration = await WindowsAutoStart.CaptureAsync();
                SetAutoStartState(registration.IsEnabled);
                IsAutoStartChangeEnabled = true;
            }
            catch (Exception exception) {
                ArcLog.GetLogger<GeneralSettingViewModel>().Error("Startup state could not be read", exception);
            }
        }

        private void SetAutoStartState(bool enabled) {
            _isAutoStart = enabled;
            _userSettingsClient.Settings.IsAutoStart = enabled;
            ChangeAutoShartStatu(enabled);
            OnPropertyChanged(nameof(IsAutoStart));
        }

        private async Task UpdateAutoStartAsync(bool enabled) {
            bool previousSetting = _userSettingsClient.Settings.IsAutoStart;
            StartupRegistration? previousRegistration = null;
            bool systemChanged = false;
            IsAutoStartChangeEnabled = false;
            try {
                await Task.Yield();
                previousRegistration = await WindowsAutoStart.CaptureAsync();
                bool applied = await WindowsAutoStart.SetEnabledAsync(enabled);
                systemChanged = applied;
                SetAutoStartState(applied ? enabled : previousRegistration.IsEnabled);
                await _userSettingsClient.SaveAsync<ISettings>();
                if (!applied)
                    GlobalMessageUtil.ShowError("Windows blocked changing the startup setting.");
            }
            catch (Exception exception) {
                if (systemChanged && previousRegistration is not null) {
                    try {
                        await WindowsAutoStart.RestoreAsync(previousRegistration);
                    }
                    catch (Exception rollbackException) {
                        ArcLog.GetLogger<GeneralSettingViewModel>().Error("Startup rollback failed", rollbackException);
                        GlobalMessageUtil.ShowException(rollbackException);
                    }
                }
                SetAutoStartState(previousRegistration?.IsEnabled ?? previousSetting);
                ArcLog.GetLogger<GeneralSettingViewModel>().Error(exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally {
                IsAutoStartChangeEnabled = true;
            }
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
                LanguageUtil.LanguageUpdated -= OnLanguageUpdated;
                _appUpdater.UpdateChecked -= AppUpdater_UpdateChecked;
                _appUpdater.DownloadProgressChanged -= AppUpdater_DownloadProgressChanged;
                _appUpdater.CancelDownload();
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
