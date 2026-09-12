using System.IO.Compression;

namespace XamlNexus.Common.Projects;

public static class XamlNexusBaselineContent {
    private const int MaximumDecodedBytes = 4 * 1024 * 1024;

    public static string Encode(byte[] content) {
        ArgumentNullException.ThrowIfNull(content);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(content);
        return Convert.ToBase64String(output.ToArray());
    }

    public static byte[] Decode(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        byte[] compressed = Convert.FromBase64String(value);
        using var input = new MemoryStream(compressed, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int total = 0;
        while (true) {
            int read = gzip.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            total += read;
            if (total > MaximumDecodedBytes)
                throw new InvalidDataException("The decoded scaffold baseline exceeds 4 MiB.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
