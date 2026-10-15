using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BusinessWorkflow.Data.Persistence;
using BusinessWorkflow.MainPanel.Services;
using BusinessWorkflow.MainPanel.ViewModels;
using BusinessWorkflow.UIComponent.Activation;
using BusinessWorkflow.UIComponent.Navigation;
using BusinessWorkflow.UIComponent.Utils;

internal static class Program {
    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task<int> Main(string[] args) {
        try {
            if (args.Length < 3) throw new ArgumentException("Expected seed|reopen|upgrade, project root, database path.");
            if (args[0] == "upgrade") {
                if (args.Length != 4) throw new ArgumentException("Upgrade requires a clean target scaffold root.");
                await MaintenanceAcceptance.RunAsync(args[1], args[2], args[3]);
                return 0;
            }
            string databasePath = Path.GetFullPath(args[2]);
            string state = databasePath + ".evidence.json";
            using var services = new ServiceCollection().AddXamlNexusSqlite(databasePath).BuildServiceProvider();
            await services.GetRequiredService<SqliteDatabaseInitializer>().InitializeAsync();
            var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var store = new OrderStore(factory);
            var source = new OrderDataSource(store);
            if (args[0] == "seed") {
                var form = new OrderEditorViewModel(source);
                await form.ActivateAsync(null);
                form.Name = "Original order";
                form.Description = "Created through the generated form";
                Check(await form.TrySaveAsync(), "The generated form failed to create an order.");
                var original = (await store.ListAsync()).Single();
                Check(!form.HasUnsavedChanges, "Successful save did not clear dirty state.");

                var list = new OrdersViewModel(source);
                await list.ActivateAsync();
                Check(list.Items.Single().Id == original.Id, "List did not preserve the saved record ID.");
                var detail = new OrderDetailsViewModel(source);
                await detail.LoadAsync(original.Id);
                Check(detail.HasItem && detail.Title == original.Name, "Details did not load the saved record.");

                await form.ActivateAsync(original.Id);
                form.Name = "Edited order";
                form.Description = "Persist after a separate process reopens the database";
                Check(await form.TrySaveAsync(), "Editing through the generated form failed.");
                await list.RefreshAsync();
                Check(list.Items.Single().Title == "Edited order", "List refresh did not show the edit.");

                // A real SQLite write failure, not an in-memory substitute for the business store.
                await using (var database = await factory.CreateDbContextAsync()) {
                    await database.Database.ExecuteSqlRawAsync(
                        "CREATE TRIGGER reject_order_update BEFORE UPDATE ON AppState WHEN NEW.Key LIKE 'orders/%' BEGIN SELECT RAISE(ABORT, 'Acceptance write rejection'); END;");
                }
                form.Name = "Rejected change";
                Check(!await form.TrySaveAsync() && form.HasError && form.HasUnsavedChanges,
                    "A failed save must retain the unsaved form.");
                Check((await store.GetAsync(original.Id))!.Name == "Edited order", "Failed save changed durable data.");
                await using (var database = await factory.CreateDbContextAsync())
                    await database.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_order_update;");

                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                try {
                    await store.SaveAsync(original.Id, "Cancelled", "", cancelled.Token);
                    throw new InvalidOperationException("Cancelled write was accepted.");
                }
                catch (OperationCanceledException) { }
                Check((await store.GetAsync(original.Id))!.Name == "Edited order", "Cancelled write changed durable data.");

                await File.WriteAllTextAsync(state, JsonSerializer.Serialize(new { original.Id, Name = "Edited order" }));
                form.Deactivate(); detail.Deactivate(); list.Deactivate();
                Console.WriteLine(JsonSerializer.Serialize(new { stage = "seed", passed = true, id = original.Id,
                    checks = new[] { "create", "list", "details", "edit", "refresh", "failed-save-retains-draft", "cancelled-write" } }));
            }
            else if (args[0] == "reopen") {
                using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(state));
                string id = evidence.RootElement.GetProperty("Id").GetString()!;
                Check((await store.GetAsync(id))!.Name == "Edited order", "Separate-process persistence failed.");
                var list = new OrdersViewModel(source);
                await list.ActivateAsync();
                Check(list.Items.Single().Id == id, "Reopened list identity changed.");

                var registry = new NavigationRegistry();
                registry.Register(new("OrderDetails", typeof(OrderDetailsViewModel), "Details"));
                registry.Register(new("OrderEditor", typeof(OrderEditorViewModel), "Editor"));
                var router = new BusinessActivationNavigator(registry);
                string commandLine = $$"""--route OrderEditor --data "{\"id\":\"{{id}}\"}" """;
                var request = BusinessActivationRequest.FromArguments(ActivationCommandLine.Split(commandLine))!;
                var editor = new OrderEditorViewModel(source);
                await editor.ActivateAsync(id);
                editor.Description = "Unsaved draft in reopened process";
                Check(await router.NavigateAsync(request, (_, _, _) => Task.FromResult(false)) == ActivationNavigationResult.Cancelled,
                    "Router did not respect a rejected navigation callback.");
                Check(editor.HasUnsavedChanges && editor.Description.StartsWith("Unsaved"), "Rejected activation changed draft.");

                var detail = new OrderDetailsViewModel(source);
                var detailsRequest = BusinessActivationRequest.FromArguments(new[] { "--route", "OrderDetails", "--data", JsonSerializer.Serialize(new { id }) })!;
                Check(await router.NavigateAsync(detailsRequest, async (_, payload, _) => {
                    await detail.LoadAsync(payload.GetBusinessString("id")!);
                    return true;
                }) == ActivationNavigationResult.Navigated && detail.Title == "Edited order", "Activation data did not load the saved record.");
                Check(await router.NavigateAsync(BusinessActivationRequest.FromArguments(new[] { "--route", "unknown" })!,
                    (_, _, _) => throw new InvalidOperationException("Unknown route called navigation.")) == ActivationNavigationResult.RouteNotFound,
                    "Unknown route was not rejected.");

                list.Deactivate(); detail.Deactivate(); editor.Deactivate();
                Console.WriteLine(JsonSerializer.Serialize(new { stage = "reopen", passed = true, id,
                    checks = new[] { "separate-process-persistence", "activation-data", "declined-navigation-retains-draft", "unknown-route" },
                    boundary = "No WinUI window is constructed; ContentDialog and actual page guards require UI acceptance." }));
            }
            else throw new ArgumentException("Unknown probe stage.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally { SqliteConnection.ClearAllPools(); }
    }
}
