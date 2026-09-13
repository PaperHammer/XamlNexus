using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Winui3_Wpf_XamlNexus.AppSettingsPanel.Views;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_Wpf_XamlNexus.AppSettingsPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppSettings : ArcPage {
        public override Type ArcType => typeof(AppSettings);

        public AppSettings() {
            this.InitializeComponent();
            Loaded += (_, _) => {
                // Build the selected settings content after the outer page has loaded.
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => {
                    if (IsLoaded && ContentFrame.Content is null) NavigateSelectedPage();
                });
            };
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _) {
            if (!IsLoaded) return;
            NavigateSelectedPage();
        }

        private void NavigateSelectedPage() {
            var sender = SelBar;
            if (sender.SelectedItem is not SelectorBarItem selectedItem)
                return;

            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            Type? pageType = currentSelectedIndex switch {
                0 => typeof(GeneralSetting),
                1 => typeof(SystemSetting),
                _ => null,
            };
            if (pageType is null)
                return;
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            if (ContentFrame.SourcePageType == pageType) return;
            NavigationTransitionInfo transition = ContentFrame.Content is null
                ? new SuppressNavigationTransitionInfo()
                : new SlideNavigationTransitionInfo { Effect = slideNavigationTransitionEffect };
            ContentFrame.Navigate(pageType, Payload, transition);

            _previousSelectedIndex = currentSelectedIndex;
        }

        private int _previousSelectedIndex = 0;
    }
}
