using System.Text;
using System.Xml.Linq;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class RecipeContractTests {
    [Theory]
    [InlineData("sln")]
    [InlineData("slnx")]
    public void RootReadmes_AreRegisteredAndRemovedTransactionally(string format) {
        string root = CreateProjectDirectory();
        try {
            string solution = Path.Combine(root, "DocsTest." + format);
            File.WriteAllText(solution, format == "slnx" ? "<Solution />" : "Global\nEndGlobal\n");
            var recipe = new TestRecipe(CreateDescriptor(), new XamlNexusRecipePlan {
                Changes = [XamlNexusRecipeFileChange.CreateText("sample.README.md", "English"),
                    XamlNexusRecipeFileChange.CreateText("sample.README.zh-CN.md", "中文")],
            });
            var context = XamlNexusProjectLocator.Locate(root);
            XamlNexusRecipeTransaction.PreviewApply(context, recipe);
            Assert.DoesNotContain("sample.README", File.ReadAllText(solution));
            XamlNexusRecipeTransaction.Apply(context, recipe);
            string added = File.ReadAllText(solution);
            Assert.Contains("Docs", added);
            Assert.Contains("sample.README.md", added);
            Assert.Contains("sample.README.zh-CN.md", added);
            if (format == "slnx") {
                var folder = Assert.Single(XDocument.Parse(added).Root!.Elements("Folder"));
                Assert.Equal(2, folder.Elements("File").Count());
            }
            XamlNexusRecipeTransaction.Remove(XamlNexusProjectLocator.Locate(root), recipe);
            Assert.DoesNotContain("sample.README", File.ReadAllText(solution));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void EveryBuiltInRecipe_IncludesBothReadmeLanguages() {
        string root = CreateProjectDirectory();
        try {
            foreach (string relative in new[] { "RecipeTestApp.Common/ISystemTraySettings.cs",
                "RecipeTestApp.Models/Cores/Settings.cs", "RecipeTestApp.Models/Cores/Interfaces/ISettings.cs" }) {
                string path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "// WindowCloseBehavior support fixture");
            }
            var project = XamlNexusProjectLocator.Locate(root);
            foreach (var recipe in BuiltInRecipeCatalog.Create().Recipes) {
                var plan = recipe.CreatePlan(new(root, project.Manifest));
                var readmes = plan.Changes.Where(change => change.RelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToArray();
                Assert.Contains(readmes, change => change.RelativePath.EndsWith("README.md", StringComparison.Ordinal));
                Assert.Contains(readmes, change => change.RelativePath.EndsWith("README.zh-CN.md", StringComparison.Ordinal));
                foreach (var readme in readmes) {
                    string text = Encoding.UTF8.GetString(readme.Content!);
                    Assert.DoesNotContain("{{", text);
                    Assert.Contains("[English]", text);
                    Assert.Contains("[简体中文]", text);
                }
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("sln", false, "CustomPanel")]
    [InlineData("sln", true, "CustomPanel")]
    [InlineData("slnx", false, "CustomPanel")]
    [InlineData("slnx", true, "CustomPanel")]
    [InlineData("sln", true, "Otherpanel")]
    [InlineData("slnx", true, "Otherpanel")]
    public void AddPanel_CreatesOrReusesSolutionFolder(string format, bool existingFolder, string panelName) {
        string root = CreateProjectDirectory();
        try {
            const string folderGuid = "{CCCCCCCC-3333-3333-3333-333333333333}";
            string solution = "PanelsTest." + format;
            string initial = format == "slnx"
                ? "<Solution>" + (existingFolder ? "<Folder Name=\"/Panels/\" />" : "") + "</Solution>"
                : (existingFolder ? $"Project(\"{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}\") = \"Panels\", \"Panels\", \"{folderGuid}\"\nEndProject\n" : "")
                    + "Global\n\tGlobalSection(SolutionConfigurationPlatforms) = preSolution\n\t\tDebug|Any CPU = Debug|Any CPU\n\tEndGlobalSection\n"
                    + "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\n\tEndGlobalSection\nEndGlobal\n";
            File.WriteAllText(Path.Combine(root, solution), initial);
            var recipe = new TestRecipe(CreateDescriptor(), new XamlNexusRecipePlan {
                Changes = [XamlNexusRecipeFileChange.CreateText(panelName + ".csproj", "<Project />")],
                ProjectOperations = [new AddProjectToSolutionOperation(solution, panelName + ".csproj")],
            });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);
            string actual = File.ReadAllText(Path.Combine(root, solution));
            if (format == "slnx") {
                var folder = Assert.Single(XDocument.Parse(actual).Root!.Elements("Folder"));
                Assert.Equal("/Panels/", (string?)folder.Attribute("Name"));
                Assert.Equal(panelName + ".csproj", (string?)Assert.Single(folder.Elements("Project")).Attribute("Path"));
            }
            else {
                var headers = actual.Split('\n').Where(line => line.Contains(" = \"Panels\", ")).ToArray();
                string header = Assert.Single(headers);
                string expectedFolder = header.Split(',')[2].Trim().Trim('"');
                if (existingFolder) Assert.Equal(folderGuid, expectedFolder);
                string projectGuid = XamlNexusSolutionGuid.CreateDeterministic(panelName + ".csproj").ToString("B").ToUpperInvariant();
                Assert.Contains($"{projectGuid} = {expectedFolder}", actual);
                Assert.Contains("GlobalSection(NestedProjects)", actual);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void AppUpdateRecipe_InstallsAndRemovesWithoutEditingHost() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new AppUpdateRecipe();
            var result = XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);
            Assert.Equal(7, result.ChangedFiles.Count);
            foreach (string name in new[] { "app-update.README.md", "app-update.README.zh-CN.md" }) {
                string readme = File.ReadAllText(Path.Combine(root, name));
                Assert.Contains("RecipeTestApp.Common/Consts.cs", readme);
                Assert.Contains("app-update.README.zh-CN.md", readme);
                Assert.DoesNotContain("{{", readme);
            }
            string module = File.ReadAllText(Path.Combine(root, "RecipeTestApp.UI/Modules/AppUpdateModule.cs"));
            Assert.Contains("RecipeTestApp.UI.Modules", module);
            Assert.DoesNotContain("Winui3_XamlNexus", module);
            Assert.False(File.Exists(Path.Combine(root, "RecipeTestApp.UI/App.xaml.cs")));
            XamlNexusRecipeTransaction.Remove(XamlNexusProjectLocator.Locate(root), recipe);
            Assert.False(File.Exists(Path.Combine(root, "RecipeTestApp.UI/Modules/AppUpdateModule.cs")));
            Assert.False(File.Exists(Path.Combine(root, "app-update.README.md")));
            Assert.False(File.Exists(Path.Combine(root, "app-update.README.zh-CN.md")));
            Assert.DoesNotContain(XamlNexusProjectLocator.Locate(root).Manifest.Modules, item => item.Id == "app-update");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void BuiltInCatalog_ContainsValidEditorConfigRecipe() {
        IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();

        Assert.Equal(5, catalog.Recipes.Count);
        Assert.Equal("settings", catalog.Find("SETTINGS")!.Descriptor.Id);
        IXamlNexusRecipe recipe = catalog.Find("editorconfig")!;
        Assert.Equal("editorconfig", recipe.Descriptor.Id);
        Assert.Same(recipe, catalog.Find("EDITORCONFIG"));
        Assert.Equal("sqlite", catalog.Find("SQLITE")!.Descriptor.Id);
        Assert.Equal("system-tray", catalog.Find("SYSTEM-TRAY")!.Descriptor.Id);
    }

    [Theory]
    [InlineData("winui", "RecipeTestApp.UI/RecipeTestApp.UI.csproj")]
    [InlineData("hybrid", "RecipeTestApp/RecipeTestApp.csproj")]
    public void SqliteRecipe_TargetsOnlyThePresetDataHost(string preset, string expectedHost) {
        var manifest = new XamlNexusProjectManifest {
            GeneratorVersion = "1.0.3",
            Project = new XamlNexusProjectIdentity {
                Name = "RecipeTestApp",
                Preset = preset,
                Language = "en-US",
                SolutionFormat = "sln",
            },
            Modules = [CreateModule("settings", "template")],
        };
        IXamlNexusRecipe recipe = BuiltInRecipeCatalog.Create().Find("sqlite")!;

        XamlNexusRecipePlan plan = recipe.CreatePlan(new XamlNexusRecipeContext("unused", manifest));

        AddProjectReferenceOperation reference = Assert.Single(
            plan.ProjectOperations.OfType<AddProjectReferenceOperation>());
        Assert.Equal(expectedHost, reference.ProjectPath);
        Assert.Equal("RecipeTestApp.Data/RecipeTestApp.Data.csproj", reference.ReferencedProjectPath);
        Assert.Contains(plan.Changes, change => change.RelativePath.EndsWith("AppDbContext.cs"));
        Assert.Contains(plan.Changes, change => change.RelativePath.EndsWith("InitialCreate.cs"));
        string expectedModule = preset == "hybrid"
            ? "RecipeTestApp/Modules/SqliteModule.cs"
            : "RecipeTestApp.UI/Modules/SqliteModule.cs";
        Assert.Contains(plan.Changes, change => change.RelativePath == expectedModule);
        Assert.Equal(preset == "hybrid" ? 14 : 9, plan.Changes.Count);
        foreach (string name in new[] { "README.md", "README.zh-CN.md" }) {
            var readme = Assert.Single(plan.Changes, change => change.RelativePath == "RecipeTestApp.Data/" + name);
            string text = Encoding.UTF8.GetString(readme.Content!);
            Assert.Contains("RecipeTestApp.Data", text);
            Assert.Contains(preset == "hybrid" ? "`RecipeTestApp` WPF" : "`RecipeTestApp.UI` WinUI", text);
            Assert.Contains("README.zh-CN.md", text);
            Assert.DoesNotContain("{{", text);
        }
        Assert.Equal(
            preset == "hybrid",
            plan.ProjectOperations.OfType<AddProtobufOperation>().Any());
    }

    [Fact]
    public void Catalog_DuplicateIds_Throws() {
        var recipe = new TestRecipe(
            CreateDescriptor(),
            new XamlNexusRecipePlan { Changes = [] });

        XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
            () => new XamlNexusRecipeCatalog([recipe, recipe]));

        Assert.Equal("XR1401", exception.Code);
    }

    [Theory]
    [InlineData("Bad_Id", "1.0.0", "winui", "XR1001")]
    [InlineData("sample", "1.0", "winui", "XR1002")]
    [InlineData("sample", "1.0.0", "avalonia", "XR1004")]
    public void ValidateDescriptor_InvalidMetadata_ThrowsCodedError(
        string id,
        string version,
        string preset,
        string expectedCode) {
        XamlNexusRecipeDescriptor descriptor = CreateDescriptor(
            id: id,
            version: version,
            supportedPresets: [preset]);

        XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
            () => XamlNexusRecipeContract.ValidateDescriptor(descriptor));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void ValidateCompatibility_MissingDependency_Throws() {
        XamlNexusProjectManifest manifest = CreateManifest();
        XamlNexusRecipeDescriptor descriptor = CreateDescriptor(dependencies: ["data-core"]);

        XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
            () => XamlNexusRecipeContract.ValidateCompatibility(descriptor, manifest));

        Assert.Equal("XR1103", exception.Code);
    }

    [Fact]
    public void ValidateCompatibility_InstalledConflict_Throws() {
        XamlNexusProjectManifest manifest = CreateManifest([CreateModule("legacy-data", "template")]);
        XamlNexusRecipeDescriptor descriptor = CreateDescriptor(conflicts: ["legacy-data"]);

        XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
            () => XamlNexusRecipeContract.ValidateCompatibility(descriptor, manifest));

        Assert.Equal("XR1104", exception.Code);
    }

    [Fact]
    public void PreviewApply_ResolvesAllChangesWithoutWritingProject() {
        string root = CreateProjectDirectory();
        try {
            string projectPath = Path.Combine(root, "App.csproj");
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            byte[] originalManifest = File.ReadAllBytes(Path.Combine(root, "xamlnexus.json"));
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("Feature/owned.txt", "created")],
                    ProjectOperations = [new AddPackageReferenceOperation("App.csproj", "Sample.Package", "1.0.0")],
                });

            XamlNexusRecipePreview preview = XamlNexusRecipeTransaction.PreviewApply(
                XamlNexusProjectLocator.Locate(root),
                recipe);

            Assert.Equal("add", preview.Operation);
            Assert.Contains(preview.Changes, change =>
                change.RelativePath == "Feature/owned.txt" && change.Scope == "owned");
            Assert.Contains(preview.Changes, change =>
                change.RelativePath == "App.csproj" && change.Scope == "project");
            Assert.False(File.Exists(Path.Combine(root, "Feature", "owned.txt")));
            Assert.Equal("<Project Sdk=\"Microsoft.NET.Sdk\" />", File.ReadAllText(projectPath));
            Assert.Equal(originalManifest, File.ReadAllBytes(Path.Combine(root, "xamlnexus.json")));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreviewRemove_LeavesOwnedFilesAndManifestUnchanged() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("Feature/owned.txt", "created")],
                });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);
            byte[] originalManifest = File.ReadAllBytes(Path.Combine(root, "xamlnexus.json"));

            XamlNexusRecipePreview preview = XamlNexusRecipeTransaction.PreviewRemove(
                XamlNexusProjectLocator.Locate(root),
                recipe);

            XamlNexusRecipePreviewChange change = Assert.Single(preview.Changes);
            Assert.Equal(XamlNexusRecipeFileChangeKind.Delete, change.Kind);
            Assert.True(File.Exists(Path.Combine(root, "Feature", "owned.txt")));
            Assert.Equal(originalManifest, File.ReadAllBytes(Path.Combine(root, "xamlnexus.json")));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreviewUpdate_LeavesInstalledVersionAndContentUnchanged() {
        string root = CreateProjectDirectory();
        try {
            var installedRecipe = new TestRecipe(
                CreateDescriptor(version: "1.0.0"),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("owned.txt", "one")],
                });
            var updatedRecipe = new TestRecipe(
                CreateDescriptor(version: "2.0.0"),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("owned.txt", "two")],
                });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), installedRecipe);

            XamlNexusRecipePreview preview = XamlNexusRecipeTransaction.PreviewUpdate(
                XamlNexusProjectLocator.Locate(root),
                updatedRecipe);

            Assert.Equal("1.0.0", preview.FromVersion);
            Assert.Equal("2.0.0", preview.ToVersion);
            Assert.Equal("one", File.ReadAllText(Path.Combine(root, "owned.txt")));
            XamlNexusManagedModule module = Assert.Single(
                XamlNexusProjectManifestStore.Load(Path.Combine(root, "xamlnexus.json")).Modules,
                value => value.Id == "sample-recipe");
            Assert.Equal("1.0.0", module.Version);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_CreateFile_AddsRecipeModuleLast() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("Data/Recipe.txt", "created")],
                });

            XamlNexusRecipeApplyResult result = XamlNexusRecipeTransaction.Apply(
                XamlNexusProjectLocator.Locate(root),
                recipe);

            Assert.Equal("created", File.ReadAllText(Path.Combine(root, "Data", "Recipe.txt")));
            Assert.Equal(["Data/Recipe.txt"], result.ChangedFiles);
            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(
                Path.Combine(root, "xamlnexus.json"));
            XamlNexusManagedModule installed = Assert.Single(
                manifest.Modules,
                module => module.Id == "sample-recipe");
            Assert.Equal("1.2.3", installed.Version);
            Assert.Equal("recipe", installed.Source);
            XamlNexusManagedFile ownedFile = Assert.Single(installed.Files!);
            Assert.Equal("Data/Recipe.txt", ownedFile.Path);
            Assert.Equal(
                XamlNexusRecipeHash.ComputeFile(Path.Combine(root, "Data", "Recipe.txt")),
                ownedFile.Sha256);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_ReplaceMatchingFile_PreservesSafetyAndUpdatesContent() {
        string root = CreateProjectDirectory();
        try {
            string target = Path.Combine(root, "existing.txt");
            File.WriteAllText(target, "before");
            string expectedHash = XamlNexusRecipeHash.Compute(Encoding.UTF8.GetBytes("before"));
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.ReplaceText("existing.txt", "after", expectedHash)],
                });

            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);

            Assert.Equal("after", File.ReadAllText(target));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_DeleteMatchingFile_RemovesFile() {
        string root = CreateProjectDirectory();
        try {
            string target = Path.Combine(root, "obsolete.txt");
            File.WriteAllText(target, "obsolete");
            string expectedHash = XamlNexusRecipeHash.Compute(Encoding.UTF8.GetBytes("obsolete"));
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.Delete("obsolete.txt", expectedHash)],
                });

            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);

            Assert.False(File.Exists(target));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_ModifiedFileHashMismatch_MakesNoChanges() {
        string root = CreateProjectDirectory();
        try {
            string target = Path.Combine(root, "existing.txt");
            File.WriteAllText(target, "user change");
            string manifestBefore = File.ReadAllText(Path.Combine(root, "xamlnexus.json"));
            string staleHash = XamlNexusRecipeHash.Compute(Encoding.UTF8.GetBytes("template content"));
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [
                        XamlNexusRecipeFileChange.CreateText("should-not-exist.txt", "new"),
                        XamlNexusRecipeFileChange.ReplaceText("existing.txt", "replacement", staleHash),
                    ],
                });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
                () => XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal("XR1211", exception.Code);
            Assert.Equal("user change", File.ReadAllText(target));
            Assert.False(File.Exists(Path.Combine(root, "should-not-exist.txt")));
            Assert.Equal(manifestBefore, File.ReadAllText(Path.Combine(root, "xamlnexus.json")));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../escape.txt", "XR1203")]
    [InlineData("xamlnexus.json", "XR1204")]
    public void Apply_UnsafeRecipePath_IsRejected(string relativePath, string expectedCode) {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText(relativePath, "unsafe")],
                });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
                () => XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal(expectedCode, exception.Code);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_ParentPathIsFile_FailsBeforeFirstChange() {
        string root = CreateProjectDirectory();
        try {
            File.WriteAllText(Path.Combine(root, "blocked"), "file");
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [
                        XamlNexusRecipeFileChange.CreateText("first.txt", "first"),
                        XamlNexusRecipeFileChange.CreateText("blocked/second.txt", "second"),
                    ],
                });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
                () => XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal("XR1213", exception.Code);
            Assert.False(File.Exists(Path.Combine(root, "first.txt")));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Apply_StructuredProjectOperations_AddReferencesAndSolutionProject(string newline) {
        string root = CreateProjectDirectory();
        try {
            string appProject = Path.Combine(root, "RecipeTestApp.UI", "RecipeTestApp.UI.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProject)!);
            File.WriteAllText(appProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            string solution = Path.Combine(root, "RecipeTestApp.sln");
            File.WriteAllText(solution, CreateSolutionText().Replace("\r\n", "\n").Replace("\n", newline));

            const string dataProjectPath = "RecipeTestApp.Data/RecipeTestApp.Data.csproj";
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [
                        XamlNexusRecipeFileChange.CreateText(
                            dataProjectPath,
                            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>"),
                    ],
                    ProjectOperations = [
                        new AddPackageReferenceOperation(
                            "RecipeTestApp.UI/RecipeTestApp.UI.csproj",
                            "Microsoft.Extensions.Hosting",
                            "8.0.1"),
                        new AddProjectReferenceOperation(
                            "RecipeTestApp.UI/RecipeTestApp.UI.csproj",
                            dataProjectPath),
                        new AddProjectToSolutionOperation("RecipeTestApp.sln", dataProjectPath),
                    ],
                });

            XamlNexusRecipeApplyResult result = XamlNexusRecipeTransaction.Apply(
                XamlNexusProjectLocator.Locate(root),
                recipe);

            XDocument project = XDocument.Load(appProject);
            XElement package = Assert.Single(project.Descendants(), element => element.Name.LocalName == "PackageReference");
            Assert.Equal("Microsoft.Extensions.Hosting", (string?)package.Attribute("Include"));
            Assert.Equal("8.0.1", (string?)package.Attribute("Version"));
            XElement reference = Assert.Single(project.Descendants(), element => element.Name.LocalName == "ProjectReference");
            Assert.Equal(
                Path.Combine("..", "RecipeTestApp.Data", "RecipeTestApp.Data.csproj"),
                (string?)reference.Attribute("Include"));

            string solutionText = File.ReadAllText(solution);
            Assert.Contains("RecipeTestApp.Data\\RecipeTestApp.Data.csproj", solutionText);
            Assert.Contains(".Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.DoesNotContain(".GlobalSection(", solutionText);
            var mappingLines = solutionText.Split(newline).Where(line => line.Contains(".ActiveCfg = ") || line.Contains(".Build.0 = ")).ToArray();
            Assert.Equal(4, mappingLines.Length);
            Assert.All(mappingLines, line => {
                Assert.StartsWith("\t\t{", line);
                Assert.DoesNotContain("preSolution", line);
            });
            Assert.DoesNotContain(newline + "EndGlobalSection", solutionText);
            Assert.Contains(newline + "\tEndGlobalSection", solutionText);
            Assert.Equal(3, result.ChangedFiles.Count);

            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(
                Path.Combine(root, "xamlnexus.json"));
            XamlNexusManagedModule module = Assert.Single(
                manifest.Modules,
                value => value.Id == "sample-recipe");
            XamlNexusManagedFile ownedFile = Assert.Single(module.Files!);
            Assert.Equal(dataProjectPath, ownedFile.Path);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_DuplicateStructuredReference_MakesNoChanges() {
        string root = CreateProjectDirectory();
        try {
            string projectPath = Path.Combine(root, "App.csproj");
            const string original = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Existing.Package\" Version=\"1.0.0\" /></ItemGroup></Project>";
            File.WriteAllText(projectPath, original);
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("should-not-exist.txt", "new")],
                    ProjectOperations = [new AddPackageReferenceOperation("App.csproj", "Existing.Package", "2.0.0")],
                });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
                () => XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal("XR1226", exception.Code);
            Assert.Equal(original, File.ReadAllText(projectPath));
            Assert.False(File.Exists(Path.Combine(root, "should-not-exist.txt")));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_ProjectOperationEscapingRoot_IsRejected() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [],
                    ProjectOperations = [new AddPackageReferenceOperation("../outside.csproj", "Sample", "1.0.0")],
                });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(
                () => XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal("XR1236", exception.Code);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("8.0.0", "8.0.1")]
    [InlineData("9.0.0", "9.0.0")]
    public void Apply_EnsurePackageReference_UpgradesButNeverDowngrades(
        string currentVersion,
        string expectedVersion) {
        string root = CreateProjectDirectory();
        try {
            string projectPath = Path.Combine(root, "App.csproj");
            File.WriteAllText(
                projectPath,
                $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Sample.Package\" Version=\"{currentVersion}\" /></ItemGroup></Project>");
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [],
                    ProjectOperations = [
                        new EnsurePackageReferenceOperation("App.csproj", "Sample.Package", "8.0.1"),
                    ],
                });

            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);

            XDocument project = XDocument.Load(projectPath);
            XElement package = Assert.Single(
                project.Descendants(),
                element => element.Name.LocalName == "PackageReference");
            Assert.Equal(expectedVersion, (string?)package.Attribute("Version"));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("<ItemGroup><PackageReference Include='Sample.Package' Version='9.0.0' Condition='false'/></ItemGroup>")]
    [InlineData("<ItemGroup Condition='false'><PackageReference Include='Sample.Package' Version='8.0.0'/></ItemGroup>")]
    [InlineData("<ItemGroup Condition='false'><PackageReference Include='Sample.Package' Version='9.0.0'/></ItemGroup><ItemGroup Condition='true'><PackageReference Include='Sample.Package' Version='8.0.0'/></ItemGroup>")]
    [InlineData("<Choose><When Condition='false'><ItemGroup/></When><Otherwise><ItemGroup><PackageReference Include='Sample.Package' Version='9.0.0'/></ItemGroup></Otherwise></Choose>")]
    [InlineData("<ItemGroup><PackageReference Include='Sample.Package'><Version Condition='false'>9.0.0</Version></PackageReference></ItemGroup>")]
    [InlineData("<ItemGroup><PackageReference Include='Sample.Package' Version='9.0.0'/><PackageReference Update='Sample.Package' Version='7.0.0' Condition='true'/></ItemGroup>")]
    public void EnsureConditionalPackageRejectsPreviewAndApplyWithoutChanges(string items) {
        string root = CreateProjectDirectory();
        try {
            string projectPath = Path.Combine(root, "App.csproj");
            File.WriteAllText(projectPath, $"<Project>{items}</Project>");
            var context = XamlNexusProjectLocator.Locate(root);
            byte[] originalProject = File.ReadAllBytes(projectPath);
            byte[] originalManifest = File.ReadAllBytes(context.ManifestPath);
            var recipe = new TestRecipe(CreateDescriptor(), new XamlNexusRecipePlan {
                Changes = [XamlNexusRecipeFileChange.CreateText("module.txt", "must not be written")],
                ProjectOperations = [new EnsurePackageReferenceOperation("App.csproj", "Sample.Package", "8.0.1")],
            });
            var preview = Assert.Throws<XamlNexusRecipeException>(() => XamlNexusRecipeTransaction.PreviewApply(context, recipe));
            var apply = Assert.Throws<XamlNexusRecipeException>(() => XamlNexusRecipeTransaction.Apply(context, recipe));
            Assert.Equal("XR1247", preview.Code);
            Assert.Equal("XR1247", apply.Code);
            Assert.Contains("Sample.Package", apply.Message);
            Assert.Contains("App.csproj", apply.Message);
            Assert.Contains("conditional", apply.Message);
            Assert.Equal(originalProject, File.ReadAllBytes(projectPath));
            Assert.Equal(originalManifest, File.ReadAllBytes(context.ManifestPath));
            Assert.False(File.Exists(Path.Combine(root, "module.txt")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Remove_DeletesOwnedFilesAndManifestEntry() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("Feature/owned.txt", "managed")],
                });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);

            XamlNexusRecipeApplyResult result = XamlNexusRecipeTransaction.Remove(
                XamlNexusProjectLocator.Locate(root),
                recipe);

            Assert.Equal("sample-recipe", result.RecipeId);
            Assert.False(File.Exists(Path.Combine(root, "Feature", "owned.txt")));
            Assert.False(Directory.Exists(Path.Combine(root, "Feature")));
            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(
                Path.Combine(root, "xamlnexus.json"));
            Assert.DoesNotContain(manifest.Modules, module => module.Id == "sample-recipe");
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Remove_ModifiedOwnedFile_IsRejectedWithoutChangingManifest() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("owned.txt", "managed")],
                });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);
            File.WriteAllText(Path.Combine(root, "owned.txt"), "user change");

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(() =>
                XamlNexusRecipeTransaction.Remove(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal("XR1211", exception.Code);
            Assert.Equal("user change", File.ReadAllText(Path.Combine(root, "owned.txt")));
            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(
                Path.Combine(root, "xamlnexus.json"));
            Assert.Contains(manifest.Modules, module => module.Id == "sample-recipe");
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Remove_NotInstalledRecipe_IsRejected() {
        string root = CreateProjectDirectory();
        try {
            var recipe = new TestRecipe(
                CreateDescriptor(),
                new XamlNexusRecipePlan { Changes = [] });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(() =>
                XamlNexusRecipeTransaction.Remove(XamlNexusProjectLocator.Locate(root), recipe));

            Assert.Equal("XR1401", exception.Code);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Update_ReconcilesOwnedFilesAndManifestVersion() {
        string root = CreateProjectDirectory();
        try {
            var originalRecipe = new TestRecipe(
                CreateDescriptor(version: "1.0.0"),
                new XamlNexusRecipePlan {
                    Changes = [
                        XamlNexusRecipeFileChange.CreateText("Feature/current.txt", "version one"),
                        XamlNexusRecipeFileChange.CreateText("Feature/obsolete.txt", "remove me"),
                    ],
                });
            var updatedRecipe = new TestRecipe(
                CreateDescriptor(version: "2.0.0"),
                new XamlNexusRecipePlan {
                    Changes = [
                        XamlNexusRecipeFileChange.CreateText("Feature/current.txt", "version two"),
                        XamlNexusRecipeFileChange.CreateText("Feature/new.txt", "new file"),
                    ],
                });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), originalRecipe);

            XamlNexusRecipeApplyResult result = XamlNexusRecipeTransaction.Update(
                XamlNexusProjectLocator.Locate(root),
                updatedRecipe);

            Assert.Equal("2.0.0", result.RecipeVersion);
            Assert.Equal("version two", File.ReadAllText(Path.Combine(root, "Feature", "current.txt")));
            Assert.Equal("new file", File.ReadAllText(Path.Combine(root, "Feature", "new.txt")));
            Assert.False(File.Exists(Path.Combine(root, "Feature", "obsolete.txt")));
            XamlNexusManagedModule module = Assert.Single(
                XamlNexusProjectManifestStore.Load(Path.Combine(root, "xamlnexus.json")).Modules,
                value => value.Id == "sample-recipe");
            Assert.Equal("2.0.0", module.Version);
            Assert.Equal(2, module.Files!.Count);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Update_ModifiedOwnedFile_LeavesEntireProjectUnchanged() {
        string root = CreateProjectDirectory();
        try {
            var originalRecipe = new TestRecipe(
                CreateDescriptor(version: "1.0.0"),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("owned.txt", "version one")],
                });
            var updatedRecipe = new TestRecipe(
                CreateDescriptor(version: "1.1.0"),
                new XamlNexusRecipePlan {
                    Changes = [
                        XamlNexusRecipeFileChange.CreateText("owned.txt", "version two"),
                        XamlNexusRecipeFileChange.CreateText("new.txt", "new file"),
                    ],
                });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), originalRecipe);
            File.WriteAllText(Path.Combine(root, "owned.txt"), "user change");

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(() =>
                XamlNexusRecipeTransaction.Update(
                    XamlNexusProjectLocator.Locate(root),
                    updatedRecipe));

            Assert.Equal("XR1211", exception.Code);
            Assert.Equal("user change", File.ReadAllText(Path.Combine(root, "owned.txt")));
            Assert.False(File.Exists(Path.Combine(root, "new.txt")));
            XamlNexusManagedModule module = Assert.Single(
                XamlNexusProjectManifestStore.Load(Path.Combine(root, "xamlnexus.json")).Modules,
                value => value.Id == "sample-recipe");
            Assert.Equal("1.0.0", module.Version);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("1.0.0", "XR1503")]
    [InlineData("0.9.0", "XR1504")]
    public void Update_RejectsCurrentVersionAndDowngrades(
        string requestedVersion,
        string expectedCode) {
        string root = CreateProjectDirectory();
        try {
            var installedRecipe = new TestRecipe(
                CreateDescriptor(version: "1.0.0"),
                new XamlNexusRecipePlan { Changes = [] });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), installedRecipe);
            var requestedRecipe = new TestRecipe(
                CreateDescriptor(version: requestedVersion),
                new XamlNexusRecipePlan { Changes = [] });

            XamlNexusRecipeException exception = Assert.Throws<XamlNexusRecipeException>(() =>
                XamlNexusRecipeTransaction.Update(
                    XamlNexusProjectLocator.Locate(root),
                    requestedRecipe));

            Assert.Equal(expectedCode, exception.Code);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Update_AppliesExplicitStructuredMigrationOperations() {
        string root = CreateProjectDirectory();
        try {
            File.WriteAllText(Path.Combine(root, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            var originalRecipe = new TestRecipe(
                CreateDescriptor(version: "1.0.0"),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("owned.txt", "one")],
                });
            var updatedRecipe = new TestRecipe(
                CreateDescriptor(version: "1.1.0"),
                new XamlNexusRecipePlan {
                    Changes = [XamlNexusRecipeFileChange.CreateText("owned.txt", "two")],
                },
                [new AddPackageReferenceOperation("App.csproj", "Sample.Package", "1.0.0")]);
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), originalRecipe);

            XamlNexusRecipeTransaction.Update(
                XamlNexusProjectLocator.Locate(root),
                updatedRecipe);

            XDocument project = XDocument.Load(Path.Combine(root, "App.csproj"));
            Assert.Contains(
                project.Descendants(),
                element => element.Name.LocalName == "PackageReference" &&
                    (string?)element.Attribute("Include") == "Sample.Package");
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Apply_StaleContextDoesNotOverwriteInstalledModule() {
        string root = CreateProjectDirectory();
        try {
            var stale = XamlNexusProjectLocator.Locate(root);
            var first = new TestRecipe(CreateDescriptor(id: "first"), new() {
                Changes = [XamlNexusRecipeFileChange.CreateText("first.txt", "first")],
            });
            var second = new TestRecipe(CreateDescriptor(id: "second"), new() {
                Changes = [XamlNexusRecipeFileChange.CreateText("second.txt", "second")],
            });
            XamlNexusRecipeTransaction.Apply(stale, first);
            byte[] manifest = File.ReadAllBytes(stale.ManifestPath);
            var error = Assert.Throws<XamlNexusRecipeException>(() => XamlNexusRecipeTransaction.Apply(stale, second));
            Assert.Equal("XR1304", error.Code);
            Assert.Equal(manifest, File.ReadAllBytes(stale.ManifestPath));
            Assert.False(File.Exists(Path.Combine(root, "second.txt")));
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), second);
            var installed = XamlNexusProjectLocator.Locate(root).Manifest.Modules;
            Assert.Contains(installed, module => module.Id == "first");
            Assert.Contains(installed, module => module.Id == "second");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Apply_ConcurrentWritersCannotLoseModuleRecords() {
        string root = CreateProjectDirectory();
        try {
            var context = XamlNexusProjectLocator.Locate(root);
            using var ready = new Barrier(2);
            var tasks = new[] { "first", "second" }.Select(id => Task.Run(() => {
                var recipe = new TestRecipe(CreateDescriptor(id: id), new() {
                    Changes = [XamlNexusRecipeFileChange.CreateText(id + ".txt", id)],
                });
                if (!ready.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                return Record.Exception(() => XamlNexusRecipeTransaction.Apply(context, recipe));
            })).ToArray();
            var errors = await Task.WhenAll(tasks);
            Assert.Single(errors, error => error is null);
            var rejected = Assert.Single(errors, error => error is not null);
            Assert.True(rejected is InvalidOperationException || rejected is XamlNexusRecipeException { Code: "XR1304" });
            var installed = XamlNexusProjectLocator.Locate(root).Manifest.Modules;
            Assert.Single(installed, module => module.Source == "recipe");
            foreach (string id in new[] { "first", "second" })
                Assert.Equal(installed.Any(module => module.Id == id), File.Exists(Path.Combine(root, id + ".txt")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Apply_RejectsLinkedDirectoryWithoutChangingExternalFiles() {
        string root = CreateProjectDirectory();
        string external = CreateProjectDirectory();
        string link = Path.Combine(root, "Linked");
        try {
            TestDirectoryLink.Create(link, external);
            File.WriteAllText(Path.Combine(external, "owned.txt"), "original");
            byte[] manifest = File.ReadAllBytes(Path.Combine(root, "xamlnexus.json"));
            var recipe = new TestRecipe(CreateDescriptor(), new() {
                Changes = [XamlNexusRecipeFileChange.ReplaceText("Linked/owned.txt", "changed",
                    XamlNexusRecipeHash.Compute(Encoding.UTF8.GetBytes("original")))],
            });
            Assert.Throws<IOException>(() => XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe));
            Assert.Equal("original", File.ReadAllText(Path.Combine(external, "owned.txt")));
            Assert.Equal(manifest, File.ReadAllBytes(Path.Combine(root, "xamlnexus.json")));
            Assert.Throws<IOException>(() => XamlNexusProjectLocator.Locate(link));
        }
        finally {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(external, recursive: true);
        }
    }

    [Theory]
    [InlineData("<!-- user note -->", true)]
    [InlineData("<?user keep?>", true)]
    [InlineData("  ", false)]
    public void RemoveReferencePreservesNonWhitespaceGroupContent(string remaining, bool keepGroup) {
        string root = CreateProjectDirectory();
        try {
            string project = Path.Combine(root, "App.csproj");
            File.WriteAllText(project, $"<Project><ItemGroup>{remaining}<ProjectReference Include='Data.csproj'/></ItemGroup></Project>");
            var recipe = new TestRecipe(CreateDescriptor(), new XamlNexusRecipePlan {
                Changes = [],
                ProjectOperations = [new RemoveProjectReferenceOperation("App.csproj", "Data.csproj")],
            });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);
            var document = XDocument.Load(project);
            Assert.Empty(document.Descendants("ProjectReference"));
            Assert.Equal(keepGroup, document.Descendants("ItemGroup").Any());
            if (keepGroup) Assert.Contains(remaining, File.ReadAllText(project));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void RemoveSolutionProjectCleansOnlyItsFolderMapping(string newline) {
        string root = CreateProjectDirectory();
        try {
            const string removed = "{AAAAAAAA-1111-1111-1111-111111111111}";
            const string kept = "{BBBBBBBB-2222-2222-2222-222222222222}";
            const string folder = "{CCCCCCCC-3333-3333-3333-333333333333}";
            string mapping = $"\t\t{kept} = {folder}";
            string text = string.Join(newline, new[] {
                $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"Data\", \"Data.csproj\", \"{removed}\"",
                "EndProject",
                $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"User\", \"User.csproj\", \"{kept}\"",
                "EndProject",
                $"Project(\"{{66A26720-8FB5-11D2-AA7E-00C04F688DDE}}\") = \"Modules\", \"Modules\", \"{folder}\"",
                "EndProject", "Global",
                "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
                "\t\tDebug|Any CPU = Debug|Any CPU",
                "\tEndGlobalSection",
                "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution",
                $"\t\t{removed.ToLowerInvariant()}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                $"\t\t{kept}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                "\tEndGlobalSection", "\tGlobalSection(NestedProjects) = preSolution",
                $"\t\t{removed.ToLowerInvariant()} = {folder}", mapping,
                "\tEndGlobalSection", "EndGlobal", "",
            });
            string solution = Path.Combine(root, "App.sln");
            File.WriteAllText(solution, text);
            var recipe = new TestRecipe(CreateDescriptor(), new XamlNexusRecipePlan {
                Changes = [],
                ProjectOperations = [new RemoveProjectFromSolutionOperation("App.sln", "Data.csproj")],
            });
            XamlNexusRecipeTransaction.Apply(XamlNexusProjectLocator.Locate(root), recipe);
            string actual = File.ReadAllText(solution);
            Assert.DoesNotContain(removed, actual, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(text.Replace(text[..(text.IndexOf("EndProject", StringComparison.Ordinal) + "EndProject".Length)] + newline, "")
                .Replace($"\t\t{removed.ToLowerInvariant()}.Debug|Any CPU.ActiveCfg = Debug|Any CPU" + newline, "")
                .Replace($"\t\t{removed.ToLowerInvariant()} = {folder}" + newline, ""), actual);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static XamlNexusRecipeDescriptor CreateDescriptor(
        string id = "sample-recipe",
        string version = "1.2.3",
        IReadOnlyList<string>? supportedPresets = null,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<string>? conflicts = null) => new() {
        Id = id,
        Version = version,
        DisplayName = "Sample Recipe",
        SupportedPresets = supportedPresets ?? ["winui", "hybrid"],
        Dependencies = dependencies ?? [],
        Conflicts = conflicts ?? [],
    };

    private static XamlNexusProjectManifest CreateManifest(
        IReadOnlyList<XamlNexusManagedModule>? modules = null) => new() {
        GeneratorVersion = "1.0.3",
        Project = new XamlNexusProjectIdentity {
            Name = "RecipeTestApp",
            Preset = "winui",
            Language = "en-US",
            SolutionFormat = "sln",
        },
        Modules = modules ?? [CreateModule("settings", "template")],
    };

    private static XamlNexusManagedModule CreateModule(string id, string source) => new() {
        Id = id,
        Version = "1.0.3",
        Source = source,
    };

    private static string CreateProjectDirectory() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "xamlnexus-recipe-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        XamlNexusProjectManifestStore.Save(
            Path.Combine(root, "xamlnexus.json"),
            CreateManifest());
        return root;
    }

    private static string CreateSolutionText() => """
        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        Global
        	GlobalSection(SolutionConfigurationPlatforms) = preSolution
        		Debug|Any CPU = Debug|Any CPU
        		Release|Any CPU = Release|Any CPU
        	EndGlobalSection
        	GlobalSection(ProjectConfigurationPlatforms) = postSolution
        	EndGlobalSection
        EndGlobal
        """;

    private sealed class TestRecipe(
        XamlNexusRecipeDescriptor descriptor,
        XamlNexusRecipePlan plan,
        IReadOnlyList<XamlNexusRecipeProjectOperation>? updateOperations = null) :
        IXamlNexusRecipe,
        IXamlNexusRecipeUpdatePlanProvider {
        public XamlNexusRecipeDescriptor Descriptor { get; } = descriptor;

        public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) => plan;

        public IReadOnlyList<XamlNexusRecipeProjectOperation> CreateUpdateOperations(
            XamlNexusRecipeContext context,
            string installedVersion) => updateOperations ?? [];
    }
}
