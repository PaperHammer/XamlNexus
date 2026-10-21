namespace Winui3_Wpf_XamlNexus.Common.Utils.TaskUtils {
    public class TaskBlocking {
        public IDisposable Block() {
            var registration = new object();

            lock (_lockObj) {
                if (_registrations.Count == 0)
                    _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _registrations.Add(registration);
            }

            return new Unsubscriber(() => {
                lock (_lockObj) {
                    if (_registrations.Remove(registration) && _registrations.Count == 0)
                        _tcs.TrySetResult();
                }
            });
        }

        public Task WaitAsync() {
            lock (_lockObj) {
                if (_registrations.Count == 0)
                    return Task.CompletedTask;

                return _tcs.Task;
            }
        }

        private readonly object _lockObj = new();
        private readonly HashSet<object> _registrations = [];
        private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class Unsubscriber : IDisposable {
        private readonly Action _dispose;
        public Unsubscriber(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}

