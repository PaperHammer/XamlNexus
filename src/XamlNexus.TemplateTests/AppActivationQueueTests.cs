using Winui3_XamlNexus.UI.Activation;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class AppActivationQueueTests {
    [Fact]
    public void RequestsBeforeStartupAreRetainedAndDeliveredInOrder() {
        var delivered = new List<string>();
        var scheduled = new Queue<Action>();
        var queue = new ActivationQueue<string>(exception => throw exception);
        queue.Enqueue("initial");
        queue.Enqueue("second launch");
        Assert.Empty(delivered);
        queue.Start((request, _) => { delivered.Add(request); return Task.CompletedTask; },
            action => { scheduled.Enqueue(action); return true; });
        queue.Enqueue("third launch");
        Assert.Single(scheduled);
        scheduled.Dequeue()();
        Assert.Equal(new[] { "initial", "second launch", "third launch" }, delivered);
        queue.Enqueue("later launch");
        Assert.Single(scheduled);
        scheduled.Dequeue()();
        Assert.Equal("later launch", delivered.Last());
    }

    [Fact]
    public async Task AsyncHandlersDoNotOverlapAndNewRequestsJoinThePendingQueue() {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new List<int>();
        Action? drain = null;
        var queue = new ActivationQueue<int>(exception => throw exception);
        queue.Start(async (request, _) => {
            delivered.Add(request);
            if (request == 1) await first.Task;
            if (request == 2) second.SetResult();
        }, action => { drain = action; return true; });
        queue.Enqueue(1);
        drain!();
        queue.Enqueue(2);
        Assert.Equal(new[] { 1 }, delivered);
        first.SetResult();
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 2 }, delivered);
    }

    [Fact]
    public void FailedRequestIsReportedAndDoesNotBlockTheNextRequest() {
        var errors = new List<Exception>();
        var delivered = new List<int>();
        var queue = new ActivationQueue<int>(errors.Add);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Start((request, _) => {
            if (request == 1) throw new InvalidOperationException("bad activation");
            delivered.Add(request);
            return Task.CompletedTask;
        }, action => { action(); return true; });
        Assert.Equal("bad activation", Assert.Single(errors).Message);
        Assert.Equal(new[] { 2 }, delivered);
    }

    [Fact]
    public async Task ShutdownCancelsTheActiveRequestAndDiscardsPendingAndLaterRequests() {
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new List<int>();
        var errors = new List<Exception>();
        Action? drain = null;
        var queue = new ActivationQueue<int>(errors.Add);
        queue.Start(async (request, token) => {
            delivered.Add(request);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { canceled.SetResult(); }
        }, action => { drain = action; return true; });
        queue.Enqueue(1);
        drain!();
        queue.Enqueue(2);
        queue.Stop();
        queue.Enqueue(3);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1 }, delivered);
        Assert.Empty(errors);
    }

    [Fact]
    public void ShutdownBeforeReadinessPreventsDispatch() {
        var queue = new ActivationQueue<int>(exception => throw exception);
        queue.Enqueue(1);
        queue.Stop();
        queue.Start((_, _) => throw new Xunit.Sdk.XunitException("must not handle"),
            _ => throw new Xunit.Sdk.XunitException("must not schedule"));
        queue.Enqueue(2);
    }

    [Fact]
    public void DispatcherRejectionStopsDeliveryAndReportsTheFailureOnce() {
        var errors = new List<Exception>();
        var queue = new ActivationQueue<int>(errors.Add);
        queue.Enqueue(1);
        queue.Start((_, _) => throw new Xunit.Sdk.XunitException("must not handle"), _ => false);
        queue.Enqueue(2);
        Assert.Contains("dispatcher rejected", Assert.Single(errors).Message);
    }

    [Fact]
    public void ReentrantEnqueueDoesNotLoseOrDispatchARequestTwice() {
        var delivered = new List<int>();
        var queue = new ActivationQueue<int>(exception => throw exception);
        queue.Enqueue(1);
        queue.Start((request, _) => {
            delivered.Add(request);
            if (request == 1) queue.Enqueue(2);
            return Task.CompletedTask;
        }, action => { action(); return true; });
        Assert.Equal(new[] { 1, 2 }, delivered);
    }
}
