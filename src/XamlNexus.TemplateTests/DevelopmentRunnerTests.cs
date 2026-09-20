using XamlNexus.Tooling.Development;
using XamlNexus.Tooling.CommandLine;
using System.Diagnostics;
using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class DevelopmentRunnerTests : IDisposable {
    private readonly string root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "run tests", Guid.NewGuid().ToString("N"))).FullName;
    private XamlNexusProjectContext CreateProject(bool hybrid, bool integration = true) {
        foreach (string relative in new[] { "Demo.sln", "Directory.Build.props", "Demo.UI/Demo.UI.csproj", "Demo.Common/Demo.Common.csproj", "Demo/Demo.csproj" }) {
            string full = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "<Project />");
        }
        File.WriteAllText(Path.Combine(root, "Demo/App.xaml.cs"), integration ? "// --xamlnexus-run" : "// legacy host");
        XamlNexusProjectManifestStore.Save(Path.Combine(root, "xamlnexus.json"), new() {
            GeneratorVersion = "1.0.3", Modules = [],
            Project = new() { Name = "Demo", Preset = hybrid ? "hybrid" : "winui", Language = "en-US", SolutionFormat = "sln" },
        });
        return XamlNexusProjectLocator.Locate(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlanSelectsStartupProjectAndDoesNotCreateBuildOutput(bool hybrid) {
        var plan = DevelopmentRunner.CreatePlan(CreateProject(hybrid));
        Assert.EndsWith(hybrid ? "Demo/Demo.csproj" : "Demo.UI/Demo.UI.csproj", plan.ProjectPath.Replace('\\', '/'));
        Assert.Contains("-p:WindowsPackageType=None", plan.Build.Arguments);
        Assert.Contains("-p:RuntimeIdentifier=win-x64", plan.Build.Arguments);
        Assert.Contains(plan.ProjectPath, plan.Build.Arguments);
        Assert.False(Directory.Exists(Path.Combine(root, "Demo.UI/bin")));
    }

    [Fact]
    public void LegacyHybridHostReportsMissingIntegration() => Assert.Throws<InvalidOperationException>(() =>
        DevelopmentRunner.CreatePlan(CreateProject(true, integration: false)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunBuildsThenResolvesThenStartsAndReturnsExitCode(bool hybrid) {
        var runtime = new FakeRuntime(root);
        var plan = DevelopmentRunner.CreatePlan(CreateProject(hybrid));
        Assert.Equal(7, await DevelopmentRunner.RunAsync(plan, runtime));
        Assert.Equal(new[] { "build", "msbuild", "start", "stop" }, runtime.Events);
        Assert.Equal(hybrid ? new[] { "--xamlnexus-run" } : [], runtime.Started!.Arguments);
        Assert.EndsWith("app.exe", runtime.Started.FileName);
        Assert.Equal(root, runtime.Started.WorkingDirectory);
    }

    [Fact]
    public async Task NoBuildStillResolvesTheConfiguredTarget() {
        var runtime = new FakeRuntime(root);
        await DevelopmentRunner.RunAsync(DevelopmentRunner.CreatePlan(CreateProject(false), true), runtime);
        Assert.Equal(new[] { "msbuild", "start", "stop" }, runtime.Events);
    }

    [Fact]
    public async Task BuildFailureDoesNotLaunchAnything() {
        var runtime = new FakeRuntime(root) { FailBuild = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentRunner.RunAsync(DevelopmentRunner.CreatePlan(CreateProject(false)), runtime));
        Assert.Equal(new[] { "build" }, runtime.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingExecutableOrPluginDoesNotLaunchAnything(bool plugin) {
        var runtime = new FakeRuntime(root) { MissingPlugin = plugin, MissingExecutable = !plugin };
        await Assert.ThrowsAsync<FileNotFoundException>(() => DevelopmentRunner.RunAsync(DevelopmentRunner.CreatePlan(CreateProject(plugin)), runtime));
        Assert.DoesNotContain("start", runtime.Events);
    }

    [Fact]
    public async Task CancellationStopsOwnedApplication() {
        using var cancellation = new CancellationTokenSource();
        var runtime = new FakeRuntime(root) { OnStart = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DevelopmentRunner.RunAsync(DevelopmentRunner.CreatePlan(CreateProject(false)), runtime, cancellation.Token));
        Assert.Equal("stop", runtime.Events.Last());
    }

    [Fact]
    public void ParserSupportsRunFlagsAndRejectsDuplicatesAndUnknownOptions() {
        var parsed = CliParser.Parse(["run", "--project", root, "--no-build", "--dry-run", "--json"], root);
        Assert.True(parsed.Success);
        Assert.Equal(CliCommand.Run, parsed.Options!.Command);
        Assert.True(parsed.Options.NoBuild && parsed.Options.DryRun && parsed.Options.JsonOutput);
        Assert.Equal(root, parsed.Options.ProjectPath);
        foreach (var args in new[] {
            new[] { "run", "--no-build", "--no-build" }, new[] { "run", "--dry-run", "--dry-run" },
            new[] { "run", "--project", "--no-build" }, new[] { "run", "--configuration", "Release" },
            new[] { "run", root, "--project", root },
        }) Assert.False(CliParser.Parse(args, root).Success);
    }

    [Fact]
    public async Task ConcurrentRunIsRejectedBeforeBuildAndLeaseIsReleasedAfterCancellation() {
        var plan = DevelopmentRunner.CreatePlan(CreateProject(false));
        using var cancellation = new CancellationTokenSource();
        var first = new FakeRuntime(root) { Wait = async token => { await Task.Delay(Timeout.Infinite, token); return 0; } };
        Task<int> running = DevelopmentRunner.RunAsync(plan, first, cancellation.Token);
        try {
            Assert.NotNull(first.Started);
            var second = new FakeRuntime(root);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentRunner.RunAsync(plan, second));
            Assert.Contains("already has an active", error.Message);
            Assert.Empty(second.Events);
        }
        finally {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
        Assert.Equal(7, await DevelopmentRunner.RunAsync(plan, new FakeRuntime(root)));
    }

    [Fact]
    public async Task BuildFailureReleasesLeaseForRetry() {
        var plan = DevelopmentRunner.CreatePlan(CreateProject(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentRunner.RunAsync(plan, new FakeRuntime(root) { FailBuild = true }));
        Assert.Equal(7, await DevelopmentRunner.RunAsync(plan, new FakeRuntime(root)));
    }

    [Fact]
    public async Task DifferentProjectsCanRunConcurrently() {
        var plan = DevelopmentRunner.CreatePlan(CreateProject(false));
        using var cancellation = new CancellationTokenSource();
        var first = new FakeRuntime(root) { Wait = async token => { await Task.Delay(Timeout.Infinite, token); return 0; } };
        Task<int> running = DevelopmentRunner.RunAsync(plan, first, cancellation.Token);
        try {
            Assert.Equal(7, await DevelopmentRunner.RunAsync(plan with { Root = Path.Combine(root, "another") }, new FakeRuntime(root)));
        }
        finally {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task InheritedOutputDoesNotBlockExitOrCancellation(bool cancel, bool useStart) {
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe");
        string pidFile = Path.Combine(root, "processes.txt");
        string script = $$"""
            $ErrorActionPreference = 'Stop'
            $info = [System.Diagnostics.ProcessStartInfo]::new()
            $info.FileName = '{{powershell.Replace("'", "''")}}'
            $info.Arguments = '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 30"'
            $info.UseShellExecute = $false
            $info.CreateNoWindow = $true
            $child = [System.Diagnostics.Process]::Start($info)
            [System.IO.File]::WriteAllText('{{pidFile.Replace("'", "''")}}.tmp', ([string]$PID + ',' + [string]$child.Id))
            [System.IO.File]::Move('{{pidFile.Replace("'", "''")}}.tmp', '{{pidFile.Replace("'", "''")}}')
            "parent finished"
            """;
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var runtime = new DevelopmentRuntime(logs.Enqueue);
        using var cancellation = new CancellationTokenSource();
        var command = new DevelopmentCommand(powershell,
            ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))], root);
        async Task<DevelopmentCommandResult> RunStarted() {
            using var process = runtime.Start(command);
            try { return new(await process.WaitForExitAsync(cancellation.Token), string.Join(Environment.NewLine, logs)); }
            finally { await process.StopAsync(); }
        }
        Task<DevelopmentCommandResult> running = useStart ? RunStarted() : runtime.ExecuteAsync(command, true, cancellation.Token);
        Process? child = null;
        try {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!File.Exists(pidFile)) {
                if (running.IsCompleted) {
                    var result = await running;
                    throw new InvalidOperationException($"Helper exited {result.ExitCode}: {result.Output} {string.Join(Environment.NewLine, logs)}");
                }
                await Task.Delay(50, deadline.Token);
            }
            int[] ids = File.ReadAllText(pidFile).Split(',').Select(int.Parse).ToArray();
            child = Process.GetProcessById(ids[1]);
            Process? parent = null;
            try { parent = Process.GetProcessById(ids[0]); }
            catch (ArgumentException) { /* Parent already exited. */ }
            if (parent is not null) {
                using (parent) await parent.WaitForExitAsync(deadline.Token);
            }
            if (cancel) {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            else {
                var result = await running.WaitAsync(TimeSpan.FromSeconds(8));
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("parent finished", result.Output);
            }
            Assert.False(child.HasExited);
        }
        finally {
            cancellation.Cancel();
            if (child is not null) {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                child.Dispose();
            }
            try { await running.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task RealProcessCanBeCancelledAndStopped() {
        var runtime = new DevelopmentRuntime(_ => { });
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe");
        using var process = runtime.Start(new(powershell, ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 60"], root));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.WaitForExitAsync(cancellation.Token));
        }
        finally { await process.StopAsync(); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(deadline.Token);
    }

    [Fact]
    public async Task RealProcessReturnsExitCodeAndDrainsOutput() {
        var lines = new List<string>();
        var runtime = new DevelopmentRuntime(line => { lock (lines) lines.Add(line); });
        using var process = runtime.Start(new("cmd.exe", ["/d", "/c", "echo run-test & exit /b 7"], root));
        Assert.Equal(7, await process.WaitForExitAsync(CancellationToken.None));
        Assert.Contains("run-test ", lines);
        await process.StopAsync();
    }

    [Fact]
    public async Task ApplicationOutputPreservesUtf8OnBothStreams() {
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe");
        const string script = "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); [Console]::WriteLine('OS: Microsoft Windows 11 企业版'); [Console]::Error.WriteLine('错误：中文输出'); exit 7";
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var runtime = new DevelopmentRuntime(lines.Enqueue);
        using var process = runtime.Start(new(powershell,
            ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))], root));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try {
            Assert.Equal(7, await process.WaitForExitAsync(timeout.Token));
            Assert.Contains("OS: Microsoft Windows 11 企业版", lines);
            Assert.Contains("错误：中文输出", lines);
            Assert.Equal(2, lines.Count);
        }
        finally { await process.StopAsync(); }
    }

    private sealed class FakeRuntime(string root) : IDevelopmentRuntime {
        public List<string> Events { get; } = [];
        public bool FailBuild { get; init; }
        public bool MissingExecutable { get; init; }
        public bool MissingPlugin { get; init; }
        public Action? OnStart { get; init; }
        public Func<CancellationToken, Task<int>>? Wait { get; init; }
        public DevelopmentCommand? Started { get; private set; }
        public bool FileExists(string path) => path.Contains("Plugins") ? !MissingPlugin : !MissingExecutable;
        public Task<DevelopmentCommandResult> ExecuteAsync(DevelopmentCommand command, bool captureOutput, CancellationToken cancellationToken) {
            Events.Add(command.Arguments[0]);
            return Task.FromResult(new DevelopmentCommandResult(FailBuild && command.Arguments[0] == "build" ? 1 : 0, captureOutput ? Path.Combine(root, "app.dll") : ""));
        }
        public IDevelopmentProcess Start(DevelopmentCommand command) {
            Events.Add("start"); Started = command; OnStart?.Invoke();
            return new FakeProcess(Events, Wait);
        }
    }
    private sealed class FakeProcess(List<string> events, Func<CancellationToken, Task<int>>? wait) : IDevelopmentProcess {
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return wait?.Invoke(cancellationToken) ?? Task.FromResult(7); }
        public Task StopAsync() { events.Add("stop"); return Task.CompletedTask; }
        public void Dispose() { }
    }
    public void Dispose() {
        // Windows can briefly retain the child's working-directory handle after exit.
        // Retry sharing violations only; persistent locks and other errors still fail.
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (Directory.Exists(root)) {
            try { Directory.Delete(root, recursive: true); return; }
            catch (IOException exception) when ((exception.HResult & 0xffff) == 32
                && elapsed.Elapsed < TimeSpan.FromSeconds(5)) {
                Thread.Sleep(50);
            }
        }
    }
}
