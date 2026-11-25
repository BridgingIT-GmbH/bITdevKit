---
goal: Investigate correctness, recovery, and scalability of Blob and Document Storage
version: '1.0'
date_created: 2026-11-25
last_updated: 2026-11-25
owner: bITdevKit maintainers
status: 'Planned'
tags: [storage, blobs, documents, reliability, scalability, investigation]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Find defects that can lose or hide data, misreport a committed operation, stop unrelated work, or exhaust a shared resource. Start with EF SQL Server because the recent defects surfaced with a real engine, pooled contexts, and its default collation. Use PostgreSQL and SQLite to check the same contracts under different provider behavior. Extend the highest-risk cases to the supported native providers.

The reference revision is `60410ea38`. It includes the recent blob fixes and the EF document fixes for pooled dimension registration, paging, and retention. Use those regressions as controls. Record the exact revision under test for every new finding.

## 1. Requirements & Constraints

- **REQ-001**. Investigate both storage features through their normal client and provider entry points. Include publication, conditional writes, retries, batches, reads and paging, expiration, retention, cleanup, and resource use. Include HTTP streaming and result mapping where they change the guarantees seen by callers.
- **REQ-002**. Prioritize lost, hidden, duplicated, or stale data. Also prioritize incorrect outcomes after a commit, isolated errors that halt unrelated work, and resource use that grows beyond a configured or documented bound. Record style and design observations separately from the defect list.
- **REQ-003**. Give every finding a failing test or a reproducible measurement, the expected contract, and the observed result. For a race, record the interleaving, the number of attempts, and the number of failures. Mark source leads as hypotheses until they meet this standard.
- **REQ-004**. Inspect persisted records through a fresh context, connection, or process after failures. Verify content hashes, metadata, versions, and related rows where applicable. Use the same deterministic scenario to verify each fix.
- **SEC-001**. Use disposable databases, service namespaces, and filesystem roots owned by the run. Store redacted diagnostics under `artifacts/storage-audit/<run-id>/`. Keep credentials, production data, payloads, and opaque continuation tokens out of published evidence.
- **CON-001**. Use the SDK and packages pinned by the repository. Run top-level build and test commands sequentially in one worktree. Use existing providers, fixtures, and benchmark runners before extending them.
- **CON-002**. Keep persistence and provider error handling in Infrastructure. Keep retries, admission, and client contracts in Application. Keep HTTP translation and stream ownership at the Presentation boundary. Preserve public API, token, and schema compatibility unless a separately reviewed defect fix requires a change.
- **PAT-001**. Reuse the existing xUnit contracts and Testcontainers fixtures. Use barriers and interceptors to control failure timing. Reserve repeated stress runs for timing-sensitive issues that lack a practical deterministic trigger.

## 2. Implementation Steps

### Implementation Phase 1

- **GOAL-001**. Capture a reproducible baseline and test the strongest source leads.
- **Completion**. Manifest and baseline logs exist. Each lead has a reproducer or a documented result from the test, including unresolved contract discrepancies.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | Record the reference and tested SHAs, SDK, EF/provider versions, engine and image versions, database compatibility level and collation, storage profile, pool and retry settings, clock, seed, and test commands in `manifest.json`. Use TEST-001 for the baseline. | | |
| TASK-002 | In FILE-002, test EF blob `ListPageAsync` with a page size of one and case-equivalent names and containers on case-insensitive SQL Server. The native cursor compares only the last name, and the container predicate uses database text equality. Verify that every requested key is visited once and all returned keys belong to the requested container. Exercise native and FilterModel listings separately. | | |
| TASK-003 | In FILE-003, test EF document `ListPageAsync` and `FindPageAsync` with keys whose SQL collation order differs from ordinal order, in addition to equal-key ties. Compare the observed order with the paging specification and the current guide. Record the contract discrepancy and its caller impact for maintainer review before proposing any ordering change. | | |
| TASK-004 | In FILE-001 and FILE-003, count actual provider calls from `RetryDocumentStoreClientBehavior.IsRetryable` and EF write retry and bulk fallback paths. Test SQL error 547, 1205, cancellation, a failed batch with a confirmed prefix, and a lost commit acknowledgement. Verify that retry eligibility and reported outcomes match the operation contract. | | |

### Implementation Phase 2

- **GOAL-002**. Verify data integrity and isolation at transaction, request, and process boundaries.
- **Depends on**. TASK-001. The tasks below can run independently against separate fixtures.
- **Completion**. Each scenario has a recorded provider outcome and persisted-state assertions. Each reproduced defect identifies the affected entry points and configurations.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-005 | In FILE-002 and FILE-003, control transaction failures before save, during flush or save, during rollback, and after commit. Run pooled and scoped EF SQL Server cases with two independently built service providers. Race create-only, conditional replacement, delete, and dimension or property updates on the same key. Verify that payload, metadata, dimension snapshot and index rows, ETag, and expiry describe the same committed version. Use TEST-002. | | |
| TASK-006 | In FILE-001, FILE-002, FILE-003, and FILE-005, test partial failure in `UploadManyAsync`, `DownloadManyAsync`, `GetManyAsync`, `UpsertManyAsync`, `DeleteManyAsync`, copy, and move. Check duplicate and missing keys, confirmed prefixes, cancellation, replay of nonseekable input, and conditional source deletion after a destination write. Verify each result against physical state. Use TEST-003. | | |
| TASK-007 | In FILE-002, FILE-003, and FILE-004, run a writer and sweeper, then two sweepers, against the same store from independent service providers and separately from independent processes. Pause after candidate selection. Revive, replace, or delete a selected key before resuming. Include an exact expiry boundary, persistent contention on the oldest item, and restart between sweeps. Verify that live replacements survive, unrelated due items progress, and a follow-up sweep converges after contention ends. Use TEST-004. | | |
| TASK-008 | In FILE-001 through FILE-005, verify key, result, and continuation invariants across named clients, types, query shapes, collations, and applicable providers. Walk page sizes 1, 2, and the configured maximum using mixed-case, Unicode, punctuation, repeated sort values, and selective Dimension predicates. Separate static-corpus traversal from the documented behavior under concurrent changes or advancing clocks. Use TEST-005. | | |
| TASK-009 | In FILE-001, FILE-002, and FILE-005, cancel or abandon reads and writes before admission, during body transfer, and after response headers. Confirm stream, scope, transaction, connection, and permit ownership and release. Race a cache fill with replacement, deletion, or expiration and check the documented visibility guarantee. Interrupt or corrupt a transformed payload and verify the checksum and caller outcome. Use TEST-006. | | |

### Implementation Phase 3

- **GOAL-003**. Measure where load or provider behavior can turn ordinary traffic into a reliability failure.
- **Depends on**. TASK-001. Pair scaling cases with the same correctness assertions from Phase 2.
- **Completion**. For each selected profile, record the measured operating range, saturation behavior, and any qualification gaps. Attach evidence to every scaling defect.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-010 | Use FILE-006 to measure point reads, batch reads and writes, selective and broad key/Dimension pages, and retention with 10,000, 100,000, then 1,000,000 owners. Compare shallow and deep pages at fixed result size and use separate distributions for sparse and dense expiry and Dimensions. Capture actual plans or provider diagnostics, examined rows or logical reads, round trips, and bytes projected. Run TEST-007. | | |
| TASK-011 | Use FILE-006 to drive mixed reads, writes, and sweeps with worker counts 1, 4, and 16. Measure below saturation, near capacity, and during a controlled burst above capacity. Use small and chunk-boundary blob payloads, a separate streamed large-blob case, and documents near the configured serialized size limit. Hold readers open across replacements and include skew toward one key and one tenant. Track throughput, tail latency, admission, retry, conflicts, failure rates, allocations, process memory, connection use, and post-drain recovery. Run TEST-008. | | |
| TASK-012 | Port the highest-impact applicable cases to the FILE-004 provider profiles. For Azure Blob and Table, measure SDK request amplification and page fill when almost all entries are expired. Start with `AzureBlobStoreProvider.ListContainersAsync`, which walks metadata pages until each container yields a live blob or runs out of pages. For Cosmos cover throttling, continuation, and TTL. For filesystem and LiteDB cover process restart, durability, and cleanup using existing probes. Run TEST-009 and record local, emulator, and real deployment evidence separately. | | |

### Implementation Phase 4

- **GOAL-004**. Deliver a ranked defect list and repeatable regression coverage.
- **Depends on**. Phases 1–3. Publish confirmed integrity findings as soon as evidence is available.
- **Completion**. Every confirmed high-impact defect has an individual reproducer, fix or explicit disposition, and a verification command. A profile qualifies once its high-impact defects are resolved and its required checks pass. Record unavailable profiles as qualification gaps.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-013 | Produce `findings.md` keyed by finding ID. Include severity, feature, affected providers/configurations, minimal command and fixture, expected and observed state, frequency, causal code path, and disposition. Rank data loss, cross-scope results, corrupted state, and false success first. Rank bounded workloads that can stall or exhaust resources next. | | |
| TASK-014 | For each confirmed defect, isolate the smallest fix, keep the failing regression with it, and rerun the sibling feature's contract when it shares the affected path. Update the guide and changelog if observable behavior changes. Run TEST-010 before calling a fix verified. | | |
| TASK-015 | Keep deterministic correctness checks in regular validation. Put real-service, multi-host, fault-soak, and high-cardinality runs in opt-in or scheduled jobs with explicit fixture ownership. Record thresholds and sample sizes alongside the results. Re-run known regressions and the worst workload before reporting the tested profiles as qualified. | | |

## 3. Alternatives

- **ALT-001**. Use static review to nominate cases. Verify each case against the contract and its observable runtime outcome.
- **ALT-002**. Start with SQL Server, PostgreSQL, and SQLite. Extend the matrix when a native provider uses a different publication, paging, or recovery mechanism.
- **ALT-003**. Use dashboard load tests for a quick provider check, benchmark runners for repeatable measurements, and typed client tests for transforms, admission, and retry.

## 4. Dependencies

- **DEP-001**. Repository SDK from `global.json`, Docker and Testcontainers for disposable SQL Server and PostgreSQL, and SQLite temporary database files. Use SQL Server's case-insensitive profile for the primary regressions and a bytewise profile as a control. Include both pooled and scoped contexts for lifecycle tests. Record the SQL Server MARS setting and exercise provider execution retries as a distinct configuration.
- **DEP-002**. Existing in-memory contracts and Azure emulators for local coverage. Use isolated real Azure service accounts and disposable SMB/NFS profiles only when already provisioned and authorized. Record any unavailable profile as a qualification gap.
- **DEP-003**. Existing BenchmarkDotNet project, Dimensions qualification runner, filesystem process/load probes, and provider-specific diagnostics. Keep startup, population, and cleanup costs separate from steady-state measurements.

## 5. Files

- **FILE-001**. Under `src/Application.Storage/`, inspect `Blobs/BlobStoreClient.cs`, `Blobs/Behaviors/BlobUploadAdmissionCoordinator.cs`, `Documents/DocumentStoreClient.cs`, `Documents/Extensions/DocumentStoreClientExtensions.cs`, and `StorageFilterQuery.cs`. Inspect each feature's `Behaviors/` folder for cache, timeout, retry, and transform ordering.
- **FILE-002**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs`, its `Dimensions`, `Filter`, and `Retention` partials, and `Dimensions/BlobDimensionNameResolver.cs` in the same provider directory.
- **FILE-003**. `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs`, its `Dimensions` and `Filter` partials, `Dimensions/DocumentDimensionNameResolver.cs` in the same provider directory, and `src/Infrastructure.EntityFramework/Storage/EntityFrameworkStorageFilterQuery.cs`.
- **FILE-004**. `src/Application.Storage/Blobs/Retention/BlobRetentionBackgroundService.cs`, `src/Application.Storage/Documents/Retention/DocumentRetentionBackgroundService.cs`, `src/Infrastructure.FileSystem/Internal/FileSystemStore.cs`, the filesystem and in-memory blob/document providers, `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreProvider.cs`, the Azure Blob and Table document providers in `src/Infrastructure.Azure.Storage/Documents/`, `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProvider.cs`, and `src/Infrastructure.LiteDB/Storage/Documents/LiteDbDocumentStoreProvider.cs`.
- **FILE-005**. Blob and document read and maintenance endpoints under `src/Presentation.Web.Storage/`. Start from `tests/Presentation.UnitTests/Web/Storage/` and verify the HTTP-visible contract against provider results.
- **FILE-006**. `benchmarks/Application.Benchmarks/Program.cs`, `StorageDimensionsQualification.cs`, `StorageDimensionsQualification.OfferedLoad.cs`, `BlobStorageHighVolumeUploadBenchmarks.cs`, `BlobStorageEfSqlServerBatchReadBenchmarks.cs`, the EF Document SQL Server benchmarks, and `tests/Infrastructure.FileSystem.Probes/`.
- **FILE-007**. Contract extensions belong in `tests/Application.UnitTests/Storage/`, `tests/Shared/Storage/DocumentStoreConcurrencyContractTests.cs`, and the established provider fixtures under `tests/Infrastructure.IntegrationTests/` or `tests/Infrastructure.UnitTests/EntityFramework/Storage/`.

## 6. Testing

- **TEST-001**. Run these baseline suites from the repository root. Preserve the command output and skipped-test reasons. Start from the pooled dimension proofs, retention fault tests, upload admission stress test, and case-equivalent document paging regression.

  ```bash
  dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter 'FullyQualifiedName~Storage'
  dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter 'FullyQualifiedName~Blob|FullyQualifiedName~Document|FullyQualifiedName~StorageDimensions'
  dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~EntityFrameworkSqlServerBlobStoreProviderContractTests|FullyQualifiedName~EntityFrameworkSqlServerDocumentStoreProviderTests|FullyQualifiedName~StorageDimensionsMutationProofTests|FullyQualifiedName~StorageFilterQueryProofTests'
  ```

- **TEST-002**. Extend the tests in `StorageDimensionsMutationBarrierProofTests.cs` and `StorageDimensionsCommitCancellationProofTests.cs`. For each injected boundary, assert a complete old or new version and the operation's reported outcome. An indeterminate acknowledgement requires read-back and replay checks.
- **TEST-003**. Extend the existing blob batch tests and the tests in `StorageDimensionsBatchFailureProofTests.cs`. Verify input-order results where promised, confirmed and failed keys, and the state of unattempted items. Check cancellation and replay at the first, middle, and last item.
- **TEST-004**. Extend `EntityFrameworkDocumentRetentionTests` and the SQL Server provider contracts with a real contention case. Keep injected SQL errors for deterministic branch coverage. Use a fresh reader to verify the revived key and the exact set of deletions. Demonstrate independent-process evidence for any claim about coordination across processes.
- **TEST-005**. Reuse the regressions in `StorageDocumentSqlServerRegressionTests.cs`, `StorageFilterQueryProofTests`, and the shared blob provider contract. Compare all pages with a seeded oracle and record exact membership, order where specified, and token rejection behavior. Prove metadata queries avoid reading payload columns where the provider promises a key-only or metadata projection.
- **TEST-006**. Extend `BlobUploadAdmissionCoordinatorTests`, cache and transform tests, and HTTP storage tests. Measure counters during execution and after drain. Assert that active and queued work stay within configured limits, permits are returned, and the next request succeeds after cancellation and disposal. Compare cached and uncached reads with the committed version and verify that corrupt transfers produce the documented failure.
- **TEST-007**. Start with the existing Dimensions qualification runner using `--storage-dimensions-qualification --engine sqlserver --feature combined --owners 10000 --seed 17043`. Set `--output` to the run's artifact directory. Repeat at larger cardinalities and on PostgreSQL and SQLite where supported. Use the existing random-read and item-write profiles for their respective workloads.
- **TEST-008**. Use the existing offered-load scheduler where available. Record scheduler delay, time waiting for admission, service time, and full transfer time. Compare identical seed, configuration, offered rate, and workload on the reference and candidate revisions. Use three paired repetitions for a reported regression and retain the raw sample counts. Flag a repeatable increase above 25% in p95 at the same offered rate, or throughput loss above 20% at comparable error rates, for investigation. These are triage thresholds for this audit. Use actual resource bounds, request or query work growth, and invariants to classify a defect.
- **TEST-009**. Reuse Azure publication and concurrency tests, filesystem local and process probes, and the LiteDB document paging and concurrency tests. Capture request counts, durable-state or recovery checks, and cleanup outcomes. Apply the real provider profiles only to disposable resources.
- **TEST-010**. Show the regression failing on the unfixed revision and passing on the candidate. Run the closest provider contract and relevant sibling contract. Re-run the affected load shape for resource or throughput fixes. Record any skipped, deferred, or unqualified scenario in the result.

## 7. Risks & Assumptions

- **RISK-001**. Contract expectations can differ by provider or between the guides and the implemented specifications. Record the exact discrepancy and whether callers can observe it before deciding on compatibility changes.
- **RISK-002**. Stable paging assertions require a fixed corpus and the documented visibility model. Use separate mutation and clock-advance cases to test expiry, concurrent changes, and continuation behavior.
- **RISK-003**. Seed size, client scheduling, warm caches, unrelated test fixtures, and diagnostic overhead can dominate measurements. Record those conditions and sample sizes. Repeat on dedicated capacity before making release-level performance claims.
- **RISK-004**. Emulator and single-host results cover specific code paths. Real cloud throttling, network filesystem reconnects, and power-loss behavior require their own qualification evidence.
- **ASSUMPTION-001**. The initial workload limits and measurement thresholds are investigation parameters. Record deployment-specific throughput, latency, and capacity targets separately when a deployment profile supplies them.

## 8. Related Specifications / Further Reading

- [Blob Storage guide](../features-storage-blobs.md)
- [Document Storage guide](../features-storage-documents.md)
- [Document Storage paging specification](../specs/spec-application-storage-documents-paging.md)
- [High-volume blob uploads specification](../specs/spec-application-storage-blobs-high-volume-uploads.md)
- [ADR 0030 on bounded blob upload admission and EF chunk flushing](../adr/0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md)
- [ADR 0035 on batched EF document reads and bulk writes](../adr/0035-batched-ef-document-reads-and-bulk-writes.md)
- [ADR 0036 on EF document paging and retention indexes](../adr/0036-keyset-and-filtered-indexes-for-ef-documents.md)
- [Filesystem storage provider qualification](../storage-filesystem-providers.md)
- [Storage dashboard throughput tests](../storage-load-testing.md)
- [Existing Dimensions qualification plan](../../plan/pln-feature-storage-dimensions-1.md)
