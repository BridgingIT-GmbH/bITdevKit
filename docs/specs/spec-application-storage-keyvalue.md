---
status: draft
---

# Design Specification: Key/Value Storage

> Provide Result-native, provider-neutral key/value storage with string, binary, and atomic counter values, local in-memory speed, durable backends, and lightweight multi-node cache invalidation.

[TOC]

## Overview

Key/Value Storage is a focused storage feature for fast exact-key access to small to medium sized values and atomic numeric counters.

The feature captures the Redis-shaped storage use case that belongs in the devkit: fast exact-key access to small temporary or cache-like values, atomic counters, expiration, provider-neutral persistence, local acceleration, and safe multi-node invalidation.

The feature is not a Redis-compatible server and does not duplicate existing queueing, messaging, jobs, durable workflow orchestration, or broker capabilities. It does not provide scripting, streams, pub/sub, clustering, or application-facing query capabilities. It provides the complete target design for a focused Result-native key/value and counter abstraction with pluggable providers.

The feature supports three provider shapes:

* Pure in-memory provider.
* Persistent provider.
* Cached persistent provider.

Pure in-memory is a first-class provider. It is local-only, non-durable, and non-shared across nodes. It is suitable for tests, development, ephemeral state, and single-process use cases.

Persistent providers store values in a durable backend and use the backend as the source of truth.

Cached persistent providers combine a durable backend with a per-node local in-memory cache. They are the production default for load-balanced applications that need fast reads and durable storage.

Because this feature is primarily a shared state and cache store, production correctness across processes and load-balanced nodes is a core requirement. Entity Framework Core is the durable relational backend because EF Core can provide the required atomic value write plus change-log append in one database transaction. Azure Table Storage is the Azure cloud-native backend because it is lightweight, Azure-native, cost-conscious, and fits the explicit change-log polling model.

Multi-node cache invalidation is based on a lightweight backend change log and polling worker. The existing message broker may be supported as an optional invalidation adapter, but it is not the default invalidation mechanism because full broker persistence, handler tracking, retries, and operational visibility are too heavy for ordinary cache invalidation.

## Goals

The goals of this feature are:

* Provide a provider-neutral key/value storage abstraction.
* Keep all public APIs Result-native.
* Support string values, binary values, and numeric counters.
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
* Clean up expired entries in the background.
* Support retention cleanup for expired entries.
* Support maximum value size validation.
* Support optional per-store quotas.
* Support key validation.
* Support provider-neutral checksum/hash verification.
* Support optional compression and value encryption through client behaviors.
* Provide an extensible key/value client behavior system consistent with other devkit storage features.
* Expose internal key/value operation flow through typed logging.
* Provide a built-in value encryption behavior based on `EncryptionHelper`.
* Provide an `ICacheProvider` implementation backed by `IKeyValueStoreClient` for distributed cache scenarios.
* Support optimistic concurrency through expected version checks.
* Support optional set-if-absent semantics.
* Support local single-flight for duplicate concurrent cache misses as core cached-provider behavior.
* Support optional short-lived negative caching for repeated not-found reads.
* Provide a pure in-memory provider.
* Provide a durable persistent provider contract.
* Provide a cached persistent provider for production multi-node applications.
* Provide Entity Framework Core as the durable relational persistent backend.
* Provide Azure Table Storage as the Azure cloud-native persistent backend.
* Make cross-process and cross-node operation a hard requirement for the production cached persistent provider.
* Use write-through semantics for cached persistent writes.
* Require atomic value write plus change-log append for cached multi-node providers.
* Support eventual cross-node consistency by default.
* Support explicit fresh/backend reads when stronger read consistency is needed.
* Use backend change-log polling for lightweight cross-node invalidation.
* Support optional local key-change observation after change-log polling sees a change.
* Use a key/value-specific observer adapter over `SimpleNotifier` for in-process key-change fan-out when observation is enabled.
* Persist change-log checkpoints per store and node identity when supported.
* Support configurable node identity for production deployments.
* Use local TTL as a fallback when polling is delayed or invalidation is missed.
* Provide deterministic recovery when a node misses retained change-log history.
* Provide health checks for backend, change log, checkpoints, polling, and cache health.
* Show polling lag and cache health in the Razor dashboard.
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
* Application-facing querying.
* Application-facing listing.
* Application-facing prefix scans.
* Application-facing full-store scans.
* Secondary indexes.
* Search.
* Range reads.
* Append-only value mutation.
* Provider-specific public APIs.
* Replacing `ICacheProvider` as the general-purpose devkit cache abstraction.
* Authentication or authorization policy enforcement.
* Encryption key management or rotation orchestration.
* Backup, restore, or provider disaster-recovery orchestration.
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
| Counter | Signed 64-bit numeric value addressed by one exact key and mutated through atomic increment/decrement operations. |
| Entry | Stored key, value, metadata, version, and expiry information. |
| Metadata | Provider-neutral information about an entry. |
| Tag | User-provided metadata label used for operational grouping and dashboard filtering. |
| Version | Provider-neutral optimistic concurrency token for an entry. |
| TTL | Time-to-live after which the entry is treated as expired. |
| Expiry | Absolute timestamp after which a key is treated as missing. |
| In-memory provider | Local provider that stores all entries in process memory only. |
| Persistent provider | Provider that stores entries in a durable backend. |
| Cached persistent provider | Provider that uses a durable backend plus per-node local memory cache. |
| Backend | Durable storage implementation used by persistent providers. |
| Change log | Backend-maintained ordered record of key changes used for cache invalidation. |
| Checkpoint | Last processed change-log sequence for a store and node. |
| NodeId | Stable identifier for an application node/process. |
| NodeId provider | Reusable common abstraction that resolves a stable node identity for features that need node-local checkpointing or self-originated change filtering. |
| Fresh read | Read that bypasses or verifies the local cache by reading the backend. |
| Maintenance service | Privileged operational service that can inspect and mutate persisted key/value entries for dashboards and support tools. |
| Key-change observer | Local in-process handler that reacts when this node observes a backend key change. |
| Behavior | Client decorator that adds or extends cross-cutting behavior such as logging, metrics, compression, encryption, checksum verification, single-flight, or negative caching. |
| Transform behavior | Behavior that changes value bytes on write and reverses the transform on read, such as compression or encryption. |
| Result-native API | API that returns `Result` or `Result<T>` for expected outcomes and failures. |

## Design Principles

* The public API is provider-neutral.
* The public API is Result-native.
* The store is exact-key oriented.
* Atomic counters are first-class exact-key primitives.
* Store names are first-class namespaces.
* Tags are metadata, not application query primitives.
* Missing keys are explicit failures on `Get`.
* `Exists` is the API for boolean existence probing.
* String values are convenience values over the binary storage model.
* Providers should not leak backend-specific concepts into the public client API.
* Operational maintenance APIs are separate from the application-facing client API.
* Maintenance operations work against the persistent backing store, not against per-node local caches.
* Pure in-memory is a valid provider, not only a cache implementation.
* The persistent backend is the source of truth for persistent and cached persistent providers.
* Cached persistent providers are write-through.
* Cached persistent providers use local memory only as an acceleration layer.
* Cached persistent providers use eventual cross-node consistency by default.
* Stronger read consistency is opt-in through read options.
* Backend change-log polling is the default multi-node invalidation mechanism.
* Local key-change observation is built on top of change-log polling.
* Key-change observers are local process notifications, not durable business events.
* Message-broker invalidation is optional adapter behavior, not the default cache invalidation model.
* Expiry correctness must not depend on background cleanup timing.
* Client behaviors are composable decorators around the key/value client.
* The behavior system is the primary extension point for cross-cutting client capabilities.
* Transform behaviors such as compression and encryption must be explicit and ordered.
* Value encryption is client-side behavior by default and should use existing `EncryptionHelper` primitives.
* Internal feature logging uses source-generated typed logging methods so operation flow is visible without allocating ad hoc log messages on hot paths.
* Expected validation, concurrency, and not-found failures are represented as typed Result errors.
* Cancellation remains normal .NET cancellation behavior.

## Naming Convention

The feature name is Key/Value Storage.

Type names may use `Store` when they represent the key/value store abstraction itself. This keeps names such as `IKeyValueStoreClient`, `IKeyValueStoreProvider`, `KeyValueStoreCacheProvider`, and `KeyValueStoreOptions` natural while preserving Key/Value Storage as the feature name.

Feature-level registration, documentation headings, dashboard labels, and authorization policy names may continue to use `KeyValueStorage` where they refer to the overall feature rather than one store contract.

## Relationship To ICacheProvider

`ICacheProvider` remains the general-purpose devkit cache abstraction used by existing requester, identity, document, blob, and application caching behaviors.

Key/Value Storage overlaps with `ICacheProvider` only for the simple case of reading and writing values by key with expiry. The feature exists because distributed key/value state needs a stronger contract than `ICacheProvider` currently exposes.

`ICacheProvider` is intentionally small:

* It stores typed objects through `Get`, `TryGet`, `Set`, and `Remove` operations.
* It supports sliding and absolute expiration.
* It includes broad key enumeration and prefix invalidation helpers.
* It does not expose Result-native failures, metadata, versions, hashes, tags, fresh reads, counters, optimistic concurrency, set-if-absent, change-log invalidation, local cache acceleration semantics, health, quotas, or maintenance inspection.

Key/Value Storage should therefore not replace `ICacheProvider`. Instead:

* Existing code that only needs ordinary cache semantics should continue to depend on `ICacheProvider`.
* Code that needs distributed exact-key state, counters, concurrency, metadata, or operational inspection should use `IKeyValueStoreClient`.
* A key/value-backed `ICacheProvider` implementation must be provided so existing cache consumers can opt into the distributed key/value backend without changing their cache-facing code.
* The adapter should intentionally expose only `ICacheProvider` semantics and must not leak key/value-specific capabilities through `ICacheProvider`.
* Prefix invalidation through the adapter is an adapter concern and must not introduce application-facing scans into `IKeyValueStoreClient`.

In short, `ICacheProvider` is the simple cache facade; Key/Value Storage is the distributed state engine that can also power a cache facade.

This boundary is locked in: existing cache consumers should not move to `IKeyValueStoreClient` unless they need key/value-specific capabilities such as counters, metadata, versions, fresh reads, coordination flags, maintenance visibility, or explicit distributed-state semantics.

## ICacheProvider Adapter

Key/Value Storage must provide an `ICacheProvider` implementation backed by `IKeyValueStoreClient`.

Suggested type name:

```csharp
public sealed class KeyValueStoreCacheProvider : ICacheProvider
{
    // Uses IKeyValueStoreClient for distributed cache entries.
}
```

Adapter rules:

* `Get`, `TryGet`, `Set`, and `Remove` must use `IKeyValueStoreClient` exact-key operations.
* `Set` maps sliding and absolute expiration to `KeyValueWriteOptions`.
* `Remove` maps to `DeleteAsync` and should preserve normal `ICacheProvider` idempotent remove semantics.
* Cached values are serialized through the configured devkit serializer before being stored as `KeyValueValue` bytes.
* Cache entries should use a dedicated configured store name or reserved key prefix so cache data is isolated from application key/value state.
* The adapter must expose only `ICacheProvider` semantics to callers.
* The adapter must not expose versions, tags, counters, maintenance inspection, change-log details, or fresh-read options through `ICacheProvider`.
* The adapter must not require consumers of `ICacheProvider` to reference key/value storage abstractions.
* Adapter logs must use typed logging and must not log value content.

`ICacheProvider` includes `GetKeys` and `RemoveStartsWith`, while `IKeyValueStoreClient` intentionally does not expose listing or prefix scans. The adapter must therefore handle those methods without changing the public key/value client contract.

Allowed adapter strategies:

* Use an internal adapter-owned key index stored in key/value storage under reserved keys.
* Use the maintenance provider internally when the application explicitly enables cache key enumeration for the adapter.
* Return an unsupported-feature failure through internal Result handling and surface the existing `ICacheProvider` behavior chosen for unsupported listing methods only when the adapter is configured to disallow enumeration.

Adapter strategy rules:

* The default distributed cache adapter configuration should support `GetKeys` and `RemoveStartsWith` because they are part of the existing `ICacheProvider` contract.
* Any internal key index must be maintained by adapter writes and deletes, must tolerate stale index entries, and must verify key existence before returning keys or deleting by prefix.
* Prefix invalidation is eventually consistent across nodes and relies on normal key/value delete plus change-log invalidation.
* Adapter key enumeration must be scoped to the adapter cache namespace only.
* Adapter internals must not add listing, querying, prefix scans, or full-store scans to `IKeyValueStoreClient`.

## Provider Shapes

### In-Memory Provider

The in-memory provider stores values in process memory.

Capabilities:

* Exact-key get, set, delete, and exists.
* Atomic increment and decrement within the process.
* String and binary values.
* TTL and expiry.
* Local background cleanup.
* Maximum value size validation.
* Optimistic concurrency within the process.
* Set-if-absent within the process.

Limitations:

* Not durable.
* Not shared across app nodes.
* Not suitable as the source of truth for load-balanced production deployments.
* Change-log polling is not required.

### Persistent Provider

The persistent provider stores values in a durable backend and reads directly from that backend.

Capabilities:

* Exact-key get, set, delete, and exists.
* Atomic increment and decrement when supported by the backend.
* String and binary values.
* TTL and expiry.
* Maximum value size validation.
* Optimistic concurrency when supported by the backend.
* Set-if-absent when supported by the backend.

Limitations:

* Every read hits the backend.
* No local cache acceleration.
* Change-log polling is not required unless the provider is used by a cached persistent provider.

### Cached Persistent Provider

The cached persistent provider combines:

* Shared persistent backend.
* Per-node local in-memory cache.
* Backend change log.
* Polling invalidation worker.
* Local TTL fallback.

This is the default production provider shape for multi-node applications.

Capabilities:

* Fast local reads for cached keys.
* Durable write-through persistence.
* Eventual cross-node consistency.
* Explicit fresh/backend reads.
* Atomic value write plus change-log append.
* Atomic counter update plus change-log append.
* Per-node change-log checkpointing.
* Cache invalidation through local eviction.
* Recovery after node restarts.
* Recovery after missed change-log history.

Provider requirement:

* A backend used by `CachedPersistentKeyValueStoreProvider` must support atomic value write, counter update, touch/expire, and delete plus change-log append.

Backends that cannot provide this capability may still support persistent storage or cached single-node scenarios, but they are not valid for cached multi-node use.

### Entity Framework Core Provider

Entity Framework Core is the durable relational provider.

The EF Core provider must support:

* Persistent key/value entries.
* Atomic value write plus change-log append in one database transaction.
* Atomic delete plus change-log append in one database transaction.
* Atomic touch/expire plus change-log append in one database transaction.
* Atomic increment/decrement plus change-log append in one database transaction.
* Optimistic concurrency through a provider version or row-version column.
* Set-if-absent semantics.
* Atomic counter semantics.
* Store names as namespaces.
* Tags and metadata persistence.
* Expiry filtering.
* Background cleanup of expired entries.
* Change-log paging by monotonically increasing sequence.
* Per-store/per-node checkpoint persistence.
* Maintenance listing, value inspection, and counter inspection.
* Health checks for backend, change log, checkpoint, and polling lag.

The EF Core provider should expose a context contract instead of requiring a concrete devkit-owned DbContext type. The consuming application DbContext opts in by implementing this contract and exposing the required DbSet properties.

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
* Provider-specific overrides may be used when a database provider requires them, but the default EF provider design should be annotation-first.

Required indexes:

* Unique index on `StoreName` and `Key`.
* Index on `StoreName` and `ExpiresAt`.
* Index on `StoreName` and tag storage if tag filtering is implemented relationally.
* Unique or ordered sequence index for change-log entries.
* Unique index on checkpoint `StoreName` and `NodeId`.

The exact entity names, table names, indexes, column lengths, concurrency fields, and JSON-backed helper properties should follow existing EF storage conventions during implementation, with the mapping declared on the entities through annotations by default.

### Azure Table Storage Provider

Azure Table Storage is the Azure cloud-native provider.

The Azure Table Storage provider exists for applications that want a lightweight managed Azure backing store without adopting Cosmos DB as the default cloud provider.

The provider must support:

* Persistent key/value entries.
* Atomic value write plus change-log append using same-partition batch transactions.
* Atomic delete plus change-log append using same-partition batch transactions.
* Atomic touch/expire plus change-log append using same-partition batch transactions.
* Atomic increment/decrement plus change-log append using same-partition batch transactions.
* Optimistic concurrency using Table Storage ETags.
* Set-if-absent semantics using insert semantics.
* Atomic counter semantics using provider transactions or ETag-based concurrency.
* Store names as namespaces.
* Tags and metadata persistence.
* App-managed expiry filtering.
* App-managed background cleanup of expired entries.
* Change-log paging through dedicated change-log entities.
* Per-store/per-node checkpoint persistence.
* Maintenance listing, value inspection, and counter inspection.
* Health checks for table access, change-log reads, checkpoint reads/writes, and polling lag.
* Provider-managed initialization of required tables and table entities within an existing storage account.
* Startup validation for required storage account permissions and table accessibility.

Storage account and table management:

* The consuming application provides an existing Azure Storage account connection or client configuration.
* The provider owns all key/value table setup inside that storage account.
* Required tables must be created by provider initialization when they do not exist and table creation is enabled.
* Provider initialization must be idempotent and safe to run from multiple app nodes during deployment or scale-out.
* The provider should expose configuration for table names or table name prefixes, but should provide sensible defaults.
* The provider must not require operators to manually pre-create key/value tables for normal use.
* If table creation is disabled by configuration, startup must validate that all required tables exist and fail with a clear Result/configuration error when they do not.
* The provider must validate that configured credentials can read, write, update, delete, and create tables when table creation is enabled.
* The provider must validate that configured credentials can read, write, update, and delete required table entities when table creation is disabled.
* Initialization should not create or manage the storage account itself.
* Initialization should not alter unrelated tables in the storage account.
* Table lifecycle cleanup, destructive table deletion, and storage account provisioning remain operational responsibilities outside normal provider startup.

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
* Large value support is bounded by Table Storage entity limits and should use Blob Storage or another provider if values exceed practical table limits.
* Expiry cleanup is app-managed, not a replacement for provider-native TTL.

Cosmos DB is a separate provider option for workloads that need native change feed, richer global distribution, or higher-scale partitioning. It is not the default Azure backend for this feature.

## Public Client API

The public client exposes an exact-key API for values and counters.

```csharp
public interface IKeyValueStoreClient
{
    Task<Result<KeyValueEntry>> GetAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> SetAsync(
        string key,
        KeyValueValue value,
        KeyValueWriteOptions options = null,
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

String values are encoded to bytes and stored with encoding metadata. Providers do not need a separate string storage path.

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

* Provider-neutral SHA-256 hash.
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
* Providers may store tags in the same metadata structure as other entry metadata.

## Read Options

```csharp
public sealed class KeyValueReadOptions
{
    public KeyValueReadConsistency Consistency { get; set; } = KeyValueReadConsistency.Default;

    public bool AllowNegativeCache { get; set; }
}
```

```csharp
public enum KeyValueReadConsistency
{
    Default,
    Fresh,
    BypassCache
}
```

`Default` uses provider-default behavior. For cached persistent providers, this means local cache first, then backend read on miss or expired local entry.

`Fresh` reads the persistent backend and refreshes local cache when the provider has a cache.

`BypassCache` reads the persistent backend and strictly avoids local-cache reads and writes. It does not refresh local cache after a successful backend read.

If a caller wants a backend read that refreshes the local cache, it must use `Fresh`.

Pure in-memory providers treat `Fresh` and `BypassCache` as equivalent to `Default`.

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

Both operations are exact-key operations and must append change-log entries for cached persistent stores.

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
* Persistent and cached persistent counter operations must update the counter and append the change-log entry atomically.
* Cached persistent providers must not satisfy counter writes from local cache only.
* Counter keys share the same namespace as ordinary value keys.

## Provider API

The provider API mirrors the public exact-key behavior and exposes capabilities.

```csharp
public interface IKeyValueStoreProvider
{
    KeyValueStoreProviderCapabilities Capabilities { get; }

    Task<Result<KeyValueEntry>> GetAsync(
        string key,
        KeyValueReadOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueWriteResult>> SetAsync(
        string key,
        KeyValueValue value,
        KeyValueWriteOptions options = null,
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

```csharp
public sealed record KeyValueStoreProviderCapabilities
{
    public bool IsDurable { get; init; }

    public bool IsSharedAcrossNodes { get; init; }

    public bool SupportsLocalCache { get; init; }

    public bool SupportsAtomicWriteWithChangeLog { get; init; }

    public bool SupportsOptimisticConcurrency { get; init; }

    public bool SupportsSetIfAbsent { get; init; }

    public bool SupportsAtomicCounters { get; init; }

    public bool SupportsTouch { get; init; }

    public bool SupportsSlidingExpiration { get; init; }

    public bool SupportsTags { get; init; }

    public bool SupportsBackgroundExpiryCleanup { get; init; }

    public bool SupportsMaintenance { get; init; }

    public bool SupportsHealthCheck { get; init; }
}
```

## Client Behaviors

Key/Value Storage must support a composable client behavior system.

Behaviors are decorators around `IKeyValueStoreClient`. They add cross-cutting capabilities without changing provider contracts or leaking provider-specific concerns into application code.

The behavior model should follow the surrounding storage features:

* Behaviors are registered through the key/value storage builder.
* Behaviors wrap the client in registration order.
* Behaviors can be added by type or factory.
* Behaviors can use dependency injection.
* Behaviors remain provider-neutral.

Suggested registration shape:

```csharp
services.AddKeyValueStorage(builder =>
{
    builder.AddCachedPersistent("default", options =>
    {
        options.StoreName = "default";
    })
    .WithLoggingBehavior()
    .WithMetricsBehavior()
    .WithChecksumVerificationBehavior()
    .WithCompressionBehavior()
    .WithEncryptionBehavior()
    .WithNegativeCacheBehavior();
});
```

The exact builder API should follow existing document, blob, and file storage conventions.

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
* Negative cache behavior.

Behavior rules:

* Compression and encryption are opt-in.
* The built-in compression behavior uses `CompressionHelper` GZip byte/stream APIs.
* Compression is disabled by default and should support a configurable minimum payload size.
* Compression and encryption must record enough metadata to read values back correctly.
* Compression and encryption must not change public key semantics.
* When compression and encryption are both enabled, values are compressed before they are encrypted.
* Checksum verification must validate stored bytes according to the configured transform order.
* Local single-flight is core cached-provider behavior, enabled by default, and scoped per node and per key.
* Local single-flight prevents duplicate concurrent backend loads on the same node and is not enabled through an optional client behavior.
* Negative caching stores short-lived not-found results only.
* Negative caching is optional and disabled by default.
* Negative cache entries must be evicted by local writes and change-log polling.
* Behavior ordering must be explicit because transform behaviors can affect hashes, sizes, and metadata.
* Third-party and application-specific behaviors must be able to wrap all client operations, including counter operations.
* Behaviors that only apply to value content must pass counter operations through unchanged or fail with an unsupported-feature error when explicitly configured to handle counters.
* The built-in encryption behavior uses `EncryptionHelper.AesCbcPkcs7Algorithm`.
* The built-in encryption behavior encrypts `KeyValueValue.Content` bytes and stores transform metadata including algorithm and key id.
* The encrypted byte payload may use the existing `EncryptionHelper` format where the initialization vector is prepended to ciphertext.
* The built-in encryption behavior resolves key ids through a caller-provided resolver.
* The default resolver may derive the key id from the store name.
* Encryption key resolution, storage, rotation, and tenant policy remain outside the provider contract.
* The optional logging behavior logs the outer client call boundary.
* Internal typed logging logs feature internals such as provider selection, cache hit/miss decisions, persistence operations, change-log polling, checkpoint updates, cleanup, maintenance operations, and recovery decisions.

## Convenience Operations

### Counters

`Increment` and `Decrement` mutate one exact-key numeric counter atomically.

Rules:

* Counter operations are writes and must participate in validation, quota, expiry, versioning, health, logging, and metrics.
* Counter operations append change-log entries for cached persistent stores.
* Counter operations evict or update local cache entries on the writing node according to provider policy.
* Counter operations from another node must evict local cached state through change-log polling.
* Counter operations do not use compression or value encryption behaviors by default because atomic numeric mutation requires provider-visible numeric state.

### GetOrSet

`GetOrSet` reads an exact key and stores a generated value when the key is missing or expired.

Rules:

* `GetOrSet` must return the existing non-expired entry when present.
* The value factory runs only on miss or expired entry.
* The value factory returns `Result<KeyValueValue>`.
* Failed value factories must not write a value.
* Cached persistent `GetOrSet` must use local single-flight to prevent duplicate factories on the same node.
* `GetOrSet` is not globally single-flight across nodes.
* Concurrent cross-node `GetOrSet` calls must rely on `SetIfAbsent` or `ExpectedVersion` semantics for correctness.

### Touch And Expire

`Touch` and `Expire` update expiry metadata without rewriting value content.

Rules:

* `Touch` may use the default TTL, supplied TTL, or supplied absolute expiry.
* `Expire` sets an explicit absolute expiry.
* Both operations fail with not-found when the key is missing or already expired.
* Both operations support expected-version checks.
* Both operations append change-log entries for cached persistent stores.
* Both operations update local cache metadata on the writing node.

## Persistence Provider Contract

The cached persistent provider depends on a lower-level persistence provider contract.

For production cached persistent use, the persistence provider must use storage shared by all app nodes and must provide transactional change-log writes. EF Core satisfies this requirement for relational storage.

```csharp
public interface IKeyValuePersistenceProvider
{
    KeyValuePersistenceProviderCapabilities Capabilities { get; }

    Task<Result<KeyValueBackendEntry>> ReadAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<KeyValueBackendWriteResult>> WriteAsync(
        KeyValueBackendWrite write,
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

The application-facing backend API does not expose listing values or listing keys. `ReadChangesAsync` lists change-log entries only for invalidation.

Operational listing and value inspection are exposed through a separate maintenance provider contract.

Delete operations return whether a non-expired value or counter existed before deletion. Missing and already-expired keys return `Result.Success(false)` unless an expected-version or provider-specific concurrency option requires a failure.

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
* Append change-log entries for maintenance writes, counter changes, and deletes so cached nodes invalidate stale local entries.

Maintenance operations must not read from or write only to a local in-memory cache. For cached persistent providers, the persistent backing store remains the source of truth.

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
* Maintenance writes, counter changes, and deletes must append change-log entries when used with cached persistent stores.
* Maintenance operations should capture operator identity when the presentation surface can provide it.
* Maintenance operations do not capture an operator reason.
* Maintenance services should log the operation metadata but must not log value content.

## Maintenance Provider Contract

Persistence providers that support operational maintenance expose a separate maintenance provider contract.

```csharp
public interface IKeyValueMaintenanceProvider
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

Maintenance writes, counter changes, and deletes use the same atomicity requirement as cached persistent writes:

* write/delete persistent entry or update persistent counter
* append change-log entry
* commit both together when the backend supports cached multi-node use

Backends that cannot safely list or inspect values may report `SupportsMaintenance = false`.

## Change Log

Cached multi-node providers require a backend change log.

This is not optional for production cached persistent stores. Without a shared change log, local caches on different app nodes cannot converge reliably.

Each successful state-changing operation appends a change-log entry:

* `Set`
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

Rules:

* Change-log sequence values are monotonically increasing within a backend.
* Cached persistent providers require atomic state change plus change-log append.
* Change-log entries do not include stored value content.
* Invalidation handlers evict local cache entries rather than replicating value data.
* Duplicate change-log processing is safe.
* Out-of-order processing must not corrupt local cache; providers should use sequence order.

## Change-Log Polling

Each cached persistent provider instance runs a polling worker.

The worker:

1. Resolves the configured `NodeId`.
2. Loads the last checkpoint for the store and node identity.
3. Reads change-log entries after the checkpoint sequence.
4. Ignores entries whose `OriginNodeId` matches the local node identity.
5. Evicts changed keys from the local in-memory cache.
6. Publishes an optional local key-change notification after eviction.
7. Saves the checkpoint after processing entries.
8. Repeats after the configured polling interval.

Default behavior:

* Cross-node consistency is eventual.
* Default polling interval is approximately two seconds.
* Polling interval is configurable.
* Local TTL is a separate safety net.

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
services.AddKeyValueStorage(builder =>
{
    builder.AddCachedPersistent("default", options =>
    {
        options.EnableChangeNotifications = true;
    });
});
```

The exact registration API should follow surrounding storage conventions during implementation.

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

* Production cached persistent providers require a stable configured `NodeId` or an explicit opt-in to a built-in `INodeIdProvider`.
* Startup must fail for production cached persistent providers when node identity stability is not explicit.

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
* Cached providers evict expired local entries before returning.

Cleanup behavior:

* Background cleanup physically removes expired entries after they have become logically expired.
* Correctness does not depend on cleanup timing.
* Cleanup may append `Expire` change-log entries when it physically removes backend entries.

## Consistency Semantics

Default consistency:

* Eventual across nodes.
* Read-your-writes for the same provider instance after successful local write.
* Other nodes observe changes after polling processes the backend change log or after local TTL expiry.
* Production cached persistent stores must be validated in a multi-process or multi-provider-instance scenario.

Fresh reads:

* A caller may request a fresh/backend read through `KeyValueReadOptions`.
* Fresh reads read the persistent backend and refresh local cache when applicable.

Write consistency:

* Cached persistent `Set` returns success only after the backend write and change-log append both succeed.
* Cached persistent `Delete` returns success only after the backend delete and change-log append both succeed.

Concurrency:

* `ExpectedVersion` rejects writes when the current non-expired backend version does not match.
* `SetIfAbsent` rejects writes when the key currently exists and is not expired.
* Concurrency failures return typed Result errors.

## Local Cache Semantics

The local cache belongs to the provider instance.

Local cache entries must carry:

* Key.
* Value.
* Metadata.
* Version.
* Expiry.
* Local cache insertion/update timestamp.

The local cache must support:

* Exact-key lookup.
* Exact-key upsert.
* Exact-key eviction.
* Full clear for missed change-log recovery.
* TTL-based expiry.
* Optional size/count limits.

Local cache eviction from change-log polling removes the entry only. It does not load the replacement value.

## Configuration

```csharp
public sealed class KeyValueStoreOptions
{
    public string StoreName { get; set; }

    public string NodeId { get; set; }

    public long MaxValueSizeInBytes { get; set; } = KeyValueStoreDefaults.MaxValueSizeInBytes;

    public long? MaxStoreSizeInBytes { get; set; }

    public long? MaxEntryCount { get; set; }

    public TimeSpan? DefaultTimeToLive { get; set; }

    public TimeSpan LocalCacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan NegativeCacheTtl { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ChangeLogPollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan ChangeLogRetention { get; set; } = TimeSpan.FromDays(1);

    public TimeSpan ExpiredEntryRetention { get; set; } = TimeSpan.FromDays(1);

    public bool EnableBackgroundExpiryCleanup { get; set; } = true;

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

* `StoreName` is required for persistent and cached persistent providers.
* `NodeId` is required for production cached persistent providers unless the application explicitly opts into an `INodeIdProvider`.
* `MaxValueSizeInBytes` must be enforced before provider writes.
* `MaxValueSizeInBytes` is enforced by default and defaults to 1 MB.
* Size options are represented as raw byte counts in `long` values.
* Default and example size calculations should use the common `ByteSize` helper from `Common.Utilities`, such as `ByteSize.Megabytes(1)`.
* Value-size enforcement should use a key/value-specific helper analogous to Blob Storage's `BlobSizeLimit` helper.
* `MaxStoreSizeInBytes` and `MaxEntryCount` are optional store-level quotas and are disabled unless configured.
* `LocalCacheTtl` bounds stale reads when polling is delayed.
* `NegativeCacheTtl` bounds not-found caching when optional negative caching is enabled.
* `ChangeLogRetention` must be longer than expected node restart and deployment windows.
* `ExpiredEntryRetention` controls how long expired physical entries may remain before cleanup.
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
* `KeyValueStoreProviderError`
* `KeyValueStoreUnsupportedFeatureError`
* `KeyValueStoreChangeLogUnavailableError`
* `KeyValueStoreCheckpointError`
* `KeyValueStoreValueTooLargeError`
* `KeyValueStoreQuotaExceededError`
* `KeyValueStoreHashMismatchError`
* `KeyValueStoreTypeMismatchError`
* `KeyValueStoreCounterOverflowError`
* `KeyValueStoreTransformError`

Rules:

* Missing keys use `KeyValueStoreNotFoundError`.
* Expired keys use `KeyValueStoreNotFoundError` on `Get`.
* Validation failures use `KeyValueStoreValidationError`.
* Expected version mismatch uses `KeyValueStoreConcurrencyError`.
* Set-if-absent conflict uses `KeyValueStoreConflictError`.
* Unsupported provider capability uses `KeyValueStoreUnsupportedFeatureError`.
* Store quota failures use `KeyValueStoreQuotaExceededError`.
* Existing value/counter type mismatches use `KeyValueStoreTypeMismatchError`.
* Counter overflow uses `KeyValueStoreCounterOverflowError`.
* Compression and encryption failures use `KeyValueStoreTransformError`.
* Unexpected provider exceptions are wrapped in `KeyValueStoreProviderError`.

## Operation Flows

### Default Cached Read

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
      store short-lived negative cache entry when enabled
      return Result.Failure(NotFound)
  update local cache
  return Result.Success(entry)
```

### Fresh Cached Read

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

### Cached Write

```text
SET key value
  validate key
  validate value and options
  calculate hash
  enforce maximum value size
  enforce store quotas when configured
  write value and append change-log entry atomically
  update local cache
  clear negative cache entry
  return Result.Success(write result)
```

### Touch

```text
TOUCH key
  validate key
  validate expiry options
  update expiry metadata and append change-log entry atomically
  update local cache metadata
  clear negative cache entry
  return Result.Success(write result)
```

### Expire

```text
EXPIRE key
  validate key
  validate absolute expiry
  update expiry metadata and append change-log entry atomically
  update or evict local cache depending on expiry timestamp
  clear negative cache entry
  return Result.Success(write result)
```

### Cached Delete

```text
DELETE key
  validate key
  delete key and append change-log entry atomically
  evict local cache
  clear or replace negative cache entry according to options
  return Result.Success(existed)
```

`Delete` returns `Result<bool>`. `true` means a value or counter existed and was removed; `false` means the key was already absent or expired.

`Delete` should be idempotent unless a provider-specific concurrency option is configured.

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
      if change.OriginNodeId != local NodeId:
          evict change.Key from local cache
          publish local KeyValueChangedNotification when enabled
      advance checkpoint
  save checkpoint
```

## Registration

Registration should follow existing storage builder patterns.

Example shape:

```csharp
services.AddKeyValueStorage(builder =>
{
    builder.AddInMemory("local");

    builder.AddCachedPersistent("default", options =>
    {
        options.StoreName = "default";
        options.NodeId = configuration["KeyValue:NodeId"];
        options.ChangeLogPollingInterval = TimeSpan.FromSeconds(2);
        options.LocalCacheTtl = TimeSpan.FromSeconds(30);
    });
});
```

The exact builder names should follow surrounding storage conventions during implementation.

## Health Checks

Key/Value Storage should register health checks for configured persistent and cached persistent stores.

Health checks should cover:

* Persistent backend connectivity.
* Backend read/write capability when safe to probe.
* Change-log read capability.
* Checkpoint read/write capability.
* Polling worker status.
* Polling lag compared to backend high watermark.
* Local cache availability.
* Quota status when quotas are configured.

Health check results should identify the affected store name and provider type.

Cached persistent providers should report degraded health when:

* The backend is reachable but the polling worker is stopped.
* The checkpoint cannot be saved.
* Polling lag exceeds the configured threshold.
* The node had to clear local cache because retained change-log history was missed.

## Razor Dashboard

The Razor dashboard is an operational surface for persistent key/value stores.

Dashboard authorization policies:

* `KeyValueStorage.Read` allows listing stores, keys, metadata, TTL, quota status, polling status, cache statistics, and safe value previews.
* `KeyValueStorage.Manage` allows writes, deletes, TTL changes, counter changes, force invalidation, and full value inspection.

Dashboard capabilities:

* Select a configured persistent or cached persistent store.
* Page through keys and metadata.
* View a full key.
* View full value content when value inspection is enabled.
* Display whether a value is string-like or binary.
* Display metadata such as content type, encoding, size, hash, version, creation time, update time, and expiry.
* Display store quotas and current usage when available.
* Display change-log high watermark, node checkpoint, polling lag, last poll time, and last poll error.
* Display local cache hit/miss and eviction statistics when available.
* Add a new key/value entry.
* Edit an existing key/value entry.
* Delete any persisted key.
* Provide explicit confirmation for destructive deletes.
* Surface Result errors from the maintenance service.

Dashboard design rules:

* Dashboard pages call `IKeyValueStoreMaintenanceService`.
* Dashboard pages do not access provider internals directly.
* Dashboard reads and writes go through the persistent backing store.
* Dashboard add/edit/delete operations append change-log entries through the maintenance provider so cached app nodes invalidate local entries.
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
* Delete count.
* Exists count.
* Touch count.
* Expire count.
* GetOrSet count.
* Increment count.
* Decrement count.
* Cache hits.
* Cache misses.
* Negative cache hits.
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
* Maintenance list/read/write/delete operations.
* Maintenance dashboard value-inspection attempts.
* Health check status.
* Store quota usage.
* Compression and encryption behavior failures.
* Missed change-log recovery events.
* Validation failures.
* Concurrency conflicts.
* Counter type mismatches.
* Counter overflow failures.
* Provider failures.


### Typed Logging

Key/Value Storage must expose its internal operation flow through typed logging.

Typed logging rules:

* Use source-generated logging methods with `[LoggerMessage]` on partial `TypedLogger` classes, following existing devkit patterns.
* Use stable event ids per component so logs can be filtered by operation area.
* Include `Constants.LogKey` in message templates for consistency with other devkit features.
* Include structured properties such as store name, provider type, operation, result status, error type, cache outcome, consistency mode, node id, sequence, checkpoint, polling lag, version, size, expiry, and elapsed time where relevant.
* Do not log value content.
* Log full raw keys by default because keys are generally operational identifiers rather than sensitive content.
* Support a configured safe key display strategy, key hash, or redacted key value for stores whose key names may contain sensitive data.
* Use `Trace` for high-volume inner decisions, `Debug` for normal internal operation flow, `Information` for lifecycle events and maintenance actions, `Warning` for degraded but recoverable states, and `Error` for unexpected provider or worker failures.
* Internal typed logging must be present even when the optional logging client behavior is not registered.
* The optional logging client behavior must not be the only source of operational visibility for cache invalidation, persistence writes, checkpoints, cleanup, or recovery.
* Hot-path logs must avoid string interpolation and ad hoc object allocations.
* Provider implementations should expose typed logging for provider-specific behavior without leaking provider-specific public APIs.

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
* Keep encryption key management, storage, and rotation outside the provider contract; value encryption behavior should use `EncryptionHelper` primitives for the built-in AES-CBC implementation.
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
5. Given a cached persistent store, when a counter operation succeeds, then the counter update and change-log append are committed atomically and other nodes invalidate the key.

### Story 3: Use Pure In-Memory Provider

User story: As an application developer, I want a pure in-memory provider, so that tests, local development, and ephemeral single-process features can use the same client API.

Acceptance criteria:

1. Given the in-memory provider, when values are written and read in the same process, then operations complete without a persistent backend.
2. Given the process restarts, when the in-memory provider is used, then previously stored values are not available.
3. Given multiple app nodes use independent in-memory providers, when one node writes a key, then another node does not observe that value.
4. Given TTL is configured, when a value expires, then `Get` returns not-found and `Exists` returns false.

### Story 4: Use Cached Persistent Provider

User story: As an application developer, I want a cached persistent provider, so that production apps can combine fast local reads with durable shared storage.

Acceptance criteria:

1. Given a cached persistent provider, when `Set` succeeds, then the value has been durably written and the change-log entry has been appended.
2. Given a key is cached locally, when `Get` is called before expiry and cache TTL is valid, then the provider returns the local entry.
3. Given a key is not cached locally, when `Get` is called, then the provider reads the backend and populates the local cache.
4. Given a fresh read option, when `Get` is called, then the provider reads the backend and refreshes the local cache.
5. Given a bypass-cache read option, when `Get` is called, then the provider reads the backend without reading from or writing to the local cache.
6. Given the backend cannot atomically write the value and change-log entry, when configured for cached multi-node use, then provider registration or startup fails.

### Story 5: Invalidate Across Nodes

User story: As an application operator, I want cached nodes to invalidate stale local entries, so that load-balanced deployments converge after writes.

Acceptance criteria:

1. Given Node A writes a key, when Node B polls the change log, then Node B evicts the key from its local cache.
2. Given Node B subsequently reads the key, when the local cache entry was evicted, then Node B reloads the value from the backend.
3. Given a node processes a change-log entry from its own `NodeId`, when polling runs, then it ignores that entry for local eviction.
4. Given polling is delayed, when local cache TTL expires, then the stale local entry is not returned.
5. Given a node checkpoint is older than retained change-log history, when polling runs, then the node clears its local cache and resumes from the current high watermark.

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
4. Given background cleanup is enabled, when expired entries are found, then they are physically removed by the cleanup process.
5. Given cleanup physically removes an entry in a cached persistent backend, then the backend records an expiry change-log entry when required for invalidation.

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
8. Given an operator edits an existing entry, when the maintenance write succeeds, then cached nodes invalidate the key through change-log polling.
9. Given an operator deletes an entry, when the delete is confirmed and succeeds, then the persistent backing store removes the entry and a change-log entry is appended.
10. Given the persistent backend is unavailable, when the dashboard requests entries, then the page shows a benign unavailable state and does not throw.

### Story 9: Observe Key Changes Locally

User story: As an application developer, I want local handlers to react when this node observes key changes, so that lightweight derived state can be refreshed without using the message broker.

Acceptance criteria:

1. Given change notifications are enabled, when the polling worker processes a change-log entry from another node, then it evicts the local cache entry and publishes a local `KeyValueChangedNotification`.
2. Given change notifications are enabled, when the polling worker processes a change-log entry from the same `NodeId`, then it does not publish a duplicate local observer notification for that self-originated change.
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
6. Given the cached persistent provider is used, when concurrent same-node reads miss the same key, then built-in single-flight ensures only one backend load or value factory execution occurs for that key.
7. Given a custom behavior is registered, when the client is resolved, then the behavior can wrap value and counter operations without provider-specific APIs.
8. Given encryption behavior is enabled with an `EncryptionHelper`-compatible key, when a value is written and read back, then the persisted bytes are encrypted and the client returns the original value content.
9. Given compression and encryption are both enabled, when a value is written, then compression runs before encryption.

### Story 11: Manage Expiry Without Rewriting Values

User story: As an application developer, I want to touch and expire keys, so that temporary state can be extended or shortened without rewriting stored values.

Acceptance criteria:

1. Given a key exists, when `Touch` is called with a TTL, then the entry expiry is extended and the value content is unchanged.
2. Given a key exists, when `Expire` is called with an absolute timestamp, then the entry expiry is updated and the value content is unchanged.
3. Given a key is missing or expired, when `Touch` is called, then it returns a not-found failure.
4. Given a cached persistent store, when `Touch` or `Expire` succeeds, then a change-log entry is appended and other nodes invalidate the key.

### Story 12: Use Key/Value Storage As ICacheProvider

User story: As an application developer, I want an `ICacheProvider` implementation backed by key/value storage, so that existing cache consumers can use distributed cache storage without changing their dependency.

Acceptance criteria:

1. Given `KeyValueStoreCacheProvider` is registered as `ICacheProvider`, when a consumer calls `Set` and later `Get` for the same key, then the value is stored and read through `IKeyValueStoreClient`.
2. Given sliding or absolute expiration is supplied to `ICacheProvider.Set`, when the adapter writes the value, then the expiration is mapped to key/value write options.
3. Given a consumer calls `Remove`, when the key exists or is missing, then the adapter deletes through `IKeyValueStoreClient` and preserves idempotent cache remove behavior.
4. Given a consumer calls `GetKeys`, when cache key enumeration is enabled, then only keys from the adapter cache namespace are returned.
5. Given a consumer calls `RemoveStartsWith`, when cache key enumeration is enabled, then matching cache keys are deleted through exact-key delete operations and cached nodes invalidate through the normal change-log mechanism.
6. Given cache key enumeration is disabled, when `GetKeys` or `RemoveStartsWith` is called, then the adapter follows its configured unsupported-operation behavior without adding scans to `IKeyValueStoreClient`.
7. Given values are written through the adapter, when typed logging is enabled, then adapter logs include cache operation, store name, result status, and the raw key by default without value content.

### Story 13: Observe Internal Operations With Typed Logging

User story: As an application operator, I want key/value operations to emit typed internal logs, so that distributed cache behavior, persistence writes, polling, and recovery can be diagnosed without enabling custom instrumentation.

Acceptance criteria:

1. Given any public client operation is executed, when internal logging is enabled by normal .NET logging configuration, then typed log events expose operation name, store name, provider type, result status, and elapsed time without value content.
2. Given a cached persistent read is executed, when the provider chooses local cache, negative cache, fresh backend read, or backend load after miss, then typed log events expose the cache decision and raw key by default, unless a safe key display strategy is configured.
3. Given a cached persistent write or counter operation succeeds, when the persistence provider commits the state change and change-log entry, then typed log events expose the operation, version, origin node, and change-log sequence when available.
4. Given change-log polling runs, when entries are read, skipped, processed, checkpointed, or missed-retention recovery is triggered, then typed log events expose node id, checkpoint, processed count, polling lag, and recovery action.
5. Given the optional logging behavior is not registered, when internal provider, polling, cleanup, or maintenance work runs, then typed internal logs are still emitted according to logging configuration.

### Story 14: Operate With Health And Quotas

User story: As an operator, I want health and quota visibility, so that I can detect backend, polling, and capacity problems before they cause user-facing failures.

Acceptance criteria:

1. Given a persistent store is configured, when health checks run, then backend connectivity is reported per store.
2. Given a cached persistent store is configured, when health checks run, then polling worker status and polling lag are reported.
3. Given quotas are configured, when writes exceed value, entry, or store-size limits, then writes fail with quota or size errors.
4. Given the dashboard is enabled, when an operator views a store, then quota usage and polling lag are visible when the provider can report them.

### Story 15: Use EF Core As Durable Relational Backend

User story: As an application developer, I want an EF Core key/value provider, so that production cache-store entries can be shared safely across app nodes.

Acceptance criteria:

1. Given two app nodes use the same EF Core backing store, when Node A writes a key, then Node B can read the value from the persistent backend.
2. Given Node A writes a key through the cached persistent provider, when the write succeeds, then the EF Core transaction includes both the value write and change-log append.
3. Given Node B has the key cached locally, when Node B polls the EF Core change log after Node A writes, then Node B evicts the stale local entry.
4. Given the EF Core provider is configured for cached persistent use, when atomic value write plus change-log append cannot be guaranteed, then startup or registration fails.
5. Given a node restarts, when it resumes polling, then it loads its EF Core checkpoint and continues from the last processed sequence.
6. Given a production cached persistent provider is configured without explicit node identity configuration or explicit opt-in to a built-in `INodeIdProvider`, when startup runs, then startup fails with a clear configuration error.

### Story 16: Use Azure Table Storage As Azure Backend

User story: As an Azure application developer, I want an Azure Table Storage key/value provider, so that cloud-hosted cache-store entries can use a lightweight Azure-native backing store.

Acceptance criteria:

1. Given two app nodes use the same existing Azure Storage account and provider-managed table configuration, when Node A writes a key, then Node B can read the value from the persistent backend.
2. Given the configured storage account exists and required tables are missing, when provider initialization runs with table creation enabled, then the provider creates the required tables idempotently.
3. Given Node A writes a key through the cached persistent provider, when the write succeeds, then the Azure Table Storage batch includes both the value entity change and the change-log entity.
4. Given Node B has the key cached locally, when Node B polls the matching table shard change log after Node A writes, then Node B evicts the stale local entry.
5. Given an Azure Table Storage provider is configured for cached persistent use, when value and change-log entities cannot be written in the same partition batch, then startup or registration fails.
6. Given multiple shards are configured, when polling runs, then checkpoints are tracked per store, node, and shard.
7. Given table creation is disabled and required tables do not exist, when provider initialization runs, then startup fails with a clear configuration or provider error.
8. Given a workload requires native change feed or richer global distribution, when choosing an Azure backend, then Cosmos DB is considered as a separate provider option rather than the default Azure provider.

## Resolved Design Decisions

* `LocalCacheTtl` defaults to 30 seconds for cached persistent providers.
* Production cached persistent providers require explicit stable node identity configuration or explicit opt-in to a built-in `INodeIdProvider`.
* `INodeIdProvider` belongs in `Common.Utilities` so other features can reuse stable node identity resolution.
* `KeyValueStoreDefaults.MaxValueSizeInBytes` defaults to 1 MB.
* `Delete` returns `Result<bool>` where `true` means an existing value or counter was removed and `false` means the key was already absent or expired.
* `BypassCache` strictly avoids local-cache reads and writes.
* Dashboard authorization uses `KeyValueStorage.Read` and `KeyValueStorage.Manage`.
* Full dashboard value inspection defaults to a 64 KB display limit and truncates values above that limit.
* Dashboard edits do not capture operator reasons.
* Key-change observer registration uses a storage-specific adapter over `SimpleNotifier`.
* The built-in compression behavior uses `CompressionHelper` GZip APIs.
* The built-in encryption behavior resolves key ids through a caller-provided resolver with a store-name default.
* Single-flight is core cached-provider behavior and enabled by default.
* Negative caching is optional and disabled by default.
* Expiry correctness is based on read-time logical expiry, not cleanup timing.
* Existing ordinary cache consumers continue to depend on `ICacheProvider`; Key/Value Storage provides a distributed `ICacheProvider` adapter.
* The cached persistent provider composes over `IKeyValuePersistenceProvider`; durable storage mechanics stay separate from local cache policy.

## Implementation Notes

* Keep the feature strictly exact-key based.
* Implement the EF Core provider for durable relational production use.
* Implement Azure Table Storage as the Azure cloud-native provider.
* Treat cached persistent cross-process/node behavior as required, not optional.
* Do not add listing, querying, or scan APIs to the application-facing client.
* Keep operational listing scoped to `IKeyValueStoreMaintenanceService` and the persistent maintenance provider.
* Do not add message-broker invalidation as the default invalidation mechanism.
* Add a reusable common `INodeIdProvider` abstraction in `Common.Utilities` and use it for key/value node identity.
* Use `SimpleNotifier` only behind the key/value observer adapter for local in-process key-change observer fan-out.
* Do not use key-change observers for critical durable workflows.
* Implement client behaviors as decorators around `IKeyValueStoreClient`.
* Implement `KeyValueStoreCacheProvider` as the `ICacheProvider` adapter over `IKeyValueStoreClient`.
* Keep compression, encryption, checksum verification, negative caching, and custom behaviors provider-neutral.
* Keep core cached-provider single-flight provider-neutral across durable providers.
* Add health checks for persistent and cached persistent providers.
* Add dashboard visibility for quotas, cache statistics, and polling lag.
* Do not add soft-delete or maintenance audit persistence to this focused feature.
* Do not create new projects unless the implementation plan explicitly requires it.
* Prefer existing `Result` and storage conventions.
* Add focused unit tests for validators, in-memory behavior, expiry, touch/expire, get-or-set, counters, concurrency, client behaviors, encryption behavior, typed logging, `ICacheProvider` adapter behavior, maintenance service behavior, EF Core atomic write/change-log behavior, change-log polling, checkpoint recovery, and cached persistent orchestration.
* Add integration tests using at least two cached persistent provider instances against the same EF Core backing store to prove cross-node invalidation for value writes and counter writes.
* Add Azure Table Storage provider tests for provider-managed table initialization, same-partition value/change-log batch writes, counter/change-log writes, shard checkpointing, ETag concurrency, and multi-instance invalidation.
* Keep Cosmos DB out of the default Azure provider scope for this feature.
* Run `dotnet build --nologo /p:UseSharedCompilation=false` as the broad validation command for implementation changes.
