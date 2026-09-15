using System.IO.Compression;

namespace XamlNexus.Common.Projects;

/// <summary>
/// 将脚手架文件的基线内容压缩为可存储在 JSON 中的字符串，并在需要时还原。
/// 基线保存的是文件内容快照，供后续升级时比较和合并使用。
/// </summary>
public static class XamlNexusBaselineContent {
    // 单份基线最多解压为 4 MiB，防止异常压缩内容导致过大的解压输出。
    private const int MaximumDecodedBytes = 4 * 1024 * 1024;

    /// <summary>
    /// 将原始字节先进行 GZip 压缩，再转换成 Base64 文本；此过程不提供加密。
    /// </summary>
    /// <param name="content">需要保存的原始文件内容。</param>
    /// <returns>包含 GZip 压缩数据的 Base64 字符串。</returns>
    public static string Encode(byte[] content) {
        ArgumentNullException.ThrowIfNull(content);
        // 内存流接收压缩后的字节，无需创建临时文件。
        using var output = new MemoryStream();
        // 优先选择较小的压缩体积。离开此 using 块时，gzip 会完成压缩并写入尾部数据。
        // leaveOpen 保留底层 output 流，方便随后读取压缩结果。
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(content);
        // 必须等 gzip 完成写入后再读取；Base64 将二进制数据表示为文本。
        return Convert.ToBase64String(output.ToArray());
    }

    /// <summary>
    /// 将 Base64 文本还原为 GZip 数据，并分块解压；输出超过 4 MiB 时拒绝继续处理。
    /// </summary>
    /// <param name="value">包含 GZip 压缩数据的 Base64 字符串。</param>
    /// <returns>解压后的原始文件字节。</returns>
    public static byte[] Decode(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        // 先还原压缩字节；Base64 解码本身并不执行解压。
        byte[] compressed = Convert.FromBase64String(value);
        // input 只读，gzip 从中读取并解压，output 收集还原后的内容。
        // using var 会在方法退出（包括抛出异常）时按声明的相反顺序释放流。
        using var input = new MemoryStream(compressed, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        // 重复使用 80 KiB 缓冲区，分块读取，并在每次写入前检查累计解压大小。
        var buffer = new byte[81920];
        int total = 0;
        while (true) {
            int read = gzip.Read(buffer, 0, buffer.Length);
            // 返回 0 表示解压流已读完；非零时，实际读到的字节可能少于缓冲区长度。
            if (read == 0) break;
            total += read;
            if (total > MaximumDecodedBytes)
                throw new InvalidDataException("The decoded scaffold baseline exceeds 4 MiB.");
            output.Write(buffer, 0, read); // 只写入本次有效字节，避免带入缓冲区中的旧数据。
        }
        return output.ToArray();
    }
}
