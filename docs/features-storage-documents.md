# Document Storage

## Overview

Document Storage is the typed JSON-document companion to Blob Storage. Applications use `IDocumentStoreClient<T>` with a two-part `DocumentKey` (`PartitionKey`, `RowKey`); providers persist serialized bytes and provider-neutral metadata.

The client owns validation, serialization, canonical SHA-256 hashes, size limits, payload transforms, integrity verification, continuation-token binding, and expiration resolution. Providers own durable state, conditional persistence, native paging, and resource initialization.

Document Storage does not replace `ICacheProvider`. `DocumentStoreCacheProvider` is an adapter for deployments that intentionally use a document store as cache persistence; the abstractions remain separate.

## Good Fit

Use Document Storage when data is:

- naturally addressed by partition and row key;
- serialized and consumed as a typed document;
- read by exact key or bounded key-prefix pages;
- shared across application instances;
- protected by optimistic concurrency;
- temporary or retention-managed;
- portable across in-memory, relational, Azure Storage, and Cosmos providers.

Use Blob Storage for stream-first binary objects and Key/Value Storage for low-latency scalar/shared-state workloads.

## Contract

```csharp
var properties = new PropertyBag();
properties.Set("region", "eu");
var created = await documents.UpsertAsync(
    new DocumentKey("customers", "42"),
    customer,
    new DocumentWriteOptions
    {
        CreateOnly = true,
        Expiration = ExpirationChange.After(TimeSpan.FromDays(7)),
        Properties = properties
    },
    cancellationToken);

var current = await documents.GetAsync(
    new DocumentKey("customers", "42"),
    cancellationToken);

var updated = await documents.UpdatePropertiesAsync(
    new DocumentPropertiesUpdate(current.Value.Key)
    {
        IfMatchETag = current.Value.ETag,
        Expiration = ExpirationChange.Clear
    },
    cancellationToken);
```

Core operations:

- `GetAsync`: exact read returning `DocumentEntry<T>`.
- `FindPageAsync`: bounded payload page returning `DocumentEntry<T>` items.
- `ListPageAsync`: bounded key-only page.
- `CountAsync`: provider-side count where supported.
- `ExistsAsync`: exact logical existence.
- `UpsertAsync`: one conditional typed write returning `DocumentInfo`.
- `UpsertManyAsync`: ordered, prevalidated, non-atomic batch with explicit partial completion.
- `UpdatePropertiesAsync`: metadata/expiration-only conditional update.
- `DeleteAsync`: idempotent optional ETag delete.
- `DeleteManyAsync`: ordered, prevalidated delete batch.

`DocumentEntry<T>` and `DocumentInfo` expose provider-neutral `ETag`, logical `ContentHash`, creation/modification timestamps, `ExpiresAt`, and cloned scalar properties. The stored transformed hash remains an internal integrity value.

## Query Safety

Reads are page-based and deterministic. Full scans require consent in both `DocumentStoreOptions` and the individual query. Provider capabilities decide whether exact, prefix, suffix, count, and key-only operations are accepted or require client-side filtering.

Continuation tokens are opaque and bind the normalized client name, document type, operation, query shape, page size, provider state, and first-page expiration cutoff. Configure `IContinuationTokenProtector` to require HMAC-protected tokens.

See [Document Storage Paging Specification](./specs/spec-application-storage-documents-paging.md).

## Size And Integrity

`DocumentStoreOptions.MaxDocumentSize` defaults to `ByteSize.Megabytes(1)` and is enforced against logical serialized bytes before provider I/O. A provider may advertise a lower stored-size limit.

Every write computes:

- logical `ContentHash` over serialized bytes;
- stored content hash after all transforms.

`DocumentWriteOptions.ExpectedContentHash` can enforce the caller's expected logical hash. Reads verify the stored hash before reversing transforms and verify the logical hash before deserialization. Mismatches return `DocumentStoreIntegrityError`.

## Properties And Transforms

Properties are scalar `PropertyBag` values. Supported values include null, strings, Boolean and numeric primitives, `Guid`, date/time values, `TimeSpan`, and byte arrays. Providers use `PropertyBagScalarCodec` where text persistence is required.

`IDocumentPayloadTransform` runs in registration order on writes and reverse order on reads. Compression uses `CompressionHelper`; encryption uses `EncryptionHelper` and `IEncryptionKeyProvider`, so active-key writes and historical-key reads support key rotation. Transform metadata is stored in a versioned `bdk_` envelope and is not exposed as application properties.

```csharp
services.AddDocumentStorage()
    .WithCompressionTransform<Customer>()
    .WithEncryptionTransform<Customer>()
    .WithAzureTableClient<Customer>(tableServiceClient);
```

The encryption transform requires a registered `IEncryptionKeyProvider`. Standard provider registrations resolve the configured `ISerializer`, `TimeProvider`, `IContinuationTokenProtector`, and transforms from dependency injection.

## Expiration And Retention

Writes use `ExpirationChange`:

- `Preserve`: keep the current expiration; inserts remain non-expiring.
- `At(...)`: set an absolute timestamp normalized to UTC.
- `After(...)`: resolve one relative timestamp from the operation `TimeProvider`.
- `Clear`: remove expiration.

Due documents are immediately hidden from exact reads, existence, count, pages, enumeration, copy, and move. They remain physical records until retention runs. `CreateOnly` therefore still conflicts with an expired physical record, while clearing or extending expiration revives it.

```csharp
services.AddDocumentStorage(options => options.WithRetention(retention =>
{
    retention.SweepInterval = TimeSpan.FromMinutes(15);
    retention.BatchSize = 500;
    retention.MaxBatchesPerStore = 5;
}));
```

`DocumentRetentionBackgroundService` resolves each named client in a fresh DI scope and invokes only `IDocumentStoreRetentionProvider`. It never emulates cleanup through public scans. In-memory, Entity Framework, Azure Blob, and Azure Table use bounded provider-native cleanup; Cosmos uses native item TTL.

The defaults are enabled retention, a 15-second startup delay, one-hour interval, batch size 1000, at most 10 batches per client, no batch delay, and a 10-second stop timeout.

## Named Clients And DI

`AddDocumentStorage` is the feature registration entry point. Registrations are keyed by document CLR type and normalized case-insensitive client name. At most one registration for a type is the explicit default exposed through direct `IDocumentStoreClient<T>` injection. Resolve other clients through `IDocumentStoreClientFactory.CreateClient<T>(name)`.

```csharp
services.AddDocumentStorage()
    .WithAzureTableClient<Customer>(tableServiceClient, name: "primary", isDefault: true)
    .WithEntityFrameworkClient<Customer, StorageDbContext>(name: "archive", isDefault: false);
```

Singleton, scoped, and transient client lifetimes follow normal keyed DI semantics. The factory is scoped and lookup-only; it does not construct or cache clients. Entity Framework providers receive `IServiceScopeFactory` and own one scope and `DbContext` per operation.

Both `IDocumentStoreProvider` and `IDocumentStoreClient<T>` are keyed by the same type/name identity at the configured lifetime. Retention resolves the keyed provider directly, so custom client behaviors cannot hide retention support.

Behaviors are composed between the provider adapter and the outer validating/serializing client. Invalid input never reaches logging, retry, timeout, cache, chaos, transforms, or providers.

## Behaviors

- Typed logging with raw keys by default. Register `Sha256KeyDisplayStrategy` only where keys are classified as sensitive.
- Metrics with shared operation names and key/query context.
- Retry for transient provider failures only.
- Timeout with a linked token, cancellation quiescence, caller-cancellation preservation, typed timeout errors, and injected `TimeProvider`.
- Exact-read caching with named-client-aware keys and mutation invalidation.
- Chaos behavior for controlled resilience testing.

## Transfers And Maintenance

`EnumerateAsync` and `EnumerateKeysAsync` require a positive maximum item count. `DeleteByQueryAsync` is bounded, supports dry-run and optional continue-on-error, reads the matching entry metadata, and conditionally deletes the ETag observed during enumeration. Partial results expose every failed key in input order.

`CopyAsync` and `MoveAsync` work across any two clients of the same document type. They reserialize through the target client and preserve properties and expiration unless overridden. Move copies first, then conditionally deletes the source ETag. A changed source remains intact and returns `DocumentStoreTransferError`; a move to the same client/key is a successful existence-checked no-op.

## Providers

### In-Memory

Process-local provider for tests and ephemeral use. It stores copied bytes in private synchronized state and returns cloned records.

### Entity Framework

Annotation-mapped `StorageDocument` persistence with indexed hashes for type/partition/row identity, a concurrency version, Unix-millisecond expiration, payload/transform metadata, and provider-owned contexts. SQL Server, PostgreSQL, and SQLite run the same provider contract.

### Azure Blob

Requires only `BlobServiceClient` or an account connection string. The provider derives `bdk-doc-{hash}` from normalized client name and document type, creates/reuses it through `CreateIfNotExistsAsync`, and never deletes it on disposal. Logical keys are independent Base64Url path segments, so reserved characters round-trip.

### Azure Table

Requires only `TableServiceClient` or an account connection string. The provider derives `BdkDoc{hash}`, creates/reuses it, chunks payload bytes into 60 KiB `bdk_content_####` properties, and validates the complete entity against Azure Table's 1 MiB limit. All provider metadata uses `bdk_` keys.

### Cosmos DB

Uses provider-owned database/container initialization through `CosmosSqlProvider`, enables per-item TTL, persists provider continuation state inside document tokens, and applies logical expiration filtering while native deletion remains eventual.

## Azure Resource Ownership

Azure providers depend on an existing account, not pre-created feature resources. Resource names are deterministic from `normalizedClientName + "\n" + documentTypeIdentity`; optional prefixes are validated. Initialization is asynchronous and idempotent. Concurrent callers and nodes may race safely, cancellation/failure is retryable, unrelated account resources are untouched, and disposal never removes the table/container.

## Dashboard And Diagnostics

The Document Storage dashboard inherits the shared dashboard authorization and antiforgery conventions. It selects named clients, lists document pages from 25 to 500 rows, and presents partition/row keys, serialized JSON size, modification time, and expiration in the compact table. Manual and interval refreshes use the currently applied client, key filters, query mode, page size, continuation state, and selected document. The standard refresh interval is off by default and remembered in browser-local storage. The details dialog exposes ETags, hashes, timestamps, expiration, scalar properties, and conditional save/delete actions without adding retention or transform administration to the browsing surface.

`IDocumentStorageDiagnosticsService` reports non-sensitive registration identity, lifetime, capabilities, size limits, configured transform identifiers, latest retention outcome, and health. `DocumentStorageMcpHandler` exposes `documents.summary`, `documents.clients`, and `documents.probe` when the MCP runtime is registered. Neither surface exposes payloads, continuation tokens, encryption material, or secrets.

## Shared Common APIs

Document and Blob Storage share:

- `ByteSize`, `StreamHelper`, and `TemporaryFileHelper`;
- `ExpirationChange` and `ExpirationHelper`;
- `ContentHashHelper`;
- `Base64UrlHelper`, `PropertyBagScalarCodec`, and `OpaqueContinuationTokenCodec`;
- `ContentTransformEnvelopeCodec`;
- `IEncryptionKeyProvider`;
- raw and SHA-256 key display strategies;
- `AsyncInitializationGate`.
- `PeriodicBackgroundService` and `PeriodicBackgroundServiceOptions` for the monitored retention loop.

See [Common Utilities](./common-utilities.md), [Common Serialization](./common-serialization.md), and [Blob Storage](./features-storage-blobs.md).

## Testing

The feature is covered by client/behavior tests, in-memory contracts, dashboard and MCP registration tests, Entity Framework contracts on SQLite/PostgreSQL/SQL Server, Azurite contracts for Blob/Table resource management and persistence, and Cosmos contracts when an emulator or configured account is available. Blob Storage contracts remain regression coverage for shared primitives.
