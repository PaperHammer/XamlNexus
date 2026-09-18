using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using XamlNexus.Gallery.UIComponent.Attributes;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ListGalleryPage : ListExamplePage
{
    public override Type ArcType => typeof(ListGalleryPage);
    public ListGalleryPage() { InitializeComponent(); Example.Configure(false); }
}
[KeepAlive]
public sealed partial class KeepAliveGalleryPage : ListExamplePage
{
    public override Type ArcType => typeof(KeepAliveGalleryPage);
    public KeepAliveGalleryPage() { InitializeComponent(); Example.Configure(true); }
}
public abstract class ListExamplePage : ArcPage
{
    protected ListExampleView Example => (ListExampleView)Content;
    protected ListExamplePage() { Loaded += async (_, _) => { Example.Trace("Loaded"); await Example.ActivateAsync(); }; Unloaded += (_, _) => { Example.Trace("Unloaded"); Example.Deactivate(); }; }
    protected override async void OnEnter(FrameworkPayload? payload) { base.OnEnter(payload); Example.Trace("OnEnter"); await Example.ActivateAsync(); }
    protected override Task OnPreLeaveAsync() { Example.Trace("OnPreLeaveAsync"); Example.Deactivate(); return base.OnPreLeaveAsync(); }
    protected override Task OnLeaveAsync() { Example.Trace("OnLeaveAsync"); return base.OnLeaveAsync(); }
    protected override void OnDestroy() { Example.Trace("OnDestroy"); Example.Deactivate(); base.OnDestroy(); }
}
