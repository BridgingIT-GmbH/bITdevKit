---
goal: Replace direct runtime meter creation with the optional shared IMetricsService
version: 1.0
date_created: 2026-08-04
last_updated: 2026-08-04
owner: bITdevKit maintainers
status: 'Completed'
tags: [refactor, metrics, observability, dependency-injection]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

This plan migrates runtime components and behaviors from direct `IMeterFactory` or `Meter` usage to the optionally registered `IMetricsService`. The migration is divided into independently verifiable stages so that the shared metrics API is established before consumers with increasing telemetry complexity are changed.

## 1. Requirements & Constraints

- **REQ-001**: Runtime behaviors and feature components must depend on `IMetricsService`, not `IMeterFactory`, `Meter`, `Counter<T>`, `UpDownCounter<T>`, or `Histogram<T>`.
- **REQ-002**: `IMeterFactory` must remain an implementation detail of `MetricsService`.
- **REQ-003**: Every migrated consumer must accept a missing `IMetricsService` and perform its underlying operation without emitting metrics.
- **REQ-004**: Existing instrument names, counter values, histogram values, units, and tags must remain unchanged unless this plan explicitly identifies a meter-name consolidation.
- **REQ-005**: Metrics failures must be best-effort and must never change the result, exception, cancellation, permit ownership, or persistence behavior of the instrumented operation.
- **REQ-006**: The shared service must support cumulative counters with arbitrary `long` values, up/down counters with arbitrary `long` deltas, arbitrary `long` and `double` histogram samples, elapsed-duration histograms, observable long gauges, units, and metric tags.
- **REQ-007**: Existing series-based APIs on `IMetricsService` must remain available during the staged migration.
- **REQ-008**: DI factories must resolve `IMetricsService` with `GetService<IMetricsService>()`; enabling a feature behavior must not implicitly register metrics.
- **REQ-009**: Direct fallback construction such as `new Meter(...)` must be removed from document storage and storage permalink telemetry.
- **REQ-010**: Static job tracing through `ActivitySource` must remain available when metrics are disabled.
- **CON-001**: `AddMetrics(options => options.Enabled(false))`, or omitting `AddMetrics`, must leave `IMetricsService` unregistered.
- **CON-002**: No architecture test, source guard, or Roslyn analyzer is required by this plan.
- **CON-003**: The public meter used after migration is `Metrics.MeterName`, currently `bdk`.
- **CON-004**: The migration must not introduce a service locator or static mutable `IMetricsService`.
- **CON-005**: Repository layering and existing project references must remain unchanged.
- **PAT-001**: Constructor dependencies use `IMetricsService metricsService = null` when parameter ordering permits; factories use `GetService<IMetricsService>()` when an optional dependency precedes required constructor arguments.
- **PAT-002**: Add `public readonly record struct MetricTag(string Name, object Value)` under `src/Common.Utilities/Metrics/` and pass tags as `ReadOnlySpan<MetricTag>`; consumers must not expose concrete instruments.
- **PAT-003**: `Metrics` remains responsible for normalized series naming, while `MetricsService` owns instrument creation, caching, and recording.
- **PAT-004**: Tests observe emitted instruments using `MeterListener`; production consumers must not receive an `IMeterFactory` solely for testability.
- **PAT-005**: Add the high-fidelity methods `AddCounter(string name, long value = 1, ReadOnlySpan<MetricTag> tags = default)`, `AddUpDownCounter(string name, long value, ReadOnlySpan<MetricTag> tags = default)`, `RecordHistogram(string name, long value, string unit = null, ReadOnlySpan<MetricTag> tags = default)`, `RecordHistogram(string name, double value, string unit = null, ReadOnlySpan<MetricTag> tags = default)`, `RecordHistogramDuration(string name, long startedTimestamp, ReadOnlySpan<MetricTag> tags = default)`, and `SetGauge(string name, long value)` to `IMetricsService`.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Extend and harden the shared metrics abstraction before migrating consumers.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Update `src/Common.Utilities/Metrics/MetricsService.cs` with the high-fidelity methods defined by PAT-005. Preserve the existing methods that accept series-family parts. | ✅ | 2026-08-04 |
| TASK-002 | Add `src/Common.Utilities/Metrics/MetricTag.cs` containing the public XML-documented `MetricTag` readonly record struct defined by PAT-002. Convert its spans to `TagList` only inside `MetricsService`; a default span must cause zero tag-array allocations. | ✅ | 2026-08-04 |
| TASK-003 | Change `MetricsService` to cache instruments by the tuple `(instrument kind, series name, unit)` using thread-safe dictionaries. Reject conflicting reuse of one series name with different instrument kinds or units through a no-op recording path rather than an exception escaping to a consumer. | ✅ | 2026-08-04 |
| TASK-004 | Centralize best-effort exception isolation in every `MetricsService` recording method and scope-disposal path. Do not catch `OutOfMemoryException`, `StackOverflowException`, or `AccessViolationException`. | ✅ | 2026-08-04 |
| TASK-005 | Refactor `src/Common.Utilities/Metrics/Metrics.cs` to retain pure naming and timestamp helpers. Mark or remove factory-based recording helpers only after all solution consumers have migrated in later phases. | ✅ | 2026-08-04 |
| TASK-006 | Extend `tests/Common.UnitTests/Utilities/Metrics/MetricsServiceTests.cs` to verify arbitrary counter values, positive and negative current values, histogram values and units, tag propagation, duration recording, instrument reuse, concurrent calls, disabled/no-consumer behavior, and isolation from a throwing `MeterListener`. | ✅ | 2026-08-04 |
| TASK-007 | Run `dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --no-restore --nologo` and require zero failures before Phase 2. | ✅ | 2026-08-04 |

### Implementation Phase 2

- GOAL-002: Migrate series-based Common and Application pipeline consumers that map directly to the existing service semantics.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Common.Utilities/Requester/Behaviors/MetricsRequestBehavior.cs`, `MetricsNotificationBehavior.cs`, and `MetricsNotificationHandlerBehavior.cs`. Replace all `Metrics.Increment`, `Metrics.ChangeCurrent`, and `Metrics.RecordDuration` recording calls with service calls while preserving generic and typed series. | ✅ | 2026-08-04 |
| TASK-009 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Application.Messaging/Behaviors/MetricsMessagePublisherBehavior.cs` and `MetricsMessageHandlerBehavior.cs`. Preserve cancellation short-circuiting and failure counting. | ✅ | 2026-08-04 |
| TASK-010 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Application.Queueing/Behaviors/MetricsQueueEnqueuerBehavior.cs` and `MetricsQueueHandlerBehavior.cs`. Preserve current-value balancing in every exception path. | ✅ | 2026-08-04 |
| TASK-011 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Application.JobScheduling/Behaviors/MetricsJobSchedulingBehavior.cs`. Preserve total, typed, current, failure, and duration series. | ✅ | 2026-08-04 |
| TASK-012 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Application.Orchestrations/Behaviors/MetricsOrchestrationBehavior.cs` and `src/Application.Orchestrations/Execution/InMemoryOrchestrationExecutor.cs`. Resolve the executor dependency once in its constructor and preserve orchestration start/finish/failure series. | ✅ | 2026-08-04 |
| TASK-013 | Update the corresponding requester, messaging, queueing, job-scheduling, and orchestration unit tests to construct `MetricsService` through the existing test meter factory and to add one no-service test per behavior family. | ✅ | 2026-08-04 |
| TASK-014 | Run the Common and Application unit-test projects sequentially and require zero failures before Phase 3. | ✅ | 2026-08-04 |

### Implementation Phase 3

- GOAL-003: Migrate Domain repository, bulk-insertion, and Active Entity behaviors.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Domain/Repositories/Behaviors/RepositoryMetricsBehavior.cs`. Reorder constructor parameters only where required to place the optional dependency last, and update every factory and direct construction site. | ✅ | 2026-08-04 |
| TASK-016 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Domain/Repositories/Behaviors/RepositoryDomainEventMetricsBehavior.cs`. Preserve aggregate and typed domain-event counters. | ✅ | 2026-08-04 |
| TASK-017 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterMetricsBehavior.cs` and `EntityBulkInserterDomainEventMetricsBehavior.cs`. Preserve result-failure and exception-failure ownership. | ✅ | 2026-08-04 |
| TASK-018 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Domain/ActiveEntity/Behaviors/ActiveEntityMetricsBehavior.cs`. Preserve the `AsyncLocal` operation stack and ensure metric-disabled execution does not push operation state. | ✅ | 2026-08-04 |
| TASK-019 | Update Domain behavior registration factories and unit tests, including `tests/Domain.UnitTests/Domain/Mediator/Repositories/Decorators/RepositoryMetricsBehaviorTests.cs` and `tests/Domain.UnitTests/Repositories/BulkInserter/EntityBulkInserterBehaviorTests.cs`. | ✅ | 2026-08-04 |
| TASK-020 | Run `dotnet test tests/Domain.UnitTests/Domain.UnitTests.csproj --no-restore --nologo` and require zero failures before Phase 4. | ✅ | 2026-08-04 |

### Implementation Phase 4

- GOAL-004: Migrate tagged file, document, and permalink storage telemetry.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-021 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Application.Storage/Files/Behaviors/MetricsFileStorageBehavior.cs`. Use the new tagged counter and histogram APIs and preserve operation, provider, outcome, byte-count, item-count, and duration semantics. | ✅ | 2026-08-04 |
| TASK-022 | Update `src/Application.Storage/Files/FileStorageProviderFactory.cs` to resolve `IMetricsService` instead of `IMeterFactory`. | ✅ | 2026-08-04 |
| TASK-023 | Replace concrete instruments and fallback `new Meter(...)` construction in `src/Application.Storage/Documents/Behaviors/MetricsDocumentStoreClientBehavior.cs` with optional `IMetricsService`. Emit `document.operations` and `document.operation.duration` through the shared `bdk` meter while preserving operation and outcome tags. | ✅ | 2026-08-04 |
| TASK-024 | Update `src/Application.Storage/Documents/Behaviors/DocumentStoreClientBehaviorServiceCollectionExtensions.cs` to resolve optional `IMetricsService`. | ✅ | 2026-08-04 |
| TASK-025 | Replace all concrete instruments and fallback `new Meter(...)` construction in `src/Application.Storage/Permalinks/StoragePermalinkMetrics.cs` with optional `IMetricsService`. Preserve permalink instrument names, units, and tags. | ✅ | 2026-08-04 |
| TASK-026 | Update file, document, and permalink tests to verify emitted values through the shared `bdk` meter and verify that an absent service is a no-op. | ✅ | 2026-08-04 |
| TASK-027 | Run `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore --nologo` and require zero failures before Phase 5. | ✅ | 2026-08-04 |

### Implementation Phase 5

- GOAL-005: Migrate blob client, upload admission, and Entity Framework chunk-flush telemetry.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-028 | Replace `IMeterFactory` with optional `IMetricsService` in `src/Application.Storage/Blobs/Behaviors/MetricsBlobStoreClientBehavior.cs`. Remove local instrument-creation helpers and retain timeout-versus-caller-cancellation ownership, per-admission histogram samples, retry metrics, result tags, and best-effort behavior. | ✅ | 2026-08-04 |
| TASK-029 | Replace concrete `UpDownCounter<long>` fields in `src/Application.Storage/Blobs/Behaviors/BlobUploadAdmissionCoordinator.cs` with optional `IMetricsService`. Record `blobstorage_uploads_active` and `blobstorage_uploads_queued` using tagged current-value changes after the internal `Interlocked` state change. | ✅ | 2026-08-04 |
| TASK-030 | Update `src/Application.Storage/Blobs/Behaviors/BlobStoreClientBehaviorServiceCollectionExtensions.cs` and the admission-coordinator registration path to resolve optional `IMetricsService`. | ✅ | 2026-08-04 |
| TASK-031 | Replace concrete counters and histograms in `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs` with optional `IMetricsService`. Preserve `blobstorage_ef_chunks_written`, `blobstorage_ef_chunk_flushes`, `blobstorage_ef_chunks_per_flush`, and `blobstorage_ef_bytes_per_flush`, including provider/store tags and units. | ✅ | 2026-08-04 |
| TASK-032 | Update `src/Infrastructure.EntityFramework/Storage/Blobs/ServiceCollectionExtensions.cs` to resolve optional `IMetricsService`. | ✅ | 2026-08-04 |
| TASK-033 | Update blob behavior, admission coordinator, EF provider unit tests, and SQLite provider contract tests. Retain explicit tests proving that listener failures cannot leak a permit or alter persistence results. | ✅ | 2026-08-04 |
| TASK-034 | Run Application unit tests, Infrastructure unit tests, and SQLite blob integration contract tests sequentially and require zero failures before Phase 6. | ✅ | 2026-08-04 |

### Implementation Phase 6

- GOAL-006: Remove static meter ownership from the provider-neutral job scheduler while preserving static tracing.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-035 | Split `src/Application.Jobs/JobSchedulerInstrumentation.cs` into static activity helpers and an injectable internal `JobSchedulerMetrics` service. The static portion must retain only `ActivitySource` operations; `JobSchedulerMetrics` must depend on optional `IMetricsService` and own every existing metric-recording method. | ✅ | 2026-08-04 |
| TASK-036 | Keep `JobSchedulerMetrics` as an internal, stateless adapter whose constructor accepts `IMetricsService metricsService = null`, so job scheduling remains functional without `AddMetrics`. | ✅ | 2026-08-04 |
| TASK-037 | Construct `JobSchedulerMetrics` from the optionally injected service in `JobMetricsBehavior`, `JobSchedulerBackgroundService`, `JobEventIngress`, `JobSchedulerMaintenanceService`, and `JobSchedulerService`. Replace static metric calls while leaving activity creation static and unchanged. | ✅ | 2026-08-04 |
| TASK-038 | Preserve all job instrument names, tag keys, status classification, histogram units, and counter values currently defined in `JobSchedulerInstrumentation`. | ✅ | 2026-08-04 |
| TASK-039 | Update `tests/Application.UnitTests/Jobs/JobSchedulerTelemetryTests.cs`, test harness construction, and affected scheduler tests to cover enabled and absent metrics services. | ✅ | 2026-08-04 |
| TASK-040 | Run all Application Jobs unit tests and require zero failures before Phase 7. | ✅ | 2026-08-04 |

### Implementation Phase 7

- GOAL-007: Remove obsolete factory-based APIs, document composition, and validate the complete migration.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-041 | Remove factory-based recording methods from `src/Common.Utilities/Metrics/Metrics.cs` after `rg -n --glob '*.cs' '\\bIMeterFactory\\b' src` reports only `MetricsService.cs`. | ✅ | 2026-08-04 |
| TASK-042 | Remove unused `System.Diagnostics.Metrics` imports and concrete instrument fields from migrated consumers. Direct `Meter`, `Counter<T>`, `UpDownCounter<T>`, and `Histogram<T>` use must remain only in `Common.Utilities/Metrics` and read-only presentation snapshot code. | ✅ | 2026-08-04 |
| TASK-043 | Update metrics documentation and affected feature documentation to state that feature metrics require optional `AddMetrics(...)` registration and become no-ops when the service is absent. | ✅ | 2026-08-04 |
| TASK-044 | Run `dotnet build --no-restore --nologo`, followed sequentially by the repository unit-test command and relevant integration-test projects. Require zero build warnings introduced by this refactoring and zero test failures. | ✅ | 2026-08-04 |
| TASK-045 | Run `git diff --check` and inspect the final diff for accidental metric-name, tag, unit, public API, or unrelated formatting changes. | ✅ | 2026-08-04 |

## 3. Alternatives

- **ALT-001**: Continue injecting `IMeterFactory` into behaviors and centralize only naming helpers. Rejected because consumers would still own instrument construction, exception isolation, caching, and optional composition.
- **ALT-002**: Register a no-op `IMetricsService` unconditionally. Rejected because the requested composition model uses an optionally registered service represented by `null`.
- **ALT-003**: Expose `Meter`, `Counter<T>`, or `Histogram<T>` through `IMetricsService`. Rejected because this would preserve concrete-instrument coupling and defeat the abstraction.
- **ALT-004**: Store `IMetricsService` in a static job instrumentation property. Rejected because it introduces mutable global state and a service-locator lifecycle.
- **ALT-005**: Migrate every consumer in one change. Rejected because staged migration provides smaller verification boundaries and isolates the static jobs composition change.
- **ALT-006**: Add an architecture test or analyzer to prohibit direct factory access. Excluded by CON-002.

## 4. Dependencies

- **DEP-001**: `System.Diagnostics.Metrics.IMeterFactory`, concrete instruments, and `MeterListener` remain internal implementation and testing dependencies.
- **DEP-002**: `Microsoft.Extensions.DependencyInjection` optional resolution through `GetService<IMetricsService>()`.
- **DEP-003**: Existing `Metrics.Series`, `Metrics.NormalizePart`, `Metrics.NormalizeTypeName`, and timestamp helpers.
- **DEP-004**: Existing Common, Domain, Application, Infrastructure, and Presentation project references.
- **DEP-005**: Existing test meter factories and `MeterListener` helpers in Common and Application unit tests.

## 5. Files

- **FILE-001**: `src/Common.Utilities/Metrics/MetricsService.cs` — expanded and hardened metrics abstraction.
- **FILE-002**: `src/Common.Utilities/Metrics/Metrics.cs` — pure naming and timing helpers after migration.
- **FILE-003**: `src/Common.Utilities/Metrics/` — repository-owned tag representation.
- **FILE-004**: `src/Common.Utilities/Requester/Behaviors/Metrics*.cs` — requester behavior migration.
- **FILE-005**: `src/Application.Messaging/Behaviors/Metrics*.cs` — messaging behavior migration.
- **FILE-006**: `src/Application.Queueing/Behaviors/Metrics*.cs` — queueing behavior migration.
- **FILE-007**: `src/Application.JobScheduling/Behaviors/MetricsJobSchedulingBehavior.cs` — job-scheduling behavior migration.
- **FILE-008**: `src/Application.Orchestrations/Behaviors/MetricsOrchestrationBehavior.cs` and `src/Application.Orchestrations/Execution/InMemoryOrchestrationExecutor.cs` — orchestration migration.
- **FILE-009**: `src/Domain/Repositories/Behaviors/` and `src/Domain/ActiveEntity/Behaviors/ActiveEntityMetricsBehavior.cs` — Domain behavior migration.
- **FILE-010**: `src/Application.Storage/Files/`, `Documents/`, and `Permalinks/` metrics consumers and factories.
- **FILE-011**: `src/Application.Storage/Blobs/Behaviors/` metrics consumers and registration.
- **FILE-012**: `src/Infrastructure.EntityFramework/Storage/Blobs/` EF chunk telemetry and registration.
- **FILE-013**: `src/Application.Jobs/JobSchedulerInstrumentation.cs`, job runtime consumers, and service registration.
- **FILE-014**: Corresponding Common, Domain, Application, Infrastructure, and integration test files.
- **FILE-015**: Metrics and affected feature documentation under `docs/`.

## 6. Testing

- **TEST-001**: Verify every `IMetricsService` operation emits the correct instrument kind, numeric type, value, unit, and tags, and verify enabled/disabled DI registration and disposal behavior.
- **TEST-002**: Verify concurrent metric recording reuses instruments safely.
- **TEST-003**: Verify throwing metric listeners do not escape from `MetricsService`.
- **TEST-004**: Verify every behavior family performs its underlying operation when `IMetricsService` is absent.
- **TEST-005**: Verify paired current counters return to zero after success, result failure, exception, timeout, and cancellation.
- **TEST-006**: Verify blob timeout and admission-cancellation metrics retain exclusive ownership.
- **TEST-007**: Verify upload admission metrics cannot leak permits or queued counts.
- **TEST-008**: Verify EF chunk counters and histograms retain values, tags, and flush boundaries.
- **TEST-009**: Verify document and permalink metrics move to the `bdk` meter without changing instrument names or tags.
- **TEST-010**: Verify job activities remain available when `IMetricsService` is absent and job metrics appear when it is registered.
- **TEST-011**: Verify full solution build, unit tests, relevant storage integration contracts, and clean diff whitespace.

## 7. Risks & Assumptions

- **RISK-001**: Changing document storage from its private meter to `bdk` can affect external dashboards that filter on the previous meter name. Document this consolidation explicitly.
- **RISK-002**: Constructor parameter changes can affect direct consumer construction even when DI composition remains source-compatible. Update all repository call sites and release notes.
- **RISK-003**: Instrument caching can expose name/type/unit conflicts that repeated direct creation previously obscured. Treat conflicts as non-fatal and cover them with tests.
- **RISK-004**: Tag representation choices can introduce allocations in high-volume blob and job paths. Benchmark or allocation-test the selected representation before Phase 5.
- **RISK-005**: Static job instrumentation has many call sites; separating tracing from metrics can accidentally drop tags or status-specific counters.
- **ASSUMPTION-001**: The `bdk` meter is the intended single meter for devkit-owned runtime metrics.
- **ASSUMPTION-002**: Optional metrics means no metric instruments are emitted when `IMetricsService` is unregistered.
- **ASSUMPTION-003**: Existing instrument names and tag keys are externally observable contracts and must remain stable.
- **ASSUMPTION-004**: No third-party source code directly constructs the affected behavior implementations as a compatibility requirement for this refactoring.

## 8. Related Specifications / Further Reading

- [Performance dashboard specification](../docs/specs/spec-presentation-performance-dashboard.md)
- [Blob storage feature documentation](../docs/features-storage-blobs.md)
- [High-volume blob upload specification](../docs/specs/spec-application-storage-blobs-high-volume-uploads.md)
- [Existing blob high-volume upload plan](./pln-feature-blob-high-volume-uploads-1.md)
