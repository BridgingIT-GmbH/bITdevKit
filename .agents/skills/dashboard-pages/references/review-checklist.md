# Dashboard review checklist

Use this for a consistency assessment or a final visual review. Keep findings separate from product changes unless implementation is requested.

## Choose a useful reference

| Page type | Expected structure | Exceptions and reference |
| --- | --- | --- |
| Definition catalog | Routed tabs, optional catalog filters, complete matching list | Jobs shows all matching registrations with no pager. Orchestrations Overview shows every registration without filters and exposes definition metadata and diagrams. |
| Operational history | Tabs, compact filters, clearly scoped summaries, records, continuation footer | Jobs Occurrences, broker Messages and Orchestrations Instances. The footer has Load more, Rows per load and the cumulative amount loaded. |
| Diagnostics | Tabs, short source/window label, related charts with visible units | Jobs, brokers, System and Orchestrations use multiple charts. Storage keeps its capability-aware throughput controls in a closed disclosure. Retained history and process activity have different scopes. |
| Explorer | Selection and filters, result actions, contents and continuation where supported | Files needs a folder/content workspace. Documents and Blobs put creation/upload actions in the result header. Preserve provider-specific names and capabilities. |
| Inspector | Short summary, readable details, exact identifiers and actions | Use local tabs only when several substantial subjects compete for space. Broker dialogs demonstrate fixed summary/navigation and a scrolling tab body. Small schema and health inspectors can stay single-pane. |
| Live workspace | Connection/status controls, bounded output, useful working area | Console and log streams do not need an artificial Overview/Diagnostics split. Keep their start/stop/dispose behavior. |
| Analytical view | Explicit query/window, ranked groups or traces, useful drill-down | A query limit can remain with analytical criteria. Real append pagination still needs the standard bottom footer. Label which kind of limit is being changed. |
| Unavailable feature | Clear availability message before inactive controls | Missing registration differs from an empty successful query. Avoid a screenful of usable-looking filters for an unregistered feature. |

## Check layout and controls

- Match outer edges from toolbar divider through tabs, filter panel and result frame. Keep the filter panel outside Bootstrap rows so gutter margins cannot collapse its spacing.
- Keep Apply text and the circled-X Reset together at the trailing edge of the criteria row. Logs and Errors use their first row; other multi-row forms use the final primary criteria row, ahead of optional secondary disclosures. Let the pair wrap together on narrow screens.
- Keep Load more visible but disabled when exhausted. Leave Rows per load enabled, and show the cumulative amount loaded. Do not duplicate the batch selector above the list. Do not add a pager to a complete registration catalog.
- Make results reachable on a phone. Move secondary identifiers or advanced criteria into a disclosure when a large filter form and summary grid consume the first screen. Retain the applied selection and a visible way to reopen those fields.
- Bound wide list tables inside their own scrolling wrapper. Use `minmax(0, 1fr)` grid columns and `min-width: 0` flex/grid children. Test populated long-key data; an empty-state capture cannot prove containment.
- Let two-column detail tables wrap or stack within the dialog. They should not inherit a wide list table's minimum width. Keep values visible in compact metric cards as well.
- Size short catalogs and empty results to content. Keep viewport-filling layouts for actual workspaces rather than blank space below a short list footer.
- Align icon actions at the trailing edge of the related row and provide native tooltips and accessible names. Close controls need their own stable right alignment, including when a title or identifier wraps.

## Check data meaning

- Trace recent-history ordering through the query and cursor. Use immutable creation time plus an exact identifier tie-breaker. Verify equal timestamps, arrivals during paging, duplicate suppression and retry behavior. A GUID's ordering is not evidence of chronology.
- Distinguish global retained totals, filtered totals, loaded counts and runtime samples. Label their scope. Avoid presenting a bounded result count as the total.
- Trace each metric to its emitting instrument, including quantity, unit, aggregation and tags. Histograms are not necessarily durations. Preserve bytes/s, counts, percentages and time units rather than forcing every histogram into latency.
- Use rates based on actual elapsed time. Allow fractional items/s and readable axis ticks. Keep missing duration samples absent; idle and unavailable are different states.
- Include success in an Outcome rates chart. Name a chart Failure rates when it intentionally excludes success. Distinguish handler calls, messages, activity attempts and completed instances.
- Keep absolute timestamp zones clear across filters, rows, tooltips and dialogs. Use exact zoned values behind relative displays.
- Preserve full identifiers and query values even when labels are abbreviated. Use the shared copy and correlation helpers.

## Collect evidence without changing the host

1. Inventory every page-level route and remembered tab from registration and live navigation. Distinguish built-in routes from application-owned pages. Record the current revision and relevant working-tree changes.
2. Reuse the running host where possible. Read startup/reset behavior before restarting. Use the required session browser and close only tabs or hosts started for the review.
3. Capture every route at a desktop width and a phone width. Inspect the screenshots as well as DOM overflow, accessible control names, active tabs, browser errors and failed requests. Keep an inventory so no tab silently drops out of coverage.
4. Review representative dialogs, light/dark/monochrome themes, keyboard dismissal, filter/reset, real append loading and saved navigation as applicable. State which interactions were checked; screenshots alone do not prove them.
5. Set automatic refresh off using the actual page control. Storage diagnostics uses a different selector from ordinary refresh controls. Mask visible credentials and token fields without reading them; avoid masks on hidden elements that cover unrelated content.
6. Use existing data and read-only interactions. Do not run workloads, save editors, mutate registrations or change host configuration just to improve screenshots. Background host activity can still change counts between captures; record that limit.
7. List unverified states explicitly, such as an unconfigured Change History provider, no active runtime session, alternate permissions, failed refresh or an unsupported storage capability. Do not turn an unavailable state into a claim that populated behavior passed.
8. Keep raw application evidence outside public documentation. Public documentation captures use fictional names and paths under the established screenshot policy. A review report can link local evidence and exact source locations.

## Report useful findings

Prioritize wrong data, missing records and broken interactions first. Follow with overflow, inaccessible values and investigation friction. Treat optional discoverability improvements separately from defects.

For each finding, record the observed state, affected routes, source location, recommended adjustment and an observable acceptance check. Include what already works and should remain. A screenshot of a symptom and a source trace of its cause are stronger than a general preference for more tabs or less padding.

Validate documentation links and skill structure for a documentation-only review. Run builds and behavioral tests when product code changes; do not report earlier test results as checks of the current review.
