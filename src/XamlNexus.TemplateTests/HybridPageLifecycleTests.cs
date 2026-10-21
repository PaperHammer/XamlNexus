using Xunit;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.UIComponent.Attributes;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

namespace XamlNexus.TemplateTests;

public sealed class HybridPageLifecycleTests {
    static HybridPageLifecycleTests() => CrossThreadInvoker.Initialize(new InlineUiContext());
    private sealed class InlineUiContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
    }
    private class TestPage : ArcPage {
        public override Type ArcType => GetType();
        protected override bool IsMultiInstance => true;
        public Task LeaveTask { get; set; } = Task.CompletedTask;
        public int DestroyCount { get; private set; }
        public bool LeaveSawLiveContext { get; private set; }
        protected override Task OnLeaveAsync() {
            LeaveSawLiveContext = ArcContext != null && DataContext != null;
            return LeaveTask;
        }
        protected override void OnDestroy() { DestroyCount++; base.OnDestroy(); }
    }
    private sealed class CancellablePage : TestPage {
        public CancellationToken ExitToken { get; private set; }
        protected override Task OnLeaveAsync(CancellationToken cancellationToken) {
            ExitToken = cancellationToken;
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Fact]
    public async Task Reentry_cancels_token_aware_leave_operations() {
        var page = new CancellablePage();
        page.NavigateEnter(new());
        Task<bool> exit = page.NavigateExitAsync();
        Assert.True(page.ExitToken.CanBeCanceled);
        page.NavigateEnter(new());
        Assert.False(await exit.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(page.ExitToken.IsCancellationRequested);
        Assert.Equal(0, page.DestroyCount);
        page.RaiseUnloaded();
    }

    [KeepAlive] private sealed class RetainedPage : TestPage { }
    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Removal_waits_for_leave_and_unload_destroys_exactly_once() {
        var gate = Gate();
        var page = new TestPage { LeaveTask = gate.Task, DataContext = new object() };
        page.NavigateEnter(new()); page.RaiseLoaded();
        bool removed = false, completed = false;
        Task<bool> exit = page.NavigateExitAsync(() => { removed = true; page.RaiseUnloaded(); }, () => completed = true);
        Assert.False(removed); Assert.False(exit.IsCompleted);
        Assert.True(page.LeaveSawLiveContext);
        gate.SetResult();
        Assert.True(await exit.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(removed); Assert.True(completed);
        Assert.Equal(1, page.DestroyCount);
        Assert.Null(page.ArcContext); Assert.Null(page.DataContext); Assert.Null(page.Payload);
        Assert.Equal(ArcPageStatus.Stopped, page.Status);
        page.RaiseUnloaded();
        Assert.Equal(1, page.DestroyCount);
        Assert.False(await page.NavigateExitAsync());
    }

    [Fact]
    public async Task Reentry_cancels_blocked_exit_without_stale_preleave_or_data_loss() {
        var page = new TestPage { DataContext = new object() };
        page.NavigateEnter(new()); page.RaiseLoaded();
        var context = page.ArcContext!;
        using var blocked = context.KeepAliveBlocking.Block();
        bool removed = false;
        Task<bool> exit = page.NavigateExitAsync(() => removed = true);
        Assert.False(context.IsActive);
        var payload = new FrameworkPayload();
        page.NavigateEnter(payload);
        Assert.False(await exit.WaitAsync(TimeSpan.FromSeconds(2)));
        blocked.Dispose();
        await context.KeepAliveBlocking.WaitAsync();
        Assert.False(page.IsPreLeaved); Assert.True(context.IsActive);
        Assert.Same(context, page.ArcContext); Assert.Same(payload, page.Payload);
        Assert.NotNull(page.DataContext); Assert.False(removed); Assert.Equal(0, page.DestroyCount);
        Assert.Equal(1, page.Opacity); Assert.True(page.IsHitTestVisible);
        page.RaiseUnloaded();
    }

    [Fact]
    public async Task Superseded_exit_cannot_clear_the_next_exit_cancellation_source() {
        var gate = Gate();
        var page = new TestPage { LeaveTask = gate.Task, DataContext = new object() };
        page.NavigateEnter(new()); page.RaiseLoaded();
        bool removed = false;
        Task<bool> first = page.NavigateExitAsync(() => removed = true);
        Task<bool> second = page.NavigateExitAsync(() => removed = true);
        Assert.False(await first.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(second.IsCompleted);
        page.NavigateEnter(new());
        Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(2)));
        gate.SetResult();
        Assert.False(removed); Assert.Equal(0, page.DestroyCount); Assert.NotNull(page.ArcContext);
        page.RaiseUnloaded();
    }

    [Fact]
    public async Task Leave_failure_is_observable_and_keeps_context_available_for_reentry() {
        var error = new InvalidOperationException("leave failed");
        var page = new TestPage { LeaveTask = Task.FromException(error), DataContext = new object() };
        page.NavigateEnter(new()); page.RaiseLoaded();
        bool removed = false;
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => page.NavigateExitAsync(() => removed = true)));
        Assert.False(removed); Assert.Equal(0, page.DestroyCount); Assert.NotNull(page.ArcContext);
        page.NavigateEnter(new()); Assert.True(page.ArcContext!.IsActive);
        page.RaiseUnloaded();
    }

    [Fact]
    public async Task Unrelated_cancellation_in_leave_is_observable() {
        var page = new TestPage { LeaveTask = Task.FromCanceled(new CancellationToken(true)) };
        page.NavigateEnter(new());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => page.NavigateExitAsync());
        Assert.Equal(0, page.DestroyCount);
        page.RaiseUnloaded();
    }

    [Fact]
    public async Task Keepalive_exit_preserves_the_context_until_actual_unload() {
        var page = new RetainedPage { DataContext = new object() };
        page.NavigateEnter(new()); page.RaiseLoaded();
        var context = page.ArcContext;
        bool removed = false;
        Assert.True(await page.NavigateExitAsync(() => removed = true));
        Assert.False(removed); Assert.True(page.IsPreLeaved);
        Assert.Equal(ArcPageStatus.BackgroundRunning, page.Status);
        Assert.False(context!.IsActive); Assert.Same(context, page.ArcContext);
        page.NavigateEnter(new());
        Assert.True(context.IsActive); Assert.False(page.IsPreLeaved);
        page.RaiseUnloaded(); page.RaiseUnloaded(); Assert.Equal(1, page.DestroyCount);
    }
}
