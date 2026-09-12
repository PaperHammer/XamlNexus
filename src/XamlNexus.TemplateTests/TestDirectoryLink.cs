using System.Diagnostics;

namespace XamlNexus.TemplateTests;

internal static class TestDirectoryLink {
    public static void Create(string path, string target) {
        if (!OperatingSystem.IsWindows()) {
            Directory.CreateSymbolicLink(path, target);
            return;
        }
        // Junctions exercise Windows path traversal without requiring symlink privileges.
        var start = new ProcessStartInfo("powershell.exe") {
            UseShellExecute = false, CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("New-Item -ItemType Junction -Path $env:XAMLNEXUS_TEST_LINK -Target $env:XAMLNEXUS_TEST_TARGET -ErrorAction Stop | Out-Null");
        start.Environment["XAMLNEXUS_TEST_LINK"] = path;
        start.Environment["XAMLNEXUS_TEST_TARGET"] = target;
        using var process = Process.Start(start)!;
        if (!process.WaitForExit(30000)) {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Junction creation timed out.");
        }
        if (process.ExitCode != 0) throw new IOException("Junction creation failed.");
    }
}
