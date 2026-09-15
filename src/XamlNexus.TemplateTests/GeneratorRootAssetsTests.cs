using System.Text.Json;
using System.Xml.Linq;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using XamlNexus.Common.Utils;
using XamlNexus.Generator.Winui3App;
using XamlNexus.Generator.Winui3_Wpf_App;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class GeneratorRootAssetsTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TrayAndSettingsCanBeInstalledAndRemovedInEitherOrder(bool settingsFirst) {
        string parent = Directory.CreateTempSubdirectory("xamlnexus-tray-tests-").FullName;
        const string name = "TrayDemo";
        try {
            var generator = new PureGenerator(Path.Combine(FindRepositoryRoot(), "src", "Templates", "Winui3"));
            Assert.True(generator.Generate(new ProjectConfig {
                SlnName = name, Profile = "basic", OutputPath = parent,
                Framework = FrameworkType.Winui3, SlnType = SolutionType.Sln,
            }));
            string root = Path.Combine(parent, name);
            IXamlNexusRecipe first = settingsFirst ? new SettingsRecipe() : new SystemTrayRecipe();
            IXamlNexusRecipe second = settingsFirst ? new SystemTrayRecipe() : new SettingsRecipe();
            foreach (var recipe in new[] { first, second }) {
                var context = XamlNexusProjectLocator.Locate(root);
                var preview = XamlNexusRecipeTransaction.PreviewApply(context, recipe);
                var result = XamlNexusRecipeTransaction.Apply(context, recipe);
                Assert.Equal(preview.Changes.Select(change => change.RelativePath).Order(), result.ChangedFiles.Order());
                Assert.All(result.ChangedFiles, path => Assert.DoesNotContain('\\', path));
            }
            Assert.True(XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(root)).IsValid);
            // Updating tray must restore its package reference if project migration removed it.
            string uiProject = Path.Combine(root, name + ".UI/" + name + ".UI.csproj");
            var projectXml = XDocument.Load(uiProject);
            projectXml.Descendants("PackageReference")
                .Where(item => (string?)item.Attribute("Include") == "H.NotifyIcon.WinUI").Remove();
            projectXml.Save(uiProject);
            string manifestPath = Path.Combine(root, "xamlnexus.json");
            var manifestJson = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
            manifestJson["modules"]!.AsArray().Single(module => module!["id"]!.GetValue<string>() == "system-tray")!["version"] = "0.9.0";
            File.WriteAllText(manifestPath, manifestJson.ToJsonString());
            var updated = XamlNexusRecipeTransaction.Update(XamlNexusProjectLocator.Locate(root), new SystemTrayRecipe());
            Assert.Contains(name + ".UI/" + name + ".UI.csproj", updated.ChangedFiles);
            Assert.Contains(XDocument.Load(uiProject).Descendants("PackageReference"),
                item => (string?)item.Attribute("Include") == "H.NotifyIcon.WinUI");
            string settingsPath = Path.Combine(root, name + ".Models/Cores/Settings.cs");
            string settingsBeforeRemoval = File.ReadAllText(settingsPath);
            foreach (var recipe in new[] { first, second }) {
                XamlNexusRecipeTransaction.Remove(XamlNexusProjectLocator.Locate(root), recipe);
                Assert.True(XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(root)).IsValid);
                Assert.Equal(settingsBeforeRemoval, File.ReadAllText(settingsPath));
            }
            Assert.True(File.Exists(Path.Combine(root, name + ".Common/ISystemTraySettings.cs")));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Theory]
    [InlineData(false, SolutionType.Sln)]
    [InlineData(true, SolutionType.Sln)]
    [InlineData(false, SolutionType.Slnx)]
    [InlineData(true, SolutionType.Slnx)]
    public void BasicProfileCanAddAndRemoveStandardSettings(bool hybrid, SolutionType format) {
        string templates = Path.Combine(FindRepositoryRoot(), "src", "Templates");
        string parent = Directory.CreateTempSubdirectory("xamlnexus-basic-tests-").FullName;
        var config = new ProjectConfig {
            SlnName = "BasicDemo", Profile = "basic", OutputPath = parent,
            Framework = hybrid ? FrameworkType.Winui3_Wpf : FrameworkType.Winui3, SlnType = format,
        };
        BaseGenerator generator = hybrid ? new HybridGenerator(Path.Combine(templates, "Winui3_Wpf"))
            : new PureGenerator(Path.Combine(templates, "Winui3"));
        try {
            Assert.True(generator.Generate(config));
            string root = Path.Combine(parent, "BasicDemo");
            AssertReleaseSolution(root, "BasicDemo", format);
            AssertStartupProject(root, "BasicDemo", format, hybrid);
            AssertPanelGrouping(root, "BasicDemo", format, includesSettings: false);
            var context = XamlNexusProjectLocator.Locate(root);
            Assert.Equal("basic", context.Manifest.Project.Profile);
            Assert.DoesNotContain(context.Manifest.Modules, module => module.Id == "settings");
            Assert.False(Directory.Exists(Path.Combine(root, "BasicDemo.AppSettingsPanel")));
            Assert.DoesNotContain("AppSettingsPanel", File.ReadAllText(Path.Combine(root, "BasicDemo.UI/App.xaml.cs")));
            Assert.DoesNotContain("AppSettingsPanel", File.ReadAllText(Path.Combine(root, "BasicDemo.UI/BasicDemo.UI.csproj")));
            Assert.DoesNotContain("AppSettingsPanel", File.ReadAllText(Path.Combine(root, $"BasicDemo.{format.ToString().ToLowerInvariant()}")));
            Assert.True(XamlNexusProjectValidator.Validate(context).IsValid);

            var recipe = new SettingsRecipe();
            var preview = XamlNexusRecipeTransaction.PreviewApply(context, recipe);
            Assert.False(Directory.Exists(Path.Combine(root, "BasicDemo.AppSettingsPanel")));
            var result = XamlNexusRecipeTransaction.Apply(context, recipe);
            Assert.Contains("BasicDemo.UI/Modules/SettingsModule.cs", result.ChangedFiles);
            Assert.Contains("BasicDemo.AppSettingsPanel/Views/GeneralSetting.xaml", result.ChangedFiles);
            string module = File.ReadAllText(Path.Combine(root, "BasicDemo.UI/Modules/SettingsModule.cs"));
            Assert.Contains("typeof(AppSettings)", module);
            Assert.DoesNotContain("XamlNexus.AppSettingsPanel", module);
            context = XamlNexusProjectLocator.Locate(root);
            Assert.True(XamlNexusProjectValidator.Validate(context).IsValid);
            Assert.Equal("basic", context.Manifest.Project.Profile);
            Assert.Contains(context.Manifest.Modules, module => module.Id == "settings" && module.Source == "recipe");
            XamlNexusRecipeTransaction.Remove(context, recipe);
            Assert.False(File.Exists(Path.Combine(root, "BasicDemo.UI/Modules/SettingsModule.cs")));
            Assert.DoesNotContain("AppSettingsPanel", File.ReadAllText(Path.Combine(root, "BasicDemo.UI/BasicDemo.UI.csproj")));
            Assert.DoesNotContain("AppSettingsPanel", File.ReadAllText(Path.Combine(root, $"BasicDemo.{format.ToString().ToLowerInvariant()}")));
            Assert.True(XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(root)).IsValid);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generate_CopiesAndTokenizesReleaseAssets(bool hybrid) {
        var repositoryRoot = FindRepositoryRoot();
        var outputParent = Path.Combine(AppContext.BaseDirectory, "generator-test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputParent);
        const string appName = "ReleaseAssetDemo";
        var config = new ProjectConfig {
            SlnName = appName,
            OutputPath = outputParent,
            Framework = hybrid ? FrameworkType.Winui3_Wpf : FrameworkType.Winui3,
            SlnType = SolutionType.Sln,
        };
        BaseGenerator generator = hybrid
            ? new HybridGenerator(Path.Combine(repositoryRoot, "src", "Templates", "Winui3_Wpf"))
            : new PureGenerator(Path.Combine(repositoryRoot, "src", "Templates", "Winui3"));

        try {
            Assert.True(generator.Generate(config));
            var generatedRoot = Path.Combine(outputParent, appName);
            AssertReleaseSolution(generatedRoot, appName, config.SlnType);
            AssertPanelGrouping(generatedRoot, appName, config.SlnType, includesSettings: true);

            Assert.True(File.Exists(Path.Combine(generatedRoot, "Directory.Build.props")));
            Assert.True(Directory.Exists(Path.Combine(generatedRoot, ".github")));
            Assert.True(File.Exists(Path.Combine(generatedRoot, "eng", "publishing", "release.json")));
            Assert.True(File.Exists(Path.Combine(generatedRoot, "eng", "publishing", "Build-Installer.ps1")));
            Assert.False(File.Exists(Path.Combine(generatedRoot, "eng", "publishing", "New-UpdateManifest.ps1")));
            Assert.True(File.Exists(Path.Combine(generatedRoot, "RELEASING.md")));
            Assert.True(File.Exists(Path.Combine(generatedRoot, "RELEASING.zh-CN.md")));
            Assert.True(File.Exists(Path.Combine(generatedRoot, "xamlnexus.json")));

            using var releaseConfig = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(generatedRoot, "eng", "publishing", "release.json")));
            Assert.Equal(appName, releaseConfig.RootElement.GetProperty("appName").GetString());
            Assert.DoesNotContain(
                hybrid ? "Winui3_Wpf_XamlNexus" : "Winui3_XamlNexus",
                File.ReadAllText(Path.Combine(generatedRoot, "eng", "publishing", "Build-Installer.ps1")));

            XamlNexusProjectManifest manifest = XamlNexusProjectManifestStore.Load(
                Path.Combine(generatedRoot, "xamlnexus.json"));
            Assert.Equal(XamlNexusProjectManifest.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.Equal(appName, manifest.Project.Name);
            Assert.Equal(hybrid ? "hybrid" : "winui", manifest.Project.Preset);
            Assert.Equal("zh-CN", manifest.Project.Language);
            Assert.True(Version.TryParse(manifest.GeneratorVersion, out _));
            Assert.All(
                manifest.Modules,
                module => Assert.Equal(manifest.GeneratorVersion, module.Version));
            Assert.Contains(manifest.Modules, module => module.Id == "settings");
            Assert.Equal(hybrid, manifest.Modules.Any(module => module.Id == "updater"));
            Assert.Equal(hybrid, manifest.Modules.Any(module => module.Id == "named-pipe-grpc"));
            Assert.Equal(hybrid, manifest.Modules.Any(module => module.Id == "system-tray"));
            Assert.NotNull(manifest.ScaffoldFiles);
            Assert.NotEmpty(manifest.ScaffoldFiles!);
            Assert.Contains(
                manifest.ScaffoldFiles!,
                file => file.Path.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                manifest.ScaffoldFiles!,
                file => file.Path.Equals(
                    "eng/publishing/release.json",
                    StringComparison.OrdinalIgnoreCase));
            XamlNexusManagedFile projectBaseline = Assert.Single(
                manifest.ScaffoldFiles!,
                file => file.Path.Equals(
                    $"{appName}.UI/{appName}.UI.csproj",
                    StringComparison.OrdinalIgnoreCase));
            Assert.True(projectBaseline.UserEditable);
            Assert.NotNull(projectBaseline.BaselineContentGzipBase64);

            XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(
                Path.Combine(generatedRoot, $"{appName}.UI"));
            Assert.Equal(generatedRoot, context.RootDirectory);

            XamlNexusProjectValidationReport validReport = XamlNexusProjectValidator.Validate(context);
            Assert.True(
                validReport.IsValid,
                string.Join(Environment.NewLine, validReport.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Empty(validReport.Issues);
            if (hybrid) {
                // The tray lives in the host window, not the retired raw-pipe sender.
                string trayWindow = Path.Combine(generatedRoot, appName, "MainWindow.xaml");
                byte[] windowContent = File.ReadAllBytes(trayWindow);
                try {
                    File.Delete(trayWindow);
                    Assert.Contains(XamlNexusProjectValidator.Validate(context).Issues,
                        issue => issue.Code == "XN1117");
                }
                finally { File.WriteAllBytes(trayWindow, windowContent); }
            }
            XamlNexusDoctorReport initialDoctor = XamlNexusDoctor.Diagnose(
                context,
                BuiltInRecipeCatalog.Create(),
                probeEnvironment: false);
            Assert.True(
                initialDoctor.IsHealthy,
                string.Join(Environment.NewLine, initialDoctor.Checks
                    .Where(check => check.Severity == XamlNexusDoctorSeverity.Error)
                    .Select(check => $"{check.Code}: {check.Message}")));
            Assert.Contains(
                initialDoctor.Checks,
                check => check.Code == "XD3003" && check.Severity == XamlNexusDoctorSeverity.Pass);
            Assert.Contains(
                initialDoctor.Checks,
                check => check.Code == "XD6002" && check.Severity == XamlNexusDoctorSeverity.Pass);

            IXamlNexusRecipe editorConfig = BuiltInRecipeCatalog.Create().Find("editorconfig")!;
            XamlNexusRecipeTransaction.Apply(context, editorConfig);
            XamlNexusProjectContext recipeContext = XamlNexusProjectLocator.Locate(generatedRoot);
            XamlNexusManagedModule recipeModule = Assert.Single(
                recipeContext.Manifest.Modules,
                module => module.Id == "editorconfig");
            Assert.Equal(3, recipeModule.Files!.Count);
            XamlNexusManagedFile ownedFile = Assert.Single(recipeModule.Files, file => file.Path == ".editorconfig");
            Assert.Equal(".editorconfig", ownedFile.Path);
            Assert.True(File.Exists(Path.Combine(generatedRoot, ".editorconfig")));
            Assert.Empty(XamlNexusProjectValidator.Validate(recipeContext).Issues);

            IXamlNexusRecipe sqlite = BuiltInRecipeCatalog.Create().Find("sqlite")!;
            XamlNexusRecipeApplyResult sqliteResult = XamlNexusRecipeTransaction.Apply(recipeContext, sqlite);
            string dataProjectRelative = $"{appName}.Data/{appName}.Data.csproj";
            string dataProjectPath = Path.Combine(generatedRoot, $"{appName}.Data", $"{appName}.Data.csproj");
            Assert.True(File.Exists(dataProjectPath));
            Assert.Contains(dataProjectRelative, sqliteResult.ChangedFiles);
            string panelProjectPath = Path.Combine(generatedRoot, $"{appName}.MainPanel", $"{appName}.MainPanel.csproj");
            if (!hybrid)
                Assert.Contains($"{appName}.Data.csproj", File.ReadAllText(panelProjectPath));
            else
                Assert.DoesNotContain($"{appName}.Data.csproj", File.ReadAllText(panelProjectPath));

            string sqliteModulePath = hybrid
                ? Path.Combine(generatedRoot, appName, "Modules", "SqliteModule.cs")
                : Path.Combine(generatedRoot, $"{appName}.UI", "Modules", "SqliteModule.cs");
            Assert.True(File.Exists(sqliteModulePath));

            string hostProjectPath = hybrid
                ? Path.Combine(generatedRoot, appName, $"{appName}.csproj")
                : Path.Combine(generatedRoot, $"{appName}.UI", $"{appName}.UI.csproj");
            XDocument hostProject = XDocument.Load(hostProjectPath);
            XElement dependencyInjection = Assert.Single(
                hostProject.Descendants(),
                element =>
                    element.Name.LocalName == "PackageReference" &&
                    string.Equals(
                        (string?)element.Attribute("Include"),
                        "Microsoft.Extensions.DependencyInjection",
                        StringComparison.OrdinalIgnoreCase));
            Assert.Equal("8.0.1", (string?)dependencyInjection.Attribute("Version"));
            Assert.Contains(
                hostProject.Descendants().Where(element => element.Name.LocalName == "ProjectReference"),
                element => ((string?)element.Attribute("Include"))?.EndsWith(
                    $"{appName}.Data{Path.DirectorySeparatorChar}{appName}.Data.csproj",
                    StringComparison.OrdinalIgnoreCase) == true);
            if (hybrid) {
                XDocument winuiProject = XDocument.Load(Path.Combine(
                    generatedRoot,
                    $"{appName}.UI",
                    $"{appName}.UI.csproj"));
                Assert.DoesNotContain(
                    winuiProject.Descendants().Where(element => element.Name.LocalName == "ProjectReference"),
                    element => ((string?)element.Attribute("Include"))?.Contains(
                        $"{appName}.Data",
                        StringComparison.OrdinalIgnoreCase) == true);
                Assert.True(File.Exists(Path.Combine(
                    generatedRoot,
                    $"{appName}.Grpc.Service",
                    "Protos",
                    "app_state.proto")));
                Assert.True(File.Exists(Path.Combine(
                    generatedRoot,
                    $"{appName}.Grpc.Client",
                    "AppStateClient.cs")));
                Assert.True(File.Exists(Path.Combine(
                    generatedRoot,
                    $"{appName}.UI",
                    "Modules",
                    "SqliteClientModule.cs")));
                XDocument grpcProject = XDocument.Load(Path.Combine(
                    generatedRoot,
                    $"{appName}.Grpc.Service",
                    $"{appName}.Grpc.Service.csproj"));
                Assert.Contains(
                    grpcProject.Descendants(),
                    element => element.Name.LocalName == "Protobuf" &&
                        ((string?)element.Attribute("Include"))?.EndsWith(
                            $"Protos{Path.DirectorySeparatorChar}app_state.proto",
                            StringComparison.OrdinalIgnoreCase) == true);
            }
            Assert.Contains(
                $"{appName}.Data\\{appName}.Data.csproj",
                File.ReadAllText(Path.Combine(generatedRoot, $"{appName}.sln")));

            XamlNexusProjectContext sqliteContext = XamlNexusProjectLocator.Locate(generatedRoot);
            XamlNexusManagedModule sqliteModule = Assert.Single(
                sqliteContext.Manifest.Modules,
                module => module.Id == "sqlite");
            Assert.Equal(hybrid ? 14 : 9, sqliteModule.Files!.Count);
            Assert.Empty(XamlNexusProjectValidator.Validate(sqliteContext).Issues);
            XamlNexusDoctorReport sqliteDoctor = XamlNexusDoctor.Diagnose(
                sqliteContext,
                BuiltInRecipeCatalog.Create(),
                probeEnvironment: false);
            Assert.DoesNotContain(
                sqliteDoctor.Checks,
                check => check.Category == "SQLite" &&
                    check.Severity == XamlNexusDoctorSeverity.Error);
            Assert.Contains(sqliteDoctor.Checks, check => check.Code == "XD5002");
            Assert.Equal(
                hybrid,
                sqliteDoctor.Checks.Any(check => check.Code == "XD5003"));
            string solutionPath = Path.Combine(generatedRoot, $"{appName}.sln");
            string dataProjectLine = Assert.Single(
                File.ReadLines(solutionPath),
                line => line.StartsWith("Project(\"", StringComparison.Ordinal) &&
                    line.Contains(
                        $"{appName}.Data\\{appName}.Data.csproj",
                        StringComparison.OrdinalIgnoreCase));
            string dataProjectGuid = dataProjectLine.Split(',')[^1].Trim().Trim('"');

            XamlNexusRecipeTransaction.Remove(sqliteContext, sqlite);
            Assert.False(Directory.Exists(Path.Combine(generatedRoot, $"{appName}.Data")));
            XDocument hostProjectAfterRemove = XDocument.Load(hostProjectPath);
            Assert.DoesNotContain(
                hostProjectAfterRemove.Descendants(),
                element => element.Name.LocalName == "ProjectReference" &&
                    ((string?)element.Attribute("Include"))?.EndsWith(
                        $"{appName}.Data{Path.DirectorySeparatorChar}{appName}.Data.csproj",
                        StringComparison.OrdinalIgnoreCase) == true);
            Assert.DoesNotContain(
                $"{appName}.Data\\{appName}.Data.csproj",
                File.ReadAllText(solutionPath));
            Assert.DoesNotContain(dataProjectGuid, File.ReadAllText(solutionPath));
            Assert.Contains(
                hostProjectAfterRemove.Descendants(),
                element => element.Name.LocalName == "PackageReference" &&
                    string.Equals(
                        (string?)element.Attribute("Include"),
                        "Microsoft.Extensions.DependencyInjection",
                        StringComparison.OrdinalIgnoreCase));
            if (hybrid) {
                XDocument grpcProjectAfterRemove = XDocument.Load(Path.Combine(
                    generatedRoot,
                    $"{appName}.Grpc.Service",
                    $"{appName}.Grpc.Service.csproj"));
                Assert.DoesNotContain(
                    grpcProjectAfterRemove.Descendants(),
                    element => element.Name.LocalName == "Protobuf" &&
                        ((string?)element.Attribute("Include"))?.Contains(
                            "app_state.proto",
                            StringComparison.OrdinalIgnoreCase) == true);
            }
            XamlNexusProjectContext removedSqliteContext = XamlNexusProjectLocator.Locate(generatedRoot);
            Assert.DoesNotContain(removedSqliteContext.Manifest.Modules, module => module.Id == "sqlite");
            Assert.Empty(XamlNexusProjectValidator.Validate(removedSqliteContext).Issues);

            File.AppendAllText(Path.Combine(generatedRoot, ".editorconfig"), "# user change");
            XamlNexusProjectValidationReport modifiedRecipeReport =
                XamlNexusProjectValidator.Validate(recipeContext);
            Assert.Contains(modifiedRecipeReport.Issues, issue => issue.Code == "XN1202" && issue.Severity == ProjectValidationSeverity.Warning);

            if (hybrid) {
                File.Delete(Path.Combine(
                    generatedRoot,
                    $"{appName}.Common",
                    "Updates",
                    "AppUpdateLifecycle.cs"));
                XamlNexusProjectValidationReport invalidReport = XamlNexusProjectValidator.Validate(context);
                Assert.False(invalidReport.IsValid);
                Assert.Contains(invalidReport.Issues, issue => issue.Code == "XN1108");
            }
        }
        finally {
            // Windows may briefly retain handles after dotnet finishes creating projects.
            for (int attempt = 0; Directory.Exists(outputParent); attempt++) {
                try { Directory.Delete(outputParent, recursive: true); }
                catch (IOException) when (attempt < 5) { Thread.Sleep(100); }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generate_SlnxPlacesBothPanelsInFolder(bool hybrid) {
        string parent = Directory.CreateTempSubdirectory("xamlnexus-panel-tests-").FullName;
        string templates = Path.Combine(FindRepositoryRoot(), "src", "Templates");
        BaseGenerator generator = hybrid ? new HybridGenerator(Path.Combine(templates, "Winui3_Wpf"))
            : new PureGenerator(Path.Combine(templates, "Winui3"));
        try {
            Assert.True(generator.Generate(new ProjectConfig {
                SlnName = "PanelDemo", OutputPath = parent, SlnType = SolutionType.Slnx,
                Framework = hybrid ? FrameworkType.Winui3_Wpf : FrameworkType.Winui3,
            }));
            AssertPanelGrouping(Path.Combine(parent, "PanelDemo"), "PanelDemo", SolutionType.Slnx, includesSettings: true);
            AssertSlnxPlatforms(Path.Combine(parent, "PanelDemo"), "PanelDemo");
            AssertStartupProject(Path.Combine(parent, "PanelDemo"), "PanelDemo", SolutionType.Slnx, hybrid);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    private static void AssertPanelGrouping(string root, string appName, SolutionType format, bool includesSettings) {
        string solution = File.ReadAllText(Path.Combine(root, $"{appName}.{format.ToString().ToLowerInvariant()}"));
        string[] names = includesSettings ? [appName + ".MainPanel", appName + ".AppSettingsPanel"] : [appName + ".MainPanel"];
        if (format == SolutionType.Slnx) {
            var document = XDocument.Parse(solution);
            var folder = Assert.Single(document.Root!.Elements("Folder"), item => (string?)item.Attribute("Name") == "/Panels/");
            Assert.Equal(names.Length, folder.Elements("Project").Count());
            foreach (string name in names)
                Assert.Contains(folder.Elements("Project"), item =>
                    ((string?)item.Attribute("Path"))?.Replace('\\', '/') == $"{name}/{name}.csproj");
        }
        else {
            var headers = System.Text.RegularExpressions.Regex.Matches(solution,
                "(?m)^Project\\(\"[^\"]+\"\\) = \"(?<name>[^\"]+)\", \"[^\"]+\", \"(?<id>[^\"]+)\"");
            string Id(string name) => Assert.Single(headers.Cast<System.Text.RegularExpressions.Match>(),
                item => item.Groups["name"].Value == name).Groups["id"].Value;
            string folderId = Id("Panels");
            var section = System.Text.RegularExpressions.Regex.Match(solution,
                @"GlobalSection\(NestedProjects\)[^\r\n]*[\r\n]+(?<items>.*?)EndGlobalSection",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.True(section.Success, "Panel membership must be present in NestedProjects.");
            foreach (string name in names)
                Assert.Contains($"{Id(name)} = {folderId}", section.Groups["items"].Value);
        }
    }

    private static void AssertReleaseSolution(string root, string appName, SolutionType format) {
        if (format == SolutionType.Slnx) AssertSlnxPlatforms(root, appName);
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "eng/publishing/release.json")));
        string solution = config.RootElement.GetProperty("solution").GetString()!;
        Assert.Equal($"{appName}.{format.ToString().ToLowerInvariant()}", solution);
        Assert.True(File.Exists(Path.Combine(root, solution)));
        var listed = ShellExecutor.Run("dotnet", $"sln \"{solution}\" list", root);
        Assert.True(listed.Success, listed.DiagnosticOutput);
        Assert.Contains($"{appName}.UI.csproj", listed.StandardOutput);
        string workflowDirectory = Path.Combine(root, ".github", "workflows");
        string workflow = Assert.Single(Directory.GetFiles(workflowDirectory));
        Assert.Equal("validate-pull-request.yml", Path.GetFileName(workflow));
        string content = File.ReadAllText(workflow);
        Assert.Contains("pull_request:", content);
        Assert.Contains("dotnet restore", content);
        Assert.Contains("dotnet build", content);
        Assert.Contains("dotnet test", content);
        Assert.Contains("eng/publishing/release.json", content);
        Assert.DoesNotContain("Read-ReleaseMetadata", content);
        Assert.DoesNotContain("release:stable", content);
        Assert.False(File.Exists(Path.Combine(root, "eng/publishing/Read-ReleaseMetadata.ps1")));
        Assert.False(File.Exists(Path.Combine(root, ".github/release.json")));
        Assert.DoesNotContain("release-notes", File.ReadAllText(Path.Combine(root, ".github/pull_request_template.md")));
    }

    private static void AssertStartupProject(string root, string appName, SolutionType format, bool hybrid) {
        string name = appName + (hybrid ? "" : ".UI");
        string expected = $"{name}/{name}.csproj";
        string text = File.ReadAllText(Path.Combine(root, $"{appName}.{format.ToString().ToLowerInvariant()}"));
        if (format == SolutionType.Slnx) {
            var startup = Assert.Single(XDocument.Parse(text).Descendants("Project"),
                project => (string?)project.Attribute("DefaultStartup") == "true");
            Assert.Equal(expected, startup.Attribute("Path")!.Value.Replace('\\', '/'));
        }
        else {
            string firstProject = text.Split('\n').First(line => line.StartsWith("Project("));
            Assert.Contains($"\"{expected}\"", firstProject.Replace('\\', '/'));
        }
    }

    private static void AssertSlnxPlatforms(string root, string appName) {
        var solution = XDocument.Load(Path.Combine(root, appName + ".slnx")).Root!;
        string[] platforms = solution.Element("Configurations")!.Elements("Platform")
            .Select(element => element.Attribute("Name")!.Value).ToArray();
        Assert.Equal(new[] { "x64", "x86", "ARM64" }, platforms);
        foreach (var entry in solution.Descendants("Project")) {
            string path = entry.Attribute("Path")!.Value;
            var project = XDocument.Load(Path.Combine(root, path));
            string[] supported = (project.Descendants("Platforms").FirstOrDefault()?.Value ?? "AnyCPU").Split(';');
            string mapping = entry.Element("Platform")!.Attribute("Project")!.Value;
            foreach (string platform in platforms) {
                string mapped = mapping == "*" ? platform : mapping.Replace(" ", "");
                Assert.Contains(mapped, supported);
            }
        }
        // Exercise the SDK's SLNX reader as well as checking each project's supported platforms.
        var validation = ShellExecutor.Run("dotnet",
            $"msbuild \"{appName}.slnx\" -t:ValidateSolutionConfiguration -p:Configuration=Debug -p:Platform=x64 -v:minimal", root);
        Assert.True(validation.Success, validation.DiagnosticOutput);
    }

    private static string FindRepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Templates"))) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the XamlNexus repository root.");
    }

    private sealed class PureGenerator(string templateRoot) : Winui3Generator {
        protected override string TemplateRoot => templateRoot;
        protected override void OnError(ProjectConfig config, Exception ex) => throw ex;
    }

    private sealed class HybridGenerator(string templateRoot) : Winui3_WpfGenerator {
        protected override string TemplateRoot => templateRoot;
        protected override void OnError(ProjectConfig config, Exception ex) => throw ex;
    }
}
