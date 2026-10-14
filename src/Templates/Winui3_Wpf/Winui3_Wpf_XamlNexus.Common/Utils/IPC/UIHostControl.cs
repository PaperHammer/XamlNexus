namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC;

// Private stdin/stdout channel owned by UIRunnerService. Existing gRPC wire values stay unchanged.
public static class UIHostControl {
    private const string RequestPrefix = "XAMLNEXUS_UI_CLOSE:";
    private const string ResponsePrefix = "XAMLNEXUS_UI_CLOSE_RESULT:";
    public static string Request(Guid id) => RequestPrefix + id.ToString("N");
    public static bool TryReadResponse(string line, out Guid id) {
        id = default;
        if (!line.StartsWith(ResponsePrefix, StringComparison.Ordinal)) return false;
        string[] fields = line[ResponsePrefix.Length..].Split(':');
        return fields.Length == 2 && Guid.TryParseExact(fields[0], "N", out id) &&
            fields[1] is "cancelled" or "failed";
    }

    public static async Task ListenAsync(TextReader input, TextWriter output,
        Func<CancellationToken, Task<bool>> close, Action<Exception> reportError,
        CancellationToken cancellationToken = default) {
        while (await input.ReadLineAsync(cancellationToken) is string line) {
            if (!line.StartsWith(RequestPrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(line[RequestPrefix.Length..], "N", out Guid id)) continue;
            string result = "cancelled";
            try {
                if (await close(cancellationToken)) return; // Host waits for actual process exit.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception exception) { reportError(exception); result = "failed"; }
            await output.WriteLineAsync(ResponsePrefix + id.ToString("N") + ":" + result);
            await output.FlushAsync(cancellationToken);
        }
    }
}
