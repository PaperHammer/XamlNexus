using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public sealed class SystemTrayRecipe : IXamlNexusRecipe {
    public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
        Id = "system-tray",
        Version = "1.0.0",
        DisplayName = "System tray and notifications",
        Description = "Adds a WinUI system tray menu, show/hide behavior, exit command, and native balloon notifications.",
        SupportedPresets = ["winui"],
    };

    public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) {
        string projectName = context.Manifest.Project.Name;
        return new XamlNexusRecipePlan {
            Changes = [XamlNexusRecipeFileChange.CreateText(
                $"{projectName}.UI/Modules/SystemTrayModule.cs",
                CreateModule(projectName))],
            ProjectOperations = [new EnsurePackageReferenceOperation(
                $"{projectName}.UI/{projectName}.UI.csproj",
                "H.NotifyIcon.WinUI",
                "2.3.2")],
        };
    }

    private static string CreateModule(string projectName) => $$"""
        using System;
        using System.Runtime.CompilerServices;
        using System.Threading;
        using System.Threading.Tasks;
        using H.NotifyIcon;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.UI.Xaml.Controls;
        using Microsoft.UI.Xaml.Input;
        using Microsoft.UI.Xaml.Media.Imaging;

        namespace {{projectName}}.UI.Modules;

        internal sealed class SystemTrayModule : IXamlNexusModule {
            [ModuleInitializer]
            internal static void Register() =>
                XamlNexusModuleCatalog.Register(static () => new SystemTrayModule());

            public void ConfigureServices(IServiceCollection services) =>
                services.AddSingleton<SystemTrayService>();

            public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default) {
                services.GetRequiredService<SystemTrayService>().Initialize();
                return Task.CompletedTask;
            }
        }

        public sealed class SystemTrayService(MainWindow mainWindow) : IDisposable {
            public void Initialize() {
                if (_trayIcon is not null) return;
                var toggleWindow = new XamlUICommand { Label = "Show / Hide" };
                toggleWindow.ExecuteRequested += (_, _) => ToggleWindow();
                var exit = new XamlUICommand { Label = "Exit" };
                exit.ExecuteRequested += (_, _) => App.ShutDown();
                var menu = new MenuFlyout();
                menu.Items.Add(new MenuFlyoutItem { Text = "Show / Hide", Command = toggleWindow });
                menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(new MenuFlyoutItem { Text = "Exit", Command = exit });

                _trayIcon = new TaskbarIcon {
                    ToolTipText = "{{projectName}}",
                    IconSource = new BitmapImage(new Uri("ms-appx:///Assets/xamlnexus.ico")),
                    ContextFlyout = menu,
                    LeftClickCommand = toggleWindow,
                    NoLeftClickDelay = true,
                };
                _trayIcon.ForceCreate(enablesEfficiencyMode: false);
                _trayIcon.ShowNotification("{{projectName}}", "The application is running in the system tray.");
            }

            public void ShowNotification(string title, string message) =>
                _trayIcon?.ShowNotification(title, message);

            private void ToggleWindow() {
                if (_isHidden) mainWindow.Show();
                else mainWindow.Hide();
                _isHidden = !_isHidden;
            }

            public void Dispose() {
                _trayIcon?.Dispose();
                _trayIcon = null;
            }

            private TaskbarIcon? _trayIcon;
            private bool _isHidden;
        }
        """;
}
