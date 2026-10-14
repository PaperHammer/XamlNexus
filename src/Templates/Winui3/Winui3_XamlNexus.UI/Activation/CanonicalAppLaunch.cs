using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Winui3_XamlNexus.UI.Activation;

// SDK 1.8 hashes the spelling of the image path. Normalize before calling AppInstance.
internal static class CanonicalAppLaunch {
    public static bool TryRelaunch(string[] arguments, out int exitCode) {
        exitCode = 0;
        uint length = 0;
        int packageStatus = GetCurrentPackageFullName(ref length, null);
        if (packageStatus == 0 || packageStatus == 122 /* ERROR_INSUFFICIENT_BUFFER */)
            return false; // Packaged activation data belongs to the process launched by Windows.
        if (packageStatus != 15700 /* APPMODEL_ERROR_NO_PACKAGE */) throw new Win32Exception(packageStatus);
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No executable path.");
        string canonical = GetCanonicalPath(executable);
        if (string.Equals(executable, canonical, StringComparison.Ordinal)) return false;
        var start = new ProcessStartInfo(canonical) {
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("Canonical launch failed.");
        AllowSetForegroundWindow((uint)child.Id);
        child.WaitForExit();
        exitCode = child.ExitCode;
        return true;
    }

    internal static string GetCanonicalPath(string path) {
        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var buffer = new StringBuilder(512);
        uint size = GetFinalPathNameByHandle(file, buffer, (uint)buffer.Capacity, 0);
        if (size == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (size >= buffer.Capacity) {
            buffer = new StringBuilder(checked((int)size + 1));
            if (GetFinalPathNameByHandle(file, buffer, (uint)buffer.Capacity, 0) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        string result = buffer.ToString();
        if (result.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) return @"\\" + result[8..];
        return result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..] : result;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
