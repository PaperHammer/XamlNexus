# Pages and navigation

Generate `blank`, `list`, `details`, and `form` pages through `page add` to reuse the current contracts. They live in `{{APP_NAME}}.MainPanel`; navigation registrations live in `{{APP_NAME}}.UI/Navigation`. Inspect the generated registration and injected data-source interface before wiring business actions.

`AppObjectFactory` resolves constructor dependencies from the existing provider while creating caller-owned pages/ViewModels. Register shared dependencies through the appropriate application's module catalog. Avoid constructor cycles involving MainWindow and a navigation service; resolve the window at action time if needed. Page-owned resources are cleaned up by the page; shared DI services belong to the container.

## Lifecycle

- `OnEnter(FrameworkPayload?)` receives the incoming data and can run again for a cached page.
- `CanLeaveAsync(ArcLeaveReason, CancellationToken)` decides whether navigation/close may proceed. Cancel or failed save must preserve the current record and draft.
- `OnPreLeaveAsync` waits for page loading/work before leaving; `OnLeaveAsync` handles destruction cleanup. Read the actual overloads before overriding them.
- `KeepAlive` retains a page in the navigation host. Do not assume every departure disposes it.

Use awaited `NavigateAsync` or MainWindow's `NavigateActivationAsync`, which preserve the leave guard and update sidebar selection after success. Opening a different record on the same page also needs leave approval. Window close should use `RequestCloseAsync`; hybrid UI close/restart and host shutdown must wait for frontend approval and actual process exit. A timeout does not authorize discarding unsaved work.

Business startup uses `--route <registered-route> --data <JSON-object>` on the preset's startup executable. For generated details/forms, read IDs with `payload.GetBusinessString("id")`; it supports typed external activation and ordinary in-app payloads. Pass stable record IDs, not display labels. Inspect the application's current activation support before applying these newer APIs to older generated projects.

Test save success/failure/cancellation, record switching, and page retention when those behaviors change. Keep explicit distinctions between headless protocol tests and native UI acceptance.
