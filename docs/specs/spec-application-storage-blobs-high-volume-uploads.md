---
status: implemented
---

# Design Specification: Blob Storage High-Volume Upload Control

> Add optional, bounded upload admission control and configurable Entity Framework chunk flushing so bursty and high-volume blob workloads remain predictable.

[TOC]

Related documents:

* [Blob Storage feature documentation](../features-storage-blobs.md)
* [Base Blob Storage design specification](spec-application-storage-blobs.md)

## Overview

Blob Storage currently exposes one atomic `UploadAsync` operation per blob. The Entity Framework provider creates an operation-owned `DbContext`, opens a transaction, reads the source stream in configured chunks, calls `SaveChangesAsync` for every chunk, and commits after the complete blob has been validated.

That design keeps one upload stream-first and bounds change-tracker growth, but it has two high-volume limitations:

* concurrent callers can start an unbounded number of upload operations, scopes, contexts, transactions, and database connections;
* the EF provider performs one `SaveChangesAsync` round trip per content chunk.

This specification introduces two complementary and independently configurable changes:

1. an optional `UploadConcurrencyBlobStoreClientBehavior` that admits a bounded number of uploads per named store and permits only a bounded number of callers to wait;
2. configurable EF chunk flushing that persists several bounded chunks per `SaveChangesAsync` call.

The behavior controls overload before it reaches a provider. EF chunk flushing reduces database round trips after an upload has been admitted. Neither change introduces a background upload service, a resumable-upload API, or cross-blob transactions.

## Goals

* Keep the feature opt-in and preserve existing upload behavior when it is not registered.
* Bound active and queued uploads per named blob store within one application process.
* Share admission state across DI scopes and client instances.
* Reject excess work with a typed Result error before opening a provider context or transaction.
* Preserve caller cancellation while waiting for admission.
* Preserve caller ownership of upload streams.
* Keep every blob upload independently atomic.
* Reduce EF `SaveChangesAsync` calls by flushing bounded groups of chunks.
* Bound pending chunk memory per active EF upload.
* Preserve size-limit, hash-verification, overwrite, lease, and rollback semantics.
* Provide logs, metrics, diagnostics, and tests that make queueing and flush behavior observable.
* Keep provider-neutral public blob operations unchanged.

## Non-Goals

This specification does not introduce:

* a public bulk-upload operation on `IBlobStoreClient`;
* a background queue that returns before the upload completes;
* durable upload queueing;
* resumable or multipart public upload APIs;
* cross-process or distributed concurrency limiting;
* cross-blob transactions or all-or-nothing batches;
* automatic concurrency tuning based on current database load;
* caller stream buffering, cloning, temporary-file spooling, or disposal;
* provider-specific types in the public client API;
* chunk batching for Azure Blob Storage or the in-memory provider;
* replacement of database connection-pool limits;
* rate limiting by requests per time window.

Applications that need durable asynchronous ingestion should use the existing jobs, queueing, or messaging features and call Blob Storage from bounded consumers.

## Terminology

| Term | Meaning |
| --- | --- |
| Active upload | An upload that owns an admission permit and may call the inner client/provider. |
| Queued upload | An `UploadAsync` caller waiting asynchronously for an admission permit. |
| Admission | The act of granting an upload permission to call the inner client. |
| Queue capacity | Maximum number of upload callers allowed to wait per named store. |
| Admission timeout | Maximum configured time an upload may wait for a permit. |
| Chunk flush | One EF `SaveChangesAsync` call that persists one or more newly read blob chunks. |
| Pending chunk bytes | Content bytes currently tracked by the operation-owned EF context but not yet flushed. |
| Named-store coordinator | Process-wide admission state associated with one blob client/store name. |

## Current Behavior

The current EF upload flow is:

1. create an operation scope and `DbContext`;
2. begin a relational transaction when supported;
3. load the existing blob and its rollback snapshot;
4. create or lease the blob row;
5. delete existing chunks for overwrite;
6. read one configured chunk;
7. add one `StorageBlobChunk`;
8. call `SaveChangesAsync`;
9. detach that chunk;
10. repeat until the stream ends;
11. update metadata;
12. call `SaveChangesAsync`;
13. commit.

Awaiting the source stream and database writes provides flow control within one upload. It does not limit how many callers may execute this flow concurrently.

`BlobStoreOptions.ChunkSize` controls stream-read and stored-row size. It is not an upload-concurrency option and currently also acts as the effective chunk flush cadence because every chunk is saved immediately.

## Design Summary

```text
UploadAsync caller
      |
      v
optional timeout/retry/telemetry behaviors
      |
      v
UploadConcurrencyBlobStoreClientBehavior
      |
      v
shared coordinator for store "reports"
  active <= MaxConcurrentUploads
  queued <= MaxQueuedUploads
      |
      v
provider UploadAsync
      |
      v
EF reads ChunkSize chunks
      |
      v
flush when chunk-count or pending-byte threshold is reached
      |
      v
final metadata save + transaction commit
```

Admission state is process-local. Multiple application nodes each enforce their own configured limit. Database connection-pool and server limits remain the final shared capacity boundary across nodes.

## Optional Upload-Concurrency Behavior

### Registration

Suggested registration:

```csharp
services.AddBlobStorage()
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithTimeoutBehavior(options =>
    {
        options.Timeout = TimeSpan.FromMinutes(2);
    })
    .WithRetryBehavior(options =>
    {
        options.Attempts = 3;
    })
    .WithUploadConcurrencyBehavior(options =>
    {
        options.MaxConcurrentUploads = 4;
        options.MaxQueuedUploads = 16;
        options.QueueWaitTimeout = TimeSpan.FromSeconds(30);
    })
    .WithEntityFrameworkClient<AppDbContext>("reports", options =>
    {
        options.ChunkSize = (int)ByteSize.Megabytes(4);
        options.ChunkFlushCount = 4;
        options.MaxPendingChunkBytes = ByteSize.Megabytes(16);
    });
```

`WithUploadConcurrencyBehavior` is optional. When it is not registered, uploads are passed to the provider without DevKit admission control.

The first registered behavior remains the outermost behavior. The recommended order places:

* logging, metrics, and an overall timeout outside admission so their elapsed time includes queue waiting;
* retry outside admission so a failed attempt releases its permit before retry backoff and reacquires admission for the next attempt;
* content transforms inside admission when expensive transform work should count as active upload work.

Applications may deliberately choose another order, but documentation must explain whether timeout and metrics include queue time and whether retry backoff holds a permit.

### Options

```csharp
public sealed class UploadConcurrencyBlobStoreClientBehaviorOptions
{
    public int MaxConcurrentUploads { get; set; } = 4;

    public int MaxQueuedUploads { get; set; } = 16;

    public TimeSpan QueueWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

Validation rules:

* `MaxConcurrentUploads` must be greater than zero.
* `MaxQueuedUploads` must be zero or greater.
* `QueueWaitTimeout` must be greater than zero.
* invalid behavior options fail during registration with `InvalidOperationException`, consistent with existing blob option registration.
* `MaxQueuedUploads = 0` means an upload must acquire an immediately available permit or receive an overload Result failure.

The defaults apply only after the behavior is explicitly registered. The initial defaults intentionally favor predictable resource use over maximum parallelism. Applications should tune them against blob size, EF connection-pool capacity, transaction duration, and the number of application nodes.

### Types

Suggested application-layer types:

```csharp
public sealed class UploadConcurrencyBlobStoreClientBehavior
    : BlobStoreClientBehaviorBase
{
    // Applies admission only to UploadAsync.
}
```

```csharp
public interface IBlobUploadAdmissionCoordinator
{
    ValueTask<BlobUploadAdmissionLease> AcquireAsync(
        string storeName,
        UploadConcurrencyBlobStoreClientBehaviorOptions options,
        CancellationToken cancellationToken = default);

    IReadOnlyCollection<BlobUploadAdmissionSnapshot> GetSnapshots();
}
```

```csharp
public sealed class BlobUploadAdmissionCoordinator
    : IBlobUploadAdmissionCoordinator, IDisposable
{
    // Owns one bounded concurrency limiter per normalized store name.
}
```

```csharp
public sealed class BlobUploadAdmissionLease : IAsyncDisposable
{
    public bool IsAcquired { get; }

    public IResultError Error { get; }

    public TimeSpan WaitDuration { get; }
}
```

The concrete implementation may use `System.Threading.RateLimiting.ConcurrencyLimiter` or an internal equivalent. It must provide:

* a hard active-permit limit;
* a hard waiting-queue limit;
* oldest-first processing;
* asynchronous waiting;
* cancellation of an individual waiter;
* reliable permit release;
* concurrency-safe counters.

The existing time-window `Common.Utilities.RateLimiter` is not suitable for this behavior because it limits operation frequency rather than simultaneous operations and bounded waiting.

### Lifetime And Store Isolation

Blob clients are scoped by default. Admission state must therefore not be stored only in a scoped behavior instance.

`AddBlobStorage` must register the admission coordinator as a singleton when the behavior is enabled. The behavior resolves that coordinator and passes its normalized store name on every acquisition.

Rules:

* all clients and scopes resolving the same named store share one limiter;
* different named stores use independent limiter state;
* disposal of one scoped client does not dispose shared limiter state;
* application shutdown disposes the coordinator and rejects or cancels remaining waiters;
* duplicate registration of the behavior in one builder flow must either be rejected or explicitly documented as layered independent limits; rejection is preferred.

### Admission Flow

`UploadConcurrencyBlobStoreClientBehavior.UploadAsync` must:

1. validate caller cancellation before waiting;
2. request a permit from the named-store coordinator;
3. wait no longer than `QueueWaitTimeout`;
4. return a typed overload failure if the queue is already full;
5. return a typed admission-timeout failure if its queue wait expires;
6. call the inner `UploadAsync` exactly once after admission;
7. release the permit in `finally`;
8. never read, seek, clone, buffer, rewind, or dispose `BlobUpload.Content`.

Queued requests must not create a provider scope, resolve an operation `DbContext`, begin a transaction, or consume a database connection.

All non-upload operations pass through without admission. Downloads, listing, properties, exists, and deletes must not consume upload permits.

### Bounded Queue Semantics

The queue limit bounds callers held inside the behavior. When all permits and queue positions are occupied, the next upload is rejected immediately.

The implementation must not add another unbounded waiter collection in front of the bounded limiter. In particular, wrapping a `SemaphoreSlim` with an unbounded set of awaiting callers is insufficient.

Oldest-first queue processing is required initially. Newest-first eviction and priority admission are out of scope.

The behavior does not copy the upload stream while queued. The caller remains responsible for keeping the stream alive until `UploadAsync` completes.

### Cancellation And Timeout

Caller cancellation while queued:

* removes the caller from the queue;
* does not call the inner client;
* does not consume or leak a permit;
* throws `OperationCanceledException` with normal .NET cancellation behavior.

Caller cancellation after admission is passed to the inner upload. The permit remains held until the inner upload has quiesced and returned or thrown.

Queue-wait timeout is an expected admission outcome and returns `BlobStoreUploadAdmissionTimeoutError`. It is distinct from:

* caller cancellation;
* `BlobStoreTimeoutError` produced by the general timeout behavior;
* provider command or connection timeout.

If the general timeout behavior is outside admission and its deadline expires first, its existing timeout semantics own the result.

### Result Errors

Add:

```csharp
public sealed class BlobStoreUploadOverloadedError(
    string storeName,
    int maxConcurrentUploads,
    int maxQueuedUploads)
    : ResultErrorBase(/* stable non-sensitive message */);
```

```csharp
public sealed class BlobStoreUploadAdmissionTimeoutError(
    string storeName,
    TimeSpan timeout)
    : ResultErrorBase(/* stable non-sensitive message */);
```

Rules:

* neither error includes container names, blob names, stream details, or property values;
* retry behavior treats both errors as non-transient by default;
* applications may retry at a higher workload-orchestration layer with their own bounded backoff;
* metrics distinguish queue-full rejection from queue-wait timeout.

## Entity Framework Chunk Flushing

### Options

Extend `BlobStoreOptions`:

```csharp
public int ChunkFlushCount { get; set; } = 4;

public long MaxPendingChunkBytes { get; set; } = ByteSize.Megabytes(16);
```

Rules:

* `ChunkFlushCount` must be greater than zero.
* `MaxPendingChunkBytes` must be greater than zero.
* both values are per active EF upload;
* a flush occurs when either threshold is reached;
* the final partial group is flushed when the stream ends;
* `ChunkFlushCount = 1` preserves the current per-chunk `SaveChangesAsync` cadence;
* a `MaxPendingChunkBytes` value smaller than `ChunkSize` is valid and effectively flushes each full chunk;
* these options do not affect Azure or in-memory providers;
* `ChunkSize` continues to control source reads and stored chunk-row size.

The default combination of four 4 MB chunks and a 16 MB pending-byte limit bounds tracked content near 16 MB per active upload, excluding EF and object overhead. Total process memory still depends on `MaxConcurrentUploads`.

### Write Algorithm

Replace the per-chunk save in `WriteChunksAsync` with:

1. rent one `ChunkSize` read buffer;
2. read one chunk;
3. enforce `MaxBlobSize` immediately after counting bytes;
4. append the read bytes to the incremental content hash;
5. create and add one `StorageBlobChunk`;
6. add the entity to a local pending list and increase pending-byte count;
7. when either threshold is met:
   * call `SaveChangesAsync`;
   * detach every entity in the pending list;
   * clear the list and reset pending bytes;
8. flush the final non-empty pending list after end of stream;
9. return the final hash and length;
10. return the rented read buffer in `finally`.

Suggested private helper:

```csharp
private static async Task FlushChunksAsync(
    TContext dbContext,
    List<StorageBlobChunk> pendingChunks,
    CancellationToken cancellationToken)
{
    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    foreach (var chunk in pendingChunks)
    {
        dbContext.Entry(chunk).State = EntityState.Detached;
    }

    pendingChunks.Clear();
}
```

The actual helper must clear state safely only after a successful save. Transaction rollback remains responsible for persisted chunk cleanup on relational providers.

### Transaction Semantics

Chunk flushing is not a public commit. For relational EF providers:

* all chunk flushes remain inside the existing upload transaction;
* metadata is finalized only after the complete stream is read and expected hash is verified;
* the transaction commits exactly once per blob;
* another context must not observe a partially uploaded replacement under normal transaction isolation;
* size, integrity, provider, or cancellation failures roll back all flushed chunk groups;
* each blob remains independently atomic.

This specification does not combine several blobs into one transaction. Doing so would lengthen locks, couple unrelated failures, and conflict with provider-neutral Azure semantics.

### Change Tracker And Memory

After each successful chunk flush, all chunks from that flush must be detached. The blob metadata entity remains tracked until final metadata save.

At no point should the EF context retain all chunks for a successfully progressing upload. Pending tracked chunk count and bytes must remain bounded by the configured thresholds plus at most one chunk required to cross the byte threshold.

### Relational Overwrite Snapshot

The existing provider loads all old chunks into `BlobSnapshot` before overwrite, including when a relational transaction is available. Relational rollback does not use that snapshot.

As part of this change:

* relational uploads with an active transaction must not materialize the old blob chunks solely for rollback;
* non-relational test/provider paths may keep snapshot restoration where transactions are unavailable;
* metadata needed for overwrite, such as original `CreatedAt`, may still be read with the target row;
* overwrite failure tests must continue proving that old metadata and chunks remain intact.

This prevents a large overwrite from temporarily materializing the entire previous blob in memory before new chunks are written.

### SaveChanges Expectations

For an upload producing `N` chunks, the chunk-write phase performs no more than:

```text
ceil(N / effective flush group)
```

`effective flush group` is bounded by both `ChunkFlushCount` and `MaxPendingChunkBytes`.

Placeholder-row creation, overwrite deletion fallback, lease fallback, and final metadata persistence may require additional `SaveChangesAsync` calls. Tests must distinguish these calls from chunk flush calls rather than asserting one total provider-wide count for every database provider.

## Interaction With Bulk Callers

This specification intentionally leaves `IBlobStoreClient` unchanged. Applications can process many blob sources with bounded `Parallel.ForEachAsync`, jobs, queue consumers, or message handlers.

When the optional behavior is enabled, even accidentally unbounded callers cannot cause more than the configured active and queued uploads per named store in one process.

The behavior improves overload safety; it does not make an unbounded producer free. Queue-full and queue-timeout Results must be handled explicitly by the caller or workload orchestrator.

## Observability

Add low-cardinality metrics per store:

* active uploads;
* queued uploads;
* admission wait duration;
* admitted upload count;
* queue-full rejection count;
* admission-timeout count;
* queued cancellation count;
* EF chunks written;
* EF chunk flush count;
* chunks per flush histogram;
* bytes per flush histogram.

Logging rules:

* use typed/source-generated logging for hot paths;
* do not log blob content, property values, raw continuation tokens, or stream details;
* do not use full blob names as metric dimensions;
* log store name, configured limits, queue outcome, wait duration, provider name, flush chunk count, and flush bytes where appropriate;
* normal admission and flush detail should be `Trace` or `Debug`;
* queue-full and admission-timeout outcomes should be `Warning`;
* application start should log enabled admission limits once per named store.

Diagnostics should expose current active and queued counts and configured limits without exposing queued upload keys.

## Implementation Map

Expected Application Storage changes:

* `Blobs/Behaviors/UploadConcurrencyBlobStoreClientBehavior.cs`
* `Blobs/Behaviors/UploadConcurrencyBlobStoreClientBehaviorOptions.cs`
* `Blobs/Behaviors/BlobUploadAdmissionCoordinator.cs`
* `Blobs/Behaviors/BlobStoreClientBehaviorServiceCollectionExtensions.cs`
* `Blobs/Behaviors/BlobStoreClientBehaviorTelemetry.cs`
* `Blobs/BlobStoreErrors.cs`
* `Blobs/Models/BlobStoreOptions.cs`
* `Blobs/Diagnostics/*` for admission snapshot data

Expected EF Infrastructure changes:

* `Storage/Blobs/EntityFrameworkBlobStoreProvider.cs`

Expected documentation changes during implementation:

* `docs/features-storage-blobs.md`
* `docs/specs/spec-application-storage-blobs.md` if the main design specification remains normative

No new project is required.

## Verification

### Behavior Unit Tests

Required tests:

* behavior is absent by default;
* at most `MaxConcurrentUploads` inner uploads execute simultaneously;
* the limit is shared across different DI scopes for the same named store;
* different named stores do not share permits;
* at most `MaxQueuedUploads` callers wait;
* an additional caller receives `BlobStoreUploadOverloadedError` without invoking the inner client;
* a queued caller is admitted in oldest-first order;
* a queued caller receives `BlobStoreUploadAdmissionTimeoutError` after its configured wait timeout;
* caller cancellation removes a queued waiter and throws `OperationCanceledException`;
* cancellation and inner exceptions do not leak permits;
* upload Result failures release permits;
* non-upload operations bypass admission;
* the behavior never disposes or reads a queued upload stream;
* invalid options fail during registration;
* retry does not retry admission errors by default;
* retry outside admission does not hold a permit during retry backoff;
* overall timeout outside admission includes queue wait.

Concurrency tests must use controlled task-completion sources or equivalent deterministic gates, not timing-only sleeps.

### EF Unit Tests

Required tests:

* multiple chunks are saved in one configured flush;
* flush occurs at `ChunkFlushCount`;
* flush occurs at `MaxPendingChunkBytes`;
* a final partial group is flushed;
* `ChunkFlushCount = 1` preserves per-chunk flushing;
* flushed chunk entities are detached;
* content order, length, and SHA-256 remain correct;
* non-seekable streams remain supported;
* size-limit failure after an earlier flush leaves no committed partial blob;
* expected-hash failure after several flushes leaves no committed partial blob;
* cancellation after several flushes rolls back;
* stream failure after several flushes rolls back;
* overwrite rollback preserves the previous blob;
* relational overwrite does not materialize old chunk content for a rollback snapshot;
* option validation rejects non-positive flush settings.

### Integration Tests

Run the existing EF provider contract against SQLite, SQL Server, and PostgreSQL where configured.

Add integration coverage proving:

* several chunk groups are flushed inside one transaction and become visible only after commit;
* rollback removes all previously flushed groups;
* simultaneous uploads to different keys respect database correctness;
* simultaneous uploads to the same key continue to use existing lease/conflict semantics;
* configured admission limits prevent provider concurrency from exceeding the limit.

### Performance Verification

Add a focused benchmark or diagnostic test fixture that records:

* total bytes;
* total chunks;
* chunk flush count;
* elapsed upload time;
* peak active uploads;
* admission wait distribution.

The correctness gate is deterministic reduction in chunk-phase saves. For `N` chunks with neither byte nor database-provider limits forcing a smaller group, chunk-phase `SaveChangesAsync` calls must be `ceil(N / ChunkFlushCount)` instead of `N`.

Absolute throughput targets are environment-specific and are not a merge gate.

## Acceptance Criteria

### Story 1: Bound Concurrent Blob Uploads

User story: As an application operator, I want optional bounded upload admission per blob store, so that bursts do not exhaust database and process resources.

Acceptance criteria:

1. Given the behavior is configured with four permits, when more than four uploads run concurrently, then at most four calls reach the inner upload client at one time.
2. Given callers resolve the same named store from different DI scopes, when they upload concurrently, then they share the same four permits.
3. Given the waiting queue is full, when another upload arrives, then it receives `BlobStoreUploadOverloadedError` before provider work starts.
4. Given a caller is waiting, when its cancellation token is canceled, then it leaves the queue without invoking the provider or leaking capacity.
5. Given the behavior is not registered, when uploads run, then no DevKit upload admission limit is applied.

### Story 2: Flush EF Chunks In Bounded Groups

User story: As an application operator, I want EF blob chunks persisted in bounded groups, so that large uploads require fewer database round trips without unbounded memory.

Acceptance criteria:

1. Given an upload has more chunks than `ChunkFlushCount`, when it is persisted, then the chunk phase calls `SaveChangesAsync` once per bounded flush group rather than once per chunk.
2. Given pending chunk bytes reach `MaxPendingChunkBytes`, when the next flush decision is made, then the pending group is saved and detached.
3. Given the stream ends with a partial group, when upload finalization runs, then that group is saved before metadata commit.
4. Given a failure occurs after one or more groups were flushed, when the operation completes, then no partial new content is committed.
5. Given a relational overwrite, when rollback protection is prepared, then old chunk content is not fully materialized solely for snapshot restoration.

### Story 3: Observe And Tune High-Volume Uploads

User story: As an application operator, I want admission and chunk-flush telemetry, so that I can tune throughput without diagnosing only connection-pool timeouts.

Acceptance criteria:

1. Given admission control is enabled, when uploads execute, then active count, queue depth, wait duration, rejection count, and timeout count are observable per store.
2. Given EF chunk flushing executes, when chunks are persisted, then flush count, chunks per flush, and bytes per flush are observable.
3. Given telemetry is emitted, when dimensions are inspected, then blob names and content are absent.
4. Given queue capacity is repeatedly exhausted, when logs and metrics are reviewed, then queue-full outcomes are distinguishable from provider failures and general timeouts.

## Definition Of Ready

Ready: Yes.

Ready reason:

* public behavior and provider option shapes are defined;
* lifetime, queue, cancellation, timeout, transaction, and error semantics are explicit;
* provider boundaries and non-goals are explicit;
* deterministic unit and integration acceptance criteria are defined;
* no unresolved decision blocks implementation.

Implementation should record the shared admission-controller and EF chunk-flush defaults in an ADR if they become conventions reused by other storage features.
