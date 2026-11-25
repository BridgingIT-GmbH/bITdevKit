---
goal: Fix six confirmed EF Blob Storage defects
version: '1.0'
date_created: 2026-11-25
last_updated: 2026-11-25
owner: bITdevKit maintainers
status: 'Planned'
tags: [storage, blobs, entity-framework, reliability, bug]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

The audit of `60410ea38` confirmed six EF Blob Storage defects. Case-insensitive SQL Server allowed cross-container deletion and omitted distinct blob names during paging. SQL Server, PostgreSQL, and SQLite returned mixed-version downloads, allowed replay after an unacknowledged commit, and repeatedly selected the same blocked retention candidate. PostgreSQL and SQLite also reported native lock contention as permanent failure.

## 1. Requirements & Constraints

- **REQ-001**. EF-01 requires exact container membership for every native listing page and downstream prefix deletion.
- **REQ-002**. EF-05 requires a static native traversal to return every matching key exactly once, including names that compare equal under the database collation.
- **REQ-003**. EF-02 requires each read to deliver bytes from the version identified by the download metadata. If that version becomes unavailable, the read must fail before returning replacement bytes.
- **REQ-004**. EF-04 requires an indeterminate result when an ordinary upload's commit acknowledgement fails. The retry decorator must stop replay.
- **REQ-005**. EF-06 requires each unchanged due candidate to consume at most one attempt per sweep. A subsequent sweep must be able to retry a skipped blob.
- **REQ-006**. EF-08 requires recoverable native lock failures to be marked transient. Row-level contention must allow the sweep to continue when the engine permits another write.
- **CON-001**. Test the SQL Server, PostgreSQL, and SQLite EF providers. Preserve the public operation signatures, schema, stored hash algorithms, and existing continuation envelope.
- **CON-002**. Use the SDK and packages pinned by the repo. Run top-level test commands sequentially in each worktree. Use disposable database fixtures and fresh reads after injected failures.
- **GUD-001**. Make one commit per defect, with its regression and applicable guide and changelog updates. Add XML documentation with an `<example>` block to new public and protected members.
- **PAT-001**. Keep database identity, row consistency, and native exception classification in Infrastructure. Reuse the existing Blob Storage error and retry contracts.

## 2. Implementation Steps

### Implementation Phase 1

- **GOAL-001**. Fix native listing so prefix deletion stays in scope and static traversal returns every key once.
- **Completion**. TEST-001 and TEST-002 pass on the case-insensitive scoped and pooled SQL Server profiles and the binary SQL Server, PostgreSQL, and SQLite controls.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | EF-01. In FILE-001 `ListPageAsync(BlobQuery, ...)`, hash the normalized container with `HashHelper.ComputeSha256`. Require matching `ContainerHash` and container text before prefix and Dimension predicates. Follow the existing predicate in FILE-003. Add TEST-001 through the public `DeleteByPrefixAsync` path in FILE-007. | | |
| TASK-002 | EF-05. Depends on TASK-001. In FILE-001 `ListPageAsync`, order by `(Name, NameHash)` for both Dimension candidate selection and final page projection. Seek using `Name > anchorName` or `Name == anchorName` and `NameHash > anchorHash`, using the provider-translatable `string.Compare` pattern in `EntityFrameworkStorageFilterQuery.Seek`. Derive `anchorHash` from the authenticated token's existing `Name` using `HashHelper.ComputeSha256`. Keep `Take + 1` server-side. Add TEST-002 and document how to restart a traversal begun before the fix. | | |

### Implementation Phase 2

- **GOAL-002**. Keep download content consistent with its ETag and preserve uncertainty at the upload commit boundary.
- **Depends on**. Phase 1 precedes this phase to serialize edits to FILE-001.
- **Completion**. TEST-003 and TEST-004 pass across the relational matrix.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-003 | EF-02. In FILE-001 `DownloadAsync`, give `BlobChunkReadStream` the ETag from the metadata read. In `EnsureChunkAsync`, use one statement matching the chunk index, owner ID, and owner ETag via `StorageBlobChunk.Blob`. For SQL Server only, execute each owner/chunk statement in a short `RepeatableRead` transaction through `Database.CreateExecutionStrategy().ExecuteAsync`. Apply that read boundary to the initial inline projection too. Close the transaction before returning bytes. Use a single statement for each read on PostgreSQL and SQLite. When an expected chunk is missing or the owner version differs, throw the existing `InvalidDataException` before copying any bytes from that chunk. Preserve buffered reads, cancellation propagation, and stream disposal. Add TEST-003. | | |
| TASK-004 | EF-04. Depends on TASK-003 for FILE-001 edits. In `UploadAsync`, route every relational commit through the helper in FILE-004. Rename the helper `CommitBlobMutationAsync` and update its Dimension caller. Preserve cancellation and publish Dimension cache entries only after confirmed commit. A non-cancellation commit acknowledgement failure returns `BlobStoreIndeterminateOperationError` with the transaction ID. Verify FILE-008 treats that result as terminal and retain the pre-commit transient path. Add TEST-004. | | |

### Implementation Phase 3

- **GOAL-003**. Keep retention work bounded and distinguish temporary lock contention from permanent errors.
- **Depends on**. Phase 2 precedes this phase to serialize edits to FILE-001.
- **Completion**. TEST-005 and TEST-006 pass. The document plan can use the shared classifier after TASK-006.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-005 | EF-06. In FILE-001 `SweepExpiredAsync` and FILE-002 `ReadExpiredCandidatesAsync`, keep a sweep-local cursor `(ExpiresAt, Id)`. Capture the last candidate's original values before processing its batch. Seek with `ExpiresAt > cursor.ExpiresAt` or equal expiry and `Id` greater than the cursor ID. Use `Guid.CompareTo` against the mapped ID on SQL Server and PostgreSQL. Use model-mapped parameters in the existing SQLite raw query. Apply the cutoff and seek before order and `Take`. Preserve conditional deletion and the existing rollback and detach paths. Reset the cursor on each sweep call. Add TEST-005, including tied expiry values. | | |
| TASK-006 | EF-08. Depends on TASK-005. Add FILE-006 `RelationalStorageErrors.IsTransient`. Walk the inner exception chain, retain FILE-005's SQL Server and timeout behavior, and recognize PostgreSQL SQLSTATE `55P03`, `40P01`, and `40001`, plus SQLite primary result codes `5` and `6` for busy and locked. Read PostgreSQL `DbException.SqlState` and use the optional-driver exception property pattern in `BlobDimensionNameResolver` to read `SqliteErrorCode` without a package dependency. Route `SqlServerBlobErrors.IsTransient` through the shared method while keeping its public signature. An in-transaction transient candidate failure rolls back and skips that candidate. A transient failure to begin a transaction returns a transient provider error for the sweep. Keep cancellation and permanent failures on their existing paths. Add TEST-006. | | |

## 3. Alternatives

- **ALT-001**. Forcing database-wide binary collation changes existing database behavior and requires deployment work. Exact hashes and a name tie-breaker work with the existing schema.
- **ALT-002**. Holding a transaction for the full download pins a connection and lock state for the client's entire read. Scope SQL Server consistency to one statement and transaction per chunk.
- **ALT-003**. Accumulating excluded IDs would increase query parameters with the number of contention failures. Advance the existing ordered retention traversal with two scalar cursor values.

## 4. Dependencies

- **DEP-001**. Use relational fixtures for SQL Server 2022 at compatibility level 160, PostgreSQL 16, and SQLite. Exercise SQL Server `SQL_Latin1_General_CP1_CI_AS` with scoped and pooled registrations and `Latin1_General_100_BIN2` with scoped registration.
- **DEP-002**. Use the existing continuation serializer, key hash, error types, and tolerant rollback behavior already on the audited branch.

## 5. Files

- **FILE-001**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs`
- **FILE-002**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Retention.cs`
- **FILE-003**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Filter.cs` and `src/Infrastructure.EntityFramework/Storage/EntityFrameworkStorageFilterQuery.cs` for reference and controls.
- **FILE-004**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Dimensions.cs`
- **FILE-005**. `src/Infrastructure.EntityFramework/Storage/Blobs/SqlServerBlobErrors.cs` and `src/Infrastructure.EntityFramework/Storage/SqlServerStorageErrors.cs`
- **FILE-006**. `src/Infrastructure.EntityFramework/Storage/RelationalStorageErrors.cs`, new shared classifier.
- **FILE-007**. `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobReliabilityTests.cs`, new regression fixture using the adjacent provider contract fixtures as the setup reference.
- **FILE-008**. `src/Application.Storage/Blobs/Behaviors/RetryBlobStoreClientBehavior.cs` for verification of replay eligibility.
- **FILE-009**. `docs/features-storage-blobs.md` and `CHANGELOG.md`

## 6. Testing

- **TEST-001**. Upload `cleanup/tmp/one` and `Cleanup/tmp/two`. Dry run against container `cleanup` and prefix `tmp/` reports one candidate. Real deletion reports one removal. Fresh reads establish that the intended key is gone and the adjacent key is present. Include both native and FilterModel listings in the same fixture.
- **TEST-002**. Store `A`, `a`, and `B` in `scope`. Traverse with `Take = 1` using plain, prefix, and native Dimension paths. Assert each expected key occurs once and the last page has no token. Construct an old name-only token through the existing serializer and verify decoding and the new seek boundary. Use a static corpus for all checks.
- **TEST-003**. Set chunk size to four and upload a 12-byte blob. Open a download, read exactly four bytes, overwrite the key with three different chunks, and attempt the next read. Assert the old ETag, old first chunk, and explicit failure without replacement bytes. Read the new version through a fresh stream. Cover a complete uncontended stream, delete between chunks, cancellation, disposal, and the inline read path. On SQL Server exercise both default locking read committed and an execution strategy with retries enabled.
- **TEST-004**. Throw a `TimeoutException` from `TransactionCommittedAsync` after an ordinary upload commits. Assert an indeterminate result, a fresh read of the uploaded version, and one attempt with a two-attempt retry decorator. Separately inject a pre-commit transient failure and verify legacy retry eligibility and no persisted row for the failed attempt.
- **TEST-005**. Set batch size to one and maximum batches to three. Inject a persistent timeout at save for the oldest due key only. Assert one skip, progress on the unrelated due key, and the victim still present. Remove the fault and assert a follow-up deletes the victim exactly once. Include multiple candidates with the same expiry to exercise the ID tie-breaker.
- **TEST-006**. Use independent connections to hold a PostgreSQL row lock with a 500 ms lock timeout and a SQLite WAL writer lock with a one-second busy timeout. PostgreSQL skips the locked row and deletes the unrelated due row. SQLite reports a transient sweep failure. After release, a follow-up removes the deferred rows. Include a permanent constraint error and cancellation controls.
- **TEST-007**. Run `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter FullyQualifiedName~EntityFrameworkBlobReliabilityTests`. Then run the three relational blob provider contracts and `StorageFilterQueryProofTests`. Compare the 25-item key page logical reads on the 10,000, 100,000, and 1,000,000 row SQL Server fixtures with the audit when validating TASK-002. Record the query plans and page completeness.

## 7. Risks & Assumptions

- **RISK-001**. An older token identifies an anchor but cannot recover a key that an earlier page already skipped. Document that callers should restart a traversal to check full inventory after upgrading.
- **RISK-002**. SQL Server chunk reads need a consistency boundary under locking read committed. Hold read locks only while fetching a chunk and allow the configured strategy to replay the read before any bytes are returned.
- **RISK-003**. Retention may defer a newly due row behind the current cursor until the next sweep. Keep each sweep's cutoff fixed and verify recovery on the next invocation.
- **ASSUMPTION-001**. A SQLite writer lock can block all writes. The SQLite lock test expects an explicit transient result and recovery after release.

## 8. Related Specifications / Further Reading

- [Storage reliability audit plan](pln-process-storage-reliability-audit-1.md)
- [EF Document Storage defect plan](pln-refactor-ef-document-reliability-1.md)
- [Blob Storage guide](../features-storage-blobs.md)
