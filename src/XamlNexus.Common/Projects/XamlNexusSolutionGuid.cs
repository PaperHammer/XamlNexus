using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XamlNexus.Common.Projects;

public static partial class XamlNexusSolutionGuid {
    public static Guid CreateDeterministic(string projectPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        string normalized = projectPath.Replace('/', '\\').ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static string NormalizeProjectGuids(string solutionText) {
        ArgumentNullException.ThrowIfNull(solutionText);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ProjectLinePattern().Matches(solutionText)) {
            string projectPath = match.Groups["path"].Value;
            string existingGuid = match.Groups["guid"].Value;
            string deterministicGuid = CreateDeterministic(projectPath)
                .ToString("B")
                .ToUpperInvariant();
            replacements[existingGuid] = deterministicGuid;
        }
        string normalized = solutionText;
        foreach ((string existingGuid, string deterministicGuid) in replacements) {
            if (existingGuid.Equals(deterministicGuid, StringComparison.OrdinalIgnoreCase)) continue;
            normalized = Regex.Replace(
                normalized,
                Regex.Escape(existingGuid),
                deterministicGuid,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return normalized;
    }

    [GeneratedRegex(
        "^Project\\(\"\\{[^\"]+\\}\"\\) = \"[^\"]+\", \"(?<path>[^\"]+\\.csproj)\", \"(?<guid>\\{[0-9A-Fa-f-]{36}\\})\"\\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ProjectLinePattern();
}
