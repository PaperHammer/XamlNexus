using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Winui3_Wpf_XamlNexus.Common.Utils.IPC;
using Winui3_Wpf_XamlNexus.UIComponent.Activation;
using Winui3_Wpf_XamlNexus.UIComponent.Navigation;

internal static class Program {
    private static readonly string Executable = Path.Combine(AppContext.BaseDirectory, "HybridActivationProbe.exe");
    private static async Task<int> Main(string[] args) {
        try {
            if (args.Length > 0 && args[0] == "--forward") {
                await HybridActivationPipe.ForwardAsync(args[1], args.Skip(2).ToArray());
                return 0;
            }
            if (args.Length > 0 && args[0] == "--ui") {
                await Task.Delay(500); // Simulate modules/UI not yet ready.
                string selected = "original", draft = "unsaved draft";
                var registry = new NavigationRegistry();
                registry.Register(new("Orders", typeof(Program), "Orders"));
                var navigator = new BusinessActivationNavigator(registry);
                await UIHostControl.ListenAsync(Console.In, Console.Out, _ => Task.FromResult(false),
                    async (arguments, token) => {
                        var request = BusinessActivationRequest.FromArguments(arguments);
                        if (request is null) {
                            Console.WriteLine("EVIDENCE:" + JsonSerializer.Serialize(new { ordinary = true, selected, draft, pid = Environment.ProcessId }));
                            return;
                        }
                        string? id = request.Data.GetProperty("id").GetString();
                        var result = await navigator.NavigateAsync(request, (_, payload, _) => {
                            if (id == "cancel") return Task.FromResult(false);
                            selected = payload.GetBusinessString("id")!;
                            return Task.FromResult(true);
                        }, token);
                        Console.WriteLine("EVIDENCE:" + JsonSerializer.Serialize(new { id, result = result.ToString(), selected, draft, pid = Environment.ProcessId }));
                    }, exception => Console.Error.WriteLine(exception.Message));
                return 0;
            }

            string key = Guid.NewGuid().ToString("N");
            var events = Channel.CreateUnbounded<JsonElement>();
            var errors = Channel.CreateUnbounded<Exception>();
            var coordinator = new UIActivationCoordinator();
            Process? ui = null;
            Task? reader = null, stderr = null;
            int creations = 0;
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            void StartUi() {
                ui = Start("--ui");
                creations++;
                Process child = ui;
                child.EnableRaisingEvents = true;
                child.Exited += (_, _) => coordinator.ProcessExited(child.Id);
                stderr = child.StandardError.ReadToEndAsync();
                reader = Task.Run(async () => {
                    while (await child.StandardOutput.ReadLineAsync() is string line) {
                        if (coordinator.Receive(child.Id, line)) continue;
                        if (line.StartsWith("EVIDENCE:", StringComparison.Ordinal))
                            events.Writer.TryWrite(JsonSerializer.Deserialize<JsonElement>(line[9..]));
                    }
                });
            }
            async Task StopUi() {
                if (ui is null) return;
                if (!ui.HasExited) ui.Kill(entireProcessTree: true); // Probe-owned child only.
                await ui.WaitForExitAsync(); await reader!; await stderr!;
                ui.Dispose(); ui = null;
            }
            async Task Forward(params string[] arguments) {
                using Process launcher = Start(new[] { "--forward", key }.Concat(arguments).ToArray());
                Task<string> error = launcher.StandardError.ReadToEndAsync();
                Task<string> output = launcher.StandardOutput.ReadToEndAsync();
                await launcher.WaitForExitAsync(stop.Token);
                if (launcher.ExitCode != 0) throw new IOException(await error);
                await output;
            }
            async Task<JsonElement> Next() => await events.Reader.ReadAsync(stop.Token);
            var checks = new List<string>();
            using var host = new HybridActivationPipe(key, exception => errors.Writer.TryWrite(exception));
            try {
                string special = "中文 空格 \" \\ end";
                // Secondary process exits with receipt even before host readiness and UI creation.
                await Forward("--route", "Orders", "--data", JsonSerializer.Serialize(new { id = special }));
                Require(ui is null && !events.Reader.TryRead(out _), "pre-readiness queue");
                host.StartHandling(async (arguments, token) => {
                    if (ui is null) StartUi();
                    Process child = ui!;
                    await coordinator.RequestAsync(child.Id, async id => {
                        await child.StandardInput.WriteLineAsync(UIHostControl.Activation(id, arguments));
                        await child.StandardInput.FlushAsync();
                    }, token);
                });
                var first = await Next();
                Require(first.GetProperty("selected").GetString() == special && creations == 1, "first delivery and Unicode/quotes");
                checks.Add("initialization queue creates one UI and preserves complete JSON");
                int originalPid = first.GetProperty("pid").GetInt32();
                await Forward("--route", "Orders", "--data", "{\"id\":\"cancel\"}");
                var cancelled = await Next();
                Require(cancelled.GetProperty("result").GetString() == "Cancelled" &&
                    cancelled.GetProperty("selected").GetString() == special &&
                    cancelled.GetProperty("draft").GetString() == "unsaved draft", "cancel preserves state");
                checks.Add("actual router cancellation retains modeled page and draft");
                await Forward();
                var ordinary = await Next();
                Require(ordinary.GetProperty("ordinary").GetBoolean() &&
                    ordinary.GetProperty("selected").GetString() == special, "ordinary activation");
                checks.Add("ordinary launch keeps current business record");
                await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Forward("--route", "Orders", "--data",
                    JsonSerializer.Serialize(new { id = i.ToString() }))));
                var ids = new HashSet<string>();
                for (int i = 0; i < 8; i++) {
                    var item = await Next();
                    Require(item.GetProperty("pid").GetInt32() == originalPid, "existing UI reused");
                    ids.Add(item.GetProperty("id").GetString()!);
                }
                Require(ids.Count == 8 && creations == 1, "concurrent delivery once");
                checks.Add("eight concurrent secondary processes reuse UI and deliver once");
                await StopUi();
                await Forward("--route", "Orders", "--data", "{\"id\":\"recreated\"}");
                var recreated = await Next();
                Require(creations == 2 && recreated.GetProperty("selected").GetString() == "recreated" &&
                    recreated.GetProperty("pid").GetInt32() != originalPid, "recreate after UI exit");
                checks.Add("closed UI is recreated for the next activation");
                await StopUi();
                host.Dispose();
                try {
                    await HybridActivationPipe.ForwardAsync(key, Array.Empty<string>(), timeout: TimeSpan.FromMilliseconds(250));
                    throw new Exception("Exited host unexpectedly acknowledged.");
                }
                catch (TimeoutException) { }
                Require(!errors.Reader.TryRead(out _), "host worker failure");
                checks.Add("owner exit produces bounded failure with no replacement or replay");
                Console.WriteLine(JsonSerializer.Serialize(new { passed = true, checks,
                    boundary = "Real processes and template IPC/router. UIRunnerService, native windows, focus and ContentDialog are not executed." }));
                return 0;
            }
            finally { stop.Cancel(); await StopUi(); }
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static Process Start(params string[] arguments) {
        var start = new ProcessStartInfo(Executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
