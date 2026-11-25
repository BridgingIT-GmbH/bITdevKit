---
goal: Fix blob expiration, retry classification, EF clocks, and client tracing
version: 1.0
date_created: 2026-11-25
last_updated: 2026-11-25
owner: bITdevKit maintainers
status: 'Completed'
tags: [bug, storage, blobs, entity-framework, observability]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Implement four commits in order. Expiration affects provider visibility and replacement in Application and Infrastructure. Retry and tracing use the existing Application client behavior pipeline. EF supplies persistence-specific error classification and consumes a host clock. The starting revision is `1ae3dc837`.

Implementation commits in order.

- `ceca4fd90` fix(storage): hide expired blobs
- `180cc3df7` fix(storage): retry only transient blob errors
- `9ff916c7a` fix(storage): use TimeProvider for EF blobs
- `e1ebc939d` feat(storage): trace blob client operations

## 1. Requirements & Constraints

- **REQ-001**. A blob expires when `ExpiresAt` has a value at or before the UTC instant captured for the operation. Download, batch download, properties, property update, and supported dimension update report `BlobStoreNotFoundError`. Exists returns false. Both listing APIs and the container catalog exclude expired blobs. Providers without dimensions keep returning their existing unsupported error for dimension operations.
- **REQ-002**. Replace an expired blob with a fresh identity and creation time. `FailIfExists` succeeds and `IfMatchETag` conflicts. Existing properties, chunks, and dimensions from the expired version cannot carry over. Explicit delete continues to work on the physical record and stays idempotent. The sweep uses its own `ExpiresOnOrBefore` even if it differs from the current clock. Reads perform no deletion.
- **REQ-003**. Retry a failed result only when it has at least one error and every error implements `ITransientStorageError` with `IsTransient == true`. `BlobStoreProviderError` defaults to permanent. `BlobStoreTimeoutError` is transient. Preserve retry attempt limits, cancellation, stream rewind rules, and the dimension upload guard.
- **REQ-004**. Walk `Exception.InnerException` for EF failures. A `TimeoutException` is transient. A `Microsoft.Data.SqlClient.SqlException` is transient if `IsTransient` is true or an entry in its `Errors` has a number in `1205, -2, 1222, 233, 64, 121, 10053, 10054, 10060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920`. Other exceptions default to permanent.
- **REQ-005**. EF uses the registered optional `TimeProvider` or `TimeProvider.System` for expiry checks, persistence timestamps, lease timing, and sweep completion.
- **REQ-006**. The opt-in tracing decorator starts one internal `blobs.{operation}` activity per decorated call. Its operation tags are `blobs.operation` and `blobs.store`. Failure adds `error.type` and sets the Error status description to the first result error's type name or the exception type name. Rethrow the original exception. The no-listener path forwards the call directly.
- **SEC-001**. Keep blob keys, containers, continuation tokens, property values, and error and exception messages out of activity names, tags, status descriptions, and events.
- **CON-001**. Keep shipped signatures and provider interfaces binary and source compatible. Retain old constructor signatures as forwarding overloads. Use existing projects, tables, Azure metadata, and filesystem record formats. Add XML documentation with an `<example>` for each new public or protected member, including overrides and overloads.
- **CON-002**. Make four commits with tests in each and no additional implementation commits. Update `docs/features-storage-blobs.md` and the unreleased section of `CHANGELOG.md` in the corresponding phase. If this plan file is still untracked, leave it out of the implementation commits. Follow the repository's git-commit skill when staging and committing.
- **PAT-001**. Use xUnit, Shouldly, and NSubstitute where appropriate. Inherit the current provider contract. Reuse the existing lease, publication, continuation, DI, and Result mechanisms.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001. Enforce the expiration contract before changing retry behavior, the EF clock, or tracing. Commit as `fix(storage): hide expired blobs`.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | In FILE-001, capture a single cutoff per operation using `DateTimeOffset.UtcNow` for this phase. Add a private expiry predicate for materialized rows. Apply it in `DownloadAsync` before handing out inline or streaming content and in `DownloadManyAsync` before adding a found key or creating a result stream. For the remaining direct lookups, treat expired as missing in `GetPropertiesAsync` and `UpdatePropertiesAsync`. Check expiry before the ETag in update paths. Use a live-row predicate in `ExistsAsync`, `ListPageAsync(BlobQuery)`, `ListContainersAsync`, and the FilterModel `ListPageAsync` in FILE-003. Put it on the root query before scope, dimension, seek, and page operations. Apply the same point-lookup rule to the private `UpdateDimensionsAsync(BlobDimensionsUpdate, StoredBlobPropertiesUpdate, CancellationToken)` overload in FILE-002, including property updates routed through it. Keep compiled single and batch download shapes and the existing large-blob fallback. | ✅ | 2026-11-25 |
| TASK-002 | In FILE-004, add a private composable live-row query for the list, catalog, and existence paths in TASK-001. For non-SQLite models compose `ExpiresAt == null` or `ExpiresAt > now` in LINQ. For SQLite use the retention query's delimited, model-derived table and column identifiers and UTC parameter type mapping with a parameterized `FromSqlRaw` root and the live predicate. Keep the sweep's existing query, cutoff, ordering, and lease logic. | ✅ | 2026-11-25 |
| TASK-003 | After TASK-001, in FILE-001 `UploadAsync`, inspect physical rows under the current mutation gate and transaction. For an expired row, return a conflict for any `IfMatchETag` before destructive work. Otherwise acquire the old row's existing version-checked lease, remove the old row and save before inserting its replacement because the key is unique. Use relational cascade for chunks and dimensions and explicit dependent cleanup for the nonrelational path where cascade does not load dependents. Treat the new row as an insert with a new Id, ETag, and `CreatedAt`. Preserve relational rollback and the existing new-row compensation path for a failed nonrelational upload. Leave `DeleteAsync` on the physical lookup with its existing conditional-delete semantics. | ✅ | 2026-11-25 |
| TASK-004 | In FILE-005 retain the existing public constructor and add a clock-taking overload. Resolve the clock in FILE-007 with `GetService<TimeProvider>()` and use `TimeProvider.System` for null. In FILE-006 add internal expiry-aware lookup, existence, snapshot, store, and update paths with the provider's cutoff. Use the existing context lock to make the expiry and ETag decisions atomic. For a successful write over an expired entry discard the old entry and inherited metadata. Preserve the raw context APIs and their signatures. Use the clock for in-memory timestamps and apply the live paths in `DownloadAsync`, `GetPropertiesAsync`, `UpdatePropertiesAsync`, `ExistsAsync`, `ListPageAsync`, and `ListContainersAsync`. Keep explicit delete and sweep on raw entries. | ✅ | 2026-11-25 |
| TASK-005 | In FILE-008 compare expiry before returning download or metadata and under write authority for `UpdatePropertiesAsync`. `ExistsAsync` follows the properties result. Apply the cutoff before collecting `ListPageAsync` rows and adding `ListContainersAsync` names. During `UploadAsync`, treat the expired head as absent under the same path authority, reject conditional uploads with an ETag, and publish the staged replacement as a fresh record through the current filesystem publication path. Let publication reclaim the old payload. Preserve scan bounds, unconditional delete, and sweep discovery. | ✅ | 2026-11-25 |
| TASK-006 | In FILE-009 filter expiry using `bdk_expiresat` metadata. `DownloadAsync` checks the properties before opening content, `GetPropertiesAsync` and `UpdatePropertiesAsync` return NotFound for expired properties, and `ExistsAsync` returns false through the same metadata lookup. In `ListPageAsync` keep fetching native pages with a hint of at most the remaining requested items, filtering with one cutoff and reusing the native token until the logical page is full or Azure reaches the end. `ListContainersAsync` retains only containers that yield a live item by scanning native metadata pages with early exit. Treat a container deleted during the catalog scan as empty. | ✅ | 2026-11-25 |
| TASK-007 | After TASK-006, in FILE-009 check current Azure metadata before forwarding a valid `UploadAsync` to the backend. Add a documented `GetPropertiesForExpirationAsync(BlobKey, CancellationToken)` default interface method in FILE-010 that delegates to `GetPropertiesAsync`. The native implementation reads metadata and ETag with one properties request. Use this lookup for upload preflight and Exists. A missing physical blob follows the existing upload path. For an expired blob, reject `IfMatchETag`, delete the observed version using its ETag before staging any blocks, and publish the replacement create-only. A concurrent ETag mismatch returns conflict. In FILE-010 add a documented `OpenReadAsync(BlobKey, string ifMatchETag, CancellationToken)` overload with a default implementation delegating to the old signature. Use it from FILE-009 with the ETag checked for expiry. Implement it in the native backend with `BlobOpenReadOptions` and `BlobRequestConditions.IfMatch`. Exercise the transport in FILE-011. | ✅ | 2026-11-25 |
| TASK-008 | In FILE-012 and the cache registration in FILE-013, use the registered `TimeProvider` with system fallback, keeping the old cache constructor. Skip an expired cache hit and delegate to the provider so a fresh replacement can be returned. Keep the cache's invalidation behavior. Never delete provider data on a cache miss. | ✅ | 2026-11-25 |
| TASK-009 | After TASK-001 through TASK-008, add TEST-001 through TEST-006, update the sweep fixtures in TEST-013, and run CHECK-001 through CHECK-003. In FILE-020 document the inclusive cutoff, invisible reads, explicit delete, sweep, and fresh upload behavior. Create `## [Unreleased]` in FILE-021 above the latest version and add the expiration fix. Verify every concrete provider inherits the new contract fact before committing. | ✅ | 2026-11-25 |

### Implementation Phase 2

- GOAL-002. Retry only results explicitly marked transient. Begin after the Phase 1 commit. Commit as `fix(storage): retry only transient blob errors`.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | In FILE-014 retain `BlobStoreProviderError(string message)` as a forwarding constructor. Add `BlobStoreProviderError(string message, bool isTransient = false)` and `IsTransient`, implementing the marker in FILE-015. Implement the same marker on `BlobStoreTimeoutError` with `IsTransient => true`. In FILE-016 replace the type list in `IsTransient(IResult)` with the nonempty all-errors predicate. Mark the deliberately injected fault transient in FILE-017 so the existing chaos and retry composition still works. | ✅ | 2026-11-25 |
| TASK-011 | In FILE-018 add public `SqlServerBlobErrors.ProviderError(Exception)` and `SqlServerBlobErrors.IsTransient(Exception)` with XML examples. Keep SQL Server recognition in this SQL Server-specific helper file inside the EF package, which already references SqlClient. Implement REQ-004, including all entries in `SqlException.Errors`. Construct provider errors with the existing full message and the classification flag. Map every current `new BlobStoreProviderError(exception.GetFullMessage())` in FILE-001 and FILE-002 through the helper. For the fixed-message exception catches in FILE-002 and FILE-003, retain the fixed message and classify the caught exception. Preserve typed conflict, partial update, indeterminate, and cancellation paths. | ✅ | 2026-11-25 |
| TASK-012 | After TASK-010 and TASK-011, add TEST-007 and TEST-008. Update retry test fixtures in FILE-024 and FILE-025 to set `isTransient: true` when the scenario requires a retryable failure. Run CHECK-001 and CHECK-002. In FILE-020 document the all-errors rule, timeout behavior, and EF classification. In FILE-021 add the unreleased retry fix. Commit after the tests pass. | ✅ | 2026-11-25 |

### Implementation Phase 3

- GOAL-003. Route EF blob time through the host clock. Begin after the Phase 2 commit. Commit as `fix(storage): use TimeProvider for EF blobs`.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | In FILE-001 retain the current six-parameter constructor and add a documented overload accepting a trailing `TimeProvider`. Initialize a private clock field with `timeProvider ?? TimeProvider.System`. In FILE-019 pass `sp.GetService<TimeProvider>()` in the EF provider factory used by `WithEntityFrameworkClient<TContext>`. Keep host registration optional. | ✅ | 2026-11-25 |
| TASK-014 | After TASK-013, replace every `DateTimeOffset.UtcNow` in FILE-001 through FILE-004 with the instance clock. Check expiry cutoffs, upload creation and completion, property and dimension timestamps, `TryAcquireLeaseAsync`, `AcquireNewLease`, and sweep `CompletedAt`. Pass the captured cutoff into static query helpers. Use the request cutoff for identifying sweep candidates. Verify `rg -n 'DateTimeOffset\.UtcNow' src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider*.cs` finds zero matches. | ✅ | 2026-11-25 |
| TASK-015 | After TASK-014, add TEST-009 using the existing transitive fake-clock package in FILE-032. Run CHECK-001 and CHECK-002. In FILE-020 document registration with `AddSingleton<TimeProvider>` and the system clock fallback. In FILE-021 add the unreleased EF clock change. Commit after the tests pass. | ✅ | 2026-11-25 |

### Implementation Phase 4

- GOAL-004. Start privacy-safe blob client activities across timeout, retry, and admission. Begin after the Phase 3 commit. Commit as `feat(storage): trace blob client operations`.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-016 | The starting revision has no `BlobTelemetry` declaration. Add it in FILE-028 with a documented public constant `ActivitySourceName = "BridgingIT.DevKit.Application.Storage.Blobs"` and a shared internal static `ActivitySource` named `Spans`. Add public `TracingBlobStoreClientBehavior` in FILE-029, deriving from `BlobStoreClientBehaviorBase` with constructor `(IBlobStoreClient inner, string storeName = null)`. Use the base class's fixed operation names and normalized store identifier. | ✅ | 2026-11-25 |
| TASK-017 | After TASK-016, in FILE-029 implement both `ExecuteAsync` overloads. Check `Spans.HasListeners()` before building the activity name and return the inner task directly if there are no listeners. Otherwise call a private async helper constrained to `IResult`. Start the Internal activity inside that helper to keep `Activity.Current` scoped to the operation. If sampling returns null, forward to the inner call without telemetry work. Add only the allowed tags, dispose the activity on completion, and return the unchanged Result. Set failures from the first runtime error type using `GetType().Name`. For an errorless failure set Error status with no description or `error.type`. Catch thrown exceptions only on the activity path, record the exception type, and use `throw;` to preserve the original exception. Do not serialize the operation context or add exception events. | ✅ | 2026-11-25 |
| TASK-018 | After TASK-016 and TASK-017, add `WithTracingBehavior()` immediately after `WithMetricsBehavior()` in FILE-013. Use the existing behavior factory pattern to construct `TracingBlobStoreClientBehavior(inner, name)`. Register it after metrics in the sample host in FILE-033. In the registration and high-volume samples in FILE-020 put the tracing call immediately after metrics and before timeout, retry, and admission. Document the source name, allowed tags, and opt-in registration. In FILE-021 add the unreleased tracing feature. | ✅ | 2026-11-25 |
| TASK-019 | After TASK-018, add TEST-010 through TEST-012 and run CHECK-001 through CHECK-004. Verify the documentation, changelog, and four commit subjects are in phase order before handing back. | ✅ | 2026-11-25 |

## 3. Alternatives

- **ALT-001**. Leave expiry to a more frequent sweep. Visibility would still depend on when a background job runs.
- **ALT-002**. Filter EF listing pages after materialization. Expired rows would consume page slots and distort continuation.
- **ALT-003**. Implement retries using a list of known permanent errors. Unrecognized failures would become retryable by default.
- **ALT-004**. Start a span inside the provider. It would cover an attempt and miss time waiting for retry or upload admission.

## 4. Dependencies

- **DEP-001**. Use the .NET 10 SDK selected by `global.json`. On a checkout where the system `dotnet` has no SDK, prefix the commands below with `mise exec dotnet@10.0.404 --`.
- **DEP-002**. Reuse the existing `Microsoft.Data.SqlClient` reference in `Infrastructure.EntityFramework.csproj` and the `Microsoft.Extensions.TimeProvider.Testing` version in `Directory.Packages.props`. Application unit tests already reference the latter.
- **DEP-003**. The existing provider tests link FILE-022 into Infrastructure unit and integration projects. Recorded Azure tests run without external storage. The SQL Server, PostgreSQL, SQLite, and Azurite integration classes have the shared `TestEnvironmentCollection` fixture, which starts Docker services even for its SQLite class.

## 5. Files

- **FILE-001**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs`
- **FILE-002**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Dimensions.cs`
- **FILE-003**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Filter.cs`
- **FILE-004**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Retention.cs`
- **FILE-005**. `src/Application.Storage/Blobs/InMemory/InMemoryBlobStoreProvider.cs`
- **FILE-006**. `src/Application.Storage/Blobs/InMemory/InMemoryBlobStoreContext.cs`
- **FILE-007**. `src/Application.Storage/Blobs/InMemory/InMemoryBlobStorageServiceCollectionExtensions.cs`
- **FILE-008**. `src/Infrastructure.FileSystem/Blobs/FileSystemBlobStoreProvider.cs`
- **FILE-009**. `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreProvider.cs`
- **FILE-010**. `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreBackend.cs`
- **FILE-011**. `tests/Infrastructure.IntegrationTests/Azure.Storage/Blobs/RecordingAzureBlobStoreBackend.cs`, `tests/Infrastructure.IntegrationTests/Azure.Storage/Blobs/AzureBlobStoreProviderTests.cs`, and `tests/Infrastructure.IntegrationTests/Azure.Storage/Blobs/AzureBlobPublicationTests.cs`.
- **FILE-012**. `src/Application.Storage/Blobs/Behaviors/CacheBlobStoreClientBehavior.cs`
- **FILE-013**. `src/Application.Storage/Blobs/Behaviors/BlobStoreClientBehaviorServiceCollectionExtensions.cs`
- **FILE-014**. `src/Application.Storage/Blobs/BlobStoreErrors.cs`
- **FILE-015**. `src/Common.Results/Errors/ITransientStorageError.cs` (existing marker, reference only)
- **FILE-016**. `src/Application.Storage/Blobs/Behaviors/RetryBlobStoreClientBehavior.cs`
- **FILE-017**. `src/Application.Storage/Blobs/Behaviors/ChaosBlobStoreClientBehavior.cs`
- **FILE-018**. `src/Infrastructure.EntityFramework/Storage/Blobs/SqlServerBlobErrors.cs` (new)
- **FILE-019**. `src/Infrastructure.EntityFramework/Storage/Blobs/ServiceCollectionExtensions.cs`
- **FILE-020**. `docs/features-storage-blobs.md`
- **FILE-021**. `CHANGELOG.md`
- **FILE-022**. `tests/Application.UnitTests/Storage/Blobs/BlobStoreProviderContractTests.cs`
- **FILE-023**. `tests/Application.UnitTests/Storage/Blobs/InMemoryBlobStoreProviderContractTests.cs`
- **FILE-024**. `tests/Application.UnitTests/Storage/Blobs/BlobStoreClientBehaviorTests.cs` and `tests/Application.UnitTests/Storage/Blobs/BlobDimensionsRetryTests.cs`
- **FILE-025**. `tests/Application.UnitTests/Storage/FileSystemRetrySafetyTests.cs`
- **FILE-026**. `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/SqlServerBlobErrorsTests.cs` (new)
- **FILE-027**. `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderRelationalReadTests.cs` and `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderContractTests.cs`
- **FILE-028**. `src/Application.Storage/Blobs/BlobTelemetry.cs` (new)
- **FILE-029**. `src/Application.Storage/Blobs/Behaviors/TracingBlobStoreClientBehavior.cs` (new)
- **FILE-030**. `tests/Application.UnitTests/Storage/Blobs/TracingBlobStoreClientBehaviorTests.cs` (new)
- **FILE-031**. `tests/Application.UnitTests/Storage/Blobs/BlobStoreCacheBehaviorTests.cs`
- **FILE-032**. `tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj` (existing test dependency graph)
- **FILE-033**. `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs`
- **FILE-034**. `tests/Infrastructure.UnitTests/FileSystem/FileSystemProviderTests.cs`, `tests/Infrastructure.UnitTests/FileSystem/FileSystemIntegrationBehaviorTests.cs`, and `tests/Infrastructure.UnitTests/FileSystem/FileSystemShareTests.cs`

## 6. Testing

Implementation verification on .NET 10.0.404 passed 399 Application, 440 Infrastructure, and 109 Presentation unit tests. The filesystem fixtures skipped 11 platform or share cases. All 291 provider integration cases passed, including the shared expiry contract. The WeatherFiesta build and `git diff --check` also passed.

Baseline on the starting revision with SDK 10.0.404. The filters in CHECK-001 and CHECK-002 passed 385 Application and 404 Infrastructure tests, respectively. Infrastructure skipped 11 platform or share-fixture cases. The presentation filter in CHECK-004 passed 109 tests.

- **TEST-001**. In FILE-022 add `ExpiredBlob_ReadsTreatItAsMissingAndCreateOnlyReplacesIt`. Create expired and live blobs in one container and an expired blob in a separate unique container. Use past and future times well away from the current instant. Assert download and metadata access and update report NotFound for the expired key, Exists is false, listing returns the live key, and the catalog omits the expired-only container. Use the current list query with `Take = 1` and an expired name that sorts first. Assert old ETag upload conflicts, `FailIfExists` creates a fresh blob, old metadata is absent, and the live blob's ETag and bytes are unchanged. Assert the new ETag differs. Test exact timestamp renewal in TEST-002 so service timestamp precision cannot make the shared fact flaky.
- **TEST-002**. In FILE-023 use `FakeTimeProvider` with a fixed instant and `Advance`. Assert an unexpired download succeeds one tick before expiry, then download, properties, property update, and Exists miss at equality. Express expiry with a nonzero offset representing the same instant. Assert the old entry remains in the raw context after reads. Advance one tick, replace it using `FailIfExists`, and assert a new `CreatedAt` and ETag. Cover DI clock resolution and explicit delete of a physically expired entry twice.
- **TEST-003**. In FILE-027 cover EF inline and large batch keys, including per-key NotFound, live batch reads, and the public helper's large-blob fallback. Exercise standard and FilterModel listings over live/expired rows in the existing SQLite fixture. Assert expired dimension update returns NotFound, the old row and dependents still exist after reads, and fresh replacement removes the old row, chunks, and dimensions. Verify an independent sweep removes expired rows that were read and can still use a distinct cutoff.
- **TEST-004**. In FILE-011 use the recording transport to test the Azure provider's ETag-conditioned expired replacement and no stream open on an expired read. Assert that filtering continues across a native page containing only expired items. Test the native preflight, ETag-conditioned delete, and create-only commit with the existing HTTP transport fixture. Use the inherited Azurite contract for service-backed behavior.
- **TEST-005**. In FILE-031 cache a live download, advance its fake clock to expiry, and verify that a second request reaches the provider and returns NotFound. Verify a fresh provider replacement can be read even when the cache contained the expired version.
- **TEST-006**. Verify the new FILE-022 fact runs in the application in-memory provider, EF InMemory unit provider, durable and buffered filesystem unit providers, EF SQLite/SQL Server/PostgreSQL integration providers, and Azure recording/Azurite integration providers.
- **TEST-007**. In FILE-024 set `Attempts = 3` and zero backoff. Assert one attempt for the default permanent provider error and three for an explicit transient error or timeout. Assert one attempt for mixed transient/permanent errors and a failure with zero errors. Include a non-generic delete result in the matrix. Set the dimension-publish and filesystem indeterminate tests' secondary provider fault transient to prove their existing retry guards still apply.
- **TEST-008**. In FILE-026 reflect over the installed SqlClient to construct `SqlError`, an error collection, and a `SqlException` without a connection. Use a theory for all 17 codes in REQ-004. Test a wrapped `1205`, a collection with an allowlisted secondary error, a wrapped timeout, and permanent examples `102`, `208`, and `547`. Assert both the classifier and the returned error flag. Test an ordinary unknown exception as permanent.
- **TEST-009**. In FILE-027 register a `FakeTimeProvider` through the production EF factory. At a fixed UTC instant assert exact upload `CreatedAt` and `LastModifiedAt`. Advance the clock, update properties and dimensions, and assert `LastModifiedAt` follows the clock while `CreatedAt` stays fixed. Advance to expiry, assert NotFound at equality, and check a sweep reports the clock instant in `CompletedAt`. Use the SQLite fixture for dimension support.
- **TEST-010**. In FILE-030 attach a scoped `ActivityListener` that samples the new source with `AllDataAndRecorded`. Exercise success, a failed result with two different error types, a thrown exception, and a failing non-generic delete. Assert name, Internal kind, operation/store tags, first-error selection, status, result passthrough, and exception identity. Use distinct sentinel values in keys, a continuation token, properties, error messages, and the thrown message. Assert the exact allowed tag keys and status descriptions and assert zero error events.
- **TEST-011**. In FILE-030 test without a listener and assert inner dispatch once, exact task passthrough, and no new activity. Isolate tests that attach the shared source listener in one xUnit class and dispose it per test.
- **TEST-012**. In FILE-030 register metrics, tracing, timeout, retry, and admission in that order. Assert an operation retried by the inner behavior produces one stopped outer activity and that the underlying attempts run within it. Keep an inner attempt pending at an await and assert the caller still sees its original `Activity.Current`.
- **TEST-013**. In FILE-034 update `Retention_SmallPersistedDiscoveryBudget_EventuallyWrapsAndPreservesExtendedExpiration`, `Retention_BusyKeyAndExpirationClearedAfterDiscovery_RechecksCurrentStateAndWraps`, and the SMB/NFS helper `RunRetentionAsync`. Seed each blob whose expiry will be changed with `now.AddHours(1)`. Run the involved sweeps with `now.AddHours(2)` so the sweep still discovers the original row while the foreground update sees a live blob. Keep the original assertions for the preserved row and the deleted keys.
- **CHECK-001**. Run `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --nologo --filter 'FullyQualifiedName~Blob|FullyQualifiedName~FileSystemRetrySafetyTests'`.
- **CHECK-002**. Run `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --nologo --filter 'FullyQualifiedName~Blob|FullyQualifiedName~.FileSystem.'`.
- **CHECK-003**. Check `docker info` and the shared fixture prerequisites, then run `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --nologo --filter 'FullyQualifiedName~BlobStoreProviderContractTests|FullyQualifiedName~BlobStoreProviderAzuriteContractTests|FullyQualifiedName~BlobStoreProviderTests|FullyQualifiedName~AzureBlobStoreBackendConcurrencyTests|FullyQualifiedName~AzureBlobPublicationTests'`. Record separately if any external fixture cannot start. Keep the linked contract cases enabled.
- **CHECK-004**. Run `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo --filter 'FullyQualifiedName~Blob'`, build the sample host with `dotnet build examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/WeatherFiesta.Presentation.Web.Server.csproj --nologo`, and run `git diff --check`. Run all top-level test and build commands sequentially.

## 7. Risks & Assumptions

- **RISK-001**. SQLite cannot translate the DateTimeOffset comparison used by the other EF providers. Match the existing sweep's UTC parameter representation and validate list, catalog, and exists against SQLite with the current schema.
- **RISK-002**. Two EF writers can observe an expired key. Preserve lease/version checks and a transaction for relational replacement. In memory and on the filesystem, make the expiry decision under the same lock or authority as publication.
- **RISK-003**. Azure has no transaction spanning delete and upload. A failed publication can leave an already-expired key physically absent. The ETag-conditioned delete and create-only replacement protect concurrent live data. Upload preflight adds a metadata read. The catalog reads blob metadata pages until each container has a live result or no pages remain. Document the read and list permissions needed by these operations.
- **RISK-004**. Activity listeners register process-wide. Use a source-name filter, dispose the listener, and keep source-listening cases serial within their test class.
- **ASSUMPTION-001**. Evaluate expiry when a read opens or obtains its result. An already-open stream can run to completion according to the provider's existing stream rules.
- **ASSUMPTION-002**. Live custom providers and databases continue to use the existing Result, capabilities, and DI contracts. The transient classification for non-SQL database errors is permanent except for a chained `TimeoutException`.

## 8. Related Specifications / Further Reading

- [Blob Storage reference](../docs/features-storage-blobs.md)
- [Blob Storage specification](../docs/specs/spec-application-storage-blobs.md)
- [ADR-0031. Single-query EF read for small blobs](../docs/adr/0031-single-query-ef-blob-read-for-small-blobs.md)
- [ADR-0032. Batched EF reads for many keys](../docs/adr/0032-batched-ef-blob-reads-for-many-keys.md)
- [Azure Blob Storage concurrency and conditional writes](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage)
- [Azure SDK conditional download options](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.models.blobopenreadoptions?view=azure-dotnet)
- [.NET distributed tracing instrumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
