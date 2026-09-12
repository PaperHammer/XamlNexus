namespace Winui3_XamlNexus.Common.Updates {
    public sealed class AppUpdateDownloadProgressEventArgs(
        long bytesReceived,
        long? totalBytes) : EventArgs {
        public long BytesReceived { get; } = bytesReceived;
        public long? TotalBytes { get; } = totalBytes;
        public double? Percentage => TotalBytes is > 0
            ? Math.Min(100d, BytesReceived * 100d / TotalBytes.Value)
            : null;
    }

}
