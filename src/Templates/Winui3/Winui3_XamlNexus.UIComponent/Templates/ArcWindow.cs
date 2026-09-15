using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
using Winui3_XamlNexus.Common.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.UIComponent.Utils;
using Winui3_XamlNexus.UIComponent.Utils.Extensions;
using WinUIEx;

namespace Winui3_XamlNexus.UIComponent.Templates {
    public abstract partial class ArcWindow : WindowEx {
        public virtual NavigationView? AppNavView { get; }
        public virtual bool IsMainWindow => false;
        protected virtual bool IsNeedTrack => true;
        public abstract ArcWindowHost ContentHost { get; }
        public abstract ArcWindowManagerKey Key { get; }
        protected PropertyHost PropertyHost => _propertyHost;
        public ObservableCollection<GlobalMsgInfo> InfobarMessages { get; } = [];
        public bool IsActive => _isActive ?? false;

        public ArcWindow(AppTheme appTheme = AppTheme.Auto, AppSystemBackdrop systemBackdrop = default) {
            _propertyHost = new();
            if (IsMainWindow) {
                ArcThemeUtil.SetMainWindowAppTheme(appTheme);
                ArcThemeUtil.SetMainWindowBackdrop(systemBackdrop);
            }

            this.Closed += (_, _) => _isClosed = true;
            this.Activated += ArcWindow_Activated;
            this.Closed += ArcWindow_Closed;
        }

        private void ArcWindow_Activated(object sender, WindowActivatedEventArgs args) {
            bool isActive = args.WindowActivationState != WindowActivationState.Deactivated;
            if (_isActive == isActive) return;
            _isActive = isActive;

            ArcWindowManager.UpdateWindowVisualState(this);
        }

        // Closing can be cancelled by tray behavior; release resources only after a real close.
        private void ArcWindow_Closed(object sender, WindowEventArgs args) {
            this.Activated -= ArcWindow_Activated;
            this.ContentHost.AppRoot.Loaded -= AppRoot_Loaded;
            this.ContentHost.AppRoot.ActualThemeChanged -= Host_ActualThemeChanged;

            if (IsMainWindow) {
                ArcWindowManager.Cleanup();
                ArcThemeUtil.Cleanup();
            }
        }

        private async void AppRoot_Loaded(object sender, RoutedEventArgs e) {
            await SetThemeAsync();
        }

        protected void InitializeWindow() {
            this.ContentHost.AppRoot.Loaded += AppRoot_Loaded;
            this.ContentHost.AppRoot.ActualThemeChanged += Host_ActualThemeChanged;

            if (IsNeedTrack) {
                ArcWindowManager.TrackWindow(Key, this);
            }
            SetWindowStartupPosition();
            SetWindowStyle();
            SetWindowTitleBar();
            UpdateThemeIcon();
            UpdateTheme();
        }

        #region theme
        protected void UpdateThemeFromThemeBtnClick(AppTheme theme) {
            if (!IsMainWindow) return;
            ArcThemeUtil.UpdateThemeGlobal(theme);
        }

        private void UpdateThemeIcon() {
            if (!IsMainWindow) return;

            _propertyHost.ThemeIconKey = ArcThemeUtil.MainWindowAppTheme switch {
                AppTheme.Light => "NaviIcon_ThemeLight",
                AppTheme.Dark => "NaviIcon_ThemeDark",
                _ => "NaviIcon_ThemeAuto"
            };
        }

        public async Task SetThemeAsync() {
            await _themeTransition.WaitAsync();
            var overlay = ContentHost.AppThemeTransitionImage;
            try {
                var root = ContentHost.AppRoot;
                if (_isClosed || !root.IsLoaded || root.ActualWidth <= 0 || root.ActualHeight <= 0) return;
                UpdateThemeIcon();
                // Same snapshot and Composition fade sequence as VirtualPaper.
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(root);
                if (_isClosed) return;
                overlay.Source = bitmap;
                overlay.Visibility = Visibility.Visible;
                overlay.Opacity = 1;
                UpdateTheme();
                var visual = ElementCompositionPreview.GetElementVisual(overlay);
                var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0, 1);
                fade.InsertKeyFrame(1, 0);
                fade.Duration = TimeSpan.FromMilliseconds(600);
                visual.StartAnimation(nameof(visual.Opacity), fade);
                await Task.Delay(600);
            }
            catch (Exception exception) {
                ArcLog.GetLogger<ArcWindow>().Error("Theme transition failed", exception);
                if (!_isClosed) UpdateTheme();
            }
            finally {
                if (!_isClosed) {
                    overlay.Visibility = Visibility.Collapsed;
                    overlay.Source = null;
                }
                _themeTransition.Release();
            }
        }

        private void Host_ActualThemeChanged(FrameworkElement sender, object args) {
            ArcWindowManager.UpdateWindowVisualState(this);
        }

        private void UpdateTheme() {
            ArcThemeUtil.ApplyTheme(this.ContentHost);
            ArcThemeUtil.ApplyTheme(this.ContentHost.AppRoot);
            ArcWindowManager.UpdateWindowVisualState(this);
        }
        #endregion

        #region window property
        protected virtual void SetWindowStartupPosition() {
            DisplayArea displayArea = SystemUtil.GetDisplayArea(this, DisplayAreaFallback.Nearest);
            if (displayArea is not null) {
                var centeredPosition = this.AppWindow.Position;
                centeredPosition.X = (displayArea.WorkArea.Width - this.AppWindow.Size.Width) / 2;
                centeredPosition.Y = (displayArea.WorkArea.Height - this.AppWindow.Size.Height) / 2;
                this.AppWindow.Move(centeredPosition);
            }
        }

        private void SetWindowTitleBar() {
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(this.ContentHost.AppTitleBar);
                this.AppWindow.SetIcon("Assets/xamlnexus.ico");
                this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }
            else {
                this.ContentHost.AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(ArcThemeUtil.MainWindowAppTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            this.SystemBackdrop = ArcThemeUtil.MainWindowBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }
        #endregion

        private readonly SemaphoreSlim _themeTransition = new(1, 1);
        private bool _isClosed;
        private bool? _isActive = null;
        private readonly PropertyHost _propertyHost;
    }

    public partial class PropertyHost : FrameworkElement {
        public string ThemeIconKey {
            get => (string)GetValue(ThemeIconKeyProperty);
            set => SetValue(ThemeIconKeyProperty, value);
        }
        public static readonly DependencyProperty ThemeIconKeyProperty =
            DependencyProperty.Register(nameof(ThemeIconKey), typeof(string),
                typeof(PropertyHost), new PropertyMetadata(null));
    }
}
