using System.Text.Json;
using System.Threading.Channels;

namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC;

// Private stdin/stdout channel owned by UIRunnerService. Existing gRPC wire values stay unchanged.
public static class UIHostControl {
    private const string RequestPrefix = "XAMLNEXUS_UI_CLOSE:";
    private const string ResponsePrefix = "XAMLNEXUS_UI_CLOSE_RESULT:";
    private const string ActivationPrefix = "XAMLNEXUS_UI_ACTIVATE:";
    private const string ActivationResponsePrefix = "XAMLNEXUS_UI_ACTIVATE_RESULT:";
    public static string Request(Guid id) => RequestPrefix + id.ToString("N");
    public static string Activation(Guid id, IReadOnlyList<string> arguments) =>
        ActivationPrefix + id.ToString("N") + ":" + Convert.ToBase64String(HybridActivationPipe.Serialize(arguments));

    public static bool TryReadResponse(string line, out Guid id) {
        id = default;
        if (!line.StartsWith(ResponsePrefix, StringComparison.Ordinal)) return false;
        string[] fields = line[ResponsePrefix.Length..].Split(':');
        return fields.Length == 2 && Guid.TryParseExact(fields[0], "N", out id) &&
            fields[1] is "cancelled" or "failed";
    }

    public static bool TryReadActivationResponse(string line, out Guid id, out bool accepted) {
        id = default;
        accepted = false;
        if (!line.StartsWith(ActivationResponsePrefix, StringComparison.Ordinal)) return false;
        string[] fields = line[ActivationResponsePrefix.Length..].Split(':');
        if (fields.Length != 2 || !Guid.TryParseExact(fields[0], "N", out id) ||
            fields[1] is not ("accepted" or "rejected")) return false;
        accepted = fields[1] == "accepted";
        return true;
    }

    public static Task ListenAsync(TextReader input, TextWriter output,
        Func<CancellationToken, Task<bool>> close, Action<Exception> reportError,
        CancellationToken cancellationToken = default) =>
        ListenAsync(input, output, close, null, reportError, cancellationToken);

    public static async Task ListenAsync(TextReader input, TextWriter output,
        Func<CancellationToken, Task<bool>> close,
        Func<string[], CancellationToken, Task>? activate, Action<Exception> reportError,
        CancellationToken cancellationToken = default) {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var writeGate = new SemaphoreSlim(1, 1);
        var queue = Channel.CreateBounded<Func<CancellationToken, Task>>(64);
        async Task Reply(string line) {
            await writeGate.WaitAsync(stop.Token);
            try { await output.WriteLineAsync(line); await output.FlushAsync(stop.Token); }
            finally { writeGate.Release(); }
        }
        Task worker = Task.Run(async () => {
            try {
                await foreach (var action in queue.Reader.ReadAllAsync(stop.Token)) {
                    try { await action(stop.Token); }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                    catch (Exception exception) { reportError(exception); }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        });
        try {
            // Console.In may implement ReadLineAsync synchronously and ignore cancellation.
            // Keep that owned stdin read off the caller and stop waiting when the UI exits.
            while (await Task.Run(async () => await input.ReadLineAsync(stop.Token))
                .WaitAsync(stop.Token) is string line) {
                if (line.StartsWith(ActivationPrefix, StringComparison.Ordinal)) {
                    string[] fields = line[ActivationPrefix.Length..].Split(':', 2);
                    if (fields.Length != 2 || !Guid.TryParseExact(fields[0], "N", out Guid id)) continue;
                    bool accepted = false;
                    try {
                        if (fields[1].Length > 90000) throw new FormatException("Activation frame is too large.");
                        string[] arguments = JsonSerializer.Deserialize<string[]>(Convert.FromBase64String(fields[1]))
                            ?? throw new FormatException("Invalid activation arguments.");
                        _ = HybridActivationPipe.Serialize(arguments);
                        if (activate is not null)
                            accepted = queue.Writer.TryWrite(token => activate(arguments, token));
                    }
                    catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException) {
                        reportError(exception);
                    }
                    await Reply(ActivationResponsePrefix + id.ToString("N") + ":" + (accepted ? "accepted" : "rejected"));
                }
                else if (line.StartsWith(RequestPrefix, StringComparison.Ordinal) &&
                    Guid.TryParseExact(line[RequestPrefix.Length..], "N", out Guid id)) {
                    bool queued = queue.Writer.TryWrite(async token => {
                        string result = "cancelled";
                        try {
                            if (await close(token)) { stop.Cancel(); return; } // Host still waits for actual process exit.
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                        catch (Exception exception) { reportError(exception); result = "failed"; }
                        await Reply(ResponsePrefix + id.ToString("N") + ":" + result);
                    });
                    if (!queued) await Reply(ResponsePrefix + id.ToString("N") + ":failed");
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally {
            queue.Writer.TryComplete();
            // EOF completes already received work; explicit UI exit cancels pending work.
            await worker;
        }
    }
}
