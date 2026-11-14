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

`WithFileSystemProvider()` registers a complete native `Application.Caching` provider. It uses a dedicated root for payloads, checked metadata/indexes/journals, source ownership, publication fences, reader pins, and quota credits. It does not compose the cache over FileStorage or Blob Storage, add a hidden SQLite catalog, or replace `Common.Caching`.

```csharp
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using Microsoft.Extensions.DependencyInjection;

services.AddApplicationCaching(cache => cache
	.WithFileSystemProvider("report-disk", provider => provider
		.Root(absoluteDedicatedCacheRoot)
		.AccessKind(FileSystemStorageAccessKind.Local)
		.Durability(FileSystemStorageDurability.Durable)
		.MaxResidentBytes(128 * CacheDefaults.MiB)
		.MaxStorageBytes(10 * CacheDefaults.GiB)
		.MaxEntries(1000000))
	.WithBinaryContract("report-file")
	.WithBinaryCollection("reports", collection => collection
		.UseProvider("report-disk")));
```

The root must be absolute and provider-owned. Applications do not edit, rename, remove, or read managed files directly. Logical keys never become arbitrary physical paths. The [feature guide](../../docs/features-application-caching.md#native-filesystem-registration) explains registration, exact-key/read/index behavior, binary ownership, and finite defaults.

### Native profiles and acknowledgements

Local roots target Windows and Linux. SMB targets Windows and Linux clients; NFS targets Linux clients. Windows-to-NFS and mixed protocols over one root are not supported profiles. Native probes can reject unsupported authority or persistence semantics. Declaring an access kind does not qualify a particular share, server, mount, reconnect, or mixed-host deployment.

`Durable` is the default acknowledgement and includes the selected profile's data and namespace persistence barriers. `Buffered` explicitly accepts loss of recent acknowledged changes after storage or host failure. It retains atomic publication, conditions, fences, and integrity. A failed durable barrier never becomes buffered success.

Format one uses 256 lazy shards, 64-KiB metadata/index blocks, immutable chunks up to 1 MiB, checked commit receipts, and bounded journal/index work. Defaults checkpoint at 16 MiB, cap an uncheckpointed tail at 32 MiB, and cap a quota credit at 8 MiB. Current, staged, retired, and reader-owned data count until safe reclamation. Exact-key calls do not scan the complete root.

### Offline recovery requires an external maintenance proof

`FileSystemCacheRecovery.GetStateAsync` reads the checked `StoreId`, generation, and recovery state without creating a store or scanning payloads. Before `RecoverQuiescentAsync` or `ResetQuiescentAsync`, stop or independently fence every reader, writer, sweeper, and recovery process on every host, and drain server-side replays. A timeout or stale marker does not establish that proof.

Pass the independently inspected store identity and `allParticipantsStopped: true` only after that maintenance boundary exists. The API records the acknowledgement; it cannot stop other hosts for the operator. Recovery durably fences earlier generations before cleanup and reopens only verified state. Damaged or uncertain committed state can return `RequiresReset` while keeping the root fenced. Reset is a separate explicit choice that discards the disposable cache and creates a new empty store identity.

Recovery and reset both require a valid checked root identity/profile. If `cache-root.json` itself cannot be verified, do not invent an identity or adopt the directory. Select a new dedicated empty cache root and rewarm, or manage disposal independently outside these APIs. Reset applies to damaged or uncertain data/control shards under a verified root, not an arbitrary unidentifiable directory.

An uncertain Buffered tail must not reopen an older readable generation that could undo acknowledged invalidation. Do not delete authority/journal files to force availability, infer native termination from elapsed time, or reconstruct missing committed metadata from leftover payloads. Recreate providers after completed offline recovery; old instances and continuations remain excluded.

See the [operator procedure](../../docs/features-application-caching.md#offline-recovery-and-reset), [native benchmark instructions](../../benchmarks/Application.Benchmarks/Caching/README.md), and [recorded profiles](../../benchmarks/Application.Benchmarks/Caching/RESULTS.md). An incomplete profile or a smaller replacement dataset cannot establish performance at a larger scale. Local restart/crash tests and local measurements do not qualify external SMB/NFS, reconnect, independent-host authority, kernel crash, or power loss. Unexecuted deployment profiles remain unqualified.