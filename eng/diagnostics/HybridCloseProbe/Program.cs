using System.Diagnostics;
using System.Text.Json;
using Winui3_Wpf_XamlNexus.Common.Utils.IPC;

internal static class Program {
    private static async Task<int> Main(string[] args) {
        if (args.Length == 2 && args[0] == "--child") {
            int calls = 0;
            await UIHostControl.ListenAsync(Console.In, Console.Out, async token => {
                calls++;
                switch (args[1]) {
                    case "cancel-retry": return calls > 1;
                    case "saving": await Task.Delay(4500, token); return true;
                    case "failed": throw new IOException("synthetic save failure");
                    case "unresponsive": await Task.Delay(Timeout.Infinite, token); return false;
                    default: return true;
                }
            }, exception => Console.Error.WriteLine(exception.Message));
            return 0;
        }
        var results = new List<object>();
        foreach (string mode in new[] { "cancel-retry", "saving", "failed", "unresponsive" }) {
            var coordinator = new UICloseCoordinator();
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "HybridCloseProbe.exe")) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add("--child"); start.ArgumentList.Add(mode);
            using Process child = Process.Start(start)!;
            child.EnableRaisingEvents = true;
            child.Exited += (_, _) => coordinator.ProcessExited(child.Id);
            Task errors = child.StandardError.ReadToEndAsync();
            Task read = Task.Run(async () => {
                while (await child.StandardOutput.ReadLineAsync() is string line)
                    coordinator.Receive(child.Id, line);
            });
            async Task Send(Guid id) {
                await child.StandardInput.WriteLineAsync(UIHostControl.Request(id));
                await child.StandardInput.FlushAsync();
                if (child.HasExited) coordinator.ProcessExited(child.Id);
            }
            bool passed = false;
            try {
                using var cancel = new CancellationTokenSource();
                Task<bool> close = coordinator.RequestAsync(child.Id, Send, cancel.Token);
                switch (mode) {
                    case "cancel-retry":
                        bool rejected = !await close.WaitAsync(TimeSpan.FromSeconds(10)) && !child.HasExited;
                        Task<bool> retry = coordinator.RequestAsync(child.Id, Send);
                        passed = rejected && await retry.WaitAsync(TimeSpan.FromSeconds(10));
                        break;
                    case "saving":
                        await Task.Delay(3600);
                        bool stillSaving = !close.IsCompleted && !child.HasExited;
                        passed = stillSaving && await close.WaitAsync(TimeSpan.FromSeconds(10));
                        break;
                    case "failed": passed = !await close.WaitAsync(TimeSpan.FromSeconds(10)) && !child.HasExited; break;
                    case "unresponsive":
                        await Task.Delay(300); cancel.Cancel();
                        try { await close; } catch (OperationCanceledException) { passed = !child.HasExited; }
                        break;
                }
                results.Add(new { mode, passed, pid = child.Id });
            }
            finally {
                // Cleanup applies only to the probe child owned by this test, never a user's UI.
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(); await read; await errors;
            }
            if (!passed) { Console.Error.WriteLine(JsonSerializer.Serialize(results)); return 1; }
        }
        Console.WriteLine(JsonSerializer.Serialize(results));
        return 0;
    }
}
