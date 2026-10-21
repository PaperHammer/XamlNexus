using System;
using System.Threading;
using System.Threading.Tasks;
using XamlNexus.Gallery.UIComponent.Navigation;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public sealed partial class BusinessPagesGalleryPage : ArcPage {
    public override Type ArcType => typeof(BusinessPagesGalleryPage);
    public BusinessPagesGalleryPage() => InitializeComponent();

    public override Task<bool> CanLeaveAsync(ArcLeaveReason reason, CancellationToken cancellationToken = default) =>
        BusinessWorkflow.CanLeaveAsync(reason, cancellationToken);
}
