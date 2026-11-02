# ADR-0037: Use Direct File I/O for Filesystem Storage Providers

## Status

Accepted and implemented as of 2026-11-02. The providers use direct file I/O and existing optional TTL/retention constructs. Windows local/SMB and Linux local/SMB/NFS are the implementation targets. Deployment and full reference-performance qualification are tracked separately in the [qualification checklist](../storage-filesystem-qualification.md); acceptance of this design does not certify an untested deployment.

## Date

2026-11-01

## Context

Applications need filesystem implementations of `IBlobStoreProvider` and `IDocumentStoreProvider`. The [requirements](../prd/storage/prd-0000-STORAGE-filesystem-blob-and-document-providers.md) cover restart persistence, complete record publication, concurrency conditions, metadata, queries, and streaming blobs. The first release must support multiple local processes and multiple servers sharing a store, targeting both SMB and NFS.

Windows and Linux are both required application-host platforms for local disk and file shares. The requirements document defines the mandatory host/storage matrix. The persisted format, logical key semantics, and coordination identity must support compatible clients on either OS, including mixed-platform clients accessing one shared store. Physical path syntax and file-sharing primitives may differ behind the provider boundary.

Windows-to-NFS access is explicitly out of scope. Mixed Windows/Linux clients access a shared store over SMB; NFS is required for Linux clients. One share protocol per shared store is agreed. Simultaneous SMB and NFS access to the same underlying store is out of scope; separate stores may use different protocols. Local-only multi-process access remains supported.

The existing File Storage feature already supplies local file operations. Reusing it is attractive because it offers Result errors, streams, optional staged writes, and established registration. However, a blob/document record includes more state than one content file.

Current source establishes these boundaries:

- [`IFileStorageProvider`](../../src/Application.Storage/Files/IFileStorageProvider.cs) has file operations but no atomic compare-and-swap ETag contract, complete-record transaction, or explicit commit/abort operation.
- [`LocalFileStorageProvider`](../../src/Infrastructure.FileSystem/Files/LocalFileStorageProvider.cs) writes ordinary content with `FileMode.Create`. Its staged writable stream moves one temporary file into place on successful disposal. Its local metadata implementation exposes file length and modification time, not the complete blob/document metadata model. [ADR-0038](0038-shared-filesystem-infrastructure-for-file-storage.md) subsequently moves the provider into Infrastructure and shares low-level primitives without composing the blob or document provider over File Storage.
- That local provider coordinates through instance-local semaphores. Its listing sorts and materializes all matching paths before offset paging with a fixed page size of 100.
- [`IDocumentStoreProvider`](../../src/Application.Storage/Documents/IDocumentStoreProvider.cs) assigns atomic persistence, ETags, expiration filtering, and native continuation state to providers. [`IBlobStoreProvider`](../../src/Application.Storage/Blobs/IBlobStoreProvider.cs) similarly requires blob-specific content and metadata operations.
- File Storage and both contracts already live in [`Application.Storage`](../../src/Application.Storage/Application.Storage.csproj). Composing File Storage does not currently add a separate package dependency.

## Decision

Implement two filesystem providers using direct file I/O, preferring BCL `System.IO` where it can satisfy the qualified filesystem contracts. Share an internal persistence component for key-to-path mapping, shared-store initialization, cross-process/server mutation coordination, explicit publication/abort, and safe cleanup. BCL-only sufficiency is a feasibility question to prove, not a settled guarantee.

Do not take a lifetime exclusive lock on the whole root. Compatible application instances must be able to join it. Coordination must resolve the same store/key across machines and mount aliases. Per-key mutation coordination is preferred over serializing all unrelated writes behind one store-wide lock.

The agreed foreground acquisition wait is five seconds by default, configurable per store, with cancellation and a zero-wait fail-fast setting. Exhaustion produces a typed busy/timeout failure, never lock theft or unlocked fallback. This is an acquisition budget, not a lock lease, an upload limit, or a guaranteed network I/O deadline. Retention uses non-waiting candidate acquisition, skips busy keys, and revisits them in subsequent sweeps without starving other candidates. Staged uploads acquire authority only for the publication phase and revalidate conditions then.

Keep blob semantics in the blob provider and document semantics in the document provider. Reuse existing clients for validation, typed document serialization, transforms, integrity checks, behaviors, and protected public continuation tokens.

Include optional expiration and physical retention in the first release. Implement the existing provider retention interfaces and reuse the existing hosted workers, options, and result types. No filesystem-specific retention scheduler is introduced.

Place production filesystem adapters in a proposed optional `Infrastructure.FileSystem` project referencing `Application.Storage`. Follow the current Infrastructure provider pattern rather than expanding production I/O in the Application layer. Do not move existing File Storage types as part of this feature.

Do not introduce a public `IFileSystem` abstraction or route Document Storage through Blob Storage. Extract internal utilities only when both providers need the same proven operation. This applies the Laziness Protocol principle to avoid speculative layers.

Direct I/O is the agreed implementation boundary, not an accepted on-disk format or a demonstrated performance improvement. The publication protocol and its measured costs remain prerequisite evidence.

## Agreed durability policy

Use Durable acknowledgement by default. Buffered acknowledgement is an explicit per-store opt-in for workloads that accept losing recent changes after a host or storage failure. These names map to the implemented FileSystemStorageDurability options. Deployment qualification remains separate.

Durable success follows completion of the qualified persistence protocol for both content and authoritative publication metadata. Metadata-only updates, deletes, and references to content previously created in Buffered mode require the same complete durability reasoning. A payload flush alone does not prove that the record's publication or deletion will survive a storage failure.

Buffered mode may return once the OS has accepted a complete mutation before durable persistence. It does not weaken ETag conditions, atomic record publication, reader lifetime, or cross-host coordination. A durability failure or missing acknowledgement cannot silently become buffered success.

The exact flush/ordering/publication operations and hardware/server assumptions remain part of the R2 proof for each supported local filesystem and share. Measure default-mode performance with Durable acknowledgement; report Buffered separately under its different failure guarantee.

## Agreed layout ownership

Providers own physical directory layout and filenames. Logical keys need not be visible as friendly filesystem names; they remain available through existing APIs and persisted record metadata. Encoded or hashed path components must preserve the existing key semantics, with original identity verified on access.

Applications and operators do not edit, rename, or remove individual managed files externally. Provider-owned layout permits sharding, staging files, and payload generations without committing to a fixed directory schema here. The proposed immutable-payload/manifest representation still requires publication, recovery, and performance qualification.

## Agreed live pagination

Listings and document queries use live pagination, not retained query snapshots. On an unchanged dataset, forward continuation returns each match once in deterministic logical-key order. Use the last logical ordering position rather than an offset into a changing directory listing.

Concurrent inserts after that position may appear on later pages; inserts at or before it may be missed by the current sequence. Deletes and updates can change later results. Each returned record must still represent one committed generation, and point-read/write consistency is unchanged. Neither an entire page nor all pages are promised to reflect one instant or a snapshot-consistent count.

Reuse existing public token protection/query binding and preserve the document page sequence's fixed visibility cutoff. That cutoff controls expiration filtering, not dataset versioning. The concrete traversal is documented in the implementation state below. Complete matrix qualification and scan-cost acceptance remain R3 work.

## Performance requirements shaping the design

Both providers must qualify as high-performance disk storage under explicit workload and environment budgets from requirements decision R5. The choice of direct I/O allows control over these costs; removing an abstraction alone does not establish a measurable gain.

Balanced workload coverage is agreed. Small records, large blob streams, metadata/query operations, and retention all need performance and allocation evidence. No favorable aggregate score can substitute for a failing required scenario. The requirements document defines adjustable initial payload, cardinality, concurrency, feature, and mixed-workload profiles.

Reuse the existing [`Application.Benchmarks`](../../benchmarks/Application.Benchmarks/Application.Benchmarks.csproj) BenchmarkDotNet project and its memory-diagnosis conventions where appropriate. Complement those runs with repeatable multi-process/multi-host sustained load tests; allocation microbenchmarks alone do not establish network throughput or p95/p99 latency. No filesystem benchmark implementation or measurement exists yet.

- Resolve exact logical keys to deterministic record paths. Keep directory enumeration, cleanup scans, and query-index maintenance that requires broad scans out of exact-key request paths.
- Coordinate mutations per key. Distinct keys must not share a store-wide mutation lock. Shared-format initialization can use separately scoped coordination.
- Stage and validate large uploads before taking exclusive publication authority, then re-read committed state and check conditions inside that authority. Shared staging/cleanup rules must protect those files before publication.
- Stream blobs with bounded transfer buffers. Account for existing integrity checks and avoid additional full-payload copies, rereads, or hash passes without a demonstrated need.
- Update properties through metadata publication without reading or rewriting unchanged payloads. Keep key-only listings independent of payload reads.
- Measure lock wait and hold time, file opens, metadata operations, flushes, payload passes, allocations, and network round trips. Separate small-record random I/O from large sequential transfers and same-key contention from unrelated-key concurrency.
- Evaluate sharded paths, buffer sizes, and bounded concurrency against measured bottlenecks. Any caching must preserve the required cross-host visibility; any index must preserve shared mutation and recovery semantics.
- Compare candidate layouts using the same correctness and durability settings. Network latency, disk bandwidth/IOPS, server metadata capacity, or lock contention may dominate even when provider CPU overhead is low.
- Report allocated bytes per completed operation, allocation rate, GC activity, and steady/peak memory as well as throughput and latency. Observe asynchronous workers and all participating processes, and separate runner/serialization/transform overhead from provider persistence overhead.
- Verify bounded blob transfer allocations, no payload materialization for metadata/key-only operations, and no store-sized exact-key collections or unbounded retained coordination state. Document serialization and returned models have legitimate allocations that must be reported rather than hidden.
- Fix reference machines and use baseline evidence to set numerical budgets before optimization comparisons and release sign-off. Use Durable for default acceptance runs; Buffered measurements remain separate. Baselines and acceptance numbers have not yet been measured or selected.

This applies the Explain the Number principle: set the decision and acceptance budgets before measurement, report errors and run-to-run variation, and identify the actual limiter. Qualify local disk, SMB, and NFS separately through both provider and end-to-end client paths. No throughput, latency, or speedup is claimed at planning time.

## Alternatives considered

| Approach | Benefits | Costs and limitations | Recommendation |
| --- | --- | --- | --- |
| Compose any `IFileStorageProvider` | Reuses existing registration, streams, errors, and potential backend choices. | The interface cannot promise conditional atomic mutation, cross-host exclusion, lock-loss handling, or content/metadata publication. Wrapping arbitrary providers would create a new portability contract. | Do not use as the persistence boundary. |
| Compose `LocalFileStorageProvider` only | Reuses established local operations without claiming arbitrary backend portability. | Still needs persisted metadata, shared coordination, publication/abort, key mapping, and query planning. Requiring raw I/O beside the wrapper splits ownership. | Not preferred for the initial design. |
| Direct file I/O with shared internal utilities | Owns required filesystem behavior and tuning explicitly; no separate database/coordination service; blob and document semantics stay separate. | Must prove cross-host coordination, stale-writer exclusion, recovery, performance budgets, and platform/mount behavior. BCL APIs may not expose every needed primitive. Some overlap with File Storage is accepted. | Direction agreed; detailed protocol and performance qualification remain open. |
| EF providers with SQLite | Reuses existing transaction and provider implementations. | Persists payloads inside a database, not individual content files. Does not satisfy the requested storage representation. | Remains an existing deployment alternative. |
| Separate database/coordinator with filesystem payloads | Can provide an explicit metadata/CAS authority independent of share locks. | Adds deployment dependencies and requires consistency between that authority and payload storage. An external lock alone does not prevent an obsolete writer unless publication enforces its authority. | Contingency if filesystem-only publication cannot be proven; would require a revised decision. |

## Publication design to prove

With layout ownership agreed, a candidate representation uses immutable content generations and one replaceable manifest per logical key. The manifest holds original identity, generation reference, format version, and complete provider metadata. The implemented format-2 encoding and sharding are documented below; the required deployment guarantees remain to be qualified.

1. Stage new content in the same managed filesystem, stream/hash it, and finish validation before publishing anything.
2. Acquire qualified cross-process/server authority for the key, re-read authoritative committed state, check create-only/ETag conditions, and publish the manifest through a qualified single-file replacement operation. Obsolete authority must not permit publication after another writer has acquired the key.
3. Readers acquire one committed generation and retain its lifetime while streaming across hosts. Coordinate the manifest-to-payload-open interval with cleanup. Metadata-only updates publish a new manifest referencing the same content.
4. Deletion removes the published reference under the same coordination rules. Cleanup removes abandoned or superseded generations only after proving they are not referenced, held by readers, or still staged by a live writer on any participant.

This is a candidate, not a guarantee that one `File.Move` call supplies all required durability or distributed exclusion. Same-filesystem staging, metadata/cache visibility, reader sharing, deletion, process termination, disconnect/reconnect, flush policy, and orphan cleanup must be verified on supported hosts and shares.

Writing `content.bin` and `metadata.json` independently into their final locations is insufficient because termination between writes can expose mismatched state. The new component must expose explicit publication and abort rather than treating disposal alone as proof that a source copy completed.

The BCL documents cross-volume moves as copy-and-delete and warns that copy-and-delete replacement is not atomic. The design must therefore avoid cross-volume publication and cannot substitute that sequence for a qualified commit operation. See [File.Move documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0). Buffer flushing is a separate concern; see [FileStream.Flush documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0).

## Shared-filesystem qualification

SMB is required on Windows and Linux; NFS is required on Linux only. Proposed baselines are SMB 3.x and NFSv4.1+ on Linux. The exact client/server/runtime and mount configuration must be selected per supported combination and published after validation. Windows NFS client qualification is not required.

Every participant in a shared store uses the same configured protocol. Keep that deployment rule in the store registration/operating documentation and reject incompatible configuration where detectable. Do not claim startup validation can discover every external alias, mount, or alternate server export of the backing directory.

The [SMB2 LOCK specification](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-smb2/6178b960-48b6-4999-b589-669f88e9017d) associates byte-range locks with file handles and assigns same-client process conflict resolution to the client. [Durable-open reconnect](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-smb2/3309c3d1-3daf-4448-9faa-81d2d6aa3315) is a separate protocol operation. These facts motivate testing the actual OS/runtime lock mapping and reconnect behavior rather than assuming a .NET lock call is a complete distributed protocol.

[NFSv4.1](https://www.rfc-editor.org/rfc/rfc8881.html#section-8.4.3) permits revocation of locks after a client lease expires during a network partition. Therefore a writer that once held a lock cannot assume it may publish after reconnecting. Proof must cover whether publication itself is protected from an obsolete writer, not merely whether a lock can be acquired initially.

The protocol proof must establish:

- Shared coordination for create-only, ETag mutation, properties updates, and deletes; local semaphores are only optional contention optimizations.
- No unsafe lockfile deletion based on age or local clock. A paused or partitioned owner may still resume. Do not assume a separate lockfile automatically fences replacement of a different manifest file.
- A later successful reader on another healthy client observes the committed generation. No process-local metadata cache may override authoritative state.
- Cross-host reader protection and safe cleanup. Uncertain files may be retained conservatively until safe collection is proven; document the storage cost.
- Contention/unavailable outcomes and the limits of OS/network call cancellation. No fallback to unlocked I/O.
- Indeterminate commit outcomes. If publication succeeded but its acknowledgement was lost, return an explicit indeterminate provider failure and provide reconciliation. Never report definite rollback or automatically replay an overwrite. Verify interaction with existing retry behaviors.
- Process pause/resume, network partition/heal, client crash, server restart, reconnect, and mount/cache behavior using independent client hosts.

Prefer server-enforced handle/lock and publication semantics where the qualified target can prove exclusion. Do not invent a timestamp-based distributed lease protocol to simulate them. If neither a filesystem protocol nor another verified publication design can meet the contract, revise this architecture before implementation rather than quietly reducing the confirmed deployment scope.

## Retention integration

The filesystem blob provider implements [`IBlobStoreRetentionProvider`](../../src/Application.Storage/Blobs/Retention/IBlobStoreRetentionProvider.cs); the filesystem document provider implements [`IDocumentStoreRetentionProvider`](../../src/Application.Storage/Documents/Retention/IDocumentStoreRetentionProvider.cs). Existing [`BlobRetentionBackgroundService`](../../src/Application.Storage/Blobs/Retention/BlobRetentionBackgroundService.cs) and [`DocumentRetentionBackgroundService`](../../src/Application.Storage/Documents/Retention/DocumentRetentionBackgroundService.cs) resolve these providers through the normal named/keyed registrations.

Reuse [`StorageRetentionOptions`](../../src/Application.Storage/Retention/StorageRetentionOptions.cs), including its enabled default, startup delay, sweep interval, batch size, maximum batches, batch delay, and stop timeout. Preserve the existing optional expiration APIs. Documents are logically absent at the supplied visibility cutoff; blobs retain their existing read behavior until deletion. Items without expiration remain stored.

Candidate discovery belongs inside the provider, not in a new timer or public-client listing loop. Expiration buckets or resumable metadata traversal are layout candidates, not a finalized design. Bound inspected metadata as well as deletion batches, and prove progress without losing due records after a crash or expiration update. Record candidate maintenance/recovery costs when comparing layouts.

For each due candidate, acquire the proven cross-host mutation authority and re-read its current generation and expiration before deleting it. An expiration that was extended or cleared must prevent deletion. Multiple sweepers treat already-deleted records idempotently. Payload reclamation respects active readers on every host, and uncertain commit outcomes follow the shared reconciliation rules.

Candidate acquisition does not wait for the foreground acquisition budget. Skip busy keys and revisit them later. Preserve the existing blob skipped-count and document progress reporting semantics, and ensure repeated busy candidates do not starve other due records.

Return the existing result types with confirmed deleted keys/counts and their provider-specific progress fields. This preserves the existing diagnostics and permalink change processing. Advertise expiration and retention capabilities accurately; filesystem sweeping is not native server lifecycle management.

This applies the Laziness Protocol principle by reusing the existing retention lifecycle rather than adding another scheduler or public maintenance API. Internal candidate state and cleanup coordination still require a filesystem implementation and qualification.

## Consequences

- Filesystem consistency belongs to one internal component rather than being split between a general file adapter and specialized record logic.
- Existing clients, other providers, and File Storage remain usable without adopting the new package.
- Direct I/O is not automatically safer. The implementation carries the verification cost for failures, path confinement, concurrent processes/servers, recovery, and stream lifetime. The shared component must not be assumed small until the distributed protocol is proven.
- Keys may map to hashed/encoded directories rather than friendly filenames. Original identities must be stored and verified to preserve semantics and detect mismatches.
- A file-backed document can hold transformed bytes; readable JSON is not a universal storage guarantee.
- Index-free prefix queries and full scans have costs proportional to inspected metadata. Bounded output does not imply bounded scan work. Capabilities must expose the actual query implementation.
- Windows and Linux support, local disks, and both same-machine multi-process and cross-server file-share access are first-release requirements. Filesystem qualification and real shared-access tests are release prerequisites.
- High performance is also a first-release requirement. A correct layout that misses the agreed latency/throughput budgets must be tuned or revised before release; a faster layout that weakens consistency is not an acceptable substitute.
- Low allocation and balanced coverage are explicit release requirements. Required evidence spans every supported operation and host/storage combination, including foreground traffic with active retention. Benchmarks are implementation deliverables, not claims of performance at design time.
- Durable acknowledgement is the agreed default and includes commit persistence costs. Buffered mode is an explicit trade-off in acknowledged-data durability, not a change to concurrency or atomicity guarantees.
- Network interruption can make an operation unavailable or its commit outcome indeterminate. Consistency takes priority over continuing writes without confirmed coordination.
- Physical retention is included in the first release. Expired records are deleted through bounded provider-side sweeps using existing hosted services; actual deletion can lag the expiration timestamp due to scheduling, backlog, or storage unavailability.
- Retention competes for filesystem metadata operations and mutation authority. Performance qualification must include active sweepers and concurrent expiration updates, not only idle-cleanup foreground throughput.

## Validation required before acceptance

- Resolve the requirements document's protocol qualification and query decisions; the deployment choice itself is confirmed.
- Set representative workload/environment budgets before comparing layouts. Record repeated-run throughput, p95/p99 latency, memory/allocations, lock wait/hold time, file/metadata operations, verified results, and errors for local disk, SMB, and NFS.
- Demonstrate complete record publication and cross-host reader lifetime across source-copy errors, cancellation, disk errors, conditional conflicts, process termination, and competing cleanup.
- Qualify Durable flush/publication ordering for content, metadata-only updates, and deletes, including references to previously buffered content. Verify explicit Buffered behavior, failure/indeterminate outcomes, and that no durable failure falls back silently. Process-crash tests alone do not establish power-loss durability.
- Verify shared initialization, same-key coordination, alias handling, stale-writer exclusion, and safe reacquisition using independent processes and two client hosts on both SMB and NFS.
- Run both provider contract suites on Windows against local disk/SMB and on Linux against local disk/SMB/NFS. Verify common format, key comparison, path confinement, and mixed Windows/Linux concurrency on the same SMB store.
- Exercise share-server restart and network interruption during publication, document indeterminate-outcome reconciliation, and verify retry behavior cannot replay an uncertain mutation unsafely.
- Verify integration with both existing retention workers/options, absent expiration, disabled hosted sweeping, cutoff/batch/delay bounds, confirmed result reporting, and provider capabilities. Exercise concurrent sweepers, expiration extension/clearing, crash-safe discovery, and active readers across hosts.
- Measure bounded candidate discovery, recovery, and deletion alongside foreground workloads. No retention path may rely on an unbounded public-client scan or a store-wide foreground mutation lock.
- Verify original-key identity, case behavior, Unicode, containment, format rejection, and corrupt-record handling on qualified local filesystems.
- Verify deterministic complete pagination on unchanged data, forward continuation under concurrent inserts/deletes/updates, unchanged token binding and document visibility cutoffs, and per-record committed-generation consistency without snapshot guarantees.
- Run existing provider contracts and new real-disk checks during implementation. No benchmarks or runtime correctness claims are made by this ADR.

## Related decisions

- [ADR-0030](0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md) establishes stream ownership and atomic blob upload expectations.
- [Filesystem provider requirements](../prd/storage/prd-0000-STORAGE-filesystem-blob-and-document-providers.md) define observable outcomes and readiness blockers.

## Execution sequencing revision

Subsequent user authorization permits full implementation independently of unavailable share environments. This changes task ordering, while preserving qualification requirements. The design is accepted and implemented. Remaining deployment matrix and performance gates are tracked separately from implementation status.

The implementation uses format version 2, immutable payloads, durable metadata/tombstones and per-key authority. Linux uses file fsync and containing-directory fsync, including creation ordering. Exclusive installation uses native atomic link and replacement uses native rename, with no copy or stat-then-rename fallback. Atomic hard-link support is a required Linux filesystem/server primitive. Windows uses write-through namespace moves for creation and a write-through source handle with POSIX FileRenameInfoEx replacement plus publication-handle flush. The filesystem provider reference links the native API contracts and records assumptions. Process restart and API success do not establish hardware/server power-loss persistence.

Shared ownership is a persistent atomically installed namespace marker. No lease revocation, age or local clock transfers ownership. An uncertain publisher retains the marker; a crashed owner blocks that key until every participant on every host is stopped and an operator reconciles the mutation before explicit recovery. This replaces the unsafe premise that a separate revocable lock can fence unchecked publication. Unsupported native replacement/persistence semantics fail closed. Local execution of the shared protocol is not SMB/NFS qualification.

Retention uses a persisted bounded virtual-radix traversal with wrap-around. Discovery never materializes the complete inventory. Expiry is rechecked under non-waiting key authority. Immutable handles protect old readers, separate generation ownership protects staged writers, and bounded cleanup physically reclaims obsolete content. Tombstones/key coordination metadata remain until an offline migration/compaction procedure; that procedure is not implemented. Query scans prune blob containers and document type/partition and enforce metadata bounds. Managed discovery bounds do not prove a fixed number of filesystem-server RPCs.

Both modes flush complete staged manifests. Buffered mode may leave namespace publication uncheckpointed, but reclamation must durably checkpoint the selected head before removing any possibly referenced older generation. Only a confirmed Durable publication in the same live authority scope can skip that checkpoint; a new provider configured Durable does not prove an old head was durable. This preserves recoverable complete records rather than trading atomicity for Buffered acknowledgement. Both providers and existing client/worker integration are implemented. Portable local tests and repeated local measurements are separate evidence. Windows SMB, Linux SMB/NFS, mixed clients, outage/server restart, power-loss and the full benchmark acceptance matrix remain unqualified. See [the implementation reference and operating procedure](../storage-filesystem-providers.md).
