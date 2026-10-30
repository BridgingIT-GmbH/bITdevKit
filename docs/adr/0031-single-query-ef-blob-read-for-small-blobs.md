# ADR-0031: Read Small EF Blobs With a Single Compiled Query

## Status

Accepted

## Date

2026-10-30

## Context

The Entity Framework blob provider read a blob with three database round trips: one for the
blob metadata, one for the lease or snapshot, and one per content chunk. Workloads that read
many small compressed blobs by generated key (for example a day plus a 32-character id) are
dominated by this per-read overhead, not by content transfer. A BenchmarkDotNet suite in
`benchmarks/Application.Benchmarks` measured 3.28 ms per 10 KB read on SQL Server against a raw
SQL floor of 0.88 ms, with 255 KB allocated per read.

The public `IBlobStoreClient` and provider contracts must remain unchanged. SQL Server and
PostgreSQL need the gain; SQLite only has to stay correct.

## Decision

- For blobs whose stored length is at most `ChunkSize`, read the blob row and its chunk index 0
  in one left-join query and return the content as an in-memory stream.
- Release the provider lease before returning that stream, because no further database access
  is needed.
- For larger blobs, keep forward-only chunk streaming with one query per chunk. End the stream
  when `Position >= Length` and throw `InvalidDataException` when an expected chunk is missing.
- Run the inline read through `EF.CompileAsyncQuery`. Keep the compiled delegate on the provider
  instance, not in a static field.
- Return the inline content as a publicly visible `MemoryStream`, so `DownloadBytesAsync` can
  hand out the backing array without a second copy. Otherwise it pre-sizes its buffer from
  `BlobInfo.Length`.

## Rationale

The left join works on SQL Server, PostgreSQL, and SQLite. A correlated `FirstOrDefault` inside
the projection does not translate on SQLite. Selecting `Content` only when
`Length <= ChunkSize` keeps large blobs from loading a chunk they would read again later.

A compiled query removes expression-tree translation and cache lookup from each read. EF binds a
compiled query to the first provider it executes against. A static field on the generic provider
type therefore broke processes that run several providers with the same `DbContext` type, which
the integration tests do (SQL Server, PostgreSQL, and SQLite in one process). A per-instance
field avoids that and costs one compilation per provider instance.

## Consequences

### Positive

- 10 KB reads need 1 query instead of 3 and measured 1.40 ms instead of 3.28 ms on SQL Server.
- Allocation per read fell by 27% to 54% across 10 KB to 3 MB payloads.
- Small-blob reads no longer hold a lease or a connection while the caller consumes the stream.
- No public API change.

### Negative

- The first read on each provider instance pays the one-time query compilation.
- A blob written with a smaller `ChunkSize` than the current read `ChunkSize` is read through the
  inline path only if it has a single chunk. Existing multi-chunk blobs keep the streaming path.
- The remaining gap to the raw SQL floor (about 0.5 ms at 10 KB) is EF scope creation, key
  hashing, and materialization.

### Neutral

- Blobs above `ChunkSize` behave as before, except for the explicit end-of-stream and missing
  chunk handling.
- Azure and in-memory providers are unaffected.

## Alternatives Considered

- **Static compiled query**
  - Rejected because EF binds it to the first provider and breaks mixed-provider processes.
- **Raw SQL or Dapper for the read path**
  - Rejected because it bypasses the shared model, value conversions, and provider portability.
- **Read-through cache of small blobs**
  - Rejected for this change. The existing download cache behavior stays opt-in and separate.
- **Parallel bulk read helper (`DownloadBytesManyAsync`)**
  - Deferred. It is a new public API and needs its own design and measurement.

## References

- `plan/pln-feature-blob-ef-read-performance-1.md`
- [ADR-0030](0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md)
