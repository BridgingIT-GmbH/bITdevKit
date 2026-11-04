# Dashboard follow-up assessment

The findings below have been implemented. See the [completion report and comparisons](dashboard-followup-completion-2026-11-04.md) for the changes and verification.

The dashboard now has a coherent structure. Routed tabs, compact filter panels, circled-X reset controls, bottom continuation controls and the shared diagnostics charts work as a recognizable system. The next pass should fix data meaning and containment first. Another broad redesign would add little value.

This assessment covers 52 page-level routes, including 48 built-in routes and four application-owned routes. Each route was opened and captured at 1440 × 1000 and 390 × 844. No product code was changed for this review. The dashboard skill was updated separately with the reusable rules.

## Scope and evidence

- Reviewed the running WeatherFiesta instance at `https://localhost:5211/_bdk/dashboard` on 2026-11-04. The checkout base was `a098ac849e3e5ecf6513bab96e8031d0eb86e1b3`, with the ongoing dashboard implementation already present as working-tree changes.
- Captured 104 route/viewport combinations and inspected the desktop captures and phone contact sheets. Checked route responses, browser errors, failed requests, overflow, visible control names, filter placement, table containment, tabs and continuation controls.
- Opened 12 dialog types at both widths. Checked the broker Processing tabs, selected Entities state and Escape dismissal for all 12. Reviewed Jobs Diagnostics, Documents and MCP Connections in Catppuccin Latte and White in addition to the default Tokyo Night sweep.
- Verified real Logs continuation from 50 to 100 rows. All 50 original IDs remained, and all 100 resulting IDs were unique.
- The baseline sweep returned HTTP 200 for all routes, with no captured browser page errors, failed HTTP responses or unnamed visible icon buttons. Follow-up dialog checks also recorded no page errors. The populated Documents follow-up exposed overflow that its initially empty state did not show.
- The shared preview could not produce an automation snapshot after two attempts. The reported error was `Preview automation snapshot timed out after 15000ms.` Review continued with the repository Playwright helper. Review browsers were closed; the existing sample host was left running.

The host continued its ordinary background work during the review, so counts and records differ between captures. No workloads, throughput tests, saves, uploads, deletes or operational pause/resume actions were invoked. Normal page rendering can connect Console or log streams and run health probes. Token disclosures stayed closed; raw orchestration context was masked in its detail captures.

Evidence is under [dashboard-review-2026-11-04](assets/dashboard-review-2026-11-04/manifest.json). The [dialog checks](assets/dashboard-review-2026-11-04/detail-checks.json) and [interaction checks](assets/dashboard-review-2026-11-04/interaction-checks.json) record the follow-up measurements. These are local application captures, outside the public documentation tree.

## Keep the changes already made

- Keep the common order of refresh toolbar, routed tabs, filters, scoped summaries, results and bottom continuation. Filters now sit directly below the tabs on the storage explorers.
- Keep Apply as text and Reset as a named circled-X icon. Logs and Errors correctly put the pair in their first filter row.
- Keep Load more, Rows per load and the cumulative loaded amount together below growing lists. Exhausted controls remain visible and disabled while the selector stays enabled.
- Keep Jobs as a full-width complete filtered registration catalog. Its Occurrences tab is the paged history and now exposes the occurrence ID with a copy action.
- Keep Orchestrations Overview as an unfiltered registration catalog with definition metadata and diagrams. Instances has the orchestration selector, exact instance IDs and its own bottom continuation; Diagnostics owns runtime charts.
- Keep System's four routes and MCP's four routes. Their subjects are much easier to scan separately.
- Keep the broker dialog tabs and subscription icon actions. Their narrow detail tables fit the dialog, and the actions remain alongside the subscription text.
- Keep the Entities close-button fix. The measured right gap is 8 px at both tested widths.
- Keep the shared four-chart workload layout, including successful completions in Outcome rates. Current Jobs, Messaging, Queueing and Orchestrations charts showed activity. Idle storage rates and absent duration samples alone are not evidence of a chart failure.

## Prioritized adjustments

P1 means data can mislead an investigation. P2 means an interaction, value or important context is difficult to access. P3 means a useful refinement after those issues are resolved.

| ID | Priority | Adjustment | Affected pages |
| --- | --- | --- | --- |
| A1 | P1 | Order histories chronologically and carry that order through continuation | Jobs Occurrences, Messaging Messages, Queueing Messages |
| A2 | P1 | Preserve metric quantities and units; expose compact summary values | Metrics, especially App |
| A3 | P2 | Contain populated Documents on a phone | Documents Explorer and its editor workflow |
| A4 | P2 | Apply the broker inspector pattern to dense or wide details | Jobs, Orchestrations, Health; selected Entities metadata |
| A5 | P2 | Finish Profiling's continuation and filter-action alignment | Profiling Operations and related analytical views |
| A6 | P2 | Reduce mobile criteria density and handle missing registration first | Auditing, Profiling, Change History, operational histories |
| A7 | P2 | Make timestamp zones and summary scope explicit | Logs, Errors, histories, storage details and shared summaries |
| A8 | P2 | Allow fractional rate ticks | Files, Documents and Blobs Diagnostics |
| A9 | P2 | Name the environment-variable search field | System Environment |
| A10 | P3 | Remove excessive blank panel height from short results | Jobs, Documents, Blobs and broker histories |
| A11 | P3 | Add simple catalog discovery controls where volume warrants them | MCP Operations; optionally Health |
| A12 | P3 | Bring application-owned page chrome into line | Profiling Lab, API Requests and clipboard helper |

### A1. Recent histories are ordered by GUID

Jobs Occurrences displayed Created ages such as 10 minutes, 15 minutes, 5 minutes and under one minute in the same descending-ID list. Messaging and Queueing showed the same jumps. These are real ordering differences, not timestamp formatting alone.

The queries order by `OccurrenceId` or `Id` descending and use that identifier for seek boundaries. See [Jobs Data](../src/Presentation.Web.Jobs/Dashboard/Pages/Data.cshtml) at lines 382–403, [Messaging Data](../src/Presentation.Web.Messaging/Dashboard/Pages/Data.cshtml) at line 262 and [Queueing Data](../src/Presentation.Web.Queueing/Dashboard/Pages/Data.cshtml) at line 281. A GUID alone does not establish creation order.

Order by immutable creation time descending, then the exact ID. Update the continuation cursor and initial snapshot boundary together. Preserve applied criteria, deduplication and retry state. Orchestrations Instances already provides a composite creation-time/ID reference.

Acceptance: mixed creation times and equal timestamps remain in chronological order across batches; new arrivals between requests neither skip nor duplicate the bounded history. The newest item appears first even when its GUID sorts below an older item.

Evidence: [Jobs Occurrences](assets/dashboard-review-2026-11-04/jobs-occurrences-1440.png), [Messaging Messages](assets/dashboard-review-2026-11-04/messaging-messages-1440.png), [Queueing Messages](assets/dashboard-review-2026-11-04/queueing-messages-1440.png).

### A2. Metrics turns a transfer-rate histogram into latency

Metrics App includes `blobstorage_bytes_per_second` in Latency Highlights and labels its average as milliseconds. The producer records `By/s`. The snapshot service places every observed histogram into `Durations`, and the descriptor/rendering paths label those values as `ms`.

See [MetricsSnapshotService](../src/Presentation.Web/Metrics/MetricsSnapshotService.cs) at lines 204–220, 254 and 453, [Metrics content](../src/Presentation.Web/Metrics/Dashboard/Pages/Content.cshtml) at lines 113–126, and [MetricsBlobStoreClientBehavior](../src/Application.Storage/Blobs/Behaviors/MetricsBlobStoreClientBehavior.cs) at lines 221–225.

Carry quantity and unit through collection and aggregation. Only time measurements belong in latency summaries; convert their units explicitly. Keep transfer-rate histograms in a correctly named section. Do not sort unrelated units together as if the numeric values were comparable durations.

The compact Top Current and Latency Highlights cards also devote most of their width to technical labels. Their values can sit off to the right in a scrollable table. Use wrapping or shortened labels with full tooltips and a visible value column. Wide metric detail tables can retain local scrolling.

Acceptance: a rate histogram never appears as milliseconds, genuine durations retain their correct conversion, and a compact card shows its value without horizontal scrolling.

Evidence: [Metrics App](assets/dashboard-review-2026-11-04/metrics-1440.png).

### A3. Populated Documents escapes the phone viewport

The empty Documents capture fit a 390 px viewport. With background-created documents present, the document width became 1038 px. The same width was measured before opening the editor, while it was open and after closing it. The editor exposes the overflow but does not cause it.

The overflowing nodes include the result panel/header, result body and table wrapper. [Documents Content](../src/Presentation.Web.Storage/Documents/Dashboard/Pages/Content.cshtml) already uses a bounded desktop grid, but its narrow rules change the layout to `1fr` and visible overflow. The panel/header also need their own width and wrapping constraints.

Constrain the narrow grid and its children, wrap result-header actions/counts, and keep wide columns inside the table's scrolling wrapper. Preserve the bottom footer and long key suffixes.

Acceptance: the populated list and open editor keep document width within the viewport at 390 px. The table alone can scroll horizontally, and creation, detail and continuation controls remain reachable.

Evidence: [populated Documents with editor](assets/dashboard-review-2026-11-04/document-editor-dialog-390.png) and [before/open/closed measurements](assets/dashboard-review-2026-11-04/interaction-checks.json).

### A4. Some inspectors still behave like wide list tables

At 390 px, Jobs, Orchestrations and Health detail tables measured 672 px inside roughly 362 px containers. Labels remain visible while values require sideways scrolling. Messaging and Queueing measure 362 px in the same space and wrap their values correctly.

The shared [layout](../src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml) applies a 42 rem minimum to responsive tables at narrow widths. Keep that behavior for actual multi-column lists, but introduce or reuse a detail-table style that wraps within its inspector.

Jobs also stacks Messages, Details and Properties. Orchestrations stacks Details, Context, History, Signals and Timers. Apply local tabs to these substantial subjects, keeping summary/navigation fixed above the scrolling body. Entities could separate business fields from audit/concurrency/raw metadata once the selected model becomes dense. Its right-aligned close control already works and should remain.

Small schema, health-data, blob and permalink dialogs can remain single-pane. Health primarily needs fitting key/value rows. Profiling and Auditing already use useful disclosures; their real multi-column segment/change tables can retain local scrolling.

Acceptance: phone detail values are readable without scrolling the whole inspector sideways; switching local tabs preserves identifiers and actions, resets body scroll appropriately, and Escape returns focus to the opener. Open a different item on the default tab.

Evidence: [Jobs detail](assets/dashboard-review-2026-11-04/jobs-occurrence-dialog-390.png), [Orchestration detail](assets/dashboard-review-2026-11-04/orchestration-instance-dialog-390.png), [Health data](assets/dashboard-review-2026-11-04/health-data-dialog-390.png), [broker reference](assets/dashboard-review-2026-11-04/messaging-message-dialog-390.png), [Entities detail](assets/dashboard-review-2026-11-04/entity-details-dialog-390.png).

### A5. Profiling Operations has an older continuation layout

Operations puts Rows in the upper filter form. Its real Load more control appears below results only while a next cursor exists, then disappears. Apply/Reset sit in another row beside Advanced filters rather than beside the final criteria.

See [Operations Index](../src/Presentation.Web/Profiling/Dashboard/Pages/Operations/Index.cshtml) at lines 24–35 and [Operations Data](../src/Presentation.Web/Profiling/Dashboard/Pages/Operations/Data.cshtml) at line 57. This is actual cursor continuation, so the bottom footer contract applies. Preserve the distinction between grouped results and recent executions when connecting its count and append state.

Move Rows per load beside Load more, show the cumulative loaded amount, retain the exhausted disabled button and align Apply/Reset with the criteria. For other analytical views, first determine whether a control limits a ranked query or pages records. Label a genuine query limit explicitly rather than moving every numeric selector mechanically.

Acceptance: continuation preserves the submitted selector, loaded groups and execution details; exhaustion leaves the standard footer visible. Changing batch size starts a new query consistently. The action pair shares a criteria row on desktop and wraps together on a phone.

Evidence: [Operations desktop](assets/dashboard-review-2026-11-04/profiling-operations-1440.png), [Operations phone](assets/dashboard-review-2026-11-04/profiling-operations-390.png).

### A6. Mobile filters and unavailable features need less initial density

Auditing, Profiling and Change History can place most or all results below the first phone screen. Broker and orchestration histories also repeat a six-card summary between the filters and list. The content is useful, but secondary identifiers and advanced criteria should not dominate the initial view.

Keep a small primary investigation row and expose secondary criteria through a disclosure with an applied-filter indication. Retain Logs/Errors' established action placement. Consider a more compact summary for a history tab while preserving its scope and links.

Change History has no registrations in this host, yet renders the full active-looking form before saying so. [Change History Content](../src/Presentation.Web.EntityFramework/ChangeHistory/Dashboard/Pages/Content.cshtml) renders the missing-registration message after the form at line 148. Show that availability state first and omit or disable irrelevant controls. When populated behavior is checked later, move its batch-size control out of the filter form if it governs continuation.

Acceptance: a phone user can reach the list without traversing a screenful of inactive or secondary controls. Applied advanced criteria remain visible and editable. An unregistered feature clearly explains availability before its controls.

Evidence: [Auditing phone](assets/dashboard-review-2026-11-04/auditing-390.png), [Change History phone](assets/dashboard-review-2026-11-04/change-history-390.png), [Orchestration Instances phone](assets/dashboard-review-2026-11-04/orchestrations-instances-390.png).

### A7. Timestamp zones and count scopes need explicit labels

Logs and Errors use UTC criteria and a UTC toolbar, but row timestamps use local time without a zone in their label. [LogEntriesDashboard](../src/Presentation.Web/Logging/Dashboard/LogEntriesDashboard.cs) at lines 162–174 formats `LocalDateTime`. Several inspector dates and permalink dates use local formatting as well.

Use UTC for diagnostic absolute timestamps, or explicitly label local time and its offset. Preserve relative displays with an exact zoned tooltip. Auditing and Profiling already provide explicit UTC references.

Also label whether cards describe all retained work, the submitted filter or the loaded rows. Jobs Occurrences shows registration-level counts such as Enabled and Paused; broker summaries can describe a broader scope than their message filters. These scopes may be intentional, but the label must make them clear.

Acceptance: copying or comparing a timestamp never requires guessing its zone, and narrowing a list leaves no ambiguity about unchanged global counts.

Evidence: [Logs](assets/dashboard-review-2026-11-04/logentries-1440.png), [Jobs Occurrences](assets/dashboard-review-2026-11-04/jobs-occurrences-1440.png).

### A8. Storage item-rate axes round fractional ticks to repeated integers

The shared [Storage Diagnostics](../src/Presentation.Web.Storage/Pages/Diagnostics.cshtml) forces `tickformat = ',d'` for the items/s chart at line 252. A small automatic range can therefore show repeated 0 and 1 labels. Items per second can be fractional even though the underlying item count is integral.

Use adaptive decimal precision or a coherent tick step appropriate to the rate. Keep the existing four chart subjects and metric sources.

Acceptance: both low rates and larger throughput values have distinct, readable tick labels with an items/s unit.

Evidence: [Storage diagnostics](assets/dashboard-review-2026-11-04/storage-blobs-diagnostics-1440.png).

### A9. The environment search has no accessible name

The Environment Variables search input relies on its placeholder. [SystemEnvironment](../src/Presentation.Web/Dashboard/Pages/SystemEnvironment.cshtml) at line 98 has no associated label or accessible name.

Add a concise label or `aria-label`, keeping the existing local filtering. The automated sweep found no unnamed visible icon buttons, so this should be a focused fix rather than a broad control rewrite.

Acceptance: assistive technology identifies the field as filtering environment variables, and keyboard filtering behaves as before.

### A10. Short result panels reserve too much empty space

Jobs shows six registrations in a tall result panel. Documents and Blobs can leave a large blank tail below a short result and its footer. Broker histories use a similar fill pattern even with only a few messages.

Use content-sized cards with a maximum height for long results. Retain deliberate workspace height for Files' folder/content panes, Console and log streams. Check the sidebar and main scroll owners together before changing heights.

Acceptance: short and empty lists end near their content/footer; long lists still scroll within their intended owner without losing controls.

Evidence: [Jobs](assets/dashboard-review-2026-11-04/jobs-1440.png), [Blobs](assets/dashboard-review-2026-11-04/storage-blobs-1440.png), [Documents](assets/dashboard-review-2026-11-04/storage-documents-1440.png).

### A11. Large static catalogs could be easier to search

MCP Operations lists 71 operations and produces a roughly 3147 px desktop document. The new tabs solve the mixed-subject clutter, but finding one operation still involves scanning the whole catalog.

Add a simple local operation search and, if useful, toolset/feature selection with a matching/total count. Keep the full catalog available; it does not need artificial Load more. Health could gain name/status/tag filtering if real registries grow enough to warrant it.

Acceptance: users can find an operation by its displayed name, feature or handler and clear that selection without losing schema inspection.

Evidence: [MCP Operations](assets/dashboard-review-2026-11-04/mcp-operations-1440.png).

### A12. Application-owned pages should reuse the shell more closely

Profiling Lab has its own navigation and a separate status/refresh row with substantial procedural text. API Requests starts directly with its workload controls and lacks the ordinary toolbar/divider structure. These are application-owned views, so their domain forms should remain distinct.

Reuse shared toolbar/navigation primitives where they match actual page behavior. Move lengthy instructions into a help disclosure, retaining information required to choose or understand an action. Do not invent polling for a command form.

The application overview's `selected-city-weather-json` textarea is an offscreen copy transport, not a normal visible field. Review its focus handling and reuse the shared copy helper where suitable. See [OverviewContent](../examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Dashboard/Pages/OverviewContent.cshtml) at lines 85 and 148.

Acceptance: application views have familiar chrome and keyboard behavior while their business controls remain clear. Copying JSON does not leave focus on an offscreen textarea.

Evidence: [Profiling Lab](assets/dashboard-review-2026-11-04/app-core-profiling-1440.png), [API Requests](assets/dashboard-review-2026-11-04/app-core-api-requests-1440.png).

## Coverage by page family

All paths below are relative to `/_bdk/dashboard`. Every listed route has both desktop and phone evidence in the manifest.

| Family | Routes reviewed | Assessment |
| --- | --- | --- |
| Dashboard home | `/` | Feature grouping and icons are consistent. Keep the card navigation; no structural change proposed. |
| Identity | `/identity` | Principal/authentication inspection fits its purpose. Token disclosures stayed closed. No additional routed tab proposed. |
| System | `/system`, `/system/process`, `/system/environment`, `/system/diagnostics` | Four subjects are well separated. Fix the environment search name, A9; preserve chart/source distinctions. |
| Metrics | `/metrics`, `/metrics/dotnet`, `/metrics/aspnet`, `/metrics/routes` | Routed views and tables work. Unit classification and compact value visibility need A2. |
| Profiling | `/profiling/runtime`, `/profiling/operations`, `/profiling/requests`, `/profiling/timeline`, `/profiling/insights` | Keep specialized analytical views and detail disclosures. Finish footer/action consistency and reduce mobile criteria density, A5/A6. Runtime had no active session. |
| Health | `/health` | Status cards distinguish healthy and failed checks. Detail values need A4; catalog filtering is optional under A11. |
| Console | `/console` | Preserve connection state and terminal workspace. No forced tab split or list footer. No commands were executed. |
| MCP | `/mcp`, `/mcp/connections`, `/mcp/handlers`, `/mcp/operations` | Tabs resolve the main clutter. Connections' key/value presentation should follow A4 on phones. Operation discovery is A11. Offline server-session state was visible. |
| Jobs | `/jobs`, `/jobs/occurrences`, `/jobs/diagnostics` | Full-width complete catalog, occurrence IDs and four-chart diagnostics are good. Fix chronological history A1, inspector A4, scope A7 and excess height A10. |
| Logging | `/logentries`, `/errors`, `/logentries/stream` | First-row Apply/Reset and real append behavior are good. Clarify zones A7 and review mobile criteria A6. Preserve the stream workspace. |
| Auditing | `/auditing`, `/auditing/diagnostics` | Keep execution drill-down, disclosures and source diagnostics. Reduce mobile criteria A6; retain explicit UTC and source/persistence distinctions. |
| Messaging | `/messaging`, `/messaging/messages`, `/messaging/diagnostics` | Tabs, subscription actions, broker dialog tabs and runtime charts are useful references. Fix history order A1 and clarify summary scope A7. |
| Queueing | `/queueing`, `/queueing/messages`, `/queueing/diagnostics` | Same successful structure as Messaging. Fix history order A1 and clarify summary scope A7. Preserve queue/type/circuit action distinctions. |
| Broadcasting | `/broadcasting` | Compact node/scopes view fits its purpose. Wide node columns scroll locally. No additional tabs proposed for current content. |
| Orchestrations | `/orchestrations`, `/orchestrations/instances`, `/orchestrations/diagnostics` | Definitions, exact registration selector, bottom footer and charts now fit. Improve the dense inspector A4 and phone summary/filter density A6. |
| Files | `/storage/files`, `/storage/files/diagnostics` | Explorer is the storage spacing reference. Keep two-pane workspace; fix fractional diagnostic ticks A8. Provider contents were empty. |
| Documents | `/storage/documents`, `/storage/documents/diagnostics` | Filter/action/footer placement is correct. Initial empty and later populated states differed; populated phone overflow needs A3. Also A8/A10. Editor opened and dismissed without saving. |
| Blobs | `/storage/blobs`, `/storage/blobs/diagnostics` | Explorer actions, exact details and footer are consistent. Follow A8/A10. No upload or expiry update was performed. |
| Permalinks | `/storage/permalinks` | Filter spacing and bottom footer are aligned. Details remain appropriately small. Clarify absolute date zone A7. No expiry update was performed. |
| Change History | `/change-history` | Missing-registration state reviewed. Put availability first, A6. Populated grouping, restore and continuation remain unverified in this host. |
| Entities | `/entities` | Initial selector, selected table/footer and native details reviewed. Close alignment passes; consider a metadata split under A4 for dense models. No edit/save performed. |
| Application overview | `/app/core` | Domain summary and weather cards remain useful. Review custom clipboard focus under A12. |
| City management | `/app/core/cities` | Business create form and catalog fit the application purpose. Do not force an operational-history pager or Diagnostics tab onto it. No city was created. |
| Profiling Lab | `/app/core/profiling` | Align custom chrome and hide lengthy procedural help under A12. No workload was run. |
| API Requests | `/app/core/api-requests` | Align chrome under A12 while retaining workload controls. No request workload was sent. |

## Suggested implementation order

1. Fix chronological history contracts and metric units, A1/A2. Cover equal timestamps, continuation boundaries and mixed histogram units with focused behavioral tests.
2. Fix populated Documents containment and inspector table styles, A3/A4. Visually verify empty/populated/long-value states at phone width and preserve editor/close behavior.
3. Finish Profiling continuation, mobile criteria and unavailable-state handling, A5/A6. Review analytical query limits individually.
4. Clarify zones/scopes, rate ticks and the missing field name, A7–A9. These can be small focused changes with live rendering checks.
5. Refine short-panel height, catalog discovery and application chrome, A10–A12. Keep this last pass limited to observed user friction.

## Limits of this review

The captured state does not certify every permission, provider, failure path, detached window or theme. Missing-registration Change History, active runtime profiling, mutation success/failure, dirty-editor discard, arbitrary long payloads and refresh races were not exercised in this assessment. No synthetic failure or workload was added to create those states. Theme checks were representative visual checks, not a measured contrast audit.

The existing host was not rebuilt or restarted. No .NET build or test suite was run for this documentation-only assessment. Current checks cover route rendering, the listed interactions, evidence integrity and documentation/skill validation; earlier implementation test runs are not counted as validation of this review.

## Skill updates and validation

Updated [dashboard-pages](../.agents/skills/dashboard-pages/SKILL.md), consolidated the recent rules in [page patterns](../.agents/skills/dashboard-pages/references/page-patterns.md), and added a [review checklist](../.agents/skills/dashboard-pages/references/review-checklist.md). The skill now distinguishes complete catalogs, operational histories, diagnostics, explorers, inspectors and live workspaces. It records the ordering, unit, containment, timestamp and review-evidence lessons without treating every page as the same table layout.

Documentation validation checked four Markdown files, 61 local links, the skill's simple scalar frontmatter and its 75-line size. Capture validation checked all 52 route pairs, the 12 successful dialog Escape checks and the Logs append result. `git diff --check` passed for the assessment and changed skill files. The source map's earlier working-tree changes were preserved.
