using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC;

// Receipt acknowledges bounded in-memory queuing, not successful navigation or durable delivery.
public sealed class HybridActivationPipe : IDisposable {
    public const int MaximumBytes = 65536;
    private readonly string _name;
    private readonly Action<Exception> _reportError;
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<string[]> _queue = Channel.CreateBounded<string[]>(64);
    private readonly Task _listen;
    private Task? _drain;
    private int _disposed;

    public HybridActivationPipe(string applicationKey, Action<Exception> reportError) {
        _name = GetPipeName(applicationKey);
        _reportError = reportError;
        // Publish before host modules initialize. Processing starts explicitly after readiness.
        _listen = ListenAsync();
    }

    public static string GetPipeName(string applicationKey) =>
        "XamlNexus.Activation." + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(applicationKey + "|" + Environment.UserName)));

    public void StartHandling(Func<string[], CancellationToken, Task> handle) {
        if (_drain is not null) throw new InvalidOperationException("Activation handling already started.");
        _drain = Task.Run(async () => {
            try {
                await foreach (string[] arguments in _queue.Reader.ReadAllAsync(_stop.Token)) {
                    try { await handle(arguments, _stop.Token); }
                    catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                    catch (Exception exception) { _reportError(exception); }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }

    public static async Task ForwardAsync(string applicationKey, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default, TimeSpan? timeout = null) {
        byte[] bytes = Serialize(arguments);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        using var pipe = new NamedPipeClientStream(".", GetPipeName(applicationKey),
            PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try {
            await pipe.ConnectAsync(deadline.Token);
            await WriteFrameAsync(pipe, bytes, deadline.Token);
            byte[] receipt = new byte[1];
            await pipe.ReadExactlyAsync(receipt, deadline.Token);
            if (receipt[0] != 1) throw new IOException("The running host rejected activation (closing or queue full).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("The running host did not acknowledge activation within the deadline. Delivery is uncertain; do not automatically replay.");
        }
    }

    internal static byte[] Serialize(IReadOnlyList<string> arguments) {
        if (arguments.Count > 128 || arguments.Any(argument => argument is null))
            throw new ArgumentException("Activation supports at most 128 non-null arguments.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(arguments);
        if (bytes.Length > MaximumBytes) throw new ArgumentException("Activation arguments exceed 64 KiB.");
        return bytes;
    }

    private async Task ListenAsync() {
        try {
            while (!_stop.IsCancellationRequested) {
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                try {
                    byte[] length = new byte[4];
                    await pipe.ReadExactlyAsync(length, deadline.Token);
                    int count = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(length);
                    if (count < 2 || count > MaximumBytes) throw new IOException("Invalid activation frame length.");
                    byte[] bytes = new byte[count];
                    await pipe.ReadExactlyAsync(bytes, deadline.Token);
                    string[] arguments = JsonSerializer.Deserialize<string[]>(bytes)
                        ?? throw new IOException("Invalid activation arguments.");
                    _ = Serialize(arguments);
                    bool accepted = !_stop.IsCancellationRequested && _queue.Writer.TryWrite(arguments);
                    await pipe.WriteAsync(new byte[] { accepted ? (byte)1 : (byte)0 }, deadline.Token);
                    await pipe.FlushAsync(deadline.Token);
                }
                catch (OperationCanceledException) when (!_stop.IsCancellationRequested) {
                    _reportError(new TimeoutException("An activation connection did not finish its frame."));
                }
                catch (Exception exception) when (exception is IOException or JsonException or ArgumentException) {
                    _reportError(exception); // A malformed client cannot terminate the listener.
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception exception) { _reportError(exception); }
    }

    private static async Task WriteFrameAsync(Stream stream, byte[] bytes, CancellationToken token) {
        byte[] length = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        await stream.WriteAsync(length, token);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _queue.Writer.TryComplete();
        _stop.Cancel(); // Drop pending requests; never start another host as a fallback.
    }
}
