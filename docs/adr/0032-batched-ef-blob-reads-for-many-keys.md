# ADR-0032: Batch EF Blob Reads for Many Keys Behind an Ambient Scope

## Status

Accepted

## Date

2026-10-30

## Context

After [ADR-0031](0031-single-query-ef-blob-read-for-small-blobs.md) a single small blob costs one
query. A request that reads many small blobs by generated key (100 keys of 10 KB) still pays one
EF scope, one query, and one connection checkout per key. 16 parallel single reads took 20.1 ms
and allocated 22.8 MB on SQL Server. A raw SQL probe showed that 4 parallel multi-key queries of
25 keys need 5.4 ms to 5.9 ms for the same request.

`IBlobStoreClient` is decorated by many behaviors (compression, encryption, cache, retry, and
others). Adding a batch method to it would break every decorator, and a batch path that bypasses
the pipeline would skip the per-blob transforms.

## Decision

- Add the optional provider capability `IBlobStoreBatchDownloadProvider` with
  `MaxDownloadBatchSize` and `DownloadManyAsync`. A key that is missing from the returned
  dictionary is read individually.
- Add the client extension `DownloadBytesManyAsync` with `BlobBytesDownloadManyOptions`
  (`MaxDegreeOfParallelism`, `MaxConcurrentBatches`, `ContinueOnError`). It returns one item per
  requested key in input order, with a per-key `BlobStoreNotFoundError` for missing blobs.
- Carry the key set to the innermost `ProviderClient` with an `AsyncLocal` scope
  (`BlobDownloadBatchScope`). Each key still travels through the full behavior pipeline on its own,
  and the innermost call takes its result from a shared multi-key read when the key is part of the
  scope.
- Implement the capability in the EF provider with one compiled query per container and batch
  (`NameHash IN (...)` joined to chunk index 0) in a single scope lease. Blobs larger than
  `ChunkSize` are left out of the batch result and use the streaming single read.
- Bound the number of batches in flight with `MaxConcurrentBatches` (default 4). Add
  `BlobStoreOptions.DownloadBatchSize` (default 25, valid 1 to 1000).
- Fall back to a single read for duplicate keys, retries, keys outside the scope, and any batch
  that throws.

## Rationale

The ambient scope keeps `IBlobStoreClient` and all decorators unchanged. `Parallel.ForEachAsync`
captures the execution context, so the scope reaches every key worker. A batch of 25 keys with 4 in
flight was the best shape in the probe. Larger batches serialize on one connection, and smaller
ones add round trips. SQL Server allows 2100 parameters and PostgreSQL 32767, so the batch size is
capped at 1000.

The batch read serves rows from one scope. Content is copied into in-memory streams before the
lease is released, so a batch never holds a connection after the call returns.

## Consequences

### Positive

- 100 keys of 10 KB: 20.1 ms to 9.4 ms (2.1x), allocation 22.8 MB to 7.6 MB (67% less).
- 100 keys of 100 KB: 47.5 ms to 39.6 ms (17%). 100 keys of 1 MB: 410 ms to 385 ms (6%), because
  large payloads are bandwidth bound.
- Behaviors such as compression and encryption keep working per blob.
- No change to `IBlobStoreClient`. Providers without the capability behave as before.

### Negative

- A new public surface: one provider interface, three option and result types, and one scope type.
- Batches are prefetched, so a cache hit can still load neighbouring keys that it did not need.
- All content of a batch is buffered in memory before it is consumed.
- The gap to the raw SQL probe (9.4 ms against 5.9 ms at 10 KB) is the per-key behavior pipeline,
  `DownloadBytesAsync`, and EF materialization.

### Neutral

- Only the first occurrence of a duplicate key is batched.
- Azure and in-memory providers read key by key with the same bounded parallelism.

## Alternatives Considered

- **Batch method on `IBlobStoreClient`**
  - Rejected because every decorator would need to implement it.
- **Batch path that bypasses the behavior pipeline**
  - Rejected because it would skip compression, encryption, and caching.
- **Client-only parallel helper**
  - Rejected as the sole option. The existing benchmark is already 16-way parallel, so it adds
    convenience but no speed.
- **Raw SQL for the batch read**
  - Rejected for the same reasons as in ADR-0031.

## References

- `plan/pln-feature-blob-ef-read-performance-1.md`
- [ADR-0031](0031-single-query-ef-blob-read-for-small-blobs.md)
