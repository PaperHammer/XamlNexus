using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winui3_Wpf_XamlNexus.UIComponent.Navigation {
    public enum ArcLeaveReason { Navigation, WindowClose }

    /// <summary>Reject concurrent leave requests so one confirmation owns the decision.</summary>
    public sealed class ArcLeaveRequest {
        private int pending;
        public async Task<bool> TryRunAsync(Func<Task<bool>> request) {
            if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) return false;
            try { return await request(); }
            finally { Volatile.Write(ref pending, 0); }
        }
    }

    /// <summary>Cancel the native close first; replay it only while an approved request owns this scope.</summary>
    public sealed class ArcCloseRequest {
        private readonly ArcLeaveRequest request = new();
        public bool IsReplaying { get; private set; }
        public Task<bool> TryCloseAsync(Func<CancellationToken, Task<bool>> confirm, Action close, CancellationToken cancellationToken = default) =>
            request.TryRunAsync(async () => {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await confirm(cancellationToken)) return false;
                cancellationToken.ThrowIfCancellationRequested();
                IsReplaying = true;
                try { close(); return true; }
                finally { IsReplaying = false; }
            });
    }
}
