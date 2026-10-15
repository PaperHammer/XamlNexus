using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;
using Winui3_Wpf_XamlNexus.Common.Utils.IPC;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class HybridActivationTests {
    [Fact]
    public async Task HostAcceptsBeforeReadinessAndDrainsInReceiptOrder() {
        string key = Guid.NewGuid().ToString("N");
        var received = Channel.CreateUnbounded<string[]>();
        var errors = new List<Exception>();
        using var server = new HybridActivationPipe(key, errors.Add);
        string json = JsonSerializer.Serialize(new { id = "中文 空格 \" \\ end" });
        await HybridActivationPipe.ForwardAsync(key, new[] { "--route", "Orders", "--data", json });
        await HybridActivationPipe.ForwardAsync(key, new[] { "--route", "Editor" });
        Assert.False(received.Reader.TryRead(out _));
        server.StartHandling((args, _) => { received.Writer.TryWrite(args); return Task.CompletedTask; });
        var first = await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var second = await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(json, first[3]);
        Assert.Equal("Editor", second[1]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task BoundedQueueRejectsAndDisposedHostDoesNotBecomeAnotherOwner() {
        string key = Guid.NewGuid().ToString("N");
        var server = new HybridActivationPipe(key, _ => { });
        try {
            for (int i = 0; i < 64; i++) await HybridActivationPipe.ForwardAsync(key, new[] { i.ToString() });
            await Assert.ThrowsAsync<IOException>(() => HybridActivationPipe.ForwardAsync(key, new[] { "full" }));
        }
        finally { server.Dispose(); }
        await Assert.ThrowsAsync<TimeoutException>(() => HybridActivationPipe.ForwardAsync(key, Array.Empty<string>(),
            timeout: TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task MalformedClientDoesNotKillListener() {
        string key = Guid.NewGuid().ToString("N");
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new HybridActivationPipe(key, exception => error.TrySetResult(exception));
        using (var client = new NamedPipeClientStream(".", HybridActivationPipe.GetPipeName(key),
            PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)) {
            await client.ConnectAsync(3000);
            await client.WriteAsync(new byte[] { 255, 255, 255, 127 });
            await client.FlushAsync();
        }
        Assert.IsType<IOException>(await error.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await HybridActivationPipe.ForwardAsync(key, new[] { "--route", "Orders" });
    }

    [Fact]
    public async Task ReceiptIsBoundToProcessAndRequestAndExitFailsDelivery() {
        var coordinator = new UIActivationCoordinator();
        Guid id = default;
        Task delivery = coordinator.RequestAsync(11, value => { id = value; return Task.CompletedTask; });
        coordinator.Receive(12, "XAMLNEXUS_UI_ACTIVATE_RESULT:" + id.ToString("N") + ":accepted");
        coordinator.Receive(11, "XAMLNEXUS_UI_ACTIVATE_RESULT:" + Guid.NewGuid().ToString("N") + ":accepted");
        Assert.False(delivery.IsCompleted);
        coordinator.Receive(11, "XAMLNEXUS_UI_ACTIVATE_RESULT:" + id.ToString("N") + ":accepted");
        await delivery;
        delivery = coordinator.RequestAsync(11, _ => Task.CompletedTask);
        coordinator.ProcessExited(11);
        await Assert.ThrowsAsync<IOException>(() => delivery);
    }

    [Fact]
    public async Task UnresponsiveUiTimesOutWithoutCreatingAReplacement() {
        var coordinator = new UIActivationCoordinator();
        int sends = 0;
        await Assert.ThrowsAsync<TimeoutException>(() => coordinator.RequestAsync(11,
            _ => { sends++; return Task.CompletedTask; }, timeout: TimeSpan.FromMilliseconds(100)));
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task ControlReaderAcceptsRequestsWhileGuardAwaitsAndSerializesClose() {
        using var stop = new CancellationTokenSource();
        var input = new Lines();
        var output = new Replies();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        Task listen = UIHostControl.ListenAsync(input, output, _ => {
            order.Add("close"); done.TrySetResult(); return Task.FromResult(false);
        }, async (arguments, _) => {
            order.Add(arguments[0]);
            if (arguments[0] == "first") { entered.TrySetResult(); await release.Task; }
        }, exception => done.TrySetException(exception), stop.Token);
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), close = Guid.NewGuid();
        input.Send(UIHostControl.Activation(first, new[] { "first" }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        input.Send(UIHostControl.Activation(second, new[] { "second" }));
        input.Send(UIHostControl.Request(close));
        // Receipt is independent of the user's decision in the first navigation.
        string one = await output.Read();
        string two = await output.Read();
        Assert.Contains(first.ToString("N") + ":accepted", one);
        Assert.Contains(second.ToString("N") + ":accepted", two);
        Assert.Equal(new[] { "first" }, order);
        release.TrySetResult();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(close.ToString("N") + ":cancelled", await output.Read());
        Assert.Equal(new[] { "first", "second", "close" }, order);
        stop.Cancel();
        await listen;
    }

    private sealed class Lines : TextReader {
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
        public void Send(string line) => _lines.Writer.TryWrite(line);
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => Read(cancellationToken);
        private async ValueTask<string?> Read(CancellationToken token) => await _lines.Reader.ReadAsync(token);
    }
    private sealed class Replies : StringWriter {
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
        public override Task WriteLineAsync(string? line) { _lines.Writer.TryWrite(line!); return Task.CompletedTask; }
        public async Task<string> Read() => await _lines.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
