# DoFiesta Playwright notes

Use this note when automating the local DoFiesta app with Playwright.

## Local app

- Start the app with the server project, not the built DLL:
  - `dotnet run --project examples\DoFiesta\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj --nologo --urls "https://localhost:5001;http://localhost:5000"`
- Prefer `https://localhost:5001`.
- In Playwright use `ignoreHTTPSErrors: true`.
- If the app root turns into a white page and the browser console mentions a hashed `_framework/dotnet.<hash>.js` module or a MIME-type failure, treat that as a bootstrap-asset mismatch first:
  - verify the app was started with `dotnet run` on the server project
  - retry with a fresh browser context
  - probe `https://localhost:5001/`, `https://localhost:5001/_framework/blazor.web.js`, and `https://localhost:5001/_framework/dotnet.js`
  - the healthy state is `200` HTML for `/` and `200` JavaScript for both framework assets

## Authentication

DoFiesta uses the local fake identity provider in development.

- Authority: `https://localhost:5001`
- Client id: `blazor-wasm`
- Default scopes include: `openid profile email roles offline_access`
- Test user:
  - email: `luke.skywalker@starwars.com`
  - password: `starwars`

### Reliable login flow

1. Open `https://localhost:5001`.
2. Trigger sign-in from the UI and continue into the fake identity provider flow.
3. On the sign-in page, choose the `luke.skywalker@starwars.com` user card.
4. Wait for the redirect back into the SPA shell.
5. Only then navigate by clicking app links such as `Todos`, `Operations`, or `Notifications`.

### Navigation rule

Direct navigation to authenticated routes now works again because the server serves the SPA shell first and the client router performs the auth redirect. For reliable automation you can still prefer visible in-app navigation after login, but these direct entry routes should no longer return a raw `401` document response:

- `/todos`
- `/operations`
- `/operations/notifications`
- `/operations/files`
- `/operations/fileevents`

### Useful auth detail

The OIDC user payload is stored in `sessionStorage` under:

- `oidc.user:https://localhost:5001:blazor-wasm`

That entry contains the access token used by the generated API client.

## Navigation

- Top-level nav link: `Todos`
- Operations nav group: `Operations`
- Operations overview button: `Open Notifications`
- Operations nav link: `Notifications`
- Reliable post-login entry into Todos: home-page `Get Started`

Unauthenticated direct hits to protected routes should redirect into the login flow through the SPA shell instead of rendering a raw `401` response. During automation, prefer waiting for the redirect/login UI to settle before asserting page content.

## Todo creation

- The add action lives on the todos page toolbar.
- A newly created todo is assigned to the current user by default on the server.
- Todo creation should queue a notification email row in `core.__Notifications_Emails`.
- The quick-add input is the textbox labeled `New Todo`.
- The create action is the filled secondary plus button in the same quick-add row.
- The `New Todo` input uses `Immediate="false"`. In Playwright, commit the value first (for example with `Tab` or blur) before clicking the add button, otherwise `AddNewTodo()` still sees an empty `newTodoTitle`.
- The create flow persists the todo immediately, but email visibility depends on outbox-domain-event processing. With the current configuration it now forwards new events immediately instead of waiting only for the periodic background sweep.

## Notifications operations page

- After opening `Operations`, click `Notifications`.
- The page filter label is `Subject contains`, but the backing provider query parameter is `subject`.
- The notifications endpoint used by the page is:
  - `GET /api/_system/notifications/emails?subject=<title-fragment>&take=10`
- A successfully queued todo email shows:
  - `to = luke.skywalker@starwars.com`
  - `from = DoFiesta <noreply@dofiesta.local>`
  - subject format `DoFiesta Todo #<number>: <title>`

## Known automation quirks

- The `Operations` nav toggle can end up outside the viewport when the todos page is scrolled deep into the list.
- If a normal Playwright click on the nav toggle fails because of viewport layout, scroll back to the top or use the nav link after bringing the shell back into view.
- Querying provider-backed endpoints from Playwright requires the bearer token from `sessionStorage`; cookies alone are not enough for `/api/_system/...` fetch calls.
- For provider verification, prefer the notifications endpoint over direct database inspection.
- A clean Playwright browser context currently loads `/` and the direct `/todos` route successfully. If a previously opened browser still requests an older hashed `dotnet.*.js` module, suspect stale client bootstrap state before suspecting the current server build.

## Recommended automation pattern

1. Detect the dev server first.
2. Open the home page.
3. Log in with the fake identity provider user above.
4. After redirect, click `Get Started` to reach `Todos`.
5. Fill the `New Todo` textbox with a unique title and trigger the plus button in the same row.
6. Click `Operations`, then `Notifications`.
7. Filter by the unique title.
8. Verify a matching email row exists through the notifications UI or `GET /api/_system/notifications/emails`.

## Backend tables

- todo items: `core.TodoItems`
- notification outbox: `core.__Notifications_Emails`
- notification attachments: `core.__Notifications_EmailAttachments`
