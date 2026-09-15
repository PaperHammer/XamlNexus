namespace XamlNexus.Common.Projects;

internal static class ProjectOutputReservation {
    public static string Create(string parent, string name) {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || Path.GetFileName(name) != name)
            throw new ArgumentException("The project name must be a single directory name.", nameof(name));

        parent = Path.GetFullPath(parent);
        Directory.CreateDirectory(parent);

        string reservation = Path.Combine(parent, ".xamlnexus-reserve-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(reservation);
        
        string requested = Path.Combine(parent, name);
        string alternate = requested + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
        try {
            for (int attempt = 0; ; attempt++) {
                string destination = attempt == 0 ? requested : attempt == 1 ? alternate : alternate + "_" + (attempt - 1);
                try {
                    // Rename fails if another creator already owns the destination.
                    Directory.Move(reservation, destination);
                    return destination;
                }
                catch (IOException) when (Directory.Exists(destination) || File.Exists(destination)) { }
            }
        }
        finally { if (Directory.Exists(reservation)) Directory.Delete(reservation); }
    }
}
