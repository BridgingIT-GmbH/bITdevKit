# ADR-0036: Add a Keyset Index and a Filtered Expiration Index for EF Documents

## Status

Accepted

## Date

2026-11-02

## Context

Paging over a full document type sorted by `PartitionKey, RowKey` and continued with a `string.Compare` predicate that the database cannot seek, so each page scanned every row of the type. The index `(TypeHash, ExpiresAtUnixMilliseconds)` held a row for every document although most have no expiration.

`StorageDocument` is mapped with annotations, and a filtered index cannot be expressed with annotations.

## Decision

- Add the annotation index `(TypeHash, PartitionKey, RowKey)` to `StorageDocument`. Key columns are `nvarchar(256)`. The index is non-clustered, so the 900-byte clustered limit does not apply.
- Change the continuation predicate to `PartitionKey >= p AND (PartitionKey > p OR RowKey > r)`, which the index can seek.
- Add `ModelBuilder.ApplyDocumentStoreConfiguration()` (`DocumentStoreModelBuilderExtensions`). It makes the `(TypeHash, ExpiresAtUnixMilliseconds)` index filtered with `"ExpiresAtUnixMilliseconds" IS NOT NULL`. The double-quoted form is valid on SQL Server (with quoted identifiers on), PostgreSQL and SQLite. Calling it is optional.
- Clustering on the hash columns, which changes the primary key, is not part of this decision.

## Consequences

### Positive

- Full-type paging seeks instead of scanning, and a page costs the same at any position.
- The filtered index is much smaller and still serves `SweepExpiredAsync`.

### Negative

- Consumers must generate a migration. The library does not own migrations.
- On SQL Server, dropping and recreating an index is online on Enterprise edition only. Other editions lock the table, so large tables need a maintenance window.
- The keyset index adds write cost and space. Databases on the old schema keep working, only slower on full scans and sweeps.
- Gains were measured only on SQL Server. At 1,000,000 rows a deep `FindPageAsync` page took 183 ms before and 10.4 ms after, and `ListPageAsync` 167 ms before and 8.3 ms after. A page still costs 3x to 5x more at 1,000,000 rows than at 1,000 rows, so it is flat across depth but not independent of table size.
- The keyset index takes 24,426 pages (about 190 MB) at 1,000,000 rows and fragments to 42.6% under 100,000 inserts. Sustained insert time was the same as before (46.7 s against 49.7 s, within noise).
- The filtered expiration index is 29 times smaller (1,087 against 31,205 pages with 5% expiring rows) but a sweep of 1,000 expired rows is not faster (140 ms against 136 ms with the plain index, 168 ms before). It is justified by size only.

## References

- [Specification](../specs/spec-application-storage-documents-ef-performance.md)
- [Document Storage appendix](../features-storage-documents.md#appendix-ef-performance-and-indexes)
