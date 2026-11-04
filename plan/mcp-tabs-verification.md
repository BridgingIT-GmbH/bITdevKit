# MCP dashboard tabs verification

Verified on 2026-11-04.

## Result

- Overview shows handler/operation counts, server status, runtime target, and operation counts by toolset and feature.
- Connections contains Host Bridge and MCP Server Sessions, including the existing offline state.
- Handlers contains the complete registered-handler catalog.
- Operations contains the complete capability catalog and existing argument-schema dialog.
- Each tab has a server route and its own content route under the configured dashboard root. Navigation stays outside refreshed content. The selected tab is remembered.
- Phone tables scroll within their cards. Operation descriptions retain readable column widths; summary rows retain their natural height.

## Automated verification

`dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Mcp" --nologo /p:UseSharedCompilation=false`

101 passed, zero failed. Includes default/custom dashboard roots, separate content fragments, remembered paths, and absent-MCP routes.

`git diff --check` passed for the files changed for this follow-up.

## Browser verification

Used the shared T3 preview against the existing WeatherFiesta development instance at `https://localhost:5211`.

- Confirmed four tab links and the active route.
- Confirmed all 10 handlers and 71 operations remain available in their respective tabs.
- Manual refresh on Operations replaces only its content, retains Operations, and requests `/mcp/operations/content`.
- The first schema button opens the existing dialog with `blobs.clients` and its parsed JSON schema; close works.
- Selected tab persists in the dashboard's route-scoped local storage.
- Connection state was offline, with no active MCP server session. A connected server was not simulated.

Final screenshot checks and capture links are recorded below.

At 1280 x 800, the Overview fits within the viewport; the original combined page continued through the bridge and both catalogs. At 390 x 844, tabs wrap and the Operations table scrolls within its card without widening the document. Confirmed natural-height summary badges and compact operation rows after the narrow-layout correction. Also inspected White and Matte Black themes, then restored Tokyo Night.

Returning from System through the robot header link restores Operations; the explicit Overview tab still opens Overview. Refresh also preserves Operations, and schema buttons work after the content is replaced.

## Captures

- [Before MCP, 1280](assets/mcp-tabs/before-mcp-1280.png)
- [Overview, 1280](assets/mcp-tabs/after-overview-1280.png)
- [Connections, desktop](assets/mcp-tabs/after-connections-1440.png)
- [Operations, 1280](assets/mcp-tabs/after-operations-1280.png)
- [Overview, phone](assets/mcp-tabs/after-overview-390.png)
- [Operations, phone](assets/mcp-tabs/after-operations-390.png)
- [Overview, White](assets/mcp-tabs/after-overview-white-1280.png)
- [Overview, Matte Black](assets/mcp-tabs/after-overview-matte-black-1280.png)

The existing sample server was rebuilt and restored on HTTPS 5211 / HTTP 5210. No additional sample instance was left running.
