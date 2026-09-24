using XamlNexus.Tooling.Development;
using XamlNexus.Tooling.Diagnostics;
using System.Text;
using XamlNexus.Tooling.CommandLine;
using XamlNexus.Common.Projects;
using Xunit;
using XamlNexus.Common.Recipes;
using XamlNexus.Recipes.BuiltIn;

namespace XamlNexus.TemplateTests;

public sealed class PageGeneratorTests : IDisposable {
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "page-tests", Guid.NewGuid().ToString("N"));
    public PageGeneratorTests() {
        Write("PageTest.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\r\nGlobal\r\nEndGlobal\r\n");
        Write("PageTest.UIComponent/Navigation/INavigationRegistry.cs", "public interface INavigationRegistry {}");
        Write("Directory.Build.props", "<Project />");
        Write("PageTest.UI/PageTest.UI.csproj", "<Project />");
        Write("PageTest.Common/PageTest.Common.csproj", "<Project />");
        Write("PageTest.MainPanel/PageTest.MainPanel.csproj", "<Project />");
        Write("PageTest.UI/MainWindow.xaml", "<Root xmlns:navi=\"test\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><navi:ArcNavigationView.MenuItems></navi:ArcNavigationView.MenuItems></Root>");
        Write("PageTest.UI/MainWindow.xaml.cs", "\"Nav_MainPage\" => typeof(MainPage),");
        XamlNexusProjectManifestStore.Save(Path.Combine(root, "xamlnexus.json"), new XamlNexusProjectManifest {
            GeneratorVersion = "1.0.3",
            Project = new XamlNexusProjectIdentity { Name = "PageTest", Preset = "winui", Language = "en-US", SolutionFormat = "sln" },
            Modules = [],
        });
    }
    private void Write(string path, string content) {
        string full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(true));
    }
    private string Read(string path) => File.ReadAllText(Path.Combine(root, path));
    private XamlNexusProjectContext Project => XamlNexusProjectLocator.Locate(root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListPageUsesTransactionalGeneration(bool skipNavigation) {
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order().ToArray();
        Assert.Equal(skipNavigation ? 4 : 5, PageGenerator.Add(Project, "Orders", true, skipNavigation, "list").Count);
        Assert.Equal(before, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order().ToArray());
        PageGenerator.Add(Project, "Orders", skipNavigation: skipNavigation, kind: "list");
        Assert.Contains("IOrdersDataSource", Read("PageTest.MainPanel/Services/OrdersDataSource.cs"));
        Assert.Contains("ViewModel.SearchText", Read("PageTest.MainPanel/OrdersPage.xaml"));
        Assert.Contains("OnPreLeaveAsync", Read("PageTest.MainPanel/OrdersPage.xaml.cs"));
        Assert.DoesNotContain("__APP__", Read("PageTest.MainPanel/ViewModels/OrdersViewModel.cs"));
        Assert.Empty(Project.Manifest.Modules);
    }

    [Fact]
    public void ListServiceCollisionPreventsAllPageWrites() {
        Write("PageTest.MainPanel/Services/OrdersDataSource.cs", "user service");
        Assert.ThrowsAny<Exception>(() => PageGenerator.Add(Project, "Orders", kind: "list"));
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
        Assert.Equal("user service", Read("PageTest.MainPanel/Services/OrdersDataSource.cs"));
    }

    [Fact]
    public void PageKindIsValidated() {
        var parsed = CliParser.Parse(["page", "add", "Orders", "--kind", "list"], root);
        Assert.True(parsed.Success);
        Assert.Equal("list", parsed.Options!.PageKind);
        Assert.True(CliParser.Parse(["page", "add", "Orders", "--kind", "details"], root).Success);
        Assert.True(CliParser.Parse(["page", "add", "Orders", "--kind", "form"], root).Success);
        Assert.False(CliParser.Parse(["page", "add", "Orders", "--kind"], root).Success);
        Assert.False(CliParser.Parse(["page", "add", "Orders", "--kind", "list", "--kind", "blank"], root).Success);
        Assert.False(CliParser.Parse(["add", "sqlite", "--kind", "list"], root).Success);
        Assert.Throws<ArgumentException>(() => PageGenerator.Add(Project, "Orders", kind: "unknown"));
    }

    [Theory]
    [InlineData("details", "GetAsync", "TryGet<string>(\"id\"")]
    [InlineData("form", "SaveAsync", "HasUnsavedChanges")]
    public void BusinessPageKindsGenerateRunnableContracts(string kind, string serviceMember, string pageMember) {
        IReadOnlyList<string> preview = PageGenerator.Add(Project, "Orders", dryRun: true, kind: kind);
        Assert.Equal(5, preview.Count);
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));

        PageGenerator.Add(Project, "Orders", kind: kind);
        Assert.Contains(serviceMember, Read("PageTest.MainPanel/Services/OrdersDataSource.cs"));
        Assert.Contains(pageMember, Read("PageTest.MainPanel/OrdersPage.xaml.cs") + Read("PageTest.MainPanel/ViewModels/OrdersViewModel.cs"));
        Assert.Contains("OrdersNavigation", string.Join("\n", Directory.GetFiles(Path.Combine(root, "PageTest.UI/Navigation")).Select(Path.GetFileName)));
    }

    [Theory]
    [InlineData("Directory.Build.props", "<Project><PropertyGroup><Version>2.0.0</Version></PropertyGroup></Project>")]
    [InlineData("eng/publishing/release.json", "{\"custom\":true}")]
    [InlineData("eng/custom.ps1", "Write-Output 'customized'")]
    public void InfrastructureCustomizationAllowsRunPagesAndBatchAddition(string relative, string customized) {
        Write(relative, "original");
        var original = Project;
        string path = Path.Combine(root, relative);
        string hash = XamlNexusRecipeHash.ComputeFile(path);
        XamlNexusProjectManifestStore.Save(original.ManifestPath, new() {
            GeneratorVersion = original.Manifest.GeneratorVersion, Project = original.Manifest.Project,
            Modules = original.Manifest.Modules,
            ScaffoldFiles = [new() { Path = relative, Sha256 = hash,
                BaselineContentGzipBase64 = XamlNexusBaselineContent.Encode(File.ReadAllBytes(path)), UserEditable = false }],
        });
        Write(relative, customized);
        var context = Project;
        var validation = XamlNexusProjectValidator.Validate(context);
        Assert.True(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Code == "XN1302" && issue.Severity == ProjectValidationSeverity.Warning);
        Assert.EndsWith("PageTest.UI.csproj", DevelopmentRunner.CreatePlan(context).ProjectPath);
        PageGenerator.Add(context, "Orders");
        var plan = XamlNexusRecipeTransaction.PrepareApplyBatch(Project, [new EditorConfigRecipe()]);
        XamlNexusRecipeTransaction.ApplyBatch(Project, plan);
        Assert.Equal(customized, Read(relative));
        Assert.Equal(hash, Assert.Single(Project.Manifest.ScaffoldFiles!).Sha256);
        Assert.True(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
        Assert.Contains(Project.Manifest.Modules, module => module.Id == "editorconfig");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecipeCustomizationAllowsDevelopmentButMissingFilesStillBlock(bool missing) {
        var recipe = new EditorConfigRecipe();
        XamlNexusRecipeTransaction.Apply(Project, recipe);
        var context = Project;
        byte[] baselineManifest = File.ReadAllBytes(context.ManifestPath);
        string path = Path.Combine(root, ".editorconfig");
        if (missing) File.Delete(path);
        else File.AppendAllText(path, "\n# user customization\nindent_size = 2\n");
        var report = XamlNexusProjectValidator.Validate(context);
        var issue = Assert.Single(report.Issues);
        Assert.Equal(missing ? "XN1201" : "XN1202", issue.Code);
        Assert.Equal(missing ? ProjectValidationSeverity.Error : ProjectValidationSeverity.Warning, issue.Severity);
        Assert.Equal(!missing, report.IsValid);
        if (missing) {
            Assert.Throws<InvalidOperationException>(() => DevelopmentRunner.CreatePlan(context));
            Assert.Throws<InvalidOperationException>(() => PageGenerator.Add(context, "Orders"));
        }
        else {
            Assert.EndsWith("PageTest.UI.csproj", DevelopmentRunner.CreatePlan(context).ProjectPath);
            Assert.Equal(4, PageGenerator.Add(context, "Orders", dryRun: true).Count);
            Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
            PageGenerator.Add(context, "Orders");
            Assert.True(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
            var doctor = XamlNexusDoctor.Diagnose(Project, BuiltInRecipeCatalog.Create(), probeEnvironment: false);
            Assert.Contains(doctor.Checks, check => check.Code == "XN1202" && check.Severity == XamlNexusDoctorSeverity.Warning);
            byte[] edited = File.ReadAllBytes(path);
            var removal = Assert.Throws<XamlNexusRecipeException>(() => XamlNexusRecipeTransaction.Remove(Project, recipe));
            Assert.Equal("XR1211", removal.Code);
            Assert.Equal(edited, File.ReadAllBytes(path));
        }
        Assert.Equal(baselineManifest, File.ReadAllBytes(context.ManifestPath));
    }

    [Fact]
    public void Parser_RecognizesPageOptions() {
        var result = CliParser.Parse(["page", "add", "Orders", "--dry-run", "--json"], root);
        Assert.True(result.Success);
        Assert.Equal(CliCommand.PageAdd, result.Options!.Command);
        Assert.Equal("Orders", result.Options.PageName);
        Assert.True(result.Options.DryRun);
        Assert.True(result.Options.JsonOutput);
    }
    [Fact]
    public void Preview_DoesNotWrite() {
        string before = Read("PageTest.UI/MainWindow.xaml");
        Assert.Equal(4, PageGenerator.Add(Project, "Orders", true).Count);
        Assert.Equal(before, Read("PageTest.UI/MainWindow.xaml"));
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
    }
    [Fact]
    public void AddTwoPages_PreservesNavigationAndUserOwnership() {
        PageGenerator.Add(Project, "Orders");
        PageGenerator.Add(Project, "Customers");
        Assert.Contains("NavigationRegistry.Default.Register", Read("PageTest.UI/Navigation/OrdersNavigation.cs"));
        Assert.Contains("CustomersPage", Read("PageTest.UI/Navigation/CustomersNavigation.cs"));
        Assert.DoesNotContain("Orders", Read("PageTest.UI/MainWindow.xaml"));
        Assert.DoesNotContain("Customers", Read("PageTest.UI/MainWindow.xaml.cs"));
        Assert.Contains("OrdersViewModel", Read("PageTest.MainPanel/OrdersPage.xaml.cs"));
        Assert.Empty(Project.Manifest.Modules);
    }
    [Theory]
    [InlineData("../Bad")]
    [InlineData("class")]
    [InlineData("Bad-Name")]
    public void InvalidName_IsRejected(string name) {
        Assert.Throws<ArgumentException>(() => PageGenerator.Add(Project, name));
    }
    [Fact]
    public void Collision_PreservesExistingFiles() {
        Write("PageTest.MainPanel/OrdersPage.xaml", "user work");
        string navigation = Read("PageTest.UI/MainWindow.xaml");
        Assert.ThrowsAny<Exception>(() => PageGenerator.Add(Project, "Orders"));
        Assert.Equal("user work", Read("PageTest.MainPanel/OrdersPage.xaml"));
        Assert.Equal(navigation, Read("PageTest.UI/MainWindow.xaml"));
    }
    [Fact]
    public void CustomNavigation_RegistersWithoutEditingShell() {
        Write("PageTest.UI/MainWindow.xaml.cs", "custom navigation");
        string xaml = Read("PageTest.UI/MainWindow.xaml");
        Assert.Equal(4, PageGenerator.Add(Project, "Orders").Count);
        Assert.True(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
        Assert.Equal("custom navigation", Read("PageTest.UI/MainWindow.xaml.cs"));
        Assert.Equal(xaml, Read("PageTest.UI/MainWindow.xaml"));
    }
    [Fact]
    public void Hybrid_UsesSameFrontendGeneration() {
        Write("PageTest/PageTest.csproj", "<Project />");
        var project = Project;
        var identity = project.Manifest.Project;
        var manifest = new XamlNexusProjectManifest {
            GeneratorVersion = "1.0.3",
            Project = new XamlNexusProjectIdentity { Name = identity.Name, Preset = "hybrid", Language = "en-US", SolutionFormat = "sln" },
            Modules = [],
        };
        XamlNexusProjectManifestStore.Save(project.ManifestPath, manifest);
        Assert.Equal(4, PageGenerator.Add(Project, "Orders").Count);
        Assert.Contains("global::PageTest.MainPanel.OrdersPage", Read("PageTest.UI/Navigation/OrdersNavigation.cs"));
    }
    [Fact]
    public void SkipNavigation_DoesNotReadMissingNavigationFiles() {
        File.Delete(Path.Combine(root, "PageTest.UI/MainWindow.xaml"));
        File.Delete(Path.Combine(root, "PageTest.UI/MainWindow.xaml.cs"));
        Assert.Equal(3, PageGenerator.Add(Project, "Orders", skipNavigation: true).Count);
        Assert.False(File.Exists(Path.Combine(root, "PageTest.UI/MainWindow.xaml")));
    }
    [Fact]
    public void MissingShell_StillRegistersNavigation() {
        File.Delete(Path.Combine(root, "PageTest.UI/MainWindow.xaml.cs"));
        Assert.Equal(4, PageGenerator.Add(Project, "Orders").Count);
    }
    [Fact]
    public void ReplacedShell_StillRegistersNavigation() {
        var project = Project;
        XamlNexusProjectManifestStore.Save(project.ManifestPath, new XamlNexusProjectManifest {
            GeneratorVersion = project.Manifest.GeneratorVersion,
            Project = project.Manifest.Project,
            Modules = [new XamlNexusManagedModule { Id = "app-shell", Version = "1.0.3", Source = "template" }],
        });
        File.Delete(Path.Combine(root, "PageTest.UI/MainWindow.xaml"));
        Assert.Equal(4, PageGenerator.Add(Project, "Orders").Count);
    }
    [Fact]
    public void CustomNavigationPreview_DoesNotWrite() {
        Write("PageTest.UI/MainWindow.xaml", "<CustomNavigation />");
        Assert.Equal(4, PageGenerator.Add(Project, "Orders", dryRun: true).Count);
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
    }
    [Fact]
    public void Parser_RestrictsNoNavigationToPages() {
        var parsed = CliParser.Parse(["page", "add", "Orders", "--no-navigation"], root);
        Assert.True(parsed.Success);
        Assert.True(parsed.Options!.SkipNavigation);
        Assert.False(CliParser.Parse(["page", "add", "Orders", "--no-navigation", "--no-navigation"], root).Success);
        Assert.False(CliParser.Parse(["add", "sqlite", "--no-navigation"], root).Success);
    }
    [Fact]
    public void MissingMainPanel_IsRejected() {
        File.Delete(Path.Combine(root, "PageTest.MainPanel/PageTest.MainPanel.csproj"));
        Assert.Throws<InvalidOperationException>(() => PageGenerator.Add(Project, "Orders"));
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
    }
    [Fact]
    public void Parser_RejectsUnknownPageOperation() {
        Assert.False(CliParser.Parse(["page", "remove", "Orders"], root).Success);
    }
    [Fact]
    public void ManifestWriteFailure_RollsBackFiles() {
        string original = Read("PageTest.UI/MainWindow.xaml");
        using var held = new FileStream(Path.Combine(root, "xamlnexus.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.ThrowsAny<Exception>(() => PageGenerator.Add(Project, "Orders"));
        Assert.Equal(original, Read("PageTest.UI/MainWindow.xaml"));
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
    }
    [Fact]
    public void LegacyProject_WithoutContractFallsBackToPageOnly() {
        File.Delete(Path.Combine(root, "PageTest.UIComponent/Navigation/INavigationRegistry.cs"));
        Assert.Equal(3, PageGenerator.Add(Project, "Orders").Count);
        Assert.False(File.Exists(Path.Combine(root, "PageTest.UI/Navigation/OrdersNavigation.cs")));
    }
    [Fact]
    public void RegistrationCollision_DoesNotWriteAnyPageFiles() {
        Write("PageTest.UI/Navigation/OrdersNavigation.cs", "user registration");
        Assert.ThrowsAny<Exception>(() => PageGenerator.Add(Project, "Orders"));
        Assert.Equal("user registration", Read("PageTest.UI/Navigation/OrdersNavigation.cs"));
        Assert.False(File.Exists(Path.Combine(root, "PageTest.MainPanel/OrdersPage.xaml")));
    }
    [Fact]
    public void InjectionCapableProject_CreatesViewModelThroughFactory() {
        Write("PageTest.Common/Utils/DI/AppObjectFactory.cs", "factory contract");
        PageGenerator.Add(Project, "Orders");
        Assert.Contains("AppObjectFactory.Create<OrdersViewModel>()", Read("PageTest.MainPanel/OrdersPage.xaml.cs"));
    }
    [Fact]
    public void LegacyProject_PreservesParameterlessViewModelCreation() {
        PageGenerator.Add(Project, "Orders");
        Assert.Contains("ViewModel { get; } = new();", Read("PageTest.MainPanel/OrdersPage.xaml.cs"));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
