---
status: implemented
---

# Design Specification: DoFiesta Operations Log Entries

> This document captures the planned DoFiesta operations log viewer, including
> reliable tailing, quick filters, cleanup, and a log-derived trace timeline.
> The trace view is based on Seq-compatible Serilog data persisted through the
> SQL sink, not on Jaeger or a separate distributed tracing backend.

[TOC]

## 1. Summary

DoFiesta should expose a SEQ-inspired operations page at
`/operations/logentries` for persisted Serilog log entries.

The page is intended for development diagnostics and should use the existing
`LogEntryEndpoints` backend surface. The viewer must keep log entries sorted
newest-first only, support tailing, make common metadata easy to filter, and provide
a lightweight trace view when log entries contain `TraceId` and `SpanId`.

## 2. Goals

- Show persisted Serilog log entries from `core.__Logging_LogEntries`.
- Keep the main list sorted newest-first at all times.
- Make (optional) tailing reliable, with new matching entries appearing at the top.
- Provide dedicated filters for operational metadata:
  - `CorrelationId`
  - `TraceId`
  - `SpanId`
  - `LogKey`
  - `ModuleName`
  - `ShortTypeName`
- Provide broad text search against message, exception, and event payload data (properties).
- Provide row menu actions to filter by metadata values from the selected row.
- Provide a cleanup dialog backed by the existing cleanup endpoint.
- Provide a log-derived trace timeline popup for entries with a `TraceId`.

## 3. Non-goals

- Do not replace Seq.
- Do not require indexed server-side virtualization for v1.
- Do not add offset paging or total-count requirements to log queries.
- Do not build a full distributed tracing backend from log entries.

## 4. Backend Design

### 4.1 Endpoint Registration

DoFiesta should register log endpoints in development only:

```csharp
builder.Services.AddEndpoints<LogEntryEndpoints>(builder.Environment.IsDevelopment());
```

The default `LogEntryEndpointsOptions` path remains:

```text
/api/_system/logentries
```

The existing authorization behavior remains in place.

### 4.2 Query API Additions

Add `AfterId` to `LogEntryQueryRequest` and expose it as an optional
`afterId` query parameter on `GET /api/_system/logentries`.

Behavior:

- `afterId` returns entries with `Id > afterId`.
- Results remain ordered by `Id` descending.
- `afterId` is used for tailing newer rows.
- `continuationToken` remains the existing mechanism for loading older rows.
- If both `afterId` and `continuationToken` are supplied, the request is
  invalid and should return a bad request.

### 4.3 Span Filtering

Add `SpanId` to `LogEntryQueryRequest` and support it in:

- `LogEntryEndpoints.GetLogEntries`
- `LogEntryService.QueryAsync`
- `LogEntryEndpoints.ExportLogEntries`, if exports should preserve all query
  filters
- `ILogEntryService.StreamAsync` and `LogEntryService.StreamAsync`, if stream
  filters are kept aligned with query filters

Filtering should match the persisted `SpanId` column exactly.

### 4.4 Model Mapping

`LogEntryModel` should expose the persisted trace and span identifiers.

The current MSSQL sink is configured with standard `TraceId` and `SpanId`
columns. For SQL rows, these columns are the primary source of truth. If a
future sink stores compact Serilog fields only in `LogEvent`, mapping may fall
back to `@tr` and `@sp`, but the current DoFiesta data does not require that
fallback.

### 4.5 Cleanup Endpoint

Reuse the existing endpoint:

```text
DELETE /api/_system/logentries
```

The UI should use the `olderThan` date path, not `ageDays`.

Expected query parameters:

- `olderThan`
- `archive`
- `batchSize`

## 5. Frontend Design

### 5.1 Page and Navigation

Create `OperationsLogEntries.razor` at:

```text
examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsLogEntries.razor
```

Route:

```text
/operations/logentries
```

Add links in:

- Operations drawer navigation
- Top operations menu
- Operations overview page

### 5.2 Layout

The UI should be inspired by Seq while staying within existing MudBlazor
patterns used by DoFiesta.

Primary regions:

- Top query/search bar for broad search text.
- Compact toolbar for tail, refresh, auto-refresh, page size, reset, and cleanup.
- Main event list.
- Optional right-side signals panel for quick filters.
- Expandable row details.

The page should favor dense, operational scanning over card-heavy presentation.

### 5.3 Filters

Filters:

- Date range: `startTime`, `endTime`
- Minimum level
- General search text
- `CorrelationId`
- `TraceId`
- `SpanId`
- `LogKey`
- `ModuleName`
- `ShortTypeName`
- Page size

Applying or resetting filters must clear:

- Loaded rows
- Selected row
- `maxLoadedId`
- `continuationToken`

The page then reloads from the newest page.

### 5.4 Event List

The main list should use a dense MudBlazor table or list with fixed header and
scrollable height.

Columns:

- Timestamp
- Level
- Message
- Module
- Log key
- Correlation ID
- Trace ID
- Span ID
- Row menu

Rows are always sorted by newest first, using `Id` as the stable ordering key.

Expandable row details should show:

- Full message
- Message template
- Exception
- Trace ID
- Span ID
- Correlation ID
- Log key
- Module name
- Thread ID
- Short type name
- Structured `LogEvents`

### 5.5 Row Menu

Each row has a hamburger menu at the end.

Show actions only when the row has a non-empty value:

- Find all for `CorrelationId`
- Find all for `TraceId`
- Find all for `SpanId`
- Find all for `LogKey`
- Find all for `ModuleName`
- Find all for `ShortTypeName`
- Show trace timeline, when `TraceId` is present

Selecting a quick filter sets the matching dedicated filter, clears paging
state, and reloads newest-first results.

## 6. Tailing Behavior

Tailing is required and should be treated as a core acceptance criterion.

The tail implementation uses polling through the JSON query endpoint:

```text
GET /api/_system/logentries?afterId={maxLoadedId}
```

Behavior:

1. Initial load requests the newest page without `afterId` or
   `continuationToken`.
2. Store `maxLoadedId` from the newest loaded row.
3. When Tail is enabled, poll with active filters plus `afterId=maxLoadedId`.
4. Prepend returned entries in newest-first order.
5. Deduplicate by `Id`.
6. Update `maxLoadedId`.
7. Do not change the older-page `continuationToken`.
8. Pause tail polling while any other load, filter reset, cleanup, or manual
   refresh is active.

Tail mode must not use the current NDJSON stream endpoint in v1.

## 7. Loading Older Entries

Older entries use existing continuation-token paging. Infinite scroll means
older data loads automatically when the user reaches the bottom of the
scrollable log list.

Behavior:

1. Use the current `continuationToken` to request older entries.
2. Append older rows at the bottom.
3. Deduplicate by `Id`.
4. Replace the stored `continuationToken` with the response token.
5. Show a no-more-entries state when the token is null.
6. Trigger the next older-page request when the scroll container is near the
   bottom.
7. Guard against duplicate bottom triggers while an older-page request is
   already running.
8. Keep a visible `Load older` fallback action for accessibility and for cases
   where scroll detection does not fire.

This is an infinite-load pattern, not indexed virtual scrolling.

## 8. Cleanup Dialog

The toolbar should include a Cleanup action.

Dialog fields:

- `Older than` date picker
- `Archive instead of delete` switch
- Batch size input, default `1000`

Confirm behavior:

- Disabled until a date is selected.
- Calls `DELETE /api/_system/logentries`.
- Sends `olderThan`, `archive`, and `batchSize`.
- Shows success or error snackbar.
- Closes on accepted response.
- Reloads the current view after success.

## 9. Trace Timeline

The trace timeline is log-derived. It uses persisted SQL log entries created by
the Serilog SQL sink and should align with the trace/span metadata visible in
Seq.

The trace dialog intentionally uses only:

- `TraceId`
- `SpanId`
- `TimeStamp`

No parent span relationship is required for v1.

Open from:

- Row menu action
- Expanded row details action

Query:

- Filter by selected `TraceId`.
- Use current date range where possible.
- Use a large page size sufficient for a request trace.

Rendering:

- Sort trace entries oldest-to-newest.
- Show header summary:
  - Trace ID
  - Correlation ID
  - Request path
  - Total event count
  - First timestamp
- Last timestamp
- Inferred duration
- Group by distinct `SpanId` when present.
- Fall back to grouping by `LogKey` or `SourceContext` when span IDs are absent.
- Highlight the source log entry.
- Show relative offsets from the first event timestamp.
- Show each span lane with:
  - span id
  - event count
  - first offset
  - last offset
  - inferred lane duration
  - dominant `LogKey` values
- Render repeated events in the same span as stacked event rows or compact
  markers inside that span lane.
- Allow clicking a span lane or span id to apply the `SpanId` filter in the
  main log view.
- Allow clicking a trace id to apply the `TraceId` filter in the main log view.

For the observed trace `2e1cd03cf0e62004abd0b79fd966f7eb`, SQL currently
contains 113 rows and 4 span ids:

- `de0c7ea84529a561`
- `eef60774a6f69ba3`
- `42be2645a02bbd01`
- `043fcf838b8d1697`

The trace timeline should therefore render four span lanes for that trace.

Another observed trace pattern has repeated events in multiple span lanes, for
example one span containing `AUT` permission events and another span containing
`RES` rule/repository events. The timeline must not collapse repeated rows into
a single event. It should aggregate at the lane header while still preserving the
individual log entries in the lane details.

Span duration should be inferred from first-to-last log timestamp per span,
because persisted log entries do not contain formal activity duration data.

The timeline should be a span-lane event view, not a parent/child waterfall.

Useful structured properties to surface when present:

- `RequestPath`
- `RequestType`
- `BehaviorType`
- `EntityType`
- `EntityId`
- `Permission`
- `UserId`
- `StatusCode`
- `TimeElapsed`
- `SourceContext`

## 10. Tracing Investigation

### 10.1 SQL Log Entries

DoFiesta's MSSQL Serilog sink is configured with standard `TraceId` and
`SpanId` columns. Local SQL inspection showed:

- Rows for the provided trace have populated `TraceId` and `SpanId` columns.
- Compact Serilog fields `@tr` and `@sp` are not stored in `LogEvent` for those
  rows because the sink extracts them into standard columns.
- Many unrelated rows still have null trace/span columns.

Conclusion: the log viewer can use persisted SQL `TraceId` and `SpanId` columns
for trace grouping.

### 10.2 Seq and SQL Sink Scope

DoFiesta should use Seq and the SQL log table as the span source for this
feature. No Jaeger dependency is planned for the operations log viewer.

Seq receives the live Serilog events and the SQL sink persists operational
history. The page should query SQL through `LogEntryEndpoints`; Seq remains the
external viewer for users who prefer its native query experience.

### 10.3 Span Availability

DoFiesta has code that creates `Activity` spans, and the SQL sink can persist
the current `TraceId` and `SpanId` into standard columns. The supplied trace
proves this works for request-level diagnostic logs.

Implementation should focus on making those persisted columns searchable and
visible. If some rows have null `TraceId` or `SpanId`, the UI should degrade
cleanly instead of treating that as an infrastructure failure.

## 11. Acceptance Criteria

### 11.1 Log Viewer

- Given the page opens, when logs exist, then the newest logs appear first.
- Given filters are applied, when the page reloads, then only matching entries
  appear.
- Given a row has metadata, when its menu is opened, then quick filter actions
  are available for non-empty metadata values.
- Given a quick filter is selected, when the query reloads, then results match
  the selected metadata value.

### 11.2 Tail

- Given Tail is enabled, when new matching log rows are persisted, then they
  appear at the top without manual refresh.
- Given Tail is enabled, when older rows are loaded, then the older page token is
  preserved.
- Given filters change, when Tail resumes, then it only polls for entries
  matching the new filters.

### 11.3 Cleanup

- Given no date is selected, when the cleanup dialog is open, then confirm is
  disabled.
- Given a date is selected, when cleanup is confirmed, then the existing delete
  endpoint is called with `olderThan`.
- Given cleanup is accepted, then the dialog closes and the log view reloads.

### 11.4 Trace Timeline

- Given a row has `TraceId`, when the trace timeline opens, then entries for
  that trace are shown oldest-to-newest.
- Given trace entries have `SpanId`, then entries are grouped by span lane.
- Given trace entries have no `SpanId`, then an event timeline is shown with a
  clear no-span-data state.
- Given the supplied trace is available, then the timeline shows four span lanes.

## 12. Test Plan

Backend tests:

- `AfterId` returns only rows newer than the supplied id.
- `AfterId` results are newest-first.
- `AfterId` rejects combination with `ContinuationToken`.
- `SpanId` filters exactly.
- `AfterId` composes with date range, level, search text, correlation id, trace
  id, span id, log key, and module filters.
- Existing continuation-token paging remains unchanged.

Frontend/browser checks:

- Initial load renders newest-first.
- Tail prepends new rows reliably and without duplicates.
- Older loading appends older rows.
- Row quick filters reload matching rows.
- Cleanup dialog validates date and calls the cleanup endpoint.
- Trace timeline renders the supplied trace as four span lanes.
- Long messages, exceptions, and structured properties do not overflow the UI.

Seq/SQL span checks:

- Seq shows trace/span metadata for new Serilog events when activity context is
  present.
- SQL rows contain `TraceId` and `SpanId` columns for traced request logs.
- The operations trace timeline can be built from SQL rows alone.

## 13. Implementation Notes

- Use MudBlazor components already present in DoFiesta operations pages.
- Prefer a dense operational UI over large cards.
- Persist auto-refresh or tail settings in `localStorage` with a log-viewer
  specific key.
- Avoid adding a new backend endpoint unless the existing query endpoint cannot
  support a required behavior.
- Keep the first implementation focused on DoFiesta; generalizing the UI can be
  a later task.
