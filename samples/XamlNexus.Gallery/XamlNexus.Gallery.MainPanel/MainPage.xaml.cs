using System;
using XamlNexus.Gallery.MainPanel.Gallery;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.Common.Logging;
using XamlNexus.Gallery.Models.Cores.Interfaces;
using XamlNexus.Gallery.Models.Datas.Interfaces;
using XamlNexus.Gallery.Common.Utils.DI;
using XamlNexus.Gallery.Data.Models;
using XamlNexus.Gallery.Data.Persistence;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel {
    public sealed partial class MainPage : ArcPage {
        public override Type ArcType => typeof(MainPage);

        public ObservableCollection<AppStateRow> Entries { get; } = [];

        public MainPage() {
            _settings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
            _contextFactory = AppServiceLocator.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            _databaseInitializer = AppServiceLocator.Services.GetRequiredService<SqliteDatabaseInitializer>();
            this.InitializeComponent();
            RecentFirstCheckBox.IsChecked = _settings.Settings.RecentEntriesFirst;
            this.Loaded += async (_, _) => {
                LanguageUtil.LanguageUpdated -= LanguageUpdated;
                LanguageUtil.LanguageUpdated += LanguageUpdated;
                await LoadEntriesAsync();
            };
            this.Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageUpdated;
        }

        private async void Save_Click(object sender, RoutedEventArgs e) {
            if (_busy) return;
            string key = KeyInput.Text.Trim();
            string value = ValueInput.Text;
            if (key.Length is 0 or > 200) {
                SetStatus("Showcase_InvalidKey");
                return;
            }
            await RunAsync("save", async () => {
                await using AppDbContext database = await ContextFactory.CreateDbContextAsync();
                AppStateEntry? entry = await database.AppState.FindAsync(key);
                if (entry is null) {
                    database.AppState.Add(new AppStateEntry { Key = key, Value = value });
                }
                else {
                    entry.Value = value;
                    entry.UpdatedAtUtc = DateTime.UtcNow;
                }
                await database.SaveChangesAsync();
                await RefreshAfterWriteAsync("Showcase_Saved", key);
            });
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadEntriesAsync();

        private async void Delete_Click(object sender, RoutedEventArgs e) {
            if (_busy) return;
            if (EntriesList.SelectedItem is not AppStateRow selected) {
                SetStatus("Showcase_SelectFirst");
                return;
            }
            await RunAsync("delete", async () => {
                await using AppDbContext database = await ContextFactory.CreateDbContextAsync();
                int deleted = await database.AppState.Where(entry => entry.Key == selected.Key).ExecuteDeleteAsync();
                KeyInput.Text = ValueInput.Text = string.Empty;
                await RefreshAfterWriteAsync(deleted == 0 ? "Showcase_Missing" : "Showcase_Deleted", selected.Key);
            });
        }

        private async void Integrity_Click(object sender, RoutedEventArgs e) =>
            await RunAsync("integrity", async () =>
                SetStatus("Showcase_IntegrityResult", await DatabaseInitializer.CheckIntegrityAsync()));

        private void RefreshBackups() {
            BackupList.ItemsSource = DatabaseInitializer.ListBackups();
            BackupList.SelectedIndex = BackupList.Items.Count > 0 ? 0 : -1;
        }

        private async void Backup_Click(object sender, RoutedEventArgs e) =>
            await RunAsync("backup", async () => {
                string path = await Task.Run(() => DatabaseInitializer.CreateBackupAsync());
                RefreshBackups();
                BackupList.SelectedItem = path;
                SetStatus("Showcase_BackupCreated", path);
            });

        private async void Restore_Click(object sender, RoutedEventArgs e) =>
            await RunAsync("restore", async () => {
                if (BackupList.SelectedItem is not string path) {
                    SetStatus("Showcase_SelectBackup");
                    return;
                }
                var dialog = new ContentDialog {
                    XamlRoot = XamlRoot,
                    Title = Text("Showcase_RestoreTitle"),
                    Content = Text("Showcase_RestoreWarning", path),
                    PrimaryButtonText = Text("Showcase_RestoreConfirm"),
                    CloseButtonText = Text("Showcase_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                string safety = await Task.Run(() => DatabaseInitializer.RestoreAsync(path));
                KeyInput.Text = ValueInput.Text = string.Empty;
                RefreshBackups();
                await RefreshAfterWriteAsync("Showcase_Restored", safety);
            });

        private Task LoadEntriesAsync() => RunAsync("refresh", async () => {
            RefreshBackups();
            await LoadEntriesCoreAsync();
            SetStatus("Showcase_Count", Entries.Count);
        });

        private async Task LoadEntriesCoreAsync() {
            await using AppDbContext database = await ContextFactory.CreateDbContextAsync();
            var query = database.AppState.AsNoTracking();
            AppStateEntry[] entries = await (_settings.Settings.RecentEntriesFirst
                ? query.OrderByDescending(entry => entry.UpdatedAtUtc).ThenBy(entry => entry.Key)
                : query.OrderBy(entry => entry.Key)).ToArrayAsync();
            Entries.Clear();
            foreach (AppStateEntry entry in entries) {
                Entries.Add(new AppStateRow {
                    Key = entry.Key,
                    Value = entry.Value,
                    UpdatedAtUtc = entry.UpdatedAtUtc,
                });
            }
        }

        private void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (EntriesList.SelectedItem is not AppStateRow row) return;
            KeyInput.Text = row.Key;
            ValueInput.Text = row.Value;
        }

        private async void RecentFirst_Click(object sender, RoutedEventArgs e) {
            if (_busy) return;
            bool previous = _settings.Settings.RecentEntriesFirst;
            bool requested = RecentFirstCheckBox.IsChecked == true;
            await RunAsync("save-sort", async () => {
                try {
                    _settings.Settings.RecentEntriesFirst = requested;
                    await _settings.SaveAsync<ISettings>();
                }
                catch {
                    _settings.Settings.RecentEntriesFirst = previous;
                    RecentFirstCheckBox.IsChecked = previous;
                    throw;
                }
                await RefreshAfterWriteAsync("Showcase_SortSaved");
            });
        }

        private async Task RefreshAfterWriteAsync(string successKey, params object[] arguments) {
            // A refresh failure must not imply that a committed write was rolled back. / 刷新失败不代表已提交的写入被回滚。
            try {
                await LoadEntriesCoreAsync();
                SetStatus(successKey, arguments);
            }
            catch (Exception exception) {
                ArcLog.GetLogger<MainPage>().Error("Showcase refresh after write failed", exception);
                SetStatus(successKey, arguments);
                _refreshFailed = true;
                RenderStatus();
            }
        }

        private async Task RunAsync(string name, Func<Task> operation) {
            if (_busy) return;
            _busy = true;
            SetControlsEnabled(false);
            BusyIndicator.IsActive = true;
            try {
                await operation();
                ArcLog.GetLogger<MainPage>().Info($"Showcase {name} completed");
            }
            catch (Exception exception) {
                ArcLog.GetLogger<MainPage>().Error($"Showcase {name} failed", exception);
                SetStatus("Showcase_Failed", exception.Message);
            }
            finally {
                BusyIndicator.IsActive = false;
                SetControlsEnabled(true);
                _busy = false;
            }
        }

        private void SetControlsEnabled(bool enabled) {
            foreach (Control control in new Control[] { KeyInput, ValueInput, SaveButton, RefreshButton,
                DeleteButton, IntegrityButton, NotifyButton, BackupButton, RestoreButton, BackupList, EntriesList, RecentFirstCheckBox })
                control.IsEnabled = enabled;
        }

        private async void Notify_Click(object sender, RoutedEventArgs e) =>
            await RunAsync("notification", () => {
                // Resolve at the user action boundary, after tray initialization has completed. / 在用户操作时解析服务，此时托盘初始化已完成。
                AppServiceLocator.Services.GetRequiredService<INotificationService>().ShowNotification(
                    Text("Showcase_NotificationTitle"), Text("Showcase_NotificationBody", Entries.Count));
                SetStatus("Showcase_NotificationRequested");
                return Task.CompletedTask;
            });

        private static string Text(string key, params object[] arguments) =>
            string.Format(LanguageUtil.GetI18n(key), arguments);

        private void SetStatus(string key, params object[] arguments) {
            _statusKey = key;
            _statusArguments = arguments;
            _refreshFailed = false;
            RenderStatus();
        }

        private void LanguageUpdated(object? sender, EventArgs e) {
            RenderStatus();
        }

        private void RenderStatus() {
            if (_statusKey is null) return;
            StatusText.Text = Text(_statusKey, _statusArguments)
                + (_refreshFailed ? " " + Text("Showcase_RefreshFailed") : string.Empty);
        }

        private string? _statusKey;
        private object[] _statusArguments = [];
        private bool _refreshFailed;
        private readonly IUserSettingsClient _settings;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly SqliteDatabaseInitializer _databaseInitializer;
        private bool _busy;
        private IDbContextFactory<AppDbContext> ContextFactory => _contextFactory;
        private SqliteDatabaseInitializer DatabaseInitializer => _databaseInitializer;
    }

    public sealed class AppStateRow {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; }
    }
}
