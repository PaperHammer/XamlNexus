using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GalleryIntegrationView : UserControl
{
    private bool ready;
    public GalleryIntegrationView() { InitializeComponent(); ready = true; Update(); Loaded += (_, _) => { LanguageUtil.LanguageUpdated += Changed; Update(); }; Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= Changed; }
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(GalleryIntegrationView), new PropertyMetadata("List", (d, e) => ((GalleryIntegrationView)d).Update()));
    private void Changed(object? sender, EventArgs e) => Update();
    private void Start_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate("getting-started");
    private void Update()
    {
        if (!ready) return;
        bool data = Kind == "Data", cached = Kind == "KeepAlive";
        Step1.Text = GalleryStrings.Get(data ? "DataStep1" : "ListStep1");
        Step2.Text = GalleryStrings.Get(data ? "DataStep2" : "ListStep2");
        Check.Text = GalleryStrings.Get(data ? "DataCheck" : cached ? "CachedCheck" : "ListCheck");
        KeepAliveSection.Visibility = cached ? Visibility.Visible : Visibility.Collapsed;
        Commands.SetSource(data ? "xamlnexus add sqlite --dry-run\nxamlnexus add sqlite\nxamlnexus doctor\nxamlnexus run" : "xamlnexus page add Projects --kind list --dry-run\nxamlnexus page add Projects --kind list\nxamlnexus run", "commands");
        KeepAliveCode.SetSource("using MyApp.UIComponent.Attributes;\n\n[KeepAlive]\npublic sealed partial class ProjectsPage : ArcPage\n{\n    // ...\n}", "snippet.cs");
    }
}
