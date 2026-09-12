using System.Text;
using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ThreeWaySolutionMergeTests {
    private const string AppGuid = "{11111111-1111-1111-1111-111111111111}";
    private const string LocalGuid = "{22222222-2222-2222-2222-222222222222}";
    private const string TargetGuid = "{33333333-3333-3333-3333-333333333333}";
    private const string ProjectType = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

    [Fact]
    public void Merge_PreservesIndependentProjectsAndConfigurationMappings() {
        string baseline = Solution([Project("App", "App\\App.csproj", AppGuid)], [Mapping(AppGuid)]);
        string local = Solution(
            [Project("App", "App\\App.csproj", AppGuid), Project("Local", "Local\\Local.csproj", LocalGuid)],
            [Mapping(AppGuid), Mapping(LocalGuid)]);
        string target = Solution(
            [Project("App", "App\\App.csproj", AppGuid), Project("Target", "Target\\Target.csproj", TargetGuid)],
            [Mapping(AppGuid), Mapping(TargetGuid)]);

        XamlNexusSolutionMergeResult result = XamlNexusThreeWaySolutionMerge.Merge(
            Bytes(baseline),
            Bytes(local),
            Bytes(target));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        string merged = Encoding.UTF8.GetString(result.Content!);
        Assert.Contains("Local\\Local.csproj", merged);
        Assert.Contains("Target\\Target.csproj", merged);
        Assert.Contains(Mapping(LocalGuid), merged);
        Assert.Contains(Mapping(TargetGuid), merged);
    }

    [Fact]
    public void Merge_PreservesWindowsLineEndings() {
        string baseline = Solution([Project("App", "App\\App.csproj", AppGuid)], [Mapping(AppGuid)]);
        string local = baseline.Replace("\n", "\r\n", StringComparison.Ordinal)
            .Replace("EndProject\r\nGlobal", $"EndProject\r\n{Project("Local", "Local\\Local.csproj", LocalGuid)}\r\nGlobal", StringComparison.Ordinal);
        string target = Solution(
            [Project("App", "App\\App.csproj", AppGuid), Project("Target", "Target\\Target.csproj", TargetGuid)],
            [Mapping(AppGuid)]).Replace("\n", "\r\n", StringComparison.Ordinal);

        XamlNexusSolutionMergeResult result = XamlNexusThreeWaySolutionMerge.Merge(
            Bytes(baseline.Replace("\n", "\r\n", StringComparison.Ordinal)),
            Bytes(local),
            Bytes(target));

        Assert.Equal(XamlNexusMergeStatus.Merged, result.Status);
        string merged = Encoding.UTF8.GetString(result.Content!);
        Assert.Contains("\r\n", merged);
        Assert.DoesNotContain("\n", merged.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_RejectsConflictingConfigurationValue() {
        string baseline = Solution([Project("App", "App\\App.csproj", AppGuid)], [Mapping(AppGuid, "Debug|Any CPU")]);
        string local = Solution([Project("App", "App\\App.csproj", AppGuid)], [Mapping(AppGuid, "Debug|x64")]);
        string target = Solution([Project("App", "App\\App.csproj", AppGuid)], [Mapping(AppGuid, "Release|Any CPU")]);

        XamlNexusSolutionMergeResult result = XamlNexusThreeWaySolutionMerge.Merge(
            Bytes(baseline), Bytes(local), Bytes(target));

        Assert.Equal(XamlNexusMergeStatus.Conflict, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Merge_RejectsProjectRemovedLocallyButChangedByTarget() {
        string baselineProject = Project("App", "App\\App.csproj", AppGuid);
        string baseline = Solution([baselineProject], [Mapping(AppGuid)]);
        string local = Solution([], [Mapping(AppGuid)]);
        string target = Solution(
            [baselineProject.Replace("\"App\"", "\"Renamed\"", StringComparison.Ordinal)],
            [Mapping(AppGuid)]);

        XamlNexusSolutionMergeResult result = XamlNexusThreeWaySolutionMerge.Merge(
            Bytes(baseline), Bytes(local), Bytes(target));

        Assert.Equal(XamlNexusMergeStatus.Conflict, result.Status);
    }

    [Fact]
    public void Merge_RejectsUnknownGlobalContent() {
        string baseline = Solution([Project("App", "App\\App.csproj", AppGuid)], [Mapping(AppGuid)]);
        string local = baseline.Replace("Global\n", "Global\n\tUnexpected\n", StringComparison.Ordinal);
        string target = baseline.Replace("# Visual Studio Version 17", "# Visual Studio Version 18", StringComparison.Ordinal);

        XamlNexusSolutionMergeResult result = XamlNexusThreeWaySolutionMerge.Merge(
            Bytes(baseline), Bytes(local), Bytes(target));

        Assert.Equal(XamlNexusMergeStatus.Unsupported, result.Status);
    }

    private static string Solution(IReadOnlyList<string> projects, IReadOnlyList<string> mappings) =>
        "Microsoft Visual Studio Solution File, Format Version 12.00\n" +
        "# Visual Studio Version 17\n" +
        string.Join("\n", projects) + (projects.Count > 0 ? "\n" : string.Empty) +
        "Global\n" +
        "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\n" +
        string.Join("\n", mappings) + "\n" +
        "\tEndGlobalSection\n" +
        "EndGlobal\n";

    private static string Project(string name, string path, string guid) =>
        $"Project(\"{ProjectType}\") = \"{name}\", \"{path}\", \"{guid}\"\nEndProject";

    private static string Mapping(string guid, string value = "Debug|Any CPU") =>
        $"\t\t{guid}.Debug|Any CPU.ActiveCfg = {value}";

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
