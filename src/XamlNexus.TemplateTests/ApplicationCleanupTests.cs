using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PureCleanup = Winui3_XamlNexus.Common.Utils.Lifecycle.ApplicationCleanup;
using HybridCleanup = Winui3_Wpf_XamlNexus.Common.Utils.Lifecycle.ApplicationCleanup;

namespace XamlNexus.TemplateTests;

public sealed class ApplicationCleanupTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncOnlyServiceIsAwaitedBeforeSynchronousResourcesAndFinalExit(bool hybrid) {
        var events = new List<string>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resource = new AsyncResource(events, release.Task);
        var provider = new ServiceCollection()
            .AddSingleton(_ => new SyncResource(events))
            .AddSingleton(_ => resource).BuildServiceProvider();
        provider.GetRequiredService<SyncResource>();
        provider.GetRequiredService<AsyncResource>();
        var errors = new List<Exception>();
        var shutdown = Run(hybrid, errors.Add,
            () => DisposeServices(hybrid, provider),
            () => { events.Add("exit"); return ValueTask.CompletedTask; });
        try {
            Assert.False(shutdown.IsCompleted);
            Assert.Equal(new[] { "async-start" }, events);
        }
        finally {
            release.TrySetResult();
            await shutdown;
        }
        Assert.Empty(errors);
        Assert.Equal(new[] { "async-start", "async-end", "sync-dispose", "exit" }, events);
        Assert.Equal(1, resource.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupFailuresDoNotSkipLaterApplicationResources(bool hybrid) {
        var events = new List<string>();
        var errors = new List<Exception>();
        var services = new ServiceCollection().AddSingleton<ThrowingAsyncResource>().BuildServiceProvider();
        services.GetRequiredService<ThrowingAsyncResource>();
        await Run(hybrid, errors.Add,
            () => throw new IOException("settings write failed"),
            () => DisposeServices(hybrid, services),
            () => { events.Add("notifications"); return ValueTask.CompletedTask; },
            () => { events.Add("exit"); return ValueTask.CompletedTask; });
        Assert.Collection(errors,
            exception => Assert.Equal("settings write failed", Assert.IsType<IOException>(exception).Message),
            exception => Assert.Equal("async service cleanup failed", Assert.IsType<InvalidOperationException>(exception).Message));
        Assert.Equal(new[] { "notifications", "exit" }, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousCustomProviderAndUninitializedStartupAreSupported(bool hybrid) {
        var events = new List<string>();
        var provider = new SyncProvider(events);
        var errors = new List<Exception>();
        await Run(hybrid, errors.Add,
            () => DisposeServices(hybrid, null),
            () => DisposeServices(hybrid, provider));
        Assert.Empty(errors);
        Assert.Equal(new[] { "provider-dispose" }, events);
    }

    private static Task Run(bool hybrid, Action<Exception> reportError, params Func<ValueTask>[] steps) =>
        hybrid ? HybridCleanup.RunAsync(reportError, steps) : PureCleanup.RunAsync(reportError, steps);
    private static ValueTask DisposeServices(bool hybrid, IServiceProvider? services) =>
        hybrid ? HybridCleanup.DisposeServicesAsync(services) : PureCleanup.DisposeServicesAsync(services);

    private sealed class AsyncResource(List<string> events, Task release) : IAsyncDisposable {
        public int Disposals { get; private set; }
        public async ValueTask DisposeAsync() {
            Disposals++;
            events.Add("async-start");
            await release;
            events.Add("async-end");
        }
    }
    private sealed class SyncResource(List<string> events) : IDisposable {
        public void Dispose() => events.Add("sync-dispose");
    }
    private sealed class ThrowingAsyncResource : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            await Task.Yield();
            throw new InvalidOperationException("async service cleanup failed");
        }
    }
    private sealed class SyncProvider(List<string> events) : IServiceProvider, IDisposable {
        public object? GetService(Type serviceType) => null;
        public void Dispose() => events.Add("provider-dispose");
    }
}
