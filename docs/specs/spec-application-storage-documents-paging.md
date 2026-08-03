---
status: implemented
---

# Document Storage Paging Specification

> This specification defines the completed paging, query-safety, continuation, and expiration behavior of Document Storage.

[TOC]

## Goals

- Keep every document query bounded.
- Return payloads only when the caller asks for payloads.
- keep provider continuation state opaque and provider-neutral.
- Prevent tokens from being reused across clients, document types, operations, or query shapes.
- Keep one logical page sequence stable while documents expire.
- Support multiple named stores for the same CLR document type.

## Public Operations

`IDocumentStoreClient<T>` exposes:

```csharp
Task<Result<DocumentEntry<T>>> GetAsync(
    DocumentKey key,
    CancellationToken cancellationToken = default);

Task<Result<DocumentPage<T>>> FindPageAsync(
    DocumentQuery query,
    CancellationToken cancellationToken = default);

Task<Result<DocumentKeyPage>> ListPageAsync(
    DocumentQuery query,
    CancellationToken cancellationToken = default);

Task<Result<long>> CountAsync(
    DocumentCountQuery query,
    CancellationToken cancellationToken = default);

Task<Result<bool>> ExistsAsync(
    DocumentKey key,
    CancellationToken cancellationToken = default);
```

`FindPageAsync` returns `DocumentEntry<T>` values and therefore materializes payloads. `ListPageAsync` returns only `DocumentKey` values and must use key-only projection or metadata listing where the provider supports it. `CountAsync` must not deserialize payloads.

`GetAsync` is an exact-key operation. A missing or logically expired document returns `DocumentStoreNotFoundError`; it never returns a successful null value.

## Query Shape

`DocumentQuery` contains:

- an optional `DocumentKey`;
- a `DocumentKeyFilter` (`FullMatch`, `RowKeyPrefixMatch`, or `RowKeySuffixMatch`);
- a positive `Take` no greater than `DocumentStoreOptions.MaxTake`;
- an optional opaque `ContinuationToken`;
- explicit `AllowFullScan` consent.

`DocumentCountQuery` carries the same key/filter/full-scan shape without paging state.

An omitted key is a full scan. It is accepted only when both the client options and the individual request allow full scans. Unsupported or client-side-filtered shapes are rejected according to `DocumentStoreProviderCapabilities` and `RejectClientSideFilteredQueries` before behaviors or provider I/O.

## Ordering

Every provider orders documents lexically by:

1. `PartitionKey` using ordinal semantics;
2. `RowKey` using ordinal semantics.

Continuation state resumes strictly after the last returned logical key. Providers may encode native continuation state, but it is never exposed directly.

## Continuation Tokens

The client wraps provider continuation state with `OpaqueContinuationTokenCodec` using the `document-storage` purpose. The envelope binds:

- normalized client name;
- document type identity;
- operation (`find` or `list`);
- normalized query hash, including `Take`;
- first-page visibility cutoff;
- provider-native continuation state.

When `IContinuationTokenProtector` is configured, tokens are HMAC protected. The client rejects unsigned, modified, incorrectly signed, wrong-purpose, wrong-client, wrong-type, wrong-operation, and query-mismatched tokens before provider I/O. Without a protector, versioned unsigned tokens are emitted for development scenarios.

Tokens are ephemeral application state. They are not a durable bookmark or stored-data format.

## Expiration Snapshot

The first page captures `TimeProvider.GetUtcNow()` as its visibility cutoff. Every continuation page reuses the cutoff stored in the token.

Consequences:

- documents due at or before the first-page cutoff are excluded;
- a document that expires after page one remains eligible for the rest of that page sequence;
- a new query gets a new cutoff and hides the document immediately;
- physical retention cleanup does not change the logical sequence contract.

Providers receive the resolved cutoff explicitly and must apply it to exact reads, existence, pages, and counts.

## Page Filling

A provider that must filter native results after listing, such as suffix matching on blob names, continues reading native pages until one of these conditions is reached:

- `Take` logical items have been collected;
- no native continuation remains;
- cancellation is requested.

The provider must not return an underfilled page merely because one native page contained filtered-out items. `ListPageAsync` must still avoid payload downloads.

## Enumeration

`EnumerateAsync` and `EnumerateKeysAsync` are client extensions over the page operations. `DocumentEnumerationOptions.MaxItems` is mandatory and positive. Enumeration:

- requests bounded pages;
- stops immediately at `MaxItems` without fetching another page;
- preserves cancellation;
- never exposes provider-native tokens.

## Provider Expectations

### In-Memory

Uses private synchronized state, copied byte payloads, deterministic ordering, and logical-key continuation state.

### Entity Framework

Uses an owned DI scope and `DbContext` per operation. Queries apply indexed type/key hashes, raw-key collision checks, Unix-millisecond expiration filtering, deterministic ordering, and key-only projection for listing.

### Azure Blob

Uses a provider-managed deterministic container derived from normalized client name and document type. Partition and row keys are independent Base64Url path segments, so arbitrary key text round-trips. Listing uses the encoded partition prefix where possible and fills logical pages across native pages.

### Azure Table

Uses a provider-managed deterministic table. Encoded partition and row keys are used for exact reads; raw logical keys are retained in `bdk_` properties. Payloads are split into 60 KiB binary properties. The complete entity is bounded by the 1 MiB service limit.

### Cosmos DB

Uses the native feed continuation state inside the document token. Container initialization is provider-owned, per-item TTL is enabled, and item expiration remains logically filtered even before Cosmos physically removes the item.

## Errors

- Invalid key/query/page size/full-scan consent: `ValidationError` or `DocumentStoreInvalidQueryError`.
- Unsupported query capability: `DocumentStoreUnsupportedQueryError`.
- Token format/binding/signature failure: `DocumentStoreContinuationTokenError`.
- Missing or expired exact document: `DocumentStoreNotFoundError`.
- Provider failure: `DocumentStoreProviderError` or a more specific typed storage error.
- Caller cancellation: `OperationCanceledException`.
- Timeout behavior deadline: `DocumentStoreTimeoutError`.

## Verification

Provider contracts cover:

- exact, prefix, suffix, and explicitly allowed full-scan shapes;
- deterministic bounded pages and continuation traversal;
- key-only listing and payload-bearing find pages;
- wrong-operation and query-mismatched tokens;
- protected token tampering and wrong-purpose rejection;
- stable visibility cutoff across clock advancement;
- arbitrary key round-tripping;
- logical page filling across filtered native pages;
- cancellation and provider-owned resource initialization.
