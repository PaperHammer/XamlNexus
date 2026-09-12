using System;
using Winui3_Wpf_XamlNexus.Common.Utils.DI;
using Winui3_Wpf_XamlNexus.MainPanel.ViewModels;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;

namespace Winui3_Wpf_XamlNexus.MainPanel;

public sealed partial class MainPage : ArcPage {
    public override Type ArcType => typeof(MainPage);
    public MainViewModel ViewModel { get; } = AppObjectFactory.Create<MainViewModel>();
    public MainPage() => InitializeComponent();
}