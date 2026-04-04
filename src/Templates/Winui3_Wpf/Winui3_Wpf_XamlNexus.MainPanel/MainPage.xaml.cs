using System;
using Microsoft.UI.Xaml;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_Wpf_XamlNexus.MainPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage {
        public override Type ArcType => typeof(MainPage);

        public MainPage() {
            this.InitializeComponent();

            InitClock();
        }

        private void InitClock() {
            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timer.Tick += (_, __) => {
                var now = DateTime.Now;

                TimeText.Text = now.ToString("HH:mm:ss");
                DateText.Text = now.ToString("yyyy-MM-dd dddd");
            };

            _timer.Start();
        }

        private DispatcherTimer? _timer = null!;
    }
}
