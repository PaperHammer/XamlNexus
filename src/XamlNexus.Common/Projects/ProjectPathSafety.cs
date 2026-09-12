namespace XamlNexus.Common.Projects;

internal static class ProjectPathSafety {
    // Inspect ancestors too: a project opened through a junction must not get a different write lock.
    public static void EnsureNoLinks(string fullPath) {
        for (string? path = Path.GetFullPath(fullPath); path is not null; path = Path.GetDirectoryName(path)) {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Linked project files or directories are not supported: {path}");
        }
    }
}
