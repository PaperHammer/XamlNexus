using System.Diagnostics;

namespace XamlNexus.Common.Utils {
    public static class ShellExecutor {
        public static bool Run(string fileName, string args, string workingDir) {
            var startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
    }
}
