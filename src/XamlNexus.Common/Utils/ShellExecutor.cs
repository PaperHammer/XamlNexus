using System.Diagnostics;

namespace XamlNexus.Common.Utils {
    public sealed record ShellExecutionResult(
        int ExitCode,
        string StandardOutput,
        string StandardError) {
        public bool Success => ExitCode == 0;

        public string DiagnosticOutput {
            get {
                string error = StandardError.Trim();
                if (!string.IsNullOrEmpty(error))
                    return error;

                string output = StandardOutput.Trim();
                return string.IsNullOrEmpty(output) ? "No process output was captured." : output;
            }
        }
    }

    public static class ShellExecutor {
        public static ShellExecutionResult Run(string fileName, string args, string workingDir) {
            var startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                return new ShellExecutionResult(-1, string.Empty, $"Failed to start process: {fileName}");

            // Drain both redirected streams while the process runs. Waiting first without
            // reading can deadlock when either OS pipe buffer becomes full.
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit();

            return new ShellExecutionResult(
                process.ExitCode,
                outputTask.GetAwaiter().GetResult(),
                errorTask.GetAwaiter().GetResult());
        }
    }
}
