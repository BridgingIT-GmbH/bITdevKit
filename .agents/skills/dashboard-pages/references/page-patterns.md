# Dashboard implementation patterns

## Registration and access

- Choose `DashboardPageSet` when a host module owns several related pages and actions. It implements both dashboard endpoint mapping and page discovery. Use existing feature-owned providers when changing a package plugin.
- Keep stable page keys separate from paths and labels. Build URLs with configured group-path helpers; test a custom root such as `/ops`.
- Route through `EndpointsBase.MapGroup` so configured authorization, CORS, rate limiting, and metadata remain attached. Keep action endpoints protected even when a navigation item or button is hidden.
- Respect explicit plugin assemblies, disabled page keys, and optional feature registrations. A dashboard read must not enable source capture, create a schema, or change host configuration.
- Distinguish an absent service, an unready database, an unsupported provider operation, a failed read, and an empty successful result. Use the feature's readiness/capability contracts. A count of zero is appropriate only for an observed zero.
- Set `Group`, `GroupOrder`, and `Order` deliberately on both page and card descriptors. Use `ShowInSidebar` and `ShowOnIndex` for presentation, not as access controls. Page-provider/card failures should use the existing safe fallback behavior.

## Page structure and layout

- The full `Index.cshtml` uses `IUsesLayout<_DashboardLayout, DashboardLayoutModel>` and sets `LayoutModel.Title`. The shared breadcrumb supplies the title; put the toolbar and tabs directly below it.
- Refresh endpoints return a fragment such as `Content` or `Data`, without another layout or duplicated toolbar. Typed `MapDashboardPage<TPage>` and compiled RazorSlice identifiers are both supported.
- Keep modal shells and long-lived event handlers outside replaced fragments. Use delegated events on a stable owner, or explicitly initialize/dispose fragment behavior after each replacement. Scripts inserted with `innerHTML` do not automatically initialize the new content.
- Use compact rows, `g-2` gaps, `form-control-sm`/`form-select-sm`, small labels, `.card-header`, badges, and `table-sm`. Reuse the shared shell's typography and surface tokens.
- Use one outer card for a related metric group. Inside it, group named sources and label/value pairs with whitespace rather than nested bordered panels and per-value cards. System database contexts demonstrate this.
- For master/detail pages, use flexible columns with `min-width: 0`, `min-height: 0`, and independent scrolling regions. A long table should not push the selected detail or toolbar off the page. Jobs and Messaging demonstrate this.
- Keep responsive tables within `.table-responsive`. Bound long lists and histories; make headers sticky only within the intended scroll owner. Avoid viewport heights copied from another page without checking its toolbar, tabs, mobile behavior, and detached window.
- Result grids need `minmax(0, 1fr)` columns and `min-width: 0` panels. Keep horizontal scrolling inside the table wrapper so wide instance columns cannot push the paging footer outside a phone viewport; see Orchestrations Instances. Compact key/value cards need wrapping labels and visible values, rather than the minimum width of a multi-column list table.
- Size finite catalogs and empty results to their content. Use viewport-filling panels where they help a workspace, such as Files, Console and log streams. A short list should not leave a large empty tail after its footer.
- Scope CSS under a page root or feature prefix. Shared layout changes need cross-page validation.
- When an overview grows into a wall of unrelated cards, keep a short Overview and move the groups to routed tabs. System does this with Overview, Process (OS, architecture, resources, .NET runtime, versions) and Environment (configuration and worktree details). Put styles shared by the tabs in one partial.
- Optional or host-specific cards, such as worktree details, must never break the page when their source is missing or fails. Show a short "not active" or unavailable state instead.
- Respect detached views through the existing `dashboardView=window` behavior and `HideSideBar` layout option. Preserve the intended view mode when building paging, filter, and detail links.

## Navigation, filters, and time

- Page-level tabs look and behave the same on every page: the refresh toolbar row comes first, then `<nav class="nav nav-pills mb-2" aria-label="... views" data-dashboard-view="...">`, then the content. Mark the current link with `active` and `aria-current="page"`. Don't add `gap-1` or use `nav-tabs` for page-level views; `nav-tabs` stays for graph switches inside cards. Each tab is a server route listed in `DashboardPage.RememberedViews`. Keep the nav outside the refreshed fragment and pass the view to the fragment, so a refresh never re-renders or resets the tabs.
- Map the same full page at each view route. The fragment reads the view from `?view=` or the last path segment, checks it against a whitelist, falls back to the first view, and renders only that view's section. Shared summary cards can stay above the view sections.
- Supply `DashboardPage.RememberedViews` with stable view IDs and configured URLs. The shared handler restores sidebar/overview destinations across navigation and preserves Profiling's existing storage key.
- Judge tabs by behavior, not only looks. Metrics once had computed styles identical to System and still felt wrong: its nav sat inside the refreshed fragment, so every refresh reset it to the first tab and replayed the fade; the first tab flashed before the saved one returned; and the URL never changed. Client-side tabs that keep their own state cause this. Use routed tabs.
- `RememberedViews` currently belongs to `DashboardPage` descriptors. Check a page set's definition-to-metadata mapping before extending it; the current page-set builder does not expose a fluent remembered-view setting.
- The dashboard home uses routed `?group=` links for dynamic card groups such as `bdk` and application groups. Keep its group navigation outside the refreshed cards, pass the selection to the fragment, and render only the selected group's cards. Derive groups from the actual cards and fall back to the first available group when a selection disappears.
- Put `data-dashboard-view` on explicit tab links or the feature view container. Saved state must not rewrite a deliberate tab choice, Reset, paging, or another query-bearing action. The handler matches more specific view paths before a root view.
- Keep filters discoverable through domain labels. Use `contains` matching for ordinary key/type investigation where the query contracts support it. Do not silently change a field advertised as exact.
- Jobs is a complete filtered registration catalog: show every matching job in a full-width table without Load more or Rows per load. Drain the existing bounded query batches internally rather than introducing a display limit. Occurrences remains a paged operational history. Orchestrations Overview stays an unfiltered definition catalog.
- Keep raw filter-model editors and implementation details out of ordinary investigation controls. Quick filter buttons may use an internal serialized transport while exposing a simple action to the user.
- Apply uses text; Reset is an icon-only `bi bi-x-circle` control with `title="Reset filters"` and `aria-label="Reset filters"`. Hide the decorative icon from assistive technology. Use the same markup for link and button variants through [FilterActions](../../../../src/Presentation.Web/Dashboard/Pages/FilterActions.cshtml). Preserve query parameters during paging and use URLs for shareable filter selections.
- Use `DashboardDateTime.ParseUtc`, `FormatInput`, and `FormatQuery` for UTC filters. The input has no zone, while the query serialization retains offsets/precision correctly. Reuse `DateRange` with `DashboardDateRangeModel` for quick UTC windows.
- Define empty-date behavior explicitly: a rolling live window and a fixed historical interval are different modes. Avoid making a historical selection drift on automatic refresh.
- Label absolute timestamp zones in list headers, details and tooltips. UTC filter inputs and UTC refresh status must not leave local row timestamps ambiguous. Relative timestamps should retain an exact, zoned value in their detail or tooltip.

- Quick filter chips are toggle controls. Restore their selected style and aria-pressed state from the submitted filter on every load, allow deselection, and avoid adding duplicate criteria on Apply.

- For append loading, use a compact bottom Load more button, as [Logs](../../../../src/Presentation.Web/Logging/Dashboard/Pages/Index.cshtml) and [Auditing](../../../../src/Presentation.Web.Auditing/Dashboard/Pages/LoadMore.cshtml) do. Request the next batch with the applied query and batch size, rather than unsaved form inputs. Deduplicate appended rows by their exact IDs, disable the button during loading, and retain rows and the next-page or cursor value on failure so retry requests the same batch. Keep the exhausted button visible and disabled, immediately followed by an enabled Rows per load selector. Move focus to that selector when a focused button reaches exhaustion. Keep detail handlers outside appended fragments or delegate them from a stable owner.
- Order recent operational histories by immutable creation time descending, then exact identifier. Encode both fields in continuation and initial boundaries; a random GUID alone cannot order activity by time. Orchestrations Instances demonstrates the composite seek contract. Validate equal timestamps and arrivals between batches.
- Use `DashboardHistoryCursor` for the shared `CreatedDate` and ID seek contract. When a cursor protocol changes, version only that list's remembered history through `historyVersion`; preserve its independent batch-size preferences. Profiling's grouped continuation distinguishes groups loaded from individual rows loaded.
- Distinguish a batch size from an analytical query limit. Put batch size beside continuation below items. A limit on ranked groups or an analytical time window may stay in the query controls, with a label that states what it bounds.

## Refresh lifecycle

Use the shared refresher before writing a new polling loop:

```javascript
window.bdkDashboard.createRefresher({
    buildUrl: () => buildContentUrl(currentSelection()),
    contentSelector: '#activity-content',
    statusSelector: '#activity-refresh-status',
    intervalSelector: '#activity-refresh-interval',
    refreshButtonSelector: '#activity-refresh-now',
    storageKey: 'bdk.dashboard.activity.refreshInterval',
    shouldRefresh: () => !detailsModal.classList.contains('show'),
    onSuccess: root => initializeFragment(root)
});
```

The example uses page-owned URL, selection, modal, and initialization functions. Adapt those to the feature rather than inventing another generic helper.

- `createRefresher` accepts HTML fragments by default. For JSON endpoints, set `responseType: 'json'` and provide `applyResponse`; it receives the response text, so parse and validate that payload before changing chart state. Match the expected response MIME type and reject redirects/failures for both modes. Apply `[data-dashboard-query-error]` template checks only to HTML. The shared storage Diagnostics page is the JSON reference. A global HTML-only check once rejected successful diagnostics JSON, leaving all four charts blank and the run state stale.
- The shared refresher uses `cache: 'no-store'`, cancellation, generation checks, and serialized refresh work. It ignores late responses, pauses when the document is hidden, and preserves displayed data on failure with a stale-state message.
- Set `shouldRefresh` around modal editors, pending actions, and other workflows that must not lose edits or selection. Cancel stale selection requests before applying new responses.
- Use `preserveActiveTab` for Bootstrap tabs inside a refreshable fragment. Add stable `data-preserve-key` values to native `details` whose open state should survive replacement.
- Reinitialize page-specific controls in `onSuccess`; use `applyResponse` when replacing content also requires cleanup, state restoration, or chart disposal. Preserve scroll and selected record independently from the filtered list.
- Initialize tooltips for new fragment nodes and dispose instances owned by removed nodes. Existing shell initialization runs at page load; fragment replacement needs its own lifecycle handling.
- Store refresh preferences under a feature-specific key. Keep previous keys stable during refactoring. New storage access should tolerate disabled browser storage.

## Shared controls and applied selections

- Render `FilterActions` with `DashboardFilterActionsModel` for the trailing Apply/Reset pair and use `.dashboard-filter-panel` for ordinary criteria. Keep creation, upload and destructive actions outside that pair. Logs and Errors place the pair at the right end of their first filter row; their identifier fields remain in the second row. Other multi-row forms place the pair beside their final criteria. Reserve a natural-width trailing action column on desktop so Apply/Reset stays beside the final fields; let fields share the remaining width. For Bootstrap grids, use `col-12 col-lg-auto d-flex ms-auto` for the pair and flexible columns for text fields. Do not allocate a full desktop row to the actions. Only wrap the pair together when the viewport requires it.
- Keep page-level tabs adjacent to their filters: do not leave an empty creation/upload toolbar between them. Put New document and Upload in the result header, as Documents and Blobs do; Files is the spacing reference.
- Keep the bordered filter panel outside the Bootstrap grid: `<form class="dashboard-filter-panel"><div class="row g-2 align-items-end">...</div></form>`. Never put `.row` and `.dashboard-filter-panel` on the same element. Bootstrap's negative top/side gutters otherwise collapse the gap below the toolbar divider and move the filter border beyond the results. [Permalinks](../../../../src/Presentation.Web.Storage/Permalinks/Dashboard/Pages/Index.cshtml) is the corrected example. Compare its divider-to-filter gap and left/right borders with Logs at desktop and phone widths; rely on shared spacing rather than compensating margins.
- Check bordered result frames at phone widths as well as filter panels. The shared mobile `.table-responsive` rule has negative side margins; use `m-0` on a standalone bordered scroll wrapper so its border aligns with the toolbar and filters. Permalinks illustrates this case.
- Render `RefreshControls` with `DashboardRefreshModel` where a page supports ordinary refresh. It preserves stable feature IDs, names Refresh accessibly, and displays UTC. Specialized Tail, live connections and native navigation keep their transport.
- Use `createAppliedFilters` to commit criteria only after a successful response. Refresh, export, continuation and batch changes use its applied selection. Preserve drafts across result replacement with `captureFilterDraft`/`restoreFilterDraft` or the refresher's `preserveDraftSelector`.
- `ListFooter`/`DashboardListFooterModel` puts Load more immediately beside Rows per load below the items. Remove any duplicate top-row batch selector. Supply `LoadedCount` on the initial render and update the cumulative count after append, refresh and deletion; it describes loaded items, not just the latest batch or an assumed total. Documents, Blobs, Permalinks and Entities are the placement/count references.
- `createGrowingLists` supplies bounded continuation, exact-ID deduplication, retry, history retention, size preferences and route-scoped restoration for Jobs, brokers, Documents, Blobs and Permalinks. Use each provider's real continuation token or supported seek contract. Keep the exhausted Load more button visible and disabled while leaving Rows per load enabled. Batch changes start a new first batch using applied criteria and preserve unrelated drafts. Store boundaries/counts/scroll only, never row payloads. Restoration requests at most 100 bounded batches (20,000 rows); larger sessions resume from their first batch.
- Messaging and Queueing separate Overview (counts/subscriptions), Messages (filters, retained items and continuation), and Diagnostics (charts). Full-page routes select the view; refresh fragments receive an explicit view key. Ignore a stale Messages view key on Overview and skip its retained-message search there.
- Operational Jobs/broker routes emit no chart owners or graph polling. Diagnostics routes own and dispose their chart timers and repaint on theme changes. Keep diagnostic windows separate from operational selections.
- Split dense read-only dialogs into Bootstrap `nav-tabs` with linked tab/panel IDs and ARIA roles. Messaging and Queueing use Overview, Processing, Properties and Payload; keep status and tabs above the scrollable body, reset to Overview for each opened item, and preserve full identifiers and correlation actions. Modal tabs are local controls, unlike routed page-level views. For their two-column detail tables, override the shared narrow-screen minimum with `min-width: 0`, fixed layout and wrapping cells; list tables can retain horizontal scrolling.
- Use `dashboard-detail-table` for key/value inspectors and `dashboard-tabbed-inspector` for the shared local-tab reset/scroll behavior. These tables must override Bootstrap's `text-nowrap` utility as well as the list-table minimum width. Check labels for overlap separately from container overflow. Jobs and Orchestrations use this pattern; Entities keeps optional metadata in a disclosure.
- Secondary-filter badges describe fields selected in the form. Ignore empty serialized arrays and objects; do not call unsaved drafts applied filters. Keep the actual applied query separate from drafts when changing a bottom batch selector.
- Apply `.dashboard-dialog` and `ModalActions` to native or Bootstrap dialogs. The shared lifecycle preserves dirty editors, confirms discard inside the same dialog, ignores dirty backdrops and retains failed-save values. Mark a successful save with `markDialogSaved` before dismissal.
- Native entity-dialog headers anchor `.btn-close` with its own auto start margin and prevent it shrinking. Do not rely on an auto margin on the title: shared dialog title spacing resets those margins. The same header rule covers details, error states and editors.

## Values, names, and drill-down

- Keep human display labels readable. Shorten technical namespace/type strings with `Shortener.Apply(value, maximumLength, ".")`, as Messaging does. The adaptive strategy abbreviates non-terminal segments and uses left truncation as a fallback.
- Audit keys can end in `Type.Method`. Preserve both parts when budgeting namespace abbreviation; `AuditDashboardText` demonstrates this feature-specific adaptation. Do not abbreviate the identifying type to the same prefix for every row.
- CSS clipping must preserve the end as well. For technical labels, a clipped container with `direction: rtl; text-align: left` and an inner `<bdi dir="ltr">...</bdi>` gives leading ellipsis without reversing the displayed identifier. Keep this separate from ordinary display-name styling.
- Use the original complete value for `title`, copy actions, IDs, route parameters, and queries. The shared `data-dashboard-copy` action preserves each button's original tooltip and accessible name after feedback; Jobs Occurrences uses it for full occurrence IDs. Never send a shortened identifier to an action endpoint. Exact detail views should retain complete, copyable values.
- Use `DashboardCorrelationLink.Render` for server-rendered correlation links/copy buttons and `window.bdkDashboard.appendCorrelationLink` for client-rendered details. Render every displayed correlation identifier through these helpers so it links to the filtered Logs dashboard and provides copying. Preserve the full identifier and existing log-query constraints.
- Escape values with Razor. For dynamic values, use DOM construction and `textContent`; use `innerHTML` only for trusted server-rendered fragments. Validate external detail/download links using the feature's existing rules.

## Actions, dialogs, and export tools

- Use the shared `window.bdkDashboard.confirmAction` and `showMessage` components where the existing workflow requires confirmation or action feedback. Keep theming and keyboard behavior consistent.
- Subscription controls in Messaging and Queueing use compact icon buttons at the trailing edge of each text row. Keep the group on the same row, give each action a native `title` tooltip and accessible name, and leave paused-state badges beside the text. Native titles remain available after fragment replacement.
- Map operational changes to the feature's action endpoints; a navigation link or refresh GET should remain observational. Preserve server-side permission, scope, state, concurrency, and request-validation checks.
- Disable or mark busy controls while an action is in flight, show actionable failure feedback, and refresh the affected data after success. Avoid clearing a user's editor or selection when a request fails.
- Use a scrollable Bootstrap modal for focused properties, payloads, or editing. Preserve ETags and other concurrency metadata, pause background replacement, and restore focus when the modal closes.
- For execution drill-down, reuse a server-rendered detail fragment under the existing dashboard group. Intercept ordinary link clicks while preserving direct URLs and native modified clicks. Do not add an explicit execution-in-new-tab control. Abort earlier requests when switching records or closing; discard stale responses, show loading/unavailable states, and restore focus to the original list trigger even after following related records. Keep list filters, paging, scroll, and export forms intact. Keep focus inside the modal after replacing its content so Escape still works. [Auditing's detail dialog](../../../../src/Presentation.Web.Auditing/Dashboard/Pages/DetailDialog.cshtml) follows this pattern.
- Put exports and secondary tools behind the rightmost toolbar button, as Profiling's tools menu and Auditing's export menu do. Align the popup to that button's right edge, keep it within the viewport, bound its height, support Escape/click-outside closing, and keep form controls accessible. A form popup is not an ARIA menu of menuitems.
- Ordinary download menus offer a format selector, Download, status, and optional next chunk. Keep companion metadata and integrity checks in the export protocol; do not require users to tick a verification checkbox. Preserve the native form, selected query, and continuation state. Successful server output alone does not establish that a downloaded file was verified by the client.

## Storage and provider differences

- Read the selected provider's capability descriptor. Documents, blobs, files, and permalinks have different identifiers, query modes, mutation support, and preview/download behavior.
- Offer supported exact/prefix/suffix/full-match modes. Show a truthful unsupported/unavailable state for missing capabilities. Do not assume a provider has native paging, an exact total, or relational transaction semantics.
- Keep client/store selection, filter history, and page-size preferences distinct. Clear dependent key filters when switching providers if the existing page does so.
- Preserve continuation/cursor tokens and optimistic concurrency fields such as ETags. Use full resource keys in actions and downloads. Keep editing dialogs outside auto-replaced content.

## Charts, histories, health, and live streams

- Load Plotly through `window.bdkDashboard.loadPlotly`, and use `createPlotlyLayout`, `getChartTheme`, `getCssValue`, and `applySeriesColors` where appropriate. Reuse semantic/chart-series tokens rather than fixed colors or an additional chart loader.
- Repaint chart colors on `bdk-dashboard-theme-changed`. Purge charts and detach handlers owned by removed content. Follow Profiling's `applyResponse` cleanup and Jobs' lifecycle handling.
- Resize charts after their tab or collapsible pane becomes visible, and when their owning panel changes size. A chart initialized in a hidden container can measure zero width.
- A definitions Overview must read the registration catalog, including definitions that have never run. Persisted instance metrics are not a definition catalog. Orchestrations separates its unfiltered Overview, filterable Instances and session-wide Diagnostics routes.
- Workload rates come from runtime event counters, never differences between retained database counts. `DashboardMetricsHistory` collects exact aggregate instrument names in completed one-second buckets for fifteen minutes; `WorkloadCharts` renders the shared four-chart grid. Snapshot requests do not append events or consume history. Keep missing latency null, label handler calls separately from messages, and distinguish Jobs retained history from runtime measurements. An Outcome rates chart includes successful completion alongside unsuccessful outcomes, so successful work remains visible. If a chart excludes success, name it Failure rates instead.
- Preserve the instrument's quantity and unit when aggregating histograms. Only time measurements belong in latency summaries. Convert supported time units explicitly; never relabel bytes/s as milliseconds. Rate axes must allow fractional items/s and avoid repeated rounded tick labels. Check series names, values, units and scope against their emitting instrument.
- Keep realtime retained samples bounded; separate live activity from historical selections. Show an honest no-data state instead of fabricating zero points or healthy status.
- Label whether summary cards describe all retained work, a filtered selection, or the current page. A bounded list length is not an observed global total.
- State, outcome, delivery, completeness, and persistence evidence are different facts. Preserve these distinctions in badges/tooltips and summaries. A diagnostics display limit should say which rows are hidden and whether it affects capture.
- Health pages distinguish missing registration, degraded, unhealthy, and measured checks. Keep units and observation time visible for metrics and runtime snapshots.
- Console/log-stream panels own their connections and subscriptions. Follow the existing start/stop/dispose lifecycle when toggling, replacing, or leaving a panel. Bound browser-retained output and expose connection/error state.

## Documentation screenshots

- Store dashboard captures in `docs/assets/dashboard/` using the existing numbered names (`03-system-system.png`). Put extra tab captures of one page in a subfolder (`system/process.png`, `system/environment.png`) and link them from the feature guide.
- Capture the `main` element at 1x scale in a 2000x1700 viewport, about 1740px wide, signed in and with auto refresh off.
- Replace application names, paths, machine and user names with fictional values (`Contoso.Orders`, `C:\src\orders-export`, `DEV-WORKSTATION`, `developer`). Documentation never shows repository example applications.
- After changing a page's layout, search `docs/` for its screenshots. Recapture them, or list them as stale in the handback.

## Browser preferences

- Use `window.bdkDashboard.state` for persistent UI preferences and `.session` for tab-local restoration. Use `getJson`/`setJson` for structured values and provide validators for constrained inputs. The helper handles unavailable storage and malformed JSON.
- Preserve legacy keys and formats when migrating a control. For new preferences, use `state.key(feature, preference, route)` to include the configured dashboard root and view scope. The shell's Reset dashboard state action clears the dashboard key families in both stores and prevents unload handlers from saving discarded values. See [StateScript](../../../../src/Presentation.Web/Dashboard/Pages/StateScript.cshtml) and the [browser-state reference](../../../../docs/features-presentation-dashboard.md#browser-state).

## Validation

- Exercise populated, empty, unavailable, failed, long-name, and unsupported-capability states. Verify custom dashboard paths, disabled features, page/card grouping, saved views, exact identifiers, and safe text rendering.
- Test meaningful query/action/rendering behavior with the existing TestServer and unit-test patterns. Use focused checks for plain markup or copy edits. Run builds and top-level test commands sequentially in the worktree.
- Dashboard pages need few unit tests. Cover routes, view resolution, authorization and query contracts; check layout and interaction in a live page instead of writing tests that copy markup.
- Tab check: open every view route directly, click each tab, set refresh to the shortest interval and confirm the view and nav survive, confirm the sidebar link goes to the remembered view, and look for page errors. Compare against another page's tabs side by side. In Playwright, match tab names with `exact: true`, because `.NET` also matches `ASP.NET`.
- Keep each file's line endings and BOM when editing `.cshtml` and `.cs` files. A whole-file line in `git diff --stat` usually means they changed.
- For shared-shell changes, broaden presentation tests and inspect representative page families. For browser work, check narrow and wide layouts, light/dark themes, keyboard access, menu bounds, modal edits, refresh races, and the browser console/network results.
- Verify real running pages when browser testing is requested. Reuse the session's required browser tools and repository launch instructions. Do not mistake a standalone visual sketch for proof that the integrated page works.
