# Dashboard consistency verification

Completed 2026-11-04 against the worktree based on a098ac849e3e5ecf6513bab96e8031d0eb86e1b3. All eight phases in [the implementation plan](pln-refactor-dashboard-page-consistency-1.md) are complete. No commit or push was requested.

## Checks

| Check | Result |
| --- | --- |
| `dotnet build bITdevKit.slnx --nologo` | Passed, zero warnings and errors |
| `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo` | 1420 passed, zero failed/skipped |
| Application UnitTests, `FullyQualifiedName~JobSchedulerQueryServiceTests` | 28 passed |
| Infrastructure UnitTests, `FullyQualifiedName~OperationalSearchSqliteTests` | 4 passed, real SQLite |
| Common UnitTests, `FullyQualifiedName~FilterModelQuery` | 10 passed |
| Custom-root/detached route and continuation rendering checks | 8 passed |
| Browser layout matrix | 44 routes x 5 widths x 3 palettes, 660 passed |
| Browser page errors | Zero |

The matrix uses actual CSS widths 1440, 1024, 768, 390 and 320, with Tokyo Night, Catppuccin Latte and White. It checks page overflow, toolbar/filter bounds, wrapped-header overlap and duplicate IDs. Screenshots were inspected to find and correct defects; the System database-card recheck replaces its earlier failing measurements. The final matrix is `.tmp/dashboard-final/verified-layout-matrix.json`.

## Observed interactions

- Messaging, Queueing and Jobs occurrences loaded at least three bounded batches. Forced failures retried the same cursor, IDs stayed unique, refresh retained appended rows, exhaustion retained a disabled button and usable selector, and size changes loaded a new first batch.
- Each list retained the same loaded identity sequence through Diagnostics navigation. Operational pages emitted no graph owners or graph polling. Charts initialized in Diagnostics and disposed on navigation; custom-root routes inherited their existing dashboard group.
- Logs refresh/export used applied filters while drafts remained visible. Failed Apply retained results; size changes preserved drafts; Reset retained batch size; append survived refresh. Auditing continuation and size changes preserved drafts. Document fragment refresh preserved an unapplied partition.
- The new-record indication and Start new list action retained draft criteria. Tools closed with Escape and returned focus to the trigger.
- Native and Bootstrap editors retained failed-save values and supported Keep editing / Discard changes. Dirty backdrops stayed open. Populated broker/Jobs contexts had Close; phone editor/details captures kept actions visible.

Actual comparison captures are under [the spec assets](../docs/specs/assets/dashboard-page-consistency/). Public documentation has 38 refreshed captures with fictionalized display names and paths, separate from actual execution evidence. Older public detail figures are explicitly identified in [the image notes](../docs/assets/dashboard/README.md).

## Scope and limits

The query adjustment aligns canonical GUID ordering and seek comparison. Broker and occurrence continuation uses immutable identity order and an upper identity boundary. It preserves deterministic browsing under the tested mutations; it does not promise chronological ordering or a transactional snapshot. Arrivals within an unvisited key range can appear in later batches. Restoration stores query/boundary/count/scroll metadata rather than row payloads and is limited to 100 batches / 20,000 rows.

SQLite tests cover equal timestamps, an arrival beyond the upper identity boundary, deletion of a loaded row and a status change of an unvisited row. Jobs tests also cover equal-time batches and new arrivals. External-service integration suites were not run; no cross-provider integration pass is claimed.

The isolated WeatherFiesta worktree instance used HTTPS 5211 and HTTP 5210. Built-in Alive actions supplied benign diagnostic rows; no stress, cleanup or purge action was invoked. The earlier DoFiesta verification instance was also stopped. All task-started hosts were stopped, and ports 5200, 5201, 5210 and 5211 were confirmed released. Runtime scripts/logs remain ignored under `.tmp/`.

Final compatibility check: existing broker `take` values retain the 1-500 cap, including custom values and the former 250/500 choices. Default remains 50. Rendering/seek tests cover 10, 40, 250 and 500 without changing the caller's applied query. The selected supported size remains visible in the footer.


Follow-up correction, 2026-11-04: removed the separate creation/upload row between storage tabs and filters. New document and Upload now live in their respective result headers, retaining their delegated commands. Files, Documents, Blobs and Jobs all measured the same 7.2px tab-to-filter gap at 1440, 390 and 320 CSS pixels, without page overflow. Jobs now provides name/group substring and enabled/paused filters. Browser checks verified Apply, Refresh with an unapplied draft and Reset; focused query coverage verifies criteria are passed before continuation. These checks supplement the original matrix above.

Occurrences filter follow-up, 2026-11-04: added independent Job, Trigger, Status and Correlation ID criteria. Apply/Reset resets occurrence continuation while preserving its batch preference and the Jobs selection. Refresh and Load more use the applied occurrence criteria. Focused query coverage checks the typed status, substring criteria and strict continuation together.

Inline action correction, 2026-11-04: Documents, Blobs, Jobs and Occurrences keep Apply/Reset beside the criteria on desktop, using a natural-width action column and flexible field width. The action pair wraps together on narrow screens.

The same inline action correction also covers Permalinks and the final Change History filter row. Change History retains its multiple criteria rows; only the terminal action placement and the natural width of its Oldest first control change.

Inline action validation: 35 focused Presentation tests passed. All six forms align action and final field baselines at 1440 and 1024 CSS pixels; all six wrap without page overflow at 390 and 320 pixels (24 browser checks). Runtime evidence is in `.tmp/inline-filter-evidence.json`; selected actual captures are beside the spec.

Storage continuation follow-up, 2026-11-04: Documents, Blobs and Permalinks now use the shared Load more / Rows per load footer below their result items. Batch selectors retain their existing IDs, form field names, supported choices and provider limits. Load more appends unique identities with each provider's opaque continuation token; failed requests retain the cursor for retry, and exhaustion keeps the disabled action beside an enabled selector. Batch changes use applied criteria and preserve unrelated drafts. The shared growing-list helper also preserves appended history during refresh. Documents retains its previous page-size preference key. Browser append tests use controlled HTML continuation pages; these are transport/UI checks, not a new provider qualification claim.

Storage footer validation: 29 focused tests passed, including real empty in-memory provider rendering. Controlled browser continuation pages verified 25 -> 50 -> 75 unique rows for Documents, Blobs and Permalinks, retry of the same cursor after a forced failure, and disabled exhaustion with usable size selection. Size changes preserved draft criteria and used applied selections; refresh retained appended rows. Nine real-page layout checks at 1440/390/320 confirmed the footer below items, no duplicate top selector and no page overflow.

Loaded-count follow-up, 2026-11-04: Entities now supplies its initial row count to the shared footer and recalculates the displayed cumulative count whenever row loading, row refresh or deletion updates the list. Documents, Blobs and Permalinks already supply initial counts and use the shared append helper's cumulative counter. Focused rendering assertions cover populated Entities and empty storage/permalink results.

Loaded-count validation: 45 focused tests passed. The running Entities list displayed four loaded rows and a controlled append updated the footer to five. Actual Documents/Blobs/Permalinks listings displayed 0/1/1 loaded rows, matching their rendered data-row counts.

Orchestration view follow-up, 2026-11-04: Overview now lists every registered definition, without instance filters or the former twelve-name cap. Instances has the existing filter bar and operational details on its own route. Diagnostics owns runtime summaries and the Realtime/History charts, with global scope independent of instance filters. Charts are absent from the other routes and dispose their polling owner when leaving Diagnostics. Public combined-view illustrations are explicitly flagged as stale in the guide and image notes.

Orchestration view validation: 36 focused tests passed (`OrchestrationDashboardTests`, `OrchestrationEndpointsTests`, `DashboardPagingTests`), including fourteen registered definitions with no instances, full-page/fragment filter agreement and custom dashboard paths. The final host rebuild succeeded. Browser checks passed for all three views at 1440, 1024, 390 and 320 pixels in dark, light and monochrome palettes (36 checks), with no page errors. Draft/Refresh/Apply/Reset behavior, the instance details dialog, both chart modes and History Week were exercised. Overview and Instances emitted no chart data requests. Final screenshots use `after-orchestrations-{overview,instances,diagnostics}-{1440,390}.png` beside the spec. The existing sample host was restored and remains available.

Reset convention follow-up, 2026-11-04: The shared FilterActions link and button variants now render the circled-X `bi-x-circle` icon, with Reset filters as both tooltip and accessible name. The standalone Logs Stream filter uses the same icon. Apply retains its text label. The dashboard skill and its pattern/source references now record inline action placement, tab-to-filter spacing, bottom Load more / Rows per load controls, cumulative loaded counts, disabled exhaustion, applied-query paging and retained drafts. The guide, specification and plan requirements use the same convention; screenshots showing text Reset are flagged as older illustrations.

Broker Messages tabs, 2026-11-04: Messaging and Queueing now separate Overview (unfiltered counts and subscriptions), Messages (filterable retained messages, details and bounded continuation) and Diagnostics (session-wide charts). Overview does not search retained messages, even with stale message-view or filter parameters in its URL. The message tables use the full content width; their existing global waiting-for-handler previews follow below. Navigation retains applied filters and restores loaded history. Reset remains the circled-X icon.

Broker view validation: 34 focused Presentation tests passed, covering both full Messages routes, fragments, custom dashboard paths, bounded continuation and readable type labels. The final host rebuild succeeded. Actual broker data verified unique append loading, cumulative loaded counts, disabled exhaustion with an enabled size selector, draft/Refresh/Apply/Reset separation, and history restoration across Overview and Diagnostics. Both brokers passed 24 route/width checks at 1440, 1024, 390 and 320 pixels, with no page errors. Captures use `after-{messaging,queueing}-{overview,messages}-{1440,390}.png` beside the spec. Public combined-view illustrations are flagged as stale. The previously running sample host has been restored.

Permalinks spacing follow-up, 2026-11-04: Bootstrap `.row` had been applied to the bordered filter form, introducing negative top and side margins. The grid now sits inside a normal filter-panel form. Its divider-to-panel gap matches Logs (about 8 CSS pixels, previously about 0.8 pixels), and the toolbar, filter border and result frame share their edges. The standalone bordered table also cancels the shared mobile table gutter with `m-0`. The dashboard skill documents both causes and the corrected structure. Nine focused Permalink/storage footer tests passed; the final host rebuild succeeded. Browser measurements matched Logs at 1280, 1440, 1024, 390 and 320 pixels, with aligned edges, inline desktop actions and no page overflow. Apply/Reset retained their behavior and selected row size, with no page errors. Before/after captures use `*-permalinks-filter-spacing*.png` beside the spec. The existing sample host remains available.

Storage diagnostics refresh regression, 2026-11-04: The shared refresher's HTML-only MIME check rejected HTTP 200 application/json snapshots before storage diagnostics could update its run state or initialize Plotly. Added explicit `responseType: 'json'` support while retaining HTML as the default, and selected it in the shared Files/Documents/Blobs diagnostics view. HTTP failures, redirects and format mismatches retain prior data; HTML query-error marker checks remain scoped to HTML. Nine dependency-free Node behavior tests reproduced three failures before the patch and now all pass. Run them with `node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs`. All 1440 Presentation unit tests passed.

Live storage verification: all three diagnostics pages rendered four charts with two read/write traces. Each completed a local 4 KiB, one-worker, one-second throughput run with measured chart points, zero errors and zero pending cleanup items. A controlled 503 response retained all four Files charts and showed stale status; the next actual JSON response cleared the status. All three pages retained four responsive charts at 390 pixels without horizontal page overflow. Logs' HTML fragment refresh also succeeded. The shared refresh format contract and regression command are recorded in the dashboard skill and feature guide. Actual after evidence is `after-storage-diagnostics-json-refresh.png` beside the spec; the sample host remains available.

Logs/Errors first-row actions, 2026-11-04: Apply and the circled-X Reset now sit at the right end of the primary filter row on both pages. Secondary identifier fields remain below, and the former standalone action row is removed. The dashboard skill, page patterns and C03 contract record this placement. Two focused log-query endpoint tests passed, and the sample host rebuilt successfully. Eight browser checks across both routes at 1440, 1024, 390 and 320 pixels confirmed first-row ownership, trailing alignment, desktop field/action baselines, action placement before secondary identifiers and no horizontal page overflow. Actual captures use `after-{logs,errors}-first-row-actions-1440.png` beside the spec. The sample remains available.
