using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.AppLifecycle;

namespace Winui3_XamlNexus.UIComponent.Activation;

/// <summary>
/// Handles initial and redirected Windows activation after module initialization.
/// Registered handlers run serially on the UI thread, in DI registration order.
/// </summary>
public interface IAppActivationHandler {
    Task HandleAsync(AppActivationArguments arguments, CancellationToken cancellationToken);
}
