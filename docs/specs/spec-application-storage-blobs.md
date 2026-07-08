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

The feature explicitly excludes these providers:

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
* Use `ContentType` and `ContentTypeExtensions` from `src/Common.Utilities/ContentTypes` for all provider-neutral content-type values and MIME/extension conversion.
* Support continuation-token based listing.
* Require explicit opt-in for full container scans.
* Support idempotent delete.
* Support property updates without re-uploading content.
* Support fluent `AddBlobStorage(...)` registration with named clients, provider-specific client registration, and composable client behaviors.
* Support resolving configured clients by name through a client factory/catalog.
* Register one aggregate ASP.NET Core health check for all configured blob clients.
* Support configurable maximum blob size per registered client.
* Define a provider-neutral SHA-256 content hash format and calculation rule.
* Provide telemetry metrics for blob operations.
* Provide retry and timeout client behaviors.
* Support EF Core as a first-class durable provider.
* Store EF Core blobs in chunks.
* Require EF Core blob contexts to expose the blob storage tables through a context contract.
* Support internal provider leases for write/delete consistency.
* Do not support range downloads.
* Avoid provider-specific concepts in the public client API.

## Non-Goals

This feature does not introduce:

* Typed document/entity storage.
* JSON serialization of typed entities as the primary model.
* Cosmos DB provider.
* Azure Table Storage provider.
* FileSystem provider.
* Range downloads.
* Resumable uploads.
* Resumable downloads.
* Public staged, multipart, or block upload APIs.
* Authentication or authorization policy enforcement.
* Encryption policy management.
* Multi-tenant isolation or tenant-aware routing.
* Backup, restore, or provider disaster-recovery orchestration.
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
| ContentType        | Shared `ContentType` enum from `src/Common.Utilities/ContentTypes`.                             |
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
* Content type values use the shared `ContentType` enum; providers convert to or from MIME strings with `ContentTypeExtensions`.
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

## Fluent Registration

Blob Storage registration should follow the finalized storage-builder style used by other storage features.

Example:

```csharp
services.AddBlobStorage(o => o.Enabled(true))
    .WithBehavior<LoggingBlobStoreClientBehavior>()
    .WithBehavior<MetricsBlobStoreClientBehavior>()
    .WithBehavior<RetryBlobStoreClientBehavior>()
    .WithBehavior<TimeoutBlobStoreClientBehavior>()
    .WithEntityFrameworkClient<AppDbContext>("reports", o => o
        .MaxBlobSize(50 * 1024 * 1024))
    .WithAzureBlobClient("media", o => o
        .Container("media")
        .MaxBlobSize(500 * 1024 * 1024));
```

Rules:

* `AddBlobStorage(...)` registers the feature-level options and returns a fluent builder.
* Provider registration methods register named clients.
* Each blob client/store name must be unique.
* Duplicate client names must fail during registration or service-provider validation with a clear error.
* The visible client identity is the configured store/client name, not a display value with the provider appended.
* Provider-specific setup remains behind fluent provider methods such as `WithEntityFrameworkClient(...)`, `WithAzureBlobClient(...)`, and `WithInMemoryClient(...)`.
* Provider-specific setup must allow configuring per-client `BlobStoreOptions`, including `MaxBlobSize`.
* Client behaviors can be registered before or after provider clients and compose into each registered client.
* Registration must not require consumers to resolve or use raw providers directly.

## Blob Client Factory

Runtime code should resolve configured blob clients by store/client name.

```csharp
public interface IBlobStoreClientFactory
{
    IBlobStoreClient CreateClient(string name);

    IReadOnlyCollection<BlobStoreClientRegistration> GetRegistrations();
}

public sealed class BlobStoreClientRegistration
{
    public string Name { get; init; }

    public string ProviderName { get; init; }

    public BlobStoreProviderCapabilities Capabilities { get; init; }
}
```

Rules:

* The factory resolves the proper configured `IBlobStoreClient` instance for the selected name.
* Unknown names must fail with a clear typed error or throw a deterministic configuration exception, depending on whether the operation is runtime Result-based or startup validation.
* `GetRegistrations()` returns enough metadata for diagnostics and operational tooling without exposing provider instances.
* The factory and registration catalog are infrastructure for app/runtime code and operational tooling.

## Health Checks

Blob Storage must register one aggregate ASP.NET Core health check for all configured clients.

```text
BlobStorage
```

Rules:

* The health check runs only when Blob Storage is enabled and at least one client is registered.
* The check probes every registered client.
* The probe must be non-mutating.
* Recommended probe: `ExistsResultAsync(new BlobKey("__bdk", "healthcheck/probe"))`.
* A missing probe blob is healthy.
* Provider failures make the aggregate health check unhealthy.
* The health-check description and data must identify the failed client names clearly.
* Arrays in health-check data must be rendered as joined strings or another readable scalar representation, not as `System.String[]`.
* The health check should use tags `ready`, `storage`, and `blobs`.

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

    public ContentType? ContentType { get; init; }

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
* `ContentType` is optional and uses the shared `ContentType` enum.
* `ContentHash` uses the provider-neutral SHA-256 format defined in this specification when present.
* `ETag` is optional and provider-dependent.
* `CreatedAt` is optional and provider-dependent.
* `LastModifiedAt` is optional and provider-dependent.
* `Properties` contains custom user/application properties.
* `Properties` must not contain the blob content.
* `Properties` should be safe to return from list operations.

## Content Type Handling

Blob Storage must use the shared content-type utilities in `src/Common.Utilities/ContentTypes`.

Rules:

* Provider-neutral models expose `ContentType?`, not raw MIME strings.
* `BlobInfo.ContentType`, `BlobUpload.ContentType`, and `BlobPropertiesUpdate.ContentType` use `ContentType?`.
* MIME string conversion uses `ContentTypeExtensions.MimeType()`.
* MIME string parsing uses `ContentTypeExtensions.FromMimeType(...)`.
* Extension and file-name based conversion uses `ContentTypeExtensions.FromExtension(...)` and `ContentTypeExtensions.FromFileName(...)`.
* Provider implementations must not maintain a separate blob-storage MIME mapping table.
* Unknown or unsupported provider MIME values must either map through `ContentTypeExtensions.FromMimeType(...)` with an explicit provider default or return a typed Result failure when lossless mapping is required by the operation.
* Automatic content-type inference remains optional. If inference is offered, it must be explicit and based on `ContentTypeExtensions.FromFileName(...)` or `ContentTypeExtensions.FromExtension(...)`.
* Native providers such as Azure Blob Storage persist the MIME string returned by `ContentType.MimeType()`.
* EF Core and InMemory providers may persist the enum value or the MIME string, but returned public models must use `ContentType?`.

## Content Hashing

Blob Storage uses SHA-256 as the provider-neutral content hash algorithm.

Hash format:

```text
sha256:<lowercase-64-character-hex>
```

Rules:

* The hash is calculated over the exact uploaded content bytes in stream order.
* The hash excludes blob key fields, content type, custom properties, timestamps, ETags, leases, provider metadata, and storage chunk boundaries.
* Providers and behaviors must calculate the hash incrementally while reading the upload stream.
* Implementations must not buffer the full blob solely to calculate the hash.
* Hash calculation uses `System.Security.Cryptography.SHA256`.
* Hex output must be lowercase invariant hexadecimal.
* Native provider hashes, ETags, and Azure Content-MD5 values are not substitutes for `BlobInfo.ContentHash`.
* Azure Blob Storage should persist the devkit hash as metadata, for example `bdk-content-sha256`, unless a stronger provider-native SHA-256 property is available.
* EF Core and InMemory providers must store and return the calculated hash.
* `BlobInfo.ContentHash` should be returned by upload, download, get properties, update properties, and list operations when the provider has the value.
* Download operations return the stored upload hash in `BlobInfo.ContentHash`; automatic download stream verification is not required.

When an upload supplies an expected hash, the expected value must use the same `sha256:<lowercase-64-character-hex>` format. The calculated hash must match the expected hash exactly, otherwise the upload must fail with a typed integrity Result failure and must not commit partial content.

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

    public ContentType? ContentType { get; init; }

    public string? ExpectedContentHash { get; init; }

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
* `ContentType` is optional and uses `ContentType?`.
* `ExpectedContentHash` is optional and must use `sha256:<lowercase-64-character-hex>` when supplied.
* When `ExpectedContentHash` is supplied, upload must fail if the calculated SHA-256 content hash differs.
* `Properties` is optional and defaults to an empty `PropertyBag`.
* `OverwriteMode.Overwrite` creates or replaces the blob.
* `OverwriteMode.FailIfExists` fails if the blob already exists.
* Upload returns `Result<BlobInfo>`.
* Upload must not close or dispose the input stream.
* The caller remains responsible for disposing the upload stream.
* Upload should stream content to the provider where possible.
* Upload is a single operation; resumable upload is not supported.

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
* Range downloads are not supported.

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

    public ContentType? ContentType { get; init; }

    public PropertyBag Properties { get; init; } = new();

    public string? IfMatchETag { get; init; }
}
```

Rules:

* `Key` is required.
* `ContentType` may be updated without uploading content.
* `Properties` replaces the custom property bag. Merge mode is not part of this feature.
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
* Suffix filtering is not supported.
* Arbitrary property filtering is not supported.

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

Feature-level options:

```csharp
public sealed class BlobStorageOptions
{
    public bool Enabled { get; set; } = true;
}
```

Client/provider options:

```csharp
public sealed class BlobStoreOptions
{
    public int DefaultTake { get; set; } = 100;

    public int MaxTake { get; set; } = 1000;

    public long? MaxBlobSize { get; set; }

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
| `MaxBlobSize`                     |     `null` | Optional maximum upload size in bytes for this client. |
| `AllowFullScans`                  |    `false` | Disallows full container scans by default.      |
| `RequireExplicitFullScanApproval` |     `true` | Requires `AllowFullScan = true` per query.      |
| `ChunkSize`                       |     `4 MB` | EF Core blob chunk size.                        |
| `LeaseDuration`                   | `1 minute` | EF Core internal lease duration.                |
| `LeaseOwner`                      |     `null` | Optional logical owner used for EF Core leases. |

`MaxBlobSize` is evaluated per registered client. A null value means the devkit abstraction does not impose an additional size limit, but native provider limits still apply.

## Blob Size Limits

Maximum blob size enforcement must happen before provider commit.

Rules:

* `BlobStoreOptions.MaxBlobSize` is optional and configured per named client.
* If `MaxBlobSize` is null, no abstraction-level maximum is enforced.
* If the upload stream can report length before reading and that length exceeds `MaxBlobSize`, upload must fail before provider write work starts.
* If the upload stream length is unknown, upload must count bytes while streaming and fail as soon as the count exceeds `MaxBlobSize`.
* Providers must not commit partial blob content after a size-limit failure.
* Size-limit failures must return `BlobStoreSizeLimitExceededError`.
* The size check applies to create and overwrite uploads.
* The size check does not apply to property updates.

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

| Provider         | Paging | Prefix listing | Full scan | Properties | ETag     | Content hash | Leases              | Streaming      |
| ---------------- | ------ | -------------- | --------- | ---------- | -------- | ------------ | ------------------- | -------------- |
| InMemory         | Yes    | Yes            | Yes       | Yes        | Optional | Yes          | Internal/no-op      | Memory stream  |
| Entity Framework | Yes    | Yes            | Yes       | Yes        | Yes      | Yes          | Internal EF lease   | Chunked stream |
| Azure Blob       | Yes    | Yes            | Yes       | Yes        | Yes      | Yes          | Native where needed | Native stream  |

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
* `ContentType`, if supplied, must map to a provider MIME value through `ContentTypeExtensions.MimeType()`.
* `ExpectedContentHash`, if supplied, must use the configured SHA-256 content hash format.
* `MaxBlobSize`, if configured and checkable before upload, must not be exceeded.

Properties update validation rules:

* `BlobPropertiesUpdate` must not be null.
* `Key` is required.
* `Properties` must be valid for the selected provider.
* `ContentType`, if supplied, must map to a provider MIME value through `ContentTypeExtensions.MimeType()`.

## Continuation Token Design

The public continuation token is an opaque string.

Internally, it is a base64url-encoded JSON envelope.

```csharp
internal sealed class BlobContinuationToken
{
    public string Provider { get; init; } = default!;

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

```csharp
public sealed class BlobStoreSizeLimitExceededError : ResultErrorBase
{
    public BlobStoreSizeLimitExceededError(long actualSize, long maxSize)
        : base($"Blob size {actualSize} bytes exceeds the configured maximum blob size {maxSize} bytes.")
    {
        this.ActualSize = actualSize;
        this.MaxSize = maxSize;
    }

    public long ActualSize { get; }

    public long MaxSize { get; }
}
```

```csharp
public sealed class BlobStoreIntegrityError : ResultErrorBase
{
    public BlobStoreIntegrityError(string message)
        : base(message)
    {
    }
}
```

```csharp
public sealed class BlobStoreTimeoutError : ResultErrorBase
{
    public BlobStoreTimeoutError(string operation, TimeSpan timeout)
        : base($"Blob storage operation '{operation}' timed out after {timeout}.")
    {
        this.Operation = operation;
        this.Timeout = timeout;
    }

    public string Operation { get; }

    public TimeSpan Timeout { get; }
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
        var validationResult = this.blobValidator.Validate(
            upload,
            this.options,
            this.provider.Capabilities);

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

## Client Behaviors

Blob client behaviors are decorators around `IBlobStoreClient`.

Built-in behavior scope:

* `LoggingBlobStoreClientBehavior`
* `MetricsBlobStoreClientBehavior`
* `RetryBlobStoreClientBehavior`
* `TimeoutBlobStoreClientBehavior`

Optional behavior outside the required scope:

* cache for properties or list metadata

Recommended retry behavior options:

```csharp
public sealed class RetryBlobStoreClientBehaviorOptions
{
    public int MaxAttempts { get; set; } = 3;

    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);
}
```

Recommended timeout behavior options:

```csharp
public sealed class TimeoutBlobStoreClientBehaviorOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
```

Rules:

* Behaviors are registered through `AddBlobStorage(...)`.
* Registration order defines wrapping; the first registered behavior is the outermost wrapper.
* Behaviors must preserve Result-native semantics.
* Behaviors must not dispose caller-provided upload streams.
* Behaviors must not buffer download streams unless explicitly documented.
* Logging behavior must not log blob content, raw continuation token values, or full property values.
* Metrics behavior must avoid high-cardinality labels such as full blob names and raw continuation tokens.
* Retry behavior must retry transient provider failures only.
* Retry behavior must not retry validation, not-found, conflict, concurrency, size-limit, integrity, unsupported-query, or caller-cancellation failures.
* Retry behavior may retry uploads only when the content stream is seekable and can be rewound to its original position, or when the provider operation is known to be safely replayable.
* Retry behavior must not leave duplicate or partial content after a retry.
* Timeout behavior must use a linked cancellation token and must distinguish timeout from caller cancellation where Result conventions allow it.
* Timeout behavior should return `BlobStoreTimeoutError` for operation timeouts.
* Behavior construction must support named clients and must not collapse multiple client registrations into a single shared provider unintentionally.

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

There is no public lease API.

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

## Entity Framework Context Contract

Applications using the EF Core blob provider must expose the blob storage tables through a context contract.

```csharp
public interface IBlobStoreContext
{
    DbSet<StorageBlob> StorageBlobs { get; }

    DbSet<StorageBlobChunk> StorageBlobChunks { get; }
}
```

Rules:

* `WithEntityFrameworkClient<TContext>(...)` requires `TContext` to implement `IBlobStoreContext`.
* The EF registration must resolve a fresh scoped `DbContext` per operation when the blob client itself is singleton-safe.
* The library does not ship consuming-application migrations.
* The owning application is responsible for adding the blob storage tables and indexes to its schema.

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

    public string? ContentTypeMimeType { get; set; }

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

`ContentTypeMimeType` stores the MIME string produced by `ContentType.MimeType()`. EF mapping back to public models uses `ContentTypeExtensions.FromMimeType(...)` with an explicit default or a typed Result failure when an unknown MIME value must not be lossy.

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
* Count bytes while reading and enforce `MaxBlobSize` when configured.
* Calculate SHA-256 while reading the stream.
* Insert chunks with sequential indexes.
* Verify `ExpectedContentHash` when supplied.
* Update length, content hash, ETag, timestamps, properties.
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
| `BlobInfo.ContentType`       | HTTP content type from `ContentType.MimeType()` |
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
* Set content type when provided, using `ContentType.MimeType()`.
* Set metadata from `PropertyBag`.
* Calculate SHA-256 while reading the upload stream.
* Enforce `MaxBlobSize` when configured.
* Verify `ExpectedContentHash` when supplied.
* Persist the devkit content hash in metadata.
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
* Enforce `MaxBlobSize` before storing content.
* Calculate and store the SHA-256 content hash.
* Verify `ExpectedContentHash` when supplied.
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
* Upload must calculate and update `ContentHash` when the provider supports content hashes.
* Upload must verify `ExpectedContentHash` when supplied.
* Upload must enforce `BlobStoreOptions.MaxBlobSize` when configured.
* Upload must update `ETag` if supported.
* Upload must update `LastModifiedAt`.
* Upload must preserve or replace properties according to the supplied upload request.
* Upload must be atomic from the caller perspective where provider support allows it.
* Upload must not expose resumable upload behavior.

## Download Semantics

`DownloadResultAsync` retrieves blob content and properties.

Rules:

* Download returns `Result<BlobDownload>`.
* Missing blob returns `BlobStoreNotFoundError`.
* Caller owns the returned stream.
* Provider should stream content where possible.
* Range download is not supported.
* Resumable download is not supported.
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
* `ContentType` may be changed and mapped to native provider MIME values through `ContentType.MimeType()`.
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
* Properties may be cached only if explicitly configured.
* List pages may be cached only if explicitly configured.

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

Add structured logging for all operations through `LoggingBlobStoreClientBehavior`.

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
* content type when known, preferably as `ContentType` plus its MIME string
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
    result.IsSuccess ? result.Value.ContentType?.MimeType() : null);
```

## Telemetry Metrics

Blob Storage must emit operation metrics through `MetricsBlobStoreClientBehavior`.

Required metrics:

* operation count
* operation duration
* operation failure count
* bytes uploaded
* bytes downloaded
* list page item count
* retry attempt count
* timeout count
* size-limit failure count

Recommended metric names:

| Metric name                       | Type      | Meaning                                      |
| --------------------------------- | --------- | -------------------------------------------- |
| `bdk.blob_storage.operations`     | Counter   | Number of blob operations started/completed. |
| `bdk.blob_storage.duration`       | Histogram | Operation duration in milliseconds.          |
| `bdk.blob_storage.failures`       | Counter   | Operation failures by error type.            |
| `bdk.blob_storage.bytes`          | Histogram | Uploaded or downloaded byte counts.          |
| `bdk.blob_storage.list_items`     | Histogram | Number of items returned by list operation.  |
| `bdk.blob_storage.retry_attempts` | Counter   | Retry attempts by operation.                 |
| `bdk.blob_storage.timeouts`       | Counter   | Operations that timed out.                   |

Recommended tags:

* client name
* provider name
* operation name
* result: `success` or `failure`
* error type for failures
* content type when known and low-cardinality
* full scan flag for list operations

Metrics must not use these as labels:

* full blob name
* raw continuation token
* arbitrary property keys or values
* unbounded exception messages
* user, tenant, or caller identity

## Testing Requirements

## Shared Contract Tests

Create provider-neutral contract tests that run against all providers.

Required tests:

* Upload returns success for valid stream.
* Upload returns `BlobInfo`.
* Upload with `FailIfExists` returns conflict failure when blob exists.
* Upload enforces `MaxBlobSize` when configured.
* Upload with unknown stream length fails once streamed bytes exceed `MaxBlobSize`.
* Size-limit failure does not commit partial content.
* Upload calculates `BlobInfo.ContentHash` as `sha256:<lowercase-64-character-hex>`.
* Upload with matching `ExpectedContentHash` succeeds.
* Upload with mismatched `ExpectedContentHash` fails with an integrity error and does not commit partial content.
* Download returns content equal to uploaded content.
* Download returns `BlobInfo`.
* Download returns the stored content hash in `BlobInfo`.
* Caller can dispose returned download.
* Download missing blob returns `BlobStoreNotFoundError`.
* Get properties returns info without downloading content.
* Update properties changes content type and property bag.
* Upload maps `ContentType.PDF` to `application/pdf` through `ContentTypeExtensions`.
* Provider MIME strings map back to `ContentType` through `ContentTypeExtensions.FromMimeType(...)`.
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
* Resumable upload and resumable download APIs are not exposed.

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

## DI Registration Tests

Required tests:

* `AddBlobStorage(o => o.Enabled(true))` registers the feature options.
* Disabled Blob Storage does not register runtime clients or health checks.
* Named provider registration resolves an `IBlobStoreClient` through `IBlobStoreClientFactory`.
* Duplicate client/store names fail with a clear configuration error.
* Factory resolution for an unknown name fails deterministically.
* `GetRegistrations()` returns the configured client names and capabilities.
* Behavior registration wraps named clients in the configured order.
* The first registered behavior is the outermost wrapper.
* `LoggingBlobStoreClientBehavior` does not log content, raw continuation tokens, or full property values.
* `MetricsBlobStoreClientBehavior` emits operation metrics without high-cardinality blob names.
* `RetryBlobStoreClientBehavior` retries transient failures and does not retry validation, size-limit, integrity, conflict, concurrency, or caller-cancellation failures.
* `RetryBlobStoreClientBehavior` rewinds seekable upload streams before retrying.
* `TimeoutBlobStoreClientBehavior` maps operation timeout to `BlobStoreTimeoutError` without masking caller cancellation.
* Aggregate health check is registered as `BlobStorage`.
* Aggregate health check checks every configured client.
* Aggregate health-check failure data identifies failed client names as readable strings.
* EF Core registration requires a context implementing `IBlobStoreContext`.
* EF Core singleton-safe clients resolve a fresh scoped `DbContext` per operation.
* Content type values map through `ContentTypeExtensions` instead of provider-local MIME tables.

## EF Core Provider Tests

Additional tests:

* Upload stores content in chunks.
* Upload stores the SHA-256 content hash.
* Upload enforces `MaxBlobSize` without committing partial chunks.
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
* Upload stores the SHA-256 content hash as metadata.
* Upload enforces `MaxBlobSize`.
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
* Upload stores the SHA-256 content hash.
* Upload enforces `MaxBlobSize`.
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
* Add `BlobStorageBuilderContext` and fluent registration extensions.
* Add named client registration metadata.
* Add `IBlobStoreClientFactory`.
* Add `LoggingBlobStoreClientBehavior`.
* Add `MetricsBlobStoreClientBehavior`.
* Add `RetryBlobStoreClientBehavior`.
* Add `TimeoutBlobStoreClientBehavior`.
* Add aggregate `BlobStorage` health check.
* Add typed Result error types.
* Add SHA-256 content hash calculation helpers.
* Add maximum blob size enforcement helpers.
* Add continuation token model and serializer.
* Add validators.
* Add `IBlobStoreClient`.
* Add `IBlobStoreProvider`.
* Implement `BlobStoreClient`.
* Implement InMemory provider.
* Add `IBlobStoreContext`.
* Implement EF Core entities.
* Implement EF Core provider chunked upload/download.
* Implement EF Core lease handling.
* Implement EF Core listing/properties/exists/delete.
* Implement Azure Blob provider.
* Add shared contract tests.
* Add builder tests.
* Add DI registration, factory, behavior, health-check, metrics, retry, timeout, and EF context tests.
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
* `BlobStoreOptions.MaxBlobSize` is configurable per registered client.
* Upload enforces the configured maximum blob size before committing content.
* `BlobInfo.ContentHash` uses SHA-256 formatted as `sha256:<lowercase-64-character-hex>`.
* Upload can validate an optional `ExpectedContentHash`.
* Blob content types use `ContentType?` in public models.
* MIME conversion uses `ContentTypeExtensions` from `src/Common.Utilities/ContentTypes`.
* Properties can be read without downloading content.
* Properties can be updated without uploading content.
* Listing is continuation-token based.
* Listing returns `BlobInfo` only and never content.
* Full container scans require explicit global and query-level approval.
* Delete is idempotent.
* Blob Storage is registered through `AddBlobStorage(...)`.
* Blob clients are registered by unique store/client name.
* `IBlobStoreClientFactory` resolves configured clients by name.
* Client behaviors compose around named clients in registration order.
* `LoggingBlobStoreClientBehavior` is available.
* `MetricsBlobStoreClientBehavior` emits low-cardinality operation metrics.
* `RetryBlobStoreClientBehavior` is available and retries only safe transient failures.
* `TimeoutBlobStoreClientBehavior` is available and maps operation timeouts to Result failures.
* A single aggregate health check named `BlobStorage` checks all configured clients.
* EF Core provider stores content in chunks.
* EF Core provider requires `IBlobStoreContext` with `StorageBlobs` and `StorageBlobChunks`.
* EF Core provider uses internal leases for upload, delete, and property update.
* Azure Blob provider uses native blob semantics and native continuation tokens.
* InMemory provider supports the same logical behavior.
* Azure Table provider is not implemented.
* Cosmos provider is not implemented.
* FileSystem provider is not implemented.
* Range downloads are not implemented.
* Resumable uploads are not implemented.
* Resumable downloads are not implemented.
* Authentication, authorization, tenant isolation, and backup/restore are not implemented by this feature.
* Shared contract tests pass.
* Provider-specific tests pass.

## Example Usage

## Registration

```csharp
services.AddBlobStorage(o => o.Enabled(true))
    .WithBehavior<LoggingBlobStoreClientBehavior>()
    .WithBehavior<MetricsBlobStoreClientBehavior>()
    .WithBehavior<RetryBlobStoreClientBehavior>()
    .WithBehavior<TimeoutBlobStoreClientBehavior>()
    .WithEntityFrameworkClient<AppDbContext>("reports", o => o
        .MaxBlobSize(50 * 1024 * 1024))
    .WithAzureBlobClient("media", o => o
        .Container("media")
        .MaxBlobSize(500 * 1024 * 1024));
```

## Resolve A Named Client

```csharp
var factory = serviceProvider.GetRequiredService<IBlobStoreClientFactory>();
var blobs = factory.CreateClient("reports");
```

## Upload

```csharp
await using var content = File.OpenRead("report.pdf");

var uploadResult = await blobs.UploadResultAsync(
    new BlobUpload
    {
        Key = new BlobKey("reports", "2026/06/report.pdf"),
        Content = content,
        ContentType = ContentType.PDF,
        Properties = new PropertyBag()
            .Set("customerId", "42")
            .Set("source", "monthly-export"),
        OverwriteMode = BlobOverwriteMode.Overwrite
    },
    cancellationToken);
```

## Upload With Expected Hash

```csharp
var expectedHash = "sha256:<lowercase-64-character-hex-sha256>";

var uploadResult = await blobs.UploadResultAsync(
    new BlobUpload
    {
        Key = new BlobKey("reports", "2026/06/report.pdf"),
        Content = content,
        ContentType = ContentType.PDF,
        ExpectedContentHash = expectedHash
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
        ContentType = ContentType.PDF,
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
* exposing public lease APIs
* implementing range download
* implementing resumable upload or download APIs
* implementing Azure Table provider
* implementing Cosmos provider
* implementing FileSystem provider
* using ETag, Content-MD5, or provider-native hashes as a replacement for the devkit SHA-256 content hash
* skipping `MaxBlobSize` enforcement for streams with unknown length
* retrying uploads without a replayable stream or provider-safe replay guarantee
* emitting metrics with full blob names, raw continuation tokens, or property values as labels
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
