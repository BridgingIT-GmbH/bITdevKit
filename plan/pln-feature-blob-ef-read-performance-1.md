---
goal: Reduce Entity Framework Blob Storage read latency, SQL round trips and allocations for blobs smaller than 4 MB on SQL Server
version: 1.0
date_created: 2026-10-30
last_updated: 2026-10-30
owner: bITdevKit maintainers
status: 'In progress'
tags: [feature, storage, blobs, entity-framework, sql-server, performance, read-path]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

**Progress (2026-10-30):** Phases 1 to 3 are implemented (commits `2056b92dc`, `1941f83dc`) and verified on SQL Server, PostgreSQL and SQLite by the integration contract tests. Documentation, ADR-0031 and the changelog entry (TASK-019 to TASK-021) are done. Deviations: the compiled query is a per-instance field, not `static readonly` (TASK-014), because EF binds a compiled query to the first provider and a static field broke mixed-provider processes; the inline stream is a publicly visible `MemoryStream` instead of a new `BlobBufferedReadStream` (TASK-009, TASK-015). TASK-016 (hash cost) is not done. Measured after Phase 3 on SQL Server: 10 KB read 3.28 to 1.40 ms, 1 query instead of 3, allocation 255 to 185 KB; 100 KB read 3.67 to 1.76 ms, 708 to 458 KB. The results file (TASK-018), the benchmark re-run with the per-instance field, and a PostgreSQL benchmark are open. Phase 5 (option C, `DownloadBytesManyAsync`) is implemented and measured: 100 keys of 10 KB went from 20.1 to 9.4 ms with 67% less allocation (ADR-0032).

This plan improves the read path of `EntityFrameworkBlobStoreProvider<TContext>.DownloadAsync` for blobs up to the default `ChunkSize` of 4 MB, measured on SQL Server. The baseline was captured with the benchmarks in `benchmarks/Application.Benchmarks` (commit `ad8a599c3`). Workload: many blobs under 100 KB, keys `{32-char id}/{yyyy-MM-dd}`, direct key lookup (no listing), compressed payloads. Scope is reads only. No schema change, no public API change in Phases 1 to 4. After implementation the same benchmarks are re-run and compared against the baseline in section 1.

## 1. Requirements & Constraints

### Baseline (measured, local Docker SQL Server 2022, Ryzen 9 9900X, .NET 10, BenchmarkDotNet 0.15.8)

| Payload | DevKit read (ms) | Raw SQL floor (ms) | DevKit alloc / read | Raw SQL alloc / read | SQL queries / read |
|---------|------------------|--------------------|---------------------|----------------------|--------------------|
| 10 KB | 3.28 | 0.88 | 255 KB | 33 KB | 3 |
| 100 KB | 3.67 | 1.17 | 708 KB | 304 KB | 3 |
| 1 MB | 8.81 | 5.73 | 6.3 MB | 3.1 MB | 3 |
| 3 MB | 24.2 | 19.7 | 20.9 MB | 9.4 MB | 3 |

Request of 100 blobs, `DevKit_ReadRequest` (parallel, mean per request): 10 KB P=4/16/32 = 100.3 / 41.3 / 42.5 ms; 100 KB = 121.9 / 67.1 / 65.0 ms; 1 MB = 475 / 429 / 444 ms; 3 MB = 1289 / 1614 / 2157 ms. Raw SQL floor at 10 KB P=16 = 11.2 ms.

### Root causes (from code reading, confirmed by `sqlQueriesPerRead=3`)

1. `DownloadAsync` runs query 1 (blob row by `ContainerHash` + `NameHash`).
2. `BlobChunkReadStream.EnsureChunkAsync` runs query 2 (chunk `Index = 0`).
3. After chunk 0 is consumed, `EnsureChunkAsync` runs query 3 (chunk `Index = 1`), which returns no row, to detect end of stream.
4. The stream holds a scoped `DbContext` and an open connection until disposed, even when the whole blob is already in memory.
5. `DownloadBytesAsync` copies into an unsized `MemoryStream`, growing by doubling, although `BlobInfo.Length` is known.
6. `chunk.Content.Take(chunk.Length).ToArray()` is used when `Length != Content.Length`.

### Requirements

- **REQ-001**: A read of a blob with `Length <= BlobStoreOptions.ChunkSize` must issue exactly 1 SQL query on SQL Server (`sqlQueriesPerRead = 1`).
- **REQ-002**: A read of a blob with `Length == 0` must issue exactly 1 SQL query and return an empty stream.
- **REQ-003**: A read of a blob with `Length > ChunkSize` must issue 1 query for the blob row plus 1 query per chunk, with no extra end-of-stream query.
- **REQ-004**: End of stream must be determined by `bytesRead == BlobInfo.Length`, not by a failed chunk lookup. It must not depend on the current `ChunkSize` option, because blobs may have been written with a different value.
- **REQ-005**: For fully buffered small blobs, release the scoped `DbContext` and connection before `DownloadAsync` returns.
- **REQ-006**: `BlobDownload.Info` and `BlobDownload.Content` semantics must not change: forward-only, non-seekable, caller disposes the stream, `Length` equals `BlobInfo.Length`.
- **REQ-007**: Return `BlobStoreNotFoundError` for a missing key and `BlobStoreProviderError` for provider exceptions, as today.
- **REQ-008**: If a chunk row is missing or shorter than expected (`bytesRead < Length` and no more chunks), fail the read with `BlobStoreProviderError` or throw `InvalidDataException` from the stream. Silent truncation is not allowed.
- **REQ-009**: `DownloadBytesAsync` must pre-size its buffer from `BlobInfo.Length` when it is greater than 0 and not larger than `int.MaxValue`.
- **REQ-010**: Targets after Phases 1 to 4 (same hardware, same benchmark parameters):
  - 10 KB `DevKit_Read` <= 1.3 ms (baseline 3.28), `sqlQueriesPerRead = 1`.
  - 100 KB `DevKit_Read` <= 1.7 ms (baseline 3.67).
  - 10 KB allocation per read <= 120 KB (baseline 255 KB).
  - 10 KB `DevKit_ReadRequest` P=16 <= 16 ms (baseline 41.3 ms).
  - 1 MB and 3 MB `DevKit_Read` must not regress by more than 3 percent against baseline.
- **CON-001**: No schema change, no migration, no new column on `__Storage_Blobs` or `__Storage_BlobChunks`.
- **CON-002**: Do not change `IBlobStoreProvider` or `IBlobStoreClient` in Phases 1 to 4.
- **CON-003**: All code must work on SQL Server and SQLite (`IsSqlite` branches exist). InMemory must keep working for unit tests.
- **CON-004**: Use `TreatWarningsAsErrors`, file-scoped namespaces, `this.` qualification, no underscore fields, and XML docs with an example on every new public member.
- **CON-005**: Write and delete paths are out of scope.
- **GUD-001**: Reuse the existing benchmarks unchanged for the after-run, so before and after results are comparable. Only add benchmark methods, never alter existing ones.
- **GUD-002**: Run `dotnet build` and `dotnet test` sequentially, never in parallel.
- **PAT-001**: Prefer a single projected query over multiple queries; project only needed columns with `AsNoTracking()`.
- **PAT-002**: Use `Result<T>` for failures at the provider boundary.

## 2. Implementation Steps

### Implementation Phase 1: Remove the end-of-stream query

- GOAL-001: Drop query 3 without changing observable behavior. Expected queries per read: 3 to 2.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | In `EntityFrameworkBlobStoreProvider.cs`, `BlobChunkReadStream.EnsureChunkAsync`: set `this.completed = true` and return false when `this.Position >= length` before issuing any chunk query. | | |
| TASK-002 | In the same method, when the chunk query returns no row and `this.Position < length`, throw `InvalidDataException($"Blob chunk {this.nextIndex} of blob {blobId} is missing.")`. | | |
| TASK-003 | Replace `chunk.Content.Take(chunk.Length).ToArray()` with `chunk.Content.AsSpan(0, chunk.Length).ToArray()`. | | |
| TASK-004 | Add unit tests in `EntityFrameworkBlobStoreProviderUploadDownloadTests.cs`: zero-length blob, exactly one chunk, exactly two chunks (`ChunkSize` = 4 bytes, 8-byte payload), truncated chunk row (delete chunk 1 manually, expect `InvalidDataException`), blob written with `ChunkSize` A and read with `ChunkSize` B. | | |
| TASK-005 | Run `dotnet test tests/Infrastructure.UnitTests --filter "FullyQualifiedName~EntityFramework.Storage.Blobs"` and the SQL Server integration contract tests. | | |
| TASK-006 | Re-run `BlobStorageEfSqlServerSequentialReadBenchmarks` and record results as "after phase 1" in `plan/pln-feature-blob-ef-read-performance-1-results.md`. | | |

### Implementation Phase 2: Single-query read for small blobs

- GOAL-002: Return blob metadata and chunk 0 content in one SQL round trip when `Length <= ChunkSize`. Expected queries per read: 2 to 1.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Add a private method `QueryBlobWithInlineContent(TContext dbContext, KeyIdentity key, long inlineLimit)` in `EntityFrameworkBlobStoreProvider.cs`. It left-joins `StorageBlobs` to `StorageBlobChunks` on `BlobId == Id && Index == 0` (`join ... into ... DefaultIfEmpty`) and projects `{ Blob, Content = blob.Length <= inlineLimit ? chunk.Content : null, ChunkLength = chunk.Length }`. | | |
| TASK-008 | Verify the query translates on SQL Server and SQLite. Do not use a correlated `FirstOrDefault` in `Select`; it fails on SQLite with "requires SQL APPLY". Capture the generated SQL via `ToQueryString()` in a test. | | |
| TASK-009 | In `DownloadAsync`, call the new query. When `Content` is non-null, or `Length == 0`, build a fully buffered stream (`BlobBufferedReadStream`, a new private sealed class wrapping the byte array) and dispose the lease before returning (REQ-005). Otherwise return `BlobChunkReadStream` seeded with no chunk, as today. | | |
| TASK-010 | Set `inlineLimit` to `this.options.ChunkSize`. A blob larger than the limit never loads content in the first query, so large downloads still stream chunk by chunk. | | |
| TASK-011 | Keep `GetPropertiesAsync` and `ExistsAsync` on `QueryExactBlob` (no content). | | |
| TASK-012 | Add tests: small blob returns equal bytes, query count equals 1 (use a `DbCommandInterceptor` counter on SQLite), blob above `ChunkSize` streams with 1 + N queries, disposing the returned stream twice is safe, lease released before first read (assert the scope was disposed via a test `IServiceScopeFactory`). | | |
| TASK-013 | Run the same unit, integration and benchmark commands as TASK-005 and TASK-006. Record "after phase 2". | | |

### Implementation Phase 3: Compiled query and allocation reduction

- GOAL-003: Cut per-read CPU and allocation in EF and the client extension.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | Replace the Phase 2 query with `EF.CompileAsyncQuery` held in a `static readonly` field per `TContext` closed generic type. Parameters: `TContext`, `string containerHash`, `string nameHash`, `long inlineLimit`. Keep the non-compiled path as a fallback for InMemory if the compiled query fails to run there. | | |
| TASK-015 | In `BlobBytesStorageExtensions.DownloadBytesAsync` (`src/Application.Storage/Blobs/Content/`), replace `new MemoryStream()` with a pre-sized buffer when `download.Info.Length` is between 1 and `int.MaxValue`. When the stream is already a `BlobBufferedReadStream` exposing its array, return that array without copying (internal accessor, no public API change). | | |
| TASK-016 | Avoid double SHA-256 per read where a hash is already computed: compute container hash once per client call path only if profiling shows more than 3 percent of time. Otherwise leave unchanged and note the result. | | |
| TASK-017 | Re-run all benchmark classes. Record "after phase 3". Compare to REQ-010 targets. | | |

### Implementation Phase 4: Documentation and comparison

- GOAL-004: Publish verified before and after numbers and update docs.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-018 | Create `plan/pln-feature-blob-ef-read-performance-1-results.md` with baseline, after-phase tables, ratios and a pass/fail line per REQ-010 target. | | |
| TASK-019 | Update `docs/features-storage-blobs.md` read-path section: single query for blobs up to `ChunkSize`, forward-only streaming for larger blobs, memory notes. | | |
| TASK-020 | Add an ADR in `docs/adr/` using the `adr-architecture` skill, covering the single-query read and the end-of-stream rule (REQ-004). | | |
| TASK-021 | Update `CHANGELOG.md` under the unreleased section. | | |

### Implementation Phase 5 (gated): Bulk read

- GOAL-005: Reduce time to read many keys per request. Requested by the user (planned workload: read many items at once).

**Spike result (2026-10-30, `BlobStorageEfSqlServerBatchReadBenchmarks`, request of 100 distinct keys, mean per request):**

| Payload | DevKit, 16 parallel single reads | Raw SQL, 16 parallel | Raw SQL, 1 query of 100 keys | Raw SQL, 4 parallel queries of 25 keys | Raw SQL, 10 parallel queries of 10 keys |
|---------|-----------|-----------|----------|----------|----------|
| 10 KB | 20.5 ms (19.8 MB) | 11.0 ms | 6.4 ms | 5.4 ms | 6.9 ms |
| 100 KB | 48.2 ms (44.4 MB) | 40.7 ms | 46.4 ms | 38.7 ms | 41.3 ms |
| 1 MB | 413 ms (326 MB) | 402 ms | 448 ms | 375 ms | 379 ms |

Findings: a client-side parallel helper cannot beat the existing `DevKit_Parallel16` row, because that row already is 16-way parallel; it would only add convenience. A provider level multi-get (`IN` list plus chunk join) removes the per-request overhead and reaches about 3.2 to 3.8x at 10 KB, where the workload is latency bound. At 100 KB and above the read is bandwidth bound and batching gains at most 10 to 20%. The best shape is a few parallel batches (4 x 25), not one huge query. DevKit allocates 19.8 MB per 100 reads at 10 KB against 3.1 MB for raw SQL, so scope and context creation per key is the other half of the gap.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-022 | Spike: add `ReadRequestRawSqlBatchAsync` and `BlobStorageEfSqlServerBatchReadBenchmarks` to measure the multi-get ceiling. Fix the benchmark seed ids, which collapsed 20 ids to 16 distinct values. | ✅ | 2026-10-30 |
| TASK-023 | Decision: option (C), chosen by the user on 2026-10-30. A plus an optional provider capability (`IBlobStoreBatchDownloadProvider`) implemented by the EF provider with compiled multi-key queries and one scope per batch, and a per-key fallback for providers without it. | ✅ | 2026-10-30 |
| TASK-024 | Implemented: `IBlobStoreBatchDownloadProvider`, `BlobDownloadBatchScope` (ambient scope, keeps the behavior pipeline per key), `DownloadBytesManyAsync` with `BlobBytesDownloadManyOptions`, `BlobStoreOptions.DownloadBatchSize`, and `EntityFrameworkBlobStoreProvider.DownloadManyAsync`. Result ordering, per-key `BlobStoreNotFoundError`, duplicate keys, and `ContinueOnError` are covered by unit, SQLite, and integration tests (SQL Server, PostgreSQL, SQLite). | ✅ | 2026-10-30 |
| TASK-025 | Added `DevKit_Many_Batches4` and `DevKit_Many_Batches8` to `BlobStorageEfSqlServerBatchReadBenchmarks`. Results below. | ✅ | 2026-10-30 |

**Result (2026-10-30, SQL Server, 100 distinct keys, mean per request):**

| Payload | DevKit, 16 parallel single reads | DevKit `DownloadBytesManyAsync` (16 workers, 4 batches) | Raw SQL, 4 x 25 (ceiling) |
|---------|-----------|-----------|----------|
| 10 KB | 20.1 ms (22.8 MB) | 9.4 ms (7.6 MB), 2.1x faster, 67% less allocation | 5.9 ms (3.1 MB) |
| 100 KB | 47.5 ms (46.4 MB) | 39.6 ms (35.3 MB), 17% faster | 38.2 ms (30 MB) |
| 1 MB | 410 ms (326 MB) | 385 ms (316 MB), 6% faster | 378 ms (307 MB) |

Eight batches in flight with 32 workers gave no further gain. The remaining gap to the raw ceiling at 10 KB is the per-key behavior pipeline, `DownloadBytesAsync`, and EF materialization.

## 3. Alternatives

- **ALT-001**: Batched multi-get with `WHERE NameHash IN (...)` plus chunk join. Previously rejected. The Phase 5 spike shows 3.2 to 3.8x for 10 KB blobs, so it is now option B or C of TASK-023. It needs an optional provider interface, a batch size limit (SQL Server allows 2100 parameters, PostgreSQL 32767), and a per-key result contract.
- **ALT-002**: Store small blobs inline on `__Storage_Blobs` (new column). Rejected: schema change and migration, forbidden by CON-001. It would give the same single-row read as Phase 2 without a join.
- **ALT-003**: Raw SQL read path for SQL Server only. Rejected: provider-specific code, bypasses model mapping, and the floor is already within about 1.3 to 1.5x of the Phase 2 target.
- **ALT-004**: In-process cache of hot blobs. Rejected: outside this plan's scope, and the workload reads many distinct keys.
- **ALT-005**: Use `ChunkSize` to infer end of stream. Rejected: the configured value can change after blobs were written (REQ-004).

## 4. Dependencies

- **DEP-001**: `Microsoft.EntityFrameworkCore` (existing, central package management).
- **DEP-002**: Docker and Testcontainers SQL Server 2022 for the benchmarks and integration tests.
- **DEP-003**: Benchmarks from commit `ad8a599c3` on branch `feature/blobs-ef-read-benchmarks`.

## 5. Files

- **FILE-001**: `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs`: `DownloadAsync`, `BlobChunkReadStream`, new query and buffered stream.
- **FILE-002**: `src/Application.Storage/Blobs/Content/BlobBytesStorageExtensions.cs`: pre-sized buffer in `DownloadBytesAsync`.
- **FILE-003**: `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderUploadDownloadTests.cs`: new read tests.
- **FILE-004**: `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderContractTestsBase.cs`: SQL Server coverage.
- **FILE-005**: `benchmarks/Application.Benchmarks/BlobStorageEfSqlServer*ReadBenchmark*.cs`: only additive changes.
- **FILE-006**: `plan/pln-feature-blob-ef-read-performance-1-results.md`: new results file.
- **FILE-007**: `docs/features-storage-blobs.md`, `docs/adr/`, `CHANGELOG.md`: documentation.

## 6. Testing

- **TEST-001**: Unit: zero-length, single-chunk, multi-chunk and truncated-chunk reads (TASK-004).
- **TEST-002**: Unit: query count is 1 for blobs up to `ChunkSize` and 1 + N above it (TASK-012).
- **TEST-003**: Unit: lease and connection released before the first read for buffered blobs (TASK-012).
- **TEST-004**: Unit: blob written with a different `ChunkSize` reads correctly (TASK-004).
- **TEST-005**: Integration (SQL Server): existing provider contract suite passes unchanged.
- **TEST-006**: Benchmark: REQ-010 targets, sequential and parallel classes, same parameters as baseline.
- **TEST-007**: Regression: all existing blob unit tests under `tests/Infrastructure.UnitTests` and `tests/Application.UnitTests` pass.

## 7. Risks & Assumptions

- **RISK-001**: The left-join projection with a conditional `Content` may translate differently or run slower on SQL Server than on SQLite. Mitigation: inspect the generated SQL and the execution plan (TASK-008) and fall back to a two-query read if the single query is slower than baseline.
- **RISK-002**: Loading `varbinary(max)` through a CASE expression may prevent streaming. Acceptable because it only applies to blobs at or below `ChunkSize`.
- **RISK-003**: Concurrent overwrite between the blob row read and chunk read cannot occur in one query, but a multi-chunk stream can still see mixed versions across queries. This is existing behavior and is not changed by this plan.
- **RISK-004**: Local Docker numbers understate network latency. In a real deployment the removed round trips are worth more, not less. Compare ratios, not absolute values.
- **RISK-005**: Benchmarks for 1 MB and above are dominated by local data transfer, so improvements there will be small.
- **ASSUMPTION-001**: Every non-empty blob has chunk index 0 and contiguous indexes.
- **ASSUMPTION-002**: `StorageBlob.Length` equals the sum of chunk `Length` values.
- **ASSUMPTION-003**: The workload reads each key directly by hash, so the unique index on (`ContainerHash`, `NameHash`) is the only lookup path needed.

## 8. Related Specifications / Further Reading

- `docs/specs/spec-application-storage-blobs.md`
- `docs/specs/spec-application-storage-blobs-high-volume-uploads.md`
- `docs/adr/0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md`
- `plan/pln-feature-blob-high-volume-uploads-1.md`
- `benchmarks/Application.Benchmarks/BlobStorageEfSqlServerReadBenchmarkEnvironment.cs`
