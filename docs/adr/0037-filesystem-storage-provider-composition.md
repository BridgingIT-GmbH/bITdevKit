# ADR-0037: Use Direct File I/O for Filesystem Storage Providers

## Status

Proposed. Direct file I/O is agreed, and high performance on disk is an explicit requirement. Windows and Linux hosts must support local disks and file shares, including concurrent local processes and concurrent network clients using both SMB and NFS. Coordination/publication feasibility and performance budgets remain under review; no implementation is authorized by this document.

## Date

2026-11-01

## Context

Applications need filesystem implementations of `IBlobStoreProvider` and `IDocumentStoreProvider`. The [requirements](../prd/storage/prd-0000-STORAGE-filesystem-blob-and-document-providers.md) cover restart persistence, complete record publication, concurrency conditions, metadata, queries, and streaming blobs. The first release must support multiple local processes and multiple servers sharing a store, targeting both SMB and NFS.

Windows and Linux are both required application-host platforms for local disk and file shares. The requirements document defines the mandatory host/storage matrix. The persisted format, logical key semantics, and coordination identity must support compatible clients on either OS, including mixed-platform clients accessing one shared store. Physical path syntax and file-sharing primitives may differ behind the provider boundary.

The existing File Storage feature already supplies local file operations. Reusing it is attractive because it offers Result errors, streams, optional staged writes, and established registration. However, a blob/document record includes more state than one content file.

Current source establishes these boundaries:

- [`IFileStorageProvider`](../../src/Application.Storage/Files/IFileStorageProvider.cs) has file operations but no atomic compare-and-swap ETag contract, complete-record transaction, or explicit commit/abort operation.
- [`LocalFileStorageProvider`](../../src/Application.Storage/Files/LocalFileStorageProvider.cs) writes ordinary content with `FileMode.Create`. Its staged writable stream moves one temporary file into place on successful disposal. Its local metadata implementation exposes file length and modification time, not the complete blob/document metadata model.
- That local provider coordinates through instance-local semaphores. Its listing sorts and materializes all matching paths before offset paging with a fixed page size of 100.
- [`IDocumentStoreProvider`](../../src/Application.Storage/Documents/IDocumentStoreProvider.cs) assigns atomic persistence, ETags, expiration filtering, and native continuation state to providers. [`IBlobStoreProvider`](../../src/Application.Storage/Blobs/IBlobStoreProvider.cs) similarly requires blob-specific content and metadata operations.
- File Storage and both contracts already live in [`Application.Storage`](../../src/Application.Storage/Application.Storage.csproj). Composing File Storage does not currently add a separate package dependency.

## Proposed decision

Implement two filesystem providers using direct file I/O, preferring BCL `System.IO` where it can satisfy the qualified filesystem contracts. Share an internal persistence component for key-to-path mapping, shared-store initialization, cross-process/server mutation coordination, explicit publication/abort, and safe cleanup. BCL-only sufficiency is a feasibility question to prove, not a settled guarantee.

Do not take a lifetime exclusive lock on the whole root. Compatible application instances must be able to join it. Coordination must resolve the same store/key across machines and mount aliases. Per-key mutation coordination is preferred over serializing all unrelated writes behind one store-wide lock.

Keep blob semantics in the blob provider and document semantics in the document provider. Reuse existing clients for validation, typed document serialization, transforms, integrity checks, behaviors, and protected public continuation tokens.

Place production filesystem adapters in a proposed optional `Infrastructure.FileSystem` project referencing `Application.Storage`. Follow the current Infrastructure provider pattern rather than expanding production I/O in the Application layer. Do not move existing File Storage types as part of this feature.

Do not introduce a public `IFileSystem` abstraction or route Document Storage through Blob Storage. Extract internal utilities only when both providers need the same proven operation. This applies the Laziness Protocol principle to avoid speculative layers.

Direct I/O is the agreed implementation boundary, not an accepted on-disk format or a demonstrated performance improvement. The publication protocol and its measured costs remain prerequisite evidence.

## Performance requirements shaping the design

Both providers must qualify as high-performance disk storage under explicit workload and environment budgets from requirements decision R5. The choice of direct I/O allows control over these costs; removing an abstraction alone does not establish a measurable gain.

- Resolve exact logical keys to deterministic record paths. Keep directory enumeration, cleanup scans, and query-index maintenance that requires broad scans out of exact-key request paths.
- Coordinate mutations per key. Distinct keys must not share a store-wide mutation lock. Shared-format initialization can use separately scoped coordination.
- Stage and validate large uploads before taking exclusive publication authority, then re-read committed state and check conditions inside that authority. Shared staging/cleanup rules must protect those files before publication.
- Stream blobs with bounded transfer buffers. Account for existing integrity checks and avoid additional full-payload copies, rereads, or hash passes without a demonstrated need.
- Update properties through metadata publication without reading or rewriting unchanged payloads. Keep key-only listings independent of payload reads.
- Measure lock wait and hold time, file opens, metadata operations, flushes, payload passes, allocations, and network round trips. Separate small-record random I/O from large sequential transfers and same-key contention from unrelated-key concurrency.
- Evaluate sharded paths, buffer sizes, and bounded concurrency against measured bottlenecks. Any caching must preserve the required cross-host visibility; any index must preserve shared mutation and recovery semantics.
- Compare candidate layouts using the same correctness and durability settings. Network latency, disk bandwidth/IOPS, server metadata capacity, or lock contention may dominate even when provider CPU overhead is low.

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

A candidate layout uses immutable content generations and one replaceable manifest per logical key. The manifest holds original identity, generation reference, format version, and complete provider metadata.

1. Stage new content in the same managed filesystem, stream/hash it, and finish validation before publishing anything.
2. Acquire qualified cross-process/server authority for the key, re-read authoritative committed state, check create-only/ETag conditions, and publish the manifest through a qualified single-file replacement operation. Obsolete authority must not permit publication after another writer has acquired the key.
3. Readers acquire one committed generation and retain its lifetime while streaming across hosts. Coordinate the manifest-to-payload-open interval with cleanup. Metadata-only updates publish a new manifest referencing the same content.
4. Deletion removes the published reference under the same coordination rules. Cleanup removes abandoned or superseded generations only after proving they are not referenced, held by readers, or still staged by a live writer on any participant.

This is a candidate, not a guarantee that one `File.Move` call supplies all required durability or distributed exclusion. Same-filesystem staging, metadata/cache visibility, reader sharing, deletion, process termination, disconnect/reconnect, flush policy, and orphan cleanup must be verified on supported hosts and shares.

Writing `content.bin` and `metadata.json` independently into their final locations is insufficient because termination between writes can expose mismatched state. The new component must expose explicit publication and abort rather than treating disposal alone as proof that a source copy completed.

The BCL documents cross-volume moves as copy-and-delete and warns that copy-and-delete replacement is not atomic. The design must therefore avoid cross-volume publication and cannot substitute that sequence for a qualified commit operation. See [File.Move documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0). Buffer flushing is a separate concern; see [FileStream.Flush documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0).

## Shared-filesystem qualification

Both SMB and NFS are requirements on Windows and Linux, not interchangeable claims of support. Proposed baselines are SMB 3.x and NFSv4.1+. The exact client/server/runtime and mount configuration must be selected per host and published after validation. A proposed protocol baseline does not imply that every OS includes a compatible built-in client.

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

## Consequences

- Filesystem consistency belongs to one internal component rather than being split between a general file adapter and specialized record logic.
- Existing clients, other providers, and File Storage remain usable without adopting the new package.
- Direct I/O is not automatically safer. The implementation carries the verification cost for failures, path confinement, concurrent processes/servers, recovery, and stream lifetime. The shared component must not be assumed small until the distributed protocol is proven.
- Keys may map to hashed/encoded directories rather than friendly filenames. Original identities must be stored and verified to preserve semantics and detect mismatches.
- A file-backed document can hold transformed bytes; readable JSON is not a universal storage guarantee.
- Index-free prefix queries and full scans have costs proportional to inspected metadata. Bounded output does not imply bounded scan work. Capabilities must expose the actual query implementation.
- Windows and Linux support, local disks, and both same-machine multi-process and cross-server file-share access are first-release requirements. Filesystem qualification and real shared-access tests are release prerequisites.
- High performance is also a first-release requirement. A correct layout that misses the agreed latency/throughput budgets must be tuned or revised before release; a faster layout that weakens consistency is not an acceptable substitute.
- Network interruption can make an operation unavailable or its commit outcome indeterminate. Consistency takes priority over continuing writes without confirmed coordination.
- Deferred retention means expired physical records may continue consuming disk until explicitly deleted. Document logical expiration remains required.

## Validation required before acceptance

- Resolve the requirements document's protocol qualification and query decisions; the deployment choice itself is confirmed.
- Set representative workload/environment budgets before comparing layouts. Record repeated-run throughput, p95/p99 latency, memory/allocations, lock wait/hold time, file/metadata operations, verified results, and errors for local disk, SMB, and NFS.
- Demonstrate complete record publication and cross-host reader lifetime across source-copy errors, cancellation, disk errors, conditional conflicts, process termination, and competing cleanup.
- Verify shared initialization, same-key coordination, alias handling, stale-writer exclusion, and safe reacquisition using independent processes and two client hosts on both SMB and NFS.
- Run both provider contract suites on Windows and Linux against local disk and qualified shares. Verify common format, key comparison, path confinement, and mixed Windows/Linux concurrency on the same shared store.
- Exercise share-server restart and network interruption during publication, document indeterminate-outcome reconciliation, and verify retry behavior cannot replay an uncertain mutation unsafely.
- Verify original-key identity, case behavior, Unicode, containment, format rejection, and corrupt-record handling on qualified local filesystems.
- Run existing provider contracts and new real-disk checks during implementation. No benchmarks or runtime correctness claims are made by this ADR.

## Related decisions

- [ADR-0030](0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md) establishes stream ownership and atomic blob upload expectations.
- [Filesystem provider requirements](../prd/storage/prd-0000-STORAGE-filesystem-blob-and-document-providers.md) define observable outcomes and readiness blockers.
