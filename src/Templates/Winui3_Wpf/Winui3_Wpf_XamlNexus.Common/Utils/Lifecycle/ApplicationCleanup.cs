using System;
using System.Threading.Tasks;

namespace Winui3_Wpf_XamlNexus.Common.Utils.Lifecycle;

/// <summary>Runs application cleanup in order without skipping later steps when one fails.</summary>
public static class ApplicationCleanup {
    public static async Task RunAsync(Action<Exception> reportError, params Func<ValueTask>[] steps) {
        ArgumentNullException.ThrowIfNull(reportError);
        foreach (var step in steps) {
            try { await step(); }
            catch (Exception exception) { reportError(exception); }
        }
    }

    public static ValueTask DisposeServicesAsync(IServiceProvider? services) {
        if (services is IAsyncDisposable asyncDisposable) return asyncDisposable.DisposeAsync();
        (services as IDisposable)?.Dispose();
        return ValueTask.CompletedTask;
    }
}
