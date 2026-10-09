using Xunit;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.UIComponent.Navigation;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;
using Winui3_Wpf_XamlNexus.UIComponent.Utils.Extensions;

namespace XamlNexus.TemplateTests;

public sealed class HybridLeaveGuardTests {
    public HybridLeaveGuardTests() => CrossThreadInvoker.Initialize(new InlineUiContext());
    private sealed class InlineUiContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
    }
    private class GuardedPage : ArcPage {
        public override Type ArcType => GetType();
        protected override bool IsMultiInstance => true;
        public Task<bool> Decision { get; set; } = Task.FromResult(true);
        public int Confirmations { get; private set; }
        public int PreLeaves { get; private set; }
        public ArcLeaveReason? Reason { get; private set; }
        public override Task<bool> CanLeaveAsync(ArcLeaveReason reason, CancellationToken cancellationToken = default) {
            Confirmations++; Reason = reason; return Decision;
        }
        protected override Task OnPreLeaveAsync() { PreLeaves++; return base.OnPreLeaveAsync(); }
        public void Background() => Status = ArcPageStatus.BackgroundRunning;
    }
    private sealed class TargetPage : GuardedPage { }
    private static (ArcNavigationContentView Host, GuardedPage Page) ActiveHost() {
        var host = new ArcNavigationContentView();
        var page = new GuardedPage { DataContext = new object() };
        host.PageMap[page.ArcType] = page; host.ContentGrid.Children.Add(page);
        page.NavigateEnter(new()); page.SetActiveStatus();
        return (host, page);
    }
    private static void Cache(ArcNavigationContentView host, GuardedPage page) {
        host.PageMap[page.ArcType] = page; host.ContentGrid.Children.Add(page); page.Background();
    }

    [Fact]
    public async Task Refusal_keeps_the_page_payload_and_context_without_creating_a_target() {
        var (host, page) = ActiveHost(); page.Decision = Task.FromResult(false);
        var context = page.ArcContext; var payload = page.Payload; var data = page.DataContext;
        Assert.False(await host.ArcNavigateAsync(typeof(TargetPage)));
        Assert.Equal(ArcPageStatus.Active, page.Status); Assert.Equal(0, page.PreLeaves);
        Assert.Same(context, page.ArcContext); Assert.Same(payload, page.Payload); Assert.Same(data, page.DataContext);
        Assert.True(page.IsHitTestVisible); Assert.Single(host.ContentGrid.Children);
        Assert.Single(host.PageMap); Assert.Equal(ArcLeaveReason.Navigation, page.Reason);
        page.RaiseUnloaded();
    }

    [Fact]
    public async Task Approval_activates_the_target_and_runs_cleanup_only_after_confirmation() {
        var (host, page) = ActiveHost(); var target = new TargetPage(); Cache(host, target);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); page.Decision = gate.Task;
        Task<bool> pending = host.ArcNavigateAsync(typeof(TargetPage));
        Assert.Equal(0, page.PreLeaves); Assert.Equal(ArcPageStatus.Active, page.Status);
        gate.SetResult(true); Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(ArcPageStatus.Active, target.Status); Assert.Equal(1, page.PreLeaves);
        Assert.Equal(ArcPageStatus.Stopped, page.Status); Assert.Null(page.ArcContext);
        target.RaiseUnloaded();
    }

    [Fact]
    public async Task Confirmation_excludes_duplicate_navigation_and_window_close() {
        var (host, page) = ActiveHost(); var target = new TargetPage(); Cache(host, target);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); page.Decision = gate.Task;
        Task<bool> first = host.ArcNavigateAsync(typeof(TargetPage));
        Assert.False(await host.ArcNavigateAsync(typeof(TargetPage)));
        var close = new ArcCloseRequest(); bool closed = false;
        Assert.False(await close.TryCloseAsync(ct => host.ArcCanLeaveAsync(ArcLeaveReason.WindowClose, ct), () => closed = true));
        Assert.False(closed); Assert.Equal(1, page.Confirmations);
        gate.SetResult(false); Assert.False(await first);
        page.RaiseUnloaded(); target.RaiseUnloaded();
    }

    [Fact]
    public async Task Window_confirmation_excludes_navigation_and_cancelling_close_preserves_state() {
        var (host, page) = ActiveHost();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); page.Decision = gate.Task;
        var close = new ArcCloseRequest(); bool closed = false;
        Task<bool> pending = close.TryCloseAsync(ct => host.ArcCanLeaveAsync(ArcLeaveReason.WindowClose, ct), () => closed = true);
        Assert.Equal(ArcLeaveReason.WindowClose, page.Reason);
        Assert.False(await host.ArcNavigateAsync(typeof(TargetPage)));
        Assert.False(await close.TryCloseAsync(_ => Task.FromResult(true), () => closed = true));
        gate.SetResult(false); Assert.False(await pending);
        Assert.False(closed); Assert.Equal(ArcPageStatus.Active, page.Status); Assert.NotNull(page.ArcContext);
        page.RaiseUnloaded();
    }

    [Fact]
    public async Task Guard_failures_and_cancellation_do_not_mutate_the_page_or_block_retry() {
        var (host, page) = ActiveHost();
        page.Decision = Task.FromException<bool>(new InvalidOperationException("confirmation failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ArcNavigateAsync(typeof(TargetPage)));
        Assert.Equal(ArcPageStatus.Active, page.Status); Assert.Equal(0, page.PreLeaves);
        using var cancellation = new CancellationTokenSource();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); page.Decision = gate.Task;
        Task<bool> pending = host.ArcNavigateAsync(typeof(TargetPage), cancellationToken: cancellation.Token);
        cancellation.Cancel(); gate.SetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(ArcPageStatus.Active, page.Status); Assert.Equal(0, page.PreLeaves);
        page.Decision = Task.FromResult(false);
        Assert.False(await host.ArcNavigateAsync(typeof(TargetPage)));
        Assert.Equal(3, page.Confirmations); page.RaiseUnloaded();
    }

    [Fact]
    public async Task Closing_checks_retained_background_pages_too() {
        var (host, page) = ActiveHost(); var retained = new TargetPage { Decision = Task.FromResult(false) }; Cache(host, retained);
        Assert.False(await host.ArcCanLeaveAsync(ArcLeaveReason.WindowClose));
        Assert.Equal(ArcLeaveReason.WindowClose, retained.Reason);
        Assert.Equal(ArcPageStatus.Active, page.Status); Assert.NotNull(retained.ArcContext);
        page.RaiseUnloaded(); retained.RaiseUnloaded();
    }

    [Fact]
    public async Task Selecting_the_current_page_is_a_noop_but_new_payload_requires_confirmation() {
        var (host, page) = ActiveHost(); var payload = page.Payload;
        page.Decision = Task.FromResult(false);
        Assert.True(await host.ArcNavigateAsync(page.ArcType)); Assert.Equal(0, page.Confirmations); Assert.Same(payload, page.Payload);
        Assert.False(await host.ArcNavigateAsync(page.ArcType, new FrameworkPayload()));
        Assert.Same(payload, page.Payload);
        page.Decision = Task.FromResult(true);
        var next = new FrameworkPayload();
        Assert.True(await host.ArcNavigateAsync(page.ArcType, next)); Assert.Same(next, page.Payload);
        Assert.Single(host.ContentGrid.Children); Assert.Same(page, host.ContentGrid.Children[0]);
        page.RaiseUnloaded();
    }
}
