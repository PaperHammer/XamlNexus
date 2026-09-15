using System.Diagnostics;

namespace XamlNexus.Common.Projects;

public sealed record DevelopmentCommand(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);
public sealed record DevelopmentRunPlan(string Root, string ProjectPath, bool Hybrid, bool NoBuild, DevelopmentCommand Build, DevelopmentCommand ResolveTarget);
public sealed record DevelopmentCommandResult(int ExitCode, string Output);

public interface IDevelopmentProcess : IDisposable {
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
    Task StopAsync();
}

public interface IDevelopmentRuntime {
    Task<DevelopmentCommandResult> ExecuteAsync(DevelopmentCommand command, bool captureOutput, CancellationToken cancellationToken);
    IDevelopmentProcess Start(DevelopmentCommand command);
    bool FileExists(string path);
}

public static class DevelopmentRunner {
    public static DevelopmentRunPlan CreatePlan(XamlNexusProjectContext project, bool noBuild = false) {
        var report = XamlNexusProjectValidator.Validate(project);
        if (!report.IsValid) throw new InvalidOperationException("The project is not valid. Run 'xamlnexus validate' first.");

        string name = project.Manifest.Project.Name;
        bool hybrid = project.Manifest.Project.Preset == "hybrid";
        string projectPath = Path.Combine(project.RootDirectory, hybrid ? name : name + ".UI", hybrid ? name + ".csproj" : name + ".UI.csproj");

        if (!File.Exists(projectPath)) throw new FileNotFoundException("The startup project is missing.", projectPath);
        if (hybrid) {
            string app = Path.Combine(project.RootDirectory, name, "App.xaml.cs");
            if (!File.Exists(app) || !File.ReadAllText(app).Contains("--xamlnexus-run", StringComparison.Ordinal))
                throw new InvalidOperationException("This hybrid host has no development launch entry. Upgrade its scaffold or add the --xamlnexus-run startup integration before running it.");
        }

        string[] properties = ["-p:Configuration=Debug", "-p:Platform=x64", "-p:RuntimeIdentifier=win-x64", "-p:WindowsPackageType=None"];

        return new(project.RootDirectory, projectPath, hybrid, noBuild,
            new("dotnet", ["build", projectPath, "-m:1", "-v:minimal", .. properties], project.RootDirectory),
            new("dotnet", ["msbuild", projectPath, "-nologo", "-getProperty:TargetPath", .. properties], project.RootDirectory));
    }

    public static async Task<int> RunAsync(DevelopmentRunPlan plan, IDevelopmentRuntime runtime, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        using var lease = DevelopmentRunLease.Acquire(plan.Root);
        if (!plan.NoBuild) {
            var build = await runtime.ExecuteAsync(plan.Build, false, cancellationToken);
            if (build.ExitCode != 0) throw new InvalidOperationException($"Build failed (exit code {build.ExitCode}). See the build output above.");
        }

        var target = await runtime.ExecuteAsync(plan.ResolveTarget, true, cancellationToken);
        if (target.ExitCode != 0) throw new InvalidOperationException($"Could not resolve the startup executable: {target.Output.Trim()}");

        string targetPath = target.Output.Trim();
        if (string.IsNullOrWhiteSpace(targetPath) || targetPath.Contains('\n') || targetPath.Contains('\r'))
            throw new InvalidOperationException("MSBuild did not return a single TargetPath.");

        string executable = Path.ChangeExtension(Path.GetFullPath(targetPath, plan.Root), ".exe");
        if (!runtime.FileExists(executable))
            throw new FileNotFoundException("The startup executable is missing. Run again without --no-build.", executable);
        if (plan.Hybrid) {
            string ui = Path.Combine(Path.GetDirectoryName(executable)!, "Plugins", "UI", Path.GetFileNameWithoutExtension(plan.ProjectPath) + ".UI.exe");
            if (!runtime.FileExists(ui)) throw new FileNotFoundException("The host's UI plugin is missing. Rebuild the hybrid project.", ui);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var process = runtime.Start(new(executable, plan.Hybrid ? ["--xamlnexus-run"] : [], Path.GetDirectoryName(executable)!));

        try { return await process.WaitForExitAsync(cancellationToken); }
        finally { await process.StopAsync(); }
    }
}

public sealed class DevelopmentRuntime(Action<string> log) : IDevelopmentRuntime {
    public bool FileExists(string path) => File.Exists(path);

    public async Task<DevelopmentCommandResult> ExecuteAsync(DevelopmentCommand command, bool captureOutput, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = CreateStartInfo(command, redirect: true) };
        if (!process.Start()) throw new IOException($"Could not start {command.FileName}.");

        using var owned = new OwnedProcess(process);
        using var reading = new CancellationTokenSource();
        var output = new System.Text.StringBuilder();

        async Task Drain(StreamReader reader, bool capture) {
            while (await reader.ReadLineAsync(reading.Token) is { } line) {
                if (capture) output.AppendLine(line);
                else log(line);
            }
        }

        var stdout = Drain(process.StandardOutput, captureOutput);
        var stderr = Drain(process.StandardError, false);

        try {
            int exitCode = await owned.WaitForExitAsync(cancellationToken);
            await FinishOutputAsync(Task.WhenAll(stdout, stderr), reading, cancellationToken);

            return new(exitCode, output.ToString());
        }
        finally {
            try { await owned.StopAsync(); }
            finally {
                reading.Cancel();
                try { await Task.WhenAll(stdout, stderr); }
                catch (OperationCanceledException) when (reading.IsCancellationRequested) { }
            }
        }
    }

    public IDevelopmentProcess Start(DevelopmentCommand command) {
        var startInfo = CreateStartInfo(command, redirect: true);
        // Generated applications emit UTF-8. The parent console encoding alone does not
        // define the encoding of redirected child-process streams.
        startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        var process = new Process { StartInfo = startInfo };
        var reading = new CancellationTokenSource();

        try {
            if (!process.Start()) throw new IOException($"Could not start {command.FileName}.");
            async Task Drain(StreamReader reader) {
                while (await reader.ReadLineAsync(reading.Token) is { } line) log(line);
            }
            return new OwnedProcess(process, Task.WhenAll(Drain(process.StandardOutput), Drain(process.StandardError)), reading);
        }
        catch { reading.Dispose(); process.Dispose(); throw; }
    }

    private static async Task FinishOutputAsync(Task output, CancellationTokenSource reading, CancellationToken cancellationToken) {
        try { await output.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken); }
        catch (TimeoutException) {
            // Detached children can retain inherited pipe handles after their parent exits.
            reading.Cancel();
            try { await output; }
            catch (OperationCanceledException) when (reading.IsCancellationRequested) { }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static ProcessStartInfo CreateStartInfo(DevelopmentCommand command, bool redirect) {
        var info = new ProcessStartInfo(command.FileName) {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
        };
        foreach (string argument in command.Arguments) info.ArgumentList.Add(argument);

        return info;
    }

    private sealed class OwnedProcess(Process process, Task? output = null, CancellationTokenSource? reading = null) : IDevelopmentProcess {
        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken) {
            await process.WaitForExitAsync(cancellationToken);
            if (output is not null) {
                try { await FinishOutputAsync(output, reading!, cancellationToken); }
                catch (OperationCanceledException) when (reading?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested) { }
            }
            return process.ExitCode;
        }

        public async Task StopAsync() {
            try {
                if (process.HasExited) return;
                if (process.CloseMainWindow()) {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { await process.WaitForExitAsync(timeout.Token); }
                    catch (OperationCanceledException) { }
                }
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException) when (process.HasExited) { }
            finally {
                reading?.Cancel();
                if (output is not null) {
                    try { await output; }
                    catch (OperationCanceledException) when (reading?.IsCancellationRequested == true) { }
                }
            }
        }
        public void Dispose() { reading?.Dispose(); process.Dispose(); }
    }
}
