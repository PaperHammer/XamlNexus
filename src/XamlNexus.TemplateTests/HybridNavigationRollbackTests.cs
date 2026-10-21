using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Winui3_Wpf_XamlNexus.Common.Utils.DI;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.UIComponent.Attributes;
using Winui3_Wpf_XamlNexus.UIComponent.Context;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;
using Winui3_Wpf_XamlNexus.UIComponent.Utils.Extensions;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class HybridNavigationRollbackTests {
    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();
    private static readonly AsyncLocal<Attempt?> CurrentAttempt = new();
    public HybridNavigationRollbackTests() {
        AppServiceLocator.Services = Services;
        CrossThreadInvoker.Initialize(new InlineUiContext());
    }
    private sealed class InlineUiContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
    }
    private sealed class Attempt {
        public bool Fail { get; init; } = true;
        public bool CleanupFails { get; init; }
        public CancellationTokenSource? CancelOnEnter { get; init; }
        public FaultPage? Page { get; set; }
        public InvalidOperationException Error { get; } = new("page entry failed");
    }
    private class FaultPage : ArcPage {
        public override Type ArcType => GetType();
        protected override bool IsMultiInstance => true;
        public ArcPageContextKey Key => ContextKey;
        public bool Fail { get; set; }
        public bool CleanupFails { get; set; }
        public CancellationTokenSource? CancelOnEnter { get; set; }
        public InvalidOperationException Error { get; }
        public int DestroyCount { get; private set; }
        public int PreLeaveCount { get; private set; }
        public int Confirmations { get; private set; }
        public Task LeaveTask { get; set; } = Task.CompletedTask;
        public CancellationToken ExitToken { get; private set; }
        public FaultPage() {
            var attempt = CurrentAttempt.Value;
            Fail = attempt?.Fail ?? false;
            CleanupFails = attempt?.CleanupFails ?? false;
            CancelOnEnter = attempt?.CancelOnEnter;
            Error = attempt?.Error ?? new("page entry failed");
            if (attempt is not null) attempt.Page = this;
        }
        public override Task<bool> CanLeaveAsync(Winui3_Wpf_XamlNexus.UIComponent.Navigation.ArcLeaveReason reason, CancellationToken cancellationToken = default) {
            Confirmations++;
            return Task.FromResult(true);
        }
        protected override void OnEnter(FrameworkPayload? payload) {
            if (Fail) Payload?.Set("old-payload-mutated", true);
            base.OnEnter(payload);
            CancelOnEnter?.Cancel();
            if (!Fail) return;
            Payload?.Set("entry-mutated", true);
            DataContext = new object();
            Opacity = 0.3;
            IsHitTestVisible = true;
            Translation = new Vector3(9, 9, 9);
            Canvas.SetZIndex(this, 99);
            if (ArcContext is not null) ArcContext.IsActive = true;
            throw Error;
        }
        protected override Task OnPreLeaveAsync() { PreLeaveCount++; return base.OnPreLeaveAsync(); }
        protected override Task OnLeaveAsync(CancellationToken cancellationToken) { ExitToken = cancellationToken; return LeaveTask; }
        protected override void OnDestroy() {
            DestroyCount++;
            if (CleanupFails) throw new IOException("page cleanup failed");
            base.OnDestroy();
        }
        public void Background() { Status = ArcPageStatus.BackgroundRunning; }
    }
    [KeepAlive] private sealed class HomePage : FaultPage { public HomePage() { } }
    [KeepAlive] private sealed class CachedPage : FaultPage { public CachedPage() { } }
    private sealed class NewPage : FaultPage { public NewPage() { } }
    private sealed class PendingPage : FaultPage { public PendingPage() { } }
    private sealed class ConstructorFailure : FaultPage {
        public ConstructorFailure() => throw new InvalidOperationException("constructor failed");
    }
    private static ArcNavigationOptions? Options(bool animated) => animated ? new(ArcNavigationTransition.Fade) : null;
    private static (ArcNavigationContentView Host, HomePage Home) ActiveHost() {
        CurrentAttempt.Value = null;
        var host = new ArcNavigationContentView();
        var page = new HomePage { DataContext = new object() };
        host.PageMap.Add(page.ArcType, page);
        host.ContentGrid.Children.Add(page);
        page.NavigateEnter(new FrameworkPayload { ["id"] = "home" });
        page.SetActiveStatus();
        return (host, page);
    }
    private static void AssertHome(ArcNavigationContentView host, HomePage home) {
        Assert.Same(home, host.PageMap[home.ArcType]);
        Assert.Contains(home, host.ContentGrid.Children);
        Assert.Equal(ArcPageStatus.Active, home.Status);
        Assert.Equal("home", home.Payload!.Get<string>("id"));
        Assert.NotNull(home.DataContext);
        Assert.True(home.ArcContext!.IsActive);
        Assert.Equal(0, home.PreLeaveCount);
        Assert.Equal(0, home.DestroyCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FailedNewPageIsRemovedAndCleanedWithoutReplacingEntryError(bool animated, bool cleanupFails) {
        var (host, home) = ActiveHost();
        var attempt = new Attempt { CleanupFails = cleanupFails };
        CurrentAttempt.Value = attempt;
        var input = new FrameworkPayload { ["id"] = "new" };
        try {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.ArcNavigateAsync(typeof(NewPage), input, Options(animated)));
            Assert.Same(attempt.Error, error);
            AssertHome(host, home);
            Assert.Single(host.PageMap);
            Assert.Single(host.ContentGrid.Children);
            var failed = Assert.IsType<NewPage>(attempt.Page);
            Assert.Equal(1, failed.DestroyCount);
            Assert.Equal(ArcPageStatus.Stopped, failed.Status);
            Assert.Null(failed.Payload); Assert.Null(failed.ArcContext); Assert.Null(failed.DataContext);
            Assert.False(ArcPageContextManager.HasContext(failed.Key));
            Assert.Single(input.GetRawData()); Assert.Equal("new", input.Get<string>("id"));
            failed.RaiseUnloaded(); Assert.Equal(1, failed.DestroyCount);
            CurrentAttempt.Value = new Attempt { Fail = false };
            Assert.True(await host.ArcNavigateAsync(typeof(NewPage), input, Options(animated)));
            Assert.Equal(ArcPageStatus.Active, host.PageMap[typeof(NewPage)].Status);
        }
        finally { CurrentAttempt.Value = null; foreach (var page in host.PageMap.Values.ToArray()) page.RaiseUnloaded(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedPageRetainsItsPayloadDataContextAndHiddenStateAfterEntryFailure(bool animated) {
        var (host, home) = ActiveHost();
        var cached = new CachedPage { DataContext = new object() };
        host.PageMap.Add(cached.ArcType, cached); host.ContentGrid.Children.Add(cached);
        var payload = new FrameworkPayload { ["id"] = "cached" };
        cached.NavigateEnter(payload); cached.Background();
        cached.Opacity = 0.45; cached.IsHitTestVisible = false; cached.Translation = new Vector3(1, 2, 3);
        Canvas.SetZIndex(cached, 7); cached.ArcContext!.IsActive = false;
        var context = cached.ArcContext; var data = cached.DataContext;
        cached.Fail = true;
        try {
            Assert.Same(cached.Error, await Assert.ThrowsAsync<InvalidOperationException>(() => host.ArcNavigateAsync(cached.ArcType, options: Options(animated))));
            AssertHome(host, home);
            Assert.Same(cached, host.PageMap[cached.ArcType]); Assert.Contains(cached, host.ContentGrid.Children);
            Assert.Equal(ArcPageStatus.BackgroundRunning, cached.Status);
            Assert.Same(payload, cached.Payload); Assert.Single(payload.GetRawData());
            Assert.Same(data, cached.DataContext); Assert.Same(context, cached.ArcContext);
            Assert.False(context!.IsActive); Assert.Same(context, ArcPageContextManager.GetContext(cached.Key));
            Assert.Equal(0.45, cached.Opacity); Assert.False(cached.IsHitTestVisible);
            Assert.Equal(new Vector3(1, 2, 3), cached.Translation); Assert.Equal(7, Canvas.GetZIndex(cached));
            Assert.Equal(0, cached.DestroyCount);
            cached.Fail = false;
            Assert.True(await host.ArcNavigateAsync(cached.ArcType, options: Options(animated)));
            Assert.Equal(ArcPageStatus.Active, cached.Status);
        }
        finally { home.RaiseUnloaded(); cached.RaiseUnloaded(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SamePageFailureRestoresExistingAndCallerPayloadWithoutUndoingBusinessWork(bool animated) {
        var (host, home) = ActiveHost();
        var previous = home.Payload!; var context = home.ArcContext; var data = home.DataContext;
        var input = new FrameworkPayload { ["id"] = "other" };
        home.Fail = true;
        try {
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.ArcNavigateAsync(home.ArcType, input, Options(animated)));
            AssertHome(host, home);
            Assert.Same(previous, home.Payload); Assert.Single(previous.GetRawData());
            Assert.Same(context, home.ArcContext); Assert.Same(data, home.DataContext);
            Assert.Single(input.GetRawData()); Assert.Equal("other", input.Get<string>("id"));
            Assert.Equal(1, home.Opacity); Assert.True(home.IsHitTestVisible);
            Assert.Equal(Vector3.Zero, home.Translation); Assert.Equal(0, Canvas.GetZIndex(home));
            Assert.Single(host.ContentGrid.Children); Assert.Single(host.PageMap);
            home.Fail = false;
            Assert.True(await host.ArcNavigateAsync(home.ArcType, input, Options(animated)));
            Assert.Same(input, home.Payload); Assert.Equal("other", home.Payload!.Get<string>("id"));
        }
        finally { home.RaiseUnloaded(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedReentryDoesNotCancelAnAlreadyPendingExit(bool animationFails) {
        var (host, home) = ActiveHost();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new PendingPage { LeaveTask = gate.Task };
        host.PageMap.Add(pending.ArcType, pending); host.ContentGrid.Children.Add(pending);
        pending.NavigateEnter(new FrameworkPayload { ["id"] = "pending" });
        var exit = pending.NavigateExitAsync(() => { host.PageMap.Remove(pending.ArcType); host.ContentGrid.Children.Remove(pending); });
        pending.Fail = !animationFails;
        Storyboard.BeginFailure.Value = animationFails ? new InvalidOperationException("animation failed") : null;
        try {
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.ArcNavigateAsync(pending.ArcType, options: Options(animationFails)));
            AssertHome(host, home); Assert.False(pending.ExitToken.IsCancellationRequested);
            Assert.True(pending.IsPreLeaved); Assert.False(exit.IsCompleted);
            Assert.Equal(ArcPageStatus.BackgroundRunning, pending.Status);
            Assert.Equal("pending", pending.Payload!.Get<string>("id")); Assert.False(pending.ArcContext!.IsActive);
        }
        finally { Storyboard.BeginFailure.Value = null; gate.TrySetResult(); await exit.WaitAsync(TimeSpan.FromSeconds(2)); home.RaiseUnloaded(); }
        Assert.Equal(1, pending.DestroyCount); Assert.False(host.PageMap.ContainsKey(pending.ArcType));
    }

    [Fact]
    public async Task AnimationStartupFailureRollsBackNewPageAndAllowsRetry() {
        var (host, home) = ActiveHost();
        var attempt = new Attempt { Fail = false }; CurrentAttempt.Value = attempt;
        var failure = new InvalidOperationException("animation failed"); Storyboard.BeginFailure.Value = failure;
        try {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => host.ArcNavigateAsync(typeof(NewPage), options: Options(true))));
            AssertHome(host, home); Assert.Single(host.ContentGrid.Children); Assert.Single(host.PageMap);
            Assert.Equal(1, attempt.Page!.DestroyCount);
            Storyboard.BeginFailure.Value = null;
            Assert.True(await host.ArcNavigateAsync(typeof(NewPage), options: Options(true)));
        }
        finally { CurrentAttempt.Value = null; Storyboard.BeginFailure.Value = null; foreach (var page in host.PageMap.Values.ToArray()) page.RaiseUnloaded(); }
    }

    [Fact]
    public async Task CancellationDuringEntryDiscardsThePreparedTarget() {
        var (host, home) = ActiveHost();
        using var cancellation = new CancellationTokenSource();
        var attempt = new Attempt { Fail = false, CancelOnEnter = cancellation }; CurrentAttempt.Value = attempt;
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.ArcNavigateAsync(typeof(NewPage), cancellationToken: cancellation.Token));
            AssertHome(host, home); Assert.Single(host.PageMap); Assert.Single(host.ContentGrid.Children);
            Assert.Equal(1, attempt.Page!.DestroyCount);
        }
        finally { CurrentAttempt.Value = null; home.RaiseUnloaded(); }
    }

    [Fact]
    public async Task ConstructorFailureAndInvalidTransitionsDoNotStartDeparture() {
        var (host, home) = ActiveHost();
        try {
            await Assert.ThrowsAnyAsync<Exception>(() => host.ArcNavigateAsync(typeof(ConstructorFailure)));
            AssertHome(host, home); Assert.Single(host.PageMap); Assert.Single(host.ContentGrid.Children);
            int confirmations = home.Confirmations;
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.ArcNavigateAsync(typeof(NewPage), options: new((ArcNavigationTransition)999)));
            Assert.Equal(confirmations, home.Confirmations); AssertHome(host, home);
        }
        finally { home.RaiseUnloaded(); }
    }
}
