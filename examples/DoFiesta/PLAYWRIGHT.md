# DoFiesta Playwright notes

Use this note when automating the local DoFiesta app with Playwright.

## Local app

Start the server project with `dotnet run`, rather than its built DLL:

```powershell
dotnet run --project examples/DoFiesta/DoFiesta.Presentation.Web.Server/DoFiesta.Presentation.Web.Server.csproj --nologo
```

In a linked worktree, read the actual URL from the `[BDK] worktree` startup line or `bdk dev status --json`. Each worktree has its own ports, catalogs and cookies. Use the instance's HTTPS origin and set Playwright's `ignoreHTTPSErrors: true`.

If the app root becomes blank and the browser reports hashed framework module or MIME-type failures:

- Confirm the server was started with `dotnet run`.
- Retry in a fresh browser context.
- Probe `/`, `/_framework/blazor.web.js` and `/_framework/dotnet.js` on the actual instance origin. Expect HTML for the root and JavaScript for the framework assets.

Stop instances started for verification with `bdk dev stop --app dofiesta` and confirm their ports are released.

## Authentication

DoFiesta uses the local fake identity provider in development. Its authority follows the instance origin. The Blazor client id is `blazor-wasm`; its scopes include `openid profile email roles offline_access`.

1. Open the instance's home page.
2. Trigger sign-in from the UI.
3. Choose a configured fake user card, such as `luke.skywalker@starwars.com`.
4. Wait for the redirect back into the app.
5. Click `Get Started` or `Todos`.

Direct navigation to `/todos` serves the app shell, then the client router handles authentication. Wait for the login redirect to settle before asserting page content.

The DevKit dashboard has its own authentication flow and requires an administrator role. Follow its login flow when opening a dashboard link; the Blazor login alone does not establish the dashboard session.

## Navigation

- Home and Todos are application pages. Todo help remains at `/todos/help`.
- The Dashboard link in the app header opens the DevKit dashboard in a new tab.
- The dashboard overview is `/_bdk/dashboard`.
- Navigate to Auditing, Change History, Entities, Jobs, Logs, Metrics, Messaging, Orchestrations, Queueing, Files and Health from the dashboard itself.
- The Files dashboard is `/_bdk/dashboard/storage/files`.
- Legacy `/operations*` pages and the `/streaming` page are removed. Do not use them as test entry points.
- This checkout has no Notifications or File Events dashboard. Do not use obsolete sample links or invent replacement routes.

DevKit dashboard pages and login retain their own UI. They are outside the planned Blazor application restyling.

## Todo creation

- A newly created todo is assigned to the current user by default.
- The quick-add input is labeled `New Todo`.
- The quick-add field updates while typing. Its Add Todo action stays disabled until task options and the list have loaded.
- The adjacent detailed-create button opens the `Create Task` dialog.
- Title, status, priority, due date and local due time remain editable in the table.
- Attachments, Change History, import/export and bulk completion remain available.
- Creating a todo still queues its notification email. Removing the old operations APIs does not remove notification processing or storage.

## Dashboard verification

1. Sign in and create a todo with a unique title.
2. Click Dashboard in the app header, then open Auditing from the dashboard.
3. Complete the dashboard administrator login if needed.
4. Inspect the captured Todo action.
5. Use Change History to inspect the tracked changes.
6. Use Messaging, Queueing and Jobs for their respective processing state.

The dashboards use their own server-rendered content and action routes under `/_bdk/dashboard`. The optional operational REST APIs for jobs, logs, metrics, messaging, queueing, notifications, orchestrations and file storage are not exposed by this sample. Do not verify them with `/_bdk/api/notifications/emails` or other removed operations APIs.

The Todo APIs, enumerations, attachment endpoints, DataPorter and Todo Change History API remain. The standalone subscription Change History API and all three streaming demo endpoints are removed. System discovery and the development identity provider remain available.

## Known automation quirks

- Capture the new tab opened by the header's Dashboard link.
- Use a fresh context if the browser requests stale hashed bootstrap assets.
- Keep generated API-client checks separate from dashboard authentication: Todo API calls use the Blazor bearer-token client, while dashboards use their own authenticated session.

## Backend tables

- Todo items: `core.TodoItems`
- Notification outbox: `core.__Notifications_Emails`
- Notification attachments: `core.__Notifications_EmailAttachments`
