using Xunit;
using Winui3_XamlNexus.Common.Utils.ThreadContext;
using Winui3_XamlNexus.UIComponent.Attributes;
using Winui3_XamlNexus.UIComponent.Templates;
using Winui3_XamlNexus.UIComponent.Utils.Extensions;

namespace XamlNexus.TemplateTests;

public sealed class WinuiNavigationLifecycleTests {
    // Share the same inline context as page tests: either initialization is interchangeable.
    public WinuiNavigationLifecycleTests() => CrossThreadInvoker.Initialize(new InlineUiContext());
    private sealed class InlineUiContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
    }
    private class TestPage : ArcPage {
        public override Type ArcType => GetType();
        protected override bool IsMultiInstance => true;
        public TaskCompletionSource LeaveFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task LeaveTask { get; set; } = Task.CompletedTask;
        protected override async Task OnLeaveAsync() { await LeaveTask; LeaveFinished.TrySetResult(); }
        public TaskCompletionSource Destroyed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void OnDestroy() { base.OnDestroy(); Destroyed.TrySetResult(); }
        public void PrepareBackground() { Status = ArcPageStatus.BackgroundRunning; }
    }
    private sealed class FirstPage : TestPage { }
    [KeepAlive] private sealed class SecondPage : TestPage { }
    [KeepAlive] private sealed class ThirdPage : TestPage { }

    [Fact]
    public async Task Delayed_old_exit_cannot_reactivate_a_page_after_navigation_moves_on() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new FirstPage { LeaveTask = gate.Task };
        var second = new SecondPage(); var third = new ThirdPage();
        var view = new ArcNavigationContentView();
        foreach (var page in new TestPage[] { first, second, third }) {
            view.PageMap[page.ArcType] = page; view.ContentGrid.Children.Add(page);
            page.PrepareBackground();
        }
        first.NavigateEnter(new()); first.SetActiveStatus();
        view.ArcNavigate(typeof(SecondPage));
        Assert.Equal(ArcPageStatus.Active, second.Status);
        view.ArcNavigate(typeof(ThirdPage));
        Assert.Equal(ArcPageStatus.BackgroundRunning, second.Status);
        Assert.Equal(ArcPageStatus.Active, third.Status);
        gate.SetResult();
        await first.LeaveFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // Wait for the actual removal/cleanup, not merely completion of the leave hook.
        await first.Destroyed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ArcPageStatus.BackgroundRunning, second.Status);
        Assert.Equal(ArcPageStatus.Active, third.Status);
        Assert.False(view.PageMap.ContainsKey(typeof(FirstPage)));
        second.RaiseUnloaded(); third.RaiseUnloaded();
    }
}
