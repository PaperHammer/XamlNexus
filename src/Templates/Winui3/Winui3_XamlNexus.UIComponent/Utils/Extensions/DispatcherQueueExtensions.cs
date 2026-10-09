using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace Winui3_XamlNexus.UIComponent.Utils.Extensions {
    public static class DispatcherQueueExtensions {
        /// <summary>Run on the UI dispatcher and preserve completion, failure and cancellation.</summary>
        public static Task EnqueueOrInvokeAsync(this DispatcherQueue queue, Func<Task> action) {
            ArgumentNullException.ThrowIfNull(queue);
            ArgumentNullException.ThrowIfNull(action);
            if (queue.HasThreadAccess)
                return InvokeAsync(action);

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!queue.TryEnqueue(async () => {
                try {
                    await action();
                    completion.TrySetResult(true);
                }
                catch (OperationCanceledException ex) {
                    completion.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            })) {
                completion.TrySetException(new InvalidOperationException("The UI dispatcher no longer accepts work."));
            }
            return completion.Task;
        }

        private static async Task InvokeAsync(Func<Task> action) => await action();
    }
}
