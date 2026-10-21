using System;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public sealed partial class DepartureRetentionPage : ArcPage {
    public override Type ArcType => typeof(DepartureRetentionPage);
    protected override bool IsMultiInstance => true;
    public DepartureRetentionPage() => InitializeComponent();
}
