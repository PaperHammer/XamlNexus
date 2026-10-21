using System;
using System.Threading.Tasks;
using XamlNexus.Gallery.UIComponent.Attributes;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class RegularRetentionPage : RetentionExamplePage {
    public override Type ArcType => typeof(RegularRetentionPage);
    public RegularRetentionPage() { InitializeComponent(); Example.Configure(false); }
}
[KeepAlive]
public sealed partial class CachedRetentionPage : RetentionExamplePage {
    public override Type ArcType => typeof(CachedRetentionPage);
    public CachedRetentionPage() { InitializeComponent(); Example.Configure(true); }
}
public abstract class RetentionExamplePage : ArcPage {
    protected RetentionExampleView Example => (RetentionExampleView)Content;
    // 示例实例由当前导航容器持有，避免被其他容器按类型复用。 / The current navigation container owns each sample instance, preventing reuse by another container based on type.
    protected override bool IsMultiInstance => true;
    protected RetentionExamplePage() {
        Loaded += (_, _) => Example.Trace("Loaded");
        Unloaded += (_, _) => Example.Trace("Unloaded");
    }
    protected override void OnEnter(FrameworkPayload? payload) { base.OnEnter(payload); Example.Trace("OnEnter"); }
    protected override Task OnPreLeaveAsync() { Example.Trace("OnPreLeaveAsync"); return base.OnPreLeaveAsync(); }
    protected override Task OnLeaveAsync() { Example.Trace("OnLeaveAsync"); return base.OnLeaveAsync(); }
    protected override void OnDestroy() { Example.Trace("OnDestroy"); base.OnDestroy(); }
}
