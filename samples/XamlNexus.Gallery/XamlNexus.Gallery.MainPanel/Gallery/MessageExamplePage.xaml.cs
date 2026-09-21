using System;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public sealed partial class MessageExamplePage : ArcPage {
    public override Type ArcType => typeof(MessageExamplePage);
    protected override bool IsMultiInstance => true;
    public MessageExamplePage() => InitializeComponent();
    protected override void OnEnter(FrameworkPayload? payload) {
        base.OnEnter(payload);
        ReceivedMessage.Text = payload is not null && payload.TryGet<string>("message", out var message) ? message : string.Empty;
    }
}
