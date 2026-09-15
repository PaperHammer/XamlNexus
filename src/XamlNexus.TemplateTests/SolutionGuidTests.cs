using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class SolutionGuidTests {
    [Fact]
    public void NormalizeProjectGuids_MakesEquivalentSolutionsDeterministic() {
        string first = Solution("{11111111-1111-1111-1111-111111111111}");
        string second = Solution("{22222222-2222-2222-2222-222222222222}");

        string normalizedFirst = XamlNexusSolutionGuid.NormalizeProjectGuids(first);
        string normalizedSecond = XamlNexusSolutionGuid.NormalizeProjectGuids(second);

        string expected = XamlNexusSolutionGuid.CreateDeterministic("App\\App.csproj")
            .ToString("B")
            .ToUpperInvariant();
        Assert.Equal(normalizedFirst, normalizedSecond);
        Assert.Contains(expected, normalizedFirst);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", normalizedFirst);
    }

    [Fact]
    public void NormalizeProjectGuids_SupportsWindowsLineEndings() {
        string projectGuid = "{11111111-1111-1111-1111-111111111111}";
        // 原始字符串的换行随源码检出格式变化，避免把 CRLF 再转换成 CRCRLF。
        string solution = Solution(projectGuid).ReplaceLineEndings("\r\n");

        string normalized = XamlNexusSolutionGuid.NormalizeProjectGuids(solution);

        string expected = XamlNexusSolutionGuid.CreateDeterministic("App\\App.csproj")
            .ToString("B")
            .ToUpperInvariant();
        Assert.Contains(expected, normalized);
        Assert.DoesNotContain(projectGuid, normalized);
    }

    private static string Solution(string projectGuid) => $$"""
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{{projectGuid}}"
        EndProject
        Global
        	GlobalSection(ProjectConfigurationPlatforms) = postSolution
        		{{projectGuid}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        	EndGlobalSection
        EndGlobal
        """;
}
