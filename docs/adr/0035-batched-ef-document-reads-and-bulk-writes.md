# ADR-0035: Batch EF Document Reads and Writes Behind Optional Provider Interfaces

## Status

Accepted

## Date

2026-11-02

## Context

Reading many documents meant calling `GetAsync` in a loop: one scope, one `DbContext` and one round trip per key. `UpsertManyAsync` and `DeleteManyAsync` were the same loop, each iteration with its own scope, `SELECT` and `SaveChanges`. [ADR-0032](0032-batched-ef-blob-reads-for-many-keys.md) cut blob read time by more than half with a batched read.

Document storage has other providers (in-memory, Azure Blob, Azure Table, Cosmos) that should not change.

## Decision

- Add `IDocumentStoreClient<T>.GetManyAsync`, returning one `Result<DocumentEntry<T>>` per key in input order. A missing or expired key is a per-item `DocumentStoreNotFoundError`. A key that fails validation fails the whole call before any I/O. Duplicate keys are read once.
- Add two optional provider interfaces: `IDocumentStoreBatchGetProvider` and `IDocumentStoreBulkWriteProvider`. Providers without them keep the key-by-key and per-operation loops.
- The EF provider groups keys by partition and reads each chunk of `DocumentStoreOptions.GetManyBatchSize` (default 25, valid 1 to 1000) with one compiled query on the hash columns, then confirms the exact keys in memory. Up to `DocumentStoreOptions.GetManyMaxParallelism` chunks (default 4, valid 1 to 32) run at the same time, each with its own scope, context and connection. A sequential version was measured first and missed the goal of 2x over 16 parallel reads. A batch size of 100 as the default was faster for 1,000 keys but 17% slower for 100 KB documents, so it stays 25.
- The EF bulk writes use one scope and context, load the existing rows with the same chunked query and evaluate the operations in input order. One `SaveChanges` persists the successful prefix.
- Failure semantics are the existing ones: stop at the first failure, keep the prefix, report `FailedKey`. A missing key on delete is success. Duplicate keys apply in order.
- If the single commit throws, nothing was saved. The provider then repeats the batch through the per-operation path, which keeps the retry and conflict semantics of single writes under contention.
- The client does payload preparation (serialization, size limit, hashes, transforms) for every write before the provider call. A write that fails preparation stops the batch after the valid prefix.
- The behavior pipeline stays on the client contract, so cache, retry, timeout, profiling, chaos, logging and metrics see `GetManyAsync`.

## Consequences

### Positive

- Round trips for reading N keys grow with the number of chunks. Bulk writes need one lookup per chunk and one commit per batch.
- No change for other providers, which still work through the fallback.

### Negative

- Every `IDocumentStoreClient<T>` implementation and behavior has a new member.
- A batch that fails its commit and falls back costs one extra attempt.
- The cache behavior does not invalidate entries on `UpsertManyAsync` or `DeleteManyAsync`. This was already the case.
- The benefit is smaller than the plan assumed outside one-partition small reads (SQL Server, 100 keys, warm). `GetManyAsync` is 29x faster than a loop of `GetAsync` and 3.5x faster than 16 parallel `GetAsync` calls for 1 KB in one partition (4.0 against 14.0 ms), but 1.9x at 10 KB, 1.2x to 1.4x when the keys span 20 partitions, and equal at 100 KB. The criterion of 2x over 16 parallel reads was met only for 1 KB in one partition and nearly for 10 KB in one partition.
- On a cold 1,000,000 row table `GetManyAsync` (40 to 52 ms) is slower than 16 parallel `GetAsync` calls (28 to 29 ms) because only 4 batches are in flight. Raise `GetManyMaxParallelism` for I/O bound tables.
- One call can hold up to `GetManyMaxParallelism` connections, which affects pool sizing under many concurrent callers.
- Bulk writes of 100 documents are 6.2x to 7.4x faster than the loop (320.6 against 51.4 ms for inserts) and 12x to 15x at 1,000 documents for overwrite and delete.

## References

- [Specification](../specs/spec-application-storage-documents-ef-performance.md)
- [ADR-0032](0032-batched-ef-blob-reads-for-many-keys.md)
