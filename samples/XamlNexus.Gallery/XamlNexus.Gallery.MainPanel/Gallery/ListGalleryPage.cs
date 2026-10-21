using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ListGalleryPage : ArcPage
{
    public override Type ArcType => typeof(ListGalleryPage);
    private ListExampleView Example => (ListExampleView)Content;
    public ListGalleryPage() {
        InitializeComponent();
        Loaded += async (_, _) => await Example.ActivateAsync();
        Unloaded += (_, _) => Example.Deactivate();
    }
    protected override async void OnEnter(FrameworkPayload? payload) { base.OnEnter(payload); await Example.ActivateAsync(); }
    protected override Task OnPreLeaveAsync() { Example.Deactivate(); return base.OnPreLeaveAsync(); }
    protected override void OnDestroy() { Example.Deactivate(); base.OnDestroy(); }
}
