---
name: dashboard-pages
description: Build and assess bITdevKit dashboard pages using existing RazorSlice layouts, plugin registration, refresh helpers, accessible controls, storage browsers, charts, and feature icons. Use this when reviewing consistency or adding or restyling dashboard pages, tabs, tables, detail views, or operational controls.
---

# Dashboard pages

Use the existing DevKit dashboard as the design system. Read the closest working page before editing. This skill covers implementation and review; it does not authorize deployment or operational actions.

## Start with the right sources

1. Read [the dashboard feature guide](../../../docs/features-presentation-dashboard.md) and inspect the shared [layout](../../../src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml).
2. Use the [source map](references/source-map.md) to choose a reference page. Messaging is useful for retained work and type formatting; Jobs for master/detail panels and charts; Storage for capability-aware browsing and editing; Profiling for analytical views; Health for status and unavailable states; Console for live connections.
3. Read the relevant sections of [page patterns](references/page-patterns.md). Reuse helpers and semantic theme tokens before creating feature-specific code.
4. Inspect the feature's registration, queries, capabilities, authorization, and readiness. A screenshot alone does not establish these contracts.

## Choose the page structure

- Follow the shared order: refresh toolbar and divider, routed view tabs when needed, compact filters, relevant summaries, results, then continuation below the items. Keep all outer edges aligned. Omit sections that have no purpose on that view.
- Choose a complete catalog, growing operational history, diagnostics grid, inspector, or live workspace deliberately. Jobs lists every matching definition without paging; Orchestrations Overview shows every registered definition without filters. Their execution histories use filters and continuation.
- Move independent subjects into routed tabs. Use local tabs for dense dialogs, with summary and navigation above the scrolling content. Keep small inspectors simple and retain the useful workspace layout of Files, Console and log streams.
- Read the [review checklist](references/review-checklist.md) for page exceptions, evidence requirements and assessment priorities. A review request authorizes the assessment and requested documentation updates; implement product changes only when requested.

## Implement the page

- For application-owned collections of pages, consider `DashboardPageSet` and its `.Page(...).Razor<TPage>().Content<TContent>()` declarations. Existing feature plugins can retain separate `IDashboardEndpoints` and `IDashboardPageProvider` implementations.
- Build routes from `DashboardEndpointsOptions`, `DashboardPath`, or the feature's route helpers. Keep full pages, fragments, data routes, and actions under the configured dashboard group and its access rules.
- Prefer the feature's registered query/admin services. Keep business operations and persistence in their owning services rather than adding dashboard-specific database access.
- Set page and card groups explicitly. DevKit features use `bdk` with `GroupOrder = 0`. Choose navigation order beside related pages; card order can differ. Do not assume a card inherits its provider's group.
- Use `RazorSlice` with the shared `_DashboardLayout` and `DashboardLayoutModel.Title`. The shell supplies navigation and the page title; avoid another large page heading.
- Keep filters, content, dialogs, scripts, and styles separable where the page needs independent refresh. Reuse existing structures rather than making every file a new component system.

## Match the dashboard's controls

- Use compact toolbars, `nav nav-pills`, bordered cards, small forms, badges, responsive tables, and feature-scoped styles. Keep explanatory text short. Put technical explanations behind an info tooltip or useful detail view.
- Apply uses its text label. Reset uses the circled-X icon (`bi bi-x-circle`), with `title="Reset filters"`, `aria-label="Reset filters"` and `aria-hidden="true"` on the icon. Use the shared `FilterActions` component for both reset links and buttons. Other icon-only actions also need a tooltip and accessible name. Statuses need text or distinct shapes as well as color.
- Keep Apply and Reset together at the trailing edge of the filter row on desktop. Give the pair a natural-width column and let fields share the remaining space. Logs and Errors keep the pair in the first filter row, with secondary identifiers below. Other multi-row forms place the pair beside the final primary criteria, ahead of optional secondary disclosures. Wrap the pair together only when the viewport requires it.
- Put Load more immediately beside Rows per load below the items, with the cumulative amount loaded. Keep Load more visible and disabled at exhaustion and keep the selector enabled. Use `ListFooter` and the existing continuation helper; see [page patterns](references/page-patterns.md) for state and retry rules.
- Keep `.dashboard-filter-panel` on an outer block or form and nest the Bootstrap `.row` inside it. Combining both classes lets negative gutter margins collapse the divider gap and shift the panel beyond the result edges. Align the toolbar divider, filter border and result frame; use the shared spacing instead of page-specific margins or extra dividers.
- Place filters directly below page-level tabs. Keep creation/upload actions in the result header when an extra action row would create a gap between tabs and filters. Compare Documents and Blobs with Files when checking this spacing.
- Use friendly filters for ordinary investigation. Partial key/type searches should advertise `contains` and use supported query operators. Keep raw `FilterModel` JSON editors out of the normal dashboard filter form.
- Keep type and method suffixes visible. Use the shared `Shortener` with `Separator = "."`; use left truncation when the text still overflows. Preserve the full value in a tooltip and the exact detail view. See the examples in page patterns.
- Prefer a right-aligned toolbar menu for exports and secondary tools. Keep ordinary downloads simple: format, Download, status, and chunk continuation when needed. Do not add manual verification checkboxes or metadata panels unless requested. Preserve the export API's integrity contract internally.
- Avoid generic instance-information panels, implementation essays, and empty oversized headings. Retain important distinctions such as unavailable versus zero and source evidence versus confirmed persistence.

## Preserve interaction and state

- Use `window.bdkDashboard.createRefresher` for refreshable fragments. It handles cancellation, stale responses, hidden tabs, scheduling, and error state. Pause refresh around editing and modal workflows.
- The shared refresher defaults to HTML fragments. JSON data consumers must set `responseType: 'json'` and parse the text in their `applyResponse` callback. Keep HTML error-marker validation scoped to HTML; storage diagnostics uses JSON for all three storage families.
- Use `DashboardPage.RememberedViews` for page-level tabs. Mark explicit links or their containing view with `data-dashboard-view` so saved navigation cannot redirect Apply, Reset, paging, or a deliberate tab choice.
- Make page-level tabs server routes with the nav outside the refreshed fragment, placed after the refresh toolbar. Every page's tabs must look and behave the same; see the tab notes in page patterns.
- Scope saved state by feature and route. Keep query parameters for shareable selections; store only appropriate view preferences in browser storage. Preserve existing storage keys when refactoring.
- Keep exact identifiers separate from shortened labels. Preserve paging/cursor state, read scope, provider capabilities, ETags, and selection/export consistency.
- Order recent histories by creation time with an exact identifier as the tie-breaker. A GUID alone does not establish chronology. Continuation and snapshot boundaries must follow the same ordering.
- Keep metric units through collection, aggregation and rendering. A histogram can measure bytes or rates as well as duration. Label count scope and timestamp zone; values in compact summaries must remain visible beside their labels.
- Use the shared dialog, correlation, date-range, and chart helpers. Encode server values with Razor and use `textContent` for client-generated text. Dispose subscriptions, charts, timers, and handlers when replacing their owners.

## Icons and themes

Follow [the canonical icon instructions](../../../docs/assets/features/README.md). Draw editable SVGs with the prescribed geometry, two semantic colors, and accessibility attributes. Update the catalog and regenerate its sprite/gallery.

Built-in navigation resolves `DashboardPage.Key` through `DashboardFeatureIcons`. Overview cards can set `DashboardPageCard.FeatureKey`; `Icon` remains the Bootstrap fallback. Add the SVG resource to `Presentation.Web.csproj` and extend the icon tests. Marked documentation images feed the existing MkDocs hook.

Use Bootstrap and dashboard CSS variables, contrast-adjusted `--dashboard-*-text` roles, and shared chart colors. Verify light, dark, monochrome, and narrow layouts. Avoid hardcoded palette colors or global overrides for one feature.

## Verify and hand back

1. For product changes, build the affected web project and run relevant existing tests sequentially. Add tests for changed behavior, authorization, query contracts, or rendering; a text-only change does not need a test that copies its markup. For a documentation-only assessment, validate links, skill structure and recorded evidence.
2. Broaden presentation tests when changing the shared layout, refresh/navigation helpers, or card rendering. For shared refresher changes, also run `node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs`. Check unavailable, empty, populated, long-name, and error states, plus query/export agreement where relevant.
3. When browser work is requested, verify actual interactions using the session's required browser tool. Check tab restoration, filter/reset/paging, tooltips, menu focus, modal editing, refresh races, and desktop/mobile overflow as applicable.
4. Follow the icon README's generation, visual, documentation sync, and MkDocs checks for icon work. Rebuild/restart a host to load changed embedded SVGs.
5. Prefer workspace launch tasks or `dotnet run` for sample browser checks so development static assets load. Inspect startup behavior before restarting a running host; development profiles can recreate databases.
6. Recapture or flag documentation screenshots of pages whose layout changed. Follow the screenshot notes in page patterns.
7. Report the actual checks and any runtime restart requirement. Keep temporary screenshots, logs, and unrelated generated files out of the change.
8. Consolidate new lessons into [page patterns](references/page-patterns.md), the [review checklist](references/review-checklist.md), or the [source map](references/source-map.md). Add a short rule, its reason and a reference page. Correct outdated notes and avoid a chronological tail of repeated instructions.
