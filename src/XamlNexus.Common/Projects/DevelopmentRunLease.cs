using System.Security.Cryptography;
using System.Text;

namespace XamlNexus.Common.Projects;

internal static class DevelopmentRunLease {
    public static IDisposable Acquire(string projectRoot) {
        string canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        if (OperatingSystem.IsWindows()) canonical = canonical.ToUpperInvariant();
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        string directory = Path.Combine(Path.GetTempPath(), "xamlnexus-run-locks");
        Directory.CreateDirectory(directory);
        try {
            // The OS releases the lease even when the CLI is terminated unexpectedly.
            return new FileStream(Path.Combine(directory, key + ".lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33) {
            throw new InvalidOperationException("This project already has an active 'xamlnexus run'. Stop that run before starting another.", exception);
        }
    }
}
