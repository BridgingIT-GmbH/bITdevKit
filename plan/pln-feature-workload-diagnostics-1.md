---
goal: Replace Jobs, Messaging and Queueing stock-count charts with measured throughput diagnostics
version: 1.0
date_created: 2026-11-04
last_updated: 2026-11-04
owner: bITdevKit maintainers
status: 'Completed'
tags: [dashboard, diagnostics, metrics, jobs, messaging, queueing]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

Use the storage diagnostics layout and refresh lifecycle for Jobs, Messaging and Queueing. The existing broker charts poll retained database counts, and Jobs derives changes between retained-count samples. These samples are not reliable event throughput: archival, retention and purge can change them independently of processing. Use runtime measurements for rates and latency. Implementation is complete; validation is recorded in [the verification report](workload-diagnostics-verification.md).

## 1. Requirements & Constraints

- **REQ-001**: Render four live charts in a two-column desktop grid and one column on phones, matching storage diagnostics controls, theme handling and time axes.
- **REQ-002**: Count events in one-second server-side buckets. Retain at most 900 completed buckets per feature, independent of browser visits. Divide counts by the actual observed interval, including a partial startup interval.
- **REQ-003**: Jobs charts: execution rate (started/completed per second); outcome rates (completed/failed/retried/timed out/cancelled/interrupted per second); mean execution duration in milliseconds; mean scheduling delay in milliseconds.
- **REQ-004**: Messaging charts: publication attempts/successes per second; handler attempts/successes per second; publication/handler failures per second; mean publication/handler duration in milliseconds. Queueing uses enqueue instead of publication.
- **REQ-005**: Handler rates count handler invocations. A message with multiple subscribers can produce multiple handler executions. Label these series accordingly; do not call them unique processed-message counts.
- **REQ-006**: Preserve Overview, Messages, Jobs and Occurrences investigation behavior. Keep the existing Jobs retained-history capability separately labelled; it is not historical runtime telemetry.
- **REQ-007**: Reuse `IMetricsService` and the DevKit meter. Observe aggregate instrument names only. Exclude typed copies from aggregation to avoid counting events twice. Do not retain job names, message types, keys or correlation IDs as chart-history dimensions.
- **REQ-008**: Keep charts visible before observations. Show missing latency as a gap, not a measured zero. Distinguish unavailable instrumentation from an enabled idle source.
- **REQ-009**: Keep metrics opt-in through the existing application registration. Do not force-enable telemetry or change broker/persistence configuration from a dashboard.
- **REQ-010**: Use `createRefresher` with `responseType: 'json'`. Preserve failed-refresh data, configurable intervals, hidden-page suspension, theme redraw, resize and navigation cleanup.
- **CON-001**: Rates describe this application process since collection began. Distributed aggregation and durable metric history are outside this change.
- **CON-002**: Keep application measurement changes in Application and collection/rendering in Presentation. Add no dependency from Application to Presentation.
- **CON-003**: Do not calculate throughput from retained stock counts or serialize payloads to estimate bytes. Do not add a synthetic workload generator; existing diagnostic actions can validate measured activity.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Complete event and timing instrumentation before chart migration.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | In `src/Application.Messaging/Behaviors/MetricsMessagePublisherBehavior.cs` and `MetricsMessageHandlerBehavior.cs`, add explicit successful-completion counters after successful delegate return and duration histograms in `finally`, using existing aggregate and typed naming conventions. Preserve exception propagation and current-value balancing. | Yes | 2026-11-04 |
| TASK-002 | In `src/Application.Queueing/Behaviors/MetricsQueueEnqueuerBehavior.cs` and `MetricsQueueHandlerBehavior.cs`, add explicit successful-completion counters. Reuse existing duration, attempt, current and failure instruments. | Yes | 2026-11-04 |
| TASK-003 | Audit `src/Application.Jobs/Behaviors/JobMetricsBehavior.cs` and `JobSchedulerMetrics.cs` for exactly one execution outcome and balanced current values on success, failed results, exceptions and cancellation. Existing started/outcome/duration/occurrence-age instruments are the primary sources. | Yes | 2026-11-04 |
| TASK-004 | Extend `tests/Application.UnitTests/Messaging/MetricsMessageBehaviorsTests.cs`, `Queueing/MetricsQueueingBehaviorTests.cs` and the existing Jobs metric tests. Verify start versus successful completion, failure/cancellation, durations and fan-out semantics. | Yes | 2026-11-04 |

Exit: success counters describe completed operations, existing metric names retain their meaning, and focused application tests pass.

### Implementation Phase 2

- GOAL-002: Collect bounded time-series data. Depends on Phase 1.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-005 | Add a shared bounded meter-history collector under `src/Presentation.Web/Dashboard/`, following `src/Presentation.Web.Storage/StorageDashboardMetrics.cs`. Feature packages provide explicit aggregate instrument mappings. Ignore typed instruments and high-cardinality tags. | Yes | 2026-11-04 |
| TASK-006 | In each feature's `Dashboard/DashboardEndpoints.cs`, initialize the collector before workers start, dispose it with the host, and expose a JSON snapshot route under the configured dashboard group and its existing authorization. | Yes | 2026-11-04 |
| TASK-007 | Add deterministic-clock Presentation tests for count/second conversion, partial startup buckets, latency means, absent latency, feature isolation, typed-series exclusion, bounded retention and disposal. Verify collection depends only on metric events, independently of retained-data queries; record the archive/purge verification boundary. | Yes | 2026-11-04 |

Exit: polling frequency and number of viewers cannot change event totals or rates; storage is bounded and all collector tests pass.

### Implementation Phase 3

- GOAL-003: Render the new diagnostics and verify real activity. Depends on Phase 2.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-008 | Replace the live graph sections in `src/Presentation.Web.Jobs/Dashboard/Pages/Content.cshtml`, `src/Presentation.Web.Messaging/Dashboard/Pages/Content.cshtml` and `src/Presentation.Web.Queueing/Dashboard/Pages/Content.cshtml` with the four-chart layout. Share chart rendering where the structures match. Retain Jobs historical retained-work reporting with an explicit scope label. | Yes | 2026-11-04 |
| TASK-009 | Wire existing feature refresh controls to JSON snapshots through the shared refresher. Remove superseded live-session sampling/timers after migrating their consumers. Preserve supported routes or provide a deliberate compatibility mapping. | Yes | 2026-11-04 |
| TASK-010 | Verify all three pages with real diagnostic activity, idle sources, failure/recovery, metrics-disabled hosts, multiple viewers, theme changes, phone layouts and repeated view transitions. Run relevant .NET suites and `node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs`. | Yes | 2026-11-04 |
| TASK-011 | Update feature guides, dashboard skill references and screenshot notes. Save actual comparison captures separately from fictional public documentation illustrations. Record commands, results and measurement scope in the verification report. | Yes | 2026-11-04 |

Exit: each chart displays measured rates or latency with correct units, investigation pages retain their behavior, and browser/test evidence is recorded.

## 3. Alternatives

- **ALT-001**: Differentiate database counts. Rejected because retention and archive changes are not processing events, and short operations can occur entirely between polls.
- **ALT-002**: Count handler invocations as unique messages. Rejected because messaging fan-out makes these different quantities.
- **ALT-003**: Add distributed metric persistence. Deferred; storage-style process-local diagnostics supplies the requested operational view without introducing another telemetry backend.

## 4. Dependencies

- **DEP-001**: Existing optional `IMetricsService`, the `bdk` meter and registered application metric behaviors.
- **DEP-002**: Existing Plotly loader, chart theme/layout helpers and JSON-capable dashboard refresher.
- **DEP-003**: Existing `TimeProvider` test support and storage time-bucket implementation as references.

## 5. Files

- **FILE-001**: `src/Application.Messaging/Behaviors/MetricsMessagePublisherBehavior.cs`, `MetricsMessageHandlerBehavior.cs`.
- **FILE-002**: `src/Application.Queueing/Behaviors/MetricsQueueEnqueuerBehavior.cs`, `MetricsQueueHandlerBehavior.cs`.
- **FILE-003**: `src/Application.Jobs/JobSchedulerMetrics.cs`, `Behaviors/JobMetricsBehavior.cs`.
- **FILE-004**: Shared collector/chart support under `src/Presentation.Web/Dashboard/`; feature `Dashboard/DashboardEndpoints.cs` and `Dashboard/Pages/Content.cshtml`.
- **FILE-005**: Owning Application and Presentation tests, feature guides and `.agents/skills/dashboard-pages/references/`.

## 6. Testing

- **TEST-001**: Application behaviors emit exactly one successful completion after success and none after failure, with valid timing and balanced in-flight measurements.
- **TEST-002**: Collector tests cover complete/partial intervals, zero activity, missing timing observations, bounded memory and no duplicate aggregate/typed counting.
- **TEST-003**: Rates remain correct across different polling intervals, multiple viewers and retained-data deletion.
- **TEST-004**: Endpoints inherit custom roots and authorization. Missing instrumentation does not appear as observed success.
- **TEST-005**: Four charts initialize and refresh at desktop and phone widths; failures retain charts and recover; leaving Diagnostics stops its polling and chart ownership.

## 7. Risks & Assumptions

- **RISK-001**: Broker metrics count pipeline calls rather than unique end-to-end deliveries. Labels and documentation must preserve that distinction.
- **RISK-002**: Optional metric behaviors may not be registered. Dashboard rendering must not silently enable them.
- **RISK-003**: Runtime event history and retained Jobs history have different boundaries. Keep their scope explicit.
- **ASSUMPTION-001**: Process-local, fifteen-minute diagnostics is the desired initial scope, matching the storage diagnostics reference.

## 8. Related Specifications / Further Reading

- [Dashboard consistency specification](../docs/specs/spec-dashboard-page-consistency.md)
- [Dashboard feature guide](../docs/features-presentation-dashboard.md)
- [Jobs guide](../docs/features-jobs.md)
- [Messaging guide](../docs/features-messaging.md)
- [Queueing guide](../docs/features-queueing.md)
- [Storage metric reference](../src/Presentation.Web.Storage/StorageDashboardMetrics.cs)
