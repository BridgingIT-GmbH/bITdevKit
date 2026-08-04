---
goal: Implement bounded Blob Storage upload admission and Entity Framework chunk flushing
version: 1.0
date_created: 2026-08-04
last_updated: 2026-08-04
owner: bITdevKit maintainers
status: 'Completed'
tags: [feature, storage, blobs, entity-framework, concurrency, performance, observability]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

This plan implements the behavior and persistence changes defined by `docs/specs/spec-application-storage-blobs-high-volume-uploads.md`. The implementation adds optional process-local admission control for `IBlobStoreClient.UploadAsync`, enables bounded Entity Framework chunk grouping by default, preserves one transaction per blob, and adds deterministic observability and verification. It does not add a bulk-upload API, background upload queue, distributed limiter, resumable uploads, or database schema changes.

## 1. Requirements & Constraints

- **REQ-001**: Keep `IBlobStoreClient` and `IBlobStoreProvider` unchanged; admission is implemented as an optional `IBlobStoreClient` behavior that intercepts only `UploadAsync`.
- **REQ-002**: Add `UploadConcurrencyBlobStoreClientBehaviorOptions` with `MaxConcurrentUploads = 4`, `MaxQueuedUploads = 16`, and `QueueWaitTimeout = TimeSpan.FromSeconds(30)`.
- **REQ-003**: Validate upload-admission options during registration. Require `MaxConcurrentUploads > 0`, `MaxQueuedUploads >= 0`, and `QueueWaitTimeout > TimeSpan.Zero`; invalid values throw `InvalidOperationException`.
- **REQ-004**: Share one admission coordinator across every DI scope and client instance in the process. Isolate limiter state by normalized, case-insensitive store name.
- **REQ-005**: Enforce a hard active-upload limit, a hard waiting-queue limit, asynchronous oldest-first admission, and reliable permit release.
- **REQ-006**: Return `BlobStoreUploadOverloadedError` immediately when the waiting queue is full and `BlobStoreUploadAdmissionTimeoutError` when the configured queue wait expires.
- **REQ-007**: Preserve normal .NET cancellation. Caller cancellation while queued throws `OperationCanceledException`, removes the waiter, does not invoke the inner client, and does not leak a permit.
- **REQ-008**: Hold an acquired permit until the inner upload has returned or thrown. Release it in `finally` for success, Result failure, caller cancellation, timeout, and exception paths.
- **REQ-009**: Never read, seek, clone, buffer, rewind, or dispose `BlobUpload.Content` in the coordinator or admission behavior.
- **REQ-010**: Pass downloads, properties, existence checks, listing, updates, and deletes through without acquiring upload admission.
- **REQ-011**: Reject duplicate `WithUploadConcurrencyBehavior` registration in one `BlobStorageBuilderContext`. Do not layer multiple admission behaviors around one client.
- **REQ-012**: Keep admission errors non-transient in `RetryBlobStoreClientBehavior`; retry backoff must not hold an admission permit when retry is registered outside admission.
- **REQ-013**: Keep admission process-local. Each application process has independent per-store limits; database connection-pool and server limits remain the cross-node capacity boundary.
- **REQ-014**: Extend `BlobStoreOptions` with `ChunkFlushCount = 4` and `MaxPendingChunkBytes = ByteSize.Megabytes(16)`.
- **REQ-015**: Grouped EF chunk flushing is enabled by default. Backward compatibility with one `SaveChangesAsync` call per chunk is not required; `ChunkFlushCount = 1` remains an explicit configuration.
- **REQ-016**: Validate `ChunkFlushCount > 0` and `MaxPendingChunkBytes > 0` in `BlobStoreOptions.Validate()`.
- **REQ-017**: In `EntityFrameworkBlobStoreProvider<TContext>.WriteChunksAsync`, flush pending chunks when either the count or byte threshold is reached and flush a final non-empty partial group at end of stream.
- **REQ-018**: After each successful chunk flush, detach every flushed `StorageBlobChunk`, clear the pending list, and reset pending bytes. Do not clear or detach pending state after a failed save.
- **REQ-019**: Bound one active EF upload to `MaxPendingChunkBytes` plus at most one `ChunkSize` chunk and EF/object overhead.
- **REQ-020**: Preserve incremental SHA-256 calculation, content ordering, non-seekable streams, `MaxBlobSize`, expected-hash verification, caller stream ownership, overwrite mode, leases, and final metadata.
- **REQ-021**: Keep every relational chunk flush inside the existing upload transaction and commit exactly once after the complete stream and expected hash are validated.
- **REQ-022**: Roll back all flushed groups on size, integrity, stream, provider, or cancellation failure. Preserve the previously committed blob during failed overwrites.
- **REQ-023**: Do not load old chunk content into `BlobSnapshot` when a relational transaction provides rollback. Retain snapshot restoration only for transactionless/non-relational paths that require compensation.
- **REQ-024**: Do not apply chunk-flush settings to Azure Blob Storage or the in-memory provider.
- **REQ-025**: Emit low-cardinality admission metrics for active count, queue depth, wait duration, admissions, queue-full rejection, queue timeout, and queued cancellation.
- **REQ-026**: Emit low-cardinality EF metrics for chunks written, flush count, chunks per flush, and bytes per flush.
- **REQ-027**: Add typed/source-generated logs for admission configuration, admission outcome, and EF chunk-flush outcome. Never log blob content, property values, raw continuation tokens, stream details, or queued blob keys.
- **REQ-028**: Extend Blob Storage diagnostics with admission enabled state, configured limits, current active count, and current queued count per named store.
- **REQ-029**: Document every new public type and member with XML documentation and a usage example.
- **REQ-030**: Use deterministic concurrency tests with task-completion gates, fake time, or controllable streams; do not use timing-only sleeps as correctness assertions.
- **SEC-001**: Result errors, logs, metrics, and diagnostics may include normalized store name and configured numeric limits but must exclude container names, blob names, stream data, and property values.
- **CON-001**: Do not add a public bulk-upload method, durable queue, background upload service, distributed lock, distributed limiter, cross-blob transaction, resumable upload, or multipart upload API.
- **CON-002**: Do not create a new production or test project and do not add a database migration.
- **CON-003**: Preserve existing provider contracts and run the complete Blob Storage unit and integration contract suites.
- **PAT-001**: Follow `BlobStorageBuilderContext`, `BlobStoreClientBehaviorBase`, existing behavior registration extensions, `BlobStoreClientBehaviorTelemetry`, and `BlobStorageDiagnosticsService`.
- **PAT-002**: Use `System.Threading.RateLimiting.ConcurrencyLimiter` with `QueueProcessingOrder.OldestFirst`; do not use the time-window `Common.Utilities.RateLimiter` or an unbounded `SemaphoreSlim` waiter set.
- **PAT-003**: Use operation-owned EF scopes and contexts already created by `EntityFrameworkBlobStoreProvider<TContext>`; do not share a `DbContext` between uploads.
- **GUD-001**: Keep the first registered client behavior as the outermost decorator. Document the recommended order as logging, metrics, overall timeout, retry, upload admission, content transforms, and provider.
- **GUD-002**: Absolute throughput is not a merge gate. Deterministic boundedness, atomicity, and reduction of chunk-phase `SaveChangesAsync` calls are merge gates.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Add package, option, error, and validation contracts required by later phases.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `System.Threading.RateLimiting` version `10.0.9` to Central Package Management and add its package reference to `src/Application.Storage/Application.Storage.csproj` using the repository `dotnet add package` workflow; do not hand-edit package XML during execution. Restore and verify that `ConcurrencyLimiter` is available to `Application.Storage`. | Yes | 2026-08-04 |
| TASK-002 | Create `src/Application.Storage/Blobs/Behaviors/UploadConcurrencyBlobStoreClientBehaviorOptions.cs`. Implement the three defaults from REQ-002 and a `Validate()` method returning `Result` with `BlobStoreValidationError` for each invalid value. Add XML documentation and an example to the public class and every public property/method. Depends on no other task. | Yes | 2026-08-04 |
| TASK-003 | Extend `src/Application.Storage/Blobs/Models/BlobStoreOptions.cs` with documented `ChunkFlushCount` and `MaxPendingChunkBytes` properties using the defaults in REQ-014. Add both validation rules to `Validate()`. Depends on no other task. | Yes | 2026-08-04 |
| TASK-004 | Add documented `BlobStoreUploadOverloadedError` and `BlobStoreUploadAdmissionTimeoutError` types to `src/Application.Storage/Blobs/BlobStoreErrors.cs`. Expose only store name and configured numeric limits/timeout; use stable messages that contain no blob key or stream information. Depends on no other task. | Yes | 2026-08-04 |
| TASK-005 | Update `RetryBlobStoreClientBehavior.IsTransient` in `src/Application.Storage/Blobs/Behaviors/RetryBlobStoreClientBehavior.cs` to classify both admission errors as non-transient. Add both types to `NonRetryableErrors()` in `tests/Application.UnitTests/Storage/Blobs/BlobStoreClientBehaviorTests.cs`. Depends on TASK-004. | Yes | 2026-08-04 |
| TASK-006 | Extend `tests/Application.UnitTests/Storage/Blobs/BlobStorageModelTests.cs` with exact default assertions for `ChunkFlushCount = 4` and `MaxPendingChunkBytes = ByteSize.Megabytes(16)`, plus validation tests for zero and negative values. Add option validation tests for all upload-admission fields in a new `tests/Application.UnitTests/Storage/Blobs/UploadConcurrencyBlobStoreClientBehaviorOptionsTests.cs`. Depends on TASK-002 and TASK-003. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-001**: `Application.Storage` compiles with the concurrency-limiter dependency.
- **GATE-002**: Default and validation tests pass and prove that EF grouped flushing is default-on with a count of four.
- **GATE-003**: Retry tests prove admission errors are not retried.

### Implementation Phase 2

- GOAL-002: Implement the singleton, per-store, bounded FIFO admission coordinator.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Create `src/Application.Storage/Blobs/Behaviors/BlobUploadAdmissionCoordinator.cs` containing internal `IBlobUploadAdmissionCoordinator`, `BlobUploadAdmissionCoordinator`, `BlobUploadAdmissionLease`, immutable per-store snapshot models, and a normalized case-insensitive store-state dictionary. `AcquireAsync` receives store name, validated options, and caller token. Depends on TASK-001 and TASK-002. | Yes | 2026-08-04 |
| TASK-008 | Configure one `ConcurrencyLimiter` per normalized store with `PermitLimit = MaxConcurrentUploads`, `QueueLimit = MaxQueuedUploads`, and `QueueProcessingOrder.OldestFirst`. Reject an attempt to reuse one store with different limits by throwing `InvalidOperationException` because registration is inconsistent. Depends on TASK-007. | Yes | 2026-08-04 |
| TASK-009 | Implement queue timeout using injected `TimeProvider` and a timeout cancellation source linked with the caller token. Distinguish caller cancellation from elapsed queue timeout: caller cancellation throws; elapsed timeout returns a non-acquired lease carrying `BlobStoreUploadAdmissionTimeoutError`; an immediately failed rate-limit lease returns `BlobStoreUploadOverloadedError`. Depends on TASK-008. | Yes | 2026-08-04 |
| TASK-010 | Implement concurrency-safe active/queued counters and `GetSnapshots()` for diagnostics. Increment queued count only while asynchronously waiting, decrement it exactly once on every completion path, increment active count only after acquisition, and decrement active count exactly once when `BlobUploadAdmissionLease.DisposeAsync()` releases the underlying lease. Use optional `IMeterFactory` to publish active and queued transitions through low-cardinality `UpDownCounter<long>` instruments tagged only by normalized store. Depends on TASK-009. | Yes | 2026-08-04 |
| TASK-011 | Implement idempotent coordinator disposal. Dispose every limiter, prevent new acquisitions, and cause queued acquisitions to complete through cancellation/failure without leaking counters or permits. Depends on TASK-010. | Yes | 2026-08-04 |
| TASK-012 | Create `tests/Application.UnitTests/Storage/Blobs/BlobUploadAdmissionCoordinatorTests.cs`. Use deterministic task-completion gates and `FakeTimeProvider` to prove active limits, queue limits, FIFO order, immediate overload, queue timeout, caller cancellation, permit release, disposal, same-store sharing, case-insensitive store normalization, different-store isolation, and snapshot counters. Depends on TASK-011. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-004**: No execution path can exceed configured active or queued counts.
- **GATE-005**: FIFO, cancellation, timeout, disposal, and permit-release tests pass without timing-only sleeps.
- **GATE-006**: The coordinator has no unbounded waiter collection outside `ConcurrencyLimiter`.

### Implementation Phase 3

- GOAL-003: Compose upload admission into named Blob Storage clients without changing non-upload operations.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Create `src/Application.Storage/Blobs/Behaviors/UploadConcurrencyBlobStoreClientBehavior.cs` as a documented public behavior derived from `BlobStoreClientBehaviorBase`. Implement both protected `ExecuteAsync` overloads; in the generic overload, apply admission only when `operation == "upload"` and `context.Upload` is present, otherwise invoke `next` directly; the non-generic overload always invokes `next` directly. For upload, check caller cancellation, await the coordinator lease, map non-acquired leases to their typed Result error, invoke `next` exactly once after acquisition, and release in `finally`. Depends on TASK-011. | Yes | 2026-08-04 |
| TASK-014 | Add an internal behavior-registration key set and `WithBehaviorOnce` helper to `src/Application.Storage/Blobs/BlobStorageBuilderContext.cs`. Reject a repeated upload-admission key with `InvalidOperationException` while preserving existing composition semantics for all other behavior registrations. Depends on no other Phase 3 task. | Yes | 2026-08-04 |
| TASK-015 | Add documented `WithUploadConcurrencyBehavior(Action<UploadConcurrencyBlobStoreClientBehaviorOptions>)` to `src/Application.Storage/Blobs/Behaviors/BlobStoreClientBehaviorServiceCollectionExtensions.cs`. Validate options immediately, register `IBlobUploadAdmissionCoordinator`/`BlobUploadAdmissionCoordinator` once as singleton, register the keyed behavior through `WithBehaviorOnce`, and resolve optional `TimeProvider`, `ILoggerFactory`, and `IMeterFactory`. Depends on TASK-013 and TASK-014. | Yes | 2026-08-04 |
| TASK-016 | Add `tests/Application.UnitTests/Storage/Blobs/UploadConcurrencyBlobStoreClientBehaviorTests.cs`. Prove the inner upload is invoked once only after admission; success, Result failure, exception, and cancellation release permits; queue-full and queue-timeout never invoke the inner client; queued streams are never read or disposed; all non-upload methods bypass admission. Depends on TASK-015. | Yes | 2026-08-04 |
| TASK-017 | Extend `tests/Application.UnitTests/Storage/Blobs/BlobStoreClientBehaviorTests.cs` and `BlobStorageRuntimeShellTests.cs` to prove behavior order with metrics/timeout/retry, timeout includes queue wait when outermost, retry reacquires after releasing the prior permit, singleton coordinator sharing across DI scopes, independent named-store state, optional absence by default, and duplicate registration rejection. Depends on TASK-015 and TASK-016. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-007**: The public blob client interface remains byte-for-byte unchanged.
- **GATE-008**: DI tests prove all scopes for one named store share one coordinator and different stores do not share limiter state.
- **GATE-009**: Stream-ownership and behavior-order tests pass for success and failure paths.

### Implementation Phase 4

- GOAL-004: Implement default-on bounded EF chunk flushing and remove unnecessary relational overwrite snapshots.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-018 | Refactor `EntityFrameworkBlobStoreProvider<TContext>.WriteChunksAsync` in `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs` to maintain `List<StorageBlobChunk> pendingChunks` and `long pendingChunkBytes`; add each chunk after size/hash processing; flush when `pendingChunks.Count >= ChunkFlushCount` or `pendingChunkBytes >= MaxPendingChunkBytes`; flush the final non-empty group. Depends on TASK-003. | Yes | 2026-08-04 |
| TASK-019 | Add private `FlushChunksAsync` in the same provider. Call `SaveChangesAsync`, then detach all pending chunk entities, clear the list, and reset pending bytes only after a successful save. Return flush counts/bytes to the caller for telemetry without retaining chunk content. Depends on TASK-018. | Yes | 2026-08-04 |
| TASK-020 | Refactor upload snapshot selection in `EntityFrameworkBlobStoreProvider<TContext>.UploadAsync` and `CreateSnapshotAsync`. When a relational transaction was successfully opened, retain only metadata already needed by the tracked target row and do not query/materialize previous chunk content. Keep complete `BlobSnapshot` content only for transactionless/non-relational restoration. Depends on TASK-018. | Yes | 2026-08-04 |
| TASK-021 | Preserve failure semantics in `UploadAsync`: all flushed groups remain under the existing transaction; expected hash and final metadata are validated/saved before the single commit; rollback and non-cancelable restoration paths remain unchanged in intent. Add comments only where necessary to explain why flushed rows are not public commits. Depends on TASK-019 and TASK-020. | Yes | 2026-08-04 |
| TASK-022 | Extend `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderUploadDownloadTests.cs` with an instrumented test context/interceptor that separates placeholder, chunk-phase, fallback, and final metadata saves. Prove count threshold, byte threshold, final partial flush, `ChunkFlushCount = 1`, detachment, content order, hash, length, and non-seekable behavior. Depends on TASK-021. | Yes | 2026-08-04 |
| TASK-023 | Add failure tests in the same file proving size limit, expected-hash mismatch, caller cancellation, throwing stream, and `SaveChangesAsync` failure after one or more successful groups leave no partial new blob and preserve an overwritten blob. Depends on TASK-021. | Yes | 2026-08-04 |
| TASK-024 | Add a relational query-observation test proving an overwrite with an active transaction does not enumerate old `StorageBlobChunk.Content` into a rollback snapshot. Keep non-relational restoration coverage proving the compensation path still restores old chunks. Depends on TASK-020. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-010**: For `N` chunks with no byte-threshold reduction, chunk-phase saves equal `ceil(N / ChunkFlushCount)`.
- **GATE-011**: Pending tracked content remains bounded by configured pending bytes plus one chunk, and flushed chunk entities are detached.
- **GATE-012**: Every failure-path test proves no partial blob becomes committed.
- **GATE-013**: Relational overwrite tests prove old chunk content is not materialized solely for rollback.

### Implementation Phase 5

- GOAL-005: Add low-cardinality telemetry, typed logs, and diagnostics for admission and chunk flushing.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-025 | Extend `src/Application.Storage/Blobs/Behaviors/BlobStoreClientBehaviorTelemetry.cs` with admission outcome, wait duration, queued cancellation, and admission timeout/rejection counters for the current async operation. Update `MetricsBlobStoreClientBehavior` to emit admitted count, wait histogram, rejection count, timeout count, and cancellation count with only `operation`, `store`, and bounded outcome tags. Keep active/queued gauges in the coordinator `UpDownCounter` instruments from TASK-010. Depends on TASK-013. | Yes | 2026-08-04 |
| TASK-026 | Add source-generated `TypedLogger` methods to `UploadConcurrencyBlobStoreClientBehavior` for enabled limits, admitted uploads, queue-full rejection, queue timeout, queued cancellation, and unexpected coordinator failure. Use Debug/Trace for normal flow and Warning for overload/timeout. Depends on TASK-013. | Yes | 2026-08-04 |
| TASK-027 | Add EF chunk-flush counters/histograms and source-generated logs in `EntityFrameworkBlobStoreProvider<TContext>`. Resolve optional `IMeterFactory` and `ILoggerFactory` through `src/Infrastructure.EntityFramework/Storage/Blobs/ServiceCollectionExtensions.cs`; preserve optional constructor parameters so direct unit construction remains simple. Emit store/provider identifiers, chunk count, and byte count only. Depends on TASK-019. | Yes | 2026-08-04 |
| TASK-028 | Extend `src/Application.Storage/Blobs/Diagnostics/BlobStorageClientDiagnostics.cs` with admission enabled, max concurrent, max queued, active, and queued properties. Update `BlobStorageDiagnosticsService` to optionally consume coordinator snapshots and map them by normalized client name; report disabled/zero values when the behavior is absent. Depends on TASK-010. | Yes | 2026-08-04 |
| TASK-029 | Extend `tests/Application.UnitTests/Storage/Blobs/BlobStoreClientBehaviorTests.cs`, `BlobStorageRuntimeShellTests.cs`, and `BlobStorageConvenienceExtensionsTests.cs` to assert metric names/counts/tags, typed log levels, diagnostics values, disabled behavior diagnostics, and absence of blob keys/content/property values. Add EF telemetry assertions to `EntityFrameworkBlobStoreProviderUploadDownloadTests.cs`. Depends on TASK-025 through TASK-028. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-014**: Operators can distinguish admission, queue-full, queue-timeout, caller cancellation, and provider failure.
- **GATE-015**: Diagnostics report current limits and counts per named store without exposing queued keys.
- **GATE-016**: Telemetry privacy tests prove no high-cardinality blob identity or content is emitted.

### Implementation Phase 6

- GOAL-006: Verify relational atomicity, provider concurrency, and performance characteristics.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-030 | Extend `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderContractTestsBase.cs` with shared high-volume tests inherited by SQLite, SQL Server, and PostgreSQL suites. Verify several flush groups commit atomically, a second context cannot observe partial replacement content, and rollback removes every flushed group. Depends on TASK-021. | Yes | 2026-08-04 |
| TASK-031 | Add integration cases to the same shared base proving simultaneous uploads to different keys remain independent and simultaneous uploads to the same key preserve existing lease/conflict semantics under grouped flushing. Depends on TASK-030. | Yes | 2026-08-04 |
| TASK-032 | Add an integration registration case that resolves the EF named client from multiple scopes, blocks inner uploads deterministically, and proves configured admission never permits more than `MaxConcurrentUploads` provider operations. Depends on TASK-015 and TASK-030. | Yes | 2026-08-04 |
| TASK-033 | Add `benchmarks/Application.Benchmarks/BlobStorageHighVolumeUploadBenchmarks.cs`. Compare `ChunkFlushCount = 1` and `4` for a fixed deterministic payload and report total bytes, chunk count, flush count, elapsed time, and allocation data. Add the benchmark to the existing `Application.Benchmarks` assembly without introducing a throughput pass/fail threshold. Depends on TASK-021 and TASK-027. | Yes | 2026-08-04 |
| TASK-034 | Run targeted tests sequentially: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --nologo --filter FullyQualifiedName~Storage.Blobs`, then `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --nologo --filter FullyQualifiedName~Storage.Blobs`, then configured SQLite/SQL Server/PostgreSQL Blob Storage integration tests. Record any environment-skipped container suite in the implementation handoff. Depends on TASK-029 through TASK-032. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-017**: All available provider suites preserve atomic upload, overwrite, lease, cancellation, and stream-ownership contracts.
- **GATE-018**: Relational tests prove no partial chunk group is visible after rollback.
- **GATE-019**: Benchmark output demonstrates fewer chunk-phase saves for count four than count one; absolute throughput remains informational.

### Implementation Phase 7

- GOAL-007: Finalize documentation, architectural record, and repository-wide validation.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-035 | Update `docs/features-storage-blobs.md` with registration examples, default limits, queue-full/timeout handling, behavior-order effects, per-process scope, memory-budget formula, EF default-on flush count four, diagnostics, and tuning guidance. Depends on all implementation behavior being final. | Yes | 2026-08-04 |
| TASK-036 | Update `docs/specs/spec-application-storage-blobs.md` so its normative option table, behavior catalog, errors, observability, EF algorithm, and acceptance criteria agree with the implemented high-volume specification. Remove or revise any statement that implies grouped EF flushing is opt-in. Depends on TASK-035. | Yes | 2026-08-04 |
| TASK-037 | Create proposed ADR `docs/adr/0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md` using MADR. Record process-local per-store admission, bounded FIFO waiting, default-on EF group size four, 16 MB pending-byte cap, transaction atomicity, alternatives, and consequences. Add ADR-0030 to `docs/adr/README.md`. Depends on the final implementation. | Yes | 2026-08-04 |
| TASK-038 | Run `dotnet build --nologo /p:UseSharedCompilation=false`, then repository unit tests, then configured integration tests sequentially. Do not run top-level build/test commands concurrently. Resolve warnings/errors caused by this feature without modifying unrelated user changes. Depends on TASK-034 through TASK-037. | Yes | 2026-08-04 |
| TASK-039 | Run formatting/quality validation: repository format task for touched files, `git diff --check`, XML-documentation/source-guard tests, and a diff review proving no public bulk API, background queue, migration, or unrelated package update was introduced. Depends on TASK-038. | Yes | 2026-08-04 |

Completion criteria:

- **GATE-020**: Implementation, base specification, feature documentation, and ADR describe the same defaults and semantics.
- **GATE-021**: Repository build and all available relevant tests pass sequentially.
- **GATE-022**: The final diff contains no migration, new project, public bulk-upload API, or unrelated change.

## 3. Alternatives

- **ALT-001**: Use `SemaphoreSlim` directly. Rejected because awaiting callers would form an unbounded queue unless a second queue implementation were added, and FIFO plus cancellation would need custom correctness work.
- **ALT-002**: Use `Common.Utilities.RateLimiter`. Rejected because it limits operations per time window rather than simultaneous active operations and bounded waiting.
- **ALT-003**: Implement a custom FIFO admission queue. Rejected in favor of `System.Threading.RateLimiting.ConcurrencyLimiter`, which already provides bounded FIFO admission and cancellation-safe leases.
- **ALT-004**: Apply one process-wide limiter to every blob store. Rejected because stores may use different providers, databases, accounts, and capacity budgets.
- **ALT-005**: Add a distributed concurrency limiter. Rejected because it adds coordination latency and a new shared dependency; database pool/server limits remain the cross-node boundary.
- **ALT-006**: Add `UploadManyAsync` or a background upload service. Rejected because caller-owned streams, per-item results, durability, and restart semantics require a separate API design.
- **ALT-007**: Keep per-chunk `SaveChangesAsync` as the default. Rejected because grouped flushing is a new feature with no backward-compatibility requirement; count one remains explicitly configurable.
- **ALT-008**: Buffer the entire blob before one save. Rejected because it violates stream-first behavior and introduces blob-sized memory consumption.
- **ALT-009**: Flush only by chunk count. Rejected because chunk sizes are configurable and a byte cap is required to bound memory independently.
- **ALT-010**: Materialize old relational chunks for overwrite compensation. Rejected because the active database transaction already provides rollback and full snapshotting doubles large-blob memory pressure.

## 4. Dependencies

- **DEP-001**: `System.Threading.RateLimiting` version `10.0.9`, managed through repository Central Package Management and referenced by `Application.Storage`.
- **DEP-002**: Existing `BlobStorageBuilderContext`, `BlobStoreClientBehaviorBase`, `BlobStoreClientBehaviorTelemetry`, `BlobStoreClientFactory`, and keyed named-client registrations.
- **DEP-003**: Existing `EntityFrameworkBlobStoreProvider<TContext>`, `IBlobStoreContext`, `StorageBlob`, and `StorageBlobChunk` implementation.
- **DEP-004**: Existing `Result`, typed Blob Storage errors, source-generated logging, `IMeterFactory`, and diagnostics conventions.
- **DEP-005**: `Microsoft.Extensions.TimeProvider.Testing` already referenced by `Application.UnitTests` for deterministic queue-timeout tests.
- **DEP-006**: Existing SQLite, SQL Server, and PostgreSQL EF integration fixtures; unavailable external container environments may skip their corresponding suites but do not remove the test implementation requirement.
- **DEP-007**: `docs/specs/spec-application-storage-blobs-high-volume-uploads.md` is the authoritative feature specification.

## 5. Files

- **FILE-001**: `Directory.Packages.props` — central `System.Threading.RateLimiting` version added through CLI workflow.
- **FILE-002**: `src/Application.Storage/Application.Storage.csproj` — concurrency-limiter package reference added through CLI workflow.
- **FILE-003**: `src/Application.Storage/Blobs/Behaviors/UploadConcurrencyBlobStoreClientBehaviorOptions.cs` — new admission options.
- **FILE-004**: `src/Application.Storage/Blobs/Behaviors/BlobUploadAdmissionCoordinator.cs` — new singleton coordinator, lease, and snapshots.
- **FILE-005**: `src/Application.Storage/Blobs/Behaviors/UploadConcurrencyBlobStoreClientBehavior.cs` — new upload-only decorator.
- **FILE-006**: `src/Application.Storage/Blobs/Behaviors/BlobStoreClientBehaviorServiceCollectionExtensions.cs` — fluent registration.
- **FILE-007**: `src/Application.Storage/Blobs/Behaviors/BlobStoreClientBehaviorTelemetry.cs` — admission telemetry.
- **FILE-008**: `src/Application.Storage/Blobs/Behaviors/MetricsBlobStoreClientBehavior.cs` — admission metrics mapping.
- **FILE-009**: `src/Application.Storage/Blobs/Behaviors/RetryBlobStoreClientBehavior.cs` — non-transient admission errors.
- **FILE-010**: `src/Application.Storage/Blobs/BlobStorageBuilderContext.cs` — duplicate behavior guard.
- **FILE-011**: `src/Application.Storage/Blobs/BlobStoreErrors.cs` — overload and admission-timeout errors.
- **FILE-012**: `src/Application.Storage/Blobs/Models/BlobStoreOptions.cs` — EF flush defaults and validation.
- **FILE-013**: `src/Application.Storage/Blobs/Diagnostics/BlobStorageClientDiagnostics.cs` — per-store admission diagnostics.
- **FILE-014**: `src/Application.Storage/Blobs/Diagnostics/BlobStorageDiagnosticsService.cs` — coordinator snapshot projection.
- **FILE-015**: `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs` — chunk grouping, transaction-safe rollback, snapshot optimization, telemetry.
- **FILE-016**: `src/Infrastructure.EntityFramework/Storage/Blobs/ServiceCollectionExtensions.cs` — optional telemetry dependencies.
- **FILE-017**: `tests/Application.UnitTests/Storage/Blobs/UploadConcurrencyBlobStoreClientBehaviorOptionsTests.cs` — option validation.
- **FILE-018**: `tests/Application.UnitTests/Storage/Blobs/BlobUploadAdmissionCoordinatorTests.cs` — coordinator concurrency suite.
- **FILE-019**: `tests/Application.UnitTests/Storage/Blobs/UploadConcurrencyBlobStoreClientBehaviorTests.cs` — behavior suite.
- **FILE-020**: `tests/Application.UnitTests/Storage/Blobs/BlobStoreClientBehaviorTests.cs` — behavior composition and retry coverage.
- **FILE-021**: `tests/Application.UnitTests/Storage/Blobs/BlobStorageModelTests.cs` — EF option defaults/validation.
- **FILE-022**: `tests/Application.UnitTests/Storage/Blobs/BlobStorageRuntimeShellTests.cs` — DI and diagnostics registration.
- **FILE-023**: `tests/Application.UnitTests/Storage/Blobs/BlobStorageConvenienceExtensionsTests.cs` — diagnostics output.
- **FILE-024**: `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderUploadDownloadTests.cs` — chunk flushing and rollback tests.
- **FILE-025**: `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderContractTestsBase.cs` — shared relational integration coverage.
- **FILE-026**: `benchmarks/Application.Benchmarks/BlobStorageHighVolumeUploadBenchmarks.cs` — diagnostic benchmark.
- **FILE-027**: `docs/features-storage-blobs.md` — feature usage and tuning documentation.
- **FILE-028**: `docs/specs/spec-application-storage-blobs.md` — base normative specification alignment.
- **FILE-029**: `docs/adr/0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md` — proposed architectural decision.
- **FILE-030**: `docs/adr/README.md` — ADR index entry.

## 6. Testing

- **TEST-001**: Verify exact admission and EF flush defaults and reject all non-positive/negative invalid option combinations.
- **TEST-002**: Verify at most `MaxConcurrentUploads` leases are active for one normalized store across different DI scopes.
- **TEST-003**: Verify at most `MaxQueuedUploads` callers wait and the next caller receives `BlobStoreUploadOverloadedError` without provider invocation.
- **TEST-004**: Verify oldest-first admission order with deterministic gates.
- **TEST-005**: Verify fake-time queue expiry returns `BlobStoreUploadAdmissionTimeoutError`.
- **TEST-006**: Verify caller cancellation throws `OperationCanceledException`, removes the waiter, and leaks neither queue count nor permit.
- **TEST-007**: Verify success, Result failure, thrown exception, timeout, and post-admission cancellation release permits.
- **TEST-008**: Verify different named stores have independent permits and case variants of one normalized store share permits.
- **TEST-009**: Verify non-upload operations bypass admission and queued upload streams are never read, rewound, buffered, or disposed.
- **TEST-010**: Verify duplicate behavior registration fails and absence of the behavior leaves uploads unrestricted by DevKit.
- **TEST-011**: Verify admission errors are non-transient and retry outside admission releases/reacquires permits between attempts.
- **TEST-012**: Verify chunk flush by count, by bytes, and at final partial group.
- **TEST-013**: Verify count one retains per-chunk flushing while the default count four reduces chunk-phase saves to `ceil(N / 4)` when bytes do not lower the group.
- **TEST-014**: Verify every successfully flushed chunk is detached and pending tracked bytes/count remain bounded.
- **TEST-015**: Verify content order, length, SHA-256, non-seekable streams, stream ownership, and final metadata remain unchanged.
- **TEST-016**: Verify size, integrity, cancellation, throwing-stream, and database-save failures roll back every prior flush group.
- **TEST-017**: Verify failed overwrite preserves old metadata/content and relational overwrite does not materialize old chunks solely for rollback.
- **TEST-018**: Verify concurrent different-key uploads remain independent and same-key uploads retain lease/conflict behavior.
- **TEST-019**: Verify a second context cannot observe a partially uploaded relational replacement before commit.
- **TEST-020**: Verify admission and chunk telemetry names, values, low-cardinality tags, diagnostics snapshots, and privacy constraints.
- **TEST-021**: Run existing in-memory, Azure, EF unit, and EF integration Blob Storage contract suites as regressions.
- **TEST-022**: Benchmark count one versus count four and record save count, elapsed time, and allocations without enforcing environment-specific throughput.

## 7. Risks & Assumptions

- **RISK-001**: Each process enforces its own limit, so total database concurrency equals approximately `application node count * MaxConcurrentUploads`. Mitigation: document the formula and require tuning against connection-pool/server capacity.
- **RISK-002**: Four active uploads with default settings retain approximately 64 MB of pending chunk bytes plus read buffers, EF tracking, transforms, and stream overhead. Mitigation: expose both active count and pending-byte options and document the memory budget.
- **RISK-003**: Long uploads hold relational transactions and connections for the complete stream duration. Mitigation: admission bounds concurrent transactions; operational metrics expose wait and duration; distributed scheduling remains outside this feature.
- **RISK-004**: Incorrect cancellation classification could turn queue timeout into caller cancellation or leak permits. Mitigation: use separate caller/timeout tokens and deterministic fake-time tests for every completion race.
- **RISK-005**: Detaching chunk entities before a successful save could lose pending data. Mitigation: detach and clear only after `SaveChangesAsync` succeeds and add failure-injection tests.
- **RISK-006**: Removing relational snapshots could expose rollback differences for SQLite or transactionless test paths. Mitigation: select snapshot strategy from the actual acquired transaction and preserve compensation tests for non-transactional paths.
- **RISK-007**: Adding telemetry directly in hot chunk loops may create excess allocations. Mitigation: aggregate per flush, use source-generated logs, and avoid per-chunk high-cardinality tags.
- **RISK-008**: Multiple `AddBlobStorage` builder flows could attempt inconsistent options for one named store. Mitigation: existing duplicate named-client validation remains authoritative and coordinator state rejects inconsistent limits.
- **ASSUMPTION-001**: `System.Threading.RateLimiting` version `10.0.9` is compatible with the repository's .NET 10 and Microsoft.Extensions package line.
- **ASSUMPTION-002**: The locked design intentionally defaults `ChunkFlushCount` to four; no compatibility shim or migration is required.
- **ASSUMPTION-003**: Existing relational upload transactions provide atomic rollback for flushed chunks on supported SQLite, SQL Server, and PostgreSQL configurations.
- **ASSUMPTION-004**: Store names are low-cardinality deployment configuration and are acceptable metric dimensions; blob container/name values are not.
- **ASSUMPTION-005**: Application callers retain ownership and lifetime of upload streams until `UploadAsync` completes, including queue wait.

## 8. Related Specifications / Further Reading

- [Blob Storage high-volume upload control specification](../docs/specs/spec-application-storage-blobs-high-volume-uploads.md)
- [Base Blob Storage design specification](../docs/specs/spec-application-storage-blobs.md)
- [Blob Storage feature documentation](../docs/features-storage-blobs.md)
- [ADR-0016: Logging and observability strategy](../docs/adr/0016-logging-observability-strategy.md)
- [ADR-0018: Dependency injection and service lifetimes](../docs/adr/0018-dependency-injection-service-lifetimes.md)
- [Microsoft .NET concurrency limiter API](https://learn.microsoft.com/dotnet/api/system.threading.ratelimiting.concurrencylimiter)
