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