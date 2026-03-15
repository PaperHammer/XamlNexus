using System;
using Microsoft.UI.Xaml;
using Winui3_XamlNexus.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_XamlNexus.MainPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage {
        public override Type ArcType => typeof(MainPage);

        public MainPage() {
            this.InitializeComponent();
        }

        private void myButton1_Click(object sender, RoutedEventArgs e) {
            myButton1.Content = "Clicked";
        }
        
        private void myButton2_Click(object sender, RoutedEventArgs e) {
            myButton2.Content = "Clicked";
        }
        
        private void myButton3_Click(object sender, RoutedEventArgs e) {
            myButton3.Content = "Clicked";
        }
    }
}
