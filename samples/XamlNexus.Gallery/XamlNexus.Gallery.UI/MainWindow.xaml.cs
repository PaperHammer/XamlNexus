using System;
using System.Linq;
using System.Collections.Generic;
using XamlNexus.Gallery.MainPanel.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIEx;
using WinRT.Interop;
using XamlNexus.Gallery.Common;
using XamlNexus.Gallery.Common.Logging;
using XamlNexus.Gallery.MainPanel;
using XamlNexus.Gallery.UIComponent;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
using XamlNexus.Gallery.Models.Datas.Interfaces;
using XamlNexus.Gallery.Models.Cores.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace XamlNexus.Gallery.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow {
        public override IWindowSurface ContentHost => this.MainHost;
        private readonly Stack<NavigationViewItem> navigationHistory = new();
        private NavigationViewItem? currentItem;
        private bool goingBack;
        private bool initialSearchFocus;
        public override NavigationView AppNavView => this.NavigationViewControl;
        public override bool IsMainWindow => true;
        public override ArcWindowManagerKey Key => _windowKey;

        public MainWindow(IUserSettingsClient userSettings)
            : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop == AppSystemBackdrop.Acrylic ? AppSystemBackdrop.Acrylic : AppSystemBackdrop.Mica) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.Main);
            this.InitializeComponent();
            this.InitWindowConst();
            base.InitializeWindow();
            MainHost.AppTitleBar.Loaded += (_, _) => UpdateTitleBarRegions();
            MainHost.AppTitleBar.SizeChanged += (_, _) => UpdateTitleBarRegions();
            TitleBarTools.SizeChanged += (_, _) => UpdateTitleBarRegions();
            MainHost.AppRoot.Loaded += (_, _) => MainHost.XamlRoot.Changed += TitleBarRootChanged;


            _userSettings = userSettings;
            MainHost.Menu.Click += (_, _) => NavigationViewControl.IsPaneOpen = !NavigationViewControl.IsPaneOpen;
            MainHost.Back.Click += (_, _) => {
                if (navigationHistory.Count == 0) return;
                goingBack = true;
                NavigationViewControl.SelectedItem = navigationHistory.Pop();
            };
            this.AppWindow.Closing += (_, _) => this.Hide();
            this.Closed += MainWindow_Closed;
            GalleryCatalog.NavigationRequested += NavigateToSample;
            LanguageUtil.LanguageUpdated += GalleryLanguageChanged;
            UpdateGalleryLabels();
            NavigationViewControl.SelectedItem = Nav_Home;
            SampleSearch.Loaded += (_, _) => QueueInitialSearchFocus();
            Activated += (_, e) => { if (e.WindowActivationState != WindowActivationState.Deactivated) QueueInitialSearchFocus(); };

        }

        private void QueueInitialSearchFocus() {
            if (initialSearchFocus) return;
            MainHost.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => {
                if (!initialSearchFocus && SampleSearch.IsLoaded)
                    initialSearchFocus = SampleSearch.Focus(FocusState.Programmatic);
            });
        }
        protected override void SetWindowStartupPosition() {
            var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            if (display is null) return;
            var area = display.WorkArea;
            int width = Math.Min(AppWindow.Size.Width, Math.Max(1, area.Width - 32));
            int height = Math.Min(AppWindow.Size.Height, Math.Max(1, area.Height - 32));
            AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            AppWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2));
        }
        private void TitleBarRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateTitleBarRegions();
        private void UpdateTitleBarRegions() {
            if (!ExtendsContentIntoTitleBar || MainHost.XamlRoot is null) return;
            double scale = MainHost.XamlRoot.RasterizationScale;
            MainHost.SetCaptionInsets(AppWindow.TitleBar.LeftInset / scale, AppWindow.TitleBar.RightInset / scale);
            var regions = new List<Windows.Graphics.RectInt32>();
            foreach (var element in new FrameworkElement[] { TitleBarTools, MainHost.Back, MainHost.Menu }) {
                var bounds = element.TransformToVisual(null).TransformBounds(
                    new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
                if (bounds.Width <= 0 || bounds.Height <= 0) continue;
                regions.Add(new Windows.Graphics.RectInt32((int)Math.Floor(bounds.X * scale), (int)Math.Floor(bounds.Y * scale),
                    (int)Math.Ceiling(bounds.Width * scale), (int)Math.Ceiling(bounds.Height * scale)));
            }
            Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(AppWindow.Id)
                .SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough, regions.ToArray());
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            WindowConsts.WindowHandle = WindowNative.GetWindowHandle(this);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args) {
            GalleryCatalog.NavigationRequested -= NavigateToSample;
            LanguageUtil.LanguageUpdated -= GalleryLanguageChanged;
            if (MainHost.XamlRoot is not null) MainHost.XamlRoot.Changed -= TitleBarRootChanged;
            App.ShutDown();
        }

        #region navigation control
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                string? id = args.SelectedItemContainer?.Tag as string;
                Type? pageType = id switch {
                    "home" => typeof(GalleryHomePage),
                    "settings" => typeof(GallerySettingsPage),
                    "fundamentals" => typeof(WindowNavigationGalleryPage),
                    "capabilities" => typeof(ListsDataGalleryPage),
                    _ => GalleryCatalog.Entries.FirstOrDefault(entry => entry.Id == id)?.PageType,
                };
                if (pageType is null)
                    return;

                if (id is not null) GalleryCatalog.RecordVisit(id);
                NaviContent.Navigate(pageType);
                if (!goingBack && currentItem is not null && currentItem != args.SelectedItemContainer)
                    navigationHistory.Push(currentItem);
                goingBack = false;
                currentItem = args.SelectedItemContainer as NavigationViewItem;
                MainHost.Back.IsEnabled = navigationHistory.Count > 0;
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex, ArcWindowManager.GetArcWindow(Key));
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        private void GalleryLanguageChanged(object? sender, EventArgs args) => UpdateGalleryLabels();
        private void UpdateGalleryLabels() {
            Nav_Home.Content = GalleryStrings.Get("NavigationOverview");
            Nav_Fundamentals.Content = GalleryStrings.Get("NavigationFundamentals");
            Nav_Capabilities.Content = GalleryStrings.Get("NavigationCapabilities");
            Nav_AppSettings.Content = GalleryStrings.Get("Settings");
            ToolTipService.SetToolTip(MainHost.Back, GalleryStrings.Get("Back"));
            ToolTipService.SetToolTip(MainHost.Menu, GalleryStrings.Get("NavigationMenu"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(MainHost.Back, GalleryStrings.Get("Back"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(MainHost.Menu, GalleryStrings.Get("NavigationMenu"));
            SampleSearch.PlaceholderText = GalleryStrings.Get("SearchFeatures");
            foreach (var item in SampleItems()) {
                var entry = GalleryCatalog.Entries.FirstOrDefault(entry => entry.Id == item.Tag as string);
                if (entry is not null) item.Content = entry.Title;
            }
        }
        private IEnumerable<NavigationViewItem> SampleItems() => new[] { Nav_Home, Nav_ToolGuide, Nav_GettingStarted, Nav_ArcWindow, Nav_ArcPage, Nav_Lifetime, Nav_List, Nav_MainPage };
        private void NavigateToSample(string id) {
            var item = id == "settings" ? Nav_AppSettings : SampleItems().FirstOrDefault(item => item.Tag as string == id);
            if (item is null) return;
            if (item == Nav_Fundamentals || item == Nav_Lifetime || item == Nav_ArcWindow || item == Nav_ArcPage) Nav_Fundamentals.IsExpanded = true;
            if (item == Nav_Capabilities || item == Nav_List || item == Nav_MainPage) Nav_Capabilities.IsExpanded = true;
            NavigationViewControl.SelectedItem = item;
        }
        private void SampleSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            string query = sender.Text.Trim();
            sender.ItemsSource = GalleryCatalog.Entries.Where(entry =>
                (entry.Title + entry.Description)
                    .Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        private void SampleSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            var entry = args.ChosenSuggestion as GalleryEntry ?? GalleryCatalog.Entries.FirstOrDefault(entry =>
                entry.Title.Equals(args.QueryText.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entry is not null) {
                NavigateToSample(entry.Id);
                sender.Text = string.Empty;
                sender.ItemsSource = null;
                sender.IsSuggestionListOpen = false;
            }
        }

        public async System.Threading.Tasks.Task SetThemePreferenceAsync(AppTheme theme) {
            AppTheme previous = _userSettings.Settings.ApplicationTheme;
            try {
                _userSettings.Settings.ApplicationTheme = theme;
                UpdateThemeFromThemeBtnClick(theme);
                await _userSettings.SaveAsync<ISettings>();
            }
            catch {
                _userSettings.Settings.ApplicationTheme = previous;
                UpdateThemeFromThemeBtnClick(previous);
                throw;
            }
        }

        private readonly IUserSettingsClient _userSettings;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
