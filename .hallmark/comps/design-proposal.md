# DoFiesta Wayfind night proposal

Status: static design mocks for approval. No application files changed. These are HTML/CSS renders, not screenshots of a running Blazor application.

Direction: Wayfind night only. Chivo Mono, blue-black surfaces, cobalt actions, amber due dates on ink, square borders, no gradients or soft cards. Reference: https://www.usehallmark.com/themes/wayfind/

Two read-only subagents inspected the actual routes and MudBlazor implementation. MudBlazor 9.12.0 supports the proposed treatment using the existing theme, table, fields, selects, date picker, dialogs, app bar and drawer.

## Mock files

- b-departure-board.png: desktop Todos.
- c-mobile.png: mobile Todos.
- d-create-task.png: Create Task dialog.
- Matching HTML files retain the editable design sources. Controls in these mock files are illustrative; they do not call the backend.

Task rows are illustrative data. No new projects, boards, task analytics or backend features are proposed.

## Proposed implementation

- Edit client Layout/Themes.cs for the dark palette, typography and square radii.
- Edit client Layout/MainLayout.razor and Layout/NavMenu.razor and associated styles for the shared shell. Preserve authentication, operations destinations, health behavior and profile access.
- Add a shared client stylesheet for Mud components, dialogs and popovers.
- Edit client Pages/TodoItemIndex.razor markup and add its scoped stylesheet. Retain fields, handlers, filtering, sorting, quick add, full create, bulk completion, import/export, attachments and history.
- Use responsive stacked table rows on mobile, exposing due date/time and row actions currently hidden at small widths. Status and priority remain editable.
- Apply the same system to Home, Help, Streaming and UserProfile while retaining each route and logic.
- Add the font and stylesheet references in server Components/App.razor, which owns host assets.
- Delete no application files or routes. Avoid generated API clients and backend changes.
- Use the existing Material icon library in the implementation. Mock SVGs illustrate the corresponding controls.

## Verification

Rendered desktop at 1280px, mobile at 390px, dialog at 760px using HTML preview. Inspected the rendered compositions. Static Hallmark checks: zero FAIL; remaining contrast warnings arise from duplicate light/dark token declarations in these mock sources and require resolved-style validation during implementation. The linter render tier could not run because puppeteer-core is absent; HTML preview supplied actual screenshots. No build, application runtime verification or functional tests were performed during this design-only stage.

Before implementation handoff: validate resolved contrast, full Mud control states, keyboard focus, mobile menus, loading/empty/error states, and the real Blazor pages at desktop and narrow widths. Stop any sample hosts started for verification.
