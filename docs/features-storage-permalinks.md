# Storage Permalinks

Storage Permalinks provide stable, opaque download URLs for resources managed by Blob Storage, Document Storage, and File Storage. The registry owns the stable identifier and maps it to the resource's current storage kind, configured registration, and location. Storage providers remain unaware of permalinks.

Use a permalink when a download URL must remain valid while a resource is renamed or moved within the same configured storage registration. The identifier is a cryptographically random 256-bit Base64Url value and does not reveal provider names, containers, partitions, keys, or paths.

[TOC]

## Overview

Storage Permalinks add stable public identifiers above Blob, Document, and File Storage. An application selects a registry, opts named storage registrations into change tracking, and can expose one download endpoint that resolves identifiers without revealing physical storage locations.

## Challenges

Direct storage URLs couple callers to a provider, registration, and mutable path. Renames and moves can invalidate shared links, while exposing raw keys or paths can reveal implementation details. The link registry must also stay consistent with successful storage mutations without making every provider permalink-aware.

## Solution

Opt-in storage behaviors maintain mappings in `IStoragePermalinkRegistryProvider`. Each mapping joins an opaque `StoragePermalinkId` to a typed storage location. A hosted dispatcher applies successful storage changes, and the download endpoint resolves the current location through the configured storage client.

## Key Features

- 256-bit Base64Url identifiers that do not encode storage locations
- in-memory and Entity Framework registry providers
- opt-in Blob, Document, and File Storage tracking
- stable identifiers across moves within one registration
- expiration, revocation, optimistic concurrency, and maintenance APIs
- optional anonymous or authorized download endpoints
- dashboard controls and low-cardinality metrics

## Architecture

Storage clients remain responsible for reading and mutating resources. Permalink behaviors verify resources and publish bounded change notifications after successful operations. The registry stores the durable mapping, while the HTTP endpoint resolves a mapping and delegates the download to the corresponding storage registration.

## Use Cases

- share a report without exposing its blob container and key
- keep a download URL valid after a file is renamed
- revoke a link without deleting its underlying resource
- apply an expiration date to an externally shared document
- manage stable links from an operations dashboard

## Basic Usage

This example configures an in-memory registry and file provider, writes a file, checks both results, and returns the stable download URL.

```csharp
using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStoragePermalinks()
    .UseInMemory()
    .AddDownloadEndpoints();

builder.Services.AddFileStorage(factory => factory
    .RegisterProvider("reports", storage => storage
        .UseInMemory("Reports")
        .WithPermalinks()
        .WithLifetime(ServiceLifetime.Singleton)));

var app = builder.Build();
app.MapEndpoints();

app.MapPost("/reports/{name}/link", async (
    string name,
    IFileStorageProviderFactory factory,
    CancellationToken cancellationToken) =>
{
    var reports = factory.CreateProvider("reports");
    await using var content = new MemoryStream(
        Encoding.UTF8.GetBytes($"Report: {name}"),
        writable: false);

    var write = await reports.WriteFileAsync(
        name,
        content,
        cancellationToken: cancellationToken);

    if (write.IsFailure)
    {
        return Results.Problem(string.Join(
            "; ",
            write.Errors.Select(error => error.Message)));
    }

    var permalink = await reports.GetPermalinkAsync(
        name,
        cancellationToken: cancellationToken);

    if (permalink.IsFailure)
    {
        return Results.Problem(string.Join(
            "; ",
            permalink.Errors.Select(error => error.Message)));
    }

    var downloadUrl = $"/_bdk/api/storage/permalinks/{permalink.Value.Id.Value}";
    return Results.Created(downloadUrl, new { DownloadUrl = downloadUrl });
});

app.Run();
```

`POST /reports/summary.txt/link` returns a URL such as `/_bdk/api/storage/permalinks/{id}`. A `GET` to that URL downloads `summary.txt`. The in-memory registry is volatile; use the Entity Framework provider for links that must survive restarts.

## Registration

Select a registry provider explicitly, add the download endpoint, and opt individual storage registrations into tracking:

```csharp
services.AddStoragePermalinks(options =>
    {
        options.QueueCapacity = 4096;
        options.RetryAttempts = 3;
    })
    .UseInMemory()
    .AddDownloadEndpoints();

services.AddBlobStorage()
    .WithInMemoryClient("reports")
    .WithPermalinks("reports");

services.AddDocumentStorage()
    .WithInMemoryClient<Report>(name: "default")
    .WithPermalinks<Report>("default");

services.AddFileStorage(factory => factory
    .RegisterProvider("files", builder => builder
        .UseInMemory("files")
        .WithPermalinks()));
```

`UseInMemory()` is volatile and intended for development, tests, and process-local scenarios. For persistent links, let the application `DbContext` implement `IStoragePermalinkRegistryContext`, expose `DbSet<StoragePermalink>`, and use:

```csharp
services.AddStoragePermalinks()
    .UseEntityFramework<AppDbContext>()
    .AddDownloadEndpoints();
```

The EF provider creates and owns a dependency-injection scope for every operation. The entity contains its table and index configuration through annotations.

## Creating links

Permalink creation verifies that the requested resource exists when called through an opted-in storage client:

```csharp
var result = await blobs.GetPermalinkAsync(
    new BlobKey("public", "reports/summary.pdf"),
    new StoragePermalinkCreateOptions
    {
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
    },
    cancellationToken);
```

Repeated calls for the same active location return the same identifier. Writes do not return a permalink automatically; call `GetPermalinkAsync` after a successful write when the link is needed.

Permalink identifiers are immutable and cannot be renewed in place. Expiration can be replaced or cleared without changing the identifier. Deleting a permalink permanently revokes that identifier; the feature does not currently expose identifier rotation or renewal.

## Resolution and downloads

The default route is:

```text
GET /_bdk/api/storage/permalinks/{id}
```

It is anonymous by default because the random identifier is the bearer secret. Standard endpoint options can require a policy, role, or authenticated user:

```csharp
services.AddStoragePermalinks()
    .UseInMemory()
    .AddDownloadEndpoints(options => options
        .RequireAuthorization()
        .RequirePolicy("StorageDownloads"));
```

Responses use `Cache-Control: private, no-store` and `Referrer-Policy: no-referrer`. Invalid, expired, deleted, physically absent, and unavailable-provider targets do not expose their registry location.

Documents download as JSON. Blobs and files retain their inferred or stored content type and download filename.

## Consistency model

Storage mutation and registry persistence are intentionally not one transaction. Opt-in behaviors publish bounded in-process change notifications after successful DevKit storage operations. A hosted dispatcher handles each notification in a fresh scope and retries failures.

- Uploads and upserts ensure an active location mapping exists.
- Deletes tombstone the mapping. A delayed older upsert cannot recreate it.
- A later real write to the same location creates a new identifier.
- Same-registration moves preserve the existing identifier and expiration.
- A copy followed by a failed source delete creates or retains the target mapping without moving the source mapping.
- Cross-registration moves are not permalink-preserving and retain the storage APIs' existing copy/delete semantics.
- File directory moves and recursive deletes update all tracked descendants.
- Provider-native Blob and Document retention sweeps publish the exact successfully deleted keys for opted-in registrations, so expired physical resources also tombstone their permalink mappings.

There is no periodic storage scan. Direct provider, database, filesystem, or Azure changes made outside the DevKit storage clients are not observed. A later verified `GetPermalinkAsync` call repairs a missing active mapping lazily.

## Expiration and maintenance

Permalink expiration controls the link, not the underlying resource. `IStoragePermalinkMaintenanceService` supports bounded listing, lookup including expired records, replacement or clearing of expiration, and permalink deletion. ETags provide optional optimistic concurrency for expiration and delete operations.

The dashboard contributes a compact **Permalinks** page for filtering, direct downloads, copying links, expiration edits, and deletion. Blob, Document, and File Storage tables include permalink download and copy actions. The registry page refresh interval is off by default and remembered in browser local storage.

## Metrics

The `BridgingIT.DevKit.Storage.Permalinks` meter emits low-cardinality counters and duration histograms for registry operations, downloads, synchronization events, retries, and queue depth. Tags include operation, outcome, storage kind, and registry provider. Permalink identifiers, keys, paths, containers, and partitions are never metric tags.

## Security

- Treat permalink IDs as bearer credentials when anonymous downloads are enabled.
- Prefer finite expiration for externally shared links.
- Delete a permalink to revoke it without deleting the resource.
- Application logs and telemetry should not record permalink identifiers in public request analytics.
- Registry encryption is not required for IDs or ordinary locations; applications should avoid sensitive data in storage keys and paths.

## Related features

- [Blob Storage](./features-storage-blobs.md)
- [Document Storage](./features-storage-documents.md)
- [File Storage](./features-storage-files.md)
- [Presentation Dashboard](./features-presentation-dashboard.md)
