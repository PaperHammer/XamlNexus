using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.AppLifecycle;

namespace Winui3_XamlNexus.UI.Activation;

internal static class ActivationRedirector {
    public static AppInstance FindOrRegister(string key) {
        // An owner can exit between the SDK's failed registration and owner lookup.
        for (int attempt = 0; attempt < 3; attempt++) {
            AppInstance? instance = AppInstance.FindOrRegisterForKey(key);
            if (instance is not null) return instance;
            Thread.Sleep(10);
        }
        throw new InvalidOperationException("No activation owner could be registered.");
    }

    public static void Redirect(AppInstance owner, AppActivationArguments arguments, uint timeout = 15000) {
        using SafeWaitHandle process = OpenProcess(0x00100000 /* SYNCHRONIZE */, false, owner.ProcessId);
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        using var completed = new EventWaitHandle(false, EventResetMode.ManualReset);
        Task redirect = Task.Run(async () => {
            try { await owner.RedirectActivationToAsync(arguments); }
            finally {
                try { completed.Set(); }
                catch (ObjectDisposedException) { } // Caller may have timed out or the owner exited.
            }
        });
        int result = CoWaitForMultipleObjects(0, timeout, 2,
            new[] { completed.SafeWaitHandle.DangerousGetHandle(), process.DangerousGetHandle() }, out uint index);
        if (result == unchecked((int)0x80010115)) throw new TimeoutException("Activation redirection timed out.");
        Marshal.ThrowExceptionForHR(result);
        if (index == 1) throw new InvalidOperationException("The activation owner exited during redirection.");
        redirect.GetAwaiter().GetResult();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWaitHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoWaitForMultipleObjects(uint flags, uint timeout, uint count, IntPtr[] handles, out uint index);
}
