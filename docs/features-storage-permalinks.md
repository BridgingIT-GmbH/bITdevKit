# Storage Permalinks

Storage Permalinks provide stable, opaque download URLs for resources managed by Blob Storage, Document Storage, and File Storage. The registry owns the stable identifier and maps it to the resource's current storage kind, configured registration, and location. Storage providers remain unaware of permalinks.

Use a permalink when a download URL must remain valid while a resource is renamed or moved within the same configured storage registration. The identifier is a cryptographically random 256-bit Base64Url value and does not reveal provider names, containers, partitions, keys, or paths.

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

## Creating Links

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

## Resolution and Downloads

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

## Consistency Model

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

## Expiration and Maintenance

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

## Related Features

- [Blob Storage](./features-storage-blobs.md)
- [Document Storage](./features-storage-documents.md)
- [File Storage](./features-storage-files.md)
- [Presentation Dashboard](./features-presentation-dashboard.md)
