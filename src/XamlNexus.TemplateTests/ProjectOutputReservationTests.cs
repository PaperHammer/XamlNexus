using System.Reflection;
using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ProjectOutputReservationTests {
    private static string Reserve(string parent, string name) => (string)typeof(ProjectComposer).Assembly
        .GetType("XamlNexus.Common.Projects.ProjectOutputReservation")!
        .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [parent, name])!;

    [Fact]
    public async Task ConcurrentCreatorsOwnDifferentDirectoriesAndCleanupIsIsolated() {
        string parent = Directory.CreateTempSubdirectory("xamlnexus-reservation-tests-").FullName;
        try {
            using var start = new ManualResetEventSlim();
            var tasks = Enumerable.Range(0, 8).Select(index => Task.Run(() => {
                start.Wait();
                string path = Reserve(parent, "App");
                File.WriteAllText(Path.Combine(path, "owner.txt"), index.ToString());
                return (Path: path, Owner: index.ToString());
            })).ToArray();
            start.Set();
            var results = await Task.WhenAll(tasks);
            Assert.Equal(results.Length, results.Select(result => result.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains(results, result => result.Path == Path.Combine(parent, "App"));
            Directory.Delete(results[0].Path, recursive: true);
            foreach (var result in results.Skip(1))
                Assert.Equal(result.Owner, File.ReadAllText(Path.Combine(result.Path, "owner.txt")));
            Assert.Empty(Directory.GetDirectories(parent, ".xamlnexus-reserve-*"));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public void ExistingFileIsPreservedAndAlternateDirectoryIsReserved() {
        string parent = Directory.CreateTempSubdirectory("xamlnexus-reservation-tests-").FullName;
        try {
            string existing = Path.Combine(parent, "App");
            File.WriteAllText(existing, "user content");
            string reserved = Reserve(parent, "App");
            Assert.NotEqual(existing, reserved);
            Assert.True(Directory.Exists(reserved));
            Assert.Equal("user content", File.ReadAllText(existing));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }
}
