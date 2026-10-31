# ADR-0034: Let EF Core Generate Sequential Row Ids for Documents

## Status

Accepted

## Date

2026-11-02

## Context

`StorageDocument.Id` is the clustered primary key of `__Storage_Documents`. `StorageDocument.Create` assigned `Guid.NewGuid()`, so every insert landed on a random page. [ADR-0033](0033-time-ordered-ef-blob-row-ids.md) measured the effect for blobs: 99% fragmentation at 1,000,000 rows against under 1% with time-ordered ids, and tables about a third smaller.

Documents use a `Guid` column, not a string. SQL Server sorts `uniqueidentifier` by its last six bytes first, so `Guid.CreateVersion7` is still effectively random for the clustered index there. PostgreSQL `uuid` sorts byte-wise, so version 7 would work, but only on that provider.

## Decision

Stop assigning `Id` in `StorageDocument.Create`. EF Core's value generator for `Guid` keys then creates the value: `SequentialGuidValueGenerator` on SQL Server (ordered for `uniqueidentifier`) and Npgsql's sequential generator on PostgreSQL (byte order). SQLite keeps random ids.

Existing rows keep their ids and no migration is needed. The id is internal and not part of the public API.

## Consequences

### Positive

- New rows append to the clustered key on SQL Server and PostgreSQL, so fragmentation and page splits drop and tables stay denser.
- No custom generator type or configuration surface is added, and each provider uses the ordering it sorts by.
- An integration test checks that ids are distinct and, on SQL Server (`SqlGuid` order) and PostgreSQL (byte order), increase in insert order.

### Negative

- Ids on SQLite are random. For SQLite this has no practical effect.
- The id depends on the EF provider's generator, so a custom provider may return random ids.
- Insert time did not improve. At 100,000 inserts with 8 writers into 1,000,000 rows, 49.7 s with random ids and 46.7 s with sequential ids are within noise, because the log flush dominates.

### Measured

SQL Server, 100,000 parallel inserts into a 1,000,000 row table built in id order: clustered index fragmentation 34.6% with random ids against 15.3% with sequential ids, 766,676 against 722,319 pages, page fullness 72.8% against 77.3%. A table that was already fragmented only improved on the new pages (99.3% to 86.3% against 99.3%). See the [appendix](../features-storage-documents.md#appendix-ef-performance-and-indexes).

## References

- [Specification](../specs/spec-application-storage-documents-ef-performance.md)
- [ADR-0033](0033-time-ordered-ef-blob-row-ids.md)
