![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you're working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

This repository includes the complete source code for the bITDevKit, along with a variety of sample
applications located in the ./examples folder within the solution. These samples serve as practical
demonstrations of how to leverage the capabilities of the bITDevKit in real-world scenarios. All
components are available
as [nuget packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [CHANGELOG](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/CHANGELOG.md).

Join us in advancing the world of software development with the bITDevKit!

## Storage Dimensions

The relational blob and document providers discover their own annotated name/value tables from the existing parent navigations. `IBlobStoreContext` and `IDocumentStoreContext` require no extra sets or SQL model-builder configuration. Generate host-owned migrations separately for SQL Server, PostgreSQL, and SQLite; no backfill worker is supplied.

Each feature keeps a nullable typed snapshot on the parent and three ordinary search indexes on its value table. Names register automatically and remain retained after owner deletion. Name caches are bounded per provider instance. Recreate the provider, or quiesce operations and call `ClearDimensionNameCache()`, after external catalog recreation.

Dimension values are searchable database metadata in plaintext. Payload encryption does not encrypt their searchable representations. EF Cosmos is unsupported and excludes the two relational document members with `modelBuilder.ExcludeDocumentDimensions()`.

See the [blob](../../docs/features-storage-blobs.md#queryable-dimensions) and [document](../../docs/features-storage-documents.md#queryable-dimensions) guides. Volume qualification remains incomplete.

## Advanced Application Caching

The optional complete `EntityFramework` cache provider belongs to `Application.Caching`. It is independent of existing minimal `Common.Caching`, Blob/Document Storage, and Key/Value Storage. One database contains cache metadata, indexes, payloads/chunks, source ownership, reader protection, and invalidation fences.

See the [Application Caching guide](../../docs/features-application-caching.md#entity-framework-and-host-owned-migrations) for the complete context capability, source coordination, limits, and deployment procedure.

### Register a consumer-owned context

```csharp
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.Extensions.DependencyInjection;

services.AddSqlServerDbContext<CatalogDbContext>(options => options.UseConnectionString(connectionString))
	.WithDbContextFactory();

services.AddApplicationCaching(cache => cache
	.WithEntityFrameworkProvider<CatalogDbContext>("central")
	.WithCollection<ReportSummary>("summaries", collection => collection
		.UseProvider("central")
		.ShareLoadsAcrossNodes()));
```

`CatalogDbContext` and `ReportSummary` are fictional application types. The context implements `IApplicationCachingContext`; the guide shows its twelve `DbSet` properties. A `ModuleDbContextBase` implementation receives the cache model automatically through that interface. A plain `DbContext` calls `CacheModelConfiguration.Configure` from `OnModelCreating`. The factory must create a fresh independently owned context for each bounded native call, rather than returning a request-scoped instance.

### Deploy the annotated model

**The runtime package contains no migrations or deployment SQL.** Consumers scaffold and deploy engine-specific migrations for their own cache-capable context. The standalone `CacheDbContext` is an alternative when configured with a consumer-owned migrations assembly. Cache registration/requests do not create or migrate the database. Deploy first, then satisfy the configured named `IDatabaseReadyService` gate before provider readiness.

The twelve annotated default tables are `__Caching_Stores`, `__Caching_Collections`, `__Caching_Contracts`, `__Caching_Keys`, `__Caching_Entries`, `__Caching_Payloads`, `__Caching_Chunks`, `__Caching_Dimensions`, `__Caching_Fences`, `__Caching_Ownerships`, `__Caching_Readers`, and `__Caching_Operations`. Annotations define keys, lengths, indexes, and relationships. Provider transactions enforce publication fencing and operation revisions. Enrichment supplies canonical token conversions, physical naming, and the format seed.

`CacheSchemaOptions` can select bounded physical names. Changing schema/prefix requires matching host deployment artifacts; it is not a runtime rename. The immutable payload generation also identifies the public entry version, so there is no separate duplicate entry-version column. Collection counters and tracked-operation revisions protect concurrent publication and operation continuation.

### Engine and execution limits

SQL Server and PostgreSQL provide shared database authority. SQLite is a local-host file profile and can coordinate local processes; do not put its database on a share or claim multi-host SQLite. SQLite native asynchronous methods can execute synchronously, and its busy timeout does not prove prompt cancellation. Actual occupied calls retain admission until they end.

Avoid `EnableRetryOnFailure` or another automatic mutation execution strategy for the provider's contexts. The cache owns operation IDs, conditions, and reconciliation; a consumed stream or uncertain mutation cannot be replayed blindly. Small values use the configured inline threshold, larger values use bounded immutable chunks, and metadata selection does not deserialize payloads.

A central provider alone does not select shared source coalescing. `NodeLocal` is the collection default. Select `SharedStore` when one confirmed source owner across web nodes is required. Provider authority failure then starts no new source execution. Hot retention, when enabled, still validates the database on every reuse.

Use the [common provider benchmark workloads](../../benchmarks/Application.Benchmarks/Caching/README.md) and [recorded profiles](../../benchmarks/Application.Benchmarks/Caching/RESULTS.md) for matching engine/payload/concurrency measurements. Local SQLite success does not supply SQL Server/PostgreSQL performance, network-failure, or native cancellation evidence.
## Storage properties

Blob and document `Properties` are optional scalar metadata. Null preserves the existing bag on replacement/update, an empty bag clears it, and a non-empty bag replaces it. Inserts without properties return an empty bag. Typed .NET calls preserve the `PropertyBagScalarCodec` scalar types and copy byte arrays. HTTP retains ordinary JSON semantics. Transform metadata is separate from application properties. Properties are not indexed; use the separate Dimensions collections for indexed metadata queries.

The blob provider contracts and persisted blob formats have changed. Use newly created storage; this refactor supplies no migrations, backfill, or compatibility readers. See the [blob guide](../../docs/features-storage-blobs.md#properties) and [document guide](../../docs/features-storage-documents.md).

## Storage Dimensions

The EF blob and document providers implement independent optional Dimensions on SQL Server, PostgreSQL, and SQLite. Every accepted scalar is indexed automatically. Applications supply names and values; the database generates dictionary IDs. Standard EF annotations discover each feature's name/value tables through the parent navigations, with no new context sets or SQL model-builder calls.

Values support `int`, exact `decimal(18,6)`, `bool`, Unicode string, UTC `DateTime`, and explicit null. Strings compare exactly and accept at most 256 UTF-8 bytes. SQLite supports decimal equality only; SQL Server and PostgreSQL also support decimal ranges. Inspect the effective provider capabilities before accepting Dimensions. Unsupported EF engines reject explicit Dimensions, including empty collections, before I/O. EF Cosmos document contexts use `ExcludeDocumentDimensions()` in their Cosmos-specific model configuration.

Omitted replacement input preserves Dimensions; empty clears them. Dimensions-only updates preserve payload bytes and use the existing whole-item ETag. Name registration, value changes, the parent snapshot, and its version commit atomically. Relational owner deletion cascades to values and content chunks while retaining dictionary names. The chunk composite primary key enforces uniqueness without a duplicate secondary index.

Runtime support is implemented; server volume qualification remains incomplete. See the [blob Dimensions guide](../../docs/features-storage-blobs.md#queryable-dimensions), [document Dimensions guide](../../docs/features-storage-documents.md#queryable-dimensions), and [implementation plan](../../plan/pln-feature-storage-dimensions-1.md) for contracts, setup, measured scaling, and remaining acceptance checks.
