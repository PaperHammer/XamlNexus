namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC;

// Approval means the owned process actually exited. Cancellation/failure leaves the host alive.
public sealed class UICloseCoordinator {
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private Pending? _pending;
    private sealed record Pending(int ProcessId, Guid Id, TaskCompletionSource<bool> Completion);

    public async Task<bool> RequestAsync(int processId, Func<Guid, Task> send,
        CancellationToken cancellationToken = default) {
        if (!await _gate.WaitAsync(0, cancellationToken)) return false;
        var pending = new Pending(processId, Guid.NewGuid(),
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        try {
            lock (_sync) _pending = pending;
            await send(pending.Id);
            return await pending.Completion.Task.WaitAsync(cancellationToken);
        }
        finally {
            lock (_sync) if (ReferenceEquals(_pending, pending)) _pending = null;
            _gate.Release();
        }
    }

    public void ProcessExited(int processId) {
        lock (_sync) if (_pending?.ProcessId == processId) _pending.Completion.TrySetResult(true);
    }

    public bool Receive(int processId, string line) {
        if (!UIHostControl.TryReadResponse(line, out Guid id)) return false;
        lock (_sync) {
            if (_pending?.ProcessId == processId && _pending.Id == id)
                _pending.Completion.TrySetResult(false);
        }
        return true;
    }
}
