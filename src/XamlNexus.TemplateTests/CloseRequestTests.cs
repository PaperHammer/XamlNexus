using Xunit;
namespace XamlNexus.TemplateTests;
public sealed class CloseRequestTests {
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Approved_close_replays_once_and_resets_after_failure(bool hybrid) {
        int confirms = 0, closes = 0;
        var pure = new Winui3_XamlNexus.UIComponent.Navigation.ArcCloseRequest();
        var mixed = new Winui3_Wpf_XamlNexus.UIComponent.Navigation.ArcCloseRequest();
        bool Replaying() => hybrid ? mixed.IsReplaying : pure.IsReplaying;
        Task<bool> Close(Func<CancellationToken, Task<bool>> confirm, Action close) => hybrid
            ? mixed.TryCloseAsync(confirm, close) : pure.TryCloseAsync(confirm, close);
        Assert.False(await Close(_ => Task.FromResult(false), () => closes++)); Assert.Equal(0, closes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Close(_ => Task.FromResult(true), () => { Assert.True(Replaying()); throw new InvalidOperationException(); }));
        Assert.False(Replaying());
        Assert.True(await Close(_ => { confirms++; return Task.FromResult(true); }, () => { closes++; Assert.True(Replaying()); }));
        Assert.False(Replaying()); Assert.Equal(1, confirms); Assert.Equal(1, closes);
    }
}
