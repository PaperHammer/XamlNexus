using System;
using System.Threading;
using System.Threading.Tasks;
using Winui3_Wpf_XamlNexus.UIComponent.Navigation;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

namespace Winui3_Wpf_XamlNexus.UIComponent.Activation;

public enum ActivationNavigationResult { Navigated, Cancelled, RouteNotFound }

/// <summary>Resolves registered business routes and delegates to the host's guarded navigation.</summary>
public sealed class BusinessActivationNavigator {
    private readonly INavigationRegistry _registry;
    public BusinessActivationNavigator(INavigationRegistry registry) => _registry = registry;

    public async Task<ActivationNavigationResult> NavigateAsync(BusinessActivationRequest request,
        Func<Type, FrameworkPayload, CancellationToken, Task<bool>> navigate,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(navigate);
        cancellationToken.ThrowIfCancellationRequested();
        Type? pageType = _registry.Resolve(request.Route);
        if (pageType is null) return ActivationNavigationResult.RouteNotFound;
        var payload = new FrameworkPayload();
        payload.Set(BusinessActivationRequest.PayloadKey, request);
        return await navigate(pageType, payload, cancellationToken)
            ? ActivationNavigationResult.Navigated : ActivationNavigationResult.Cancelled;
    }
}
