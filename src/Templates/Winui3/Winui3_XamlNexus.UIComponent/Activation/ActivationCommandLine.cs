using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace Winui3_XamlNexus.UIComponent.Activation;

/// <summary>Uses Windows quoting rules for SDK launch argument strings, including redirected launches.</summary>
public static class ActivationCommandLine {
    public static string[] Split(string arguments) {
        if (string.IsNullOrWhiteSpace(arguments)) return Array.Empty<string>();
        // Prefix a dummy executable: packaged activations can contain arguments only.
        nint argv = CommandLineToArgvW("activation.exe " + arguments, out int count);
        if (argv == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            return Enumerable.Range(1, count - 1)
                .Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!)
                .ToArray();
        }
        finally { LocalFree(argv); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CommandLineToArgvW(string commandLine, out int argumentCount);
    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint memory);
}
