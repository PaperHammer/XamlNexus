using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using XamlNexus.Tooling.Gallery;

namespace XamlNexus;

internal partial class Program {
    private static string GalleryText(string en, string zh) => CultureInfo.CurrentUICulture.Name.StartsWith("zh") ? zh : en;

    private static async Task<int> RunGalleryAsync() {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) {
            Console.Error.WriteLine(GalleryText("Gallery requires Windows 10 1809 or later.", "Gallery 需要 Windows 10 1809 或更新版本。"));
            return GenerationFailureExitCode;
        }
        string? rid = RuntimeInformation.OSArchitecture switch {
            Architecture.X64 => "win-x64", Architecture.Arm64 => "win-arm64", _ => null,
        };
        if (rid is null) {
            Console.Error.WriteLine(GalleryText("Gallery supports Windows x64 and ARM64.", "Gallery 支持 Windows x64 和 ARM64。"));
            return GenerationFailureExitCode;
        }
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "gallery-manifest.json");
        if (!File.Exists(manifestPath)) {
            Console.Error.WriteLine(GalleryText("This tool build has no Gallery release manifest. Install an official package, or build samples/XamlNexus.Gallery from source.",
                "此工具构建未包含 Gallery 发布清单。请安装正式工具包，或从 samples/XamlNexus.Gallery 构建源码。"));
            return GenerationFailureExitCode;
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMinutes(15));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try {
            var manifest = GalleryManifest.Read(manifestPath, GetVersion());
            var asset = manifest.Assets.SingleOrDefault(a => a.RuntimeIdentifier == rid)
                ?? throw new InvalidDataException(GalleryText("No Gallery release for this architecture.", "此版本未提供当前架构的 Gallery。"));
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XamlNexus", "Gallery");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("XamlNexus/" + GetVersion());
            string exe = await GalleryDistribution.EnsureInstalledAsync(cache, manifest, asset,
                (uri, ct) => http.GetStreamAsync(uri, ct),
                step => Console.WriteLine(step == "download"
                    ? GalleryText($"Downloading Gallery {manifest.Version} ({rid})…", $"正在下载 Gallery {manifest.Version}（{rid}）…")
                    : GalleryText("Verified download; extracting…", "下载校验通过，正在解压…")), cancellation.Token);
            var running = Process.GetProcessesByName("XamlNexus.Gallery");
            bool alreadyRunning = running.Length > 0;
            foreach (var process in running) process.Dispose();
            if (alreadyRunning) {
                Console.WriteLine(GalleryText("Gallery is already running. Close it and run xamlnexus gallery again to open this version.",
                    "Gallery 已在运行。请关闭现有窗口，再执行 xamlnexus gallery 打开当前版本。"));
                return GenerationFailureExitCode;
            }
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
            Console.WriteLine(GalleryText($"Started Gallery {manifest.Version}.", $"已启动 Gallery {manifest.Version}。"));
            return SuccessExitCode;
        }
        catch (OperationCanceledException) {
            Console.Error.WriteLine(GalleryText("Gallery download cancelled or timed out; retry xamlnexus gallery.", "Gallery 下载已取消或超时，请重试 xamlnexus gallery。"));
            return GenerationFailureExitCode;
        }
        catch (Exception error) {
            Console.Error.WriteLine(GalleryText("Could not open Gallery: ", "无法打开 Gallery：") + error.Message);
            return GenerationFailureExitCode;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
