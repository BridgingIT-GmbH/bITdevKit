---
created: 2026-06-22
status: proposed
---

# Document Storage Entity Framework Performance Specification

> This specification defines a measured performance improvement of the Entity Framework Document Storage provider: a batched `GetManyAsync`, a sequential row id strategy, a leaner read path, batched bulk writes and index changes. It ports the approach of ADR-0031, ADR-0032 and ADR-0033 (Blob Storage) to documents.

[TOC]

## Background

The blob provider gained large read-throughput improvements from four changes: a compiled single-key query, a batched multi-key read, time-ordered row ids and a metric for bytes per second. The document provider (`EntityFrameworkDocumentStoreProvider`) has the same shape and the same weaknesses, so most of the gains should transfer. Nothing below has been measured yet. Phase 0 produces the baseline, and every later phase must beat it on the benchmark before it is kept.

Observations from reading the code:

| Area | Current behavior | Expected cost |
|---|---|---|
| Row id | `StorageDocument.Create` sets `Guid.NewGuid()` on the clustered primary key | Random inserts split pages and fragment the clustered index at scale |
| Reading N documents | No batch API; the caller loops `GetAsync` | N scopes, N `DbContext` instances, N round trips |
| Single read | One round trip, but the 6-predicate query and 3 SHA-256 key hashes are rebuilt per call | Modest overhead per call |
| Read copies | `Map` calls `Content?.ToArray()`, then `DocumentStoreClient` calls `stored.Content.ToArray()` again | Two extra payload copies per read |
| Integrity checks | SHA-256 over stored bytes and again over logical bytes | Required; stays |
| Transform metadata | `TransformMetadataJson` parsed on every row | Small CPU cost per row |
| `UpsertManyAsync`, `DeleteManyAsync` | Loop of single operations, each with its own scope, SELECT and `SaveChanges` | N times the round trips |
| Full-type `FindPage`/`ListPage` | Sorted by raw `PartitionKey`, `RowKey`; the `string.Compare` continuation is not sargable | Each page scans every row of the type (opt-in via `AllowFullScans`) |
| Expiration index | `(TypeHash, ExpiresAtUnixMilliseconds)` covers all rows although most are NULL | Index larger than needed |

## Goals

- Add a batched read, `GetManyAsync`, that costs a bounded number of round trips regardless of the number of keys.
- Keep inserts append-friendly on SQL Server, and verify the effect on the other EF providers instead of assuming it.
- Remove avoidable allocations and copies from the read path without weakening integrity verification.
- Make `UpsertManyAsync` and `DeleteManyAsync` use one context, one lookup and one `SaveChanges` per batch in the EF provider.
- Make full-type paging and expiration sweeps index-friendly.
- Keep every existing observable behavior: ETag semantics, create-only, stop-at-first-failure batches, continuation tokens, expiration snapshots and capability reporting.
- Prove each change with a SQL Server benchmark and keep the results in the repository.

## Non-Goals

- No Postgres or SQLite benchmarks. Correctness on those providers is covered by the existing integration tests.
- No change to payload transforms, serialization, envelope format or hash algorithms.
- No change to the continuation token format.
- No batched `FindPage` or `ListPage`.
- No NDJSON or streaming document format work.

## Public Operations

### `GetManyAsync`

`IDocumentStoreClient<T>` gains:

```csharp
Task<Result<IReadOnlyList<Result<DocumentEntry<T>>>>> GetManyAsync(
    IReadOnlyList<DocumentKey> keys,
    CancellationToken cancellationToken = default);
```

Semantics:

- The outer result fails only for request level errors: `keys` is null, a key is invalid, `keys.Count` exceeds `DocumentStoreOptions.MaxTake`, or the operation is cancelled or the provider fails as a whole.
- The inner list has exactly one entry per input key, in input order. Each entry is the same `Result<DocumentEntry<T>>` that `GetAsync` would return for that key: success, `DocumentNotFound`-style failure for a missing or expired document, or a verification failure for corrupt content.
- Duplicate keys are allowed. Each duplicate gets its own entry. The provider reads a duplicated key once.
- An empty input returns an empty list without touching the provider.
- Expired documents are reported as not found, using the same expiry rule as `GetAsync`.
- Payload transforms (compression, encryption) run per document inside `DocumentStoreClient<T>`, so no ambient scope like `BlobDownloadBatchScope` is needed. The batch is an interface member so that behaviors can wrap the whole batch.

`GetManyAsync` is an interface member and not an extension, because the Cache, Logging, Metrics, Profiling, Retry, Timeout and Chaos behaviors must see one logical operation for the whole batch.

### Optional provider contract

```csharp
public interface IDocumentStoreBatchGetProvider
{
    Task<Result<IReadOnlyList<StoredDocument>>> GetManyAsync(
        DocumentTypeIdentity type,
        IReadOnlyList<DocumentKey> keys,
        CancellationToken cancellationToken);
}
```

- Providers that implement it receive the de-duplicated key list and return the found documents. Missing keys are absent from the list; the client maps them to not-found results.
- `DocumentStoreClient<T>` uses the batch provider when available and otherwise falls back to key-by-key `IDocumentStoreProvider.GetAsync`, so the in-memory, Azure Blob, Azure Table and Cosmos providers keep working with no change.
- The interface is public and lives next to `IDocumentStoreProvider`. No `internal` types are introduced.

### Options

`EntityFrameworkDocumentStoreOptions` (or the existing provider options type) gains:

| Option | Default | Valid range | Meaning |
|---|---|---|---|
| `GetManyBatchSize` | 25 | 1 to 1000 | Maximum number of row hashes per SQL query |

Invalid values fail at options validation, matching `DownloadBatchSize` for blobs. The default is a starting point. Phase 2 confirms or changes it from benchmark data.

### Behaviors

| Behavior | `GetManyAsync` handling |
|---|---|
| Cache | Look up every key in the cache, fetch only the misses with one inner `GetManyAsync`, cache the successes, return all in input order |
| Logging | One log entry per batch with key count and failure count |
| Metrics | Record under operation `get_many`; count of documents and failures; no per-key cardinality |
| Profiling | One span per batch |
| Retry | Retry the whole batch on transient provider failure; per-key not-found is not retried |
| Timeout | One timeout for the whole batch |
| Chaos | One injection decision per batch |

`DocumentStoreClientBehaviorBase<T>` forwards `GetManyAsync` virtually to `Inner`. The inline implementer in `DocumentStorageBuilderContext.cs`, `DocumentStoreClientAccessor<T>`, and the test stubs and scripted clients are updated to implement the member.

## Provider Expectations

### Entity Framework (all relational providers)

#### Batched read

- Group keys by `(TypeHash, PartitionKeyHash)`. Issue one query per group and per `GetManyBatchSize` chunk:

  ```csharp
  context.StorageDocuments.AsNoTracking()
      .Where(d => d.TypeHash == typeHash
               && d.PartitionKeyHash == partitionHash
               && rowHashes.Contains(d.RowKeyHash))
  ```

- This shape seeks on the unique `(TypeHash, PartitionKeyHash, RowKeyHash)` index.
- After the query, confirm `Type`, `PartitionKey` and `RowKey` on each row against the requested key, as `Exact` does today, to defend against hash collisions. A mismatch is treated as not found.
- Apply the existing expiry predicate.
- Use one `AsyncScope` and one `DbContext` for the whole call. Chunks run sequentially on that context.
- Use a compiled query where the shape is fixed. A variable-length `Contains` cannot be compiled the usual way, so Phase 2 must verify against the blob implementation (`EntityFrameworkBlobStoreProvider`) which chunk sizes and padding make the query plan stable. If padding is used, pad with a hash value that cannot match.

#### Single read

- Replace the per-call query construction in `Get` with a static compiled query over `(typeHash, type, partitionHash, partitionKey, rowHash, rowKey)`.
- Keep the 6-predicate semantics, so collision defence is unchanged.

#### Read-path copies

- `Map` must not call `Content?.ToArray()` when the entity buffer is already a private snapshot owned by the no-tracking query result. Hand the array to `StoredDocument` directly.
- `DocumentStoreClient<T>` must not call `stored.Content.ToArray()` before verification and transformation. Operate on the array or a `ReadOnlyMemory<byte>` over it.
- The stored-hash and logical-hash SHA-256 checks stay. They are the integrity guarantee.
- Cache parsed transform metadata only if the benchmark shows it matters. Parsing is skipped for rows whose `TransformMetadataJson` is empty.

#### Row ids

- `StorageDocument.Create` stops assigning `Guid.NewGuid()`. The key is configured `ValueGeneratedOnAdd`, so the EF provider's own generator runs:
  - SQL Server: sequential `uniqueidentifier` (`NEWSEQUENTIALID`-style generation in EF's client generator).
  - Npgsql: behavior with Npgsql EF 10.0.3 is not yet verified. Phase 1 checks it and, if the generator is random, documents the result.
  - SQLite: random. No fix is planned.
- `Guid.CreateVersion7` is not a fix for SQL Server on its own. `uniqueidentifier` sorts by its last six bytes first, so a v7 value is still effectively random for the clustered index. It is an option only for providers that order Guids byte-wise (Postgres `uuid`).
- If EF's generator proves unsuitable, the fallback is a small public `IDocumentRowIdGenerator` chosen per provider. That fallback needs its own ADR.
- Existing rows keep their ids. No data migration is needed for ids.

#### Bulk writes

- `UpsertManyAsync` and `DeleteManyAsync` in the EF provider use one scope and one context per batch:
  1. One query loads all existing rows for the batch keys, using the batched read shape above.
  2. Each operation is evaluated in input order against the loaded state: create-only conflicts, ETag mismatches, expiration and property preservation.
  3. One `SaveChanges` persists the successful prefix.
- The stop-at-first-failure contract is preserved. Operations after the first failure are not applied. The prefix that succeeded stays committed. `DocumentBatchResult<T>` reports `Items`, `FailedKey` and `FailedKeys` as today.
- A concurrency or unique-key exception during the single `SaveChanges` falls back to the existing per-operation path for the affected batch, so semantics under contention are identical to today. The 4-attempt retry on `DbUpdateException` stays for single upserts.
- Duplicate keys within one batch are applied in input order, with later operations seeing the result of earlier ones.
- Because the client contract is on `IDocumentStoreClient<T>`, this needs an optional provider interface, `IDocumentStoreBulkWriteProvider`, with `UpsertManyAsync` and `DeleteManyAsync` over `StoredDocumentWrite` and key lists. Providers without it keep the current loop.

#### Indexes and schema

All schema changes are allowed. Each needs a migration note in the documentation and updated test migration stubs.

| Change | Reason | Notes |
|---|---|---|
| Filtered index `(TypeHash, ExpiresAtUnixMilliseconds)` where `ExpiresAtUnixMilliseconds IS NOT NULL` | Most rows have NULL expiry; the filtered index is far smaller and is what `SweepExpired` needs | SQL Server and SQLite support `HasFilter`; Postgres supports partial indexes. Needs fluent configuration, see below |
| Index `(TypeHash, PartitionKey, RowKey)` | Makes full-type keyset paging a seek instead of a scan | Key columns are `nvarchar(256)`, so the key size is about 1 KB; verify the 900-byte clustered limit does not apply (it applies to clustered only; nonclustered allows 1700 bytes). Evaluate against the benchmark |
| Keyset continuation without `string.Compare` | The current predicate is non-sargable | Use a lexicographic `(PartitionKey > p) OR (PartitionKey = p AND RowKey > r)` predicate that the new index can seek |
| Clustering on `(TypeHash, PartitionKeyHash, RowKeyHash)` | Best locality for the dominant access pattern | Larger change: changes the primary key. Evaluate only if Phases 1 to 3 leave a measurable gap. Needs its own ADR |

Shipping index changes: `StorageDocument` is mapped with annotations only, and a filtered index cannot be expressed with annotations. The spec requires one of:

- an `IEntityTypeConfiguration<StorageDocument>` that the library exposes and `IDocumentStoreContext` consumers apply through an extension such as `modelBuilder.ApplyDocumentStoreConfiguration()`; or
- the same configuration applied by the `StorageDocument` entity registration the context already uses, if one exists.

Phase 4 selects the mechanism after checking how `IDocumentStoreContext` consumers register the entity today. Whichever is chosen must keep consumers that apply nothing extra working: they simply lack the new indexes.

#### Migration and compatibility

- The library does not own migrations. Consumers generate them from their model. The documentation must list the new indexes and the generated migration steps.
- Dropping the old `(TypeHash, ExpiresAtUnixMilliseconds)` index and adding the filtered one is an online operation on SQL Server Enterprise only; other editions lock the table. The documentation must say so and recommend a maintenance window for large tables.
- A database with the old schema keeps working. Queries remain correct without the new indexes; they are only slower.
- The test migration stubs that reference `__Storage_Documents` are updated: `tests\Application.IntegrationTests\Migrations\20230526172401_Initial.cs`, `tests\Infrastructure.IntegrationTests\EntityFramework\Migrations\20231005163848_Initial.cs`, their model snapshots and `_shared\Stubs.cs`.

### Other providers

In-memory, Azure Blob, Azure Table and Cosmos need no change. `GetManyAsync` uses the key-by-key fallback. A native batch implementation for Cosmos or Table can follow later and is out of scope.

### Capabilities

No capability changes. `FullMatch` and `KeyListing` stay `SupportedEfficiently`. After Phase 4, full-type paging may be re-evaluated for `SupportedEfficiently`; that is a separate decision and needs benchmark evidence.

## Errors

- A missing or expired key is a per-key failure inside the inner list, never an outer failure.
- A key that fails validation fails the whole request before any I/O, so no partial work happens.
- A hash mismatch or envelope failure for one document fails that entry only.
- Provider exceptions fail the outer result using the same exception-to-result mapping `GetAsync` uses.
- Bulk write failures follow the existing `DocumentBatchResult<T>` contract.

## Benchmarks

New benchmark classes live in `benchmarks\Application.Benchmarks`, reuse the blob SQL Server environment pattern (`BLOB_BENCH_SQL_CONNECTION` style variable, for example `DOC_BENCH_SQL_CONNECTION`) and run against a throwaway SQL Server container.

| Benchmark | Measures |
|---|---|
| Single read | `GetAsync` latency for 1 KB, 10 KB and 100 KB payloads |
| Batch read | `GetManyAsync` against parallel `GetAsync` and a raw SQL ceiling, with 1, 10, 25, 100 and 1000 keys |
| Bulk write | `UpsertManyAsync` and `DeleteManyAsync` with 10, 100 and 1000 documents |
| Insert at scale | Insert throughput and clustered index fragmentation at 1M rows, comparing random ids with EF sequential ids |
| Large table reads | Cold and warm single and batch reads against 1M rows |
| Paging | Full-type `FindPage` and `ListPage` page latency at 1M rows before and after the index and keyset change |
| Expiration sweep | `SweepExpired` with 1M rows, 1% expiring, before and after the filtered index |

Method:

- Phase 0 runs the full set against the unchanged provider and stores the raw reports under `docs\benchmarks` or the location the blob reports use. Every later phase reruns the affected subset.
- Report blobs per second and MiB per second for reads and writes, in the same table format as the blob appendix.
- Report fragmentation with `sys.dm_db_index_physical_stats` (`avg_fragmentation_in_percent`, page count) after seeding.

Acceptance criteria (a change is kept only if met on SQL Server):

| Change | Criterion |
|---|---|
| `GetManyAsync` | At least 2x the throughput of parallel `GetAsync` for 1 KB to 10 KB payloads with 100 keys; no regression for 100 KB |
| Row ids | Lower fragmentation and no slower inserts at 1M rows than random Guids |
| Compiled single read and copy removal | Lower allocations per read in the memory diagnoser; no latency regression |
| Bulk writes | At least 3x the throughput of the loop for 100 documents |
| Paging and indexes | Page latency independent of table size for full-type paging; sweep cost proportional to expiring rows |

If a change misses its criterion, it is dropped or reworked and the result is recorded in the ADR.

## Delivery Phases

| Phase | Content | Schema change |
|---|---|---|
| 0 | Benchmark infrastructure and baseline for the unchanged provider | No |
| 1 | Row id generation: stop assigning `Guid.NewGuid()`, verify Npgsql and SQLite behavior, measure fragmentation at 1M rows | No |
| 2 | `GetManyAsync`: client interface, behaviors, batch provider interface, EF implementation, Cache behavior | No |
| 3 | Read path: compiled `Get`, copy removal, metadata parsing | No |
| 4 | Index changes and keyset continuation, configuration mechanism and migration notes | Yes |
| 5 | Bulk writes: `IDocumentStoreBulkWriteProvider` and EF implementation | No |
| 6 | Documentation, appendix with measured figures, ADRs and changelog | No |

Each phase ends with a green build, green unit tests and green integration tests for the EF providers before the next starts. Each phase is its own commit.

## Verification

Unit tests (`tests\Application.UnitTests\Storage\Documents\`):

- `GetManyAsync` on `DocumentStoreClient<T>`: order, duplicates, missing keys, expired keys, empty input, invalid key, over-limit input, per-key verification failure, provider fallback when the batch interface is absent.
- Behavior forwarding for every behavior, including Cache hit, miss and mixed batches, and Metrics tags.
- Bulk write semantics against a scripted provider: prefix commit, `FailedKey`, `FailedKeys`, ETag mismatch and create-only conflict.
- Options validation for `GetManyBatchSize`.

Provider contract tests (`tests\Infrastructure.IntegrationTests\EntityFramework\Storage\Documents\`), run on SQL Server, Postgres and SQLite:

- Batch read across multiple partitions and types, chunk boundaries (`GetManyBatchSize` and one more), hash collision defence (forced via a test hash), expiry.
- Bulk upsert and delete: ordering, stop-at-first-failure, ETag, create-only, duplicate keys, concurrent writer fallback.
- Paging still returns identical sequences and continuation tokens before and after the keyset predicate change, including across expiration.
- Row id assignment: ids are populated by EF and unique.

Guards:

- Check whether a source guard like `BlobStorageSourceGuardTests` applies to the document folders. If so, keep new public types free of `internal` visibility.
- Every new public member has XML documentation with an `<example>`.

## Risks

- **Sequential ids on Npgsql and SQLite.** The gain may be SQL Server only. Phase 1 states the finding per provider and does not claim more.
- **Filtered index on SQLite and Postgres.** Syntax and semantics differ. Verify with the integration tests on all three providers.
- **Shared contexts.** Consumers may pool or customize the context. Do not rely on state carried between calls.
- **Behavior changes under contention.** Batched writes must fall back to the single-operation path on conflict so results match today.
- **Cache behavior.** Partial hits must not reorder or drop entries. Covered by unit tests.
- **Compiled query with `Contains`.** Query plan stability depends on chunk padding. Verified by the batch benchmark.
- **Index width.** `nvarchar(256)` key columns make the `(TypeHash, PartitionKey, RowKey)` index wide. Phase 4 measures the size and drops the index if the benefit does not justify it.

## Related Documents

- `docs\specs\spec-application-storage-documents-paging.md`
- `docs\adr\0031-*.md`, `docs\adr\0032-batched-ef-blob-reads-for-many-keys.md`, `docs\adr\0033-time-ordered-ef-blob-row-ids.md`
- `docs\features-storage-blobs.md` (performance appendix)
- `docs\features-storage-documents.md`
