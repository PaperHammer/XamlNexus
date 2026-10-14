using System.Text.Json;
using Xunit;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.UIComponent.Activation;
using Winui3_Wpf_XamlNexus.UIComponent.Navigation;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;
using Winui3_Wpf_XamlNexus.UIComponent.Utils.Extensions;

namespace XamlNexus.TemplateTests;

public sealed class HybridBusinessActivationTests {
    public HybridBusinessActivationTests() => CrossThreadInvoker.Initialize(new InlineContext());
    private sealed class InlineContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
    }
    private sealed class BusinessPage : ArcPage {
        public override Type ArcType => typeof(BusinessPage);
        protected override bool IsMultiInstance => true;
        public Task<bool> Decision { get; set; } = Task.FromResult(true);
        public int Confirmations { get; private set; }
        public string? Id { get; private set; }
        public override Task<bool> CanLeaveAsync(ArcLeaveReason reason, CancellationToken cancellationToken = default) {
            Confirmations++;
            Assert.Equal(ArcLeaveReason.Navigation, reason);
            return Decision.WaitAsync(cancellationToken);
        }
        protected override void OnEnter(FrameworkPayload? payload) {
            base.OnEnter(payload);
            Id = payload.GetBusinessString("id");
        }
    }
    private static BusinessActivationRequest Request(string id) =>
        BusinessActivationRequest.FromArguments(new[] { "--route", "orders", "--data", JsonSerializer.Serialize(new { id }) })!;
    private static (ArcNavigationContentView View, BusinessPage Page, BusinessActivationNavigator Router) Host() {
        var view = new ArcNavigationContentView();
        var page = new BusinessPage();
        view.PageMap[page.ArcType] = page;
        view.ContentGrid.Children.Add(page);
        page.NavigateEnter(new FrameworkPayload { ["id"] = "original" });
        page.SetActiveStatus();
        var registry = new NavigationRegistry();
        registry.Register(new("orders", page.ArcType, "Orders"));
        return (view, page, new BusinessActivationNavigator(registry));
    }
    private static Task<ActivationNavigationResult> Navigate(BusinessActivationNavigator router,
        ArcNavigationContentView view, BusinessActivationRequest request, CancellationToken token = default) =>
        router.NavigateAsync(request, (type, payload, ct) => view.ArcNavigateAsync(type, payload, cancellationToken: ct), token);

    [Fact]
    public void Ordinary_launch_has_no_business_request() {
        Assert.Null(BusinessActivationRequest.FromArguments(Array.Empty<string>()));
        Assert.Null(BusinessActivationRequest.FromArguments(new[] { "app.exe", "--xamlnexus-run", "unrelated" }));
    }

    [Theory]
    [InlineData("--route")]
    [InlineData("--route", "")]
    [InlineData("--route", "orders", "--route", "other")]
    [InlineData("--route", "orders", "--data")]
    [InlineData("--data", "{}")]
    [InlineData("--route", "--data", "{}")]
    [InlineData("--route", "orders", "--data", "bad-json")]
    [InlineData("--route", "orders", "--data", "[]")]
    [InlineData("--route", "orders", "--data", "null")]
    [InlineData("--route", "orders", "--data", "{}", "--data", "{}")]
    public void Malformed_reserved_arguments_are_rejected(params string[] args) =>
        Assert.Throws<FormatException>(() => BusinessActivationRequest.FromArguments(args));

    [Fact]
    public void Request_keeps_an_owned_snapshot_and_object_data() {
        var args = new[] { "--route", "orders", "--data", "{\"id\":\"订单 42\",\"nested\":{\"ok\":true}}" };
        var request = BusinessActivationRequest.FromArguments(args)!;
        args[1] = "changed";
        Assert.Equal("orders", request.Route);
        Assert.Equal("orders", request.Arguments[1]);
        Assert.Equal("订单 42", request.Data.GetProperty("id").GetString());
        Assert.True(request.Data.GetProperty("nested").GetProperty("ok").GetBoolean());
        Assert.Empty(BusinessActivationRequest.FromArguments(new[] { "--route", "orders" })!.Data.EnumerateObject());
    }

    [Fact]
    public void Windows_quoting_preserves_spaces_unicode_and_json_for_full_or_argument_only_launch() {
        string data = "{\"id\":\"订单 42\"}";
        string quoted = "\"{\\\"id\\\":\\\"订单 42\\\"}\"";
        foreach (string prefix in new[] { "", "\"C:\\Program Files\\App.exe\" " }) {
            var tokens = ActivationCommandLine.Split(prefix + "--route orders --data " + quoted);
            var request = BusinessActivationRequest.FromArguments(tokens)!;
            Assert.Equal(data, request.Data.GetRawText());
            Assert.Equal("orders", request.Route);
        }
        Assert.Empty(ActivationCommandLine.Split(""));
    }

    [Fact]
    public async Task Unknown_route_does_not_prompt_or_mutate_an_active_page() {
        var (view, page, router) = Host();
        try {
            var payload = page.Payload;
            var request = BusinessActivationRequest.FromArguments(new[] { "--route", "Orders" })!;
            Assert.Equal(ActivationNavigationResult.RouteNotFound, await Navigate(router, view, request));
            Assert.Same(payload, page.Payload);
            Assert.Equal(0, page.Confirmations);
        }
        finally { page.RaiseUnloaded(); }
    }

    [Fact]
    public async Task Same_page_activation_replaces_record_only_after_leave_approval() {
        var (view, page, router) = Host();
        try {
            var original = page.Payload;
            page.Decision = Task.FromResult(false);
            Assert.Equal(ActivationNavigationResult.Cancelled, await Navigate(router, view, Request("cancelled")));
            Assert.Equal("original", page.Id);
            Assert.Same(original, page.Payload);
            page.Decision = Task.FromResult(true);
            var next = Request("next");
            Assert.Equal(ActivationNavigationResult.Navigated, await Navigate(router, view, next));
            Assert.Equal("next", page.Id);
            Assert.Same(next, page.Payload!.Get<BusinessActivationRequest>(BusinessActivationRequest.PayloadKey));
            Assert.Equal("original", original!.Get<string>("id"));
            Assert.Single(view.PageMap);
            Assert.Equal(2, page.Confirmations);
        }
        finally { page.RaiseUnloaded(); }
    }

    [Fact]
    public async Task Next_activation_and_legacy_in_app_navigation_supersede_cached_business_data() {
        var (view, page, router) = Host();
        try {
            Assert.Equal(ActivationNavigationResult.Navigated, await Navigate(router, view, Request("first")));
            var firstPayload = page.Payload;
            Assert.Equal(ActivationNavigationResult.Navigated, await Navigate(router, view, Request("second")));
            Assert.Equal("second", page.Id);
            Assert.Equal("first", firstPayload!.GetBusinessString("id"));
            Assert.True(await view.ArcNavigateAsync(page.ArcType, new FrameworkPayload { ["id"] = "in-app" }));
            Assert.Equal("in-app", page.Id);
            Assert.False(page.Payload!.TryGet<BusinessActivationRequest>(BusinessActivationRequest.PayloadKey, out _));
            Assert.Equal(ActivationNavigationResult.Navigated, await Navigate(router, view,
                BusinessActivationRequest.FromArguments(new[] { "--route", "orders" })!));
            Assert.Null(page.Id); // Missing id means a new form, never the cached previous record.
        }
        finally { page.RaiseUnloaded(); }
    }

    [Fact]
    public async Task Cancellation_during_confirmation_keeps_payload_and_allows_retry() {
        var (view, page, router) = Host();
        try {
            var original = page.Payload;
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Decision = gate.Task;
            using var cancellation = new CancellationTokenSource();
            var pending = Navigate(router, view, Request("cancelled"), cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Same(original, page.Payload);
            Assert.Equal("original", page.Id);
            page.Decision = Task.FromResult(true);
            Assert.Equal(ActivationNavigationResult.Navigated, await Navigate(router, view, Request("retry")));
            Assert.Equal("retry", page.Id);
        }
        finally { page.RaiseUnloaded(); }
    }
}
