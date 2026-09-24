using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ProjectUpgradeTests {
    [Fact]
    public void Apply_RejectsDirectoryReplacedByJunctionAfterPlanning() {
        string parent = CreateTemporaryDirectory();
        string? link = null;
        try {
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["eng/test.txt"] = "old" });
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["eng/test.txt"] = "new" });
            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            byte[] manifest = File.ReadAllBytes(current.ManifestPath);
            string external = Path.Combine(parent, "external");
            link = Path.Combine(current.RootDirectory, "eng");
            Directory.Move(link, external);
            TestDirectoryLink.Create(link, external);
            Assert.Throws<IOException>(() => XamlNexusProjectUpgrade.Apply(current, target, plan));
            Assert.Equal("old", File.ReadAllText(Path.Combine(external, "test.txt")));
            Assert.Equal(manifest, File.ReadAllBytes(current.ManifestPath));
        }
        finally {
            if (link is not null && Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, "standard", true)]
    [InlineData("basic", "basic", true)]
    [InlineData("basic", "standard", false)]
    [InlineData("standard", "basic", false)]
    public void UpgradePreservesProfileAndRejectsProfileChanges(string? currentProfile, string targetProfile, bool allowed) {
        string parent = CreateTemporaryDirectory();
        try {
            var files = new Dictionary<string, string> { ["eng/test.txt"] = "same" };
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0", files, profile: currentProfile);
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0", files, profile: targetProfile);
            if (!allowed) {
                var error = Assert.Throws<XamlNexusProjectUpgradeException>(() => XamlNexusProjectUpgrade.CreatePlan(current, target));
                Assert.Equal("XU1003", error.Code);
                return;
            }
            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            XamlNexusProjectUpgrade.Apply(current, target, plan);
            Assert.Equal(currentProfile, XamlNexusProjectManifestStore.Load(current.ManifestPath).Project.Profile);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public void Apply_ReconcilesScaffoldFilesAndPreservesRecipeModules() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> {
                    ["eng/replace.txt"] = "old",
                    ["eng/delete.txt"] = "delete",
                },
                includeRecipe: true);
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> {
                    ["eng/replace.txt"] = "new",
                    ["eng/create.txt"] = "create",
                });
            File.WriteAllText(Path.Combine(current.RootDirectory, "user.txt"), "preserve");

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            XamlNexusProjectUpgradeResult result = XamlNexusProjectUpgrade.Apply(current, target, plan);

            Assert.True(plan.CanApply);
            Assert.Equal(3, result.ChangedFiles.Count);
            Assert.Equal("new", File.ReadAllText(Path.Combine(current.RootDirectory, "eng", "replace.txt")));
            Assert.Equal("create", File.ReadAllText(Path.Combine(current.RootDirectory, "eng", "create.txt")));
            Assert.False(File.Exists(Path.Combine(current.RootDirectory, "eng", "delete.txt")));
            Assert.Equal("preserve", File.ReadAllText(Path.Combine(current.RootDirectory, "user.txt")));
            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(current.ManifestPath);
            Assert.Equal("2.0.0", manifest.GeneratorVersion);
            Assert.Contains(manifest.Modules, module =>
                module.Id == "app-shell" && module.Version == "2.0.0");
            Assert.Contains(manifest.Modules, module =>
                module.Id == "sample-recipe" && module.Version == "4.0.0");
            Assert.Equal(2, manifest.ScaffoldFiles!.Count);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_ModifiedManagedFileReportsConflictWithoutWriting() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> {
                    ["eng/managed.txt"] = "old",
                    ["eng/unchanged.txt"] = "same",
                });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> {
                    ["eng/managed.txt"] = "new",
                    ["eng/unchanged.txt"] = "same",
                    ["eng/new.txt"] = "new file",
                });
            File.WriteAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt"), "user edit");

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);

            Assert.False(plan.CanApply);
            XamlNexusUpgradeConflict conflict = Assert.Single(plan.Conflicts);
            Assert.Equal("XU2011", conflict.Code);
            Assert.Contains("<<<<<<< LOCAL", conflict.MergeDocument);
            Assert.Contains(
                XamlNexusProjectValidator.Validate(current).Issues,
                issue => issue.Code == "XN1302" && issue.Severity == ProjectValidationSeverity.Warning && issue.RelativePath == "eng/managed.txt");
            Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                XamlNexusProjectUpgrade.Apply(current, target, plan));
            Assert.Equal("user edit", File.ReadAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt")));
            Assert.False(File.Exists(Path.Combine(current.RootDirectory, "eng", "new.txt")));
            Assert.Equal("1.0.0", XamlNexusProjectManifestStore.Load(current.ManifestPath).GeneratorVersion);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_UnparseableSolutionKeepsTextConflict() {
        string parent = CreateTemporaryDirectory();
        try {
            const string baseline = "header\nshared=old\nfooter\n";
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["UpgradeApp.sln"] = baseline });
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["UpgradeApp.sln"] = "header\nshared=target\nfooter\n" });
            File.WriteAllText(Path.Combine(current.RootDirectory, "UpgradeApp.sln"),
                "header\nshared=local\nfooter\n");

            var conflict = Assert.Single(XamlNexusProjectUpgrade.CreatePlan(current, target).Conflicts);
            Assert.Equal(ProjectUpgradeErrors.TextMergeConflict.GetMessage(), conflict.Message);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public void Apply_ThreeWayMergesNonOverlappingUserAndTemplateEdits() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> {
                    ["eng/managed.txt"] = "header\nsetting=old\nfooter\n",
                });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> {
                    ["eng/managed.txt"] = "header\nsetting=new\nfooter\n",
                });
            File.WriteAllText(
                Path.Combine(current.RootDirectory, "eng", "managed.txt"),
                "custom header\nsetting=old\nfooter\n");

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            XamlNexusUpgradeChange change = Assert.Single(plan.Changes);
            XamlNexusProjectUpgrade.Apply(current, target, plan);

            Assert.True(plan.CanApply);
            Assert.Equal(XamlNexusUpgradeChangeStrategy.TextMerge, change.Strategy);
            Assert.Equal(
                "custom header\nsetting=new\nfooter\n",
                File.ReadAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt")));
            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(current.ManifestPath);
            Assert.Equal("2.0.0", manifest.GeneratorVersion);
            Assert.NotNull(Assert.Single(manifest.ScaffoldFiles!).BaselineContentGzipBase64);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Apply_PreservesUserEditWhenTargetBaselineIsUnchanged() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "template\n" });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "template\n" });
            File.WriteAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt"), "user\n");

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            XamlNexusProjectUpgrade.Apply(current, target, plan);

            Assert.True(plan.CanApply);
            Assert.Empty(plan.Changes);
            Assert.Equal("user\n", File.ReadAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt")));
            Assert.Equal("2.0.0", XamlNexusProjectManifestStore.Load(current.ManifestPath).GeneratorVersion);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Apply_PreservesXmlBytesWhenNoMergeIsNeeded(bool targetMatchesLocal, bool withComment) {
        string parent = CreateTemporaryDirectory();
        try {
            string comment = withComment ? "  <!-- keep user comment -->\r\n" : "";
            string baseline = "<Grid>\r\n" + comment + "  <TextBlock Text=\"A\" />\r\n</Grid>\r\n";
            string local = baseline.Replace("Text=\"A\"", "Text=\"Custom\"");
            string updated = targetMatchesLocal ? local : baseline;
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["MainPage.xaml"] = baseline });
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["MainPage.xaml"] = updated });
            string path = Path.Combine(current.RootDirectory, "MainPage.xaml");
            File.WriteAllText(path, local);
            byte[] original = File.ReadAllBytes(path);

            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);

            Assert.True(plan.CanApply);
            Assert.Empty(plan.Changes);
            XamlNexusProjectUpgrade.Apply(current, target, plan);
            Assert.Equal(original, File.ReadAllBytes(path));
            var manifest = XamlNexusProjectManifestStore.Load(current.ManifestPath);
            Assert.Equal("2.0.0", manifest.GeneratorVersion);
            Assert.Equal(Encoding.UTF8.GetBytes(updated), XamlNexusBaselineContent.Decode(
                Assert.Single(manifest.ScaffoldFiles!).BaselineContentGzipBase64!));
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Apply_UsesXmlSemanticMergeForOverlappingProjectFileLine() {
        string parent = CreateTemporaryDirectory();
        try {
            const string baseline = "<Project><ItemGroup><PackageReference Include=\"Sample\" Version=\"1.0\" /></ItemGroup></Project>";
            const string local = "<Project><ItemGroup><PackageReference Include=\"Sample\" Version=\"1.0\" PrivateAssets=\"all\" /></ItemGroup></Project>";
            const string targetContent = "<Project><ItemGroup><PackageReference Include=\"Sample\" Version=\"2.0\" /></ItemGroup></Project>";
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> { ["App.csproj"] = baseline });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> { ["App.csproj"] = targetContent });
            File.WriteAllText(Path.Combine(current.RootDirectory, "App.csproj"), local);

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            XamlNexusUpgradeChange change = Assert.Single(plan.Changes);
            XamlNexusProjectUpgrade.Apply(current, target, plan);

            Assert.True(plan.CanApply);
            Assert.Equal(XamlNexusUpgradeChangeStrategy.XmlMerge, change.Strategy);
            XDocument merged = XDocument.Load(Path.Combine(current.RootDirectory, "App.csproj"));
            XElement package = Assert.Single(merged.Descendants("PackageReference"));
            Assert.Equal("2.0", (string?)package.Attribute("Version"));
            Assert.Equal("all", (string?)package.Attribute("PrivateAssets"));
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Upgrade_XamlOrderChangesArePreservedOrBlockWithoutWriting(bool conflictingOrder, bool named) {
        string parent = CreateTemporaryDirectory();
        try {
            string Document(string order, bool customized = false) => new XElement("StackPanel",
                order.Select(name => new XElement("TextBlock", named ? new XAttribute("Name", name) : null,
                    !named && customized && name == 'A' ? new XAttribute("FontSize", "24") : null,
                    new XAttribute("Text", named && customized && name == 'A' ? "Custom" : name.ToString()))))
                .ToString(SaveOptions.DisableFormatting);
            XamlNexusProjectContext current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["MainPage.xaml"] = Document("ABC") });
            XamlNexusProjectContext target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["MainPage.xaml"] = Document("BAC") });
            string path = Path.Combine(current.RootDirectory, "MainPage.xaml");
            string local = Document(conflictingOrder ? "ACB" : "ABC", customized: true);
            File.WriteAllText(path, local);
            byte[] manifest = File.ReadAllBytes(current.ManifestPath);

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);

            if (conflictingOrder || !named) {
                Assert.False(plan.CanApply);
                Assert.Throws<XamlNexusProjectUpgradeException>(() => XamlNexusProjectUpgrade.Apply(current, target, plan));
                Assert.Equal(local, File.ReadAllText(path));
                Assert.Equal(manifest, File.ReadAllBytes(current.ManifestPath));
            }
            else {
                Assert.True(plan.CanApply);
                Assert.Equal(XamlNexusUpgradeChangeStrategy.XmlMerge, Assert.Single(plan.Changes).Strategy);
                XamlNexusProjectUpgrade.Apply(current, target, plan);
                XElement root = XDocument.Load(path).Root!;
                Assert.Equal("BAC", string.Concat(root.Elements().Select(child => (string?)child.Attribute("Name"))));
                Assert.Equal("Custom", (string?)root.Elements().Single(child => (string?)child.Attribute("Name") == "A").Attribute("Text"));
            }
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData("duplicate", false)]
    [InlineData("unnamed", false)]
    [InlineData("comment", false)]
    [InlineData("named", true)]
    public void Upgrade_ChecksXmlStructureEvenWhenTextMergeSucceeds(string scenario, bool allowed) {
        string parent = CreateTemporaryDirectory();
        try {
            const string start = "<Grid xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n";
            string first = scenario == "unnamed" ? "  <TextBlock Text=\"A\" />\n" : "  <TextBlock x:Name=\"A\" />\n";
            string second = scenario == "unnamed" ? "  <TextBlock Text=\"B\" />\n" : "  <TextBlock x:Name=\"B\" />\n";
            string comment = scenario == "comment" ? "  <!-- user comment -->\n" : "";
            string baseline = start + comment + first + second + "</Grid>";
            string local = baseline.Replace(first, first.Replace(" />", " FontSize=\"24\" />"));
            string updated = baseline.Replace(second, second.Replace(" />", " FontSize=\"32\" />"));
            if (scenario == "duplicate") {
                const string added = "  <TextBlock x:Name=\"Status\" />\n";
                local = baseline.Replace(first, added + first);
                updated = baseline.Replace(second, second + added);
            }
            Assert.Equal(XamlNexusMergeStatus.Merged, XamlNexusThreeWayTextMerge.Merge(
                Encoding.UTF8.GetBytes(baseline), Encoding.UTF8.GetBytes(local), Encoding.UTF8.GetBytes(updated)).Status);
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["MainPage.xaml"] = baseline });
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["MainPage.xaml"] = updated });
            string path = Path.Combine(current.RootDirectory, "MainPage.xaml");
            File.WriteAllText(path, local);
            byte[] manifest = File.ReadAllBytes(current.ManifestPath);

            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            Assert.Equal(allowed, plan.CanApply);
            if (!allowed) {
                Assert.Equal(scenario == "comment" ? "XU2012" : "XU2011", Assert.Single(plan.Conflicts).Code);
                Assert.Throws<XamlNexusProjectUpgradeException>(() => XamlNexusProjectUpgrade.Apply(current, target, plan));
                Assert.Equal(local, File.ReadAllText(path));
                Assert.Equal(manifest, File.ReadAllBytes(current.ManifestPath));
            }
            else {
                Assert.Equal(XamlNexusUpgradeChangeStrategy.XmlMerge, Assert.Single(plan.Changes).Strategy);
                XamlNexusProjectUpgrade.Apply(current, target, plan);
                Assert.Equal(new[] { "24", "32" }, XDocument.Load(path).Root!.Elements()
                    .Select(element => (string?)element.Attribute("FontSize")));
            }
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Apply_UsesSolutionSemanticMergeForIndependentProjectAdditions() {
        string parent = CreateTemporaryDirectory();
        try {
            const string baseline = """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Global
                	GlobalSection(ProjectConfigurationPlatforms) = postSolution
                		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                	EndGlobalSection
                EndGlobal
                """;
            const string local = """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Local", "Local\Local.csproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Global
                	GlobalSection(ProjectConfigurationPlatforms) = postSolution
                		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                		{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                	EndGlobalSection
                EndGlobal
                """;
            const string targetContent = """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Target", "Target\Target.csproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
                Global
                	GlobalSection(ProjectConfigurationPlatforms) = postSolution
                		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                	EndGlobalSection
                EndGlobal
                """;
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> { ["UpgradeApp.sln"] = baseline });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> { ["UpgradeApp.sln"] = targetContent });
            File.WriteAllText(Path.Combine(current.RootDirectory, "UpgradeApp.sln"), local);

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            XamlNexusUpgradeChange change = Assert.Single(plan.Changes);
            XamlNexusProjectUpgrade.Apply(current, target, plan);

            Assert.True(plan.CanApply);
            Assert.Equal(XamlNexusUpgradeChangeStrategy.SolutionMerge, change.Strategy);
            string merged = File.ReadAllText(Path.Combine(current.RootDirectory, "UpgradeApp.sln"));
            Assert.Contains("Local\\Local.csproj", merged);
            Assert.Contains("Target\\Target.csproj", merged);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_LegacyHashOnlyBaselineKeepsConservativeConflict() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "old" },
                includeBaselineContent: false);
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "new" });
            File.WriteAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt"), "user edit");

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);

            XamlNexusUpgradeConflict conflict = Assert.Single(plan.Conflicts);
            Assert.Equal("XU2002", conflict.Code);
            Assert.Null(conflict.MergeDocument);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void WriteConflictArtifacts_ExportsReviewableDocumentWithoutChangingProject() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "base\n" });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "target\n" });
            string managedPath = Path.Combine(current.RootDirectory, "eng", "managed.txt");
            File.WriteAllText(managedPath, "local\n");
            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            string output = Path.Combine(parent, "conflicts");

            IReadOnlyList<string> artifacts = XamlNexusProjectUpgrade.WriteConflictArtifacts(plan, output);

            Assert.Equal(["eng/managed.txt.merge"], artifacts);
            string document = File.ReadAllText(Path.Combine(output, "eng", "managed.txt.merge"));
            Assert.Contains("<<<<<<< LOCAL", document);
            Assert.Contains("||||||| BASE", document);
            Assert.Contains(">>>>>>> TARGET", document);
            Assert.Equal("local\n", File.ReadAllText(managedPath));
            Assert.Equal("1.0.0", XamlNexusProjectManifestStore.Load(current.ManifestPath).GeneratorVersion);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveConflicts_RejectsMissingFileAndMalformedXml(bool bom) {
        string parent = CreateTemporaryDirectory();
        try {
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["Page.xaml"] = "<Grid Tag=\"base\" />" });
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["Page.xaml"] = "<Grid Tag=\"target\" />" });
            string local = Path.Combine(current.RootDirectory, "Page.xaml");
            File.WriteAllText(local, "<Grid Tag=\"local\" />");
            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            Assert.Equal("XU2011", Assert.Single(plan.Conflicts).Code);
            string output = Path.Combine(parent, "conflicts");
            XamlNexusProjectUpgrade.WriteConflictArtifacts(plan, output);
            string resolution = Path.Combine(output, "Page.xaml.merge");
            File.Delete(resolution);
            Assert.Equal("XU2021", Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                XamlNexusProjectUpgrade.ResolveConflicts(plan, output)).Code);
            File.WriteAllText(resolution, "<Grid>");
            Assert.Equal("XU2021", Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                XamlNexusProjectUpgrade.ResolveConflicts(plan, output)).Code);
            Assert.Equal("<Grid Tag=\"local\" />", File.ReadAllText(local));
            File.WriteAllText(resolution, "<Grid Tag=\"resolved\" />", new System.Text.UTF8Encoding(bom));
            XamlNexusProjectUpgrade.Apply(current, target, XamlNexusProjectUpgrade.ResolveConflicts(plan, output));
            Assert.Equal("<Grid Tag=\"resolved\" />", File.ReadAllText(local));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveConflicts_ValidatesExportThenAppliesTogether(bool stale) {
        string parent = CreateTemporaryDirectory();
        try {
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0",
                new Dictionary<string, string> { ["file.txt"] = "base\n", ["other.txt"] = "old\n" });
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0",
                new Dictionary<string, string> { ["file.txt"] = "target\n", ["other.txt"] = "new\n" });
            string localPath = Path.Combine(current.RootDirectory, "file.txt");
            File.WriteAllText(localPath, "local\n");
            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            string output = Path.Combine(parent, "conflicts");
            XamlNexusProjectUpgrade.WriteConflictArtifacts(plan, output);
            Assert.Equal("XU2021", Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                XamlNexusProjectUpgrade.ResolveConflicts(plan, output)).Code);
            Assert.Equal("old\n", File.ReadAllText(Path.Combine(current.RootDirectory, "other.txt")));
            File.WriteAllText(Path.Combine(output, "file.txt.merge"), "resolved\n");
            if (stale) {
                File.WriteAllText(localPath, "late edit\n");
                var fresh = XamlNexusProjectUpgrade.CreatePlan(current, target);
                Assert.Equal("XU2020", Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                    XamlNexusProjectUpgrade.ResolveConflicts(fresh, output)).Code);
                var resolved = XamlNexusProjectUpgrade.ResolveConflicts(plan, output);
                Assert.Equal("XU2006", Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                    XamlNexusProjectUpgrade.Apply(current, target, resolved)).Code);
                Assert.Equal("old\n", File.ReadAllText(Path.Combine(current.RootDirectory, "other.txt")));
                return;
            }
            XamlNexusProjectUpgrade.Apply(current, target, XamlNexusProjectUpgrade.ResolveConflicts(plan, output));
            Assert.Equal("resolved\n", File.ReadAllText(localPath));
            Assert.Equal("new\n", File.ReadAllText(Path.Combine(current.RootDirectory, "other.txt")));
            var manifest = XamlNexusProjectManifestStore.Load(current.ManifestPath);
            Assert.Equal("2.0.0", manifest.GeneratorVersion);
            var baseline = manifest.ScaffoldFiles!.Single(file => file.Path == "file.txt");
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("target\n"),
                XamlNexusBaselineContent.Decode(baseline.BaselineContentGzipBase64!));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public void Apply_RejectsPlanThatBecameStale() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext current = CreateProject(
                Path.Combine(parent, "current"),
                "1.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "old" });
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "2.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "new" });
            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            File.WriteAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt"), "late edit");

            XamlNexusProjectUpgradeException exception = Assert.Throws<XamlNexusProjectUpgradeException>(() =>
                XamlNexusProjectUpgrade.Apply(current, target, plan));

            Assert.Equal("XU2006", exception.Code);
            Assert.Equal("late edit", File.ReadAllText(Path.Combine(current.RootDirectory, "eng", "managed.txt")));
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void BaselineAdoption_RequiresExactSameVersionScaffold() {
        string parent = CreateTemporaryDirectory();
        try {
            XamlNexusProjectContext generated = CreateProject(
                Path.Combine(parent, "generated"),
                "1.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "same" });
            XamlNexusProjectManifest withoutBaseline = new() {
                GeneratorVersion = generated.Manifest.GeneratorVersion,
                Project = generated.Manifest.Project,
                Modules = generated.Manifest.Modules,
            };
            XamlNexusProjectManifestStore.Save(generated.ManifestPath, withoutBaseline);
            XamlNexusProjectContext current = XamlNexusProjectLocator.Locate(generated.RootDirectory);
            XamlNexusProjectContext target = CreateProject(
                Path.Combine(parent, "target"),
                "1.0.0",
                new Dictionary<string, string> { ["eng/managed.txt"] = "same" });

            XamlNexusProjectUpgradePlan plan = XamlNexusProjectUpgrade.CreateBaselineAdoptionPlan(current, target);
            XamlNexusProjectUpgrade.Apply(current, target, plan);

            Assert.True(plan.CanApply);
            Assert.Empty(plan.Changes);
            Assert.Single(XamlNexusProjectManifestStore.Load(current.ManifestPath).ScaffoldFiles!);
        }
        finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void UpgradeSynchronizesTemplateModulesAndPreservesRecipeMetadata() {
        string parent = CreateTemporaryDirectory();
        try {
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0", new Dictionary<string, string>(), includeRecipe: true);
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0", new Dictionary<string, string>());
            target = WithManifest(target, [new() { Id = "new-module", Version = "2.1.0", Source = "template" }], target.Manifest.ScaffoldFiles);
            XamlNexusProjectUpgrade.Apply(current, target, XamlNexusProjectUpgrade.CreatePlan(current, target));
            var modules = XamlNexusProjectLocator.Locate(current.RootDirectory).Manifest.Modules;
            Assert.DoesNotContain(modules, module => module.Id == "app-shell");
            var template = Assert.Single(modules, module => module.Source == "template");
            Assert.Equal("new-module", template.Id);
            Assert.Equal("2.1.0", template.Version);
            var recipe = Assert.Single(modules, module => module.Source == "recipe");
            Assert.Equal("sample-recipe", recipe.Id);
            Assert.Equal("4.0.0", recipe.Version);
            Assert.Empty(recipe.Files!);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Theory]
    [InlineData(false, "same")]
    [InlineData(false, "different")]
    [InlineData(true, "same")]
    [InlineData(true, "different")]
    public void UpgradeRejectsRecipeOwnedPathsBeforeWriting(bool adoption, string targetContent) {
        string parent = CreateTemporaryDirectory();
        try {
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0", new Dictionary<string, string> { ["eng/owned.txt"] = "same" });
            var owned = current.Manifest.ScaffoldFiles![0];
            current = WithManifest(current, [new() {
                Id = "sample-recipe", Version = "4.0.0", Source = "recipe",
                Files = [new() { Path = "ENG\\owned.txt", Sha256 = owned.Sha256 }],
            }], adoption ? null : []);
            var target = CreateProject(Path.Combine(parent, "target"), adoption ? "1.0.0" : "2.0.0",
                new Dictionary<string, string> { ["eng/owned.txt"] = targetContent });
            byte[] manifest = File.ReadAllBytes(current.ManifestPath);
            var plan = adoption ? XamlNexusProjectUpgrade.CreateBaselineAdoptionPlan(current, target)
                : XamlNexusProjectUpgrade.CreatePlan(current, target);
            Assert.False(plan.CanApply);
            Assert.Empty(plan.Changes);
            Assert.Contains(plan.Conflicts, conflict => conflict.Code == "XU2013" && conflict.Message.Contains("sample-recipe"));
            Assert.Throws<XamlNexusProjectUpgradeException>(() => XamlNexusProjectUpgrade.Apply(current, target, plan));
            Assert.Equal(manifest, File.ReadAllBytes(current.ManifestPath));
            Assert.Equal("same", File.ReadAllText(Path.Combine(current.RootDirectory, "eng/owned.txt")));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public void UpgradeRejectsTemplateModuleIdAlreadyOwnedByRecipe() {
        string parent = CreateTemporaryDirectory();
        try {
            var current = CreateProject(Path.Combine(parent, "current"), "1.0.0", new Dictionary<string, string>(), includeRecipe: true);
            var target = CreateProject(Path.Combine(parent, "target"), "2.0.0", new Dictionary<string, string>());
            target = WithManifest(target, [new() { Id = "SAMPLE-RECIPE", Version = "2.0.0", Source = "template" }], []);
            var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
            Assert.False(plan.CanApply);
            Assert.Contains(plan.Conflicts, conflict => conflict.Code == "XU2014");
            Assert.Empty(plan.Changes);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    private static XamlNexusProjectContext WithManifest(XamlNexusProjectContext context,
        IReadOnlyList<XamlNexusManagedModule> modules, IReadOnlyList<XamlNexusManagedFile>? scaffold) {
        XamlNexusProjectManifestStore.Save(context.ManifestPath, new() {
            GeneratorVersion = context.Manifest.GeneratorVersion, Project = context.Manifest.Project,
            Modules = modules, ScaffoldFiles = scaffold,
        });
        return XamlNexusProjectLocator.Locate(context.RootDirectory);
    }

    private static XamlNexusProjectContext CreateProject(
        string root,
        string version,
        IReadOnlyDictionary<string, string> files,
        bool includeRecipe = false,
        bool includeBaselineContent = true,
        string? profile = null) {
        Directory.CreateDirectory(root);
        foreach ((string relativePath, string content) in files) {
            string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
        var modules = new List<XamlNexusManagedModule> {
            new() { Id = "app-shell", Version = version, Source = "template" },
        };
        if (includeRecipe) {
            modules.Add(new XamlNexusManagedModule {
                Id = "sample-recipe",
                Version = "4.0.0",
                Source = "recipe",
                Files = [],
            });
        }
        var manifest = new XamlNexusProjectManifest {
            GeneratorVersion = version,
            Project = new XamlNexusProjectIdentity {
                Name = "UpgradeApp",
                Profile = profile,
                Preset = "winui",
                Language = "en-US",
                SolutionFormat = "sln",
            },
            Modules = modules,
            ScaffoldFiles = files.Select(pair => new XamlNexusManagedFile {
                Path = pair.Key,
                Sha256 = ComputeSha256(Path.Combine(
                    root,
                    pair.Key.Replace('/', Path.DirectorySeparatorChar))),
                BaselineContentGzipBase64 = includeBaselineContent
                    ? XamlNexusBaselineContent.Encode(File.ReadAllBytes(Path.Combine(
                        root,
                        pair.Key.Replace('/', Path.DirectorySeparatorChar))))
                    : null,
            }).ToArray(),
        };
        XamlNexusProjectManifestStore.Save(Path.Combine(root, "xamlnexus.json"), manifest);
        return XamlNexusProjectLocator.Locate(root);
    }

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(
            Path.GetTempPath(),
            "xamlnexus-upgrade-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
