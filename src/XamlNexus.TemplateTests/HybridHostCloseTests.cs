using Winui3_Wpf_XamlNexus.Common.Utils.IPC;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class HybridHostCloseTests {
    [Fact]
    public async Task CancelledClosePreservesTheOwnedProcessAndAllowsRetry() {
        var coordinator = new UICloseCoordinator();
        var rejected = new StringWriter();
        Task<bool> result = coordinator.RequestAsync(10, async id => {
            await UIHostControl.ListenAsync(new StringReader(UIHostControl.Request(id)), rejected,
                _ => Task.FromResult(false), _ => { });
            coordinator.Receive(10, rejected.ToString().Trim());
        });
        Assert.False(await result);
        Task<bool> retry = coordinator.RequestAsync(10, _ => Task.CompletedTask);
        Assert.False(retry.IsCompleted);
        coordinator.ProcessExited(10);
        Assert.True(await retry);
    }

    [Fact]
    public async Task SavingDoesNotCompleteTheRequestUntilTheProcessExits() {
        var coordinator = new UICloseCoordinator();
        var save = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new StringWriter();
        Task? listening = null;
        Task<bool> request = coordinator.RequestAsync(42, id => {
            listening = UIHostControl.ListenAsync(new StringReader(UIHostControl.Request(id)), output,
                _ => save.Task, _ => { });
            sent.SetResult();
            return Task.CompletedTask;
        });
        await sent.Task;
        Assert.False(request.IsCompleted);
        Assert.Equal("", output.ToString());
        save.SetResult(true);
        await listening!;
        Assert.False(request.IsCompleted); // A close callback is not proof the process has exited.
        coordinator.ProcessExited(42);
        Assert.True(await request);
    }

    [Fact]
    public async Task SaveFailureReportsRejectionAndLeavesHostRunning() {
        var coordinator = new UICloseCoordinator();
        var output = new StringWriter();
        var errors = new List<Exception>();
        Task<bool> request = coordinator.RequestAsync(9, async id => {
            await UIHostControl.ListenAsync(new StringReader(UIHostControl.Request(id)), output,
                _ => throw new IOException("save failed"), errors.Add);
            Assert.True(coordinator.Receive(9, output.ToString().Trim()));
        });
        Assert.False(await request);
        Assert.Equal("save failed", Assert.Single(errors).Message);
    }

    [Fact]
    public async Task DuplicateCloseDoesNotStackRequestsAndStaleResponsesCannotCancelANewRequest() {
        var coordinator = new UICloseCoordinator();
        Guid firstId = default, nextId = default;
        Task<bool> first = coordinator.RequestAsync(1, id => { firstId = id; return Task.CompletedTask; });
        Assert.False(await coordinator.RequestAsync(1, _ => throw new Exception("must not send")));
        coordinator.Receive(2, $"XAMLNEXUS_UI_CLOSE_RESULT:{firstId:N}:cancelled");
        coordinator.ProcessExited(2);
        Assert.False(first.IsCompleted);
        coordinator.Receive(1, $"XAMLNEXUS_UI_CLOSE_RESULT:{firstId:N}:cancelled");
        Assert.False(await first);
        Task<bool> next = coordinator.RequestAsync(3, id => { nextId = id; return Task.CompletedTask; });
        coordinator.Receive(3, $"XAMLNEXUS_UI_CLOSE_RESULT:{firstId:N}:cancelled");
        Assert.False(next.IsCompleted);
        coordinator.Receive(3, $"XAMLNEXUS_UI_CLOSE_RESULT:{nextId:N}:cancelled");
        Assert.False(await next);
    }

    [Fact]
    public async Task UnresponsiveProcessCanCancelWaitingWithoutApprovingClose() {
        var coordinator = new UICloseCoordinator();
        using var cancellation = new CancellationTokenSource();
        Task<bool> waiting = coordinator.RequestAsync(10, _ => Task.CompletedTask, cancellation.Token);
        Assert.False(waiting.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Task<bool> retry = coordinator.RequestAsync(10, _ => Task.CompletedTask);
        Assert.False(retry.IsCompleted);
        coordinator.ProcessExited(10);
        Assert.True(await retry);
    }

    [Fact]
    public async Task DebugOutputAndMalformedRequestsAreNotControlCommands() {
        int calls = 0;
        await UIHostControl.ListenAsync(new StringReader("debug output\nXAMLNEXUS_UI_CLOSE:invalid\n"),
            new StringWriter(), _ => { calls++; return Task.FromResult(false); }, _ => { });
        Assert.Equal(0, calls);
        Assert.False(UIHostControl.TryReadResponse("debug output", out _));
    }
}
