using Microsoft.UI.Dispatching;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class DispatcherQueueTests {
    private static Task Invoke(bool hybrid, DispatcherQueue queue, Func<Task> action) => hybrid
        ? Winui3_Wpf_XamlNexus.UIComponent.Utils.Extensions.DispatcherQueueExtensions.EnqueueOrInvokeAsync(queue, action)
        : Winui3_XamlNexus.UIComponent.Utils.Extensions.DispatcherQueueExtensions.EnqueueOrInvokeAsync(queue, action);

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Completion_waits_for_the_action(bool hybrid, bool onUiThread) {
        var queue = new DispatcherQueue { HasThreadAccess = onUiThread };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool invoked = false;
        Task pending = Invoke(hybrid, queue, () => { invoked = true; return gate.Task; });
        Assert.Equal(onUiThread, invoked);
        if (!onUiThread) queue.DispatchNext();
        Assert.True(invoked);
        Assert.False(pending.IsCompleted);
        gate.SetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Synchronous_and_asynchronous_failures_reach_the_caller(bool hybrid, bool onUiThread) {
        var queue = new DispatcherQueue { HasThreadAccess = onUiThread };
        var expected = new InvalidOperationException("test failure");
        Task sync = Invoke(hybrid, queue, () => throw expected);
        if (!onUiThread) queue.DispatchNext();
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => sync));
        Task asyncFailure = Invoke(hybrid, queue, async () => { await Task.Yield(); throw expected; });
        if (!onUiThread) queue.DispatchNext();
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => asyncFailure));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Cancellation_remains_cancellation(bool hybrid, bool onUiThread) {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var queue = new DispatcherQueue { HasThreadAccess = onUiThread };
        Task pending = Invoke(hybrid, queue, () => Task.FromCanceled(cancellation.Token));
        if (!onUiThread) queue.DispatchNext();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(pending.IsCanceled);
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Rejected_enqueue_fails_without_invoking_or_hanging(bool hybrid) {
        bool invoked = false;
        var queue = new DispatcherQueue { AcceptWork = false };
        Task pending = Invoke(hybrid, queue, () => { invoked = true; return Task.CompletedTask; });
        Assert.True(pending.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.False(invoked);
    }
}
