# ADR-0030: Bound Blob Upload Admission and Group EF Chunk Flushes

## Status

Accepted

## Date

2026-08-04

## Context

Blob uploads are stream-first and atomic per blob, but concurrent callers could start an
unbounded number of provider operations, EF contexts, transactions, and database connections.
The EF provider also issued one `SaveChangesAsync` call per content chunk. Large bursts could
therefore create resource congestion while large individual blobs incurred avoidable database
round trips.

The public blob operation contract must remain unchanged. Caller streams cannot be copied or
owned by a background queue, and every relational upload must retain its existing transaction,
hash verification, overwrite, lease, and rollback semantics.

## Decision

- Provide optional process-local upload admission as an `IBlobStoreClient` behavior.
- Share one singleton coordinator across DI scopes and isolate limiter state by normalized,
  case-insensitive named store.
- Use `System.Threading.RateLimiting.ConcurrencyLimiter` with oldest-first processing, four
  active permits, sixteen queue positions, and a 30-second wait timeout by default.
- Return typed Result failures for a full queue and expired queue wait; preserve normal .NET
  cancellation for callers.
- Never inspect, buffer, seek, clone, or dispose the queued upload stream.
- Enable grouped EF chunk flushing by default with `ChunkFlushCount = 4` and
  `MaxPendingChunkBytes = 16 MB`.
- Flush when either threshold is reached, detach successfully flushed chunks, and flush the
  final partial group.
- Keep every relational group inside the existing per-blob transaction and commit once after
  complete-stream and expected-hash validation.
- Do not materialize old relational chunk content solely for rollback because the transaction
  already restores the committed version.

## Rationale

A bounded concurrency limiter rejects overload before provider resources are opened and avoids
the unbounded waiter set of a plain `SemaphoreSlim`. Per-store state reflects different backend
capacity while a singleton lifetime makes limits effective across scoped clients. Process-local
coordination avoids a distributed dependency; database pools and servers remain the cross-node
capacity boundary.

Grouped EF saves reduce round trips without blob-sized buffering. Combining a chunk-count limit
with a byte limit keeps memory predictable when chunk sizes change. Transactional intermediate
saves retain atomic visibility.

## Consequences

### Positive

- Active and waiting upload work is bounded per named store and observable.
- EF uploads use approximately `ceil(chunk count / 4)` chunk-phase saves unless the byte
  threshold causes an earlier flush.
- Pending content memory is bounded by the byte threshold plus at most one chunk and EF/object
  overhead.
- Existing provider and client operation interfaces remain unchanged.

### Negative

- Limits are per process, so multi-node capacity is the per-node limit multiplied by node count.
- Queued callers must keep their upload streams alive until completion.
- Grouped tracked entities use more memory than the former one-chunk cadence.

### Neutral

- The behavior is optional; without registration DevKit does not limit upload concurrency.
- `ChunkFlushCount = 1` remains available for an explicit one-save-per-chunk configuration.
- Azure and in-memory providers do not use EF chunk-flush settings.

## Alternatives Considered

- **Use `SemaphoreSlim` directly**
  - Rejected because waiting callers would be unbounded without another cancellation-safe FIFO
    queue.
- **Use the DevKit time-window rate limiter**
  - Rejected because request frequency is not active-operation concurrency.
- **Add a distributed limiter**
  - Rejected because it adds coordination latency and infrastructure beyond this feature.
- **Add `UploadManyAsync` or a background uploader**
  - Rejected because durability, restart behavior, stream ownership, and per-item results need a
    separate API design.
- **Save the entire blob in one EF call**
  - Rejected because memory would scale with blob size.
- **Retain one save per chunk as the default**
  - Rejected because this is a new capability and count one remains explicitly configurable.

## Related Decisions

- [ADR-0004](0004-repository-decorator-behaviors.md): Repository Pattern with Decorator Behaviors
- [ADR-0016](0016-logging-observability-strategy.md): Logging & Observability Strategy
- [ADR-0018](0018-dependency-injection-service-lifetimes.md): Dependency Injection & Service Lifetime Management

## References

- [High-volume Blob Storage specification](../specs/spec-application-storage-blobs-high-volume-uploads.md)
- [Blob Storage feature documentation](../features-storage-blobs.md)
- [Base Blob Storage specification](../specs/spec-application-storage-blobs.md)
