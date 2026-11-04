---
goal: Apply one visual and interaction contract across dashboard pages
version: 1.0
date_created: 2026-11-04
last_updated: 2026-11-04
owner: bITdevKit maintainers
status: 'Complete'
tags: [refactor, dashboard, presentation, paging, accessibility]
---

# Introduction

![Status: Complete](https://img.shields.io/badge/status-Complete-green)

Implement the eight phases of [Dashboard Page Consistency](../docs/specs/spec-dashboard-page-consistency.md). The spec owns the assessment, C01 through C10 design contract, acceptance criteria, and before screenshots. This plan owns the execution checklist, dependencies, phase status, and implementation evidence. It retains the original phase and task IDs.

The user explicitly permits query-layer changes for paging deficiencies or adjustments. Exhausted lists must retain a disabled Load more button beside the usable row-count selector. All eight phases are complete; implementation evidence and limits are recorded below.

## 1. Requirements & Constraints

- **REQ-001**: Apply C01 through C10 across built-in pages and equivalent application-owned controls. Preserve specialized terminals, streams, trees, and analytical layouts.
- **REQ-002**: Use shared shell title, toolbar, routed tabs, context/filter panel, results, and bottom list footer in that order where applicable. Fix wrapped-header overlap in the shared layout.
- **REQ-003**: Use visible Apply text and a circled-X Reset with an accessible name, natural widths, small controls, trailing action alignment, and semantic theme roles. Use the spec's .5rem panel padding and gaps and .25rem label separation.
- **REQ-004**: Input edits remain drafts. Apply commits successful validated selections. Refresh, export, pagination, and append use applied filters. Invalid Apply preserves successful results.
- **REQ-005**: Reset clears investigation criteria, paging, and selection and loads documented defaults. Preserve valid context, column/batch preferences, palette, and refresh mode. Reset expensive-scan opt-in to its safe default.
- **REQ-006**: Use neutral Refresh styling, explicit UTC status with the working wording Updated HH:mm:ss UTC, and feature-supported modes. Preserve Tail semantics and data on refresh failure.
- **REQ-007**: Place Load more immediately followed by Rows per load in one compact centered footer below each growing list. Keep Load more visible and disabled at exhaustion, including empty successful results. Keep the selector usable. Do not advance continuation after failure.
- **REQ-008**: Default batch size to 50 with choices 10, 25, 50, 100, and 200 where supported. Respect provider caps. Changing size reloads the first batch using applied filters without overwriting draft fields.
- **REQ-009**: Append bounded batches with deterministic ordering, exact-ID deduplication, and a stable browsing boundary. Refresh preserves loaded history. New arrivals cannot shift continuation.
- **REQ-010**: Use Jobs / Occurrences / Diagnostics and broker Operations / Diagnostics routed views. Restore applied filters, loaded history, batch preference, and scroll after tab navigation. Poll charts only while Diagnostics is active.
- **REQ-011**: Standardize native and Bootstrap dialog frames, linked titles, compact actions, bounded scrolling, cancellation, focus return, ETags, and retained values after failed saves.
- **REQ-012**: Use this dirty-editor working default: ignore backdrop dismissal while dirty; intercept Escape, Cancel, and header close with discard or continue-editing confirmation. Preserve values and focus when dismissal is canceled. Avoid stacked dialogs by preserving editor state while confirmation is active.
- **SEC-001**: Preserve access rules, capability checks, exact identifiers, download scope, scan limits, validation, and destructive confirmations.
- **CON-001**: Keep dashboard composition in Presentation. Required paging changes may affect owning application query contracts/services and provider implementations. Shared UI components must not access persistence.
- **CON-002**: Reuse existing query APIs before adding contracts. Add focused owning-layer tests for query adjustments. Do not create a UI framework, generic filter engine, dashboard persistence abstraction, source scanner, or separate acceptance framework.
- **CON-003**: Preserve routes, storage keys, query parameters, native forms/downloads, and detached-window behavior. Use instance-specific IDs and dispose fragment-owned requests, listeners, tooltips, and charts.
- **CON-004**: Execute phases in numeric order and check prerequisites. This plan does not authorize parallel agents, commits, pushes, or publication.
- **CON-005**: Run top-level builds/tests sequentially. Check 1440, 1024, 768, 390, and 320 CSS pixels with dark, light, and monochrome palettes.
- **CON-006**: Preserve before captures and save separately named after captures. Keep runtime scripts/logs outside docs. Use fictional examples for public documentation.
- **PAT-001**: Reuse DashboardPageSlice, typed RazorSlice partials, ViewNavigation, DashboardPage.RememberedViews, DateRange, DashboardDateTime, shared dialogs, and window.bdkDashboard.createRefresher.

## 2. Implementation Steps

Execute each phase's tasks in numeric order. Each task depends on the preceding task within its phase. Phase dependencies are explicit below.

| Phase | Work package | Depends on | Status |
| --- | --- | --- | --- |
| PHASE-01 | Baseline and contract readiness | None | Complete |
| PHASE-02 | Shared toolbar/filter controls and phone header | PHASE-01 | Complete |
| PHASE-03 | Investigation filters and applied state | PHASE-02 | Complete |
| PHASE-04 | Storage and Change History controls | PHASE-03 | Complete |
| PHASE-05 | Shared modal frames and lifecycle | PHASE-04 | Complete |
| PHASE-06 | Growing-list footer and continuation | PHASE-03, PHASE-05 | Complete |
| PHASE-07 | Operational and Diagnostics routes | PHASE-06 | Complete |
| PHASE-08 | Remaining pages and final verification | PHASE-02 through PHASE-07 | Complete |

Set Completed to Yes and Date to the actual YYYY-MM-DD completion date when a task passes. Append changed files, commands/results, screenshot references, limitations, and remaining work beneath its phase. Mark a phase Complete only after every task and exit criterion passes. A missing required check keeps it In progress and explicitly unverified. Update front matter and badge when execution starts or finishes. Mark the spec implemented only after all eight phases are Complete.

### Implementation Phase 1

- GOAL-001: Complete PHASE-01, Baseline and contract readiness.

Scope is the spec, its source references, and local read-only inspection. No application code changes belong to this phase.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-01.1 | Compare the current revision, dashboard page providers, and `src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml` with the review baseline. Record source drift that changes a finding or target file. | Yes | 2026-11-04 |
| TASK-01.2 | Record the working defaults used for C03 through C05 and C08 through C10. Map F01 through F11 to the later phases below. Retain any unresolved material conflict with its affected phase. | Yes | 2026-11-04 |
| TASK-01.3 | Confirm the actual build/test commands, host startup isolation, and availability of fictional rows for Jobs occurrence and broker context dialogs. Inspect existing query paging contracts; do not add a new query abstraction. | Yes | 2026-11-04 |
| TASK-01.4 | Record per-list query capabilities, tie-breakers, provider caps, and insert/delete/status-change behavior for Jobs and both brokers. Inspect all SearchMessagesAsync implementations and choose an initial browsing-boundary strategy before PHASE-06. Record fictional-data preparation and worktree-isolated host commands. | Yes | 2026-11-04 |

Exit criteria: source references still resolve, drift and material blockers are explicit, the defaults for implementation are recorded, and the verification host can be started without targeting another instance's data. This phase needs no application unit-test run.


PHASE-01 evidence, 2026-11-04. HEAD a098ac849e3e5ecf6513bab96e8031d0eb86e1b3 differs from assessment sources only by an Auditing icon asset. SDK 10.0.403 satisfies global.json 10.0.402 with latestFeature roll-forward. Started the repository sample with dotnet run using its project workflow. The worktree assigned HTTPS 5201 and HTTP 5200; the running process is isolated from other sample instances. Signed in with the built-in fictional administrator identity. Messaging and Operations before captures were saved at desktop and phone settings. Startup seed provides broker subscriptions; populated broker and Jobs context rows must be prepared and checked in PHASE-05/06.

Query audit: broker SearchMessagesAsync accepts FilterModel with page size capped at 1000 in the inspected EF/in-process implementations. EF broker searches use exact ID ordering for SQLite. Jobs supports Skip/Take and ID tie-breakers. Offset paging alone cannot preserve boundaries after row deletion or status changes. PHASE-06 must use immutable tuple/keyset continuation or adjust the owning queries; no live-offset reliability is claimed. Default ranges and context fallbacks will be recorded per feature during PHASE-03/04. F01-F07 map to PHASE-02/03, F08 to PHASE-04/08, F09 to PHASE-05, F10 to PHASE-06, and F11 to PHASE-07. Follow the plan's UTC, filter, dirty-editor and navigation working defaults.

### Implementation Phase 2

- GOAL-002: Complete PHASE-02, Shared toolbar/filter controls and phone header.

Scope is C01 through C03, C05, and C06, addressing F01 through F04, F06, and F07. Start in `src/Presentation.Web/Dashboard/Pages/`, `src/Presentation.Web.Messaging/Dashboard/Pages/Index.cshtml`, and `src/Presentation.Web/Profiling/Dashboard/Pages/Operations/Index.cshtml` plus its `Toolbar.cshtml`.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-02.1 | Fix the shared header so wrapped global actions reserve space and cannot overlap page controls at 390 and 320 pixels. | Yes | 2026-11-04 |
| TASK-02.2 | Extract the minimum shared toolbar, refresh, filter-panel, and Apply/Reset markup/styles. Preserve stable IDs, tooltip behavior, theme roles, and `createRefresher` bindings. | Yes | 2026-11-04 |
| TASK-02.3 | Migrate Messaging and Operations as the simple and multi-row references. Keep ordinary Reset text, natural action widths, neutral Refresh, and consistent labels. Leave new append controls and Diagnostics routes to PHASE-06 and PHASE-07. | Yes | 2026-11-04 |
| TASK-02.4 | Validate shared controls against both reference pages before broader migration. Preserve stable instance IDs, form association, tooltips, and createRefresher bindings. Reserve actual header layout space instead of page-specific offsets. | Yes | 2026-11-04 |

Exit criteria: both reference pages meet the scoped contracts at 1440, 1024, 768, 390, and 320 pixels, in dark, light, and monochrome palettes. Their existing filter and refresh behavior still works. Build affected web projects and run `tests/Presentation.UnitTests/Presentation.UnitTests.csproj` because this phase changes shared layout/helpers. Save before/after evidence for both references.


PHASE-02 evidence, 2026-11-04. Added shared FilterActions, RefreshControls, ControlStyles and typed models. Messaging and Operations use the common actions and explicit shared filter-panel class. The shared header reserves wrapped-row space on phones. Full Presentation suite passed 1408/1408, zero skipped, after restoring missing test assets. WeatherFiesta supplies registered Profiling for runtime verification at worktree HTTPS 5211 / HTTP 5210; the initial DoFiesta instance was stopped. Verified 30 route/width/palette combinations with actual viewport widths 1440, 1024, 768, 390, and 320: no page overflow or header/toolbar intersection. Both actions compute to natural 52px widths in the checked palette. Operations refresh succeeded with registered service. Before/after screenshot paths are recorded in .tmp/dashboard-consistency-evidence.json and selected captures will be copied beside the spec during PHASE-08.

### Implementation Phase 3

- GOAL-003: Complete PHASE-03, Investigation filters and applied state.

Scope is C02 through C05 and C07, addressing F01 through F06. Targets are Logging, Profiling, and Entities dashboard pages under `src/Presentation.Web/`, `src/Presentation.Web.Auditing/Dashboard/Pages/`, and the Messaging, Queueing, and Orchestrations filter scripts in their `src/Presentation.Web.*` packages.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-03.1 | Migrate Logs, Errors, Auditing, Entities, and the remaining analytical Profiling forms to the shared controls. Add visible or associated labels where placeholders currently carry the field meaning. | Yes | 2026-11-04 |
| TASK-03.2 | Separate draft and applied filters for fragment-driven pages, including the broker and orchestration forms. Refresh, export, paging, and existing Load more consume the applied selection. Keep normal GET forms where they already provide that boundary. | Yes | 2026-11-04 |
| TASK-03.3 | Implement the C04 Reset rules per feature, document its default range/context fallback, and preserve preferences and explicit navigation. | Yes | 2026-11-04 |
| TASK-03.4 | Record a per-feature Reset table with default time ranges, context fallbacks, safe-scan defaults, and preserved preferences. Test drafts during automatic refresh and failed Apply, with export and append still using the last successful selection. | Yes | 2026-11-04 |

Exit criteria: typing without Apply leaves results, exports, and next-batch queries unchanged; Apply and Reset have the documented results; invalid filters retain prior data. Run relevant existing Logging, Auditing, Profiling, Entities, Messaging, Queueing, and Orchestrations tests. Add focused state/query tests where coverage is missing and verify draft/refresh/export behavior in the browser.


PHASE-03 evidence, 2026-11-04. Shared actions now cover investigation forms, with labels on secondary log identifiers and UTC quick ranges in Auditing. Added a small applied-filter helper for broker/orchestration pages. Logs, Errors, Operations, Timeline, and Insights commit only successful selections. Explicit Reset preserves selected row/limit preference and refresh mode. The existing Presentation suite passed 1408/1408; a subsequent focused Dashboard/Profiling run passed 698/698. Runtime request instrumentation confirmed draft/refresh/Apply separation on Messaging, Queueing, Orchestrations, Logs, Errors, Operations, Timeline, and Insights. Logs export stayed unchanged while typing. A forced Messaging network failure retained results and URL; subsequent Refresh used the prior successful selection while retaining the failed draft. Logs/Errors Reset retained the selected 100 rows and restored UTC midnight defaults. Malformed Operations selector JSON produced local validation with unchanged successful selection.

| Feature | Reset criteria/range | Retained context and preferences |
| --- | --- | --- |
| Logs | Clear identifiers/search; Information minimum; UTC midnight today to open end | Current batch preference and Tail |
| Errors | Clear identifiers/search; fixed error scope; UTC midnight today to open end | Current batch preference and Tail |
| Messaging and Queueing | All statuses/types/identifiers; Active archive; no explicit date range | Current batch preference and refresh mode |
| Orchestrations | All status/name/state/correlation criteria; no explicit date range | Current result-limit preference and refresh mode |
| Entities | Clear conditions/search/sort and selection | Current entity/source, batch size, columns, palette |
| Auditing | Clear investigation and date bounds; Active lifecycle | Current page-size preference; host read scope remains authoritative |
| Operations and Requests | Clear key/dimensions/node/correlation/date bounds; Recent; Requests keeps HttpRequest kind; default last 15 minutes | Current limit and Tail preference |
| Timeline | Clear investigation/date bounds; default Hour resolution and last hour | Current result-limit preference |
| Insights | Clear investigation/date bounds; p95 ranking; default last 15 minutes | Current result-limit and refresh preferences |

Context fallback: when a selected context is no longer valid, retain the owning page's existing registered/default fallback and clear incompatible dependent criteria. Never invent a provider or cross into another entity/source. Palette and column preferences remain unchanged throughout.


### Implementation Phase 4

- GOAL-004: Complete PHASE-04, Storage and Change History controls.

Scope is C01 through C07, addressing F01 through F06 and F08. Targets are `src/Presentation.Web.Storage/{Files,Documents,Blobs,Permalinks}/Dashboard/` and `src/Presentation.Web.EntityFramework/ChangeHistory/Dashboard/`.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-04.1 | Migrate storage filter/context strips and Change History forms to shared controls. Add Permalink labels and separate creation/upload tools from the Apply/Reset group. | Yes | 2026-11-04 |
| TASK-04.2 | Apply C04 while preserving provider/store/container context, dependent filters, scan permission defaults, ETags, and supported match modes. Do not make all providers support identical queries. | Yes | 2026-11-04 |
| TASK-04.3 | Align result framing and empty/unavailable/error messages without replacing the Files tree or forcing all storage results into one layout. | Yes | 2026-11-04 |
| TASK-04.4 | Verify provider changes invalidate incompatible continuation, Reset disables full-scan opt-in, and failed validation retains applied results. Keep creation/upload actions outside filter actions. | Yes | 2026-11-04 |

Exit criteria: filter/reset/provider switching preserves supported behavior and safe scan defaults, and continuation/download/edit targets retain exact identifiers. Run existing `DocumentStorageDashboardTests`, `BlobStorageDashboardTests`, and `ChangeHistoryDashboardTests` plus relevant Files/Permalinks tests. Verify populated, empty, and unsupported/unavailable cases with representative provider capabilities.


PHASE-04 evidence, 2026-11-04. Storage and Change History use shared filter panels/actions; Permalinks has associated visible labels and no duplicate page heading. Document creation/blob upload are separate from filter actions. Reset retains client/store/container/registration and page-size context, and Blob Reset disables full-scan permission. Added explicit draft preservation for filters inside replaced storage/Change History fragments, plus cancellation/stale-response protection for custom document/blob requests. Storage/ChangeHistory tests passed 131/131. Runtime checks covered Files, Documents, Blobs, Permalinks, and Change History at the isolated WeatherFiesta host. Documents and Blobs retained unsaved filter text during Refresh, applied only on submit, and reset criteria without changing context. Blob Reset changed full-scan opt-in back to false. Change History retained an unsaved entity criterion across refresh and its Reset URL retained registration/page size. Existing provider tests cover populated and unavailable/capability cases; the runtime Documents/Change History lists were empty and no populated runtime claim is made for them.

Storage Reset defaults: Files retains its navigated provider/path workflow without an artificial filter form; Documents clears partition/row criteria and selects the first supported row-key mode; Blobs clears prefix/continuation and disables full scan; Permalinks clears investigation criteria while retaining page size; Change History clears investigation/time/order criteria while retaining registration/page size. Invalid contexts keep each owner's existing default/registered fallback. Exact identifiers, ETags, and download/mutation endpoints remain feature-owned.

### Implementation Phase 5

- GOAL-005: Complete PHASE-05, Shared modal frames and lifecycle.

Scope is C08 and C06, addressing F09. Start with `src/Presentation.Web/Dashboard/Pages/_DashboardDialogs.cshtml`, Auditing `DetailDialog.cshtml`, storage modal markup, and Entities `Content.cshtml`, `Editor.cshtml`, `Detail.cshtml`, and `Script.cshtml`. Then cover Jobs, Messaging, Queueing, Profiling, Health, MCP, and Change History dialogs.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-05.1 | Extract a small shared visual/action contract. Validate it with Auditing details, a Blob editor, and the native Entities dialog before migrating other families. Do not replace native dialogs solely to match Bootstrap markup. | Yes | 2026-11-04 |
| TASK-05.2 | Link every visible title to its dialog, align header/footer sizing and action roles, and replace generic OK with a specific action. Keep destructive confirmation separate from ordinary editor submit. | Yes | 2026-11-04 |
| TASK-05.3 | Reuse detail loading/cancellation/focus logic where it actually repeats. Implement consistent dirty-editor dismissal, retain form values on failed saves, and preserve refresh pause and concurrency checks. | Yes | 2026-11-04 |
| TASK-05.4 | Apply REQ-012 to native and Bootstrap editors. Test dirty backdrop/Escape/header-close/Cancel behavior, failed saves, fragment replacement, concurrency conflicts, and two mounted frames for ID/listener collisions. | Yes | 2026-11-04 |

Exit criteria: every affected dialog meets C08 on desktop and phone, including keyboard focus, Escape/backdrop policy, long-body scrolling, retry, and return to the originating list. Verify row-backed Jobs/Messaging/Queueing dialogs with fictional rows rather than claiming source-only coverage. Run existing `AuditDashboardDialogTests`, relevant `OperationProfilingDashboardTests`, Entities, and storage tests. Check two mounted dialog instances for ID and handler collisions when the shared component supports composition.


PHASE-05 evidence, 2026-11-04. Added shared modal actions and one visual frame for Bootstrap/native dialogs, accessible title links, compact footers, and specific Entities create/save actions. Jobs/Messaging/Queueing context dialogs now have footer Close. Dirty editors ignore backdrop dismissal and use an in-frame discard/keep-editing prompt, avoiding stacked dialogs. Native entity confirmations also use the current frame. Successful saves mark the editor clean; failed Blob/File actions show local feedback and retain values. Document Delete is a secondary button rather than the form's implicit submit.

Dialog/Entities/Storage/Profiling tests passed 701/701. Live checks caught and fixed Bootstrap's capture-order interaction, which otherwise canceled a newly opened discard prompt. After two shared-preview click timeouts, switched to the repository Playwright workflow. The server detector found an unrelated 8080 service; used the already verified isolated worktree URL 5211. The visible-browser verification passed Bootstrap backdrop/Escape/header dismissal, Keep editing/discard, forced failed saves, native editor labels and failed-save retention, and populated Jobs/Messaging/Queueing context dialogs. Only built-in diagnostic Alive controls prepared context rows. No stress, cleanup, or purge jobs were invoked. Browser page-error count was zero. Phone captures cover native editor, Blob details, and document editor; desktop captures cover populated context dialogs. Evidence resides in .tmp/dashboard-dialogs and phase05-browser.log pending selected after-capture publication in PHASE-08.

### Implementation Phase 6

- GOAL-006: Complete PHASE-06, Growing-list footer and continuation.

Scope is C09 and C04, addressing F10. Targets are Entities `LoadMore.cshtml` as the reference, Jobs `Pages/Data.cshtml`, and Messaging/Queueing `Pages/Index.cshtml`, `Data.cshtml`, and `Content.cshtml`. Query owners are `src/Application.Jobs/Queries/`, `src/Application.Messaging/IMessageBrokerService.cs`, and `src/Application.Queueing/IQueueBrokerService.cs`.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-06.1 | Extract `DashboardListFooter` and render Load more immediately followed by Rows per load below each growing list. Migrate Jobs occurrences and broker messages, remove duplicate top-bar batch selectors, and give each Jobs list independent state. | Yes | 2026-11-04 |
| TASK-06.2 | Reuse Jobs Skip/Take queries and broker `SearchMessagesAsync` with bounded, deterministic ordering. Preserve applied query scope, exact identity, continuation, and a browsing boundary while new records arrive. Adjust owning query contracts and providers for required paging deficiencies or adjustments, as authorized by the user. | Yes | 2026-11-04 |
| TASK-06.3 | Implement batch-size persistence, first-batch reload on size change, append/retry/end states, loaded-count semantics, and refresh reconciliation. Do not hide older rows behind a larger Take or materialize an unbounded collection. | Yes | 2026-11-04 |
| TASK-06.4 | Implement the TASK-01.4 paging strategy. Prefer keyset continuation over immutable sort values plus unique identity. Use offsets only with proof under supported mutations; an upper timestamp alone cannot prevent deletion/status-change skips. Adjust owning contracts/providers as required and test equal timestamps, arrivals, deletions, and status changes. Keep exhausted Load more visible and disabled with its adjacent selector usable. | Yes | 2026-11-04 |

Exit criteria: each migrated list loads at least three distinct batches with no duplicates or skipped boundary rows; changing the selector reloads the first batch; failure retries the same continuation; new arrivals do not corrupt historical browsing. At the end, Load more is disabled while the adjacent selector remains usable. Run relevant Presentation and owning-query tests, including tie timestamps, row deletion, and concurrent arrivals where the provider supports them. Capture the footer on Jobs, Messaging, and Queueing at desktop and phone widths.

PHASE-06 evidence, 2026-11-04. Extracted typed ListFooter and createGrowingLists; broker searches use bounded PageSize+1 with canonical immutable-ID seeking and an upper ID boundary. Jobs registrations seek by JobName; occurrences seek by OccurrenceId. GUID ordering/comparison now agree across LINQ and SQLite. This is identity order, not a promise of chronological ordering or a transactional snapshot; later arrivals within the unvisited key range may appear during continuation. No offsets or growing Take requests were introduced. SQLite broker/queue regression tests passed 4/4, covering equal timestamps, arrivals, deletion and status changes. Common filtering checks passed 10/10. Visible-browser checks loaded 31 rows in each of Messaging, Queueing and Jobs occurrences, without duplicate IDs, and passed simulated failure/retry with the same cursor, refresh retention, exhausted disabled button with usable selector, and first-batch size reset. Logs, Errors, Auditing and Entities also consume the shared footer while preserving their owning continuation contracts. The final browser evidence is .tmp/dashboard-final/results.json.

### Implementation Phase 7

- GOAL-007: Complete PHASE-07, Operational and Diagnostics routes.

Scope is C10, addressing F11. Targets are `DashboardEndpoints.cs`, `DashboardPageProvider.cs`, and the page/fragment/chart code in `src/Presentation.Web.Jobs/Dashboard/`, `src/Presentation.Web.Messaging/Dashboard/`, and `src/Presentation.Web.Queueing/Dashboard/`. Reuse Blob storage's existing navigation pattern and shared remembered views.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-07.1 | Implement the recorded Jobs layout default: Jobs, Occurrences, and Diagnostics. Preserve the Jobs root and move realtime/history graphs and their windows entirely into Diagnostics. | Yes | 2026-11-04 |
| TASK-07.2 | Add Operations/Diagnostics views for Messaging and Queueing. Retain subscriptions and waiting-handler context with messages; move flow charts into Diagnostics. Reuse routed pill markup and preserve configured dashboard roots and access rules. | Yes | 2026-11-04 |
| TASK-07.3 | Restore applied filters, loaded history, row-count preference, and scroll when returning to a list. Stop polling and dispose charts when leaving Diagnostics; initialize and resize them on activation and palette changes. | Yes | 2026-11-04 |
| TASK-07.4 | Record and implement bounded restoration scoped by route, list, applied selection, and browsing boundary. Restore or replay loaded history after Diagnostics navigation without mixing diagnostic windows or draft inputs. Avoid sensitive row payloads in browser storage. Test repeated transitions for duplicate polling/listeners. | Yes | 2026-11-04 |

Exit criteria: direct links, remembered views, refresh fragments, and a custom dashboard root reach the correct view. Operational pages load no diagnostic charts or graph polling. Changing tabs preserves PHASE-06 list state and does not alter query scope. Run existing Jobs/Messaging/Queueing endpoint tests plus relevant dashboard navigation tests, and verify chart lifecycle and repeated tab transitions in the browser.

PHASE-07 evidence, 2026-11-04. Added Jobs / Occurrences / Diagnostics and broker Operations / Diagnostics routes under their existing dashboard groups, plus remembered view descriptors. Operational HTML emits no graph owners or chart polling; Diagnostics owns graph timers, theme rendering and pagehide disposal. List restoration stores query/boundary/count/scroll metadata in session storage and replays bounded requests, limited to 100 batches and 20,000 rows; larger sessions restart from the first batch. No row payloads are stored. Three live round trips restored the same 30-row identity sequence after Diagnostics. Eight focused rendering/continuation checks passed, including seven direct custom-root/detached-view routes. The existing Dashboard group retains access metadata. Jobs query checks passed 28/28, including equal-time continuation with new arrivals.

### Implementation Phase 8

- GOAL-008: Complete PHASE-08, Remaining pages and final verification.

Scope is the remaining page inventory, C01 through C10, and all findings. Targets include Dashboard overview, System, Metrics, Health, Identity, Console, Logs Stream, MCP, Broadcasting, Orchestrations, and equivalent application-owned controls. Keep specialized terminal, stream, and navigation workflows intact.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-08.1 | Migrate remaining equivalent toolbars, controls, and result framing. Record justified exceptions for specialized workflows. Do not add artificial filters, append controls, or empty Diagnostics tabs. | Yes | 2026-11-04 |
| TASK-08.2 | Run the complete acceptance criteria against the final page inventory. Cover populated/empty/unavailable/error states, long identifiers, keyboard use, responsive widths, and dark/light/monochrome palettes. Recheck shared component disposal and explicit navigation after fragment replacement. | Yes | 2026-11-04 |
| TASK-08.3 | Add after screenshots, update affected fictional public documentation captures and dashboard guidance, and record the final test results and any unresolved limitations. Preserve the original assessment captures. | Yes | 2026-11-04 |
| TASK-08.4 | Update .agents/skills/dashboard-pages/references/page-patterns.md to replace hide-at-end advice with the disabled exhausted Load more contract. Update shared-component guidance/source map and resolve every remaining acceptance gap. | Yes | 2026-11-04 |

Exit criteria: all global acceptance criteria pass or a remaining gap keeps this phase In progress. Run the complete Presentation unit suite and relevant owning-layer tests for actual behavior changes, then finish browser verification. Stop verification hosts and confirm their ports are released. Mark the overall spec implemented only after every phase is Complete.



PHASE-08 evidence, 2026-11-04. Equivalent built-in and application-owned controls now use the shared styles/partials. Console and Logs Stream retain connection/Start/Stop/Clear workflows; the stream's filter reset is text. Tree navigation and application workload forms retain their task-specific semantics. Metrics suppresses unavailable/error observations instead of showing fabricated zero summaries. The common mobile table minimum now applies only inside responsive scrolling regions. Identity, MCP, System and Metrics narrow-layout defects found by screenshots were corrected.

Final verification: solution build succeeded with zero warnings/errors; Presentation 1420/1420, Jobs query 28/28, SQLite operational search 4/4 and Common filtering 10/10 passed. Seven custom-root/detached routes plus the continuation rendering check passed (8/8). The final targeted responsive group passed 63/63 before the last local System wrapping correction; the complete final Presentation run includes that correction. Browser verification covered 44 routes at 1440/1024/768/390/320 with Tokyo Night, Catppuccin Latte and White (660 checks), with zero page overflow, control/header overlap, duplicate IDs or page errors after the System recheck. It saved 104 page captures and six dialog captures. Browser interaction checks passed failed Apply retention, applied export/refresh, draft-preserving size changes, Logs append retention, Auditing append, storage draft preservation, Tools Escape/focus, dirty-dialog dismissal/failed-save behavior, three-batch broker/Jobs continuation and Diagnostics round trips. New-record indication and Start new list were verified with an unchanged draft. Six desktop/phone footer captures show retained end-state controls.

Selected actual after captures are beside the preserved assessment images in docs/specs/assets/dashboard-page-consistency/. Refreshed 38 fictionalized public documentation captures; the public image README identifies older detail figures retained for their data examples. Shared-component guidance and hide-at-end advice were updated. See [verification report](dashboard-page-consistency-verification.md) for evidence and limits.

Verification hosts were stopped and their assigned 5200/5201/5210/5211 ports were confirmed released. No commit or push was performed. External-service integration suites were not run; SQLite provider tests and the isolated running host supply the provider/runtime evidence claimed here. Restoration is bounded to 100 batches / 20,000 rows, and identity-seek lists do not claim a transactional or chronological snapshot.


## 3. Alternatives

- **ALT-001**: CSS-only cleanup cannot fix inconsistent labels, markup, or applied state. Use small typed partials and shared CSS with real consumers.
- **ALT-002**: Copying a reference form preserves duplication. Validate common components with Messaging and Operations before broader migration.
- **ALT-003**: Increasing Take or using live offsets without boundary proof cannot provide reliable history. Use bounded continuation and adjust query owners where necessary.
- **ALT-004**: Hiding exhausted Load more removes the stable footer action. The user selected a visible disabled button beside an enabled selector.
- **ALT-005**: Two Jobs views would be smaller, but the plan follows the spec's Jobs / Occurrences / Diagnostics working default.
- **ALT-006**: Replacing native dialogs or normal GET forms adds unrelated migration work. Preserve rendering technologies and native semantics.

## 4. Dependencies

- **DEP-001**: Follow AGENTS.md, docs/INDEX.md, README.md, .github/copilot-instructions.md, .editorconfig, and dashboard-pages guidance. Resolve versions from global.json, props, and project files.
- **DEP-002**: Feature dashboard packages consume shared Presentation.Web components without depending on peer feature packages. Extract only components with current-phase consumers.
- **DEP-003**: PHASE-01 must identify a worktree-isolated verification host and fictional rows for populated Jobs occurrence and broker context dialogs. Record unavailable services or data explicitly.
- **DEP-004**: Reuse existing chart, date-range, dialog, correlation, and Shortener helpers. Add no dependency solely for this plan.
- **DEP-005**: Inspect Docker and external-service requirements before integration tests. Source inspection alone does not establish runtime verification.

## 5. Files

The paths below identify existing owners and target directories. New component names in the spec are proposals, not existing APIs.

| ID | Paths | Planned changes |
| --- | --- | --- |
| FILE-001 | src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml; DashboardPageSlice.cs; ViewNavigation.cshtml; _DashboardDialogs.cshtml in the same directory | Shared shell, navigation, dialogs, and refresher integration |
| FILE-002 | src/Presentation.Web/Dashboard/Pages/ | Minimal typed toolbar, refresh, filter, actions, footer, and modal partials when consumers require them |
| FILE-003 | src/Presentation.Web/Logging/Dashboard/Pages/; src/Presentation.Web/Profiling/Dashboard/Pages/; src/Presentation.Web/Entities/Dashboard/Pages/ | Investigation controls, applied state, reset defaults, dialogs, and existing footer reference |
| FILE-004 | src/Presentation.Web.Auditing/Dashboard/Pages/ | Shared toolbar, date controls, and detail dialogs |
| FILE-005 | src/Presentation.Web.Jobs/Dashboard/; src/Presentation.Web.Messaging/Dashboard/; src/Presentation.Web.Queueing/Dashboard/ | Index/Data/Content pages, endpoints, providers, growing lists, and Diagnostics routes |
| FILE-006 | src/Presentation.Web.Orchestrations/Dashboard/ | Filter state, common controls, and result framing |
| FILE-007 | src/Presentation.Web.Storage/; src/Presentation.Web.EntityFramework/ChangeHistory/Dashboard/ | Files/Documents/Blobs/Permalinks context/filter controls, results, dialogs, and Change History |
| FILE-008 | src/Application.Jobs/Queries/JobSchedulerQueryService.cs; src/Application.Jobs/Queries/JobSchedulerQueryModels.cs; src/Application.Messaging/IMessageBrokerService.cs; src/Application.Queueing/IQueueBrokerService.cs | Required paging/query adjustments and public XML documentation |
| FILE-009 | src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBrokerStoreService{TContext}.cs; src/Infrastructure.EntityFramework/Queueing/EntityFrameworkQueueBrokerService{TContext}.cs; src/Application.Queueing/Brokers/InProcessQueueBrokerService.cs | Required provider paging adjustments; find all implementations before contract changes |
| FILE-010 | tests/Presentation.UnitTests/; tests/Application.UnitTests/Jobs/JobSchedulerQueryServiceTests.cs; tests/Application.UnitTests/Queueing/; tests/Infrastructure.UnitTests/Queueing/EntityFrameworkQueueBrokerServiceTests.cs; tests/Infrastructure.IntegrationTests/ | Existing feature coverage and focused query/provider regressions |
| FILE-011 | src/Presentation.Web/Dashboard/Pages/; src/Presentation.Web/Metrics/; src/Presentation.Web/Health/; src/Presentation.Web/Identity/; src/Presentation.Web/ConsoleCommands/; src/Presentation.Web/Mcp/; src/Presentation.Web/Broadcasting/ | Remaining common chrome and specialized workflow exceptions |
| FILE-012 | docs/specs/spec-dashboard-page-consistency.md; docs/specs/assets/dashboard-page-consistency/; docs/features-presentation-dashboard.md; .agents/skills/dashboard-pages/ | Design contract, after evidence, fictional documentation captures, and guidance |

Discover application-owned targets from dashboard registrations in TASK-01.1 and record their exact paths before editing. Keep application-specific host commands and runtime evidence in this plan or local artifacts outside docs. Public documentation uses fictional models and routes.

## 6. Testing

Run these commands from the repository root during implementation, sequentially. These are planned checks, not claimed results.

| ID | Command or procedure | Required result |
| --- | --- | --- |
| TEST-001 | dotnet build src/Presentation.Web/Presentation.Web.csproj --nologo | Shared components compile; build each changed feature package using its corresponding csproj path |
| TEST-002 | dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo | Full suite passes for shared layout/helper changes and final verification |
| TEST-003 | dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --nologo | Run for application query changes; focused Jobs and Queueing paging behavior passes |
| TEST-004 | dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --nologo | Run for provider changes; affected provider paging behavior passes |
| TEST-005 | Inspect tests/Infrastructure.IntegrationTests fixtures, then run relevant tests with dotnet test and a recorded --filter | Required services available; provider paging evidence covers equal timestamps, deletion, status changes, and arrivals |
| TEST-006 | Load at least three batches for Jobs occurrences, Messaging, and Queueing | No duplicate/skipped boundary rows; correct retries and size reset; exhausted button disabled with selector enabled |
| TEST-007 | Edit draft filters, refresh manually/automatically, export, append, Apply invalid/valid criteria, then Reset | Queries use applied selection; invalid Apply retains results; Reset follows feature defaults |
| TEST-008 | Open populated dialogs, edit, dismiss, retry failed saves, and exercise concurrency conflicts | Correct title/focus/scrolling, cancellation, retained values, and ETag behavior |
| TEST-009 | Visit direct routes/custom root/detached views and repeatedly switch Diagnostics views | Explicit navigation wins; history and scroll restore; inactive chart polling stops; charts resize after palette changes |
| TEST-010 | Inspect 1440, 1024, 768, 390, and 320 CSS pixels with Tokyo Night, Catppuccin Latte, and White | No header/control overlap or horizontal page overflow; local result overflow remains usable |
| TEST-011 | Check empty, unavailable, unsupported, unready, failed, populated, long-ID, and fragment-replacement states | Accurate meaning and counts, no duplicate IDs/handlers, preserved drafts and dialog ownership |
| TEST-012 | git diff --check; inspect git diff and git status --porcelain | No whitespace errors, unrelated changes, secrets, or temporary runtime artifacts |

Use existing AuditDashboardDialogTests, OperationProfilingDashboardTests, EntitiesDashboardEndpointTests, DocumentStorageDashboardTests, BlobStorageDashboardTests, ChangeHistoryDashboardTests, DashboardMessageTypeTests, and relevant feature tests. Add focused tests for changed behavior rather than tests that copy markup.

For browser verification, use the repository's existing launch-task or dotnet run workflow, never a built server DLL. Inspect startup isolation and actual assigned URLs first. Linked worktrees replace requested ports, catalog names, and cookie names. Read the worktree startup line or use Presentation.Cli dev status. Record exact start/stop commands, actual instance, and fictional-data preparation under PHASE-01. Stop hosts started for verification and confirm their assigned ports are released. Do not assume another checkout's URL or data.

## 7. Risks & Assumptions

- **RISK-001**: Offset paging can skip records after deletion/status changes even with an upper timestamp bound. Require provider-specific proof or an owning-query adjustment.
- **RISK-002**: Fragment replacement can erase drafts, rows, or dialog ownership. Test cancellation, stale responses, and replacement explicitly.
- **RISK-003**: Shared markup regressions affect many packages. Validate two distinct consumers before broader migration and run the full Presentation suite for shared changes.
- **RISK-004**: Routed tabs alone do not preserve loaded history. PHASE-07 must implement bounded restoration with query/boundary validation.
- **RISK-005**: Development startup can recreate data. Confirm worktree isolation before starting verification and report unavailable dependencies honestly.
- **RISK-006**: Original assessment evidence does not cover every populated dialog or failure state. Require actual runtime checks before declaring phases Complete.
- **ASSUMPTION-001**: Component names, exact spacing, UTC wording, and three-view Jobs layout use the spec's working defaults. REQ-012 is a planning choice, not a previously approved user requirement.
- **ASSUMPTION-002**: Query-layer permission covers paging deficiencies and adjustments, not unrelated business-policy changes or a general query redesign.
- **ASSUMPTION-003**: Phase completion requires recorded implementation and verification evidence, not source inspection alone.

## 8. Related Specifications / Further Reading

- [Dashboard Page Consistency](../docs/specs/spec-dashboard-page-consistency.md)
- [Dashboard feature guide](../docs/features-presentation-dashboard.md)
- [Dashboard page skill](../.agents/skills/dashboard-pages/SKILL.md)
- [Dashboard page patterns](../.agents/skills/dashboard-pages/references/page-patterns.md)
- [Dashboard source map](../.agents/skills/dashboard-pages/references/source-map.md)
- [Clean Architecture](../docs/adr/0001-clean-onion-architecture.md)
- [Modular monolith architecture](../docs/adr/0003-modular-monolith-architecture.md)

To request one work package:

> Implement PHASE-02 only from plan/pln-refactor-dashboard-page-consistency-1.md and its linked design spec. Check prerequisites, leave later phases and unrelated changes untouched, run required checks, and record evidence in the plan. Do not commit or push.

This example is a future task template, not an instruction to start implementation.

Final compatibility check: existing broker `take` values retain the 1-500 cap, including custom values and the former 250/500 choices. Default remains 50. Rendering/seek tests cover 10, 40, 250 and 500 without changing the caller's applied query. The selected supported size remains visible in the footer.
