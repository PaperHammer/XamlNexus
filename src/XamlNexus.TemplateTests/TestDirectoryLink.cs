using System.Diagnostics;

namespace XamlNexus.TemplateTests;

internal static class TestDirectoryLink {
    public static void Create(string path, string target) {
        if (!OperatingSystem.IsWindows()) {
            Directory.CreateSymbolicLink(path, target);
            return;
        }
        // 使用系统 mklink 创建 Junction，避免每次测试启动 Windows PowerShell。
        // /d 禁用 cmd AutoRun；路径通过环境变量传入，保留空格等字符。
        path = Path.GetFullPath(path);
        target = Path.GetFullPath(target);
        if (path.IndexOfAny(['"', '\r', '\n']) >= 0 || target.IndexOfAny(['"', '\r', '\n']) >= 0)
            throw new ArgumentException("Junction paths must not contain quotes or newlines.");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            Arguments = "/d /v:off /c mklink /J \"%XAMLNEXUS_TEST_LINK%\" \"%XAMLNEXUS_TEST_TARGET%\"",
        };
        start.Environment["XAMLNEXUS_TEST_LINK"] = path;
        start.Environment["XAMLNEXUS_TEST_TARGET"] = target;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"Junction creation timed out: '{path}' -> '{target}'.");
        }
        if (process.ExitCode != 0)
            throw new IOException($"Junction creation failed (exit {process.ExitCode}): {stdout.GetAwaiter().GetResult()} {stderr.GetAwaiter().GetResult()}");
        if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
            throw new IOException($"Junction was not created: '{path}'.");
    }
}
