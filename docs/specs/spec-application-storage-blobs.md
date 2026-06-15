---
status: draft
---

# Design Specification: Blob Storage

> Provide Result-native, stream-first binary storage with provider-agnostic keys, properties, leases, and continuation-token based listing.

[TOC]

## Overview

Blob Storage is a separate feature from Document Storage.

Document Storage deals with typed class instances and entity-like documents. Blob Storage deals with binary content such as files, exports, images, reports, attachments, generated artifacts, and other stream or byte based payloads.

The feature provides a provider-agnostic API for uploading, downloading, listing, inspecting, updating properties, checking existence, and deleting blobs.

The API is Result-native and stream-first. Large content should be transferred through streams instead of materialized as byte arrays by default.

The feature supports these providers:

* InMemory
* Entity Framework Core
* Azure Blob Storage

The feature explicitly excludes these providers for v1:

* Azure Table Storage
* Cosmos DB
* FileSystem

Entity Framework Core is a first-class provider and stores blob content using a chunked storage model. EF Core also provides internal lease support similar to Document Storage. Other providers use their native consistency, leasing, or concurrency mechanisms where available.

## Goals

The goals of this feature are:

* Provide a separate binary/blob storage abstraction.
* Keep the API Result-native.
* Use streams as the primary content transfer mechanism.
* Use a provider-neutral `BlobKey`.
* Support metadata-like blob properties through `PropertyBag`.
* Support efficient property lookup without downloading content.
* Support continuation-token based listing.
* Require explicit opt-in for full container scans.
* Support idempotent delete.
* Support property updates without re-uploading content.
* Support EF Core as a first-class durable provider.
* Store EF Core blobs in chunks.
* Support internal provider leases for write/delete consistency.
* Avoid range-download complexity in v1.
* Avoid provider-specific concepts in the public client API.

## Non-Goals

This feature does not introduce:

* Typed document/entity storage.
* JSON serialization of typed entities as the primary model.
* Cosmos DB provider.
* Azure Table Storage provider.
* FileSystem provider.
* Range downloads.
* Public lease management APIs.
* Page-number or offset-based listing.
* Arbitrary content indexing.
* Content search.
* Directory semantics beyond prefix-based blob names.
* Virus scanning, content inspection, or DLP.
* Presigned URLs or public access URLs.
* Automatic content-type inference as required behavior.
* Multi-container scans.

## Terminology

| Term               | Meaning                                                                                         |
| ------------------ | ----------------------------------------------------------------------------------------------- |
| Blob               | Binary content stored under a `BlobKey`.                                                        |
| BlobKey            | Logical key consisting of `Container` and `Name`.                                               |
| Container          | Logical top-level grouping for blobs. Maps to an Azure Blob container or EF/InMemory namespace. |
| Name               | Path-like object name inside a container.                                                       |
| Properties         | Provider-neutral blob information and custom property bag values.                               |
| PropertyBag        | Devkit key/value property container used for custom blob properties.                            |
| BlobInfo           | Blob metadata/properties returned by upload, list, properties, and update operations.           |
| BlobDownload       | Download result containing a readable stream and blob info.                                     |
| Continuation token | Opaque token used to continue listing blobs.                                                    |
| Full scan          | Listing a container without a prefix.                                                           |
| Lease              | Internal provider mechanism used to coordinate write/delete operations.                         |
| Result-native API  | API that returns `Result` or `Result<T>` directly.                                              |

## Design Principles

* Blob Storage is independent from Document Storage.
* Blob Storage stores streams/bytes, not typed class instances.
* The public API is Result-native.
* The public API is stream-first.
* Callers own and dispose returned download streams.
* Listing returns blob information only and never downloads content.
* Properties can be read without downloading content.
* Custom properties use `PropertyBag`.
* Listing is continuation-token based.
* Continuation tokens are opaque to callers.
* Full container scans require explicit opt-in.
* `DeleteResultAsync` is idempotent.
* EF Core stores blob content in chunks.
* EF Core write/delete operations use leases.
* Provider-specific details stay inside providers.
* Expected failures are represented as typed Result errors.
* Cancellation remains normal .NET cancellation behavior.

## Public Client API

```csharp
public interface IBlobStoreClient
{
    Task<Result<BlobInfo>> UploadResultAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default);

    Task<Result<BlobDownload>> DownloadResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);

    Task<Result<BlobInfo>> GetPropertiesResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);

    Task<Result<BlobInfo>> UpdatePropertiesResultAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);

    Task<Result<BlobPage>> ListPageResultAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);
}
```

## Provider API

```csharp
public interface IBlobStoreProvider
{
    BlobStoreProviderCapabilities Capabilities { get; }

    Task<Result<BlobInfo>> UploadResultAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default);

    Task<Result<BlobDownload>> DownloadResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);

    Task<Result<BlobInfo>> GetPropertiesResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);

    Task<Result<BlobInfo>> UpdatePropertiesResultAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);

    Task<Result<BlobPage>> ListPageResultAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default);
}
```

Providers are responsible for translating the provider-neutral models into native storage operations.

## Blob Key

```csharp
public sealed record BlobKey(string Container, string Name);
```

Rules:

* `Container` is required.
* `Name` is required.
* `Container` must not be null, empty, or whitespace.
* `Name` must not be null, empty, or whitespace.
* `Name` may contain path-like separators such as `/`.
* `Name` must not be treated as a local filesystem path.
* `Container` and `Name` validation should be shared across providers.
* Provider-specific naming restrictions must be handled through validation Result errors.

Examples:

```csharp
new BlobKey("reports", "2026/06/report.pdf");

new BlobKey("exports", "customer/42/export.csv");

new BlobKey("attachments", "orders/10001/invoice.pdf");
```

## Blob Info

`BlobInfo` represents provider-neutral blob information.

```csharp
public sealed class BlobInfo
{
    public BlobKey Key { get; init; }

    public long Length { get; init; }

    public string? ContentType { get; init; }

    public string? ContentHash { get; init; }

    public string? ETag { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public DateTimeOffset? LastModifiedAt { get; init; }

    public PropertyBag Properties { get; init; } = new();
}
```

Rules:

* `Key` is required.
* `Length` is the content length in bytes.
* `ContentType` is optional.
* `ContentHash` is optional and provider-dependent.
* `ETag` is optional and provider-dependent.
* `CreatedAt` is optional and provider-dependent.
* `LastModifiedAt` is optional and provider-dependent.
* `Properties` contains custom user/application properties.
* `Properties` must not contain the blob content.
* `Properties` should be safe to return from list operations.

## PropertyBag Usage

Blob Storage uses `PropertyBag` for custom blob properties.

Provider-specific mapping rules:

* InMemory can store `PropertyBag` values directly.
* EF Core should persist `PropertyBag` as JSON or another supported serialized representation.
* Azure Blob Storage metadata is string/string, so `PropertyBag` values must be converted to strings where possible.
* Unsupported property value types must produce a typed Result failure.
* Property keys must be validated according to provider restrictions.
* Provider-reserved property names should be rejected or namespaced.
* Standard fields such as `ContentType`, `Length`, `ETag`, and timestamps must remain first-class `BlobInfo` fields, not custom property entries.

Recommended behavior for Azure Blob metadata:

* Convert simple scalar values to invariant strings.
* Preserve strings as-is.
* Reject complex objects unless an explicit serializer strategy is configured.
* Treat metadata keys case-insensitively where provider behavior requires it.

## Blob Upload

```csharp
public sealed class BlobUpload
{
    public BlobKey Key { get; init; }

    public Stream Content { get; init; }

    public string? ContentType { get; init; }

    public PropertyBag Properties { get; init; } = new();

    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;
}
```

```csharp
public enum BlobOverwriteMode
{
    Overwrite,
    FailIfExists
}
```

Rules:

* `Key` is required.
* `Content` is required.
* `Content` must be readable.
* `ContentType` is optional.
* `Properties` is optional and defaults to an empty `PropertyBag`.
* `OverwriteMode.Overwrite` creates or replaces the blob.
* `OverwriteMode.FailIfExists` fails if the blob already exists.
* Upload returns `Result<BlobInfo>`.
* Upload must not close or dispose the input stream.
* The caller remains responsible for disposing the upload stream.
* Upload should stream content to the provider where possible.

## Blob Download

```csharp
public sealed class BlobDownload : IAsyncDisposable
{
    public Stream Content { get; init; }

    public BlobInfo Info { get; init; }

    public ValueTask DisposeAsync()
    {
        return this.Content.DisposeAsync();
    }
}
```

Rules:

* `Content` is required.
* `Info` is required.
* The caller owns and disposes the returned download.
* The returned stream must be readable.
* The returned stream should not require loading the full blob into memory unless the provider is InMemory.
* Missing blob returns `Result<BlobDownload>.Failure(...)` with `BlobStoreNotFoundError`.
* Range downloads are not supported in v1.

Example:

```csharp
var result = await blobs.DownloadResultAsync(
    new BlobKey("reports", "2026/06/report.pdf"),
    cancellationToken);

if (result.IsSuccess)
{
    await using var download = result.Value;
    await download.Content.CopyToAsync(targetStream, cancellationToken);
}
```

## Blob Properties Update

```csharp
public sealed class BlobPropertiesUpdate
{
    public BlobKey Key { get; init; }

    public string? ContentType { get; init; }

    public PropertyBag Properties { get; init; } = new();

    public string? IfMatchETag { get; init; }
}
```

Rules:

* `Key` is required.
* `ContentType` may be updated without uploading content.
* `Properties` replaces the custom property bag unless an explicit merge mode is added later.
* `IfMatchETag` enables optimistic update behavior where supported.
* If `IfMatchETag` is supplied and does not match, return a typed concurrency Result failure.
* Updating properties must not download content.
* Updating properties must not rewrite blob chunks/content unless required by provider limitations.

## Blob Query

```csharp
public sealed class BlobQuery
{
    public string Container { get; init; }

    public string? Prefix { get; init; }

    public int? Take { get; init; }

    public string? ContinuationToken { get; init; }

    public bool AllowFullScan { get; init; }
}
```

Rules:

* `Container` is required.
* `Prefix` is optional.
* `Take` is optional and defaults from options.
* `ContinuationToken` is optional.
* `AllowFullScan` is required when `Prefix` is null or empty.
* Full scan means full scan within one container only.
* Cross-container listing is not supported.
* Suffix filtering is not supported in v1.
* Arbitrary property filtering is not supported in v1.

## Blob Page

```csharp
public sealed class BlobPage
{
    public IReadOnlyCollection<BlobInfo> Items { get; init; } = [];

    public string? ContinuationToken { get; init; }

    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
```

Rules:

* `Items` contains blob information only.
* `Items` must not contain content streams.
* `ListPageResultAsync` must never download content.
* `ContinuationToken` is opaque to callers.
* `HasMore` indicates whether a continuation token exists.

## Fluent Query Builder

Add a fluent builder for blob listing queries.

The builder is a thin construction helper around `BlobQuery`. It must not execute queries or replace validation.

## Blob Query Builder Factory

```csharp
public static class BlobQueries
{
    public static BlobQueryBuilder Query() =>
        BlobQueryBuilder.Create();
}
```

## BlobQueryBuilder API

```csharp
public sealed class BlobQueryBuilder
{
    public static BlobQueryBuilder Create();

    public BlobQueryBuilder InContainer(string container);

    public BlobQueryBuilder WithPrefix(string prefix);

    public BlobQueryBuilder Take(int take);

    public BlobQueryBuilder ContinueWith(string continuationToken);

    public BlobQueryBuilder AllowFullScan();

    public BlobQuery Build();
}
```

## Builder Semantics

The builder should only construct query objects.

It may perform basic local argument validation for clearly invalid values, such as:

* null or whitespace container
* null prefix passed to `WithPrefix`
* `Take <= 0`
* null or whitespace continuation token

Provider and option-dependent validation must remain in `BlobQueryValidator`.

Examples of validation that must stay outside the builder:

* whether full scans are globally enabled
* whether `Take` exceeds configured `MaxTake`
* whether continuation token matches the query
* provider capability checks

## Builder Implementation Sketch

```csharp
public sealed class BlobQueryBuilder
{
    private string? container;
    private string? prefix;
    private int? take;
    private string? continuationToken;
    private bool allowFullScan;

    private BlobQueryBuilder()
    {
    }

    public static BlobQueryBuilder Create() => new();

    public BlobQueryBuilder InContainer(string container)
    {
        if (string.IsNullOrWhiteSpace(container))
        {
            throw new ArgumentException(
                "Container must not be null or whitespace.",
                nameof(container));
        }

        this.container = container;
        return this;
    }

    public BlobQueryBuilder WithPrefix(string prefix)
    {
        if (prefix is null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }

        this.prefix = prefix;
        return this;
    }

    public BlobQueryBuilder Take(int take)
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

    public BlobQueryBuilder ContinueWith(string continuationToken)
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

    public BlobQueryBuilder AllowFullScan()
    {
        this.allowFullScan = true;
        return this;
    }

    public BlobQuery Build() => new()
    {
        Container = this.container,
        Prefix = this.prefix,
        Take = this.take,
        ContinuationToken = this.continuationToken,
        AllowFullScan = this.allowFullScan
    };
}
```

## Options

```csharp
public sealed class BlobStoreOptions
{
    public int DefaultTake { get; set; } = 100;

    public int MaxTake { get; set; } = 1000;

    public bool AllowFullScans { get; set; } = false;

    public bool RequireExplicitFullScanApproval { get; set; } = true;

    public int ChunkSize { get; set; } = 4 * 1024 * 1024;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    public string? LeaseOwner { get; set; }
}
```

## Default Option Values

| Option                            |    Default | Meaning                                         |
| --------------------------------- | ---------: | ----------------------------------------------- |
| `DefaultTake`                     |      `100` | Used when `BlobQuery.Take` is null.             |
| `MaxTake`                         |     `1000` | Maximum allowed listing page size.              |
| `AllowFullScans`                  |    `false` | Disallows full container scans by default.      |
| `RequireExplicitFullScanApproval` |     `true` | Requires `AllowFullScan = true` per query.      |
| `ChunkSize`                       |     `4 MB` | EF Core blob chunk size.                        |
| `LeaseDuration`                   | `1 minute` | EF Core internal lease duration.                |
| `LeaseOwner`                      |     `null` | Optional logical owner used for EF Core leases. |

## Provider Capabilities

```csharp
public sealed class BlobStoreProviderCapabilities
{
    public bool SupportsContinuationPaging { get; init; }

    public bool SupportsPrefixListing { get; init; }

    public bool SupportsFullContainerScan { get; init; }

    public bool SupportsProperties { get; init; }

    public bool SupportsContentType { get; init; }

    public bool SupportsETag { get; init; }

    public bool SupportsContentHash { get; init; }

    public bool SupportsNativeLeases { get; init; }

    public bool SupportsInternalLeases { get; init; }

    public bool SupportsConditionalPropertiesUpdate { get; init; }

    public bool SupportsStreamingUpload { get; init; }

    public bool SupportsStreamingDownload { get; init; }
}
```

## Expected Capability Matrix

| Provider         | Paging | Prefix listing | Full scan | Properties | ETag     | Leases              | Streaming      |
| ---------------- | ------ | -------------- | --------- | ---------- | -------- | ------------------- | -------------- |
| InMemory         | Yes    | Yes            | Yes       | Yes        | Optional | Internal/no-op      | Memory stream  |
| Entity Framework | Yes    | Yes            | Yes       | Yes        | Yes      | Internal EF lease   | Chunked stream |
| Azure Blob       | Yes    | Yes            | Yes       | Yes        | Yes      | Native where needed | Native stream  |

## Query Validation

Create a shared query validator used by `BlobStoreClient` before calling the provider.

```csharp
public sealed class BlobQueryValidator
{
    public Result<BlobQuery> NormalizeAndValidate(
        BlobQuery query,
        BlobStoreOptions options,
        BlobStoreProviderCapabilities capabilities);
}
```

The validator must return typed Result failures, not throw exceptions for expected validation errors.

## Validation Rules for BlobQuery

The validator must apply these rules:

* `query` must not be null.
* `Container` must not be null or whitespace.
* Resolve `Take` from `query.Take ?? options.DefaultTake`.
* `Take` must be greater than zero.
* `Take` must not exceed `options.MaxTake`.
* `Prefix` null or empty means full container scan.
* Full scans require `options.AllowFullScans = true`.
* Full scans require `query.AllowFullScan = true` when `RequireExplicitFullScanApproval = true`.
* Provider must support continuation paging.
* Provider must support prefix listing when `Prefix` is provided.
* Provider must support full container scans when `Prefix` is not provided.
* Continuation token must be validated against the current query before provider execution or inside the provider.
* The normalized query must carry the resolved `Take` value.

## Blob Validation

Create shared validation helpers for:

* `BlobKey`
* `BlobUpload`
* `BlobPropertiesUpdate`
* `BlobQuery`

Validation must return Result failures for expected errors.

Key validation rules:

* `Container` is required.
* `Name` is required.
* `Container` must be provider-safe after normalization.
* `Name` must be provider-safe after normalization.
* Provider-specific restrictions must be reported as typed Result errors.

Upload validation rules:

* `BlobUpload` must not be null.
* `Key` is required.
* `Content` is required.
* `Content` must be readable.
* `OverwriteMode` must be valid.
* `Properties` must be valid for the selected provider.
* `ContentType`, if supplied, must be valid for the selected provider.

Properties update validation rules:

* `BlobPropertiesUpdate` must not be null.
* `Key` is required.
* `Properties` must be valid for the selected provider.
* `ContentType`, if supplied, must be valid for the selected provider.

## Continuation Token Design

The public continuation token is an opaque string.

Internally, it is a base64url-encoded JSON envelope.

```csharp
internal sealed class BlobContinuationToken
{
    public string Provider { get; init; } = default!;

    public int Version { get; init; } = 1;

    public string QueryHash { get; init; } = default!;

    public string? Container { get; init; }

    public string? Name { get; init; }

    public string? NativeToken { get; init; }

    public Dictionary<string, string>? Properties { get; init; }
}
```

## Token Fields

| Field         | Purpose                                                             |
| ------------- | ------------------------------------------------------------------- |
| `Provider`    | Provider discriminator, for example `inmemory`, `ef`, `azure-blob`. |
| `Version`     | Token schema version.                                               |
| `QueryHash`   | Stable hash of the logical query.                                   |
| `Container`   | Container associated with the query.                                |
| `Name`        | Last returned blob name for keyset-based paging.                    |
| `NativeToken` | Provider-native continuation token.                                 |
| `Properties`  | Optional provider-specific metadata.                                |

## Token Requirements

Continuation tokens must follow these rules:

* Application code must never inspect the token.
* Tokens must not contain blob content.
* Tokens must not contain secrets.
* Tokens must be rejected if the provider discriminator does not match.
* Tokens must be rejected if the version is unsupported.
* Tokens must be rejected if the query hash does not match.
* Tokens must be reusable only for the same logical query.
* Tokens may be reused with a different `Take` value for the same logical query.

## Query Hash

The query hash must include:

* container
* prefix
* allow full scan

The query hash must exclude:

* take
* continuation token

This allows callers to request the next page with a different page size while preventing continuation across different logical queries.

## Token Serializer

```csharp
internal interface IBlobContinuationTokenSerializer
{
    Result<string> Serialize(BlobContinuationToken token);

    Result<BlobContinuationToken> Deserialize(string token);
}
```

Invalid tokens must return `Result<BlobContinuationToken>.Failure(...)` with `BlobStoreInvalidContinuationTokenError`.

## Result Error Types

Add typed Result errors.

```csharp
public sealed class BlobStoreValidationError : ResultErrorBase
{
    public BlobStoreValidationError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreNotFoundError : ResultErrorBase
{
    public BlobStoreNotFoundError(BlobKey key)
        : base($"Blob with container '{key.Container}' and name '{key.Name}' was not found.")
    {
        this.Key = key;
    }

    public BlobKey Key { get; }
}
```

```csharp
public sealed class BlobStoreQueryTooBroadError : ResultErrorBase
{
    public BlobStoreQueryTooBroadError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStorePageSizeExceededError : ResultErrorBase
{
    public BlobStorePageSizeExceededError(int take, int maxTake)
        : base($"Requested blob page size {take} exceeds the maximum page size {maxTake}.")
    {
        this.Take = take;
        this.MaxTake = maxTake;
    }

    public int Take { get; }

    public int MaxTake { get; }
}
```

```csharp
public sealed class BlobStoreQueryNotSupportedError : ResultErrorBase
{
    public BlobStoreQueryNotSupportedError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreInvalidContinuationTokenError : ResultErrorBase
{
    public BlobStoreInvalidContinuationTokenError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreConflictError : ResultErrorBase
{
    public BlobStoreConflictError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreLeaseError : ResultErrorBase
{
    public BlobStoreLeaseError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreSerializationError : ResultErrorBase
{
    public BlobStoreSerializationError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreProviderError : ResultErrorBase
{
    public BlobStoreProviderError(string message)
        : base(message)
    {
    }
}
```

Reuse existing devkit errors where already available:

* concurrency errors
* validation errors
* exception errors

Do not introduce duplicate error types if suitable existing ones already exist.

## BlobStoreClient Behavior

`BlobStoreClient` is responsible for shared validation and delegation.

```csharp
public sealed class BlobStoreClient : IBlobStoreClient
{
    private readonly IBlobStoreProvider provider;
    private readonly BlobStoreOptions options;
    private readonly BlobQueryValidator queryValidator;
    private readonly BlobValidator blobValidator;

    public async Task<Result<BlobInfo>> UploadResultAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.blobValidator.Validate(upload, this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<BlobInfo>();
        }

        return await this.provider.UploadResultAsync(upload, cancellationToken);
    }

    public async Task<Result<BlobDownload>> DownloadResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.blobValidator.Validate(key, this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<BlobDownload>();
        }

        return await this.provider.DownloadResultAsync(key, cancellationToken);
    }

    public async Task<Result<BlobInfo>> GetPropertiesResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.blobValidator.Validate(key, this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<BlobInfo>();
        }

        return await this.provider.GetPropertiesResultAsync(key, cancellationToken);
    }

    public async Task<Result<BlobInfo>> UpdatePropertiesResultAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.blobValidator.Validate(update, this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<BlobInfo>();
        }

        return await this.provider.UpdatePropertiesResultAsync(update, cancellationToken);
    }

    public async Task<Result<bool>> ExistsResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.blobValidator.Validate(key, this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<bool>();
        }

        return await this.provider.ExistsResultAsync(key, cancellationToken);
    }

    public async Task<Result<BlobPage>> ListPageResultAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.queryValidator.NormalizeAndValidate(
            query,
            this.options,
            this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For<BlobPage>();
        }

        return await this.provider.ListPageResultAsync(
            validationResult.Value,
            cancellationToken);
    }

    public async Task<Result> DeleteResultAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var validationResult = this.blobValidator.Validate(key, this.provider.Capabilities);

        if (validationResult.IsFailure)
        {
            return validationResult.For();
        }

        return await this.provider.DeleteResultAsync(key, cancellationToken);
    }
}
```

## Provider Result Rules

Each provider method must follow these rules:

* Return `Result.Success(...)` for successful operations.
* Return `Result.Failure(...)` for expected failures.
* Catch provider-specific exceptions and map them to typed Result errors.
* Do not catch cancellation exceptions caused by caller cancellation unless the devkit has a global convention for that.
* Do not throw for invalid query shape.
* Do not throw for unsupported provider semantics.
* Do not throw for missing blob on `DownloadResultAsync` or `GetPropertiesResultAsync`.
* Do not throw for idempotent missing delete.
* Do not throw for invalid continuation tokens.
* Do not return partially successful pages.
* Do not download content for `ListPageResultAsync`.
* Do not download content for `GetPropertiesResultAsync`.
* Do not download content for `ExistsResultAsync`.
* Do not download content for `UpdatePropertiesResultAsync`.

## Lease Model

Leases are provider-internal coordination mechanisms.

There is no public lease API in v1.

Lease requirements:

* EF Core provider must support internal leases.
* EF Core upload must acquire a write lease.
* EF Core delete must acquire a write lease.
* EF Core property updates must acquire a write lease.
* EF Core lease expiration must allow recovery from crashed writers.
* Reads do not normally require leases.
* Azure Blob provider may use native blob leases where appropriate.
* InMemory provider may implement a lightweight in-process lock or no-op lease depending on test requirements.

## Entity Framework Storage Model

EF Core is a first-class provider.

The EF provider stores blobs in chunks to support stream-first behavior and avoid requiring full content materialization for all operations.

## EF Core Tables

Recommended tables/entities:

```text
StorageBlob
StorageBlobChunk
```

Optional separate lease table is allowed, but lease fields on `StorageBlob` are preferred for simplicity.

## StorageBlob

Suggested fields:

```csharp
public sealed class StorageBlob
{
    public string Id { get; set; }

    public string Container { get; set; }

    public string Name { get; set; }

    public string ContainerHash { get; set; }

    public string NameHash { get; set; }

    public long Length { get; set; }

    public string? ContentType { get; set; }

    public string? ContentHash { get; set; }

    public string? ETag { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    public string? PropertiesJson { get; set; }

    public string? LeaseId { get; set; }

    public string? LeaseAcquiredBy { get; set; }

    public DateTimeOffset? LeaseAcquiredUntil { get; set; }
}
```

## StorageBlobChunk

Suggested fields:

```csharp
public sealed class StorageBlobChunk
{
    public string BlobId { get; set; }

    public int Index { get; set; }

    public byte[] Content { get; set; }

    public int Length { get; set; }
}
```

## EF Core Indexes

Recommended indexes:

```text
StorageBlob(ContainerHash, NameHash) unique
StorageBlob(Container, Name)
StorageBlob(Container, Name) for prefix listing if supported by provider/database
StorageBlobChunk(BlobId, Index) unique
StorageBlob(LeaseAcquiredUntil)
```

The exact index shape can follow existing Document Storage conventions where applicable.

## EF Core Upload Strategy

Upload must be transactional.

Recommended flow:

* Validate key, stream, properties, and overwrite mode.
* Begin transaction.
* Acquire internal lease on target blob row or create placeholder row with lease.
* If `OverwriteMode.FailIfExists` and blob exists, return conflict failure.
* Replace metadata.
* Delete existing chunks for overwrite.
* Read upload stream in configured chunk sizes.
* Insert chunks with sequential indexes.
* Update length, hash, ETag, timestamps, properties.
* Commit transaction.
* Release lease.
* Return `Result<BlobInfo>.Success(info)`.

Failure flow:

* Roll back transaction.
* Release or expire lease where appropriate.
* Return typed Result failure.

## EF Core Download Strategy

Download should stream chunks in order.

Recommended behavior:

* Query blob metadata by exact key.
* Return not-found failure when missing.
* Create a readable stream implementation that reads `StorageBlobChunk` rows ordered by index.
* Do not load all chunks into memory at once.
* Return `BlobDownload` with stream and `BlobInfo`.

Implementation options:

* Custom `Stream` implementation backed by chunk queries.
* Temporary file/pipe is not preferred.
* Memory stream is only acceptable for small test/in-memory scenarios, not primary EF behavior.

## EF Core List Strategy

Listing must not load chunk content.

For prefix listing:

* Query `StorageBlob` rows by container and name prefix.
* Order by `Name`.
* Apply keyset paging based on last returned name.
* Project only metadata fields required for `BlobInfo`.
* Return `BlobPage`.

For full container scan:

* Require full scan approval.
* Query all `StorageBlob` rows for the container.
* Order by `Name`.
* Apply keyset paging.
* Project metadata only.

## EF Core Properties Strategy

`GetPropertiesResultAsync`:

* Query only `StorageBlob`.
* Do not query chunks.
* Return `BlobInfo`.

`UpdatePropertiesResultAsync`:

* Begin transaction.
* Acquire write lease.
* Validate ETag if supplied.
* Update content type and property bag.
* Update ETag and last modified timestamp.
* Commit and release lease.
* Return updated `BlobInfo`.

## EF Core Exists Strategy

`ExistsResultAsync`:

* Query existence by key.
* Do not load chunks.
* Return `Result<bool>.Success(...)`.

## EF Core Delete Strategy

Delete is idempotent.

Recommended flow:

* Begin transaction.
* If blob does not exist, return success.
* Acquire lease.
* Delete chunks.
* Delete blob row.
* Commit transaction.
* Return success.

## EF Core Token Strategy

Use keyset token with last blob name.

```json
{
  "Provider": "ef",
  "Version": 1,
  "QueryHash": "...",
  "Container": "reports",
  "Name": "2026/06/report.pdf"
}
```

## Azure Blob Provider

Azure Blob Storage is the cloud/object storage provider.

## Azure Blob Mapping

| Blob Storage concept         | Azure Blob concept                             |
| ---------------------------- | ---------------------------------------------- |
| `BlobKey.Container`          | Container name                                 |
| `BlobKey.Name`               | Blob name                                      |
| `BlobInfo.ContentType`       | HTTP content type                              |
| `BlobInfo.ETag`              | Blob ETag                                      |
| `BlobInfo.LastModifiedAt`    | Blob last modified timestamp                   |
| `BlobInfo.Properties`        | Blob metadata, converted through `PropertyBag` |
| `BlobPage.ContinuationToken` | Azure blob listing continuation token          |

## Azure Blob Upload Strategy

Upload must:

* Validate key.
* Validate property bag conversion to metadata.
* Use Azure Blob upload streaming APIs.
* Respect `OverwriteMode`.
* Set content type when provided.
* Set metadata from `PropertyBag`.
* Return `BlobInfo`.

For `OverwriteMode.FailIfExists`, use native conditional upload behavior where possible.

## Azure Blob Download Strategy

Download must:

* Use native download/open-read stream APIs.
* Return not-found Result failure when missing.
* Return `BlobDownload`.
* Caller disposes the returned stream/download.

## Azure Blob List Strategy

List must:

* Target one container.
* Use prefix listing when `Prefix` is supplied.
* Use full listing only when full scan is approved.
* Use Azure continuation tokens.
* Return `BlobInfo` items.
* Not download content.

## Azure Blob Properties Strategy

`GetPropertiesResultAsync`:

* Use native properties/head call.
* Map HTTP headers and metadata to `BlobInfo`.
* Do not download content.

`UpdatePropertiesResultAsync`:

* Validate metadata conversion.
* Use native set properties/metadata APIs.
* Respect `IfMatchETag` where supported.
* Return updated `BlobInfo`.

## Azure Blob Exists Strategy

`ExistsResultAsync`:

* Use native existence call.
* Return `Result<bool>.Success(...)`.
* Do not download content.

## Azure Blob Delete Strategy

Delete is idempotent.

* Missing blob returns success.
* Provider request failures return typed Result failure.

## Azure Blob Token Strategy

Use native token.

```json
{
  "Provider": "azure-blob",
  "Version": 1,
  "QueryHash": "...",
  "Container": "reports",
  "NativeToken": "<azure-blob-continuation-token>"
}
```

## InMemory Provider

The InMemory provider supports tests, demos, and lightweight runtime usage.

## InMemory Storage Model

The provider may store:

```csharp
private sealed class InMemoryBlob
{
    public BlobKey Key { get; init; }

    public byte[] Content { get; set; }

    public BlobInfo Info { get; set; }
}
```

Rules:

* Clone uploaded content into memory.
* Return new readable `MemoryStream` instances on download.
* Do not expose mutable internal buffers.
* Store and return `PropertyBag`.
* Support prefix listing.
* Support full container scans when allowed.
* Support deterministic ordering by `Name`.
* Support keyset continuation tokens.
* Delete is idempotent.

## InMemory Token Strategy

```json
{
  "Provider": "inmemory",
  "Version": 1,
  "QueryHash": "...",
  "Container": "reports",
  "Name": "2026/06/report.pdf"
}
```

## Upload Semantics

`UploadResultAsync` uploads or replaces content depending on `OverwriteMode`.

Rules:

* Upload returns `Result<BlobInfo>`.
* Upload must not dispose the input stream.
* Upload must return conflict failure when `OverwriteMode.FailIfExists` and the blob exists.
* Upload must update `Length`.
* Upload should update `ContentHash` if supported/configured.
* Upload must update `ETag` if supported.
* Upload must update `LastModifiedAt`.
* Upload must preserve or replace properties according to the supplied upload request.
* Upload must be atomic from the caller perspective where provider support allows it.

## Download Semantics

`DownloadResultAsync` retrieves blob content and properties.

Rules:

* Download returns `Result<BlobDownload>`.
* Missing blob returns `BlobStoreNotFoundError`.
* Caller owns the returned stream.
* Provider should stream content where possible.
* Range download is not supported in v1.
* Download should include `BlobInfo`.

## Get Properties Semantics

`GetPropertiesResultAsync` returns blob properties without content.

Rules:

* Missing blob returns `BlobStoreNotFoundError`.
* Content must not be downloaded.
* Result contains `BlobInfo`.
* Custom properties are returned through `PropertyBag`.

## Update Properties Semantics

`UpdatePropertiesResultAsync` updates properties without uploading content.

Rules:

* Missing blob returns `BlobStoreNotFoundError`.
* Content must not be downloaded.
* Blob content must not be rewritten unless unavoidable.
* `ContentType` may be changed.
* Custom `Properties` may be changed.
* `IfMatchETag` should be honored where supported.
* ETag and last modified timestamp should be updated when supported.
* Result contains updated `BlobInfo`.

## Exists Semantics

`ExistsResultAsync` checks for exact-key existence.

Rules:

* Existing blob returns `Result<bool>.Success(true)`.
* Missing blob returns `Result<bool>.Success(false)`.
* Provider failure returns `Result<bool>.Failure(...)`.
* Content must not be downloaded.
* Properties do not need to be loaded unless provider requires it for existence checks.

## List Semantics

`ListPageResultAsync` returns a page of `BlobInfo`.

Rules:

* Query must target one container.
* Query may specify a prefix.
* Query without prefix is a full container scan.
* Full container scans require approval.
* Listing must not download content.
* Listing must be deterministic.
* Listing must return no more than `Take` items.
* Listing must return a continuation token when more results exist.
* Continuation token must be query-bound.

## Delete Semantics

`DeleteResultAsync` deletes a blob by exact key.

Rules:

* Delete is idempotent.
* Missing blob returns `Result.Success()`.
* Existing blob is deleted and returns `Result.Success()`.
* Content chunks/properties must be removed.
* Provider failures return typed Result failures.
* EF Core provider must use a lease for delete.

## Caching Behavior

Caching is optional.

Default recommendation:

* Do not cache downloads.
* Do not cache streams.
* Do not cache failures.
* Properties may be cached later if explicitly configured.
* List pages may be cached later if explicitly configured.

If caching is implemented, cache keys must include:

* operation name
* container
* name
* prefix
* take
* continuation token
* allow full scan
* provider identity

Writes must invalidate affected entries:

* upload invalidates exact blob, container listing, and prefix listing entries
* properties update invalidates exact blob and list entries
* delete invalidates exact blob and list entries

## Logging and Observability

Add structured logging for all operations.

Log fields:

* provider name
* operation name
* container
* blob name when applicable
* prefix when applicable
* take when applicable
* has continuation token
* allow full scan
* result success/failure
* error types when failed
* length when known
* content type when known
* elapsed time

Do not log:

* blob content
* raw continuation token values
* full property values by default
* secrets stored accidentally in properties

Example:

```csharp
logger.LogDebug(
    "BlobStore {Operation} completed using {Provider}. Success={Success}, Container={Container}, Name={Name}, Length={Length}, ContentType={ContentType}",
    operation,
    providerName,
    result.IsSuccess,
    key.Container,
    key.Name,
    result.IsSuccess ? result.Value.Length : null,
    result.IsSuccess ? result.Value.ContentType : null);
```

## Testing Requirements

## Shared Contract Tests

Create provider-neutral contract tests that run against all providers.

Required tests:

* Upload returns success for valid stream.
* Upload returns `BlobInfo`.
* Upload with `FailIfExists` returns conflict failure when blob exists.
* Download returns content equal to uploaded content.
* Download returns `BlobInfo`.
* Caller can dispose returned download.
* Download missing blob returns `BlobStoreNotFoundError`.
* Get properties returns info without downloading content.
* Update properties changes content type and property bag.
* Update properties missing blob returns not-found failure.
* Exists returns true for existing blob.
* Exists returns false for missing blob.
* Delete existing blob returns success.
* Delete missing blob returns success.
* List by prefix returns matching blobs only.
* List returns no content streams.
* List returns no more than `Take` items.
* List can continue using continuation token.
* Full container scan without approval fails.
* Full container scan with approval succeeds only when globally enabled.
* Continuation token cannot be reused with a different query.
* `Take = 0` fails.
* `Take < 0` fails.
* `Take > MaxTake` fails.
* Expected failures are Result failures, not thrown exceptions.
* Cancellation token is passed to provider operations.

## Builder Tests

Required tests:

* `BlobQueries.Query().InContainer(...).WithPrefix(...).Take(...).Build()` creates a prefix query.
* `BlobQueries.Query().InContainer(...).AllowFullScan().Take(...).Build()` creates a full-scan query.
* `ContinueWith(...)` sets continuation token.
* `Take(0)` throws.
* `Take(-1)` throws.
* `InContainer(null)` throws.
* `InContainer("")` throws.
* `ContinueWith(null)` throws.
* Builder-created queries pass through the same validator as manually-created queries.
* Builder does not bypass full scan approval.

## EF Core Provider Tests

Additional tests:

* Upload stores content in chunks.
* Download streams chunks in order.
* Upload does not require loading entire content into memory.
* List does not query chunk content.
* Get properties does not query chunk content.
* Exists does not query chunk content.
* Delete removes blob and chunks.
* Delete missing blob succeeds.
* Upload overwrite replaces old chunks.
* Upload `FailIfExists` returns conflict.
* Property update does not rewrite chunks.
* ETag changes after upload.
* ETag changes after properties update.
* `IfMatchETag` mismatch returns concurrency failure.
* Write lease is acquired for upload.
* Write lease is acquired for delete.
* Write lease is acquired for property update.
* Expired lease can be recovered.
* Keyset paging works.
* Full container scan requires approval.

## Azure Blob Provider Tests

Additional tests:

* Upload uses native blob upload.
* Download returns native readable stream.
* Metadata maps to/from `PropertyBag`.
* Content type maps correctly.
* ETag maps correctly.
* List uses native continuation token.
* Prefix list uses native prefix.
* Full scan requires approval.
* Delete missing blob succeeds.
* `FailIfExists` maps to native conditional upload.
* `IfMatchETag` maps to native conditional update where supported.
* Provider request failures become Result failures.

## InMemory Provider Tests

Additional tests:

* Upload clones content.
* Download returns new stream instance.
* Internal content cannot be mutated through returned stream.
* Properties are preserved.
* Prefix listing works.
* Full scan requires approval.
* Keyset continuation works.
* Delete is idempotent.

## Implementation Order

Recommended implementation order:

* Add `BlobKey`.
* Add `BlobInfo`.
* Add `BlobDownload`.
* Add `BlobUpload`.
* Add `BlobOverwriteMode`.
* Add `BlobPropertiesUpdate`.
* Add `BlobQuery`.
* Add `BlobPage`.
* Add `BlobQueries`.
* Add `BlobQueryBuilder`.
* Add `BlobStoreOptions`.
* Add `BlobStoreProviderCapabilities`.
* Add typed Result error types.
* Add continuation token model and serializer.
* Add validators.
* Add `IBlobStoreClient`.
* Add `IBlobStoreProvider`.
* Implement `BlobStoreClient`.
* Implement InMemory provider.
* Implement EF Core entities.
* Implement EF Core provider chunked upload/download.
* Implement EF Core lease handling.
* Implement EF Core listing/properties/exists/delete.
* Implement Azure Blob provider.
* Add shared contract tests.
* Add builder tests.
* Add provider-specific tests.
* Update documentation and examples.

## Acceptance Criteria

The implementation is complete when:

* Blob Storage is a separate feature from Document Storage.
* The public client API is Result-native.
* The public provider API is Result-native.
* `BlobKey(Container, Name)` is used for addressing.
* `Container` is required for listing.
* Upload is stream-first.
* Download is stream-first.
* Caller owns and disposes returned download streams.
* `BlobInfo` contains standard fields and custom `PropertyBag` properties.
* Properties can be read without downloading content.
* Properties can be updated without uploading content.
* Listing is continuation-token based.
* Listing returns `BlobInfo` only and never content.
* Full container scans require explicit global and query-level approval.
* Delete is idempotent.
* EF Core provider stores content in chunks.
* EF Core provider uses internal leases for upload, delete, and property update.
* Azure Blob provider uses native blob semantics and native continuation tokens.
* InMemory provider supports the same logical behavior.
* Azure Table provider is not implemented.
* Cosmos provider is not implemented.
* FileSystem provider is not implemented.
* Range downloads are not implemented.
* Shared contract tests pass.
* Provider-specific tests pass.

## Example Usage

## Upload

```csharp
await using var content = File.OpenRead("report.pdf");

var uploadResult = await blobs.UploadResultAsync(
    new BlobUpload
    {
        Key = new BlobKey("reports", "2026/06/report.pdf"),
        Content = content,
        ContentType = "application/pdf",
        Properties = new PropertyBag()
            .Set("customerId", "42")
            .Set("source", "monthly-export"),
        OverwriteMode = BlobOverwriteMode.Overwrite
    },
    cancellationToken);
```

## Download

```csharp
var downloadResult = await blobs.DownloadResultAsync(
    new BlobKey("reports", "2026/06/report.pdf"),
    cancellationToken);

if (downloadResult.IsSuccess)
{
    await using var download = downloadResult.Value;
    await download.Content.CopyToAsync(targetStream, cancellationToken);
}
```

## Get Properties

```csharp
var propertiesResult = await blobs.GetPropertiesResultAsync(
    new BlobKey("reports", "2026/06/report.pdf"),
    cancellationToken);

if (propertiesResult.IsSuccess)
{
    var info = propertiesResult.Value;
    var customerId = info.Properties.Get<string>("customerId");
}
```

## Update Properties

```csharp
var updateResult = await blobs.UpdatePropertiesResultAsync(
    new BlobPropertiesUpdate
    {
        Key = new BlobKey("reports", "2026/06/report.pdf"),
        ContentType = "application/pdf",
        Properties = new PropertyBag()
            .Set("customerId", "42")
            .Set("reviewed", true),
        IfMatchETag = propertiesResult.Value.ETag
    },
    cancellationToken);
```

## Exists

```csharp
var existsResult = await blobs.ExistsResultAsync(
    new BlobKey("reports", "2026/06/report.pdf"),
    cancellationToken);

if (existsResult.IsSuccess && existsResult.Value)
{
    // blob exists
}
```

## List by Prefix

```csharp
var pageResult = await blobs.ListPageResultAsync(
    BlobQueries
        .Query()
        .InContainer("reports")
        .WithPrefix("2026/06/")
        .Take(100)
        .Build(),
    cancellationToken);

if (pageResult.IsSuccess)
{
    foreach (var blob in pageResult.Value.Items)
    {
        // blob is BlobInfo, no content downloaded
    }
}
```

## Continue Listing

```csharp
var nextPageResult = await blobs.ListPageResultAsync(
    BlobQueries
        .Query()
        .InContainer("reports")
        .WithPrefix("2026/06/")
        .Take(100)
        .ContinueWith(pageResult.Value.ContinuationToken)
        .Build(),
    cancellationToken);
```

## Explicit Full Container Scan

```csharp
var pageResult = await blobs.ListPageResultAsync(
    BlobQueries
        .Query()
        .InContainer("reports")
        .AllowFullScan()
        .Take(100)
        .Build(),
    cancellationToken);
```

This only works when `BlobStoreOptions.AllowFullScans` is also enabled.

## Delete

```csharp
var deleteResult = await blobs.DeleteResultAsync(
    new BlobKey("reports", "2026/06/report.pdf"),
    cancellationToken);
```

Deleting a missing blob still returns success.

## Agent Implementation Notes

The implementation agent must avoid quick fixes such as:

* implementing listing with offset paging
* returning all blobs and slicing in memory
* downloading content for list operations
* downloading content for property operations
* downloading content for existence checks
* storing EF Core blob content inline only
* ignoring EF Core leases
* exposing public lease APIs in v1
* implementing range download
* implementing Azure Table provider
* implementing Cosmos provider
* implementing FileSystem provider
* exposing raw provider continuation tokens
* throwing for expected validation failures
* returning `Result<T>.Success(null)`
* disposing caller-provided upload streams
* hiding full container scans behind a convenience method

The implementation agent should prefer shared helper methods for:

* key validation
* query validation
* property conversion
* property bag serialization
* continuation token serialization
* query hash calculation
* page creation
* lease acquisition/release
* provider exception mapping
* Result error creation

Provider-specific code should stay provider-specific only where it deals with actual storage mechanics.
