# {{APP_NAME}}: pure WinUI architecture

Startup executable/project: `{{APP_NAME}}.UI/{{APP_NAME}}.UI.csproj`.

| Project/path | Responsibility |
|---|---|
| `{{APP_NAME}}.UI/Program.cs`, `App.xaml.cs` | AppInstance registration/redirection, queued activation, application initialization |
| `{{APP_NAME}}.UI/Modules` | Shared service registration and initialization before work starts |
| `{{APP_NAME}}.UI/MainWindow.xaml.cs`, `Navigation` | Window, sidebar and guarded business routing |
| `{{APP_NAME}}.MainPanel` | Business pages, ViewModels, data-source contracts |
| `{{APP_NAME}}.UIComponent` | ArcPage/ArcWindow, navigation host, payloads, activation contracts |
| `{{APP_NAME}}.Common`, `{{APP_NAME}}.Models` | Common helpers and shared models/contracts |

The settings panel is present initially for standard and omitted initially for basic. Read installed modules and project references for the current state. SQLite adds a Data project; pure WinUI business services can use its registered factory directly. Tray/updater are optional Recipes, so do not assume those capabilities exist.

Modules implement `IXamlNexusModule` and register an explicit factory with `XamlNexusModuleCatalog` through a module initializer. `ConfigureServices` runs before the provider is built; `InitializeAsync` runs after that and before queued activation is consumed. Inference, network or storage operations should not run on the UI thread unless they are UI APIs.

Subsequent launches forward into the existing UI process. Preserve startup path compatibility, queued delivery, and the guarded MainWindow route. Inspect actual project code for changes made after generation.
