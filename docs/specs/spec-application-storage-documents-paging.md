---
status: implemented
---

# Design Specification: Document Storage Continuation Paging

> Replace unbounded document storage reads with safe, continuation-token based paging and Result-native APIs across all providers.

[TOC]

## Overview

The Document Storage feature currently exposes read operations that can retrieve all documents or all document keys of a type. This is unsafe for large data sets because it can cause excessive database reads, object materialization, JSON deserialization, memory pressure, network transfer, and backend congestion.

This specification introduces continuation-token based paging for Document Storage and makes the public Document Storage client and provider APIs Result-native.

The public API distinguishes clearly between:

* `GetResultAsync`: retrieves one document by exact key.
* `FindPageResultAsync`: returns document payload instances.
* `ListPageResultAsync`: returns document keys only.
* `CountResultAsync`: counts documents matching a query without returning payloads.
* `ExistsResultAsync`: checks for exact-key existence.
* `UpsertResultAsync`: inserts or updates documents.
* `DeleteResultAsync`: deletes documents.

Unbounded `FindAsync()` and `ListAsync()` operations are removed.

Backward compatibility is not required because Document Storage is not used by external client projects yet.

## Goals

The goals of this change are:

* Prevent accidental full-store reads.
* Make all query operations bounded by page size.
* Use continuation-token paging instead of offset/page-number paging.
* Keep one provider-agnostic public API.
* Make the client and provider APIs Result-native.
* Let each provider use its native or most efficient paging mechanism.
* Preserve the semantic difference between finding documents and listing keys.
* Ensure `ListPageResultAsync` does not load document payloads for any provider.
* Require explicit opt-in for full type scans.
* Reject provider-unsupported query patterns consistently.
* Reject client-side filtered provider behavior by default.
* Avoid `ResultPaged<T>` for Document Storage paging.
* Provide a fluent query builder for discoverable and readable paged query construction.

## Non-Goals

This change does not introduce:

* Offset paging.
* Page-number based paging.
* `ResultPaged<T>` for Document Storage paging.
* Arbitrary document property filtering.
* Sorting options beyond provider-defined key ordering.
* Bulk scan jobs.
* Background export jobs.
* Backward-compatible overloads.
* Public exposure of provider-native continuation tokens.
* Non-Result public APIs on the document store client.
* Non-Result public APIs on the document store provider.
* Provider-specific query builders.
* Fluent builders that execute queries directly.

## Terminology

| Term               | Meaning                                                                                      |
| ------------------ | -------------------------------------------------------------------------------------------- |
| Document           | The serialized payload stored under a `DocumentKey`.                                         |
| DocumentKey        | The logical key consisting of `PartitionKey` and `RowKey`.                                   |
| Find               | Operation that returns document payload instances.                                           |
| List               | Operation that returns document keys only.                                                   |
| Page               | A bounded result set containing up to `Take` items.                                          |
| Continuation token | Opaque token used to retrieve the next page of the same logical query.                       |
| Full scan          | Query without a `DocumentKey`, scoped only by document type.                                 |
| Client-side filter | Filtering that cannot be fully pushed to the storage backend.                                |
| Native token       | Provider-specific continuation token, wrapped by the devkit token envelope.                  |
| Result-native API  | API that returns `Result` or `Result<T>` directly instead of throwing for expected failures. |
| Query builder      | Fluent helper that constructs `DocumentQuery` or `DocumentCountQuery`.                       |

## Design Principles

* Document Storage paging is continuation-token based.
* Continuation tokens are opaque strings to application code.
* Continuation tokens are bound to the logical query that produced them.
* Continuation tokens may contain provider-native continuation state internally.
* Continuation tokens must not contain document payloads.
* `FindPageResultAsync` materializes document payloads.
* `ListPageResultAsync` returns only keys and must not load document payloads.
* `GetResultAsync` is the only exact-key convenience read.
* Missing exact-key documents are represented as `Result<T>.Failure(...)`, not as `null`.
* Full scans require explicit opt-in.
* Client-side filtered queries are rejected by default.
* Providers must declare their query capabilities.
* Unsupported provider behavior must fail before executing the storage query.
* Counting must not deserialize document payloads.
* Provider implementations must be deterministic within a logical query.
* Public client and provider APIs must return `Result` or `Result<T>`.
* Expected validation and query failures must be represented as typed Result errors.
* Fluent builders must construct query models only and must not bypass validation.

## Public Client API

Replace the current public API on `IDocumentStoreClient<T>` with the following Result-native API.

```csharp
public interface IDocumentStoreClient<T>
    where T : class, new()
{
    Task<Result<T>> GetResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default);

    Task<Result<DocumentPage<T>>> FindPageResultAsync(
        DocumentQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<DocumentKeyPage>> ListPageResultAsync(
        DocumentQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<long>> CountResultAsync(
        DocumentCountQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default);

    Task<Result> UpsertResultAsync(
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default);

    Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default);
}
```

## Removed Client Methods

Remove all non-Result client methods.

```csharp
Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default);

Task<IEnumerable<T>> FindAsync(
    DocumentKey documentKey,
    CancellationToken cancellationToken = default);

Task<IEnumerable<T>> FindAsync(
    DocumentKey documentKey,
    DocumentKeyFilter filter,
    CancellationToken cancellationToken = default);

Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken = default);

Task<IEnumerable<DocumentKey>> ListAsync(
    DocumentKey documentKey,
    CancellationToken cancellationToken = default);

Task<IEnumerable<DocumentKey>> ListAsync(
    DocumentKey documentKey,
    DocumentKeyFilter filter,
    CancellationToken cancellationToken = default);

Task<long> CountAsync(CancellationToken cancellationToken = default);

Task<bool> ExistsAsync(
    DocumentKey documentKey,
    CancellationToken cancellationToken = default);

Task UpsertAsync(
    DocumentKey documentKey,
    T entity,
    CancellationToken cancellationToken = default);

Task UpsertAsync(
    IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
    CancellationToken cancellationToken = default);

Task DeleteAsync(
    DocumentKey documentKey,
    CancellationToken cancellationToken = default);
```

No `[Obsolete]` transition is needed.

## Provider API

Update `IDocumentStoreProvider` to be Result-native too.

```csharp
public interface IDocumentStoreProvider
{
    DocumentStoreProviderCapabilities Capabilities { get; }

    Task<Result<T>> GetResultAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result<DocumentPage<T>>> FindPageResultAsync<T>(
        DocumentQuery query,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result<DocumentKeyPage>> ListPageResultAsync<T>(
        DocumentQuery query,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result<long>> CountResultAsync<T>(
        DocumentCountQuery query,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result<bool>> ExistsResultAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result> UpsertResultAsync<T>(
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result> UpsertResultAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new();

    Task<Result> DeleteResultAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new();
}
```

Providers are responsible for translating `DocumentQuery` into their native query mechanism and returning typed `Result` failures for expected validation, capability, paging, continuation-token, not-found, concurrency, serialization, and storage errors.

## Result-Native Error Handling

The Document Storage public API must not rely on extension wrappers to convert exceptions into Result values.

Expected failures must be returned as Result failures directly.

Technical exceptions may still occur inside provider implementations, but public provider methods must catch them and convert them to Result failures unless cancellation is requested.

## Cancellation

Cancellation should remain normal .NET cancellation behavior.

If the supplied `CancellationToken` is cancelled, the operation may throw `OperationCanceledException` or `TaskCanceledException`.

Do not convert caller-requested cancellation into a normal Result failure unless there is already a devkit-wide convention requiring that.

## Expected Failures

Expected failures include:

* invalid query shape
* page size too large
* full scan not allowed
* unsupported filter
* client-side filtering rejected
* invalid continuation token
* continuation token query mismatch
* document not found
* lease or concurrency conflict
* serialization/deserialization failure
* provider validation failure
* provider request failure

These should return `Result.Failure(...)` or `Result<T>.Failure(...)`.

## Query Models

## DocumentQuery

`DocumentQuery` is used by `FindPageResultAsync` and `ListPageResultAsync`.

```csharp
public sealed class DocumentQuery
{
    public DocumentKey? DocumentKey { get; init; }

    public DocumentKeyFilter Filter { get; init; } = DocumentKeyFilter.FullMatch;

    public int? Take { get; init; }

    public string? ContinuationToken { get; init; }

    public bool AllowFullScan { get; init; }
}
```

## DocumentCountQuery

`DocumentCountQuery` is used by `CountResultAsync`.

```csharp
public sealed class DocumentCountQuery
{
    public DocumentKey? DocumentKey { get; init; }

    public DocumentKeyFilter Filter { get; init; } = DocumentKeyFilter.FullMatch;

    public bool AllowFullScan { get; init; }
}
```

## Query Meaning

| Query Shape                                         | Meaning                                   |
| --------------------------------------------------- | ----------------------------------------- |
| `DocumentKey != null`, `Filter = FullMatch`         | Exact key lookup or exact key page query. |
| `DocumentKey != null`, `Filter = RowKeyPrefixMatch` | Partition-bound prefix query.             |
| `DocumentKey != null`, `Filter = RowKeySuffixMatch` | Partition-bound suffix query.             |
| `DocumentKey == null`, `AllowFullScan = true`       | Type-wide scan.                           |
| `DocumentKey == null`, `AllowFullScan = false`      | Invalid query.                            |

## Page Models

## DocumentPage<T>

```csharp
public sealed class DocumentPage<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];

    public string? ContinuationToken { get; init; }

    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
```

## DocumentKeyPage

```csharp
public sealed class DocumentKeyPage
{
    public IReadOnlyCollection<DocumentKey> Items { get; init; } = [];

    public string? ContinuationToken { get; init; }

    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
```

## No ResultPaged

Document Storage must not use `ResultPaged<T>` for this API.

The API returns:

```csharp
Result<T>
Result<DocumentPage<T>>
Result<DocumentKeyPage>
Result<long>
Result<bool>
Result
```

The `DocumentPage<T>` and `DocumentKeyPage` models carry their own continuation state.

## Fluent Query Builder

Add fluent builder support for creating `DocumentQuery` and `DocumentCountQuery` instances.

The builder is intended to make paged document queries easier to discover and harder to misuse. It must remain a thin construction helper around the existing query models. It must not execute queries itself and must not duplicate provider validation logic.

## Builder Goals

The fluent builder should:

* Improve readability of paged document queries.
* Make common query shapes discoverable.
* Avoid repetitive object initializer code.
* Preserve the existing `DocumentQuery` and `DocumentCountQuery` models.
* Keep validation in `DocumentQueryValidator`.
* Support exact, prefix, suffix, full-scan, continuation, and count query construction.
* Support both document queries and key listing queries through the same `DocumentQuery` model.
* Not introduce provider-specific behavior.

## Builder Non-Goals

The fluent builder must not:

* Execute document store operations.
* Perform provider-specific validation.
* Replace `DocumentQueryValidator`.
* Hide full scan approval.
* Create unbounded queries silently.
* Introduce offset/page-number paging.
* Introduce arbitrary document property filtering.
* Bypass provider capability checks.

## Builder Factory

Add a simple static factory.

```csharp
public static class DocumentQueries
{
    public static DocumentQueryBuilder Query() =>
        DocumentQueryBuilder.Create();

    public static DocumentCountQueryBuilder Count() =>
        DocumentCountQueryBuilder.Create();
}
```

Preferred usage:

```csharp
var query = DocumentQueries
    .Query()
    .ForKey("people", "DE-")
    .WithRowKeyPrefix()
    .Take(100)
    .Build();
```

For count:

```csharp
var query = DocumentQueries
    .Count()
    .ForKey("people", "DE-")
    .WithRowKeyPrefix()
    .Build();
```

## DocumentQueryBuilder API

```csharp
public sealed class DocumentQueryBuilder
{
    public static DocumentQueryBuilder Create();

    public DocumentQueryBuilder ForKey(DocumentKey documentKey);

    public DocumentQueryBuilder ForKey(string partitionKey, string rowKey);

    public DocumentQueryBuilder WithFullMatch();

    public DocumentQueryBuilder WithRowKeyPrefix();

    public DocumentQueryBuilder WithRowKeySuffix();

    public DocumentQueryBuilder Take(int take);

    public DocumentQueryBuilder ContinueWith(string continuationToken);

    public DocumentQueryBuilder AllowFullScan();

    public DocumentQuery Build();
}
```

## DocumentCountQueryBuilder API

```csharp
public sealed class DocumentCountQueryBuilder
{
    public static DocumentCountQueryBuilder Create();

    public DocumentCountQueryBuilder ForKey(DocumentKey documentKey);

    public DocumentCountQueryBuilder ForKey(string partitionKey, string rowKey);

    public DocumentCountQueryBuilder WithFullMatch();

    public DocumentCountQueryBuilder WithRowKeyPrefix();

    public DocumentCountQueryBuilder WithRowKeySuffix();

    public DocumentCountQueryBuilder AllowFullScan();

    public DocumentCountQuery Build();
}
```

## Builder Semantics

The builder should only construct query objects. It should not enforce all validation rules.

It may perform basic local argument validation for clearly invalid values, such as:

* null `DocumentKey`
* null or whitespace continuation token
* `Take <= 0`

Provider and option-dependent validation must remain in `DocumentQueryValidator`.

Examples of validation that must stay outside the builder:

* whether full scans are globally enabled
* whether a provider supports suffix matching
* whether client-side filtering is allowed
* whether `Take` exceeds configured `MaxTake`
* whether continuation token matches the query
* whether Azure Table supports suffix matching
* whether Blob suffix matching is rejected by default

## Default Builder Filter

If no filter is explicitly selected, the builder should default to:

```csharp
DocumentKeyFilter.FullMatch
```

Example:

```csharp
var query = DocumentQueries
    .Query()
    .ForKey("people", "DE-42")
    .Take(1)
    .Build();
```

Equivalent to:

```csharp
var query = new DocumentQuery
{
    DocumentKey = new DocumentKey("people", "DE-42"),
    Filter = DocumentKeyFilter.FullMatch,
    Take = 1
};
```

## Full Scan Builder Behavior

A full scan query is represented by no `DocumentKey` and `AllowFullScan = true`.

The builder must require explicit `.AllowFullScan()` to construct this shape intentionally.

```csharp
var query = DocumentQueries
    .Query()
    .AllowFullScan()
    .Take(500)
    .Build();
```

The builder must not add a hidden document key or fake partition key for full scans.

The query must still fail unless `DocumentStoreOptions.AllowFullScans` is enabled.

## Continuation Token Builder Behavior

`ContinueWith(...)` sets `DocumentQuery.ContinuationToken`.

```csharp
public DocumentQueryBuilder ContinueWith(string continuationToken)
{
    if (string.IsNullOrWhiteSpace(continuationToken))
    {
        throw new ArgumentException(
            "Continuation token must not be null or whitespace.",
            nameof(continuationToken));
    }

    this.continuationToken = continuationToken;
    return this;
}
```

The builder should not validate token content. Token content validation belongs to the continuation token serializer and provider/query validation flow.

## Take Builder Behavior

`Take(int take)` sets `DocumentQuery.Take`.

The builder may reject `take <= 0`.

It should not validate against `DocumentStoreOptions.MaxTake`, because the builder does not know the active options.

```csharp
public DocumentQueryBuilder Take(int take)
{
    if (take <= 0)
    {
        throw new ArgumentOutOfRangeException(
            nameof(take),
            "Take must be greater than zero.");
    }

    this.take = take;
    return this;
}
```

Use `Take(...)`, not `PageSize(...)`.

Reason:

* The query returns a continuation page, not a numbered page.
* `Take` aligns with LINQ terminology.
* It avoids implying total pages or page numbers.

Use `ContinueWith(...)`, not `WithContinuationToken(...)`.

Reason:

* `ContinueWith(...)` reads naturally in caller code.
* The token remains opaque and implementation-specific.

## Builder Implementation Sketch

```csharp
public sealed class DocumentQueryBuilder
{
    private DocumentKey? documentKey;
    private DocumentKeyFilter filter = DocumentKeyFilter.FullMatch;
    private int? take;
    private string? continuationToken;
    private bool allowFullScan;

    private DocumentQueryBuilder()
    {
    }

    public static DocumentQueryBuilder Create() => new();

    public DocumentQueryBuilder ForKey(DocumentKey documentKey)
    {
        this.documentKey = documentKey ?? throw new ArgumentNullException(nameof(documentKey));
        return this;
    }

    public DocumentQueryBuilder ForKey(string partitionKey, string rowKey)
    {
        this.documentKey = new DocumentKey(partitionKey, rowKey);
        return this;
    }

    public DocumentQueryBuilder WithFullMatch()
    {
        this.filter = DocumentKeyFilter.FullMatch;
        return this;
    }

    public DocumentQueryBuilder WithRowKeyPrefix()
    {
        this.filter = DocumentKeyFilter.RowKeyPrefixMatch;
        return this;
    }

    public DocumentQueryBuilder WithRowKeySuffix()
    {
        this.filter = DocumentKeyFilter.RowKeySuffixMatch;
        return this;
    }

    public DocumentQueryBuilder Take(int take)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        this.take = take;
        return this;
    }

    public DocumentQueryBuilder ContinueWith(string continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            throw new ArgumentException(
                "Continuation token must not be null or whitespace.",
                nameof(continuationToken));
        }

        this.continuationToken = continuationToken;
        return this;
    }

    public DocumentQueryBuilder AllowFullScan()
    {
        this.allowFullScan = true;
        return this;
    }

    public DocumentQuery Build() => new()
    {
        DocumentKey = this.documentKey,
        Filter = this.filter,
        Take = this.take,
        ContinuationToken = this.continuationToken,
        AllowFullScan = this.allowFullScan
    };
}
```

## Count Builder Implementation Sketch

```csharp
public sealed class DocumentCountQueryBuilder
{
    private DocumentKey? documentKey;
    private DocumentKeyFilter filter = DocumentKeyFilter.FullMatch;
    private bool allowFullScan;

    private DocumentCountQueryBuilder()
    {
    }

    public static DocumentCountQueryBuilder Create() => new();

    public DocumentCountQueryBuilder ForKey(DocumentKey documentKey)
    {
        this.documentKey = documentKey ?? throw new ArgumentNullException(nameof(documentKey));
        return this;
    }

    public DocumentCountQueryBuilder ForKey(string partitionKey, string rowKey)
    {
        this.documentKey = new DocumentKey(partitionKey, rowKey);
        return this;
    }

    public DocumentCountQueryBuilder WithFullMatch()
    {
        this.filter = DocumentKeyFilter.FullMatch;
        return this;
    }

    public DocumentCountQueryBuilder WithRowKeyPrefix()
    {
        this.filter = DocumentKeyFilter.RowKeyPrefixMatch;
        return this;
    }

    public DocumentCountQueryBuilder WithRowKeySuffix()
    {
        this.filter = DocumentKeyFilter.RowKeySuffixMatch;
        return this;
    }

    public DocumentCountQueryBuilder AllowFullScan()
    {
        this.allowFullScan = true;
        return this;
    }

    public DocumentCountQuery Build() => new()
    {
        DocumentKey = this.documentKey,
        Filter = this.filter,
        AllowFullScan = this.allowFullScan
    };
}
```

## Options

Add shared options for document store query safety.

```csharp
public sealed class DocumentStoreOptions
{
    public int DefaultTake { get; set; } = 100;

    public int MaxTake { get; set; } = 1000;

    public bool AllowFullScans { get; set; } = false;

    public bool RequireExplicitFullScanApproval { get; set; } = true;

    public bool RejectClientSideFilteredQueries { get; set; } = true;
}
```

## Default Option Values

| Option                            | Default | Meaning                                                         |
| --------------------------------- | ------: | --------------------------------------------------------------- |
| `DefaultTake`                     |   `100` | Used when `DocumentQuery.Take` is null.                         |
| `MaxTake`                         |  `1000` | Maximum allowed page size.                                      |
| `AllowFullScans`                  | `false` | Disallows type-wide scans by default.                           |
| `RequireExplicitFullScanApproval` |  `true` | Requires `AllowFullScan = true` per query.                      |
| `RejectClientSideFilteredQueries` |  `true` | Rejects provider operations that require client-side filtering. |

## Provider Capabilities

Add capability metadata to providers.

```csharp
public enum DocumentQuerySupport
{
    Unsupported = 0,
    SupportedClientSide = 1,
    SupportedServerSide = 2,
    SupportedEfficiently = 3
}
```

```csharp
public sealed class DocumentStoreProviderCapabilities
{
    public DocumentQuerySupport FullMatch { get; init; } = DocumentQuerySupport.SupportedEfficiently;

    public DocumentQuerySupport RowKeyPrefixMatch { get; init; } = DocumentQuerySupport.Unsupported;

    public DocumentQuerySupport RowKeySuffixMatch { get; init; } = DocumentQuerySupport.Unsupported;

    public DocumentQuerySupport FullScan { get; init; } = DocumentQuerySupport.Unsupported;

    public DocumentQuerySupport KeyListing { get; init; } = DocumentQuerySupport.Unsupported;

    public bool SupportsContinuationPaging { get; init; } = true;

    public bool SupportsServerSideCount { get; init; }

    public bool SupportsKeyOnlyProjection { get; init; }
}
```

## Expected Capability Matrix

| Provider         | Full match | Prefix      | Suffix      | Full scan   | Key listing | Count                | Key-only projection |
| ---------------- | ---------- | ----------- | ----------- | ----------- | ----------- | -------------------- | ------------------- |
| InMemory         | Efficient  | Efficient   | Efficient   | Efficient   | Efficient   | Local                | Yes                 |
| Entity Framework | Efficient  | Server-side | Server-side | Server-side | Efficient   | Server-side          | Yes                 |
| Cosmos DB        | Efficient  | Server-side | Server-side | Server-side | Server-side | Server-side or paged | Yes                 |
| Azure Table      | Efficient  | Efficient   | Unsupported | Server-side | Efficient   | Paged count          | Yes                 |
| Azure Blob       | Efficient  | Efficient   | Client-side | Server-side | Efficient   | Paged count          | Yes                 |

## Client-Side Filtering

Client-side filtering is rejected by default.

This affects Azure Blob suffix matching.

When `RejectClientSideFilteredQueries = true`, a Blob `RowKeySuffixMatch` query must fail before enumerating blobs.

When `RejectClientSideFilteredQueries = false`, Blob suffix matching may enumerate blob names and filter them client-side. Blob content must still only be downloaded for `FindPageResultAsync`, not for `ListPageResultAsync` or `CountResultAsync`.

## Query Validation

Create a shared query validator used by `DocumentStoreClient<T>` before calling the provider.

The validator should be Result-native.

```csharp
public sealed class DocumentQueryValidator
{
    public Result<DocumentQuery> NormalizeAndValidate(
        DocumentQuery query,
        DocumentStoreOptions options,
        DocumentStoreProviderCapabilities capabilities);

    public Result<DocumentCountQuery> NormalizeAndValidate(
        DocumentCountQuery query,
        DocumentStoreOptions options,
        DocumentStoreProviderCapabilities capabilities);
}
```

The validator must return typed Result failures, not throw exceptions for expected validation errors.

## Validation Rules for DocumentQuery

The validator must apply these rules:

* `query` must not be null.
* Resolve `Take` from `query.Take ?? options.DefaultTake`.
* `Take` must be greater than zero.
* `Take` must not exceed `options.MaxTake`.
* `DocumentKey == null` is a full scan.
* Full scans require `options.AllowFullScans = true`.
* Full scans require `query.AllowFullScan = true` when `RequireExplicitFullScanApproval = true`.
* `Filter = FullMatch` requires non-empty `PartitionKey` and non-empty `RowKey`.
* `Filter = RowKeyPrefixMatch` requires non-empty `PartitionKey`.
* `Filter = RowKeySuffixMatch` requires non-empty `PartitionKey` and non-empty `RowKey`.
* Unsupported provider capabilities must fail before provider execution.
* Client-side provider support must fail when `RejectClientSideFilteredQueries = true`.
* Continuation tokens must be validated against the current query before provider execution or inside the provider.
* The normalized query must carry the resolved `Take` value.

## Validation Rules for DocumentCountQuery

The validator must apply these rules:

* `query` must not be null.
* `DocumentKey == null` is a full scan.
* Full scans require `options.AllowFullScans = true`.
* Full scans require `query.AllowFullScan = true` when `RequireExplicitFullScanApproval = true`.
* `Filter = FullMatch` requires non-empty `PartitionKey` and non-empty `RowKey`.
* `Filter = RowKeyPrefixMatch` requires non-empty `PartitionKey`.
* `Filter = RowKeySuffixMatch` requires non-empty `PartitionKey` and non-empty `RowKey`.
* Unsupported provider capabilities must fail before provider execution.
* Client-side provider support must fail when `RejectClientSideFilteredQueries = true`.

## Continuation Token Design

The public continuation token is an opaque string.

Internally, it is a base64url-encoded JSON envelope.

```csharp
internal sealed class DocumentContinuationToken
{
    public string Provider { get; init; } = default!;

    public int Version { get; init; } = 1;

    public string QueryHash { get; init; } = default!;

    public string? PartitionKey { get; init; }

    public string? RowKey { get; init; }

    public string? NativeToken { get; init; }

    public Dictionary<string, string>? Properties { get; init; }
}
```

## Token Fields

| Field          | Purpose                                                                                      |
| -------------- | -------------------------------------------------------------------------------------------- |
| `Provider`     | Provider discriminator, for example `ef`, `cosmos`, `azure-table`, `azure-blob`, `inmemory`. |
| `Version`      | Token schema version.                                                                        |
| `QueryHash`    | Stable hash of the logical query.                                                            |
| `PartitionKey` | Last returned partition key for keyset-based paging.                                         |
| `RowKey`       | Last returned row key for keyset-based paging.                                               |
| `NativeToken`  | Provider-native continuation token.                                                          |
| `Properties`   | Optional provider-specific metadata.                                                         |

## Token Requirements

Continuation tokens must follow these rules:

* Application code must never inspect the token.
* Tokens must not contain document payloads.
* Tokens must not contain secrets.
* Tokens must be rejected if the provider discriminator does not match.
* Tokens must be rejected if the version is unsupported.
* Tokens must be rejected if the query hash does not match.
* Tokens must be reusable only for the same logical query.
* Tokens may be reused with a different `Take` value for the same logical query.

## Query Hash

The query hash must include:

* normalized storage type name
* `DocumentKey.PartitionKey`
* `DocumentKey.RowKey`
* `DocumentKeyFilter`
* `AllowFullScan`

The query hash must exclude:

* `Take`
* `ContinuationToken`

This allows callers to request the next page with a different page size while preventing continuation across different logical queries.

## Token Serializer

Add an internal serializer.

```csharp
internal interface IDocumentContinuationTokenSerializer
{
    Result<string> Serialize(DocumentContinuationToken token);

    Result<DocumentContinuationToken> Deserialize(string token);
}
```

Invalid tokens must return `Result<DocumentContinuationToken>.Failure(...)` with `DocumentStoreInvalidContinuationTokenError`.

## Result Error Types

Add typed Result errors.

```csharp
public sealed class DocumentStoreQueryTooBroadError : ResultErrorBase
{
    public DocumentStoreQueryTooBroadError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class DocumentStorePageSizeExceededError : ResultErrorBase
{
    public DocumentStorePageSizeExceededError(int take, int maxTake)
        : base($"Requested document page size {take} exceeds the maximum page size {maxTake}.")
    {
        this.Take = take;
        this.MaxTake = maxTake;
    }

    public int Take { get; }

    public int MaxTake { get; }
}
```

```csharp
public sealed class DocumentStoreQueryNotSupportedError : ResultErrorBase
{
    public DocumentStoreQueryNotSupportedError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class DocumentStoreInvalidContinuationTokenError : ResultErrorBase
{
    public DocumentStoreInvalidContinuationTokenError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class DocumentStoreNotFoundError : ResultErrorBase
{
    public DocumentStoreNotFoundError(DocumentKey documentKey)
        : base($"Document with partition key '{documentKey.PartitionKey}' and row key '{documentKey.RowKey}' was not found.")
    {
        this.DocumentKey = documentKey;
    }

    public DocumentKey DocumentKey { get; }
}
```

```csharp
public sealed class DocumentStoreSerializationError : ResultErrorBase
{
    public DocumentStoreSerializationError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class DocumentStoreProviderError : ResultErrorBase
{
    public DocumentStoreProviderError(string message)
        : base(message)
    {
    }
}
```

Reuse existing devkit errors where already available:

* concurrency errors
* validation errors
* exception errors

Do not introduce duplicate error types if suitable existing ones are already present.

## DocumentStoreClient<T> Behavior

`DocumentStoreClient<T>` is responsible for shared validation and delegation.

It must not expose non-Result methods.

```csharp
public sealed class DocumentStoreClient<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    private readonly IDocumentStoreProvider provider;
    private readonly DocumentStoreOptions options;
    private readonly DocumentQueryValidator validator;

    public Task<Result<T>> GetResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return this.provider.GetResultAsync<T>(documentKey, cancellationToken);
    }

    public async Task<Result<DocumentPage<T>>> FindPageResultAsync(
        DocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.validator.NormalizeAndValidate(
            query,
            this.options,
            this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<DocumentPage<T>>();
        }

        return await this.provider.FindPageResultAsync<T>(
            validationResult.Value,
            cancellationToken);
    }

    public async Task<Result<DocumentKeyPage>> ListPageResultAsync(
        DocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.validator.NormalizeAndValidate(
            query,
            this.options,
            this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<DocumentKeyPage>();
        }

        return await this.provider.ListPageResultAsync<T>(
            validationResult.Value,
            cancellationToken);
    }

    public async Task<Result<long>> CountResultAsync(
        DocumentCountQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.validator.NormalizeAndValidate(
            query,
            this.options,
            this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<long>();
        }

        return await this.provider.CountResultAsync<T>(
            validationResult.Value,
            cancellationToken);
    }

    public Task<Result<bool>> ExistsResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return this.provider.ExistsResultAsync<T>(documentKey, cancellationToken);
    }

    public Task<Result> UpsertResultAsync(
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default)
    {
        return this.provider.UpsertResultAsync(documentKey, entity, cancellationToken);
    }

    public Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        return this.provider.UpsertResultAsync<T>(entities, cancellationToken);
    }

    public Task<Result> DeleteResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return this.provider.DeleteResultAsync<T>(documentKey, cancellationToken);
    }
}
```

`GetResultAsync` does not require `DocumentQuery`, because exact key lookup is safe and common.

`GetResultAsync` must return `Result<T>.Failure(...)` with `DocumentStoreNotFoundError` when the document does not exist.

## Provider Result Rules

Each provider method must follow these rules:

* Return `Result.Success(...)` for successful operations.
* Return `Result.Failure(...)` for expected failures.
* Catch provider-specific exceptions and map them to typed Result errors.
* Do not catch cancellation exceptions caused by caller cancellation unless the devkit has a global convention for that.
* Do not throw for invalid query shape.
* Do not throw for unsupported filter semantics.
* Do not throw for missing exact-key documents.
* Do not throw for invalid continuation tokens.
* Do not return partially successful pages.
* Do not deserialize or download content for `ListPageResultAsync`.

## Provider Implementation: InMemory

## InMemory Feasibility

InMemory paging is fully feasible.

## InMemory Capabilities

```csharp
public DocumentStoreProviderCapabilities Capabilities { get; } = new()
{
    FullMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeySuffixMatch = DocumentQuerySupport.SupportedEfficiently,
    FullScan = DocumentQuerySupport.SupportedEfficiently,
    KeyListing = DocumentQuerySupport.SupportedEfficiently,
    SupportsContinuationPaging = true,
    SupportsServerSideCount = true,
    SupportsKeyOnlyProjection = true
};
```

## InMemory Paging Strategy

Use deterministic keyset paging.

* Get matching entries from the in-memory context.
* Order by `PartitionKey`, then `RowKey`.
* If a continuation token exists, skip entries up to and including the last returned key.
* Take `Take + 1`.
* Return only `Take`.
* If an extra item exists, create the next token from the last returned key.
* Return `Result<DocumentPage<T>>` or `Result<DocumentKeyPage>`.

## InMemory Token Strategy

Use `PartitionKey` and `RowKey` in the token.

```json
{
  "Provider": "inmemory",
  "Version": 1,
  "QueryHash": "...",
  "PartitionKey": "last-partition",
  "RowKey": "last-row"
}
```

## InMemory Count Strategy

Use the in-memory filtered sequence count.

No deserialization should be necessary beyond what the in-memory context already stores.

## Provider Implementation: Entity Framework

## Entity Framework Feasibility

Entity Framework paging is fully feasible.

The current provider already has the right building blocks:

* type-based query
* partition-based query
* key filter application
* `AsNoTracking`
* ordering by key
* content projection for document reads
* key projection for key listing

## Entity Framework Capabilities

```csharp
public DocumentStoreProviderCapabilities Capabilities { get; } = new()
{
    FullMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
    RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide,
    FullScan = DocumentQuerySupport.SupportedServerSide,
    KeyListing = DocumentQuerySupport.SupportedEfficiently,
    SupportsContinuationPaging = true,
    SupportsServerSideCount = true,
    SupportsKeyOnlyProjection = true
};
```

## Entity Framework Paging Strategy

Use keyset paging.

Do not use `Skip`.

Use `Take + 1` to determine whether another page exists.

## Entity Framework Type-Wide Query

For type-wide queries:

```csharp
var query = this.QueryByType(dbContext, identity)
    .AsNoTracking()
    .OrderBy(e => e.PartitionKey)
    .ThenBy(e => e.RowKey);
```

Apply continuation:

```csharp
query = query.Where(e =>
    string.Compare(e.PartitionKey, token.PartitionKey) > 0 ||
    e.PartitionKey == token.PartitionKey &&
    string.Compare(e.RowKey, token.RowKey) > 0);
```

## Entity Framework Partition-Bound Query

For partition-bound queries:

```csharp
var query = this.ApplyDocumentKeyFilter(
        this.QueryByTypeAndPartition(dbContext, identity, key),
        key,
        documentQuery.Filter)
    .AsNoTracking()
    .OrderBy(e => e.RowKey);
```

Apply continuation:

```csharp
query = query.Where(e => string.Compare(e.RowKey, token.RowKey) > 0);
```

## Entity Framework Find Projection

`FindPageResultAsync` needs key and content.

```csharp
.Select(e => new StorageDocumentPageRow
{
    PartitionKey = e.PartitionKey,
    RowKey = e.RowKey,
    Content = e.Content
});
```

Deserialize only the returned page.

## Entity Framework List Projection

`ListPageResultAsync` must project only key fields.

```csharp
.Select(e => new StorageDocumentPageRow
{
    PartitionKey = e.PartitionKey,
    RowKey = e.RowKey
});
```

Do not project `Content`.

Do not deserialize content.

## Entity Framework Count Strategy

Use `LongCountAsync` with the same query filter.

Return `Result<long>.Success(count)`.

Do not deserialize documents.

## Entity Framework Token Strategy

For type-wide queries:

```json
{
  "Provider": "ef",
  "Version": 1,
  "QueryHash": "...",
  "PartitionKey": "last-partition",
  "RowKey": "last-row"
}
```

For partition-bound queries:

```json
{
  "Provider": "ef",
  "Version": 1,
  "QueryHash": "...",
  "PartitionKey": "query-partition",
  "RowKey": "last-row"
}
```

## Provider Implementation: Cosmos DB

## Cosmos Feasibility

Cosmos paging is feasible, but the existing `CosmosSqlProvider<TItem>` must be extended.

The current `ReadItemsAsync` method supports `take`, `skip`, ordering, expressions, and partition key, but it consumes all Cosmos feed pages internally and returns an accumulated result set. That is not sufficient for continuation-token based document paging.

## Cosmos Capabilities

```csharp
public DocumentStoreProviderCapabilities Capabilities { get; } = new()
{
    FullMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
    RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide,
    FullScan = DocumentQuerySupport.SupportedServerSide,
    KeyListing = DocumentQuerySupport.SupportedServerSide,
    SupportsContinuationPaging = true,
    SupportsServerSideCount = true,
    SupportsKeyOnlyProjection = true
};
```

## Cosmos Provider Page Model

Add:

```csharp
public sealed class CosmosSqlPage<TItem>
{
    public IReadOnlyCollection<TItem> Items { get; init; } = [];

    public string? ContinuationToken { get; init; }

    public double RequestCharge { get; init; }

    public string? ActivityId { get; init; }
}
```

## Cosmos Provider Page Methods

Add to `ICosmosSqlProvider<TItem>`:

```csharp
Task<Result<CosmosSqlPage<TItem>>> ReadItemsPageResultAsync(
    IEnumerable<Expression<Func<TItem, bool>>> expressions = null,
    int? take = null,
    Expression<Func<TItem, object>> orderExpression = null,
    bool orderDescending = false,
    object partitionKeyValue = null,
    string? continuationToken = null,
    CancellationToken cancellationToken = default);
```

Add convenience overload:

```csharp
Task<Result<CosmosSqlPage<TItem>>> ReadItemsPageResultAsync(
    Expression<Func<TItem, bool>> expression,
    int? take = null,
    Expression<Func<TItem, object>> orderExpression = null,
    bool orderDescending = false,
    object partitionKeyValue = null,
    string? continuationToken = null,
    CancellationToken cancellationToken = default);
```

## Cosmos Projection Page Methods

Because `ListPageResultAsync` must not load document content, add projection support.

```csharp
Task<Result<CosmosSqlPage<TResult>>> ReadItemsPageResultAsync<TResult>(
    IEnumerable<Expression<Func<TItem, bool>>> expressions,
    Expression<Func<TItem, TResult>> projection,
    int? take = null,
    Expression<Func<TItem, object>> orderExpression = null,
    bool orderDescending = false,
    object partitionKeyValue = null,
    string? continuationToken = null,
    CancellationToken cancellationToken = default);
```

Add optional single-expression overload:

```csharp
Task<Result<CosmosSqlPage<TResult>>> ReadItemsPageResultAsync<TResult>(
    Expression<Func<TItem, bool>> expression,
    Expression<Func<TItem, TResult>> projection,
    int? take = null,
    Expression<Func<TItem, object>> orderExpression = null,
    bool orderDescending = false,
    object partitionKeyValue = null,
    string? continuationToken = null,
    CancellationToken cancellationToken = default);
```

## Cosmos Page Method Rules

The new paged method must:

* Initialize the container like the existing `ReadItemsAsync`.
* Create `QueryRequestOptions` using the existing provider helper.
* Set `QueryRequestOptions.MaxItemCount` from `take`.
* Pass the incoming native continuation token into `GetItemLinqQueryable`.
* Apply `WhereIf` expressions.
* Apply `OrderByIf` when requested.
* Apply projection for projection overloads.
* Convert to feed iterator.
* Call `ReadNextAsync` only once.
* Return `response.Resource` as items.
* Return `response.ContinuationToken`.
* Preserve request charge logging.
* Preserve ETag synchronization where applicable.
* Do not use `skip`.
* Do not loop through all pages.
* Return `Result<CosmosSqlPage<TItem>>` or `Result<CosmosSqlPage<TResult>>`.

## Cosmos Take Handling

Prefer `QueryRequestOptions.MaxItemCount = take`.

Avoid applying `.Take(take)` inside the Cosmos paged provider method unless tests prove that it does not interfere with continuation behavior.

## Cosmos FindPageResultAsync

Use full item loading for `FindPageResultAsync`, because content is needed.

```csharp
var cosmosPageResult = await this.provider.ReadItemsPageResultAsync(
    expressions: expressions,
    take: query.Take,
    orderExpression: x => x.RowKey,
    partitionKeyValue: type,
    continuationToken: nativeToken,
    cancellationToken: cancellationToken);

if (cosmosPageResult.IsFailure)
{
    return cosmosPageResult.For<DocumentPage<T>>();
}
```

Deserialize only the returned page.

## Cosmos ListPageResultAsync

Use projection.

```csharp
var cosmosPageResult = await this.provider.ReadItemsPageResultAsync(
    expressions: expressions,
    projection: x => new CosmosDocumentKeyProjection
    {
        PartitionKey = x.PartitionKey,
        RowKey = x.RowKey
    },
    take: query.Take,
    orderExpression: x => x.RowKey,
    partitionKeyValue: type,
    continuationToken: nativeToken,
    cancellationToken: cancellationToken);

if (cosmosPageResult.IsFailure)
{
    return cosmosPageResult.For<DocumentKeyPage>();
}
```

Projection type:

```csharp
public sealed class CosmosDocumentKeyProjection
{
    public string PartitionKey { get; init; } = default!;

    public string RowKey { get; init; } = default!;
}
```

Do not load `Content`.

Do not deserialize document payloads.

## Cosmos Token Strategy

Wrap the native Cosmos continuation token.

```json
{
  "Provider": "cosmos",
  "Version": 1,
  "QueryHash": "...",
  "NativeToken": "<cosmos-continuation-token>"
}
```

## Cosmos Count Strategy

Preferred:

* Add a server-side count method to `ICosmosSqlProvider<TItem>` if not already available.

Suggested API:

```csharp
Task<Result<long>> CountItemsResultAsync(
    IEnumerable<Expression<Func<TItem, bool>>> expressions = null,
    object partitionKeyValue = null,
    CancellationToken cancellationToken = default);
```

Fallback:

* Count by iterating paged projected key results.
* Do not load content.
* Do not deserialize document payloads.

## Provider Implementation: Azure Table Storage

## Azure Table Feasibility

Azure Table paging is fully feasible.

The current provider already uses Azure SDK `AsPages()`, but it currently accumulates all pages into a set. This must be changed to return one logical page.

## Azure Table Capabilities

```csharp
public DocumentStoreProviderCapabilities Capabilities { get; } = new()
{
    FullMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeySuffixMatch = DocumentQuerySupport.Unsupported,
    FullScan = DocumentQuerySupport.SupportedServerSide,
    KeyListing = DocumentQuerySupport.SupportedEfficiently,
    SupportsContinuationPaging = true,
    SupportsServerSideCount = false,
    SupportsKeyOnlyProjection = true
};
```

## Azure Table Paging Strategy

Use Azure Table native continuation tokens.

Use:

```csharp
queryResult.AsPages(
    continuationToken: nativeToken,
    pageSizeHint: query.Take);
```

Read one page.

Return `Page<T>.ContinuationToken` wrapped in the document token envelope.

## Azure Table Supported Filters

| Filter              | Behavior                                                                              |
| ------------------- | ------------------------------------------------------------------------------------- |
| `FullMatch`         | Supported.                                                                            |
| `RowKeyPrefixMatch` | Supported.                                                                            |
| `RowKeySuffixMatch` | Unsupported. Return `Result.Failure(...)` with `DocumentStoreQueryNotSupportedError`. |

## Azure Table ListPageResultAsync

`ListPageResultAsync` must return only:

```csharp
new DocumentKey(entity.PartitionKey, entity.RowKey)
```

Do not convert the full table entity into a document payload.

Do not deserialize content.

## Azure Table FindPageResultAsync

`FindPageResultAsync` converts only the returned table page items to document instances.

## Azure Table Count Strategy

Azure Table count is implemented by paged enumeration.

Rules:

* Use the same query filter.
* Iterate table pages.
* Count entities.
* Do not convert entities to document payloads.
* Do not deserialize content.
* Return `Result<long>.Success(count)`.

## Azure Table Token Strategy

```json
{
  "Provider": "azure-table",
  "Version": 1,
  "QueryHash": "...",
  "NativeToken": "<azure-table-continuation-token>"
}
```

## Provider Implementation: Azure Blob Storage

## Azure Blob Feasibility

Azure Blob paging is feasible, with one caveat:

* Full match is direct.
* Prefix and full scan can use blob listing pages.
* Suffix matching requires client-side filtering and is rejected by default.

## Azure Blob Capabilities

```csharp
public DocumentStoreProviderCapabilities Capabilities { get; } = new()
{
    FullMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
    RowKeySuffixMatch = DocumentQuerySupport.SupportedClientSide,
    FullScan = DocumentQuerySupport.SupportedServerSide,
    KeyListing = DocumentQuerySupport.SupportedEfficiently,
    SupportsContinuationPaging = true,
    SupportsServerSideCount = false,
    SupportsKeyOnlyProjection = true
};
```

## Azure Blob Naming

Blob documents are stored as:

```text
<PartitionKey>__<RowKey>
```

The provider must keep using the existing naming convention.

## Azure Blob Full Match

For full match:

* Build exact blob name.
* Use `GetBlobClient(blobName)`.
* Check existence.
* Return zero or one item.
* No continuation token is needed.

## Azure Blob Prefix Query

For prefix:

```text
<PartitionKey>__<RowKeyPrefix>
```

Use blob listing with prefix:

```csharp
container.GetBlobsAsync(prefix: prefix)
```

Then use:

```csharp
.AsPages(nativeToken, query.Take)
```

## Azure Blob Full Scan

For full scan:

* Require global full scans enabled.
* Require query-level `AllowFullScan = true`.
* Use container blob listing.
* Page through blob names.
* For `ListPageResultAsync`, return keys only.
* For `FindPageResultAsync`, download only blobs on the returned page.

## Azure Blob Suffix Query

Suffix query is classified as client-side.

Default behavior:

* If `RejectClientSideFilteredQueries = true`, return `Result.Failure(...)` with `DocumentStoreQueryNotSupportedError`.

Optional behavior:

* If `RejectClientSideFilteredQueries = false`, the provider may enumerate blob names and filter by suffix.
* `ListPageResultAsync` still must not download blobs.
* `FindPageResultAsync` downloads only blobs selected for the returned page.
* `CountResultAsync` counts matching names only and must not download blobs.

## Azure Blob ListPageResultAsync

`ListPageResultAsync` must only list blob names and parse them into `DocumentKey`.

It must not download blobs.

## Azure Blob FindPageResultAsync

`FindPageResultAsync` must:

* Page blob names.
* Select up to `Take` names.
* Download only those blobs.
* Deserialize only those blobs.
* Return the provider continuation token.
* Return `Result<DocumentPage<T>>`.

## Azure Blob Count Strategy

Blob count uses paged blob listing.

Rules:

* Do not download blobs.
* Prefix count uses prefix listing.
* Full scan count uses full listing and requires full scan approval.
* Suffix count requires client-side filtering and follows the client-side filtering option.
* Return `Result<long>.Success(count)`.

## Azure Blob Token Strategy

For normal blob listing:

```json
{
  "Provider": "azure-blob",
  "Version": 1,
  "QueryHash": "...",
  "NativeToken": "<azure-blob-continuation-token>"
}
```

For v1, do not implement buffered suffix tokens unless suffix support is explicitly enabled and tests require it.

## GetResultAsync Semantics

`GetResultAsync(DocumentKey)` is an exact-key convenience method.

Rules:

* It must not require `DocumentQuery`.
* It must return `Result<T>.Success(document)` when the document exists.
* It must return `Result<T>.Failure(...)` with `DocumentStoreNotFoundError` when the document does not exist.
* It must never return more than one item.
* It must use the provider's efficient exact-key lookup where available.
* It must deserialize one document at most.
* It must not return `DocumentPage<T>`.
* It must not return `Result<T>.Success(null)`.

Provider mapping:

| Provider         | Strategy                                            |
| ---------------- | --------------------------------------------------- |
| InMemory         | exact context lookup                                |
| Entity Framework | exact key query using type, partition, row key/hash |
| Cosmos           | exact logical query by type + partition + row       |
| Azure Table      | exact table entity lookup                           |
| Azure Blob       | direct blob lookup                                  |

## CountResultAsync Semantics

`CountResultAsync(DocumentCountQuery)` returns the number of matching documents.

It must not deserialize document payloads.

Provider behavior:

| Provider         | Strategy                                                            |
| ---------------- | ------------------------------------------------------------------- |
| InMemory         | local count                                                         |
| Entity Framework | `LongCountAsync`                                                    |
| Cosmos           | server-side count if available, otherwise paged projected key count |
| Azure Table      | paged entity count                                                  |
| Azure Blob       | paged blob-name count                                               |

Full scan counts require the same full scan approval as full scan reads.

## ExistsResultAsync Semantics

`ExistsResultAsync(DocumentKey)` checks for exact-key existence.

Rules:

* It must return `Result<bool>.Success(true)` when the document exists.
* It must return `Result<bool>.Success(false)` when the document does not exist.
* It must not return not-found as failure.
* It must not load or deserialize the document payload.
* It must use efficient provider existence checks where possible.

## UpsertResultAsync Semantics

`UpsertResultAsync` inserts or updates documents.

Rules:

* It must validate the key.
* It must validate the entity.
* It must serialize the entity.
* It must return `Result.Success()` on success.
* It must return `Result.Failure(...)` on validation, serialization, provider, concurrency, or lease failures.
* Batch upsert must not enumerate the input multiple times unless explicitly materialized once.
* Batch upsert should fail as one operation when any item is invalid.

## DeleteResultAsync Semantics

`DeleteResultAsync` deletes a document by exact key.

Rules:

* It must validate the key.
* It must return `Result.Success()` when the document is deleted or when delete is idempotent and missing is acceptable.
* If the existing behavior treats missing delete as failure, preserve that behavior but return a typed Result error.
* It must return `Result.Failure(...)` on provider, concurrency, or lease failures.

## Caching Behavior

The existing cache behavior must be updated for Result-native APIs.

## Cache Key

Paged read cache keys must include:

* document type
* operation name
* partition key
* row key
* filter
* take
* continuation token
* allow full scan

## Cache Safety

Default recommendation:

* Do not cache paged reads by default.
* Allow opt-in caching later through options.

## Write Invalidation

Any `UpsertResultAsync` or `DeleteResultAsync` should invalidate cached entries for:

* exact key
* document type
* affected partition

If broad invalidation is simpler, invalidate all document store cache entries for the document type.

## Result-Aware Caching

Do not cache failures by default.

Only cache successful `Result<T>` values unless explicitly configured otherwise.

## Logging and Observability

Add structured logging for paged reads.

Log fields:

* document type
* provider name
* operation name
* has document key
* filter
* take
* has continuation token
* allow full scan
* result success/failure
* result count when successful
* has more when successful
* error types when failed
* query support classification
* elapsed time

Do not log raw continuation token values.

Do not log document content.

Example log message:

```csharp
logger.LogDebug(
    "DocumentStore {Operation} completed for {DocumentType} using {Provider}. Success={Success}, Filter={Filter}, Take={Take}, Items={ItemCount}, HasMore={HasMore}, FullScan={AllowFullScan}",
    operation,
    documentType,
    providerName,
    result.IsSuccess,
    query.Filter,
    query.Take,
    result.IsSuccess ? result.Value.Items.Count : 0,
    result.IsSuccess && result.Value.HasMore,
    query.AllowFullScan);
```

## Testing Requirements

## Shared Contract Tests

Create provider-neutral contract tests that run against all providers.

Required tests:

* `GetResultAsync` returns success for existing document by exact key.
* `GetResultAsync` returns failure with `DocumentStoreNotFoundError` for missing document.
* `ExistsResultAsync` returns success with `true` for existing document.
* `ExistsResultAsync` returns success with `false` for missing document.
* `FindPageResultAsync` returns document payloads.
* `FindPageResultAsync` returns no more than `Take` items.
* `FindPageResultAsync` returns continuation token when more items exist.
* `FindPageResultAsync` can retrieve the next page using continuation token.
* `ListPageResultAsync` returns document keys.
* `ListPageResultAsync` returns no more than `Take` keys.
* `ListPageResultAsync` can retrieve the next page using continuation token.
* `ListPageResultAsync` does not deserialize document content.
* Full scan without `AllowFullScan` fails.
* Full scan with `AllowFullScan` succeeds only when global full scans are enabled.
* `Take = 0` fails.
* `Take < 0` fails.
* `Take > MaxTake` fails.
* Continuation token cannot be reused for a different query.
* Prefix query returns only matching row keys.
* Suffix query behavior matches provider capabilities.
* `CountResultAsync` returns expected count.
* `CountResultAsync` does not deserialize document content.
* `UpsertResultAsync` returns success for valid documents.
* `DeleteResultAsync` returns success or expected typed failure according to delete semantics.
* Expected failures are Result failures, not thrown exceptions.
* Cancellation token is passed to provider operations.

## Builder Tests

Add query builder tests:

* `DocumentQueries.Query().ForKey(...).Build()` creates a full-match query by default.
* `DocumentQueries.Query().ForKey(...).WithRowKeyPrefix().Take(100).Build()` creates a prefix query.
* `DocumentQueries.Query().ForKey(...).WithRowKeySuffix().Take(100).Build()` creates a suffix query.
* `DocumentQueries.Query().AllowFullScan().Take(100).Build()` creates a full scan query.
* `DocumentQueries.Query().ContinueWith(token).Build()` sets the continuation token.
* `DocumentQueries.Query().Take(0)` throws.
* `DocumentQueries.Query().Take(-1)` throws.
* `DocumentQueries.Query().ContinueWith(null)` throws.
* `DocumentQueries.Count().ForKey(...).WithRowKeyPrefix().Build()` creates a count prefix query.
* `DocumentQueries.Count().AllowFullScan().Build()` creates a full-scan count query.
* Builder-created queries pass through the same `DocumentQueryValidator` as manually-created queries.
* Builder does not bypass provider capability checks.

## InMemory Tests

Additional tests:

* Paging order is deterministic regardless of insert order.
* Keyset token works across multiple partitions.
* Prefix and suffix filters both work.
* Full scan works only when enabled.
* All public methods return `Result` or `Result<T>`.

## Entity Framework Tests

Additional tests:

* Paging uses keyset semantics.
* `ListPageResultAsync` projects only key columns.
* `FindPageResultAsync` projects key plus content.
* Count uses database count.
* Prefix query works.
* Suffix query works.
* Full scan requires approval.
* Provider exceptions are converted to Result failures where appropriate.

## Cosmos Tests

Additional tests:

* `ReadItemsPageResultAsync` reads only one Cosmos feed page.
* Native continuation token is wrapped and restored.
* `skip` is not used for continuation paging.
* `ListPageResultAsync` uses projection and does not load content.
* `FindPageResultAsync` loads content only for returned page.
* Count uses server-side count or projected paged counting.
* Query hash rejects mismatched continuation tokens.
* Cosmos provider methods return `Result<CosmosSqlPage<T>>`.

## Azure Table Tests

Additional tests:

* Uses `.AsPages(nativeToken, pageSizeHint)`.
* Reads one page only.
* Prefix query works.
* Suffix query returns failure with `DocumentStoreQueryNotSupportedError`.
* `ListPageResultAsync` does not convert payload.
* Count enumerates pages without payload conversion.
* Azure Table request failures are converted to Result failures.

## Azure Blob Tests

Additional tests:

* Full match uses direct blob lookup.
* Prefix query uses blob prefix listing.
* `ListPageResultAsync` does not download blobs.
* `FindPageResultAsync` downloads only blobs in returned page.
* Suffix query fails by default with `DocumentStoreQueryNotSupportedError`.
* Suffix query can work only when client-side filtering is explicitly allowed.
* Count does not download blobs.
* Full scan requires approval.
* Azure Blob request failures are converted to Result failures.

## Implementation Order

Recommended implementation order:

* Add `DocumentQuery`.
* Add `DocumentCountQuery`.
* Add `DocumentPage<T>`.
* Add `DocumentKeyPage`.
* Add `DocumentQueries`.
* Add `DocumentQueryBuilder`.
* Add `DocumentCountQueryBuilder`.
* Add `DocumentStoreOptions`.
* Add `DocumentQuerySupport`.
* Add `DocumentStoreProviderCapabilities`.
* Add typed Result error types.
* Add continuation token model and Result-native serializer.
* Add Result-native query validator.
* Replace `IDocumentStoreClient<T>` API.
* Replace `IDocumentStoreProvider` API.
* Update `DocumentStoreClient<T>`.
* Implement InMemory provider Result-native paging.
* Implement Entity Framework provider Result-native paging.
* Extend `ICosmosSqlProvider<TItem>` with Result-native page and projection page APIs.
* Implement Cosmos provider Result-native paging.
* Implement Azure Table provider Result-native paging.
* Implement Azure Blob provider Result-native paging.
* Remove old `DocumentStoreClientResultExtensions` if all methods are now native.
* Update cache behavior.
* Update logging behavior.
* Update documentation.
* Add shared contract tests.
* Add builder tests.
* Add provider-specific tests.

## Acceptance Criteria

The implementation is complete when:

* No public unbounded document read API remains.
* No public non-Result document client API remains.
* No public non-Result document provider API remains.
* `GetResultAsync` retrieves one exact document or returns a not-found Result failure.
* `ExistsResultAsync` returns success with `true` or `false`.
* `FindPageResultAsync` returns document payload instances.
* `ListPageResultAsync` returns document keys only.
* `ListPageResultAsync` does not load document payloads in any provider.
* `FindPageResultAsync` never returns more than `Take` items.
* `ListPageResultAsync` never returns more than `Take` keys.
* Every provider supports continuation-token paging.
* Continuation tokens are opaque strings.
* Continuation tokens are query-bound.
* Invalid continuation token usage fails with typed Result errors.
* Full scans require global and query-level approval.
* Client-side filtered queries are rejected by default.
* Blob suffix matching is rejected by default.
* Azure Table suffix matching is unsupported and returns typed Result failure.
* EF uses keyset paging.
* Cosmos uses native continuation tokens.
* Cosmos `ListPageResultAsync` uses projection and does not load document content.
* Azure Table uses SDK continuation tokens.
* Azure Blob uses SDK continuation tokens.
* Count operations do not deserialize document payloads.
* Document Storage does not use `ResultPaged<T>`.
* `DocumentQueries.Query()` creates a `DocumentQueryBuilder`.
* `DocumentQueries.Count()` creates a `DocumentCountQueryBuilder`.
* Builders produce only `DocumentQuery` and `DocumentCountQuery`.
* Builders do not execute provider calls.
* Builders do not duplicate provider validation.
* Builders support full match, prefix, suffix, full scan, take, and continuation.
* Count builder supports full match, prefix, suffix, and full scan.
* Builder-created queries work with `FindPageResultAsync`, `ListPageResultAsync`, and `CountResultAsync`.
* Full scan still requires both builder-level `.AllowFullScan()` and enabled options.
* Provider capability checks still run after builder usage.
* Shared contract tests pass for all providers.
* Provider-specific tests pass for all providers.

## Example Usage

## Get Exact Document

```csharp
var result = await documents.GetResultAsync(
    new DocumentKey("people", "DE-42"),
    cancellationToken);

if (result.IsSuccess)
{
    var person = result.Value;
}
```

## Find First Page of Documents

```csharp
var result = await documents.FindPageResultAsync(
    DocumentQueries
        .Query()
        .ForKey("people", "DE-")
        .WithRowKeyPrefix()
        .Take(100)
        .Build(),
    cancellationToken);

if (result.IsSuccess)
{
    var page = result.Value;
}
```

## Continue Document Query

```csharp
var nextPageResult = await documents.FindPageResultAsync(
    DocumentQueries
        .Query()
        .ForKey("people", "DE-")
        .WithRowKeyPrefix()
        .Take(100)
        .ContinueWith(result.Value.ContinuationToken)
        .Build(),
    cancellationToken);
```

## List Keys Only

```csharp
var keysResult = await documents.ListPageResultAsync(
    DocumentQueries
        .Query()
        .ForKey("people", "DE-")
        .WithRowKeyPrefix()
        .Take(500)
        .Build(),
    cancellationToken);

if (keysResult.IsSuccess)
{
    foreach (var key in keysResult.Value.Items)
    {
        // retrieve later with GetResultAsync if needed
    }
}
```

## Count Documents

```csharp
var countResult = await documents.CountResultAsync(
    DocumentQueries
        .Count()
        .ForKey("people", "DE-")
        .WithRowKeyPrefix()
        .Build(),
    cancellationToken);
```

## Explicit Full Scan

```csharp
var pageResult = await documents.ListPageResultAsync(
    DocumentQueries
        .Query()
        .AllowFullScan()
        .Take(500)
        .Build(),
    cancellationToken);
```

This only works when `DocumentStoreOptions.AllowFullScans` is also enabled.

## Agent Implementation Notes

The implementation agent must not preserve the old API.

The implementation agent must avoid quick fixes such as:

* implementing paging with `Skip`
* returning all results and slicing in the client
* loading document content for `ListPageResultAsync`
* deserializing payloads for count
* exposing raw provider continuation tokens
* using `ResultPaged<T>`
* keeping non-Result public client methods
* keeping non-Result public provider methods
* treating Blob suffix matching as normal efficient provider behavior
* silently allowing full scans
* throwing for expected query validation failures
* returning `Result<T>.Success(null)` from `GetResultAsync`
* implementing the fluent builder as an execution API
* duplicating provider validation inside the fluent builder

The implementation agent should prefer shared helper methods for:

* query validation
* capability checks
* continuation token serialization
* query hash calculation
* page creation
* key projection
* provider exception mapping
* Result error creation

Provider-specific code should stay provider-specific only where it deals with actual storage mechanics.
