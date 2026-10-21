# {{APP_NAME}}: WinUI frontend with WPF host

Startup executable/project: `{{APP_NAME}}/{{APP_NAME}}.csproj`. Launch the host, which owns its UI child.

| Project/path | Responsibility |
|---|---|
| `{{APP_NAME}}/App.xaml.cs`, `Services/UIRunnerService.cs` | Host singleton, background lifecycle, UI creation, activation delivery and guarded shutdown |
| `{{APP_NAME}}/Modules`, `GrpcServers` | Host services, database initialization and RPC handlers |
| `{{APP_NAME}}.UI/App.xaml.cs`, `Modules` | Frontend initialization and client-side registrations |
| `{{APP_NAME}}.UI/MainWindow.xaml.cs`, `Navigation` | Window, sidebar and guarded business routing |
| `{{APP_NAME}}.Grpc.Service/Protos`, `{{APP_NAME}}.Grpc.Client` | Named-pipe RPC definitions and frontend clients |
| `{{APP_NAME}}.MainPanel`, `{{APP_NAME}}.UIComponent` | Business UI and framework pages/windows/navigation |
| `{{APP_NAME}}.Common`, `{{APP_NAME}}.Models`, `{{APP_NAME}}.DataAssistor` | Shared helpers/contracts and host-side support |

The WPF host owns background services and storage. A SQLite Recipe registers its Data project in the host; the frontend accesses it through the generated client/RPC contract. Do not add a frontend Data reference or instantiate a second database service to bypass RPC.

The host and UI have separate providers and module catalogs. Host `InitializeAsync` completes before business gRPC is opened; frontend modules initialize before requests are consumed. The early activation listener queues arguments during startup. Modules can bind additional RPC services through `IXamlNexusGrpcModule`; preserve existing proto numeric values when extending contracts.

The host already includes tray and updater infrastructure. The full settings panel follows the initial basic/standard profile; read current modules for later changes. `UIRunnerService` owns the child process and its private control streams. Host exit, UI close and restart go through guarded requests; ordinary teardown does not kill an unresponsive UI because a wait elapsed.

Repeated host launches forward arguments to the existing host, which reuses or creates its UI. Preserve UI request ordering and cancellation. Do not treat the UI child as an independent SDK AppInstance owner. Inspect current sources when working with older or customized generated projects.
