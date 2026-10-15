using System.Text;

namespace XamlNexus.Common.Projects;

/// <summary>Writes bundled CLI skills and application rules into a newly generated project.</summary>
internal static class XamlNexusAiGuidance {
    private static readonly string[] SkillDirectories = [".agents/skills", ".claude/skills", ".qwen/skills"];

    internal static void WriteGenerated(string root, XamlNexusProjectIdentity identity) {
        ProjectPathSafety.EnsureNoLinks(root);
        var files = BuildFiles(identity);
        foreach (var file in files) {
            string destination = Path.Combine(root, file.RelativePath);
            ProjectPathSafety.EnsureNoLinks(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            ProjectPathSafety.EnsureNoLinks(destination);
            // The generator owns an isolated output directory and rolls it back on failure.
            using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(new UTF8Encoding(false).GetBytes(file.Content));
        }
    }

    private static IReadOnlyList<GuidanceFile> BuildFiles(XamlNexusProjectIdentity identity) {
        var files = new List<GuidanceFile>();
        foreach (string directory in SkillDirectories) {
            files.Add(new(directory + "/xamlnexus-cli/SKILL.md", Read("cli/SKILL.md")));
            files.Add(new(directory + "/xamlnexus-cli/references/maintenance.md", Read("cli/references/maintenance.md")));
            files.Add(new(directory + "/xamlnexus-project/SKILL.md", Read("project/SKILL.md")));
            files.Add(new(directory + "/xamlnexus-project/references/architecture.md",
                Render(Read("project/references/" + identity.Preset + ".md"), identity)));
            files.Add(new(directory + "/xamlnexus-project/references/pages.md",
                Render(Read("project/references/pages.md"), identity)));
        }
        files.Add(new("AGENTS.md", Render(Read("AGENTS.md"), identity)));
        files.Add(new("CLAUDE.md", "@AGENTS.md\n"));
        files.Add(new("QWEN.md", "Read [AGENTS.md](AGENTS.md) for this application's architecture and development rules.\n"));
        return files;
    }

    private static string Render(string text, XamlNexusProjectIdentity identity) {
        string startup = identity.Preset == "hybrid" ? identity.Name : identity.Name + ".UI";
        return text.Replace("{{APP_NAME}}", identity.Name)
            .Replace("{{PRESET}}", identity.Preset)
            .Replace("{{PROFILE}}", identity.Profile ?? "standard")
            .Replace("{{LANGUAGE}}", identity.Language)
            .Replace("{{SOLUTION_FORMAT}}", identity.SolutionFormat)
            .Replace("{{STARTUP_PROJECT}}", startup + "/" + startup + ".csproj");
    }

    private static string Read(string relativePath) {
        string name = "XamlNexus.Common.Assets.Ai." + relativePath.Replace('/', '.');
        using Stream stream = typeof(XamlNexusAiGuidance).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing built-in AI guidance: " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed record GuidanceFile(string RelativePath, string Content);
}
