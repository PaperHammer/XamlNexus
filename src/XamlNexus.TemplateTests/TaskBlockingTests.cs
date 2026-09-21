using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class TaskBlockingTests
{
    [Fact]
    public async Task Winui_waits_until_every_registered_task_is_released()
    {
        var blocking = new Winui3_XamlNexus.Common.Utils.TaskUtils.TaskBlocking();
        await VerifyMultipleRegistrationsAsync(blocking.Block, blocking.WaitAsync);
    }

    [Fact]
    public async Task Hybrid_waits_until_every_registered_task_is_released()
    {
        var blocking = new Winui3_Wpf_XamlNexus.Common.Utils.TaskUtils.TaskBlocking();
        await VerifyMultipleRegistrationsAsync(blocking.Block, blocking.WaitAsync);
    }

    private static async Task VerifyMultipleRegistrationsAsync(
        Func<IDisposable> block,
        Func<Task> wait)
    {
        IDisposable first = block();
        IDisposable second = block();
        Task pending = wait();

        Assert.False(pending.IsCompleted);
        first.Dispose();
        Assert.False(pending.IsCompleted);

        second.Dispose();
        await pending.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(wait().IsCompletedSuccessfully);
    }
}
