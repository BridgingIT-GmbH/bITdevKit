# Dashboard follow-up completion

Implemented the twelve adjustments in the [assessment](dashboard-followup-assessment-2026-11-04.md). The existing catalogs, routed tabs, workload charts, capability checks and exact identifiers remain in place.

## Completed adjustments

| Finding | Result |
| --- | --- |
| A1. History order | Jobs Occurrences, Messaging Messages and Queueing Messages now order by immutable creation time descending, then ID. Both continuation and initial boundaries use that tuple through `DashboardHistoryCursor`. Only these lists' remembered cursor histories were versioned; batch preferences remain independent. |
| A2. Metric quantities | Time histograms normalize seconds, milliseconds, microseconds and nanoseconds to milliseconds. Other histograms retain their units in `Histograms` and an Other measurements table. Latency highlights contain only durations. Compact metric cards wrap labels and keep values visible. |
| A3. Documents containment | Narrow grids, panels, headers and table wrappers constrain populated results to the phone viewport. Header actions wrap; wide columns scroll inside the table. The populated list and open editor both measured 390 px document width in a 390 px viewport, compared with 1038 px before. |
| A4. Inspectors | Jobs has Overview, Messages and Properties tabs. Orchestrations has Details, Context, History, Signals and Timers tabs. Summary/navigation remain above the scrolling body, and reopening an item resets its first tab. Shared detail-table styles wrap labels and values in Jobs, Orchestrations, Health and MCP host details. Entities separates optional metadata into a disclosure and retains right-aligned close controls. |
| A5. Profiling continuation | Operations and Requests place Load more, the batch selector and cumulative loaded amount below the items. Exhaustion keeps the button visible and disabled. Grouped views distinguish groups from individual rows. Apply/Reset shares the primary criteria row. Batch changes use the applied selection while retaining unsaved criteria. Insights labels its analytical bound Group limit. |
| A6. Mobile criteria and availability | Auditing and Profiling expose secondary criteria through responsive disclosures. Badges describe selected form fields and ignore empty serialized dimensions. History summaries use a compact three-column phone layout. Unregistered Change History shows availability before inactive controls. Configured Change History groups secondary criteria and places its page-size selector beside paging. |
| A7. Time and scope | Logs/Errors and operational/storage detail timestamps display an explicit UTC zone. Summary labels state their retained/scheduler scope; Orchestrations names the fields used for its totals and clarifies exact-state semantics. |
| A8. Rate axes | Storage item-rate charts use general numeric tick formatting, so fractional items/s does not round several ticks to the same integer. |
| A9. Environment search | The variable search now has the accessible name Filter environment variables. |
| A10. Short panels | Jobs, broker histories, Documents and Blobs size to their contents with bounded result scrolling. Files, Console and log streams retain their workspace layouts. |
| A11. Catalog discovery | MCP Operations searches names, features, toolsets, handlers and other displayed text locally. It shows matching/total counts, retains search during refresh and provides a named reset icon. It remains a complete catalog without artificial pagination. Health filtering remains an optional registry-growth idea rather than a required defect fix. |
| A12. Application pages | Profiling Lab uses one toolbar with shared refresh controls and collapsible procedural help. API Requests uses the shared toolbar/divider structure without adding polling. Weather JSON copying uses the shared helper and no offscreen focusable textarea. |

## Before and after

The comparisons use the assessment captures and new captures at the same viewport sizes. The sample host was restarted to load compiled Razor changes and recreated its development data, so record counts and identities differ. These comparisons establish layout and behavior rather than identical application data.

- [Phone inspectors, Profiling criteria and unavailable Change History](assets/dashboard-fixes-2026-11-04/comparison-phone-inspectors.png)
- [Phone storage editor, MCP search, entity metadata and application controls](assets/dashboard-fixes-2026-11-04/comparison-phone-content.png)
- [Desktop metric units, result heights and MCP details](assets/dashboard-fixes-2026-11-04/comparison-desktop.png)

The [verified Metrics capture](assets/dashboard-fixes-2026-11-04/verified-metrics-units-1440.png) includes a real blob transfer-rate measurement in By/s outside latency highlights. The [populated Documents capture](assets/dashboard-fixes-2026-11-04/verified-documents-populated-390.png) uses a deliberately long key to exercise containment.

## Validation

| Check | Result |
| --- | --- |
| Full Presentation suite | 1469 passed, 0 failed, 0 skipped. Includes six new cases for chronological seek behavior, histogram units and missing registration, plus updated existing paging/date-filter coverage. |
| JavaScript copy/refresher tests | 12 passed, 0 failed. |
| WeatherFiesta build | Succeeded with 0 warnings and 0 errors. |
| Route sweep | All 52 page-level routes captured at 1440 × 1000 and 390 × 844, 104 route captures. All returned HTTP 200, with no captured browser page errors, failed responses, query-error markers or document overflow. |
| Dialogs | 12 dialog types checked at both widths. Escape closed all twelve. Jobs and Orchestrations fit their detail values and labels and reset to the first local tab on reopen. |
| Themes | Representative Jobs Diagnostics, Documents and MCP Connections captures in Catppuccin Latte and White, in addition to the Tokyo Night route sweep. |
| History ordering | Live Jobs, Messaging and Queueing records were descending by creation time. The seek test covers ties, an older record with a larger GUID and arrivals between requests. |
| Profiling | Appended from 100 to 184 visible rows in the recorded check. The loaded count matched, the exhausted button stayed visible/disabled, and a size change preserved an unapplied draft while querying the applied selection. |
| Analytical modes | Slow and By count each appended a second batch. Their counters respectively showed two rows and two groups, matching the rendered items. |
| MCP | Search reduced 71 operations to one, survived refresh and reset to all 71. |
| Metric units | Read the existing 37-byte blob, observed its By/s histogram outside latency highlights, and checked compact value containers for overflow. Unit tests cover four time-unit conversions. |
| Test data | Created one document for the populated long-key check, then removed only that exact document. Used the sample alive/hello workflows to populate operational histories and diagnostics. No throughput/stress workload or external email/message send was invoked. |

Commands:

```powershell
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo /p:UseSharedCompilation=false
node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs tests/Presentation.UnitTests/Web/Dashboard/dashboard-copy.test.cjs
dotnet build examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/WeatherFiesta.Presentation.Web.Server.csproj --no-restore --nologo /p:UseSharedCompilation=false
```

The [route manifest](assets/dashboard-fixes-2026-11-04/manifest.json), [dialog checks](assets/dashboard-fixes-2026-11-04/detail-checks.json), [interaction checks](assets/dashboard-fixes-2026-11-04/interaction-checks.json), [metric checks](assets/dashboard-fixes-2026-11-04/metric-checks.json) and [cleanup record](assets/dashboard-fixes-2026-11-04/cleanup.json) contain the execution evidence. No full-solution or external-provider qualification is claimed.

The final analytical-mode checks have their own [results](assets/dashboard-fixes-2026-11-04/mode-checks.json), [Slow footer capture](assets/dashboard-fixes-2026-11-04/verified-profiling-slow-footer-390.png) and [By count footer capture](assets/dashboard-fixes-2026-11-04/verified-profiling-bycount-footer-390.png).

The shared browser accepted two phone resize requests while retaining a 1280 px viewport, then reported `Preview automation evaluate timed out after 15000ms.` Verification continued through the repository Playwright helper. All review browsers and the owned preview tab were closed. The existing WeatherFiesta instance was updated and remains available at `https://localhost:5211`; no extra sample host was left running.

## Documentation and skill

Updated the [Metrics guide](../docs/features-metrics.md), [dashboard patterns](../.agents/skills/dashboard-pages/references/page-patterns.md) and [source map](../.agents/skills/dashboard-pages/references/source-map.md). The skill now points to the chronological cursor helper and records the inspector-table, local-tab, selection-badge and grouped-footer rules.

Existing public documentation screenshots of affected pages are stale. Local application captures remain outside `docs/`; they must be recreated with fictional names and paths before becoming public documentation images. This includes the Metrics App, Jobs, Health details, storage explorers, Profiling and broker/orchestration detail screenshots where present. The local before/after captures above show the current implementation.
