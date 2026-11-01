---
name: dashboard-pages
description: Build and review bITdevKit dashboard pages using existing RazorSlice layouts, plugin registration, refresh helpers, accessible controls, storage browsers, charts, and feature icons. Use this when adding or restyling dashboard pages, tabs, tables, detail views, or operational controls.
---

# Dashboard pages

Use the existing DevKit dashboard as the design system. Read the closest working page before editing. This skill covers implementation and review; it does not authorize deployment or operational actions.

## Start with the right sources

1. Read [the dashboard feature guide](../../../docs/features-presentation-dashboard.md) and inspect the shared [layout](../../../src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml).
2. Use the [source map](references/source-map.md) to choose a reference page. Messaging is useful for retained work and type formatting; Jobs for master/detail panels and charts; Storage for capability-aware browsing and editing; Profiling for analytical views; Health for status and unavailable states; Console for live connections.
3. Read the relevant sections of [page patterns](references/page-patterns.md). Reuse helpers and semantic theme tokens before creating feature-specific code.
4. Inspect the feature's registration, queries, capabilities, authorization, and readiness. A screenshot alone does not establish these contracts.

## Implement the page

- For application-owned collections of pages, consider `DashboardPageSet` and its `.Page(...).Razor<TPage>().Content<TContent>()` declarations. Existing feature plugins can retain separate `IDashboardEndpoints` and `IDashboardPageProvider` implementations.
- Build routes from `DashboardEndpointsOptions`, `DashboardPath`, or the feature's route helpers. Keep full pages, fragments, data routes, and actions under the configured dashboard group and its access rules.
- Prefer the feature's registered query/admin services. Keep business operations and persistence in their owning services rather than adding dashboard-specific database access.
- Set page and card groups explicitly. DevKit features use `bdk` with `GroupOrder = 0`. Choose navigation order beside related pages; card order can differ. Do not assume a card inherits its provider's group.
- Use `RazorSlice` with the shared `_DashboardLayout` and `DashboardLayoutModel.Title`. The shell supplies navigation and the page title; avoid another large page heading.
- Keep filters, content, dialogs, scripts, and styles separable where the page needs independent refresh. Reuse existing structures rather than making every file a new component system.

## Match the dashboard's controls

- Use compact toolbars, `nav nav-pills`, bordered cards, small forms, badges, responsive tables, and feature-scoped styles. Keep explanatory text short. Put technical explanations behind an info tooltip or useful detail view.
- Icon-only actions need a tooltip and accessible name. Apply and Reset use text labels. Statuses need text or distinct shapes as well as color.
- Use friendly filters for ordinary investigation. Partial key/type searches should advertise `contains` and use supported query operators. Keep raw `FilterModel` JSON editors out of the normal dashboard filter form.
- Keep type and method suffixes visible. Use the shared `Shortener` with `Separator = "."`; use left truncation when the text still overflows. Preserve the full value in a tooltip and the exact detail view. See the examples in page patterns.
- Prefer a right-aligned toolbar menu for exports and secondary tools. Keep ordinary downloads simple: format, Download, status, and chunk continuation when needed. Do not add manual verification checkboxes or metadata panels unless requested. Preserve the export API's integrity contract internally.
- Avoid generic instance-information panels, implementation essays, and empty oversized headings. Retain important distinctions such as unavailable versus zero and source evidence versus confirmed persistence.

## Preserve interaction and state

- Use `window.bdkDashboard.createRefresher` for refreshable fragments. It handles cancellation, stale responses, hidden tabs, scheduling, and error state. Pause refresh around editing and modal workflows.
- Use `DashboardPage.RememberedViews` for page-level tabs. Mark explicit links or their containing view with `data-dashboard-view` so saved navigation cannot redirect Apply, Reset, paging, or a deliberate tab choice.
- Scope saved state by feature and route. Keep query parameters for shareable selections; store only appropriate view preferences in browser storage. Preserve existing storage keys when refactoring.
- Keep exact identifiers separate from shortened labels. Preserve paging/cursor state, read scope, provider capabilities, ETags, and selection/export consistency.
- Use the shared dialog, correlation, date-range, and chart helpers. Encode server values with Razor and use `textContent` for client-generated text. Dispose subscriptions, charts, timers, and handlers when replacing their owners.

## Icons and themes

Follow [the canonical icon instructions](../../../docs/assets/features/README.md). Draw editable SVGs with the prescribed geometry, two semantic colors, and accessibility attributes. Update the catalog and regenerate its sprite/gallery.

Built-in navigation resolves `DashboardPage.Key` through `DashboardFeatureIcons`. Overview cards can set `DashboardPageCard.FeatureKey`; `Icon` remains the Bootstrap fallback. Add the SVG resource to `Presentation.Web.csproj` and extend the icon tests. Marked documentation images feed the existing MkDocs hook.

Use Bootstrap and dashboard CSS variables, contrast-adjusted `--dashboard-*-text` roles, and shared chart colors. Verify light, dark, monochrome, and narrow layouts. Avoid hardcoded palette colors or global overrides for one feature.

## Verify and hand back

1. Build the affected web project and run relevant existing tests sequentially. Add tests for changed behavior, authorization, query contracts, or rendering; a text-only change does not need a test that copies its markup.
2. Broaden presentation tests when changing the shared layout, refresh/navigation helpers, or card rendering. Check unavailable, empty, populated, long-name, and error states, plus query/export agreement where relevant.
3. When browser work is requested, verify actual interactions using the session's required browser tool. Check tab restoration, filter/reset/paging, tooltips, menu focus, modal editing, refresh races, and desktop/mobile overflow as applicable.
4. Follow the icon README's generation, visual, documentation sync, and MkDocs checks for icon work. Rebuild/restart a host to load changed embedded SVGs.
5. Prefer workspace launch tasks or `dotnet run` for sample browser checks so development static assets load. Inspect startup behavior before restarting a running host; development profiles can recreate databases.
6. Report the actual checks and any runtime restart requirement. Keep temporary screenshots, logs, and unrelated generated files out of the change.
