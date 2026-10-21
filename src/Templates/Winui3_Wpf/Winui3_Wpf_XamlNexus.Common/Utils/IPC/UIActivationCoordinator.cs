namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC;

// Correlate delivery receipts with the owned child. Receipt means UI queue acceptance.
public sealed class UIActivationCoordinator {
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Pending> _pending = new();
    private sealed record Pending(int ProcessId, TaskCompletionSource<bool> Completion);

    public async Task RequestAsync(int processId, Func<Guid, Task> send,
        CancellationToken cancellationToken = default, TimeSpan? timeout = null) {
        Guid id = Guid.NewGuid();
        var pending = new Pending(processId, new(TaskCreationOptions.RunContinuationsAsynchronously));
        lock (_sync) _pending.Add(id, pending);
        try {
            await send(id);
            bool accepted = await pending.Completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(30), cancellationToken);
            if (!accepted) throw new IOException("The UI exited or rejected activation before accepting it.");
        }
        finally { lock (_sync) _pending.Remove(id); }
    }

    public bool Receive(int processId, string line) {
        if (!UIHostControl.TryReadActivationResponse(line, out Guid id, out bool accepted)) return false;
        lock (_sync) {
            if (_pending.TryGetValue(id, out var pending) && pending.ProcessId == processId)
                pending.Completion.TrySetResult(accepted);
        }
        return true;
    }

    public void ProcessExited(int processId) {
        lock (_sync) foreach (var pending in _pending.Values)
            if (pending.ProcessId == processId) pending.Completion.TrySetResult(false);
    }
}
