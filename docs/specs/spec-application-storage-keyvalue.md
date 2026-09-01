---
created: 2026-08-03
status: draft
---

# Design Specification: Key/Value Storage

> Provide Result-native, backend-neutral key/value storage with string, binary, and atomic counter values, optional local read acceleration, durable backends, and lightweight multi-node invalidation.

[TOC]

## Overview

Key/Value Storage is a focused storage feature for fast exact-key access to small to medium sized values and atomic numeric counters.

The feature captures the Redis-shaped storage use case that belongs in the devkit: fast exact-key access to small application-state values, atomic counters, expiration, backend-neutral persistence, optional local acceleration, and safe multi-node invalidation.

The feature is not a Redis-compatible server and does not duplicate existing queueing, messaging, jobs, durable workflow orchestration, or broker capabilities. It does not provide scripting, streams, pub/sub, clustering, or application-facing query capabilities. It provides the complete target design for a focused Result-native key/value and counter abstraction with pluggable backends.

The feature supports three storage shapes:

* Pure in-memory store.
* Persistent store.
* Locally accelerated persistent store.

Pure in-memory is a first-class store. It is local-only, non-durable, and non-shared across nodes. It is suitable for tests, development, ephemeral state, and single-process use cases.

Persistent stores place values in a durable backend and use that backend as the source of truth.

Locally accelerated persistent stores combine a durable backend with an optional per-node in-memory read layer. They are the production default for load-balanced applications that need fast reads and durable storage.

Because this feature is primarily a shared application-state store, production correctness across processes and load-balanced nodes is a core requirement. Entity Framework Core is the durable relational backend because EF Core can provide the required atomic value write plus change-log append in one database transaction. Azure Table Storage is the Azure cloud-native backend because it is lightweight, Azure-native, cost-conscious, and fits the explicit change-log polling model.

Multi-node local-state invalidation is based on a lightweight backend change log and polling worker. The existing message broker may be supported as an optional invalidation adapter, but it is not the default mechanism because full broker persistence, handler tracking, retries, and operational visibility are too heavy for invalidating a node-local read layer.

## Goals

The goals of this feature are:

* Provide a backend-neutral key/value storage abstraction.
* Keep the advanced client and operational APIs Result-native while preserving the simple provider's established signatures.
* Support string values, binary values, and numeric counters.
* Support generic typed value convenience operations that serialize through a per-client `ISerializer`.
* Default typed value serialization to `SystemTextJsonSerializer` while allowing explicit per-client replacement.
* Preserve value metadata such as content type, encoding, hash, size, version, timestamps, and expiry.
* Support exact-key `Get`, `Set`, `Delete`, and `Exists`.
* Support exact-key `Touch`, `Expire`, and `GetOrSet` convenience operations.
* Support exact-key atomic counter `Increment` and `Decrement` operations.
* Support atomic flags through `SetIfAbsent` and optimistic concurrency.
* Support operational maintenance for persistent stores through a separate privileged service.
* Support dashboard inspection of full keys, values, counters, and metadata when explicitly enabled.
* Support dashboard add, edit, counter adjustment, and delete operations against the persistent backing store.
* Support store names as first-class namespaces.
* Support tags/labels metadata for operational grouping and dashboard filtering.
* Treat missing and expired keys as `Result<T>.Failure(...)` on `Get`.
* Keep `Exists` as the boolean probing API.
* Support TTL/expiry.
* Support sliding expiration.
* Enforce expiry on reads.
* Physically clean up expired entries through a hosted `KeyValueStoreExpiryCleanupBackgroundService`.
* Reuse the DevKit `PeriodicBackgroundService` and `StorageRetentionOptions` scheduling conventions.
* Keep cleanup work bounded, cancellable, observable, and safe when several application nodes run the service.
* Propagate physical cleanup through the shared change log so every node, including the cleanup host, invalidates local state.
* Propagate creation and re-creation so cached not-found state on other nodes converges to existence.
* Support retention cleanup for expired entries.
* Support maximum value size validation.
* Support optional per-store quotas.
* Support key validation.
* Support backend-neutral checksum/hash verification.
* Support optional compression and value encryption through client behaviors.
* Provide an extensible key/value client behavior system consistent with other devkit storage features.
* Expose internal key/value operation flow through typed logging.
* Provide a built-in value encryption behavior based on `EncryptionHelper`.
* Rename the existing `ICacheProvider` abstraction to `IKeyValueStoreProvider` and back its default distributed implementation with `IKeyValueStoreClient`.
* Support optimistic concurrency through expected version checks.
* Support optional set-if-absent semantics.
* Support bounded, backend-native bulk value writes for adding many independent keys efficiently.
* Keep bulk input, backend batch size, and concurrent backend batches explicitly bounded.
* Return deterministic per-item results for bulk writes without promising cross-key atomicity.
* Preserve atomic value write plus change-log append for every successful item in a bulk write.
* Support local single-flight for duplicate concurrent read misses as core local-acceleration behavior.
* Support optional short-lived negative caching for repeated not-found reads.
* Provide a pure in-memory backend.
* Provide a durable persistence backend contract.
* Provide a locally accelerated persistent store for production multi-node applications.
* Provide Entity Framework Core as the durable relational persistent backend.
* Provide Azure Table Storage as the Azure cloud-native persistent backend.
* Make cross-process and cross-node operation a hard requirement for the production locally accelerated persistent store.
* Use write-through semantics for locally accelerated persistent writes.
* Require atomic value write plus change-log append for locally accelerated multi-node stores.
* Support eventual cross-node consistency by default.
* Support explicit fresh/backend reads when stronger read consistency is needed.
* Use backend change-log polling for lightweight cross-node invalidation.
* Support optional local key-change observation after change-log polling sees a change.
* Use a key/value-specific observer adapter over `SimpleNotifier` for in-process key-change fan-out when observation is enabled.
* Persist change-log checkpoints per store and node identity when supported.
* Support configurable node identity for production deployments.
* Use local TTL as a fallback when polling is delayed or invalidation is missed.
* Provide deterministic recovery when a node misses retained change-log history.
* Provide health checks for backend, change log, checkpoints, polling, and local-read health.
* Show polling lag and local-read health in the Razor dashboard.
* Keep message-broker invalidation as an optional adapter rather than the default invalidation mechanism.

## Non-Goals

This feature does not introduce:

* Redis-compatible protocol or command behavior.
* Pub/sub.
* Scripting.
* Streams.
* Sorted sets.
* Lists, sets, hashes, or other collection data structures.
* Distributed locks.
* Distributed transactions across keys.
* Multi-key atomic operations.
* All-or-nothing transactions across a complete bulk request.
* Application-facing querying.
* Application-facing listing.
* Application-facing prefix scans.
* Application-facing full-store scans.
* Secondary indexes.
* Search.
* Range reads.
* Append-only value mutation.
* Backend-specific public APIs.
* Maintaining separate cache-oriented and key/value-oriented names for the same simple provider abstraction.
* Authentication or authorization policy enforcement.
* Encryption key management or rotation orchestration.
* Backup, restore, or backend disaster-recovery orchestration.
* Message-broker based invalidation as the default invalidation mechanism.
* Durable business-event delivery for key changes.
* Exactly-once key-change handler execution.
* Cross-process observer dispatch beyond the backend change-log polling mechanism.
* Soft delete as a user-visible feature.
* Audit-log persistence for maintenance actions.

## Terminology

| Term | Meaning |
| --- | --- |
| Key | Logical string identifier for one value. |
| Value | Stored content represented as bytes with metadata. String values are encoded to bytes. |
| Typed value | Caller-provided CLR value serialized by the client into the ordinary binary value model. |
| Counter | Signed 64-bit numeric value addressed by one exact key and mutated through atomic increment/decrement operations. |
| Entry | Stored key, value, metadata, version, and expiry information. |
| Metadata | Backend-neutral information about an entry. |
| Tag | User-provided metadata label used for operational grouping and dashboard filtering. |
| Version | Backend-neutral optimistic concurrency token for an entry. |
| TTL | Time-to-live after which the entry is treated as expired. |
| Expiry | Absolute timestamp after which a key is treated as missing. |
| Key/value provider | Simple application-facing typed key/value abstraction, renamed from `ICacheProvider`. |
| In-memory store | Local store that keeps all entries in process memory only. |
| Persistent store | Store that places entries in a durable backend. |
| Locally accelerated persistent store | Persistent store with an optional per-node in-memory read layer. |
| Backend | Storage implementation used by the client and hidden from application code. |
| Change log | Backend-maintained ordered record of key changes used to invalidate node-local reads. |
| Checkpoint | Last processed change-log sequence for a store and node. |
| NodeId | Stable identifier for an application node/process. |
| NodeId provider | Reusable common abstraction that resolves a stable node identity for features that need node-local checkpointing or self-originated change filtering. |
| Fresh read | Read that verifies the local read layer by loading the backend. |
| Maintenance service | Privileged operational service that can inspect and mutate persisted key/value entries for dashboards and support tools. |
| Key-change observer | Local in-process handler that reacts when this node observes a backend key change. |
| Behavior | Client decorator that adds or extends cross-cutting behavior such as logging, metrics, compression, encryption, checksum verification, single-flight, or negative caching. |
| Transform behavior | Behavior that changes value bytes on write and reverses the transform on read, such as compression or encryption. |
| Bulk write | One bounded request that sets several independent exact keys and returns one result per input item. |
| Backend batch | One bounded group of bulk-write items sent to a persistence backend for an optimized write. |
| Bulk failure mode | Policy that either continues independent backend batches after item failures or stops scheduling new batches. |
| Bulk scheduler | Core process-wide coordinator that bounds active and queued backend batches per named store across all callers. |
| Result-native API | API that returns `Result` or `Result<T>` for expected outcomes and failures. |

## Design Principles

* The public API is backend-neutral.
* `IKeyValueStoreClient` and operational APIs are Result-native; `IKeyValueStoreProvider` preserves the existing simple typed contract.
* The store is exact-key oriented.
* Atomic counters are first-class exact-key primitives.
* Store names are first-class namespaces.
* Tags are metadata, not application query primitives.
* Missing keys are explicit failures on `Get`.
* `Exists` is the API for boolean existence probing.
* String values are convenience values over the binary storage model.
* Generic typed operations are convenience operations over the same binary storage model and do not create a separate backend path.
* Serialization is a client responsibility; persistence backends store only bytes and backend-neutral metadata.
* Backends should not leak implementation-specific concepts into the public client API.
* Operational maintenance APIs are separate from the application-facing client API.
* Maintenance operations work against the persistent backing store, not against per-node local caches.
* Pure in-memory is a valid store, not only a cache implementation.
* The persistent backend is the source of truth for persistent and locally accelerated persistent stores.
* Locally accelerated persistent stores are write-through.
* Locally accelerated persistent stores use local memory only as an optional acceleration layer.
* Locally accelerated persistent stores use eventual cross-node consistency by default.
* Stronger read consistency is opt-in through read options.
* Backend change-log polling is the default multi-node invalidation mechanism.
* Local key-change observation is built on top of change-log polling.
* Key-change observers are local process notifications, not durable business events.
* Message-broker invalidation is optional adapter behavior, not the default local-read invalidation model.
* Expiry correctness must not depend on background cleanup timing.
* Client behaviors are composable decorators around the key/value client.
* The behavior system is the primary extension point for cross-cutting client capabilities.
* Transform behaviors such as compression and encryption must be explicit and ordered.
* Value encryption is client-side behavior by default and should use existing `EncryptionHelper` primitives.
* Internal feature logging uses source-generated typed logging methods so operation flow is visible without allocating ad hoc log messages on hot paths.
* Expected validation, concurrency, and not-found failures are represented as typed Result errors.
* Cancellation remains normal .NET cancellation behavior.
* High-volume writes use an explicit core bulk API rather than an implicit background buffer around `SetAsync`.
* Bulk processing applies natural backpressure by reading and scheduling only bounded input and backend batches.
* A successful bulk item preserves the same validation, transform, quota, concurrency, persistence, local-read, and change-log semantics as `SetAsync`.
* Bulk result ordering follows input ordering even when backend batches execute concurrently.
* Backend batch atomicity is an implementation detail and is not exposed as cross-key transactional semantics.
* All bulk requests for one named store share one bounded core backend-batch scheduler across DI scopes.

## Naming Convention

The feature name is Key/Value Storage.

Type names may use `Store` when they represent the key/value abstraction itself. This keeps names such as `IKeyValueStoreProvider`, `IKeyValueStoreClient`, `IKeyValueStoreBackend`, `KeyValueStoreProvider`, and `KeyValueStoreOptions` aligned while preserving Key/Value Storage as the feature name.

Feature-level registration, documentation headings, dashboard labels, and authorization policy names may continue to use `KeyValueStorage` where they refer to the overall feature rather than one store contract.

## Relationship Between Provider, Client, And Backend

The existing `ICacheProvider` abstraction is renamed to `IKeyValueStoreProvider`. This removes the assumption that all values are disposable cache entries and makes the abstraction suitable for ordinary typed application state.

`IKeyValueStoreProvider` remains intentionally small:

* it stores typed objects through `Get`, `TryGet`, `Set`, and `Remove` operations;
* it supports sliding and absolute expiration;
* it retains `GetKeys` and `RemoveStartsWith` for compatibility with existing consumers;
* it does not expose Result-native failures, metadata, versions, hashes, tags, fresh reads, counters, optimistic concurrency, set-if-absent, local acceleration controls, health, quotas, or maintenance inspection.

```csharp
public interface IKeyValueStoreProvider
{
    T Get<T>(string key);

    Task<T> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default);

    bool TryGet<T>(string key, out T value);

    Task<bool> TryGetAsync<T>(
        string key,
        out T value,
        CancellationToken cancellationToken = default);

    IEnumerable<string> GetKeys();

    Task<IEnumerable<string>> GetKeysAsync(
        CancellationToken cancellationToken = default);

    void Remove(string key);

    Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);

    void RemoveStartsWith(string key);

    Task RemoveStartsWithAsync(
        string key,
        CancellationToken cancellationToken = default);

    void Set<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null);

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default);
}
```

`IKeyValueStoreClient` is the advanced Result-native API. Application code should use it when a use case needs explicit failures, counters, metadata, versions, consistency options, coordination flags, bulk results, or operational capabilities.

`IKeyValueStoreBackend` is the internal storage SPI used by client implementations. Application code must not resolve or depend on it.

```text
simple typed consumers
  -> IKeyValueStoreProvider
       -> IKeyValueStoreClient
            -> IKeyValueStoreBackend

advanced storage consumers
  -> IKeyValueStoreClient
       -> IKeyValueStoreBackend
```

The provider implementation is:

```csharp
public sealed class KeyValueStoreProvider : IKeyValueStoreProvider
{
    // Uses IKeyValueStoreClient for typed exact-key operations.
}
```

Provider rules:

* `Get`, `TryGet`, `Set`, and `Remove` use `IKeyValueStoreClient` exact-key operations.
* `Set` maps sliding and absolute expiration to `KeyValueWriteOptions`.
* `Remove` maps to `DeleteAsync` and preserves idempotent remove semantics.
* Typed values use the selected client's generic operations, so the provider does not add a second serialization layer.
* Provider and direct typed client operations share the named client's `KeyValueStoreOptions.Serialization` configuration.
* A provider registration should use a dedicated named store or reserved key prefix when broad key enumeration must be isolated from other application state.
* When a prefix is configured, every provider operation applies it internally and returned keys omit that internal prefix.
* The provider does not expose versions, tags, counters, maintenance inspection, change-log details, or fresh-read options.
* Provider logs must use typed logging and must not log value content.

`IKeyValueStoreProvider` retains `GetKeys` and `RemoveStartsWith`, while `IKeyValueStoreClient` intentionally does not expose listing or prefix scans. The provider implementation handles those methods without changing the advanced client contract.

Allowed strategies:

* use an internal provider-owned key index stored under reserved keys;
* use the maintenance backend internally when the application explicitly enables provider key enumeration;
* surface the established unsupported-operation behavior when enumeration is disabled.

Enumeration rules:

* the default distributed provider configuration supports `GetKeys` and `RemoveStartsWith` because they are part of the renamed interface contract;
* an internal key index is maintained by provider writes and deletes, tolerates stale entries, and verifies key existence before returning keys or deleting by prefix;
* prefix removal is eventually consistent across nodes and relies on exact-key deletion plus normal change-log invalidation;
* enumeration is scoped to the provider's configured namespace;
* provider internals do not add listing, querying, prefix scans, or full-store scans to `IKeyValueStoreClient`.

### Rename And Migration

The target API contains `IKeyValueStoreProvider`, not `ICacheProvider`. Implementation should perform a repository-wide rename of the interface, implementations, behaviors, constructor parameters, tests, and registration extensions.

Initial symbol mapping:

| Current symbol | Target symbol |
| --- | --- |
| `ICacheProvider` | `IKeyValueStoreProvider` |
| `InMemoryCacheProvider` | `InMemoryKeyValueStoreProvider` |
| `InMemoryCacheProviderConfiguration` | `InMemoryKeyValueStoreProviderConfiguration` |
| `LoggingCacheProviderBehavior` | `LoggingKeyValueStoreProviderBehavior` |
| `DocumentStoreCacheProvider` | `DocumentStoreKeyValueStoreProvider` |
| `DocumentStoreCacheProviderConfiguration` | `DocumentStoreKeyValueStoreProviderConfiguration` |

Types whose actual responsibility is caching, such as requester query caching or document-client cache behaviors, may retain `Cache` in their behavior name. The rename applies to the general storage abstraction and its implementations; it does not obscure a genuinely cache-specific policy.

If a compatibility release is required, `ICacheProvider` may remain for one release as an obsolete forwarding interface or adapter. New code, documentation, and registrations use only `IKeyValueStoreProvider`; the compatibility type must not become a second independently configured abstraction.

## Backend Shapes

### In-Memory Store

The in-memory backend stores values in process memory.

Capabilities:

* Exact-key get, set, delete, and exists.
* Bounded bulk set under one in-process synchronization boundary.
* Atomic increment and decrement within the process.
* String and binary values.
* TTL and expiry.
* Opportunistic removal of expired entries during reads and writes.
* Maximum value size validation.
* Optimistic concurrency within the process.
* Set-if-absent within the process.

Limitations:

* Not durable.
* Not shared across app nodes.
* Not suitable as the source of truth for load-balanced production deployments.
* Change-log polling is not required.
* Hosted physical cleanup is not required; the backend reports `SupportsExpiryCleanup = false`.

### Persistent Store

The persistent store writes values to a durable backend and reads directly from it.

Capabilities:

* Exact-key get, set, delete, and exists.
* Bounded bulk set when the backend exposes an optimized write-many path.
* Atomic increment and decrement when supported by the backend.
* String and binary values.
* TTL and expiry.
* Maximum value size validation.
* Optimistic concurrency when supported by the backend.
* Set-if-absent when supported by the backend.

Limitations:

* Every read hits the backend.
* No local read acceleration.
* Change-log polling is not required unless local acceleration is enabled.

### Locally Accelerated Persistent Store

The locally accelerated persistent store combines:

* Shared persistent backend.
* Per-node in-memory read layer.
* Backend change log.
* Polling invalidation worker.
* Local TTL fallback.

This is the default production storage shape for multi-node applications.

Capabilities:

* Fast local reads for resident keys.
* Durable write-through persistence.
* Bounded bulk write-through using backend-native batches.
* Eventual cross-node consistency.
* Explicit fresh/backend reads.
* Atomic value write plus change-log append.
* Atomic counter update plus change-log append.
* Per-node change-log checkpointing.
* Local-state invalidation through eviction.
* Recovery after node restarts.
* Recovery after missed change-log history.

Backend requirement:

* A backend used by `LocallyAcceleratedKeyValueStoreBackend` must support atomic value write, counter update, touch/expire, and delete plus change-log append.

Backends that cannot provide this capability may still support direct persistent storage or locally accelerated single-node scenarios, but they are not valid for locally accelerated multi-node use.

### Entity Framework Core Backend

Entity Framework Core is the durable relational backend.

The EF Core backend must support:

* Persistent key/value entries.
* Bounded bulk upsert of independent keys.
* One set-based read of existing rows per EF backend batch.
* One transaction and one primary `SaveChangesAsync` call per successful EF backend batch.
* Atomic value write plus change-log append in one database transaction.
* Atomic delete plus change-log append in one database transaction.
* Atomic touch/expire plus change-log append in one database transaction.
* Atomic increment/decrement plus change-log append in one database transaction.
* Optimistic concurrency through a backend version or row-version column.
* Set-if-absent semantics.
* Atomic counter semantics.
* Store names as namespaces.
* Tags and metadata persistence.
* Expiry filtering.
* Hosted periodic cleanup of expired entries through the shared key/value cleanup service.
* Change-log paging by monotonically increasing sequence.
* Per-store/per-node checkpoint persistence.
* Maintenance listing, value inspection, and counter inspection.
* Health checks for backend, change log, checkpoint, and polling lag.

The EF Core backend should expose a context contract instead of requiring a concrete devkit-owned DbContext type. The consuming application DbContext opts in by implementing this contract and exposing the required DbSet properties.

Suggested context contract:

```csharp
public interface IKeyValueStoreDbContext
{
    DbSet<KeyValueStoreEntryEntity> KeyValueStoreEntries { get; }

    DbSet<KeyValueStoreChangeLogEntity> KeyValueStoreChangeLog { get; }

    DbSet<KeyValueStoreCheckpointEntity> KeyValueStoreCheckpoints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

Suggested EF entities:

* `KeyValueStoreEntryEntity`
* `KeyValueStoreChangeLogEntity`
* `KeyValueStoreCheckpointEntity`

Entity mapping convention:

* Configure EF entities with data annotations on the entity classes themselves, following existing infrastructure entities such as `BrokerMessage`.
* Use `[Table]` on each entity to declare the physical table name.
* Use `[Index]` attributes on each entity for required indexes and uniqueness constraints.
* Use `[Key]`, `[Required]`, `[MaxLength]`, `[Column]`, `[ConcurrencyCheck]`, and `[NotMapped]` where appropriate instead of external `IEntityTypeConfiguration<T>` classes for the default mapping.
* Keep the DbContext contract focused on `DbSet` exposure and `SaveChangesAsync`; it should not require consumers to copy fluent mapping into `OnModelCreating`.
* JSON-backed metadata, tags, transform metadata, or operational collections may use a `[NotMapped]` strongly typed property plus a `[Column]` serialized property when that matches existing entity patterns.
* Database-specific overrides may be used when a database engine requires them, but the default EF backend design should be annotation-first.

Required indexes:

* Unique index on `StoreName` and `Key`.
* Index on `StoreName` and `ExpiresAt`.
* Index on `StoreName` and tag storage if tag filtering is implemented relationally.
* Unique or ordered sequence index for change-log entries.
* Unique index on checkpoint `StoreName` and `NodeId`.

The exact entity names, table names, indexes, column lengths, concurrency fields, and JSON-backed helper properties should follow existing EF storage conventions during implementation, with the mapping declared on the entities through annotations by default.

### Azure Table Storage Backend

Azure Table Storage is the Azure cloud-native backend.

The Azure Table Storage backend exists for applications that want a lightweight managed Azure store without adopting Cosmos DB as the default cloud backend.

The backend must support:

* Persistent key/value entries.
* Bounded bulk upsert partitioned into same-partition table transactions.
* Atomic value write plus change-log append using same-partition batch transactions.
* Atomic delete plus change-log append using same-partition batch transactions.
* Atomic touch/expire plus change-log append using same-partition batch transactions.
* Atomic increment/decrement plus change-log append using same-partition batch transactions.
* Optimistic concurrency using Table Storage ETags.
* Set-if-absent semantics using insert semantics.
* Atomic counter semantics using backend transactions or ETag-based concurrency.
* Store names as namespaces.
* Tags and metadata persistence.
* App-managed expiry filtering.
* App-hosted periodic cleanup of expired entries through the shared key/value cleanup service.
* Change-log paging through dedicated change-log entities.
* Per-store/per-node checkpoint persistence.
* Maintenance listing, value inspection, and counter inspection.
* Health checks for table access, change-log reads, checkpoint reads/writes, and polling lag.
* Backend-managed initialization of required tables and table entities within an existing storage account.
* Startup validation for required storage account permissions and table accessibility.

Storage account and table management:

* The consuming application provides an existing Azure Storage account connection or client configuration.
* The backend owns all key/value table setup inside that storage account.
* Required tables must be created by backend initialization when they do not exist and table creation is enabled.
* Backend initialization must be idempotent and safe to run from multiple app nodes during deployment or scale-out.
* The backend should expose configuration for table names or table name prefixes, but should provide sensible defaults.
* The backend must not require operators to manually pre-create key/value tables for normal use.
* If table creation is disabled by configuration, startup must validate that all required tables exist and fail with a clear Result/configuration error when they do not.
* The backend must validate that configured credentials can read, write, update, delete, and create tables when table creation is enabled.
* The backend must validate that configured credentials can read, write, update, and delete required table entities when table creation is disabled.
* Initialization should not create or manage the storage account itself.
* Initialization should not alter unrelated tables in the storage account.
* Table lifecycle cleanup, destructive table deletion, and storage account provisioning remain operational responsibilities outside normal backend startup.

Partitioning is the key design concern.

Table Storage batch transactions require all entities in the batch to share the same partition key. To preserve the required atomic value write plus change-log append, the value entity and its change-log entity for a write must live in the same partition.

Suggested partitioning shape:

```text
PartitionKey = "{storeName}:{shard}"
Value RowKey = "v:{escapedKey}"
Change RowKey = "c:{sequence-or-ticks}:{escapedKey}"
Checkpoint PartitionKey = "{storeName}:checkpoint"
Checkpoint RowKey = "{nodeId}:{shard}"
```

Rules:

* The shard must be derived deterministically from the key.
* Value and change-log entities for the same key operation must share the same `PartitionKey`.
* Change-log polling is performed per store and shard.
* Checkpoints are tracked per store, node, and shard.
* The number of shards is configurable per store.
* Increasing shard count is a migration concern and should not happen implicitly.
* Azure Table Storage is eventually consumed through polling; no native change feed is required.

Azure Table Storage limitations:

* No native change feed is assumed.
* No native global ordering across all shards is required.
* Cross-key atomicity is not supported.
* Large value support is bounded by Table Storage entity limits and should use Blob Storage or another backend if values exceed practical table limits.
* Expiry cleanup is app-managed, not a replacement for backend-native TTL.

Cosmos DB is a separate backend option for workloads that need native change feed, richer global distribution, or higher-scale partitioning. It is not the default Azure backend for this feature.

## Public Client API

The public client exposes an exact-key API for values and counters.

```csharp
public interface IKeyValueStoreClient
{
    Task<Result<KeyValueEntry>> GetAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<T>> GetAsync<T>(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueEntry<T>>> GetEntryAsync<T>(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> SetAsync(
        string key,
        KeyValueValue value,
        KeyValueWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> SetAsync<T>(
        string key,
        T value,
        KeyValueWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBulkWriteResult>> SetManyAsync(
        IReadOnlyCollection<KeyValueSetItem> items,
        KeyValueBulkWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBulkWriteResult>> SetManyAsync<T>(
        IReadOnlyCollection<KeyValueSetItem<T>> items,
        KeyValueBulkWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> TouchAsync(
        string key,
        KeyValueTouchOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> ExpireAsync(
        string key,
        KeyValueExpireOptions options,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueEntry>> GetOrSetAsync(
        string key,
        Func<CancellationToken, Task<Result<KeyValueValue>>> valueFactory,
        KeyValueWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<Result<T>>> valueFactory,
        KeyValueWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueCounterEntry>> GetCounterAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueCounterResult>> IncrementAsync(
        string key,
        long delta = 1,
        KeyValueCounterOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueCounterResult>> DecrementAsync(
        string key,
        long delta = 1,
        KeyValueCounterOptions options = null,
        CancellationToken cancellationToken = default);
}
```

The application-facing client does not expose listing, querying, prefix scans, or full-store scans.

Operational listing and value inspection are provided only through the maintenance service described below.

`SetManyAsync` is the high-volume write entry point. `SetAsync` remains the preferred API for one key and must not buffer writes in the hope that later calls can be combined.

The non-generic methods are the canonical binary API. Generic methods serialize or deserialize through the configured client serializer and delegate to the same binary operation pipeline.

## Named Client Factory

Runtime code resolves a configured client by store name through a backend-neutral factory.

```csharp
public interface IKeyValueStoreClientFactory
{
    IKeyValueStoreClient CreateClient(string name);

    IReadOnlyCollection<KeyValueStoreClientRegistration> GetRegistrations();
}
```

```csharp
public sealed record KeyValueStoreClientRegistration
{
    public string Name { get; init; }

    public string BackendName { get; init; }

    public KeyValueStoreBackendCapabilities Capabilities { get; init; }

    public ServiceLifetime Lifetime { get; init; }
}
```

Rules:

* `CreateClient` resolves only previously registered names.
* a null, empty, or whitespace name fails with `ArgumentException`.
* an unknown name fails with `InvalidOperationException` because it is an application configuration error rather than a storage-operation Result.
* names use the same normalization and uniqueness rules as registration and bulk-scheduler isolation.
* the factory uses keyed DI lookup and does not construct, wrap, or independently cache clients.
* `GetRegistrations` returns diagnostic descriptors without exposing backend instances, credentials, connection strings, or configuration secrets.
* factory lifetime and keyed client lifetimes follow the established Blob Storage factory pattern.
* `IKeyValueStoreClientFactory` is the preferred application-facing named-client resolution API.
* direct keyed DI may be used by DevKit infrastructure or applications with a compile-time fixed store name, but it is not required in application-layer constructors.

## Value Model

Values support string and binary usage while keeping the storage core binary.

```csharp
public sealed record KeyValueValue
{
    public byte[] Content { get; init; }

    public string ContentType { get; init; }

    public string Encoding { get; init; }

    public static KeyValueValue FromString(
        string value,
        Encoding encoding = null,
        string contentType = "text/plain");

    public static KeyValueValue FromBytes(
        byte[] value,
        string contentType = "application/octet-stream");
}
```

String values are encoded to bytes and stored with encoding metadata. Backends do not need a separate string storage path.

### Typed Value Serialization

Generic client operations provide a typed convenience API over the binary value model:

```csharp
public sealed class KeyValueSerializationOptions
{
    public ISerializer Serializer { get; set; } = new SystemTextJsonSerializer();

    public string ContentType { get; set; } = "application/json";

    public string Encoding { get; set; } = "utf-8";

    public bool RequireContentTypeMatchOnRead { get; set; } = true;
}
```

Rules:

* serialization is configured per named client through `KeyValueStoreOptions.Serialization`;
* the default serializer is `SystemTextJsonSerializer`;
* a custom `ISerializer` instance may be supplied in the client options;
* the configured serializer must be safe for concurrent use because one client can serve concurrent operations;
* backends store only bytes and metadata and never resolve serializers or CLR types;
* typed writes serialize first, create a `KeyValueValue` with the configured content type and encoding, and then use the normal binary write pipeline;
* typed reads run the normal binary read and reverse-transform pipeline before deserializing the resulting bytes;
* `GetAsync<T>` returns only the typed value, while `GetEntryAsync<T>` returns the typed value together with its key and metadata;
* no CLR type name or discriminator is added by the key/value feature; the caller supplies `T`, and polymorphism is governed only by the configured serializer;
* a null typed write value, a null deserialization result, serializer exception, or incompatible content type returns `KeyValueStoreSerializationError`;
* when `RequireContentTypeMatchOnRead` is enabled, comparison ignores case and content-type parameters but requires the stored media type to equal the configured media type;
* cancellation is checked before and after synchronous serializer work; the existing `ISerializer` contract does not make serialization itself cancellable;
* the serialized byte array is subject to the same maximum value size, transforms, hash, quota, and persistence rules as a raw value.

With the default serializer, `SetAsync<string>` writes a JSON string and `SetAsync<byte[]>` writes the serializer's JSON representation. Use `KeyValueValue.FromString` for unquoted plain text and `KeyValueValue.FromBytes` for opaque binary data.

## Entry Model

```csharp
public sealed record KeyValueEntry
{
    public string Key { get; init; }

    public KeyValueValue Value { get; init; }

    public KeyValueMetadata Metadata { get; init; }
}
```

```csharp
public sealed record KeyValueEntry<T>
{
    public string Key { get; init; }

    public T Value { get; init; }

    public KeyValueMetadata Metadata { get; init; }
}
```

```csharp
public sealed record KeyValueMetadata
{
    public string ContentType { get; init; }

    public string Encoding { get; init; }

    public long Size { get; init; }

    public string Hash { get; init; }

    public string Version { get; init; }

    public KeyValueEntryKind Kind { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; }

    public IReadOnlyList<KeyValueTransformMetadata> Transforms { get; init; }
}
```

```csharp
public enum KeyValueEntryKind
{
    Value,
    Counter
}

```

```csharp
public sealed record KeyValueTransformMetadata
{
    public string Name { get; init; }

    public string Algorithm { get; init; }

    public string KeyId { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; }
}
```

Hash format:

* Backend-neutral SHA-256 hash.
* Calculated over the stored byte content after configured transforms have been applied.
* Encoded consistently as lowercase hexadecimal.

## Tags And Labels

Entries may carry tags for operational grouping.

Rules:

* Tags are optional metadata.
* Tags are string key/value pairs.
* Tag names and values must be length-limited.
* Tags must not contain value content or sensitive secrets.
* Tags may be shown and filtered in the maintenance dashboard.
* Tags do not create application-facing query capabilities.
* Backends may store tags in the same metadata structure as other entry metadata.

## Read Options

```csharp
public sealed class KeyValueReadOptions
{
    public KeyValueReadConsistency Consistency { get; set; } = KeyValueReadConsistency.Default;

    public bool AllowNegativeReadCaching { get; set; }
}
```

```csharp
public enum KeyValueReadConsistency
{
    Default,
    Fresh,
    BackendOnly
}
```

`Default` uses store-default behavior. For locally accelerated persistent stores, this means the local read layer first, then a backend read on miss or expiry.

`Fresh` reads the persistent backend and refreshes the local read layer when acceleration is enabled.

`BackendOnly` reads the persistent backend and strictly avoids the local read layer. It does not refresh local state after a successful backend read.

If a caller wants a backend read that refreshes local state, it must use `Fresh`.

Pure in-memory stores treat `Fresh` and `BackendOnly` as equivalent to `Default`.

## Write Options

```csharp
public sealed class KeyValueWriteOptions
{
    public TimeSpan? TimeToLive { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string ExpectedVersion { get; set; }

    public bool SetIfAbsent { get; set; }

    public string ExpectedHash { get; set; }

    public bool SlidingExpiration { get; set; }

    public IReadOnlyDictionary<string, string> Tags { get; set; }
}
```

Rules:

* `TimeToLive` and `ExpiresAt` must not both be set unless the implementation defines an explicit precedence rule.
* `ExpectedVersion` enables optimistic concurrency.
* `SetIfAbsent` succeeds only when the key does not currently exist or is expired.
* `ExpectedHash` validates caller-provided content expectations before storing.
* `SlidingExpiration` extends expiry on reads and touches using the configured TTL.
* `Tags` replace the entry's operational tags on successful write.
* Invalid option combinations fail with validation errors.

## Touch And Expire Options

```csharp
public sealed class KeyValueTouchOptions
{
    public TimeSpan? TimeToLive { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string ExpectedVersion { get; set; }
}
```

```csharp
public sealed class KeyValueExpireOptions
{
    public DateTimeOffset ExpiresAt { get; set; }

    public string ExpectedVersion { get; set; }
}
```

`Touch` updates expiry without rewriting value content.

`Expire` sets an explicit expiry timestamp without rewriting value content.

Both operations are exact-key operations and must append change-log entries for locally accelerated persistent stores.

## Write Result

```csharp
public sealed record KeyValueWriteResult
{
    public string Key { get; init; }

    public string Version { get; init; }

    public string Hash { get; init; }

    public long Size { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool Created { get; init; }
}
```

`Created` is `true` when the operation created a new non-expired key and `false` when it replaced an existing non-expired key.

## Bulk Value Writes

Bulk value writes are a core key/value capability because values are bounded byte arrays and persistence backends can efficiently write several independent entries in one backend call. Unlike Blob Storage upload streams, key/value items can be validated, transformed, partitioned, and replayed without retaining caller-owned live streams.

Bulk writes apply only to ordinary values. Counter increments/decrements, deletes, touches, and expires remain exact-key operations in this version.

### Bulk Models

```csharp
public sealed record KeyValueSetItem
{
    public string Key { get; init; }

    public KeyValueValue Value { get; init; }

    public KeyValueWriteOptions Options { get; init; }
}
```

```csharp
public sealed record KeyValueSetItem<T>
{
    public string Key { get; init; }

    public T Value { get; init; }

    public KeyValueWriteOptions Options { get; init; }
}
```

```csharp
public sealed class KeyValueBulkWriteOptions
{
    public int BatchSize { get; set; } = 100;

    public int MaxConcurrentBatches { get; set; } = 1;

    public KeyValueBulkWriteFailureMode FailureMode { get; set; } =
        KeyValueBulkWriteFailureMode.Continue;
}
```

```csharp
public enum KeyValueBulkWriteFailureMode
{
    Continue,
    StopScheduling
}
```

```csharp
public sealed record KeyValueBulkWriteItemResult
{
    public int Index { get; init; }

    public string Key { get; init; }

    public Result<KeyValueWriteResult> Result { get; init; }
}
```

```csharp
public sealed record KeyValueBulkWriteResult
{
    public int RequestedCount { get; init; }

    public int SucceededCount { get; init; }

    public int FailedCount { get; init; }

    public int SkippedCount { get; init; }

    public IReadOnlyList<KeyValueBulkWriteItemResult> Items { get; init; }
}
```

Rules:

* `items` must not be null or empty.
* item count must not exceed `KeyValueStoreOptions.MaxBulkWriteItems`.
* `BatchSize` must be greater than zero and no greater than `MaxBulkWriteBatchSize`.
* `MaxConcurrentBatches` must be greater than zero and no greater than `MaxBulkWriteConcurrency`.
* every key must be unique within one `SetManyAsync` request, using the same key comparison rules as the configured store.
* duplicate keys fail the complete request with `KeyValueStoreValidationError` before backend I/O because execution order and optimistic-concurrency intent would otherwise be ambiguous.
* a generic request is homogeneous in `T`; heterogeneous payload types use separate generic requests or the raw `KeyValueSetItem` API.
* generic items are serialized into raw `KeyValueSetItem` values before backend batching; serialization failures are reported in the corresponding item Result.
* the serialization facade retains original indexes and merges serialization failures with raw bulk results so filtering valid items never renumbers the public result.
* `Items` is ordered by original input index regardless of backend partitioning or concurrent batch completion.
* each item uses the same key, value, option, size, hash, transform, expiry, concurrency, quota, and set-if-absent validation as `SetAsync`.
* the outer `Result<KeyValueBulkWriteResult>` fails only when the request itself is invalid, cancellation is thrown, or a fatal orchestration failure prevents a meaningful item result.
* expected per-item validation, serialization, conflict, concurrency, quota, hash, transform, and backend failures are stored in the corresponding item Result.
* a successful item is durably committed before it is reported as successful.
* a bulk request does not guarantee atomicity across keys, backend batches, partitions, or shards.
* backends may commit a group atomically, but callers must not rely on that stronger implementation detail.

### Failure Modes

`Continue`:

* validates all request-level constraints before backend work;
* processes all backend batches;
* records independent item failures;
* does not roll back successful items because another item or backend batch failed.

`StopScheduling`:

* records per-item validation, serialization, or transform failures before backend scheduling;
* schedules only the valid input prefix before the first such per-item failure and marks later items as skipped;
* stops scheduling new backend batches after the first completed batch containing a failure;
* allows already running backend batches to quiesce;
* marks items in unscheduled batches with `KeyValueStoreBulkWriteSkippedError`;
* does not roll back already successful items.

`StopScheduling` is deliberately not named `FailFast`: bounded concurrent batches may already be running when a failure is observed.

### Bounded Orchestration

The core client must:

1. validate request-level limits and duplicate keys;
2. serialize generic items, then apply backend-neutral validation and registered value transforms per item;
3. partition valid items into batches no larger than `BatchSize`;
4. submit backend batches through the shared named-store bulk scheduler;
5. execute no more than `MaxConcurrentBatches` backend calls concurrently for this request and no more than the store-wide `MaxBulkWriteConcurrency` across all requests;
6. allow no more than `MaxQueuedBulkWriteBatches` to wait across all requests for the named store;
7. return a per-item overload failure without backend I/O when a backend batch cannot enter the bounded scheduler;
8. stop enumerating or scheduling new work when the bounded in-flight set is full;
9. preserve item indexes while backends reorder by shard or partition;
10. update the writing node's local cache only for successful persisted items;
11. return ordered item results.

No unbounded `Task.WhenAll` over all items is permitted. At most `MaxConcurrentBatches` backend tasks and their bounded item collections may be in flight.

`MaxConcurrentBatches = 1` is the conservative default. It still reduces round trips through backend-native batching and preserves simple ordering. Higher values are opt-in for independent-key ingestion after measuring backend capacity.

The core bulk scheduler is not an optional client behavior. It exists only on the explicit bulk path, is shared across scoped clients through singleton state keyed by normalized store name, uses oldest-first asynchronous admission, and releases capacity in `finally`. It does not delay or combine ordinary `SetAsync` calls.

When the store-wide queue is full:

* `Continue` records `KeyValueStoreBulkWriteOverloadedError` for every item in the rejected backend batch and continues according to available bounded capacity;
* `StopScheduling` records the overload failure for that batch and marks later unscheduled items as skipped;
* no persistence backend method is called for the rejected batch.

Caller cancellation removes a waiting backend batch, does not leak scheduler capacity, and preserves normal `OperationCanceledException` behavior.

A backend batch waits no longer than `BulkWriteQueueWaitTimeout`. When that deadline expires, its items receive `KeyValueStoreBulkWriteAdmissionTimeoutError`; no persistence backend method is called for that batch. A general timeout behavior outside the core operation may impose a shorter overall request deadline.

### Streaming Convenience

For datasets larger than `MaxBulkWriteItems`, provide an extension that consumes a source incrementally and yields one bounded bulk result at a time:

```csharp
public static IAsyncEnumerable<Result<KeyValueBulkWriteResult>> SetManyBatchesAsync(
    this IKeyValueStoreClient client,
    IAsyncEnumerable<KeyValueSetItem> items,
    KeyValueBulkWriteOptions options = null,
    CancellationToken cancellationToken = default);

public static IAsyncEnumerable<Result<KeyValueBulkWriteResult>> SetManyBatchesAsync<T>(
    this IKeyValueStoreClient client,
    IAsyncEnumerable<KeyValueSetItem<T>> items,
    KeyValueBulkWriteOptions options = null,
    CancellationToken cancellationToken = default);
```

The extension:

* buffers at most `min(MaxBulkWriteItems, BatchSize * MaxConcurrentBatches)` items for one outer request;
* calls the core `SetManyAsync` operation for each bounded collection;
* awaits or yields a batch result before reading an unbounded amount of additional input;
* never accumulates all item results in memory;
* preserves cancellation;
* defaults to sequential outer requests;
* does not guarantee duplicate-key detection across different yielded bulk requests;
* treats a later occurrence of a key in a later request as a normal later write.

The bounded outer size permits the requested number of backend batches to run concurrently without accumulating the complete source.

### Behavior Pipeline

`IKeyValueStoreClient` behaviors must expose and forward both `SetManyAsync` overloads. The client serialization facade converts a generic request to raw items before invoking the configured behavior pipeline.

Rules:

* logging, metrics, timeout, and retry behaviors operate at the bulk request boundary;
* typed serialization occurs at the client boundary before content-transform behaviors on writes;
* typed deserialization occurs after content-transform behaviors have restored the original bytes on reads;
* compression, encryption, and checksum behaviors transform and validate each item before backend batching;
* serialization and transform failures affect only the corresponding item in `Continue` mode;
* retry behavior retries only failed replayable backend batches, not already successful batches;
* key/value content uses owned byte arrays, so backend batch retries do not have Blob Storage's non-seekable stream restriction;
* custom behaviors must preserve item indexes and ordered results;
* behavior implementations must not expand bounded input into unbounded tasks.

### Locally Accelerated Store Semantics

For a locally accelerated persistent store:

* the persistent backend remains the source of truth;
* each successful item persists its entry and change-log record atomically;
* the writing node updates or evicts its local cache only after backend commit;
* failed and skipped items do not update local cache;
* remote nodes invalidate successful keys through normal change-log polling;
* one change-log entry remains required per successfully changed key;
* the poller may process and evict a page of changed keys as a local batch, but must not load their replacement values.

## Counter Model

Counters are signed 64-bit numeric values stored under exact keys.

```csharp
public sealed record KeyValueCounterEntry
{
    public string Key { get; init; }

    public long Value { get; init; }

    public KeyValueMetadata Metadata { get; init; }
}
```

```csharp
public sealed class KeyValueCounterOptions
{
    public TimeSpan? TimeToLive { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string ExpectedVersion { get; set; }

    public bool CreateIfMissing { get; set; } = true;

    public long InitialValue { get; set; }

    public IReadOnlyDictionary<string, string> Tags { get; set; }
}
```

```csharp
public sealed record KeyValueCounterResult
{
    public string Key { get; init; }

    public long Value { get; init; }

    public string Version { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool Created { get; init; }
}
```

Rules:

* `Increment` and `Decrement` are atomic per key.
* `GetCounter` reads a counter without mutating it.
* `Get` against a counter key fails with a type mismatch error.
* `GetCounter` against an ordinary value key fails with a type mismatch error.
* `Exists`, `Delete`, `Touch`, and `Expire` apply to both values and counters.
* Missing counters are created from `InitialValue` when `CreateIfMissing` is true.
* Existing non-counter values fail with a type mismatch error.
* Counter overflow fails with a typed counter overflow error.
* Counter operations may set or preserve expiry according to `KeyValueCounterOptions`.
* Persistent and locally accelerated counter operations must update the counter and append the change-log entry atomically.
* Locally accelerated stores must not satisfy counter writes from local memory only.
* Counter keys share the same namespace as ordinary value keys.

## Backend API

The backend SPI mirrors the client's raw exact-key behavior and exposes capabilities.

```csharp
public interface IKeyValueStoreBackend
{
    KeyValueStoreBackendCapabilities Capabilities { get; }

    Task<Result<KeyValueEntry>> GetAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> SetAsync(
        string key,
        KeyValueValue value,
        KeyValueWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBulkWriteResult>> SetManyAsync(
        IReadOnlyCollection<KeyValueSetItem> items,
        KeyValueBulkWriteOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> TouchAsync(
        string key,
        KeyValueTouchOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> ExpireAsync(
        string key,
        KeyValueExpireOptions options,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueCounterEntry>> GetCounterAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueCounterResult>> IncrementAsync(
        string key,
        long delta = 1,
        KeyValueCounterOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueCounterResult>> DecrementAsync(
        string key,
        long delta = 1,
        KeyValueCounterOptions options = null,
        CancellationToken cancellationToken = default);
}
```

Backend contracts intentionally remain non-generic. Typed serialization belongs to `IKeyValueStoreClient`; after conversion, backends receive the same `KeyValueValue` and `KeyValueSetItem` models used by raw callers.

```csharp
public sealed record KeyValueStoreBackendCapabilities
{
    public bool IsDurable { get; init; }

    public bool IsSharedAcrossNodes { get; init; }

    public bool SupportsLocalAcceleration { get; init; }

    public bool SupportsAtomicWriteWithChangeLog { get; init; }

    public bool SupportsBulkWrites { get; init; }

    public bool SupportsOptimisticConcurrency { get; init; }

    public bool SupportsSetIfAbsent { get; init; }

    public bool SupportsAtomicCounters { get; init; }

    public bool SupportsTouch { get; init; }

    public bool SupportsSlidingExpiration { get; init; }

    public bool SupportsTags { get; init; }

    public bool SupportsExpiryCleanup { get; init; }

    public bool SupportsMaintenance { get; init; }

    public bool SupportsHealthCheck { get; init; }
}
```

## Client Behaviors

Key/Value Storage must support a composable client behavior system.

Behaviors are decorators around `IKeyValueStoreClient`. They add cross-cutting capabilities without changing backend contracts or leaking backend-specific concerns into application code.

The behavior model should follow the surrounding storage features:

* Behaviors are registered through the key/value storage builder.
* Behaviors wrap the client in registration order.
* Behaviors can be added by type or factory.
* Behaviors can use dependency injection.
* Behaviors remain backend-neutral.

Suggested registration shape:

```csharp
services.AddKeyValueStorage(options =>
    options.WithRetention(retention =>
    {
        retention.Enabled = true;
        retention.StartupDelay = TimeSpan.FromSeconds(15);
        retention.SweepInterval = TimeSpan.FromMinutes(30);
        retention.BatchSize = 500;
        retention.MaxBatchesPerStore = 4;
        retention.BatchDelay = TimeSpan.FromMilliseconds(100);
        retention.StopTimeout = TimeSpan.FromSeconds(10);
    }))
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithChecksumVerificationBehavior()
    .WithCompressionBehavior()
    .WithEncryptionBehavior()
    .WithNegativeReadBehavior()
    .WithEntityFrameworkClient<AppDbContext>("default", options =>
    {
        options.StoreName = "default";
        options.NodeId = configuration["KeyValue:NodeId"];
    });
```

The first registered behavior is the outermost decorator, matching Blob Storage behavior registration.

The named client returned to callers adds one built-in serialization facade outside the configured behavior chain:

```text
application
  -> TypedKeyValueStoreClientFacade
       -> configured behaviors in registration order
            -> core backend client
```

Raw operations pass through the facade unchanged. Generic writes are serialized once and then enter every configured behavior as raw values; generic reads complete the raw read and reverse-transform behavior chain before the facade deserializes them. “Outermost behavior” therefore means outermost among configured behaviors, not outside the built-in facade.

Suggested behavior base type:

```csharp
public abstract class KeyValueStoreClientBehaviorBase(
    IKeyValueStoreClient inner,
    string storeName) : IKeyValueStoreClient
{
    protected IKeyValueStoreClient Inner { get; } = inner;

    protected string StoreName { get; } = storeName;
}
```

Built-in behaviors:

* Logging behavior.
* Metrics behavior.
* Timeout behavior.
* Retry behavior.
* Checksum verification behavior.
* GZip compression behavior backed by `CompressionHelper`.
* Encryption behavior.
* Negative-read behavior.

Behavior rules:

* Behaviors must implement and forward generic interface members, even though normal named-client resolution converts them at the built-in serialization facade.
* Compression and encryption are opt-in.
* The built-in compression behavior uses `CompressionHelper` GZip byte/stream APIs.
* Compression is disabled by default and should support a configurable minimum payload size.
* Compression and encryption must record enough metadata to read values back correctly.
* Compression and encryption must not change public key semantics.
* When compression and encryption are both enabled, values are compressed before they are encrypted.
* Checksum verification must validate stored bytes according to the configured transform order.
* Local single-flight is core local-acceleration behavior, enabled by default, and scoped per node and per key.
* Local single-flight prevents duplicate concurrent backend loads on the same node and is not enabled through an optional client behavior.
* Negative caching stores short-lived not-found results only.
* Negative caching is optional and disabled by default.
* Negative-read entries must be evicted by local writes and change-log polling.
* Behavior ordering must be explicit because transform behaviors can affect hashes, sizes, and metadata.
* Third-party and application-specific behaviors must be able to wrap all client operations, including counter operations.
* Behaviors that only apply to value content must pass counter operations through unchanged or fail with an unsupported-feature error when explicitly configured to handle counters.
* The built-in encryption behavior uses `EncryptionHelper.AesCbcPkcs7Algorithm`.
* The built-in encryption behavior encrypts `KeyValueValue.Content` bytes and stores transform metadata including algorithm and key id.
* The encrypted byte payload may use the existing `EncryptionHelper` format where the initialization vector is prepended to ciphertext.
* The built-in encryption behavior resolves key ids through a caller-provided resolver.
* The default resolver may derive the key id from the store name.
* Encryption key resolution, storage, rotation, and tenant policy remain outside the backend contract.
* The optional logging behavior logs the outer client call boundary.
* Internal typed logging logs feature internals such as backend selection, local-read decisions, persistence operations, change-log polling, checkpoint updates, cleanup, maintenance operations, and recovery decisions.

## Convenience Operations

### Counters

`Increment` and `Decrement` mutate one exact-key numeric counter atomically.

Rules:

* Counter operations are writes and must participate in validation, quota, expiry, versioning, health, logging, and metrics.
* Counter operations append change-log entries for locally accelerated persistent stores.
* Counter operations evict or update local entries on the writing node according to backend policy.
* Counter operations from another node must evict stale local state through change-log polling.
* Counter operations do not use compression or value encryption behaviors by default because atomic numeric mutation requires backend-visible numeric state.

### GetOrSet

`GetOrSet` reads an exact key and stores a generated value when the key is missing or expired.

Rules:

* `GetOrSet` must return the existing non-expired entry when present.
* The value factory runs only on miss or expired entry.
* The value factory returns `Result<KeyValueValue>`.
* Failed value factories must not write a value.
* Locally accelerated `GetOrSet` must use local single-flight to prevent duplicate factories on the same node.
* `GetOrSet` is not globally single-flight across nodes.
* Concurrent cross-node `GetOrSet` calls must rely on `SetIfAbsent` or `ExpectedVersion` semantics for correctness.

### Touch And Expire

`Touch` and `Expire` update expiry metadata without rewriting value content.

Rules:

* `Touch` may use the default TTL, supplied TTL, or supplied absolute expiry.
* `Expire` sets an explicit absolute expiry.
* Both operations fail with not-found when the key is missing or already expired.
* Both operations support expected-version checks.
* Both operations append change-log entries for locally accelerated persistent stores.
* Both operations update local cache metadata on the writing node.

## Persistence Backend Contract

The locally accelerated persistent store depends on a lower-level persistence backend contract.

For production multi-node local acceleration, the persistence backend must use storage shared by all app nodes and must provide transactional change-log writes. EF Core satisfies this requirement for relational storage.

```csharp
public interface IKeyValueStorePersistenceBackend
{
    KeyValueStorePersistenceBackendCapabilities Capabilities { get; }

    Task<Result<KeyValueBackendEntry>> ReadAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendWriteResult>> WriteAsync(
        KeyValueBackendWrite write,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendBulkWriteResult>> WriteManyAsync(
        IReadOnlyCollection<KeyValueBackendWrite> writes,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(
        KeyValueBackendDelete delete,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendWriteResult>> TouchAsync(
        KeyValueBackendTouch touch,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendWriteResult>> ExpireAsync(
        KeyValueBackendExpire expire,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueChangeLogPage>> ReadChangesAsync(
        KeyValueChangeLogQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueChangeLogCheckpoint>> ReadCheckpointAsync(
        string storeName,
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<Result> SaveCheckpointAsync(
        KeyValueChangeLogCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendCounterEntry>> ReadCounterAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendCounterResult>> IncrementAsync(
        KeyValueBackendCounterOperation operation,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendCounterResult>> DecrementAsync(
        KeyValueBackendCounterOperation operation,
        CancellationToken cancellationToken = default);
}
```

`KeyValueStorePersistenceBackendCapabilities` must include `SupportsBulkWrites`. Locally accelerated and direct persistent stores use it for diagnostics and to select the optimized `WriteManyAsync` path. A fallback backend may report `false` and execute bounded individual writes without changing public results.

The application-facing backend API does not expose listing values or listing keys. `ReadChangesAsync` lists change-log entries only for invalidation.

Operational listing and value inspection are exposed through a separate maintenance backend contract.

Delete operations return whether a non-expired value or counter existed before deletion. Missing and already-expired keys return `Result.Success(false)` unless an expected-version or backend-specific concurrency option requires a failure.

### Persistence Bulk Contract

`WriteManyAsync` receives one already validated and transformed backend batch. It is lower level than the public `SetManyAsync` orchestration.

Suggested result:

```csharp
public sealed record KeyValueBackendBulkWriteItemResult
{
    public int Index { get; init; }

    public string Key { get; init; }

    public Result<KeyValueBackendWriteResult> Result { get; init; }
}
```

```csharp
public sealed record KeyValueBackendBulkWriteResult
{
    public IReadOnlyList<KeyValueBackendBulkWriteItemResult> Items { get; init; }
}
```

Persistence rules:

* every input write retains its original public-request index;
* persistence backends may reorder writes internally by table partition or relational query shape;
* results map back to original indexes;
* every successful write atomically changes the entry and appends its change-log row when change logging is required;
* the contract does not promise atomicity across input writes;
* a backend that cannot optimize a batch may execute bounded individual writes, but must report `SupportsBulkWrites = false` so diagnostics disclose the fallback;
* the core client must not call an unbounded individual-write fallback concurrently;
* backend batch failures return per-item backend failures for every item whose outcome was rolled back or could not be established;
* backends must not report an item as successful until its state and required change-log entry are committed.

### Entity Framework Bulk Write Strategy

For one EF backend batch:

1. validate the bounded batch and begin one transaction;
2. load existing rows for all distinct keys using one set-based query scoped by `StoreName`;
3. evaluate expiry, `ExpectedVersion`, `SetIfAbsent`, type, hash, size, and quota conditions;
4. create or update valid `KeyValueStoreEntryEntity` instances;
5. add one `KeyValueStoreChangeLogEntity` for every valid changed item;
6. call `SaveChangesAsync` once for entry changes and change-log additions;
7. commit once;
8. map generated versions and sequences back to ordered item results.

Additional rules:

* invalid items discovered before persistence may be excluded in `Continue` mode while valid items in the same backend batch proceed;
* a `DbUpdateConcurrencyException`, constraint failure, or backend failure that rolls back the EF transaction marks every attempted item in that EF transaction as failed;
* implementation must not silently retry an entire transaction after some results were exposed;
* generated change-log sequence ordering within a batch must be stable enough for polling but need not match public input order;
* SQL parameter limits must be respected by enforcing `MaxBulkWriteBatchSize`;
* the EF backend must not issue one existing-row query or one `SaveChangesAsync` call per item;
* store quota enforcement that requires an exact aggregate must occur in the same transaction or fail conservatively rather than oversubscribe configured quota.

Although one EF backend batch is transactionally atomic as an implementation consequence, public callers must rely only on per-item outcomes and must not treat a backend batch as a distributed transaction contract.

### In-Memory Bulk Write Strategy

The in-memory backend should:

* validate and transform before entering the store lock where possible;
* acquire its store synchronization boundary once per backend batch;
* apply valid writes and create ordered results while holding that boundary;
* preserve exact-key concurrency and expiry rules;
* avoid one lock acquisition per item.

### Azure Table Bulk Write Strategy

Azure Table Storage must:

* derive the existing deterministic shard/partition for every key;
* group writes by partition;
* keep each value entity and its change-log entity in the same table transaction;
* respect the Azure Table transaction action limit, including both value and change-log actions;
* use at most 50 changed keys in a same-partition transaction when each key requires two actions;
* bound concurrent partition transactions by the effective `MaxConcurrentBatches`;
* map ETag, conflict, and transaction failures back to item results;
* never treat transactions across partitions as atomic.

Backend initialization should expose or calculate an effective maximum batch size that is no greater than backend limits.

## Maintenance Service

The maintenance service is a privileged operational service used by Razor dashboard pages and support tooling.

It is not the application-facing key/value client. It exists so operators can inspect and repair persistent key/value state.

Capabilities:

* View persisted keys.
* View full persisted values when inspection is enabled.
* View metadata, expiry, hash, version, and timestamps.
* View persisted counter values and metadata.
* Add new entries.
* Edit existing entries.
* Delete any entry.
* Page through persisted entries for a store.
* Read values directly from the persistent backing store.
* Mutate values and counters through the persistent backing store.
* Append change-log entries for maintenance writes, counter changes, and deletes so locally accelerated nodes invalidate stale entries.

Maintenance operations must not read from or write only to a local in-memory cache. For locally accelerated persistent stores, the persistent backing store remains the source of truth.

The maintenance service should be protocol-neutral and live close to the Application storage feature. Razor dashboard pages should call this service rather than duplicating backend access logic.

```csharp
public interface IKeyValueStoreMaintenanceService
{
    Task<Result<KeyValueMaintenancePage>> ListPageAsync(
        KeyValueMaintenanceQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueMaintenanceEntry>> GetAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> SetAsync(
        KeyValueMaintenanceWrite write,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(
        KeyValueMaintenanceDelete delete,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record KeyValueMaintenanceQuery
{
    public string StoreName { get; init; }

    public int Take { get; init; }

    public string ContinuationToken { get; init; }

    public bool IncludeValues { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; }
}
```

```csharp
public sealed record KeyValueMaintenanceEntry
{
    public string Key { get; init; }

    public KeyValueValue Value { get; init; }

    public KeyValueMetadata Metadata { get; init; }

    public bool IsExpired { get; init; }
}
```

```csharp
public sealed record KeyValueMaintenancePage
{
    public IReadOnlyList<KeyValueMaintenanceEntry> Items { get; init; }

    public string ContinuationToken { get; init; }
}
```

```csharp
public sealed record KeyValueMaintenanceWrite
{
    public string StoreName { get; init; }

    public string Key { get; init; }

    public KeyValueValue Value { get; init; }

    public KeyValueWriteOptions Options { get; init; }

    public string Operator { get; init; }
}
```

```csharp
public sealed record KeyValueMaintenanceDelete
{
    public string StoreName { get; init; }

    public string Key { get; init; }

    public string ExpectedVersion { get; init; }

    public string Operator { get; init; }
}
```

Rules:

* Maintenance listing must be paged.
* Maintenance listing must use opaque continuation tokens.
* Maintenance listing may return keys and metadata without values.
* Maintenance listing may filter by tags when the backend supports tag filtering.
* Full value inspection must be supported, but should be explicitly controlled by dashboard/options configuration.
* Maintenance `Set` uses the same validation, size, hash, expiry, and concurrency rules as normal writes.
* Maintenance `Delete` can delete any persisted entry, subject to optional expected-version checks.
* Maintenance writes, counter changes, and deletes must append change-log entries when used with locally accelerated persistent stores.
* Maintenance operations should capture operator identity when the presentation surface can provide it.
* Maintenance operations do not capture an operator reason.
* Maintenance services should log the operation metadata but must not log value content.

## Maintenance Backend Contract

Persistence backends that support operational maintenance expose a separate maintenance backend contract.

```csharp
public interface IKeyValueStoreMaintenanceBackend
{
    Task<Result<KeyValueMaintenancePage>> ListPageAsync(
        KeyValueMaintenanceQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueMaintenanceEntry>> ReadMaintenanceEntryAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> WriteMaintenanceEntryAsync(
        KeyValueMaintenanceWrite write,
        string originNodeId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteMaintenanceEntryAsync(
        KeyValueMaintenanceDelete delete,
        string originNodeId,
        CancellationToken cancellationToken = default);
}
```

Maintenance writes, counter changes, and deletes use the same atomicity requirement as locally accelerated persistent writes:

* write/delete persistent entry or update persistent counter
* append change-log entry
* commit both together when the backend supports multi-node local acceleration

Backends that cannot safely list or inspect values may report `SupportsMaintenance = false`.

## Change Log

Locally accelerated multi-node stores require a backend change log.

This is not optional for production locally accelerated persistent stores. Without a shared change log, local caches on different app nodes cannot converge reliably.

Each successful state-changing operation appends a change-log entry:

* `Set`
* every successful item in `SetMany` as operation `Set`
* `Increment`
* `Decrement`
* `Touch`
* `Expire`
* `Delete`
* Expiry cleanup when it physically removes backend data.

```csharp
public sealed record KeyValueChangeLogEntry
{
    public long Sequence { get; init; }

    public string StoreName { get; init; }

    public string Key { get; init; }

    public KeyValueChangeOperation Operation { get; init; }

    public string Version { get; init; }

    public string OriginNodeId { get; init; }

    public KeyValueChangeOriginKind OriginKind { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}
```

```csharp
public enum KeyValueChangeOperation
{
    Set,
    Delete,
    Increment,
    Decrement,
    Touch,
    Expire
}
```

```csharp
public enum KeyValueChangeOriginKind
{
    ClientMutation,
    Maintenance,
    ExpiryCleanup
}
```

Rules:

* Change-log sequence values are monotonically increasing within a backend.
* Locally accelerated persistent stores require atomic state change plus change-log append.
* Change-log entries do not include stored value content.
* Invalidation handlers evict local cache entries rather than replicating value data.
* Invalidation applies to positive values, counters, and negative not-found/existence entries for the changed key.
* Client writes use `ClientMutation`; dashboard or maintenance writes use `Maintenance`; physical expiry removal uses `ExpiryCleanup`.
* A cleanup change uses a cleanup-worker identity in `OriginNodeId`, not the application node identity, so the node running cleanup processes the invalidation as well.
* Polling uses `OriginKind` as the authoritative self-suppression discriminator and must not infer cleanup origin from an identifier prefix.
* Duplicate change-log processing is safe.
* Out-of-order processing must not corrupt local state; backends should use sequence order.

## Change-Log Polling

Each locally accelerated persistent store instance runs a polling worker.

The worker:

1. Resolves the configured `NodeId`.
2. Loads the last checkpoint for the store and node identity.
3. Reads change-log entries after the checkpoint sequence.
4. Suppresses a self-originated `ClientMutation` only when local state already reflects the committed version or deletion.
5. Processes `Maintenance` and `ExpiryCleanup` entries on every node, including the node where that operation ran.
6. Advances the key's local invalidation generation and evicts all positive, counter, and negative local state for the key.
7. Prevents backend reads started before that invalidation from repopulating stale local state.
8. Publishes an optional local key-change notification after eviction.
9. Saves the checkpoint after processing entries.
10. Repeats after the configured polling interval.

Default behavior:

* Cross-node consistency is eventual.
* Default polling interval is approximately two seconds.
* Polling interval is configurable.
* Local TTL is a separate safety net.
* A successful `Set` or re-creation invalidates negative existence state on other nodes; the next `Get` or `Exists` reloads the backend.
* A physical cleanup invalidates resident positive state on every node; the next read observes not-found unless a newer value was written.

The polling worker should be scoped per configured store/client so that stores can use different backends, polling intervals, TTLs, and node identities.

## Local Key-Change Observation

Key/Value Storage may expose local key-change observation for code that wants to react when the current node observes a key change.

This is similar in spirit to Redis keyspace notifications, but it is intentionally local and lightweight. The backend change log remains the cross-node propagation source. A key/value-specific observer adapter owns the public registration and notification contract, and `SimpleNotifier` is used only as the internal in-process fan-out mechanism after a node processes change-log entries.

Flow:

```text
Node A writes key
  -> persistent backend write
  -> change-log append

Node B polling worker
  -> reads change-log entry
  -> evicts local cache entry
  -> publishes local SimpleNotifier notification
  -> local handlers run
```

Notification shape:

```csharp
public sealed record KeyValueChangedNotification(
    string StoreName,
    string Key,
    KeyValueChangeOperation Operation,
    string Version,
    DateTimeOffset ChangedAt) : ISimpleNotification;
```

Rules:

* Key-change observation is optional.
* Public observer registration uses a storage-specific adapter rather than exposing `SimpleNotifier` directly.
* Notifications are published after local cache eviction.
* Notifications do not include value content.
* Notifications are local to the current process.
* Handlers are allowed to be delayed by the polling interval.
* Handlers must tolerate duplicate notifications.
* Handlers may miss individual notifications if a node was offline beyond change-log retention.
* Missed change-log recovery clears cache but does not replay every omitted observer notification.
* Handlers must not be used for critical exactly-once workflows.
* Critical business workflows must use message broker, outbox, jobs, or another durable workflow mechanism.

Suggested registration shape:

```csharp
services.AddKeyValueStorage()
    .WithEntityFrameworkClient<AppDbContext>("default", options =>
    {
        options.StoreName = "default";
        options.NodeId = configuration["KeyValue:NodeId"];
        options.EnableChangeNotifications = true;
    });
```

Change notifications require a locally accelerated persistent client because they are driven by shared change-log polling.

## Missed Change-Log Recovery

Backends may retain change-log entries for a bounded time.

If a node checkpoint is older than the retained change-log history:

1. The node clears its entire local cache for the store.
2. The node moves its checkpoint to the current backend high watermark.
3. The node continues polling from the new checkpoint.

This prevents stale local entries from surviving when selective eviction is no longer reliable.

The event should be observable through logs and metrics because it means the node missed its normal invalidation window.

## Node Identity

`NodeId` identifies an application node for checkpointing and self-originated change filtering.

Node identity should be resolved through a reusable common `INodeIdProvider` abstraction in `Common.Utilities` rather than a key/value-only helper.

Production distributed rule:

* Production locally accelerated persistent stores require a stable configured `NodeId` or an explicit opt-in to a built-in `INodeIdProvider`.
* Startup must fail for production locally accelerated persistent stores when node identity stability is not explicit.

Built-in fallback:

* Development, test, in-memory, and explicitly single-node deployments may use a machine or hostname-derived provider such as `MachineNameNodeIdProvider`.
* A built-in provider must be opt-in for production distributed deployments because hostnames can be unstable in containers, app-service slots, scale sets, and other ephemeral hosting models.

Risks:

* Duplicate node identities can cause nodes to ignore changes that did not originate from themselves.
* Unstable node identities can cause unnecessary replay or checkpoint proliferation.

Configuration should make these risks visible in production environments.

## Expiry Semantics

An entry is expired when its `ExpiresAt` is not null and is less than or equal to the current clock time.

Read behavior:

* `Get` treats expired entries as missing.
* `Exists` returns `false` for expired entries.
* Locally accelerated stores evict expired local entries before returning.

Cleanup behavior:

* `KeyValueStoreExpiryCleanupBackgroundService` physically removes expired entries after their configured retention window.
* Correctness does not depend on cleanup timing.
* Every winning physical removal appends an `Expire` change-log entry when multi-node invalidation is enabled.

## Expiry Cleanup Background Service

Physical cleanup runs through one hosted `KeyValueStoreExpiryCleanupBackgroundService` per process. The service derives from the shared `PeriodicBackgroundService`, matching Blob and Document Storage retention workers.

```csharp
public sealed class KeyValueStoreExpiryCleanupBackgroundService
    : PeriodicBackgroundService
{
    public Task<Result<KeyValueStoreExpiryCleanupSummary>> SweepOnceAsync(
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record KeyValueStoreExpiryCleanupSummary
{
    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }

    public int EligibleStoreCount { get; init; }

    public int SuccessfulStoreCount { get; init; }

    public int FailedStoreCount { get; init; }

    public int DeletedCount { get; init; }

    public int SkippedCount { get; init; }

    public int BatchCount { get; init; }

    public bool HasMore { get; init; }

    public IReadOnlyList<string> FailedStoreNames { get; init; }
}
```

The service is orchestration only. A cleanup-capable backend implements:

```csharp
public interface IKeyValueStoreExpiryCleanupBackend
{
    Task<Result<KeyValueStoreExpiryCleanupResult>> SweepExpiredAsync(
        KeyValueStoreExpiryCleanupRequest request,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record KeyValueStoreExpiryCleanupRequest
{
    public string StoreName { get; init; }

    public DateTimeOffset ExpiredOnOrBefore { get; init; }

    public int BatchSize { get; init; }

    public int MaxBatches { get; init; }

    public TimeSpan BatchDelay { get; init; }
}
```

```csharp
public sealed record KeyValueStoreExpiryCleanupResult
{
    public string StoreName { get; init; }

    public int DeletedCount { get; init; }

    public int SkippedCount { get; init; }

    public int BatchCount { get; init; }

    public bool HasMore { get; init; }
}
```

Scheduling and lifecycle:

* the worker waits for `IHostApplicationLifetime.ApplicationStarted`;
* it honors the configured startup delay and then runs at `StorageRetentionOptions.SweepInterval`;
* `PeriodicBackgroundService` guarantees one in-process iteration at a time;
* `SweepOnceAsync` uses the same asynchronous gate as scheduled execution so manual diagnostics cannot overlap a scheduled sweep;
* the host stopping token flows through backend calls and inter-batch delays;
* host cancellation is normal completion;
* shutdown waits no longer than `StorageRetentionOptions.StopTimeout`;
* non-cancellation exceptions are logged and the service retries on the next interval instead of terminating the host.

Sweep orchestration:

1. enumerate immutable named-client registrations in deterministic name order;
2. skip in-memory, disabled, or unsupported stores;
3. create and dispose one asynchronous DI scope per store;
4. resolve the keyed `IKeyValueStoreBackend` and require `IKeyValueStoreExpiryCleanupBackend`;
5. calculate `ExpiredOnOrBefore` as `TimeProvider.GetUtcNow() - KeyValueStoreOptions.ExpiredEntryRetention`;
6. execute at most `MaxBatchesPerStore` batches of at most `BatchSize` entries;
7. apply the cancellable `BatchDelay` between backend batches;
8. record a per-store outcome and continue with later stores after an expected Result failure;
9. publish one aggregate sweep result and update diagnostics.

Backend rules:

* the backend selects entries through an expiry index or equivalent bounded query and must not scan an unbounded store into memory;
* physical deletion is conditional on the entry still being expired and retaining the version observed by the sweep;
* a concurrent refresh, replacement, touch, or delete causes the cleanup item to be skipped rather than deleting newer state;
* each successful physical deletion and required `Expire` change-log append commit atomically;
* cleanup is idempotent and safe when several application nodes sweep the same backend;
* no distributed leader is required; conditional deletion ensures only one node wins a physical removal and appends its change-log entry;
* EF uses an operation-owned scope, context, bounded query, and transaction per backend batch;
* Azure Table Storage uses bounded partition transactions and ETag conditions;
* a backend reports `SupportsExpiryCleanup = false` when it cannot implement these guarantees.

Failure and fairness rules:

* one store failure does not prevent later registered stores from being swept;
* expected backend failures are captured in the aggregate Result, the persisted in-memory diagnostics snapshot, and typed logs;
* `KeyValueStoreExpiryCleanupError` carries the aggregate summary when one or more stores fail, so a manual caller can inspect partial progress without individual keys or values;
* an unexpected exception at the scheduled boundary is logged without value content and does not create a tight retry loop;
* `BatchSize`, `MaxBatchesPerStore`, and `BatchDelay` bound database pressure and prevent one store from monopolizing an iteration;
* `HasMore = true` defers remaining eligible entries to a later scheduled sweep rather than bypassing configured bounds.

The background service does not determine logical expiry. Reads continue treating an entry as absent immediately at `ExpiresAt`; `ExpiredEntryRetention` only delays physical deletion.

## Cross-Node Expiry And Local-State Convergence

Expiry cleanup and local read acceleration must converge across all application nodes that use the same named persistent store.

Physical cleanup flow:

```text
Node A cleanup worker
  -> conditionally deletes the expired backend version
  -> atomically appends a change-log entry with
     OriginKind = ExpiryCleanup and Operation = Expire

Every node polling that store, including Node A
  -> advances the key's invalidation generation
  -> evicts positive, counter, and negative local state
  -> prevents an older in-flight read from repopulating that state

Next Get or Exists
  -> reads the backend
  -> returns not-found unless a newer version has been created
```

Creation and re-creation flow:

```text
Node A Set or SetMany
  -> commits the value and a change-log entry with
     OriginKind = ClientMutation and Operation = Set atomically

Node B polling worker
  -> evicts any cached not-found/existence result for the key

Next Get or Exists on Node B
  -> reads the backend
  -> observes the newly created value
```

Rules:

* local caches are invalidated across nodes; values are not proactively copied into every node's memory;
* a cached positive entry uses the earlier of its local read TTL and stored `ExpiresAt` as its effective local expiry;
* a node must never return a locally cached entry after the stored `ExpiresAt`, even when change-log polling or cleanup is delayed;
* every successful cleanup deletion and its `Expire` change-log entry commit atomically, so deletion cannot become invisible to other nodes;
* every successful creation or re-creation and its `Set` change-log entry commit atomically, so cached not-found state can converge to existence;
* cleanup-originated entries are never discarded by client self-origin filtering;
* repeated cleanup or invalidation delivery is idempotent;
* a delayed cleanup entry may evict a newer local entry, but it must never delete the newer backend version; the following read reloads that version;
* the backend change-log order, per-node checkpoint, and missed-history full-cache clear provide eventual convergence after polling delays and node restarts;
* `LocalReadTtl` and `NegativeReadTtl` bound stale local state if polling is unavailable;
* application nodes must maintain operationally synchronized clocks because logical TTL evaluation uses `TimeProvider.GetUtcNow()`.

## Consistency Semantics

Default consistency:

* Eventual across nodes.
* Read-your-writes for the same store instance after a successful local write.
* Other nodes observe changes after polling processes the backend change log or after the applicable positive or negative local TTL expires.
* Other nodes observe a newly created key after its `Set` invalidation removes any cached not-found state and a subsequent read reloads the backend.
* Other nodes stop serving an expired key based on stored `ExpiresAt`; physical cleanup invalidation subsequently removes remaining local state.
* Production locally accelerated persistent stores must be validated in a multi-process or multi-store-instance scenario.

Fresh reads:

* A caller may request a fresh/backend read through `KeyValueReadOptions`.
* Fresh reads read the persistent backend and refresh local state when applicable.

Write consistency:

* Locally accelerated persistent `Set` returns success only after the backend write and change-log append both succeed.
* Locally accelerated persistent `Delete` returns success only after the backend delete and change-log append both succeed.

Concurrency:

* `ExpectedVersion` rejects writes when the current non-expired backend version does not match.
* `SetIfAbsent` rejects writes when the key currently exists and is not expired.
* Concurrency failures return typed Result errors.

## Local Read Layer Semantics

The optional local read layer belongs to the store instance and is implemented with an in-memory cache.

Local cache entries must carry:

* Key.
* Value.
* Metadata.
* Version.
* Expiry.
* Local cache insertion/update timestamp.
* An internal per-key invalidation generation captured when the entry was loaded.

The local cache must support:

* Exact-key lookup.
* Exact-key upsert.
* Exact-key eviction.
* Full clear for missed change-log recovery.
* TTL-based expiry.
* Optional size/count limits.
* Positive, counter, and negative-state eviction through one key-level invalidation operation.

Local cache eviction from change-log polling removes the entry only. It does not load the replacement value.

A backend read captures the key's invalidation generation before I/O. It may populate a positive or negative local entry only if that generation is unchanged when the read completes. If the generation changed, the client must re-evaluate or retry the read rather than allowing an older in-flight result to recreate stale local state.

## Configuration

```csharp
public sealed class KeyValueStorageOptions
{
    public StorageRetentionOptions Retention { get; } = new();

    public KeyValueStorageOptions WithRetention(
        Action<StorageRetentionOptions> configure);
}
```

`StorageRetentionOptions` is the existing shared DevKit model used by Blob and Document Storage. Its `Enabled`, `StartupDelay`, `SweepInterval`, `BatchSize`, `MaxBatchesPerStore`, `BatchDelay`, and `StopTimeout` properties configure the hosted cleanup worker consistently across storage features.

```csharp
public sealed class KeyValueStoreOptions
{
    public string StoreName { get; set; }

    public string NodeId { get; set; }

    public KeyValueSerializationOptions Serialization { get; } = new();

    public long MaxValueSizeInBytes { get; set; } = KeyValueStoreDefaults.MaxValueSizeInBytes;

    public long? MaxStoreSizeInBytes { get; set; }

    public long? MaxEntryCount { get; set; }

    public int MaxBulkWriteItems { get; set; } = 1000;

    public int MaxBulkWriteBatchSize { get; set; } = 250;

    public int MaxBulkWriteConcurrency { get; set; } = 4;

    public int MaxQueuedBulkWriteBatches { get; set; } = 16;

    public TimeSpan BulkWriteQueueWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan? DefaultTimeToLive { get; set; }

    public TimeSpan LocalReadTtl { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan NegativeReadTtl { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ChangeLogPollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan ChangeLogRetention { get; set; } = TimeSpan.FromDays(1);

    public TimeSpan ExpiredEntryRetention { get; set; } = TimeSpan.FromDays(1);

    public bool EnableExpiryCleanup { get; set; } = true;

    public bool EnableChangeNotifications { get; set; }

    public bool EnableMaintenanceDashboard { get; set; }

    public bool AllowMaintenanceValueInspection { get; set; }

    public int MaintenanceValuePreviewLimitInBytes { get; set; } = (int)ByteSize.Kilobytes(64);
}
```

```csharp
public static class KeyValueStoreDefaults
{
    public static readonly long MaxValueSizeInBytes = ByteSize.Megabytes(1);
}
```

Rules:

* `KeyValueStorageOptions.Retention` controls the process-wide hosted cleanup schedule and bounds.
* `StorageRetentionOptions.Validate()` runs during registration or startup and invalid values fail startup clearly.
* `StoreName` is required for persistent and locally accelerated persistent stores.
* `NodeId` is required for production locally accelerated persistent stores unless the application explicitly opts into an `INodeIdProvider`.
* `Serialization.Serializer` defaults to a dedicated `SystemTextJsonSerializer` and may be replaced per named client.
* `Serialization.ContentType` is required when typed operations are used; `Serialization.Encoding` may be null for a binary serializer.
* serialization configuration is fixed when the named client is built and must not be mutated while requests are running.
* `MaxValueSizeInBytes` must be enforced before backend writes.
* `MaxValueSizeInBytes` is enforced by default and defaults to 1 MB.
* Size options are represented as raw byte counts in `long` values.
* Default and example size calculations should use the common `ByteSize` helper from `Common.Utilities`, such as `ByteSize.Megabytes(1)`.
* Value-size enforcement should use a key/value-specific helper analogous to Blob Storage's `BlobSizeLimit` helper.
* `MaxStoreSizeInBytes` and `MaxEntryCount` are optional store-level quotas and are disabled unless configured.
* `MaxBulkWriteItems` must be greater than zero and bounds one materialized `SetManyAsync` request.
* `MaxBulkWriteBatchSize` must be greater than zero and no greater than `MaxBulkWriteItems`.
* `MaxBulkWriteConcurrency` must be greater than zero and is the hard process-wide active backend-batch cap for one named store across all bulk requests.
* `MaxQueuedBulkWriteBatches` must be zero or greater and bounds backend batches waiting across all bulk requests for one named store.
* `BulkWriteQueueWaitTimeout` must be greater than zero and bounds one backend batch's wait for shared scheduler admission.
* caller bulk options may lower but never exceed the configured store caps.
* backend-native limits may lower the effective batch size further.
* large or unbounded sources must use `SetManyBatchesAsync` so input and result memory remain bounded.
* `LocalReadTtl` bounds stale reads when polling is delayed.
* `NegativeReadTtl` bounds not-found caching when optional negative caching is enabled.
* `ChangeLogRetention` must be longer than expected node restart and deployment windows.
* `ExpiredEntryRetention` is per store and controls how long a logically expired physical entry remains eligible for inspection before cleanup.
* `ExpiredEntryRetention` must be zero or greater.
* `EnableExpiryCleanup` lets one named store opt out while the shared hosted service remains registered for other stores.
* `EnableChangeNotifications` controls whether observed key changes are published through the key/value observer adapter backed by local `SimpleNotifier` fan-out.
* `EnableMaintenanceDashboard` controls whether Razor dashboard pages and endpoints are available for a store.
* `AllowMaintenanceValueInspection` controls whether full values are rendered or returned through maintenance surfaces.
* `MaintenanceValuePreviewLimitInBytes` limits how much value content the dashboard may render inline or for full inspection before truncation.

## Result Errors

The feature should define typed Result errors for expected failures.

Expected errors:

* `KeyValueStoreValidationError`
* `KeyValueStoreNotFoundError`
* `KeyValueStoreConflictError`
* `KeyValueStoreConcurrencyError`
* `KeyValueStoreBackendError`
* `KeyValueStoreUnsupportedFeatureError`
* `KeyValueStoreChangeLogUnavailableError`
* `KeyValueStoreCheckpointError`
* `KeyValueStoreValueTooLargeError`
* `KeyValueStoreQuotaExceededError`
* `KeyValueStoreHashMismatchError`
* `KeyValueStoreTypeMismatchError`
* `KeyValueStoreCounterOverflowError`
* `KeyValueStoreSerializationError`
* `KeyValueStoreTransformError`
* `KeyValueStoreExpiryCleanupError`
* `KeyValueStoreBulkWriteLimitExceededError`
* `KeyValueStoreBulkWriteSkippedError`
* `KeyValueStoreBulkWriteOverloadedError`
* `KeyValueStoreBulkWriteAdmissionTimeoutError`

Rules:

* Missing keys use `KeyValueStoreNotFoundError`.
* Expired keys use `KeyValueStoreNotFoundError` on `Get`.
* Validation failures use `KeyValueStoreValidationError`.
* Expected version mismatch uses `KeyValueStoreConcurrencyError`.
* Set-if-absent conflict uses `KeyValueStoreConflictError`.
* Unsupported backend capability uses `KeyValueStoreUnsupportedFeatureError`.
* Store quota failures use `KeyValueStoreQuotaExceededError`.
* Existing value/counter type mismatches use `KeyValueStoreTypeMismatchError`.
* Counter overflow uses `KeyValueStoreCounterOverflowError`.
* typed serialization, deserialization, null-result, and configured content-type mismatch failures use `KeyValueStoreSerializationError`.
* serialization errors may report operation, target type, and content type but must not include value content or serialized bytes.
* Compression and encryption failures use `KeyValueStoreTransformError`.
* an aggregate cleanup sweep with one or more failed stores uses `KeyValueStoreExpiryCleanupError` and identifies store names without keys or value content.
* bulk request, batch-size, and concurrency caps use `KeyValueStoreBulkWriteLimitExceededError`.
* items not scheduled by `StopScheduling` use `KeyValueStoreBulkWriteSkippedError`.
* backend batches rejected by the full shared scheduler use `KeyValueStoreBulkWriteOverloadedError`.
* backend batches whose scheduler wait expires use `KeyValueStoreBulkWriteAdmissionTimeoutError`.
* Unexpected backend exceptions are wrapped in `KeyValueStoreBackendError`.

## Operation Flows

### Typed Write And Read

```text
SET<T> key, value
  validate key, value, and options
  serialize T through the named client's ISerializer
  create KeyValueValue with configured content type and encoding
  execute the normal raw write pipeline
  return the normal KeyValueWriteResult

GET<T> key
  execute the normal raw read pipeline
  reverse content transforms
  verify configured content type when required
  deserialize restored bytes through the named client's ISerializer
  return T, or KeyValueEntry<T> when metadata was requested
```

Serialization is not repeated by client behaviors, local-acceleration wrappers, persistence backends, or `KeyValueStoreProvider`.

### Default Locally Accelerated Read

```text
GET key
  validate key
  check negative cache when enabled
  if recent not-found exists:
      return Result.Failure(NotFound)
  check local cache
  if local entry exists and is not expired and cache TTL is valid:
      extend expiry when sliding expiration is enabled
      return Result.Success(entry)
  acquire local single-flight guard
  retry local cache inside guard
  if local entry exists and is not expired and cache TTL is valid:
      return Result.Success(entry)
  read backend
  if backend entry is missing or expired:
      evict local key
      store short-lived negative-read entry when enabled
      return Result.Failure(NotFound)
  update local cache
  return Result.Success(entry)
```

### Fresh Backend Read With Local Refresh

```text
GET key with Fresh
  validate key
  read backend
  if backend entry is missing or expired:
      evict local key
      return Result.Failure(NotFound)
  update local cache
  return Result.Success(entry)
```

### GetOrSet

```text
GETORSET key
  validate key
  try normal read
  if found:
      return Result.Success(entry)
  acquire local single-flight guard
  retry read inside guard
  if found:
      return Result.Success(entry)
  call value factory
  if factory fails:
      return Result.Failure(factory errors)
  set value using SetIfAbsent when configured
  return stored entry
```

### Locally Accelerated Write

```text
SET key value
  validate key
  validate value and options
  calculate hash
  enforce maximum value size
  enforce store quotas when configured
  write value and append change-log entry atomically
  update local cache
  clear negative-read entry
  return Result.Success(write result)
```

### Locally Accelerated Bulk Write

```text
SET MANY items
  validate request caps and reject duplicate keys
  validate and transform each item
  partition valid items into bounded backend batches
  while scheduled backend batches < MaxConcurrentBatches:
      persist one batch
        load current rows as a set
        evaluate per-key write conditions
        write valid entries and one change-log row per changed key
        commit the backend batch
      update local cache only for successful items
      record ordered per-item results
      if a failure completed and mode is StopScheduling:
          stop scheduling new batches
  mark unscheduled items as skipped
  return aggregate counts and input-ordered item results
```

The core orchestration never has more than the configured number of bounded backend batches in flight and never updates local cache before durable commit.

### Touch

```text
TOUCH key
  validate key
  validate expiry options
  update expiry metadata and append change-log entry atomically
  update local cache metadata
  clear negative-read entry
  return Result.Success(write result)
```

### Expire

```text
EXPIRE key
  validate key
  validate absolute expiry
  update expiry metadata and append change-log entry atomically
  update or evict local cache depending on expiry timestamp
  clear negative-read entry
  return Result.Success(write result)
```

### Locally Accelerated Delete

```text
DELETE key
  validate key
  delete key and append change-log entry atomically
  evict local cache
  clear or replace negative-read entry according to options
  return Result.Success(existed)
```

`Delete` returns `Result<bool>`. `true` means a value or counter existed and was removed; `false` means the key was already absent or expired.

`Delete` should be idempotent unless a backend-specific concurrency option is configured.

### Polling Invalidation

```text
POLL changes
  load checkpoint
  read change-log entries after checkpoint
  if checkpoint is older than retained history:
      clear local cache
      set checkpoint to high watermark
      return
  for each change:
      if change is a self-originated ClientMutation
         and local state already reflects its committed version or deletion:
          advance checkpoint
          continue
      advance change.Key invalidation generation
      evict positive, counter, and negative local state for change.Key
      block older in-flight reads from repopulating local state
      publish local KeyValueChangedNotification when enabled
      advance checkpoint
  save checkpoint
```

## Registration

Registration follows the backend-specific fluent style used by Blob Storage. `AddKeyValueStorage` registers shared feature services and returns a `KeyValueStorageBuilderContext`. A backend method then registers each named client and makes its storage shape explicit.

```csharp
public static KeyValueStorageBuilderContext AddKeyValueStorage(
    this IServiceCollection services,
    Action<KeyValueStorageOptions> configure = null);
```

The feature registration adds exactly one hosted cleanup worker for the process:

```csharp
services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IHostedService,
        KeyValueStoreExpiryCleanupBackgroundService>());

services.TryAddSingleton(serviceProvider => serviceProvider
    .GetServices<IHostedService>()
    .OfType<KeyValueStoreExpiryCleanupBackgroundService>()
    .Single());

services.TryAddBackgroundServiceHealthCheck<
    KeyValueStoreExpiryCleanupBackgroundService>(
        "KeyValueStorageExpiryCleanup");
```

Registration rules:

* the hosted service is registered once by `AddKeyValueStorage`, not once per named client;
* registration is safe even when cleanup is disabled or no backend supports cleanup;
* the singleton alias exposes `SweepOnceAsync` and diagnostics without constructing a second worker;
* named backends remain container-owned and are resolved inside worker-created asynchronous scopes;
* the hosted service receives immutable client registration descriptors rather than discovering arbitrary services from the root container;
* disabling retention through `KeyValueStorageOptions.Retention.Enabled` keeps the service dormant;
* a backend with `SupportsExpiryCleanup = true` must implement `IKeyValueStoreExpiryCleanupBackend`.

### Backend Registration Methods

Required registration methods:

| Registration method | Storage shape | Local acceleration | Change-log polling |
| --- | --- | --- | --- |
| `WithInMemoryClient(name, configure)` | Pure in-memory | The backend is local memory | No |
| `WithDirectEntityFrameworkClient<TContext>(name, configure)` | Direct persistent EF | No | No |
| `WithEntityFrameworkClient<TContext>(name, configure)` | Locally accelerated persistent EF | Yes | Yes |
| `WithDirectAzureTableClient(name, configure, configureBackend)` | Direct persistent Azure Table | No | No |
| `WithAzureTableClient(name, configure, configureBackend)` | Locally accelerated persistent Azure Table | Yes | Yes |
| `WithClient(name, backendFactory, configure, ...)` | Custom backend | Backend-defined | Backend-defined |

For durable backends, the concise method registers the recommended locally accelerated write-through production shape. The `WithDirect...Client` variant explicitly opts out of the node-local read layer and change-log polling.

Suggested EF extension signatures:

```csharp
public static KeyValueStorageBuilderContext WithDirectEntityFrameworkClient<TContext>(
    this KeyValueStorageBuilderContext context,
    string name,
    Action<KeyValueStoreOptions> configure = null,
    ServiceLifetime? lifetime = null)
    where TContext : DbContext, IKeyValueStoreDbContext;

public static KeyValueStorageBuilderContext WithEntityFrameworkClient<TContext>(
    this KeyValueStorageBuilderContext context,
    string name,
    Action<KeyValueStoreOptions> configure = null,
    ServiceLifetime? lifetime = null)
    where TContext : DbContext, IKeyValueStoreDbContext;
```

Suggested in-memory, Azure Table, and custom extension signatures:

```csharp
public static KeyValueStorageBuilderContext WithInMemoryClient(
    this KeyValueStorageBuilderContext context,
    string name,
    Action<KeyValueStoreOptions> configure = null,
    ServiceLifetime? lifetime = null);

public static KeyValueStorageBuilderContext WithDirectAzureTableClient(
    this KeyValueStorageBuilderContext context,
    string name,
    Action<KeyValueStoreOptions> configure = null,
    Action<AzureTableKeyValueStoreOptions> configureBackend = null,
    ServiceLifetime? lifetime = null);

public static KeyValueStorageBuilderContext WithAzureTableClient(
    this KeyValueStorageBuilderContext context,
    string name,
    Action<KeyValueStoreOptions> configure = null,
    Action<AzureTableKeyValueStoreOptions> configureBackend = null,
    ServiceLifetime? lifetime = null);

public static KeyValueStorageBuilderContext WithClient(
    this KeyValueStorageBuilderContext context,
    string name,
    Func<IServiceProvider, IKeyValueStoreBackend> backendFactory,
    Action<KeyValueStoreOptions> configure = null,
    string backendName = null,
    KeyValueStoreBackendCapabilities capabilities = null,
    ServiceLifetime? lifetime = null);
```

Registration rules:

* every client name must be unique case-insensitively;
* `name` is the keyed DI name and defaults `KeyValueStoreOptions.StoreName` when that option is omitted;
* when both are supplied, `StoreName` must equal the registration name after normalization;
* behaviors registered before backend clients apply to every later named client in that builder flow;
* the first registered behavior is the outermost decorator;
* named clients and backends use keyed DI and default to scoped lifetime;
* `IKeyValueStoreClientFactory` is registered once and resolves those keyed clients;
* backend-specific extensions validate their required services and options during registration or startup;
* `WithEntityFrameworkClient` and `WithAzureTableClient` require a stable `NodeId` or explicit `INodeIdProvider`;
* direct persistent registrations do not start local-cache invalidation polling and do not require `NodeId`;
* in-memory registration does not require a database, storage account, change log, or node identity.

### Simple Provider Registration

Applications that use the renamed simple abstraction select one named client as its backing store:

```csharp
public static IServiceCollection AddKeyValueStoreProvider(
    this IServiceCollection services,
    string clientName,
    Action<KeyValueStoreProviderOptions> configure = null);
```

```csharp
public sealed class KeyValueStoreProviderOptions
{
    public string KeyPrefix { get; set; }

    public bool EnableEnumeration { get; set; } = true;
}
```

```csharp
services.AddKeyValueStoreProvider(
    "application-state",
    options =>
    {
        options.KeyPrefix = "provider/";
        options.EnableEnumeration = true;
    });
```

Rules:

* `clientName` must resolve through `IKeyValueStoreClientFactory`;
* the helper registers `KeyValueStoreProvider` as the default unkeyed `IKeyValueStoreProvider`;
* only one default unkeyed provider registration is allowed;
* `KeyPrefix` scopes provider-owned enumeration and prefix removal without changing direct client keys outside that namespace;
* `EnableEnumeration = false` disables `GetKeys` and `RemoveStartsWith` according to the established unsupported-operation behavior;
* applications needing several simple providers use keyed `IKeyValueStoreProvider` registrations, each selecting one named client and namespace;
* registration must not construct a second client pipeline or serializer.

### Typed Serialization Registration

Typed operations need no additional registration. Every named client defaults to `SystemTextJsonSerializer`, `application/json`, and UTF-8. A client can replace those settings independently:

```csharp
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true
};

services.AddKeyValueStorage()
    .WithEntityFrameworkClient<AppDbContext>("application-state", options =>
    {
        options.NodeId = configuration["KeyValue:NodeId"];
        options.Serialization.Serializer =
            new SystemTextJsonSerializer(jsonOptions);
        options.Serialization.ContentType = "application/json";
        options.Serialization.Encoding = "utf-8";
        options.Serialization.RequireContentTypeMatchOnRead = true;
    });
```

This serializer applies to `SetAsync<T>`, `GetAsync<T>`, `GetEntryAsync<T>`, `GetOrSetAsync<T>`, generic bulk writes, and `KeyValueStoreProvider`. Raw `KeyValueValue` operations are unaffected.

### In-Memory Registration

Use in-memory storage for tests, development, ephemeral state, or an explicitly single-process application:

```csharp
services.AddKeyValueStorage()
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithInMemoryClient("local", options =>
    {
        options.MaxValueSizeInBytes = ByteSize.Megabytes(1);
        options.DefaultTimeToLive = TimeSpan.FromMinutes(30);
        options.MaxBulkWriteItems = 1000;
    });
```

The in-memory backend owns synchronized process memory. It is neither durable nor shared with another application node.

### Direct Entity Framework Registration

Use a direct persistent EF client when every read should go to the relational backend and local cache invalidation is unnecessary:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        configuration.GetConnectionString("DefaultConnection")));

services.AddKeyValueStorage()
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithRetryBehavior()
    .WithTimeoutBehavior()
    .WithDirectEntityFrameworkClient<AppDbContext>("durable-state", options =>
    {
        options.MaxValueSizeInBytes = ByteSize.Megabytes(1);
        options.DefaultTimeToLive = TimeSpan.FromHours(1);
        options.MaxBulkWriteItems = 1000;
        options.MaxBulkWriteBatchSize = 250;
        options.MaxBulkWriteConcurrency = 4;
    });
```

This registers a persistent named client without a local read cache or polling worker. It still supports expiry cleanup, maintenance, counters, optimistic concurrency, and bounded backend-native bulk writes.

### Locally Accelerated Entity Framework Registration

Use locally accelerated persistent EF for shared production state with fast node-local reads:

```csharp
services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        configuration.GetConnectionString("DefaultConnection")));

services.AddKeyValueStorage()
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithRetryBehavior()
    .WithTimeoutBehavior()
    .WithEntityFrameworkClient<AppDbContext>(
        "application-state",
        options =>
        {
            options.NodeId = configuration["KeyValue:NodeId"];
            options.DefaultTimeToLive = TimeSpan.FromHours(1);
            options.LocalReadTtl = TimeSpan.FromSeconds(30);
            options.ExpiredEntryRetention = TimeSpan.FromDays(1);
            options.EnableExpiryCleanup = true;
            options.ChangeLogPollingInterval = TimeSpan.FromSeconds(2);
            options.MaxValueSizeInBytes = ByteSize.Megabytes(1);
            options.MaxBulkWriteItems = 1000;
            options.MaxBulkWriteBatchSize = 250;
            options.MaxBulkWriteConcurrency = 4;
            options.MaxQueuedBulkWriteBatches = 16;
            options.BulkWriteQueueWaitTimeout = TimeSpan.FromSeconds(30);
        });
```

The locally accelerated registration composes:

```text
IKeyValueStoreClient
    -> configured client behaviors
    -> LocallyAcceleratedKeyValueStoreBackend
         -> node-local memory cache
         -> EntityFrameworkKeyValueStorePersistenceBackend<AppDbContext>
         -> change-log poller and checkpoint store
```

Writes remain write-through. A successful write commits the entry and its change-log row before the writing node updates local cache. Other nodes evict through polling.

### EF DbContext Contract

The consuming application owns and registers its EF `DbContext`. The DevKit registration does not create database options, select a relational database provider, or own application migrations.

The context must derive from `DbContext` and implement `IKeyValueStoreDbContext`:

```csharp
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IKeyValueStoreDbContext
{
    public DbSet<KeyValueStoreEntryEntity> KeyValueStoreEntries =>
        this.Set<KeyValueStoreEntryEntity>();

    public DbSet<KeyValueStoreChangeLogEntity> KeyValueStoreChangeLog =>
        this.Set<KeyValueStoreChangeLogEntity>();

    public DbSet<KeyValueStoreCheckpointEntity> KeyValueStoreCheckpoints =>
        this.Set<KeyValueStoreCheckpointEntity>();
}
```

The default entity mappings are declared on the infrastructure entities through data annotations. The application does not copy DevKit mappings into `OnModelCreating`. Backend-specific overrides may still be applied when required by the selected relational database.

The application creates and applies the migration containing the three storage tables:

```shell
dotnet ef migrations add AddKeyValueStorage --context AppDbContext
dotnet ef database update --context AppDbContext
```

EF registration and lifetime rules:

* `TContext` must already be registered through `AddDbContext`, `AddDbContextPool`, or an equivalent scoped registration;
* the extension validates that `TContext` derives from `DbContext` and implements `IKeyValueStoreDbContext`;
* each persistence operation creates and owns a DI scope and resolves a fresh `TContext`;
* one EF bulk backend batch uses one context and one transaction;
* concurrent bulk batches never share a `DbContext`;
* polling, expiry cleanup, health, and maintenance work create their own scopes;
* the backend disposes operation-owned scopes and contexts;
* backend-owned operations do not automatically enlist in an application service's separately injected `AppDbContext` transaction;
* singleton named clients remain safe because they never retain a scoped `TContext`;
* `AddDbContextPool` is supported because every operation returns its context by disposing the operation scope.

An application that needs a domain write and a key/value write to commit in the same relational transaction requires a separate explicit integration design. The backend-neutral client does not expose the consuming `DbContext` or ambient transaction enlistment.

### Azure Table Registration

Register the Azure SDK client separately, then select either direct or locally accelerated store shape:

```csharp
public sealed class AzureTableKeyValueStoreOptions
{
    public string TableNamePrefix { get; set; } = "bdkkv";

    public int ShardCount { get; set; } = 16;

    public bool CreateTablesIfMissing { get; set; } = true;
}
```

`TableNamePrefix` must satisfy Azure Table naming constraints after the backend adds its entry, change-log, and checkpoint suffixes. `ShardCount` must be greater than zero and becomes persisted partitioning configuration; changing it after data exists requires an explicit migration.

```csharp
services.AddSingleton(new TableServiceClient(
    configuration.GetConnectionString("Storage")));

services.AddKeyValueStorage()
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithAzureTableClient(
        "cloud-state",
        options =>
        {
            options.NodeId = configuration["KeyValue:NodeId"];
            options.LocalReadTtl = TimeSpan.FromSeconds(30);
            options.MaxValueSizeInBytes = ByteSize.Kilobytes(512);
        },
        backend =>
        {
            backend.TableNamePrefix = "appkv";
            backend.ShardCount = 16;
            backend.CreateTablesIfMissing = true;
        });
```

`WithDirectAzureTableClient` uses the same persistence backend without the node-local read layer or polling wrapper. Backend startup creates or validates its required tables according to `AzureTableKeyValueStoreOptions`.

### Multiple Backends In One Application

Different named stores may use different backends:

```csharp
services.AddKeyValueStorage()
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithInMemoryClient("request-scratch")
    .WithDirectEntityFrameworkClient<AppDbContext>("durable-state")
    .WithEntityFrameworkClient<AppDbContext>(
        "application-state",
        options =>
        {
            options.NodeId = configuration["KeyValue:NodeId"];
        })
    .WithAzureTableClient(
        "cloud-state",
        options =>
        {
            options.NodeId = configuration["KeyValue:NodeId"];
        });
```

The factory selects the backend through the registered name:

```csharp
var durable = clientFactory.CreateClient("durable-state");
var accelerated = clientFactory.CreateClient("application-state");
var cloud = clientFactory.CreateClient("cloud-state");
```

Hard store caps are configured at registration. Per-call bulk options may lower but never exceed those caps.

### Backend Registration Verification

Required tests:

* in-memory registration resolves an in-memory named client without EF or Azure dependencies;
* direct EF registration resolves a persistent client without local-read polling;
* locally accelerated EF registration composes the EF persistence backend, local read layer, poller, checkpointing, and node identity;
* both EF registrations reject a `TContext` that does not implement `IKeyValueStoreDbContext`;
* locally accelerated registrations fail startup when stable node identity is missing under production rules;
* operation scopes resolve and dispose a fresh context;
* concurrent EF bulk batches receive different context instances;
* direct and locally accelerated Azure registrations resolve `TableServiceClient` and validate table options;
* several differently backed named clients resolve through one factory;
* `AddKeyValueStoreProvider` resolves the selected named client and registers one default `IKeyValueStoreProvider`;
* simple provider registration fails clearly for an unknown client name or a second unkeyed default;
* duplicate client names fail registration.

## Health Checks

Key/Value Storage should register health checks for configured persistent and locally accelerated persistent stores.

Health checks should cover:

* Persistent backend connectivity.
* Backend read/write capability when safe to probe.
* Change-log read capability.
* Checkpoint read/write capability.
* Polling worker status.
* Polling lag compared to backend high watermark.
* Local read-layer availability.
* Quota status when quotas are configured.
* Expiry-cleanup background service execution status when retention is enabled.
* Last cleanup start/completion time, duration, deleted count, `HasMore`, and failed store names.

Health check results should identify the affected store name and backend type.

Locally accelerated persistent stores should report degraded health when:

* The backend is reachable but the polling worker is stopped.
* The checkpoint cannot be saved.
* Polling lag exceeds the configured threshold.
* The node had to clear local cache because retained change-log history was missed.
* Expiry cleanup is enabled but the hosted service is not running.
* No cleanup iteration has completed within the configured startup delay plus an operationally reasonable multiple of the sweep interval.
* The latest cleanup iteration failed for one or more stores.

Disabled cleanup reports healthy/not-applicable rather than degraded.

## Razor Dashboard

The Razor dashboard is an operational surface for persistent key/value stores.

Dashboard authorization policies:

* `KeyValueStorage.Read` allows listing stores, keys, metadata, TTL, quota status, polling status, local-read statistics, and safe value previews.
* `KeyValueStorage.Manage` allows writes, deletes, TTL changes, counter changes, force invalidation, and full value inspection.

Dashboard capabilities:

* Select a configured persistent or locally accelerated persistent store.
* Page through keys and metadata.
* View a full key.
* View full value content when value inspection is enabled.
* Display whether a value is string-like or binary.
* Display metadata such as content type, encoding, size, hash, version, creation time, update time, and expiry.
* Display store quotas and current usage when available.
* Display expiry-cleanup enabled state, last outcome, next expected sweep, deleted count, remaining-work indicator, and failed stores.
* Display change-log high watermark, node checkpoint, polling lag, last poll time, and last poll error.
* Display local-read hit/miss and eviction statistics when available.
* Add a new key/value entry.
* Edit an existing key/value entry.
* Delete any persisted key.
* Provide explicit confirmation for destructive deletes.
* Surface Result errors from the maintenance service.

Dashboard design rules:

* Dashboard pages call `IKeyValueStoreMaintenanceService`.
* Dashboard pages do not access backend internals directly.
* Dashboard reads and writes go through the persistent backing store.
* Dashboard add/edit/delete operations append change-log entries through the maintenance backend so locally accelerated app nodes invalidate local entries.
* Dashboard routes should remain dashboard routes and should not be part of generated public API specs by default.
* Dashboard pages should use normal Razor/RazorSlice syntax and existing shared dashboard helpers.
* Dashboard pages should prefer fragment refreshes for changing content rather than full page reloads.
* Dashboard pages should gracefully handle unavailable backends with a benign unavailable state.

Value rendering rules:

* Text values may be rendered as text when value inspection is enabled.
* Counter values may be rendered as numeric values with metadata.
* Binary values should show metadata and a safe preview strategy only when explicitly supported.
* Large values should be guarded by a configurable display limit.
* The default value inspection limit is 64 KB.
* Values above the configured display limit must show metadata plus a clear truncated indicator.
* Value content must not be written to logs.

## Observability

The feature should emit structured logs and metrics for:

* Get count.
* Set count.
* Bulk set request count.
* Bulk set requested, succeeded, failed, and skipped item counts.
* Bulk set backend batch count.
* Bulk set batch-size histogram.
* Bulk set concurrent-batch gauge.
* Bulk set queued-batch gauge.
* Bulk set scheduler wait duration.
* Bulk set scheduler rejection count.
* Bulk set scheduler timeout count.
* Bulk set duration and backend-batch latency.
* Delete count.
* Exists count.
* Touch count.
* Expire count.
* GetOrSet count.
* Increment count.
* Decrement count.
* Local-read hits.
* Local-read misses.
* Negative-read hits.
* Single-flight coalesced reads.
* Fresh reads.
* Backend read latency.
* Backend write latency.
* Change-log polling latency.
* Change-log entries processed.
* Local cache evictions.
* Key-change notifications published.
* Key-change notification handler failures.
* Full local cache clears.
* Positive, counter, and negative local-state evictions by change origin.
* Stale in-flight local-state population attempts prevented by invalidation generation.
* Maintenance list/read/write/delete operations.
* Maintenance dashboard value-inspection attempts.
* Health check status.
* Store quota usage.
* Compression and encryption behavior failures.
* Missed change-log recovery events.
* Validation failures.
* Serialization and deserialization failures.
* Concurrency conflicts.
* Counter type mismatches.
* Counter overflow failures.
* Backend failures.
* Expiry-cleanup sweep count and duration.
* Expiry-cleanup store count, deleted count, skipped count, batch count, and `HasMore` count.
* Expiry-cleanup failure count, cancellation count, and last-success timestamp.

Bulk metrics must use store name, backend type, outcome, and failure category as low-cardinality dimensions. They must not use individual keys, tags, hashes, versions, or value content as metric dimensions.

Cleanup metrics use store name, backend type, and outcome as low-cardinality dimensions. They must not use keys, versions, tags, or value content.

### Typed Logging

Key/Value Storage must expose its internal operation flow through typed logging.

Typed logging rules:

* Use source-generated logging methods with `[LoggerMessage]` on partial `TypedLogger` classes, following existing devkit patterns.
* Use stable event ids per component so logs can be filtered by operation area.
* Include `Constants.LogKey` in message templates for consistency with other devkit features.
* Include structured properties such as store name, backend type, operation, result status, error type, local-read outcome, consistency mode, node id, sequence, checkpoint, polling lag, version, size, expiry, and elapsed time where relevant.
* Do not log value content.
* Log full raw keys by default because keys are generally operational identifiers rather than sensitive content.
* Support a configured safe key display strategy, key hash, or redacted key value for stores whose key names may contain sensitive data.
* Use `Trace` for high-volume inner decisions, `Debug` for normal internal operation flow, `Information` for lifecycle events and maintenance actions, `Warning` for degraded but recoverable states, and `Error` for unexpected backend or worker failures.
* Internal typed logging must be present even when the optional logging client behavior is not registered.
* The optional logging client behavior must not be the only source of operational visibility for local-read invalidation, persistence writes, checkpoints, cleanup, or recovery.
* Hot-path logs must avoid string interpolation and ad hoc object allocations.
* Backend implementations should expose typed logging for implementation-specific behavior without leaking backend-specific public APIs.

Logs must not include value content.

Keys are generally not sensitive and logs should show full raw keys by default. Stores that use sensitive key names should configure a safe key display strategy such as hashing, redaction, or custom formatting.

## Security And Privacy

The key/value store is a storage abstraction. It does not enforce authorization policy by itself.

Security expectations:

* Do not log value content.
* Log raw keys by default for operational diagnosability.
* Use a configured safe key display strategy when a store uses sensitive key names.
* Do not put values into change-log entries.
* Restrict dashboard maintenance access to authorized operational users through `KeyValueStorage.Read` and `KeyValueStorage.Manage` policies.
* Require explicit configuration before full value inspection is exposed in dashboard surfaces.
* Treat dashboard add, edit, and delete as privileged operations.
* Keep encryption key management, storage, and rotation outside the backend contract; value encryption behavior should use `EncryptionHelper` primitives for the built-in AES-CBC implementation.
* Treat string values as content, not metadata.
* Do not expose stored values through diagnostic surfaces unless a diagnostic surface explicitly enables value inspection.

## Acceptance Criteria

### Story 1: Store And Read Values

User story: As an application developer, I want to store and read values by exact key, so that application features can persist small key-addressed state.

Acceptance criteria:

1. Given a valid key and string value, when `Set` succeeds, then `Get` returns the stored string content and metadata.
2. Given a valid key and binary value, when `Set` succeeds, then `Get` returns the stored bytes and metadata.
3. Given a missing key, when `Get` is called, then it returns `Result<T>.Failure(...)` with a not-found error.
4. Given a missing key, when `Exists` is called, then it returns `Result<bool>.Success(false)`.
5. Given an existing key, when `Delete` succeeds, then it returns `Result<bool>.Success(true)`.
6. Given a missing or expired key, when `Delete` succeeds idempotently, then it returns `Result<bool>.Success(false)`.
7. Given an invalid key, when any operation is called, then it returns a validation failure.

### Story 2: Use Atomic Counters

User story: As an application developer, I want atomic counters by exact key, so that distributed app instances can track short-lived numeric state without building a separate workflow.

Acceptance criteria:

1. Given a missing counter and `CreateIfMissing` is true, when `Increment` is called with delta 1, then the counter is created and returns value 1.
2. Given an existing counter, when concurrent app instances increment the same key, then each successful operation is atomic and no increments are lost.
3. Given an existing non-counter value at the key, when `Increment` or `Decrement` is called, then the operation fails with a type mismatch error.
4. Given a counter operation would exceed the signed 64-bit range, when the operation is called, then it fails with a counter overflow error.
5. Given a locally accelerated persistent store, when a counter operation succeeds, then the counter update and change-log append are committed atomically and other nodes invalidate the key.

### Story 3: Use A Pure In-Memory Store

User story: As an application developer, I want a pure in-memory store, so that tests, local development, and ephemeral single-process features can use the same client API.

Acceptance criteria:

1. Given the in-memory store, when values are written and read in the same process, then operations complete without a persistent backend.
2. Given the process restarts, when the in-memory store is used, then previously stored values are not available.
3. Given multiple app nodes use independent in-memory stores, when one node writes a key, then another node does not observe that value.
4. Given TTL is configured, when a value expires, then `Get` returns not-found and `Exists` returns false.

### Story 4: Use Local Read Acceleration

User story: As an application developer, I want optional local read acceleration, so that production apps can combine fast reads with durable shared storage.

Acceptance criteria:

1. Given a locally accelerated persistent store, when `Set` succeeds, then the value has been durably written and the change-log entry has been appended.
2. Given a key exists in the local read layer, when `Get` is called before expiry and `LocalReadTtl` is valid, then the store returns the local entry.
3. Given a key is absent locally, when `Get` is called, then the store reads the backend and populates the local read layer.
4. Given `Fresh` consistency, when `Get` is called, then the store reads the backend and refreshes local state.
5. Given `BackendOnly` consistency, when `Get` is called, then the store reads the backend without reading or updating local state.
6. Given the backend cannot atomically write the value and change-log entry, when configured for multi-node local acceleration, then registration or startup fails.

### Story 5: Invalidate Across Nodes

User story: As an application operator, I want locally accelerated nodes to invalidate stale entries, so that load-balanced deployments converge after writes.

Acceptance criteria:

1. Given Node A writes a key, when Node B polls the change log, then Node B evicts the key from its local cache.
2. Given Node B subsequently reads the key, when the local cache entry was evicted, then Node B reloads the value from the backend.
3. Given a node processes its own `ClientMutation`, when its local state already reflects the committed version or deletion, then polling may suppress the redundant eviction.
4. Given polling is delayed, when local cache TTL expires, then the stale local entry is not returned.
5. Given a node checkpoint is older than retained change-log history, when polling runs, then the node clears its local cache and resumes from the current high watermark.
6. Given Node B cached a missing key, when Node A creates that key and Node B polls the `Set` change, then Node B removes the negative entry and its next `Get` or `Exists` observes the created value.
7. Given an older backend read is in flight when Node B processes a change, when that read completes, then its captured invalidation generation prevents it from repopulating stale positive or negative state.
8. Given a maintenance or expiry-cleanup change originated in Node B's process, when Node B polls it, then the change is not suppressed as a self-originated client mutation.

### Story 6: Enforce Concurrency

User story: As an application developer, I want minimal optimistic concurrency, so that concurrent writers do not accidentally overwrite each other.

Acceptance criteria:

1. Given `ExpectedVersion` matches the current version, when `Set` is called, then the write succeeds and returns a new version.
2. Given `ExpectedVersion` does not match the current version, when `Set` is called, then the write fails with a concurrency error.
3. Given `SetIfAbsent` is true and the key does not exist or is expired, when `Set` is called, then the write succeeds.
4. Given `SetIfAbsent` is true and the key exists and is not expired, when `Set` is called, then the write fails with a conflict error.

### Story 7: Enforce Expiry

User story: As an application developer, I want values to expire, so that temporary state does not live forever.

Acceptance criteria:

1. Given a value has an expiry timestamp in the past, when `Get` is called, then it returns not-found.
2. Given a value has an expiry timestamp in the past, when `Exists` is called, then it returns false.
3. Given a value or counter has an expiry timestamp in the past, when update, counter, or dashboard operations inspect it, then it is treated as logically absent.
4. Given hosted expiry cleanup is enabled for a durable store, when the `KeyValueStoreExpiryCleanupBackgroundService` finds entries beyond their retention window, then it physically removes them in bounded batches.
5. Given cleanup physically removes an entry from a locally accelerated persistent store, then the backend records an expiry change-log entry when required for invalidation.
6. Given an expired value remains resident on another node, when that node reads it before receiving the cleanup change, then stored `ExpiresAt` prevents the value from being returned.
7. Given Node A physically removes an expired entry, when any node including Node A polls the `ExpiryCleanup` change, then it evicts all local state for that key.
8. Given a key is re-created after its expired version was removed, when other nodes process the ordered cleanup and `Set` changes, then their cached existence state converges to the newer backend version.

### Story 8: Operate Persistent Entries From Dashboard

User story: As an operator, I want to inspect and maintain persisted key/value entries from the Razor dashboard, so that I can diagnose and repair application state.

Acceptance criteria:

1. Given a persistent store with dashboard maintenance enabled, when an operator opens the dashboard page, then keys and metadata are shown as a paged result.
2. Given value inspection is enabled, when an operator opens an entry, then the full key, value, and metadata are available.
3. Given value inspection is disabled, when an operator opens an entry, then value content is not shown while metadata remains available.
4. Given a value exceeds the configured inspection limit, when an operator opens an entry, then the dashboard shows metadata and a clear truncated indicator.
5. Given a user has `KeyValueStorage.Read`, when they use the dashboard, then they can view stores, keys, metadata, TTL, status, and safe previews but cannot mutate values.
6. Given a user has `KeyValueStorage.Manage`, when they use the dashboard, then they can add, edit, delete, change TTL, adjust counters, force invalidation, and inspect full values when value inspection is enabled.
7. Given an operator adds a new entry, when the maintenance write succeeds, then the value is written to the persistent backing store and a change-log entry is appended.
8. Given an operator edits an existing entry, when the maintenance write succeeds, then locally accelerated nodes invalidate the key through change-log polling.
9. Given an operator deletes an entry, when the delete is confirmed and succeeds, then the persistent backing store removes the entry and a change-log entry is appended.
10. Given the persistent backend is unavailable, when the dashboard requests entries, then the page shows a benign unavailable state and does not throw.

### Story 9: Observe Key Changes Locally

User story: As an application developer, I want local handlers to react when this node observes key changes, so that lightweight derived state can be refreshed without using the message broker.

Acceptance criteria:

1. Given change notifications are enabled, when the polling worker processes a change-log entry from another node, then it evicts the local cache entry and publishes a local `KeyValueChangedNotification`.
2. Given change notifications are enabled, when the polling worker suppresses a self-originated `ClientMutation` that local state already reflects, then it does not publish a duplicate local observer notification.
3. Given a local observer handles a notification, when it runs, then it receives store name, key, operation, version, and changed timestamp without value content.
4. Given a local observer is registered, when duplicate change notifications are delivered, then the observer must be able to handle duplicates safely.
5. Given a node missed retained change-log history, when the polling worker performs full cache recovery, then it clears the local cache but does not attempt to replay individual missed observer notifications.
6. Given a workflow requires durable or exactly-once handling, when the feature is configured, then it must use message broker, outbox, jobs, or another durable workflow mechanism instead of local key-change observers.

### Story 10: Use Client Behaviors

User story: As an application developer, I want key/value client behaviors, so that cross-cutting features can be composed consistently with other devkit storage features.

Acceptance criteria:

1. Given behaviors are registered for a store, when the client is resolved, then the behaviors wrap the client in the configured order.
2. Given compression behavior is enabled, when a value above the configured minimum payload size is written, then the stored value is compressed through `CompressionHelper` GZip APIs and metadata records how to read it back.
3. Given encryption behavior is enabled, when a value is written, then the stored value is encrypted through `EncryptionHelper` AES-CBC support and metadata records the algorithm and resolved key id.
4. Given checksum verification behavior is enabled, when a value is read, then the stored bytes are verified according to the configured transform order.
5. Given negative cache behavior is enabled, when repeated reads miss the same key, then the backend is not repeatedly queried within the negative-cache TTL.
6. Given the locally accelerated persistent store is used, when concurrent same-node reads miss the same key, then built-in single-flight ensures only one backend load or value factory execution occurs for that key.
7. Given a custom behavior is registered, when the client is resolved, then the behavior can wrap value and counter operations without backend-specific APIs.
8. Given encryption behavior is enabled with an `EncryptionHelper`-compatible key, when a value is written and read back, then the persisted bytes are encrypted and the client returns the original value content.
9. Given compression and encryption are both enabled, when a value is written, then compression runs before encryption.

### Story 11: Manage Expiry Without Rewriting Values

User story: As an application developer, I want to touch and expire keys, so that temporary state can be extended or shortened without rewriting stored values.

Acceptance criteria:

1. Given a key exists, when `Touch` is called with a TTL, then the entry expiry is extended and the value content is unchanged.
2. Given a key exists, when `Expire` is called with an absolute timestamp, then the entry expiry is updated and the value content is unchanged.
3. Given a key is missing or expired, when `Touch` is called, then it returns a not-found failure.
4. Given a locally accelerated persistent store, when `Touch` or `Expire` succeeds, then a change-log entry is appended and other nodes invalidate the key.

### Story 12: Use The Simple Key/Value Provider

User story: As an application developer, I want a simple `IKeyValueStoreProvider`, so that ordinary typed application state does not require the advanced Result-native client API.

Acceptance criteria:

1. Given `KeyValueStoreProvider` is registered as `IKeyValueStoreProvider`, when a consumer calls `Set` and later `Get` for the same key, then the value is stored and read through `IKeyValueStoreClient`.
2. Given sliding or absolute expiration is supplied to `IKeyValueStoreProvider.Set`, when the provider writes the value, then the expiration is mapped to key/value write options.
3. Given a consumer calls `Remove`, when the key exists or is missing, then the provider deletes through `IKeyValueStoreClient` and preserves idempotent remove behavior.
4. Given a consumer calls `GetKeys`, when provider key enumeration is enabled, then only keys from its configured namespace are returned.
5. Given a consumer calls `RemoveStartsWith`, when provider key enumeration is enabled, then matching keys are deleted through exact-key operations and locally accelerated nodes invalidate through the normal change-log mechanism.
6. Given provider key enumeration is disabled, when `GetKeys` or `RemoveStartsWith` is called, then the provider follows its configured unsupported-operation behavior without adding scans to `IKeyValueStoreClient`.
7. Given values are written through the provider, when typed logging is enabled, then logs include operation, store name, result status, and the raw key by default without value content.
8. Given the rename is complete, when public APIs and implementations are inspected, then the target abstraction is `IKeyValueStoreProvider` and `ICacheProvider` exists only as an explicitly obsolete compatibility shim when required.
9. Given a component implements a genuine caching policy, when symbols are renamed, then cache-specific behavior names may remain while the general provider dependency changes to `IKeyValueStoreProvider`.

### Story 13: Observe Internal Operations With Typed Logging

User story: As an application operator, I want key/value operations to emit typed internal logs, so that local acceleration, persistence writes, polling, and recovery can be diagnosed without enabling custom instrumentation.

Acceptance criteria:

1. Given any public client operation is executed, when internal logging is enabled by normal .NET logging configuration, then typed log events expose operation name, store name, backend type, result status, and elapsed time without value content.
2. Given a locally accelerated read is executed, when the store chooses local memory, negative cache, fresh backend read, or backend load after miss, then typed log events expose that decision and the raw key by default, unless a safe key display strategy is configured.
3. Given a locally accelerated write or counter operation succeeds, when the persistence backend commits the state change and change-log entry, then typed log events expose the operation, version, origin node, and change-log sequence when available.
4. Given change-log polling runs, when entries are read, skipped, processed, checkpointed, or missed-retention recovery is triggered, then typed log events expose node id, checkpoint, processed count, polling lag, and recovery action.
5. Given the optional logging behavior is not registered, when internal backend, polling, cleanup, or maintenance work runs, then typed internal logs are still emitted according to logging configuration.

### Story 14: Operate With Health And Quotas

User story: As an operator, I want health and quota visibility, so that I can detect backend, polling, and capacity problems before they cause user-facing failures.

Acceptance criteria:

1. Given a persistent store is configured, when health checks run, then backend connectivity is reported per store.
2. Given a locally accelerated persistent store is configured, when health checks run, then polling worker status and polling lag are reported.
3. Given quotas are configured, when writes exceed value, entry, or store-size limits, then writes fail with quota or size errors.
4. Given the dashboard is enabled, when an operator views a store, then quota usage and polling lag are visible when the backend can report them.

### Story 15: Use EF Core As Durable Relational Backend

User story: As an application developer, I want an EF Core key/value backend, so that production application state can be shared safely across app nodes.

Acceptance criteria:

1. Given two app nodes use the same EF Core backing store, when Node A writes a key, then Node B can read the value from the persistent backend.
2. Given Node A writes a key through the locally accelerated persistent store, when the write succeeds, then the EF Core transaction includes both the value write and change-log append.
3. Given Node B has the key in its local read layer, when Node B polls the EF Core change log after Node A writes, then Node B evicts the stale entry.
4. Given the EF Core backend is configured for multi-node local acceleration, when atomic value write plus change-log append cannot be guaranteed, then startup or registration fails.
5. Given a node restarts, when it resumes polling, then it loads its EF Core checkpoint and continues from the last processed sequence.
6. Given a production locally accelerated persistent store is configured without explicit node identity configuration or explicit opt-in to a built-in `INodeIdProvider`, when startup runs, then startup fails with a clear configuration error.
7. Given `WithDirectEntityFrameworkClient<AppDbContext>` or `WithEntityFrameworkClient<AppDbContext>` is registered, when `AppDbContext` does not implement `IKeyValueStoreDbContext`, then registration fails with a clear configuration error.
8. Given the EF client performs concurrent operations, when contexts are resolved, then each operation or backend batch owns a separate DI scope and `AppDbContext`.
9. Given the application context exposes the required storage sets, when migrations are created, then the application-owned migration contains entry, change-log, and checkpoint tables.

### Story 16: Use Azure Table Storage As Azure Backend

User story: As an Azure application developer, I want an Azure Table Storage key/value backend, so that cloud-hosted application state can use a lightweight Azure-native store.

Acceptance criteria:

1. Given two app nodes use the same existing Azure Storage account and backend-managed table configuration, when Node A writes a key, then Node B can read the value from the persistent backend.
2. Given the configured storage account exists and required tables are missing, when backend initialization runs with table creation enabled, then the backend creates the required tables idempotently.
3. Given Node A writes a key through the locally accelerated persistent store, when the write succeeds, then the Azure Table Storage batch includes both the value entity change and the change-log entity.
4. Given Node B has the key in its local read layer, when Node B polls the matching table shard change log after Node A writes, then Node B evicts the stale entry.
5. Given an Azure Table Storage backend is configured for multi-node local acceleration, when value and change-log entities cannot be written in the same partition batch, then startup or registration fails.
6. Given multiple shards are configured, when polling runs, then checkpoints are tracked per store, node, and shard.
7. Given table creation is disabled and required tables do not exist, when backend initialization runs, then startup fails with a clear configuration or backend error.
8. Given a workload requires native change feed or richer global distribution, when choosing an Azure backend, then Cosmos DB is considered as a separate backend option rather than the default.

### Story 17: Add Many Keys With Bounded Bulk Writes

User story: As an application developer, I want to set many independent keys through a bounded bulk operation, so that large imports use fewer backend round trips without creating unbounded tasks or memory pressure.

Acceptance criteria:

1. Given a valid collection within configured limits, when `SetManyAsync` runs, then it returns one item Result per input item in input order.
2. Given an EF persistent backend, when a backend batch is written, then existing entries are loaded with a set-based query and successful entries plus their change-log rows are persisted with one primary `SaveChangesAsync` and one transaction commit.
3. Given a locally accelerated persistent store, when bulk items commit successfully, then the writing node updates local cache only for those successful items and other nodes invalidate them through one change-log entry per key.
4. Given `BatchSize` is 100 and `MaxConcurrentBatches` is two, when more than 200 items are supplied, then no more than two bounded backend batches from that request execute concurrently and unscheduled input remains bounded.
5. Given one item has an invalid value or expected-version conflict in `Continue` mode, when the bulk write completes, then that item fails and independent valid items are still processed.
6. Given `StopScheduling` mode and a completed backend batch contains a failure, when orchestration continues, then already running batches quiesce, no new batches start, and unscheduled items receive `KeyValueStoreBulkWriteSkippedError`.
7. Given a request contains duplicate keys, when validation runs, then the outer Result fails before backend I/O.
8. Given request count, batch size, or concurrency exceeds configured caps, when validation runs, then the outer Result fails with `KeyValueStoreBulkWriteLimitExceededError`.
9. Given an input source is larger than one materialized bulk request, when `SetManyBatchesAsync` is used, then input and results are consumed incrementally without accumulating the complete source.
10. Given an Azure Table backend, when items span partitions, then the backend creates bounded same-partition transactions and does not claim cross-partition atomicity.
11. Given compression or encryption behaviors are enabled, when a bulk write runs, then every successful item is transformed in the same order and with the same metadata as an equivalent `SetAsync`.
12. Given metrics and logs are inspected, when bulk writes have run, then batch counts, sizes, concurrency, latency, and outcomes are visible without value content or high-cardinality key metric labels.
13. Given several DI scopes submit bulk writes to the same named store, when they overlap, then all requests share the store-wide active and queued backend-batch limits.
14. Given the shared backend-batch queue is full, when another batch is submitted, then its items receive `KeyValueStoreBulkWriteOverloadedError` without persistence backend I/O.
15. Given a backend batch waits longer than `BulkWriteQueueWaitTimeout`, when admission expires, then its items receive `KeyValueStoreBulkWriteAdmissionTimeoutError` without persistence backend I/O.

### Story 18: Store And Read Typed Values

User story: As an application developer, I want the client to serialize typed values, so that application code can use key/value storage without repeating JSON and byte-conversion plumbing.

Acceptance criteria:

1. Given no serializer override, when `SetAsync<T>` writes a value, then the named client serializes it with `SystemTextJsonSerializer` and stores `application/json` and UTF-8 metadata.
2. Given a compatible stored typed value, when `GetAsync<T>` succeeds, then it returns the deserialized `T` directly.
3. Given application code also needs version or expiry metadata, when `GetEntryAsync<T>` succeeds, then it returns `KeyValueEntry<T>` with the deserialized value and original metadata.
4. Given a custom `ISerializer` is configured for one named client, when that client's generic operations run, then they use that serializer without changing another named client.
5. Given serialization throws, deserialization returns null, or content type is incompatible, when a typed operation runs, then it returns `KeyValueStoreSerializationError` without exposing value content.
6. Given raw text or opaque bytes are required, when the caller uses the non-generic API, then `KeyValueValue.FromString` and `KeyValueValue.FromBytes` preserve the canonical binary behavior.
7. Given a generic bulk request, when items are serialized, then each serialization failure is reported at the original item index and valid items follow the configured bulk failure mode.
8. Given compression or encryption behaviors are enabled, when a typed value is written and read, then serialization precedes write transforms and deserialization follows reverse read transforms.
9. Given `KeyValueStoreProvider` implements `IKeyValueStoreProvider`, when it stores a typed value, then it uses the selected named client's typed API and does not serialize the value twice.

### Story 19: Clean Up Expired Entries In A Hosted Service

User story: As an application operator, I want expired physical entries cleaned by a standard DevKit background service, so that storage remains bounded without affecting logical expiry correctness or backend stability.

Acceptance criteria:

1. Given Key/Value Storage is registered, when the service collection is built, then exactly one `KeyValueStoreExpiryCleanupBackgroundService` is registered as `IHostedService` for the process.
2. Given retention is enabled, when the host starts, then cleanup waits for application startup, honors `StartupDelay`, and repeats after each configured `SweepInterval`.
3. Given retention is disabled, when the host runs, then the cleanup service remains dormant and health reports healthy/not-applicable.
4. Given a named store has `EnableExpiryCleanup = false` or lacks `SupportsExpiryCleanup`, when a sweep runs, then that store is skipped without failure.
5. Given an entry expired within its `ExpiredEntryRetention` window, when a sweep runs, then it remains physically stored while reads still treat it as absent.
6. Given an entry expired on or before the calculated retention cutoff, when cleanup wins the conditional delete, then the entry and required `Expire` change-log record commit atomically.
7. Given an entry is refreshed, replaced, touched, or deleted after cleanup selected it, when conditional deletion runs, then cleanup skips it and does not delete newer state.
8. Given several nodes sweep the same backend, when they select the same expired entry, then cleanup remains idempotent and only the winning removal appends a change-log record.
9. Given more eligible entries exist than one bounded sweep allows, when `BatchSize` or `MaxBatchesPerStore` is reached, then the result reports `HasMore` and remaining work waits for a later iteration.
10. Given one store returns an expected failure, when a sweep continues, then the failure is recorded and later registered stores are still processed.
11. Given the host requests shutdown, when cleanup is running or delaying between batches, then cancellation reaches the backend and shutdown completes within `StopTimeout`.
12. Given an unexpected non-cancellation exception occurs, when the scheduled boundary handles it, then the failure is logged without value content and the service retries on the next interval.
13. Given cleanup is enabled, when health, metrics, logs, or the dashboard are inspected, then the latest sweep time, duration, counts, `HasMore`, and failed stores are visible.
14. Given `SweepOnceAsync` is invoked manually while a scheduled sweep is active, when it requests execution, then the shared service gate prevents overlapping in-process cleanup iterations.
15. Given Node A wins a cleanup deletion, when its transaction commits, then the `ExpiryCleanup` change is durably visible to polling workers on every node.
16. Given the cleanup worker runs inside Node A, when Node A later polls that change, then cleanup origin metadata prevents the event from being mistaken for an already-applied client mutation.
17. Given Node B holds positive, counter, or negative local state for the cleaned key, when it processes the cleanup change, then all such local state is evicted idempotently.
18. Given a concurrent writer creates a newer version after cleanup selected the expired version but before deletion, when cleanup performs its conditional delete, then it skips the newer version and does not append a cleanup change for the skipped item.
19. Given cleanup commits and a writer subsequently re-creates the key, when a delayed cleanup invalidation evicts the newer local entry, then it cannot delete the newer backend version and the next read restores that version.

## Resolved Design Decisions

* `LocalReadTtl` replaces cache-oriented option naming and defaults to 30 seconds for locally accelerated persistent stores.
* Production locally accelerated persistent stores require explicit stable node identity configuration or explicit opt-in to a built-in `INodeIdProvider`.
* `INodeIdProvider` belongs in `Common.Utilities` so other features can reuse stable node identity resolution.
* `KeyValueStoreDefaults.MaxValueSizeInBytes` defaults to 1 MB.
* `Delete` returns `Result<bool>` where `true` means an existing value or counter was removed and `false` means the key was already absent or expired.
* `BackendOnly` replaces `BypassCache` and strictly avoids local-read-layer reads and writes.
* Dashboard authorization uses `KeyValueStorage.Read` and `KeyValueStorage.Manage`.
* Full dashboard value inspection defaults to a 64 KB display limit and truncates values above that limit.
* Dashboard edits do not capture operator reasons.
* Key-change observer registration uses a storage-specific adapter over `SimpleNotifier`.
* The built-in compression behavior uses `CompressionHelper` GZip APIs.
* The built-in encryption behavior resolves key ids through a caller-provided resolver with a store-name default.
* Single-flight is core local-acceleration behavior and enabled by default.
* Negative caching is optional and disabled by default.
* Expiry correctness is based on read-time logical expiry, not cleanup timing.
* Physical expiry cleanup runs through one `KeyValueStoreExpiryCleanupBackgroundService` per process.
* The cleanup worker derives from the shared `PeriodicBackgroundService` and reuses `StorageRetentionOptions`.
* Cleanup is bounded per store, sequential across stores by default, and safe to run on multiple nodes without leader election.
* `ExpiredEntryRetention` delays physical deletion only; it never extends logical visibility.
* Cleanup deletions publish `ExpiryCleanup` change-log entries with a cleanup-worker origin, ensuring the cleanup host and all other nodes invalidate local state.
* Cross-node invalidation evicts positive values, counters, and cached not-found/existence state for the changed key.
* Per-key invalidation generations prevent reads started before an observed change from repopulating stale local state afterward.
* Local caches converge through invalidation and demand loading; the feature does not replicate newly written values into every node's memory.
* The existing `ICacheProvider` abstraction is renamed to `IKeyValueStoreProvider`; the target API no longer presents ordinary typed state as inherently cached.
* `KeyValueStoreProvider` implements the simple provider contract over `IKeyValueStoreClient`.
* `IKeyValueStoreBackend` replaces the former internal `IKeyValueStoreProvider` name so the simple provider and backend SPI cannot be confused.
* `IKeyValueStorePersistenceBackend` and `IKeyValueStoreMaintenanceBackend` consistently name lower-level storage SPIs.
* The locally accelerated persistent store composes over `IKeyValueStorePersistenceBackend`; durable storage mechanics stay separate from local read policy.
* Named application clients resolve through `IKeyValueStoreClientFactory`, backed by keyed DI in the same style as Blob Storage.
* Storage shape is explicit in registration through `WithInMemoryClient`, concise locally accelerated store methods, or explicit `WithDirect...Client` methods.
* `WithEntityFrameworkClient<TContext>` is the primary locally accelerated persistent EF registration; `WithDirectEntityFrameworkClient<TContext>` explicitly opts out of local acceleration and change-log polling.
* `WithAzureTableClient` is the primary locally accelerated persistent Azure registration; `WithDirectAzureTableClient` explicitly opts out of local acceleration and change-log polling.
* Consuming applications own EF options, the `IKeyValueStoreDbContext` implementation, and migrations.
* EF persistence operations own their DI scope and context and do not retain or share a caller's injected `DbContext`.
* Raw byte-oriented operations remain the canonical storage contract; generic methods are client conveniences over that contract.
* Typed operations use a per-named-client `ISerializer`, defaulting to `SystemTextJsonSerializer`.
* Typed values do not persist CLR type names or key/value-specific discriminators; callers provide the expected generic type.
* Persistence backends remain serializer-agnostic and expose no generic backend operations.
* High-volume value additions use core `SetManyAsync` and `WriteManyAsync` contracts rather than a buffering client behavior.
* One materialized bulk request defaults to at most 1000 items.
* Backend batch size defaults to 100 per call and is capped by a store default of 250.
* Concurrent backend batches default to one per call and are capped by a store default of four.
* The store default of four is process-wide per named store across all explicit bulk requests, not a multiplier per request.
* At most 16 backend batches wait in the shared named-store scheduler by default.
* A backend batch waits at most 30 seconds for shared scheduler admission by default.
* Bulk writes return ordered per-item Results and do not promise cross-key atomicity.
* Duplicate keys within one materialized bulk request are rejected before backend I/O.
* Every successful bulk item retains atomic state change plus change-log append.
* Datasets larger than one request use the bounded `SetManyBatchesAsync` extension.

## Implementation Notes

* Keep the feature strictly exact-key based.
* Implement the EF Core backend for durable relational production use.
* Implement Azure Table Storage as the Azure cloud-native backend.
* Treat locally accelerated persistent cross-process/node behavior as required, not optional.
* Do not add listing, querying, or scan APIs to the application-facing client.
* Keep operational listing scoped to `IKeyValueStoreMaintenanceService` and the persistent maintenance backend.
* Do not add message-broker invalidation as the default invalidation mechanism.
* Add a reusable common `INodeIdProvider` abstraction in `Common.Utilities` and use it for key/value node identity.
* Use `SimpleNotifier` only behind the key/value observer adapter for local in-process key-change observer fan-out.
* Do not use key-change observers for critical durable workflows.
* Implement client behaviors as decorators around `IKeyValueStoreClient`.
* Rename repository uses of `ICacheProvider` to `IKeyValueStoreProvider` and rename general-provider implementations plus their decorators according to the migration table.
* Implement `KeyValueStoreProvider` over `IKeyValueStoreClient`.
* Rename the internal storage SPI to `IKeyValueStoreBackend`, including capabilities, errors, factories, diagnostics, and tests.
* Rename public local-acceleration options consistently, including `LocalReadTtl` and `KeyValueReadConsistency.BackendOnly`.
* Implement `KeyValueStoreExpiryCleanupBackgroundService` over `PeriodicBackgroundService` and register it once with `TryAddEnumerable`.
* Reuse `StorageRetentionOptions`; do not implement another timer loop or cleanup scheduler.
* Implement `IKeyValueStoreExpiryCleanupBackend` for EF and Azure Table Storage with bounded, conditional, multi-node-safe deletion.
* Resolve each cleanup backend from a worker-owned asynchronous DI scope and never retain a scoped backend or `DbContext` on the singleton worker.
* Mark physical cleanup change-log rows as `KeyValueChangeOriginKind.ExpiryCleanup` and give the cleanup worker an origin identity distinct from application `NodeId` values.
* Implement one local key invalidation operation that removes positive values, counters, and negative entries and advances a per-key invalidation generation.
* Fence positive and negative cache population with the captured invalidation generation so an older in-flight backend read cannot repopulate stale state after polling.
* Implement `IKeyValueStoreClientFactory` as a scoped keyed-DI resolver with backend-neutral registration descriptors and duplicate-name validation.
* Implement backend-specific fluent registration extensions for in-memory, EF, and Azure Table direct/locally accelerated shapes.
* Implement EF backends over `IServiceScopeFactory`; do not capture a scoped consuming `DbContext` in a longer-lived backend or client.
* Record backend-specific registration and operation-owned EF context lifetime as an ADR when implementation begins.
* Implement typed operations in the client by converting to or from the existing raw value models exactly once.
* Reuse the existing `ISerializer` and `SerializerExtensions` byte helpers; typed client code must not call `JsonSerializer` directly.
* Snapshot and validate the per-client `KeyValueSerializationOptions` during registration; do not read mutable options during each operation.
* Ensure typed read content-type validation occurs after reverse transforms and before deserialization.
* Keep compression, encryption, checksum verification, negative caching, and custom behaviors backend-neutral.
* Keep core local-acceleration single-flight backend-neutral across durable backends.
* Implement bulk-write validation and bounded scheduling in the core client/backend orchestration.
* Register the core bulk scheduler as shared singleton state keyed by normalized store name so scoped clients cannot bypass store-wide limits.
* Implement optimized `WriteManyAsync` persistence paths for in-memory, EF Core, and Azure Table Storage.
* Ensure every client behavior forwards both `SetManyAsync` overloads; transform behaviors process raw items independently while preserving indexes.
* Do not implement bulk writes as an unbounded background queue or delayed implicit flush of ordinary `SetAsync` calls.
* Do not use unbounded `Task.WhenAll` for bulk items or backend batches.
* Add health checks for persistent and locally accelerated persistent stores.
* Add dashboard visibility for quotas, local-read statistics, and polling lag.
* Do not add soft-delete or maintenance audit persistence to this focused feature.
* Do not create new projects unless the implementation plan explicitly requires it.
* Prefer existing `Result` and storage conventions.
* Add focused unit tests for validators, in-memory behavior, expiry, touch/expire, get-or-set, counters, concurrency, default and custom typed serialization, content-type mismatch, null deserialization, typed metadata reads, typed bulk item failures, client behaviors, encryption behavior, typed logging, `IKeyValueStoreProvider` behavior, maintenance service behavior, cleanup scheduling, retention cutoffs, bounded cleanup batches, conditional delete races, cleanup failure isolation, cancellation, shutdown timeout, cleanup diagnostics, bulk request limits, duplicate-key rejection, ordered item results, failure modes, bounded scheduling, EF Core atomic write/change-log behavior, change-log polling, checkpoint recovery, and locally accelerated persistent orchestration.
* Add deterministic scheduler tests proving store-wide limits across DI scopes, named-store isolation, oldest-first admission, queue-full and queue-timeout per-item failures, cancellation cleanup, and permit release after Result failure or exception.
* Add integration tests using at least two locally accelerated persistent store instances against the same EF Core backing store to prove cross-node invalidation for value writes, bulk value writes, and counter writes.
* Add multi-node cleanup integration tests proving concurrent workers cannot delete refreshed entries or append duplicate `Expire` change-log records, every node including the cleanup host evicts local state, negative entries converge after re-creation, and in-flight reads cannot repopulate pre-invalidation state.
* Add EF bulk tests proving one set-based existing-row query, one primary `SaveChangesAsync`, and one commit per successful backend batch; rollback must mark all attempted items in a failed transaction without partial state/change-log commits.
* Add Azure Table Storage backend tests for backend-managed table initialization, same-partition value/change-log batch writes, bulk partition grouping and action limits, counter/change-log writes, shard checkpointing, ETag concurrency, and multi-instance invalidation.
* Keep Cosmos DB out of the default Azure backend scope for this feature.
* Run `dotnet build --nologo /p:UseSharedCompilation=false` as the broad validation command for implementation changes.

## Usage Scenarios

These scenarios demonstrate the intended application-facing API. Type and method names shown here are normative unless a later implementation plan records and updates an intentional naming change.

The examples use a named locally accelerated persistent store called `application-state`. The same client code works with in-memory and direct persistent backends, subject to the capabilities reported for that store.

### Register And Resolve A Named Client

Register the backend, hosted cleanup schedule, limits, and optional behaviors during application startup:

```csharp
services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        configuration.GetConnectionString("DefaultConnection")));

services.AddKeyValueStorage(options =>
    options.WithRetention(retention =>
    {
        retention.SweepInterval = TimeSpan.FromMinutes(30);
        retention.BatchSize = 500;
        retention.MaxBatchesPerStore = 4;
        retention.BatchDelay = TimeSpan.FromMilliseconds(100);
    }))
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithRetryBehavior()
    .WithTimeoutBehavior()
    .WithEntityFrameworkClient<AppDbContext>(
        "application-state",
        options =>
        {
            options.NodeId = configuration["KeyValue:NodeId"];
            options.DefaultTimeToLive = TimeSpan.FromHours(1);
            options.LocalReadTtl = TimeSpan.FromSeconds(30);
            options.ExpiredEntryRetention = TimeSpan.FromDays(1);
            options.EnableExpiryCleanup = true;
            options.MaxValueSizeInBytes = ByteSize.Megabytes(1);
            options.MaxBulkWriteItems = 1000;
            options.MaxBulkWriteBatchSize = 250;
            options.MaxBulkWriteConcurrency = 4;
            options.MaxQueuedBulkWriteBatches = 16;
        });

services.AddKeyValueStoreProvider(
    "application-state",
    options => options.KeyPrefix = "provider/");
```

`AppDbContext` implements `IKeyValueStoreDbContext` and exposes the DevKit entry, change-log, and checkpoint sets as shown in the registration section. The application owns the corresponding EF migration.

Resolve the named client once for an application service:

```csharp
public sealed class CustomerPreferenceService
{
    private readonly IKeyValueStoreClient keyValues;

    public CustomerPreferenceService(IKeyValueStoreClientFactory clientFactory)
    {
        this.keyValues = clientFactory.CreateClient("application-state");
    }
}
```

Applications with only one store may inject a default client if the final registration API provides one. Named resolution remains the unambiguous form for applications with multiple stores.

The factory resolves keyed DI registrations internally. Direct keyed injection is an optional equivalent when the store name is fixed at compile time:

```csharp
public sealed class CustomerPreferenceService(
    [FromKeyedServices("application-state")] IKeyValueStoreClient keyValues)
{
    // Use keyValues directly.
}
```

Prefer the factory when the store name is selected at runtime, when application code should not depend on DI attributes, or when registration metadata is needed.

### Store, Read, And Delete A Normal Value

Store a typed value with expiry and operational tags:

```csharp
var preferences = new CustomerPreferences
{
    Theme = "dark",
    Locale = "de-DE"
};

var writeResult = await keyValues.SetAsync<CustomerPreferences>(
    "customers/42/preferences",
    preferences,
    new KeyValueWriteOptions
    {
        TimeToLive = TimeSpan.FromDays(30),
        Tags = new Dictionary<string, string>
        {
            ["customerId"] = "42",
            ["category"] = "preferences"
        }
    },
    cancellationToken);

if (writeResult.IsFailure)
{
    return writeResult;
}

var version = writeResult.Value.Version;
```

Read and deserialize the value:

```csharp
var readResult = await keyValues.GetAsync<CustomerPreferences>(
    "customers/42/preferences",
    cancellationToken: cancellationToken);

if (readResult.IsFailure)
{
    if (readResult.HasError<KeyValueStoreNotFoundError>())
    {
        // Treat missing and logically expired values as absent.
    }

    return readResult;
}

var storedPreferences = readResult.Value;
```

The named client performs serialization and deserialization through its configured `ISerializer`. The default is `SystemTextJsonSerializer`.

Delete is idempotent and reports whether a non-expired entry existed:

```csharp
var deleteResult = await keyValues.DeleteAsync(
    "customers/42/preferences",
    cancellationToken);

if (deleteResult.IsSuccess && deleteResult.Value)
{
    // The entry existed and was removed.
}
```

Use `ExistsAsync` instead of calling `GetAsync` when content is not needed:

```csharp
var existsResult = await keyValues.ExistsAsync(
    "customers/42/preferences",
    cancellationToken: cancellationToken);

if (existsResult.IsSuccess && existsResult.Value)
{
    // The key exists and is not logically expired.
}
```

### Store Raw Text, JSON, Or Binary

Use the non-generic overload when the application already owns the serialized representation or needs precise content metadata:

```csharp
var rawJson = """
    {
      "theme": "dark",
      "locale": "de-DE"
    }
    """;

var jsonResult = await keyValues.SetAsync(
    "customers/42/preferences/raw",
    KeyValueValue.FromString(
        rawJson,
        Encoding.UTF8,
        "application/json"),
    cancellationToken: cancellationToken);

var binaryResult = await keyValues.SetAsync(
    "imports/2026-08/checksum",
    KeyValueValue.FromBytes(
        checksumBytes,
        "application/octet-stream"),
    cancellationToken: cancellationToken);
```

Read raw values with the non-generic `GetAsync` and decode them according to their metadata:

```csharp
var rawResult = await keyValues.GetAsync(
    "customers/42/preferences/raw",
    cancellationToken: cancellationToken);

if (rawResult.IsSuccess)
{
    var json = Encoding.UTF8.GetString(rawResult.Value.Value.Content);
}
```

Raw JSON written with a compatible content type may also be read through `GetAsync<CustomerPreferences>`. Plain text and opaque bytes should remain on the raw API.

### Create A Coordination Flag Once

Use `SetIfAbsent` for an idempotency marker or atomic flag:

```csharp
var markerResult = await keyValues.SetAsync(
    $"checkout/idempotency/{requestId}",
    KeyValueValue.FromString("accepted"),
    new KeyValueWriteOptions
    {
        SetIfAbsent = true,
        TimeToLive = TimeSpan.FromHours(24)
    },
    cancellationToken);

if (markerResult.HasError<KeyValueStoreConflictError>())
{
    // This request id was already accepted.
}
else if (markerResult.IsFailure)
{
    return markerResult;
}
```

`SetIfAbsent` coordinates only creation of this exact key. It is not a distributed lock and must not be used as a lease without a separate lease design.

### Update With Optimistic Concurrency

Read the current version, modify the value, and require that version during the write:

```csharp
var currentResult = await keyValues.GetEntryAsync<CustomerPreferences>(
    "customers/42/preferences",
    cancellationToken: cancellationToken);

if (currentResult.IsFailure)
{
    return currentResult;
}

var updatedPreferences = new CustomerPreferences
{
    Theme = "light",
    Locale = currentResult.Value.Value.Locale
};

var updateResult = await keyValues.SetAsync<CustomerPreferences>(
    currentResult.Value.Key,
    updatedPreferences,
    new KeyValueWriteOptions
    {
        ExpectedVersion = currentResult.Value.Metadata.Version,
        TimeToLive = TimeSpan.FromDays(30),
        Tags = currentResult.Value.Metadata.Tags
    },
    cancellationToken);

if (updateResult.HasError<KeyValueStoreConcurrencyError>())
{
    // Another writer changed the key. Reload and decide whether to retry.
}
```

The retry decision belongs to the use case because automatically replaying a stale business update may overwrite newer intent.

### Load Or Create A Stored Value

Use `GetOrSetAsync` for an expensive value factory:

```csharp
var catalogResult = await keyValues.GetOrSetAsync<IReadOnlyList<Product>>(
    "catalog/featured",
    async ct =>
    {
        var products = await catalogRepository.LoadFeaturedAsync(ct);

        return Result<IReadOnlyList<Product>>.Success(products);
    },
    new KeyValueWriteOptions
    {
        TimeToLive = TimeSpan.FromMinutes(10)
    },
    cancellationToken);
```

On a locally accelerated persistent store, concurrent misses for the same key on one node share the built-in local single-flight. Cross-node factories can still race, so use set-if-absent semantics when duplicate factory work or replacement is unsafe.

### Choose Local Or Backend Read Consistency

Use the default locally accelerated read for ordinary traffic:

```csharp
var normalResult = await keyValues.GetAsync<IReadOnlyList<Product>>(
    "catalog/featured",
    cancellationToken: cancellationToken);
```

Read the backend and refresh local state when a workflow needs fresher data:

```csharp
var freshResult = await keyValues.GetAsync<IReadOnlyList<Product>>(
    "catalog/featured",
    new KeyValueReadOptions
    {
        Consistency = KeyValueReadConsistency.Fresh
    },
    cancellationToken);
```

Read the backend without reading or updating local state for diagnostics or a one-off verification:

```csharp
var backendOnlyResult = await keyValues.GetAsync<IReadOnlyList<Product>>(
    "catalog/featured",
    new KeyValueReadOptions
    {
        Consistency = KeyValueReadConsistency.BackendOnly
    },
    cancellationToken);
```

`Fresh` and `BackendOnly` do not turn a sequence of operations into a transaction. They control only local-read participation for that exact read.

### Extend Or Shorten Expiry Without Rewriting Content

Extend an entry using a new TTL:

```csharp
var touchResult = await keyValues.TouchAsync(
    "checkout/session/abc123",
    new KeyValueTouchOptions
    {
        TimeToLive = TimeSpan.FromMinutes(30)
    },
    cancellationToken);

if (touchResult.IsFailure)
{
    return touchResult;
}
```

Set an explicit expiry:

```csharp
var expireResult = await keyValues.ExpireAsync(
    "checkout/session/abc123",
    new KeyValueExpireOptions
    {
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        ExpectedVersion = touchResult.Value.Version
    },
    cancellationToken);
```

Both operations preserve stored value bytes. A missing or already expired key returns `KeyValueStoreNotFoundError`.

### Maintain An Atomic Counter

Increment a rate or usage counter and create it when missing:

```csharp
var incrementResult = await keyValues.IncrementAsync(
    "usage/tenant-42/api-calls",
    delta: 1,
    options: new KeyValueCounterOptions
    {
        CreateIfMissing = true,
        InitialValue = 0,
        TimeToLive = TimeSpan.FromDays(1),
        Tags = new Dictionary<string, string>
        {
            ["tenantId"] = "42",
            ["category"] = "usage"
        }
    },
    cancellationToken: cancellationToken);

if (incrementResult.IsSuccess)
{
    var currentCount = incrementResult.Value.Value;
}
```

Decrement and inspect the counter:

```csharp
var decrementResult = await keyValues.DecrementAsync(
    "usage/tenant-42/api-calls",
    delta: 1,
    cancellationToken: cancellationToken);

var counterResult = await keyValues.GetCounterAsync(
    "usage/tenant-42/api-calls",
    cancellationToken: cancellationToken);
```

Counter operations are atomic per exact key. They are not combined atomically with updates to other keys.

### Add A Bounded Collection Of Independent Values

Use `SetManyAsync` when the caller already has a bounded collection:

```csharp
var items = customers.Select(customer => new KeyValueSetItem<Customer>
{
    Key = $"customers/{customer.Id}/summary",
    Value = customer,
    Options = new KeyValueWriteOptions
    {
        TimeToLive = TimeSpan.FromHours(6),
        Tags = new Dictionary<string, string>
        {
            ["import"] = importId,
            ["source"] = "crm"
        }
    }
}).ToArray();

var bulkResult = await keyValues.SetManyAsync(
    items,
    new KeyValueBulkWriteOptions
    {
        BatchSize = 100,
        MaxConcurrentBatches = 2,
        FailureMode = KeyValueBulkWriteFailureMode.Continue
    },
    cancellationToken);

if (bulkResult.IsFailure)
{
    // The complete request was invalid or could not be orchestrated.
    return bulkResult;
}

foreach (var item in bulkResult.Value.Items.Where(item => item.Result.IsFailure))
{
    logger.LogWarning(
        "Key/value import item failed at index {Index} for key {Key}",
        item.Index,
        item.Key);
}
```

The outer Result represents request-level orchestration. Individual validation, conflict, quota, overload, timeout, transform, and backend outcomes are represented by each item Result.

### Stream A Large Import In Bounded Requests

Use `SetManyBatchesAsync` when input is larger than `MaxBulkWriteItems` or comes from an asynchronous source:

```csharp
async IAsyncEnumerable<KeyValueSetItem<ImportRow>> ReadImportAsync(
    CancellationToken cancellationToken)
{
    await foreach (var row in importer.ReadRowsAsync(cancellationToken))
    {
        yield return new KeyValueSetItem<ImportRow>
        {
            Key = $"imports/{importId}/rows/{row.Id}",
            Value = row,
            Options = new KeyValueWriteOptions
            {
                TimeToLive = TimeSpan.FromDays(7)
            }
        };
    }
}

await foreach (var batchResult in keyValues.SetManyBatchesAsync(
    ReadImportAsync(cancellationToken),
    new KeyValueBulkWriteOptions
    {
        BatchSize = 100,
        MaxConcurrentBatches = 2,
        FailureMode = KeyValueBulkWriteFailureMode.Continue
    },
    cancellationToken))
{
    if (batchResult.IsFailure)
    {
        // Stop, retry, or record the failed bounded request.
        break;
    }

    importProgress.Add(
        succeeded: batchResult.Value.SucceededCount,
        failed: batchResult.Value.FailedCount,
        skipped: batchResult.Value.SkippedCount);
}
```

The extension does not accumulate the complete import. It buffers and executes bounded outer requests while the shared store scheduler limits backend pressure across all callers.

### Use The Simple Key/Value Provider

Components that need only ordinary typed key/value semantics should use `IKeyValueStoreProvider`:

```csharp
await provider.SetAsync(
    "catalog:featured",
    products,
    slidingExpiration: TimeSpan.FromMinutes(5),
    absoluteExpiration: DateTimeOffset.UtcNow.AddHours(1),
    cancellationToken: cancellationToken);

var featuredProducts = await provider.GetAsync<IReadOnlyList<Product>>(
    "catalog:featured",
    cancellationToken);
```

`KeyValueStoreProvider` delegates these operations to the selected named client. The simple provider intentionally does not expose versions, tags, counters, consistency modes, or per-item bulk Results.

### Select The Appropriate Operation

| Need | Operation |
| --- | --- |
| Store or replace one typed value | `SetAsync<T>` |
| Read one typed value | `GetAsync<T>` |
| Read one typed value with version and metadata | `GetEntryAsync<T>` |
| Store or read caller-owned text, JSON, or binary | Non-generic `SetAsync` / `GetAsync` using the `KeyValueValue` model |
| Probe without loading content | `ExistsAsync` |
| Remove one exact key | `DeleteAsync` |
| Create an exact key only when absent | `SetAsync` with `SetIfAbsent` |
| Prevent accidental overwrite | `SetAsync` with `ExpectedVersion` |
| Load an existing typed value or run one local single-flight factory | `GetOrSetAsync<T>` |
| Refresh expiry without rewriting value bytes | `TouchAsync` or `ExpireAsync` |
| Mutate one atomic numeric value | `IncrementAsync` or `DecrementAsync` |
| Add one homogeneous typed collection efficiently | `SetManyAsync<T>` |
| Add one heterogeneous or pre-serialized collection efficiently | Non-generic `SetManyAsync` |
| Consume a large or asynchronous import with bounded memory | `SetManyBatchesAsync<T>` or its raw overload |
| Use ordinary typed key/value semantics | `IKeyValueStoreProvider` implemented by `KeyValueStoreProvider` |
