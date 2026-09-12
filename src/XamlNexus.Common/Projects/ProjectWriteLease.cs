using System.Security.Cryptography;
using System.Text;

namespace XamlNexus.Common.Projects;

internal static class ProjectWriteLease {
    public static IDisposable Acquire(string projectRoot) {
        string canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        ProjectPathSafety.EnsureNoLinks(canonical);
        if (OperatingSystem.IsWindows()) canonical = canonical.ToUpperInvariant();
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        string directory = Path.Combine(Path.GetTempPath(), "xamlnexus-write-locks");
        Directory.CreateDirectory(directory);
        try {
            return new FileStream(Path.Combine(directory, key + ".lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33) {
            throw new InvalidOperationException("Another operation is modifying this project. Retry after it finishes.", exception);
        }
    }
}
