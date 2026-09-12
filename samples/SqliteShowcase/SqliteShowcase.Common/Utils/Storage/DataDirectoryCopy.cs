namespace SqliteShowcase.Common.Utils.Storage;

/// <summary>Copies application files without deleting originals or overwriting destination files.</summary>
public static class DataDirectoryCopy {
    public static async Task CopyAsync(string sourceDirectory, string destinationDirectory) {
        string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        if (Within(source, destination) || Within(destination, source))
            throw new IOException("Choose a separate empty folder, outside the current data directory and its parents.");
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"Source directory does not exist: {source}");
        RejectLink(source);
        if (Directory.Exists(destination)) {
            RejectLink(destination);
            if (Directory.EnumerateFileSystemEntries(destination).Any())
                throw new IOException("The destination folder must be empty. Existing files will not be overwritten.");
        }

        // Check the full source tree before creating any destination files.
        var files = new List<string>();
        var directories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(source);
        while (pending.TryPop(out string? directory)) {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory)) {
                RejectLink(entry);
                if (Directory.Exists(entry)) {
                    directories.Add(entry);
                    pending.Push(entry);
                }
                else files.Add(entry);
            }
        }
        Directory.CreateDirectory(destination);
        foreach (string directory in directories)
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (string file in files) {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            // Refuse active writers, and never overwrite a file created after the preflight.
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await input.CopyToAsync(output);
        }
        // A failed copy may leave partial copies. Both original and destination data are preserved.
    }

    private static bool Within(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static void RejectLink(string path) {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Linked files and directories cannot be copied by this operation: {path}");
    }
}
