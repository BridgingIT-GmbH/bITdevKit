# ADR-0033: Use Time-Ordered GUIDs for EF Blob Row Ids

## Status

Accepted

## Date

2026-10-30

## Context

The EF blob tables use the internal row id as their clustered key: `__Storage_Blobs.Id` and
`__Storage_BlobChunks (BlobId, Index)`. Ids were `Guid.NewGuid().ToString("N")`, so every insert
landed on a random page. The question was whether this, or the number of rows, hurts reads.

Reads look blobs up by `(ContainerHash, NameHash)`, a unique non-clustered index with fixed-length
SHA-256 keys. That index is random by nature and is not affected by the id.

## Measurements

SQL Server 2022 in Docker, 1 KB blobs, 100 keys per request, 1,000 rows against 1,000,000 rows.
Cold means `DBCC DROPCLEANBUFFERS` before every iteration (the host file cache stays warm, so
real disks will be slower).

| Table (1M rows) | Random ids | Time-ordered ids |
|---|---|---|
| Blob PK pages / fragmentation | 135,464 / 99.0% | 90,910 / 0.1% |
| Chunk PK pages / fragmentation | 215,698 / 98.8% | 142,858 / 0.0% |
| Database file size | 4.1 GB | 3.0 GB |
| Page fullness (PKs) | 64% to 67% | 97% to 98% |

Mean per request (ms), `DownloadBytesManyAsync` (4 batches) and 100 single reads in sequence:

| Case (1M rows) | Random ids | Time-ordered ids |
|---|---|---|
| Warm, many | 5.8 | 5.9 |
| Warm, sequence | 116 | 110 |
| Cold, random keys, many | 31.4 | 32.2 |
| Cold, random keys, sequence | 302 | 306 |
| Cold, recently written keys, many | 34.5 | 26.4 (23% faster) |
| Cold, recently written keys, sequence | 307 | 196 (36% faster) |

With a warm cache, 1,000,000 rows read as fast as 1,000 rows. Cold reads of random keys cost about
3x more at 1M rows than at 1K rows (31 ms against 9.5 ms for the batch), which is the cost of
extra page reads, not of a slower lookup.

## Decision

Create blob row ids with `Guid.CreateVersion7().ToString("N")` through `StorageBlob.CreateId()`.
The format stays 32 hex characters, so the column and existing rows are unchanged. Existing random
ids stay valid and no migration is needed.

## Consequences

### Positive

- New rows append to the end of both clustered keys. Fragmentation drops from about 99% to under
  1%, pages stay about 97% full, and the tables are about 33% smaller in pages.
- Blobs written close together are stored close together, so reading recently written blobs from
  a cold cache is 23% to 36% faster.
- Fewer page splits on insert (not measured here).

### Negative

- Reads of random keys on a cold cache do not improve. The hash lookup index remains random.
- The row id now encodes the creation time. It is internal and is not exposed through the API.
- `Guid.CreateVersion7` is not monotonic within one millisecond, so ids created in the same
  millisecond are ordered randomly among themselves. The effect on fragmentation is negligible.

### Neutral

- PostgreSQL and SQLite use the same string id. B-tree locality applies there as well, but only SQL Server was measured.
- The redundant unique index `IX___Storage_BlobChunks_BlobId_Index` duplicates the chunk primary
  key (about 11,000 pages at 1M rows) and the `ExpiresAt` and `LeaseAcquiredUntil` indexes
  hold a row per blob even when the value is null. They cost writes and space but not reads, and
  removing them needs a schema migration, so they are left alone here.

## Follow-up (2026-11-25)

Use `Guid` for the blob row key and both foreign keys. `StorageBlob.CreateId()` now returns the UUIDv7 directly. EF applies a shared converter and writes the lowercase `N` form to `char(32)` columns. The public blob key remains the container and name.

The standard setup discovers the key mappings from the entity annotations and uses the database's default collation. Call `UseBlobRowIdCollation` when creating a fresh schema that requires UUIDv7 timestamp order across distinct milliseconds regardless of the database default. The extension sets all three key columns to `Latin1_General_100_BIN2` on SQL Server, `C` on PostgreSQL, or `BINARY` on SQLite. SQL Server's native GUID comparison gives precedence to the last six bytes. The textual form puts the timestamp prefix first when the collation sorts bytewise.

The measurements above compare the original random and time-ordered text keys. The follow-up verifies the mapping and order on fresh databases. The SQL Server benchmark opts into bytewise collation to control key ordering when comparing the two strategies.

## Document Storage follow-up (2026-11-25)

Fresh relational Document Storage schemas use the same UUIDv7 representation as blobs. `StorageDocument` creates its own UUIDv7 key. EF uses the shared converter for `StorageDocument.Id` and `StorageDocumentDimension.DocumentId`, with both columns stored as lowercase `char(32)` text.

`UseDocumentRowIdCollation` sets both columns to the same bytewise collation as Blob Storage for each provider. The default remains the database collation. This replaces the provider-native ID generation described in [ADR-0034](0034-sequential-ef-document-row-ids.md). The measurements above are from the blob workload.

## References

- `benchmarks/Application.Benchmarks/BlobStorageEfSqlServerLargeTableReadBenchmarks.cs`
- [ADR-0031](0031-single-query-ef-blob-read-for-small-blobs.md)
- [ADR-0032](0032-batched-ef-blob-reads-for-many-keys.md)
- [SQL Server GUID comparison](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/compare-guid-uniqueidentifier-values)
