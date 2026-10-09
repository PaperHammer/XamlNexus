using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

internal static class Program {
    [STAThread]
    private static int Main(string[] args) {
        AppInstance? instance = null;
        try {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            if (args.Length < 3) throw new ArgumentException("Expected role, key, and output directory.");
            string role = args[0], key = args[1], directory = args[2];
            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            instance = AppInstance.FindOrRegisterForKey(key);
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

            if (role == "owner") {
                if (!instance.IsCurrent) throw new InvalidOperationException("Probe key is already owned.");
                object logLock = new();
                instance.Activated += (_, request) => {
                    lock (logLock) File.AppendAllText(Path.Combine(directory, "activations.jsonl"),
                        JsonSerializer.Serialize(new {
                            pid = Environment.ProcessId, kind = request.Kind.ToString(),
                            arguments = (request.Data as ILaunchActivatedEventArgs)?.Arguments,
                        }) + Environment.NewLine);
                };
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
            using var redirected = new EventWaitHandle(false, EventResetMode.ManualReset);
            Task redirect = Task.Run(async () => {
                try { await instance.RedirectActivationToAsync(activation); }
                finally {
                    try { redirected.Set(); }
                    catch (ObjectDisposedException) { } // The bounded COM wait may have timed out.
                }
            });
            PumpWait(redirected, 15000);
            redirect.GetAwaiter().GetResult();
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
