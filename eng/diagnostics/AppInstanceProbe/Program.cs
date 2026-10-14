using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Winui3_XamlNexus.UI.Activation;
using Winui3_XamlNexus.UIComponent.Activation;
using Winui3_XamlNexus.UIComponent.Navigation;
using Winui3_XamlNexus.UIComponent.Utils;

internal static class Program {
    [STAThread]
    private static int Main(string[] args) {
        AppInstance? instance = null;
        try {
            if (!args.Contains("--raw-sdk") && CanonicalAppLaunch.TryRelaunch(args, out int exitCode)) return exitCode;
            WinRT.ComWrappersSupport.InitializeComWrappers();
            if (args.Length < 3) throw new ArgumentException("Expected role, key, and output directory.");
            string role = args[0], key = args[1], directory = args[2];
            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            var pending = new ActivationQueue<AppActivationArguments>(exception => Console.Error.WriteLine(exception));
            if (args.Contains("--capture-initial")) pending.Enqueue(activation);
            object logLock = new();
            AppInstance.GetCurrent().Activated += (_, request) => pending.Enqueue(request);
            instance = ActivationRedirector.FindOrRegister(key);
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            bool elevated = IsElevated(identity);
            var runtime = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Where(module => module.ModuleName.Contains("WindowsAppRuntime", StringComparison.OrdinalIgnoreCase))
                .OrderBy(module => module.ModuleName, StringComparer.OrdinalIgnoreCase)
                .Select(module => new { path = module.FileName, version = module.FileVersionInfo.FileVersion,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName))) }).ToArray();
            File.WriteAllText(Path.Combine(directory, role + ".json"), JsonSerializer.Serialize(new {
                pid = Environment.ProcessId, isCurrent = instance.IsCurrent, targetPid = instance.ProcessId,
                executable = Environment.ProcessPath, workingDirectory = Environment.CurrentDirectory,
                userSid = identity.User?.Value, elevated, runtime,
            }));

            if (instance.IsCurrent && (role == "owner" || role.StartsWith("candidate", StringComparison.Ordinal))) {
                async Task InitializeHandling() {
                    if (args.Contains("--delay-initialize")) await Task.Delay(750);
                    pending.Start(async (request, token) => {
                        var business = (request.Data as ILaunchActivatedEventArgs) is { } launch
                            ? BusinessActivationRequest.FromArguments(ActivationCommandLine.Split(launch.Arguments)) : null;
                        ActivationNavigationResult? businessResult = null;
                        string? businessId = null;
                        if (business is not null) {
                            var registry = new NavigationRegistry();
                            registry.Register(new("probe/orders", typeof(object), "Orders"));
                            businessResult = await new BusinessActivationNavigator(registry).NavigateAsync(business,
                                (_, payload, _) => {
                                    businessId = payload.GetBusinessString("id");
                                    return Task.FromResult(true);
                                }, token);
                        }
                        lock (logLock) File.AppendAllText(Path.Combine(directory, "activations.jsonl"),
                        JsonSerializer.Serialize(new {
                            pid = Environment.ProcessId, kind = request.Kind.ToString(),
                            arguments = (request.Data as ILaunchActivatedEventArgs)?.Arguments,
                            businessRoute = business?.Route, businessData = business?.Data, businessId, businessResult = businessResult?.ToString(),
                        }) + Environment.NewLine);
                    }, action => ThreadPool.QueueUserWorkItem(_ => action()));
                }
                _ = InitializeHandling();
                using var stopped = new EventWaitHandle(false, EventResetMode.ManualReset);
                using var cancellation = new CancellationTokenSource();
                Task watchStop = Task.Run(async () => {
                    try {
                        while (!File.Exists(Path.Combine(directory, "stop")))
                            await Task.Delay(50, cancellation.Token);
                        stopped.Set();
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                });
                File.WriteAllText(Path.Combine(directory, "ready"), "ready");
                try { PumpWait(stopped, 30000); }
                finally {
                    cancellation.Cancel();
                    watchStop.GetAwaiter().GetResult();
                }
                return 0;
            }

            // Distinguish identity separation from a redirection failure without starting a UI.
            if (instance.IsCurrent) return 23;
            if (args.Contains("--owner-exit")) {
                using Process owner = Process.GetProcessById((int)instance.ProcessId);
                File.WriteAllText(Path.Combine(directory, "stop"), "stop");
                owner.WaitForExit(5000);
                try { ActivationRedirector.Redirect(instance, activation, 1000); }
                catch (Exception) { return 0; } // Bounded failure after owner exit is expected.
                return 24;
            }
            ActivationRedirector.Redirect(instance, activation);
            return 0;
        }
        catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally {
            if (instance?.IsCurrent == true) instance.UnregisterKey();
        }
    }

    private static bool IsElevated(WindowsIdentity identity) {
        if (!GetTokenInformation(identity.Token, 20 /* TokenElevation */, out int elevation,
            sizeof(int), out _)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return elevation != 0;
    }

    private static void PumpWait(EventWaitHandle signal, uint timeout) {
        int result = CoWaitForMultipleObjects(0, timeout, 1,
            new[] { signal.SafeWaitHandle.DangerousGetHandle() }, out _);
        Marshal.ThrowExceptionForHR(result);
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoWaitForMultipleObjects(uint flags, uint timeout,
        uint count, IntPtr[] handles, out uint index);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int informationClass,
        out int information, int informationLength, out int returnLength);
}
