using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winui3_XamlNexus.UI.Activation;

// AppInstance can deliver requests before the UI and modules are ready.
internal sealed class ActivationQueue<T>(Action<Exception> reportError) {
    private readonly object _gate = new();
    private readonly Queue<T> _pending = new();
    private readonly CancellationTokenSource _shutdown = new();
    private Func<T, CancellationToken, Task>? _handle;
    private Func<Action, bool>? _post;
    private bool _scheduled;
    private bool _stopped;

    public void Enqueue(T request) {
        lock (_gate) {
            if (_stopped) return;
            _pending.Enqueue(request);
        }
        Schedule();
    }

    public void Start(Func<T, CancellationToken, Task> handle, Func<Action, bool> post) {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(post);
        lock (_gate) {
            if (_stopped) return;
            if (_handle is not null) throw new InvalidOperationException("Activation handling has already started.");
            _handle = handle;
            _post = post;
        }
        Schedule();
    }

    public void Stop() {
        lock (_gate) {
            if (_stopped) return;
            _stopped = true;
            _pending.Clear();
        }
        // Cancellation callbacks must not run while holding the queue lock.
        try { _shutdown.Cancel(); }
        catch (Exception exception) { reportError(exception); }
    }

    private void Schedule() {
        Func<Action, bool> post;
        lock (_gate) {
            if (_stopped || _scheduled || _handle is null || _pending.Count == 0) return;
            _scheduled = true;
            post = _post!;
        }
        try {
            if (post(Drain)) return;
            throw new InvalidOperationException("The UI dispatcher rejected an activation request.");
        }
        catch (Exception exception) {
            Stop();
            reportError(exception);
        }
    }

    private async void Drain() {
        while (true) {
            T request;
            lock (_gate) {
                if (_stopped || _pending.Count == 0) {
                    _scheduled = false;
                    return;
                }
                request = _pending.Dequeue();
            }
            try { await _handle!(request, _shutdown.Token); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
            catch (Exception exception) { reportError(exception); }
        }
    }
}
