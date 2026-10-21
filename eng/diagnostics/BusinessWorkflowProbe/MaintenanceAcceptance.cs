using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BusinessWorkflow.Data.Persistence;
using XamlNexus.Common.Projects;

internal static class MaintenanceAcceptance {
    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // Controlled target revisions exercise the real upgrade API. This is not a historical release migration.
    public static async Task RunAsync(string root, string databasePath, string targetRoot) {
        var current = XamlNexusProjectLocator.Locate(root);
        var target = XamlNexusProjectLocator.Locate(targetRoot);
        string main = "BusinessWorkflow.UI/MainWindow.xaml.cs";
        string panel = "BusinessWorkflow.MainPanel/BusinessWorkflow.MainPanel.csproj";
        string targetMainPath = Path.Combine(targetRoot, main);
        string targetPanelPath = Path.Combine(targetRoot, panel);
        Check(File.ReadAllText(Path.Combine(root, main)).Contains("_navigationMenu.Select(\"Orders\");"),
            "The sample default route customization is missing.");
        Check(File.ReadAllText(Path.Combine(root, panel)).Contains("BusinessWorkflow.Data.csproj"),
            "The SQLite Recipe project reference is missing.");

        var businessFiles = Directory.EnumerateFiles(Path.Combine(root, "BusinessWorkflow.MainPanel"), "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Append(Path.Combine(root, "BusinessWorkflow.Data/Persistence/OrderStore.cs"))
            .Append(Path.Combine(root, "BusinessWorkflow.UI/Modules/BusinessModule.cs"))
            .ToDictionary(path => Path.GetFullPath(path), Hash);
        string baselineMain = File.ReadAllText(targetMainPath);
        string baselinePanel = File.ReadAllText(targetPanelPath);
        File.WriteAllText(targetMainPath, baselineMain + "\n// Controlled target scaffold revision for acceptance.\n");
        File.WriteAllText(targetPanelPath, File.ReadAllText(targetPanelPath).Replace("</Project>",
            "<PropertyGroup Label=\"BusinessAcceptance\"><BusinessAcceptanceRevision>2</BusinessAcceptanceRevision></PropertyGroup>\n</Project>"));
        UpdateTargetBaseline(target, main, panel);
        target = XamlNexusProjectLocator.Locate(targetRoot);

        // Repeated unnamed ItemGroups remain ambiguous when both sides restructure children.
        // Check that this boundary refuses the entire plan instead of silently choosing a node.
        var ambiguousPlan = XamlNexusProjectUpgrade.CreatePlan(current, target);
        Check(!ambiguousPlan.CanApply && ambiguousPlan.Conflicts.Any(conflict => conflict.Code == "XU2011" && conflict.RelativePath == panel),
            "Ambiguous project groups were not retained for review.");
        string beforeAmbiguousMain = Hash(Path.Combine(root, main));
        string beforeAmbiguousManifest = Hash(current.ManifestPath);
        bool ambiguousRejected = false;
        try { XamlNexusProjectUpgrade.Apply(current, target, ambiguousPlan); }
        catch (XamlNexusProjectUpgradeException) { ambiguousRejected = true; }
        Check(ambiguousRejected && Hash(Path.Combine(root, main)) == beforeAmbiguousMain &&
            Hash(current.ManifestPath) == beforeAmbiguousManifest, "Ambiguous XML changed the application.");
        // Use a supported, unambiguous target metadata change for the successful merge.
        File.WriteAllText(targetPanelPath, baselinePanel.Replace("<Project Sdk=", "<Project ToolsVersion=\"Current\" Sdk="));
        UpdateTargetBaseline(target, panel);
        target = XamlNexusProjectLocator.Locate(targetRoot);

        var plan = XamlNexusProjectUpgrade.CreatePlan(current, target);
        Check(plan.CanApply, "Independent template changes conflicted: " + JsonSerializer.Serialize(plan.Conflicts));
        Check(plan.Changes.Any(change => change.RelativePath == main && change.Strategy == XamlNexusUpgradeChangeStrategy.TextMerge),
            "The real text merge was not exercised.");
        Check(plan.Changes.Any(change => change.RelativePath == panel && change.Strategy == XamlNexusUpgradeChangeStrategy.XmlMerge),
            "The real project-structure merge was not exercised.");
        XamlNexusProjectUpgrade.Apply(current, target, plan);

        string mergedMain = File.ReadAllText(Path.Combine(root, main));
        Check(mergedMain.Contains("_navigationMenu.Select(\"Orders\");") && mergedMain.Contains("Controlled target scaffold revision"),
            "Upgrade lost the local route or target scaffold change.");
        string mergedPanel = File.ReadAllText(Path.Combine(root, panel));
        Check(mergedPanel.Contains("BusinessWorkflow.Data.csproj") && mergedPanel.Contains("ToolsVersion=\"Current\""),
            "Upgrade lost the Recipe reference or target project metadata.");
        foreach (var file in businessFiles.Where(file => file.Key != Path.GetFullPath(Path.Combine(root, panel))))
            Check(Hash(file.Key) == file.Value, "Upgrade changed business code: " + file.Key);
        current = XamlNexusProjectLocator.Locate(root);
        Check(current.Manifest.Modules.Any(module => module.Id == "sqlite" && module.Source == "recipe"), "Upgrade lost Recipe ownership.");
        await CheckDatabaseAsync(databasePath);

        // The next target wants the same default-route line that the application customized.
        File.WriteAllText(targetMainPath, File.ReadAllText(targetMainPath).Replace(
            "_navigationMenu.Select(\"home\");", "_navigationMenu.Select(\"OrderDetails\");"));
        UpdateTargetBaseline(target, main);
        target = XamlNexusProjectLocator.Locate(targetRoot);
        var conflictingPlan = XamlNexusProjectUpgrade.CreatePlan(current, target);
        Check(!conflictingPlan.CanApply && conflictingPlan.Conflicts.Any(conflict => conflict.Code == "XU2011" && conflict.RelativePath == main),
            "Overlapping default-route changes were not reported as a conflict.");
        string beforeMain = Hash(Path.Combine(root, main));
        string beforeManifest = Hash(current.ManifestPath);
        bool rejected = false;
        try { XamlNexusProjectUpgrade.Apply(current, target, conflictingPlan); }
        catch (XamlNexusProjectUpgradeException) { rejected = true; }
        Check(rejected && beforeMain == Hash(Path.Combine(root, main)) && beforeManifest == Hash(current.ManifestPath),
            "A rejected conflict changed the source or manifest.");
        await CheckDatabaseAsync(databasePath);
        Console.WriteLine(JsonSerializer.Serialize(new { stage = "upgrade", passed = true,
            checks = new[] { "text-merge-preserves-user-route", "xml-merge-preserves-sqlite-reference", "business-files-preserved", "recipe-ownership-preserved", "data-preserved", "overlap-conflict-no-write", "ambiguous-xml-requires-review" },
            boundary = "Controlled target revisions use the actual upgrade API; released-version migration is not claimed." }));
    }

    private static void UpdateTargetBaseline(XamlNexusProjectContext target, params string[] changedPaths) {
        var changed = changedPaths.ToHashSet(StringComparer.Ordinal);
        var manifest = target.Manifest;
        XamlNexusProjectManifestStore.Save(target.ManifestPath, new() {
            SchemaVersion = manifest.SchemaVersion,
            GeneratorVersion = manifest.GeneratorVersion,
            Project = manifest.Project,
            Modules = manifest.Modules,
            ScaffoldFiles = manifest.ScaffoldFiles!.Select(file => changed.Contains(file.Path) ? new XamlNexusManagedFile {
                Path = file.Path, UserEditable = file.UserEditable,
                Sha256 = Hash(Path.Combine(target.RootDirectory, file.Path)),
                BaselineContentGzipBase64 = XamlNexusBaselineContent.Encode(File.ReadAllBytes(Path.Combine(target.RootDirectory, file.Path))),
            } : file).ToArray(),
        });
    }

    private static async Task CheckDatabaseAsync(string databasePath) {
        using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(databasePath + ".evidence.json"));
        string id = evidence.RootElement.GetProperty("Id").GetString()!;
        using var services = new ServiceCollection().AddXamlNexusSqlite(databasePath).BuildServiceProvider();
        var store = new OrderStore(services.GetRequiredService<IDbContextFactory<AppDbContext>>());
        Check((await store.GetAsync(id))?.Name == "Edited order", "Maintenance changed the saved order.");
    }
}
