using __APP__.MainPanel.Services;
using __APP__.MainPanel.ViewModels;
using Xunit;

namespace XamlNexus.TemplateTests;

// Compile the shipped assets themselves so these tests also guard generated C# syntax.
public sealed class ListPageViewModelTests {
    [Fact]
    public async Task ReusingViewModelPreservesQueryAndItemsWithoutReloading() {
        var source = new Source();
        var model = new __NAME__ViewModel(source);
        var first = model.ActivateAsync();
        source.Requests[0].Result.SetResult([new("Alpha", "first"), new("Beta", "second")]);
        await first;
        model.SearchText = "Beta";
        var item = Assert.Single(model.Items);
        model.Deactivate();
        await model.ActivateAsync();
        Assert.Single(source.Requests);
        Assert.Equal("Beta", model.SearchText);
        Assert.Same(item, Assert.Single(model.Items));
        Assert.True(model.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task RecreatedViewModelHasIndependentStateAndLoadsAgain() {
        var source = new Source();
        var original = new __NAME__ViewModel(source);
        var first = original.ActivateAsync();
        source.Requests[0].Result.SetResult([new("Alpha", "")]);
        await first;
        original.SearchText = "Alpha";
        original.Deactivate();

        var recreated = new __NAME__ViewModel(source);
        Assert.Empty(recreated.SearchText);
        Assert.Empty(recreated.Items);
        var next = recreated.ActivateAsync();
        Assert.Equal(2, source.Requests.Count);
        source.Requests[1].Result.SetResult([new("Beta", "")]);
        await next;
        Assert.Equal("Beta", Assert.Single(recreated.Items).Title);
        Assert.Equal("Alpha", original.SearchText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCompletionAfterNewVisitFinishedCannotChangeDisplayedData(bool failure) {
        var source = new Source();
        var model = new __NAME__ViewModel(source);
        var old = model.ActivateAsync();
        model.Deactivate();
        var current = model.ActivateAsync();
        source.Requests[1].Result.SetResult([new("current", "")]);
        await current;
        if (failure) source.Requests[0].Result.SetException(new IOException("late error"));
        else source.Requests[0].Result.SetResult([new("stale", "")]);
        await old;
        Assert.Equal("current", Assert.Single(model.Items).Title);
        Assert.False(model.HasError);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task FailedRefreshRetainsDataAndSuccessfulRetryUsesCurrentQuery() {
        var source = new Source();
        var model = new __NAME__ViewModel(source);
        var first = model.ActivateAsync();
        source.Requests[0].Result.SetResult([new("Alpha", ""), new("Beta", "")]);
        await first;
        model.SearchText = "Beta";
        var refresh = model.RefreshAsync();
        source.Requests[1].Result.SetException(new IOException("offline"));
        await refresh;
        Assert.Equal("Beta", Assert.Single(model.Items).Title);
        Assert.True(model.HasError);
        model.Deactivate();
        await model.ActivateAsync();
        Assert.Equal(2, source.Requests.Count); // Loaded data is retained; retry is explicit.
        var retry = model.RefreshAsync();
        source.Requests[2].Result.SetResult([new("Beta new", ""), new("Gamma", "")]);
        await retry;
        Assert.Equal("Beta new", Assert.Single(model.Items).Title);
        Assert.False(model.HasError);
    }

    private sealed class Source : I__NAME__DataSource {
        public List<(TaskCompletionSource<IReadOnlyList<__NAME__Item>> Result, CancellationToken Token)> Requests { get; } = [];
        public Task<IReadOnlyList<__NAME__Item>> LoadAsync(CancellationToken cancellationToken) {
            var result = new TaskCompletionSource<IReadOnlyList<__NAME__Item>>();
            Requests.Add((result, cancellationToken));
            return result.Task;
        }
    }

    [Fact]
    public async Task SearchAndEmptyStateReflectLoadedItems() {
        var model = new __NAME__ViewModel();
        Assert.False(model.IsEmpty);
        await model.ActivateAsync();
        Assert.Equal(2, model.Items.Count);
        model.SearchText = "  DESKTOP  ";
        Assert.Single(model.Items);
        model.SearchText = "unmatched";
        Assert.True(model.IsEmpty);
        model.SearchText = "";
        Assert.Equal(2, model.Items.Count);
        model.SetLanguage("zh-CN");
        Assert.Equal("刷新", model.RefreshText);
        model.SetLanguage("en-US");
        Assert.Equal("Refresh", model.RefreshText);
    }

    [Fact]
    public async Task FailureCanBeRetriedAndDuplicateRefreshIsSuppressed() {
        var source = new Source();
        var model = new __NAME__ViewModel(source);
        var load = model.ActivateAsync();
        Assert.True(model.IsBusy);
        Assert.False(model.RefreshCommand.CanExecute(null));
        await model.RefreshAsync();
        Assert.Single(source.Requests);
        source.Requests[0].Result.SetException(new IOException("offline"));
        await load;
        Assert.True(model.HasError);
        Assert.Equal("offline", model.ErrorMessage);
        Assert.False(model.IsEmpty);
        Assert.True(model.RefreshCommand.CanExecute(null));
        var retry = model.RefreshAsync();
        source.Requests[1].Result.SetResult([]);
        await retry;
        Assert.False(model.HasError);
        Assert.True(model.IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateResultOrErrorFromPreviousVisitCannotOverwriteNewVisit(bool failure) {
        var source = new Source();
        var model = new __NAME__ViewModel(source);
        var old = model.ActivateAsync();
        model.Deactivate();
        Assert.True(source.Requests[0].Token.IsCancellationRequested);
        Assert.False(model.RefreshCommand.CanExecute(null));
        var current = model.ActivateAsync();
        if (failure) source.Requests[0].Result.SetException(new IOException("old failure"));
        else source.Requests[0].Result.SetResult([new("old", "")]);
        await old;
        Assert.True(model.IsBusy);
        Assert.False(model.HasError);
        Assert.Empty(model.Items);
        source.Requests[1].Result.SetResult([new("current", "")]);
        await current;
        Assert.Equal("current", Assert.Single(model.Items).Title);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task CancellationIsNotShownAsFailure() {
        var source = new Source();
        var model = new __NAME__ViewModel(source);
        var load = model.ActivateAsync();
        model.Deactivate();
        source.Requests[0].Result.SetCanceled(source.Requests[0].Token);
        await load;
        Assert.False(model.HasError);
        Assert.False(model.IsBusy);
    }
}
