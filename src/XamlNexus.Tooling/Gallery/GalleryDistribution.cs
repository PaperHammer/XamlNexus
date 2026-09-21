using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XamlNexus.Tooling.Gallery;

public sealed record GalleryAsset(string RuntimeIdentifier, string Url, string Sha256);

/// <summary>随工具包分发的固定版本清单；不查询远程 latest，避免示例与模板错配。</summary>
public sealed partial record GalleryManifest(int SchemaVersion, string Version, GalleryAsset[] Assets) {
    public static GalleryManifest Read(string path, string toolVersion) {
        var manifest = JsonSerializer.Deserialize<GalleryManifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing Gallery manifest.");

        if (manifest.SchemaVersion != 1 || manifest.Version != toolVersion ||
            !ManifestVersionRegex().IsMatch(manifest.Version) ||
            manifest.Assets is not { Length: > 0 })
            throw new InvalidDataException("Gallery manifest does not match this tool version.");

        if (manifest.Assets.Select(a => a.RuntimeIdentifier).Distinct().Count() != manifest.Assets.Length)
            throw new InvalidDataException("Duplicate Gallery architecture.");

        foreach (var asset in manifest.Assets) {
            if (asset.RuntimeIdentifier != "win-x64" ||
                !AssetsSHA256Regex().IsMatch(asset.Sha256 ?? "") ||
                !Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                uri.Host != "github.com" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath != $"/PaperHammer/XamlNexus/releases/download/v{manifest.Version}/XamlNexus%20Gallery%20v{manifest.Version}.zip")
                throw new InvalidDataException("Invalid Gallery release asset.");
        }
        return manifest;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$")]
    private static partial Regex ManifestVersionRegex();

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex AssetsSHA256Regex();
}

public static class GalleryDistribution {
    private const string Executable = "XamlNexus.Gallery.exe";
    private const long MaxArchiveBytes = 512L * 1024 * 1024;
    private const long MaxExpandedBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>下载到临时目录，校验并解压成功后原子发布；并发启动共享同一把文件锁。</summary>
    public static async Task<string> EnsureInstalledAsync(string cacheRoot, GalleryManifest manifest,
        GalleryAsset asset, Func<Uri, CancellationToken, Task<Stream>> download,
        Action<string>? progress = null, CancellationToken cancellationToken = default) {
        // 同样验证调用方传入的路径片段，缓存目录不能由不可信清单任意指定。
        if (!Regex.IsMatch(manifest.Version, @"^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$") ||
            asset.RuntimeIdentifier != "win-x64" ||
            !Regex.IsMatch(asset.Sha256, "^[a-fA-F0-9]{64}$"))
            throw new InvalidDataException("Invalid Gallery cache identity.");

        string root = Path.GetFullPath(cacheRoot);
        RejectLinks(root);
        Directory.CreateDirectory(root);
        string identity = $"{manifest.Version}-{asset.RuntimeIdentifier}-{asset.Sha256.ToLowerInvariant()}";
        string installed = Path.Combine(root, identity);
        string lockPath = Path.Combine(root, identity + ".lock");
        RejectLinks(lockPath);
        using var lease = await AcquireLockAsync(lockPath, cancellationToken);
        RejectLinks(installed);
        string executable = Path.Combine(installed, Executable);
        string marker = Path.Combine(installed, ".complete");
        if (File.Exists(marker) && File.Exists(executable)) {
            RejectLinks(marker);
            RejectLinks(executable);
            if (File.ReadAllText(marker) == asset.Sha256) return executable;
        }

        string staging = Path.Combine(root, ".download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try {
            progress?.Invoke("download");
            string zipPath = Path.Combine(staging, "gallery.zip");
            await using (var input = await download(new Uri(asset.Url), cancellationToken))
            await using (var output = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0) {
                    total += read;
                    if (total > MaxArchiveBytes) throw new InvalidDataException("Gallery archive is too large.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            await using (var zip = File.OpenRead(zipPath)) {
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(zip, cancellationToken));
                if (!actual.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Gallery SHA-256 verification failed. Retry the command to download again.");
            }
            progress?.Invoke("extract");
            string payload = Path.Combine(staging, "app");
            Directory.CreateDirectory(payload);
            using (var zip = ZipFile.OpenRead(zipPath)) {
                long expanded = 0;
                foreach (var entry in zip.Entries) {
                    cancellationToken.ThrowIfCancellationRequested();
                    // 禁止目录穿越、符号链接和 NTFS ADS；只允许普通发布资源。
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.StartsWith('/') || name.Split('/').Any(p => p is ".." or "." || p.Contains(':')) ||
                        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("Unsafe Gallery archive entry.");
                    expanded += entry.Length;
                    if (expanded > MaxExpandedBytes) throw new InvalidDataException("Expanded Gallery is too large.");
                }
                zip.ExtractToDirectory(payload);
            }
            foreach (string required in new[] { Executable, "XamlNexus.Gallery.pri", "App.xbf", "MainWindow.xbf" })
                if (!File.Exists(Path.Combine(payload, required))) throw new InvalidDataException($"Gallery resource missing: {required}");
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(Path.Combine(payload, ".complete"), asset.Sha256, cancellationToken);
            // 不覆盖正在运行的旧版，也不删除用户数据；不完整缓存移到旁边便于恢复。
            if (Directory.Exists(installed)) Directory.Move(installed, installed + ".incomplete-" + Guid.NewGuid().ToString("N"));
            Directory.Move(payload, installed);

            return executable;
        }
        finally {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static void RejectLinks(string path) {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Gallery cache cannot contain filesystem links.");
    }

    private static async Task<FileStream> AcquireLockAsync(string path, CancellationToken cancellationToken) {
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < deadline) { await Task.Delay(250, cancellationToken); }
        }
    }
}
