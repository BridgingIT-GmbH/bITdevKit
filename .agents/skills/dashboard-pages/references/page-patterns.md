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
- For master/detail pages, use flexible columns with `min-width: 0`, `min-height: 0`, and independent scrolling regions. A long table should not push the selected detail or toolbar off the page. Jobs and Messaging demonstrate this.
- Keep responsive tables within `.table-responsive`. Bound long lists and histories; make headers sticky only within the intended scroll owner. Avoid viewport heights copied from another page without checking its toolbar, tabs, mobile behavior, and detached window.
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
- Put `data-dashboard-view` on explicit tab links or the feature view container. Saved state must not rewrite a deliberate tab choice, Reset, paging, or another query-bearing action. The handler matches more specific view paths before a root view.
- Keep filters discoverable through domain labels. Use `contains` matching for ordinary key/type investigation where the query contracts support it. Do not silently change a field advertised as exact.
- Keep raw filter-model editors and implementation details out of ordinary investigation controls. Quick filter buttons may use an internal serialized transport while exposing a simple action to the user.
- Apply/Reset use text. Small secondary actions can be icon-only with a tooltip and accessible label. Preserve query parameters during paging and use URLs for shareable filter selections.
- Use `DashboardDateTime.ParseUtc`, `FormatInput`, and `FormatQuery` for UTC filters. The input has no zone, while the query serialization retains offsets/precision correctly. Reuse `DateRange` with `DashboardDateRangeModel` for quick UTC windows.
- Define empty-date behavior explicitly: a rolling live window and a fixed historical interval are different modes. Avoid making a historical selection drift on automatic refresh.

- Quick filter chips are toggle controls. Restore their selected style and aria-pressed state from the submitted filter on every load, allow deselection, and avoid adding duplicate criteria on Apply.

- For append loading, use a compact bottom Load more button, as [Logs](../../../../src/Presentation.Web/Logging/Dashboard/Pages/Index.cshtml) and [Auditing](../../../../src/Presentation.Web.Auditing/Dashboard/Pages/LoadMore.cshtml) do. Request the next batch with the applied query and batch size, rather than unsaved form inputs. Deduplicate appended rows by their exact IDs, disable the button during loading, and retain rows and the next-page or cursor value on failure so retry requests the same batch. Hide the button at the end and preserve keyboard focus when removing it. Keep detail handlers outside appended fragments or delegate them from a stable owner.

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

- The shared refresher uses `cache: 'no-store'`, cancellation, generation checks, and serialized refresh work. It ignores late responses, pauses when the document is hidden, and preserves displayed data on failure with a stale-state message.
- Set `shouldRefresh` around modal editors, pending actions, and other workflows that must not lose edits or selection. Cancel stale selection requests before applying new responses.
- Use `preserveActiveTab` for Bootstrap tabs inside a refreshable fragment. Add stable `data-preserve-key` values to native `details` whose open state should survive replacement.
- Reinitialize page-specific controls in `onSuccess`; use `applyResponse` when replacing content also requires cleanup, state restoration, or chart disposal. Preserve scroll and selected record independently from the filtered list.
- Initialize tooltips for new fragment nodes and dispose instances owned by removed nodes. Existing shell initialization runs at page load; fragment replacement needs its own lifecycle handling.
- Store refresh preferences under a feature-specific key. Keep previous keys stable during refactoring. New storage access should tolerate disabled browser storage.

## Values, names, and drill-down

- Keep human display labels readable. Shorten technical namespace/type strings with `Shortener.Apply(value, maximumLength, ".")`, as Messaging does. The adaptive strategy abbreviates non-terminal segments and uses left truncation as a fallback.
- Audit keys can end in `Type.Method`. Preserve both parts when budgeting namespace abbreviation; `AuditDashboardText` demonstrates this feature-specific adaptation. Do not abbreviate the identifying type to the same prefix for every row.
- CSS clipping must preserve the end as well. For technical labels, a clipped container with `direction: rtl; text-align: left` and an inner `<bdi dir="ltr">...</bdi>` gives leading ellipsis without reversing the displayed identifier. Keep this separate from ordinary display-name styling.
- Use the original complete value for `title`, copy actions, IDs, route parameters, and queries. Never send a shortened identifier to an action endpoint. Exact detail views should retain complete, copyable values.
- Use `DashboardCorrelationLink.Render` for server-rendered correlation links/copy buttons and `window.bdkDashboard.appendCorrelationLink` for client-rendered details. Render every displayed correlation identifier through these helpers so it links to the filtered Logs dashboard and provides copying. Preserve the full identifier and existing log-query constraints.
- Escape values with Razor. For dynamic values, use DOM construction and `textContent`; use `innerHTML` only for trusted server-rendered fragments. Validate external detail/download links using the feature's existing rules.

## Actions, dialogs, and export tools

- Use the shared `window.bdkDashboard.confirmAction` and `showMessage` components where the existing workflow requires confirmation or action feedback. Keep theming and keyboard behavior consistent.
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

## Validation

- Exercise populated, empty, unavailable, failed, long-name, and unsupported-capability states. Verify custom dashboard paths, disabled features, page/card grouping, saved views, exact identifiers, and safe text rendering.
- Test meaningful query/action/rendering behavior with the existing TestServer and unit-test patterns. Use focused checks for plain markup or copy edits. Run builds and top-level test commands sequentially in the worktree.
- Dashboard pages need few unit tests. Cover routes, view resolution, authorization and query contracts; check layout and interaction in a live page instead of writing tests that copy markup.
- Tab check: open every view route directly, click each tab, set refresh to the shortest interval and confirm the view and nav survive, confirm the sidebar link goes to the remembered view, and look for page errors. Compare against another page's tabs side by side. In Playwright, match tab names with `exact: true`, because `.NET` also matches `ASP.NET`.
- Keep each file's line endings and BOM when editing `.cshtml` and `.cs` files. A whole-file line in `git diff --stat` usually means they changed.
- For shared-shell changes, broaden presentation tests and inspect representative page families. For browser work, check narrow and wide layouts, light/dark themes, keyboard access, menu bounds, modal edits, refresh races, and the browser console/network results.
- Verify real running pages when browser testing is requested. Reuse the session's required browser tools and repository launch instructions. Do not mistake a standalone visual sketch for proof that the integrated page works.
