---
id: PRD-0000
title: Filesystem Blob and Document Providers
slice: STORAGE
status: Implemented
ticket:
---

# Product Requirements: Filesystem Blob and Document Providers

## Overview

Applications need persistent Blob Storage and Document Storage on local disks and shared network filesystems without a database or separate coordination service. Existing clients and their public contracts remain the application boundary. The new providers persist content and provider metadata in files under configured roots.

Implementation is complete as of 2026-11-02. Story status records delivered code, integration and verification tooling. Deployment and full reference-performance acceptance remain separate in the [qualification checklist](../../storage-filesystem-qualification.md). The implemented boundary and alternatives are recorded in [ADR-0037](../../adr/0037-filesystem-storage-provider-composition.md).

Direct file I/O is the agreed implementation direction. High performance on disk is a first-release requirement for both providers, including local disks and the qualified SMB/NFS deployments. Direct I/O enables control over access patterns and locking; it is not itself evidence of a speedup.

## Scope

- In scope: filesystem implementations of `IBlobStoreProvider` and `IDocumentStoreProvider`, registration, persisted metadata, concurrency conditions, streaming blobs, supported queries, restart recovery, shared-access recovery, contract tests, and reproducible performance qualification.
- Confirmed first deployment: multiple application processes sharing a local disk directory and multiple servers sharing a network filesystem. Multiple writers are supported in both cases.
- Windows and Linux are required application-host platforms. Both must support local disk and file shares; neither platform is a later-release addition.
- SMB on Windows/Linux and NFS on Linux are first-release targets. Windows-to-NFS access is out of scope. Support is qualified for explicit protocol versions, client/server implementations, and mount configurations; it is not a promise for every share that accepts file operations.
- Proposed qualification baselines are SMB 3.x and NFSv4.1 or later. The exact tested combinations remain open in R1. Neither target is deferred to a later release.
- Out of scope for the first release: unqualified protocol/mount combinations, cloud object-storage mounts, manual editing of stored files, database indexes, cross-record transactions, and migration from existing providers.
- Optional TTL and physical expiration sweeping are required in the first release through the existing retention interfaces, options, and background services. No separate filesystem retention scheduler is introduced.
- Blob leases, specialized batch-read/write interfaces, and a public general-purpose filesystem abstraction are deferred. Capabilities must report these limits accurately. Internal filesystem coordination is still required for safe writes and sweeping; it does not imply support for the public blob lease feature.
- Both providers persist expiration metadata and implement provider-side sweeping. Document logical expiration is required independently of physical sweeping. Blobs remain readable until deleted, matching the existing blob contract.

The required application-host/storage matrix is:

| Application host | Local disk | SMB file share | NFS file share |
| --- | --- | --- | --- |
| Windows | Required | Required | Out of scope |
| Linux | Required | Required | Required with a qualified client/mount configuration |

These are requirements, not completed qualification results. Protocol/client versions must be selected per supported host/protocol combination. Mixed Windows/Linux clients share a store over SMB; Linux-only participants can use NFS. Roots use host-appropriate paths, including Windows UNC paths or mounted shares where supported. Provider behavior must not depend on a particular drive letter, separator, or the physical filesystem's case rules.

Each shared store uses one share protocol: SMB or NFS. Simultaneous SMB and NFS access to the same underlying store is out of scope. Separate stores may use different protocols. Local-only multi-process access remains supported. Document and validate compatible registration settings where observable; application code cannot reliably detect every externally configured mount or server-side alias.

## Agreed coordination wait policy

- Normal mutations use a maximum lock-acquisition wait of five seconds by default, configurable per store. Cancellation is honored during acquisition waits, and a caller can select a zero wait for fail-fast behavior.
- Exhausting the acquisition wait returns a typed busy/timeout failure before publication. The timeout never authorizes lock theft, deletion of another participant's coordination state, or uncoordinated I/O.
- The acquisition budget is not an upload-duration limit, a lock lease, or a guaranteed end-to-end network I/O deadline. Uninterruptible OS/share calls retain the documented limits from Story 7.
- Retention attempts to acquire each candidate's mutation authority without waiting behind a busy key, then skips and revisits busy candidates in a later sweep. Busy candidates must not prevent progress on other keys.
- Uploads stage content before acquiring publication authority. Conditions are revalidated after acquisition; a failed acquisition leaves the currently published record untouched and staged content eligible for safe cleanup.

## Agreed durability policy

- `Durable` acknowledgement is the default. A per-store `Buffered` mode is an explicit opt-in for workloads that accept losing recently acknowledged changes after a host or storage failure. These names describe planned policy, not existing configuration APIs.
- Durable success requires the qualified persistence protocol for both payload and committed metadata/publication state to complete before acknowledgement. This includes metadata-only changes and deletion. Flushing a payload file alone does not establish durable publication.
- Buffered mode may acknowledge changes accepted by the OS before durable persistence. Both modes retain the same key isolation, ETag conditions, complete-record publication, reader safety, and cross-process/server coordination rules.
- Durable acknowledgement must cover every payload referenced by the committed record, including content originally created by a buffered operation. The protocol cannot assume that an existing payload is already durable.
- A failed or uncertain durable commit never silently downgrades to buffered success. Lost acknowledgements remain indeterminate outcomes under Story 7.
- The exact flush/order/publication protocol and its power-loss guarantees must be qualified per supported filesystem/server configuration. Document any storage hardware/server assumptions. Benchmark both modes separately and use Durable for default-mode acceptance measurements.

## Agreed layout ownership

- Each provider owns its managed directory structure and physical filenames. Preserving original logical keys as human-readable filesystem paths is not required.
- Original logical keys remain available through the existing APIs and persisted record metadata. Physical encoding/hashing must preserve logical identity and verify the original keys rather than relying on host filename comparison rules.
- The root is a managed store, not a folder for external tools to rename, edit, or delete individual records. Operators use the documented whole-store backup/recovery procedures.
- Provider ownership permits sharded/encoded record paths and internal metadata, staging, and payload-generation files. The immutable-payload/manifest format remains a proposed implementation to qualify for atomicity, recovery, and performance; layout ownership does not itself validate that protocol.

## Agreed live pagination policy

- Listings and document query pages are live views. A continuation sequence does not freeze the store or retain historical record versions for a query snapshot.
- On an unchanged dataset, a forward continuation sequence returns every matching item once in the existing deterministic logical-key order. Continuation advances by logical ordering position rather than an offset into a changing directory listing.
- Concurrent inserts after the continuation position can appear on later pages; inserts at or before that position can be missed by that sequence. Concurrent deletes and updates can change later results. Restarting the query begins a new live traversal.
- Each returned record still represents one committed generation. Live pagination does not permit torn content/metadata or weaken exact-key concurrency rules. A multi-record page is not promised to represent one instant in time.
- Preserve existing public token protection and query binding. For documents, retain the visibility cutoff established for the page sequence under the existing contract; live data does not mean refreshing that cutoff for each page. There is no snapshot-consistent total count across pages.

## Agreed balanced workload and benchmark coverage

- Balanced support is required across small documents/blobs, large streamed blobs, metadata operations, supported queries, and retention. No workload class is treated as secondary or excluded to improve an aggregate score.
- Reproducible throughput, latency, allocation, GC, memory, and contention benchmarks are first-release deliverables. Benchmark coverage is agreed; reference-machine budgets still need measurement and are not claimed as achieved.
- Use the existing `benchmarks/Application.Benchmarks` BenchmarkDotNet project and its memory-diagnosis conventions for appropriate single-process scenarios. Add a complementary repeatable load runner for concurrent processes/hosts, long-lived mixed workloads, and network latency distributions. A microbenchmark does not replace shared-filesystem load qualification.
- Exercise the supported matrix: Windows local/SMB and Linux local/SMB/NFS, including mixed Windows/Linux SMB clients. Run Durable as the default acceptance mode; measure Buffered separately. Qualify local and network results independently.
- Cover every supported operation, including exact reads, exists, create/overwrite, ETag conditions, metadata updates, delete, prefix/key-only pages, supported opt-in scans/counts, and TTL discovery/deletion. Exercise existing client-level multi-key/copy/move helpers where applicable without introducing deferred provider-specific batch interfaces.

### Initial benchmark inputs

These are adjustable fixture defaults for designing the suite, not performance targets or storage limits:

- Documents: serialized payloads near 1, 16, 64, and 256 KiB, plus a separate legal payload near the configured maximum. Measure typed serialization/transforms separately from provider persistence.
- Blobs: 4 and 64 KiB, then 1, 16, and 128 MiB. Use non-seekable sources and incremental sink verification; large transfers must not rely on constructing payload-sized arrays in the measured runner.
- Store cardinality: 10,000 and 100,000 records as initial profiles; 1,000,000 small records as an explicit scale profile. Do not multiply the largest payloads by every cardinality. Calculate seed data and free-space requirements before execution.
- Concurrency: 1, 4, 16, and 64 in-flight operations as an initial sweep, within declared resource limits. Include independent keys, contended hot keys, multiple local processes, and two independent client hosts. Count successes, conflicts, timeouts, and retries separately.
- Operation mixes: isolated operations, read-heavy, write-heavy, and simultaneous mixed small/large workloads. Publish request-count and byte-volume distributions; balanced coverage does not imply equal operation counts produce equal I/O demand.
- Retention/query interference: no due items, a due backlog, active foreground writes, expiration extensions/clears, busy keys, and sustained pagination. Report cleanup progress and foreground latency together.
- Cache/feature settings: distinguish verified warm/cold conditions, plain payloads, and configured transforms. Keep diagnostics/behaviors and durability settings identical in each comparison; report realistic client-path overhead separately from provider-path cost.

### Allocation and measurement acceptance rules

- Report managed allocated bytes per completed operation, allocation rate at measured throughput, GC counts/pauses, steady and peak process memory, and provider transfer-buffer usage. State whether figures include clients, serialization, transforms, and the load generator; aggregate all participating client processes where applicable.
- For blobs, increasing payload size at fixed concurrency must not cause provider transfer buffers or avoidable managed allocations to scale with the whole payload. Account for the required integrity passes and exclude fixture construction from provider measurements.
- For documents, account for the serialized content and returned model allocations explicitly. Low allocation does not mean zero allocation; measure avoidable persistence overhead separately rather than hiding required payload materialization.
- Metadata-only and key-only operations must not allocate or read payload-sized content. Exact-key operations must not allocate collections proportional to total store cardinality. Sustained runs must detect unbounded growth in coordination, reader, cache, or cleanup state after operations and streams complete.
- Observe allocations across asynchronous work and all worker threads. A thread-local before/after counter around an awaited operation alone is not sufficient allocation evidence. Distinguish managed allocations from OS/server file caches and pooled/native memory.
- Run Release builds, validate actual bytes/results, consume returned streams, and include commit/flush completion in write timing. Keep setup/seeding outside the measured region and identify any fixture overhead intentionally included.
- Before comparing implementations, define the decision, equivalent settings, and a noise threshold. Repeat comparative runs at least five times with alternating order, report median/range and latency distributions, and profile the limiting resource separately. Fast failures, skipped work, or weaker durability do not count as throughput improvements.
- Use an equivalent direct-I/O control to characterize filesystem/commit costs and separate provider overhead. Existing EF providers can provide context but do not alone determine filesystem acceptance. Set numerical throughput/latency/allocation budgets on named reference configurations after initial baseline qualification, before optimization comparisons and release sign-off.

## User roles

- Application developer: selects a provider without changing calling code.
- Application operator: configures roots and permissions, restarts the host, and backs up persistent storage.

## Stories

### Story 1: Register isolated persistent stores

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Deployment scope is confirmed. Supported protocol/client/server combinations and shared initialization require R1/R2.
- INVEST Check: E needs the protocol qualification work in R1/R2.
- User Story: As an application developer, I want to configure named filesystem stores so that my application persists data without a database or separate coordination service.

Acceptance Criteria:

1. Given a supported Windows or Linux host using local disk or a qualified file share, when a named blob or typed document client selects the filesystem provider and an absolute root, then callers use the existing client and factory interfaces.
2. Given committed data, when the host restarts with the same configuration and supported format, then content, keys, and metadata remain available.
3. Given distinct configured store roots, when equal logical keys are written, then each store retains its own data. Document type identities also isolate records within a document store.
4. Given a missing root, when creation is enabled, then initialization creates it; when creation is disabled or access is denied, then initialization reports a typed failure without deleting existing data.
5. Given compatible processes or servers using the same root, when they initialize concurrently, then they join the same store without conflicting format creation; incompatible configuration or unsupported format returns a diagnosable failure without changing existing records.

Data Requirements:

- Absolute root path; explicit create-root setting; existing normalized client name; document type identity where applicable.
- Versioned root/record format. Unknown versions fail explicitly without reinterpretation or automatic migration.
- Shared store identity and layout/coordination version; every participant must use compatible rules.
- Blob and document data occupy distinct internal namespaces, even if one parent directory contains both stores.
- Persisted identity, metadata encoding, and logical key ordering are independent of the host OS. Compatible Windows and Linux clients sharing a store use the same format and coordination identity.
- Physical directories and filenames are provider-managed. Original logical keys are retained in metadata and exposed through existing APIs; a human-readable filename mapping is not a requirement.

Notes:

- Dependencies: current client factories, options, registration lifetimes, and startup/health patterns.
- Risks: aliases, case differences, UNC paths, and mount points can refer to the same physical store. Coordination must use shared store/key identity rather than a machine-local root string.
- Proposed package placement is an optional `Infrastructure.FileSystem` project referencing `Application.Storage`, using BCL file APIs and existing repository utilities.

### Story 2: Persist and stream blobs

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Depends on protocol qualification in R1 and shared publication/recovery proof in R2.
- INVEST Check: I/N/V/E/S/T pass; implement after shared persistence rules are settled.
- User Story: As an application developer, I want to upload and download blobs on disk so that binary content survives process restarts.

Acceptance Criteria:

1. When an upload succeeds, then a later download returns exactly the uploaded bytes and the persisted `BlobInfo` fields.
2. Given a non-seekable source, when it is uploaded, then the provider copies incrementally without buffering the entire blob or disposing the caller's stream.
3. Given a committed blob, when the source throws, cancellation occurs before commit, or the expected hash does not match, then the previous blob remains intact and no partial replacement becomes visible.
4. Given `FailIfExists`, when concurrent uploads target the same existing or initially absent key, then the existing blob is preserved or exactly one creation succeeds; competing creations return the existing conflict error semantics.
5. Given an open download, when another operation replaces or deletes its key, then the stream reads one committed generation to completion or reports the normal stream I/O error for a genuine disk failure. It never mixes generations.

Data Requirements:

- `BlobKey.Container`, `BlobKey.Name`, `Length`, optional `ContentType`, SHA-256 `ContentHash`, opaque `ETag`, UTC `CreatedAt` and `LastModifiedAt`, optional UTC `ExpiresAt`, and copied `PropertyBag` values.
- Key comparison and normalization follow existing Blob Storage rules rather than the host filesystem's filename comparison rules.

Notes:

- Dependencies: current blob client validation, hash handling, stream ownership, error types, and provider contract suite.
- Risks: disk exhaustion, interrupted source streams, reader lifetime during replacement across servers, network interruption, and platform-specific file sharing.
- Metadata-only updates and conditional deletion are covered by Story 4.

### Story 3: Persist typed documents and expiration

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Depends on R1/R2 and a fixed record metadata schema.
- INVEST Check: I/N/V/E/S/T pass; the provider persists bytes and does not implement typed serialization.
- User Story: As an application developer, I want typed documents persisted on disk so that existing document clients work without a database.

Acceptance Criteria:

1. When an upsert succeeds, then existing clients round-trip the value and `DocumentInfo`, including configured compression or encryption transforms.
2. Given two document type identities or partitions using the same row key, when each is written, then exact reads return the correct independent document.
3. Given an expiration due at the supplied visibility cutoff, when get, exists, find, key-list, or count executes, then the expired document is logically absent under the existing contract.
4. Given a physical expired record, when create-only, ETag, preserve-expiration, or delete operations execute, then they follow the existing physical-record rules until that record is deleted.
5. Given damaged metadata, an unsupported format, or a missing referenced payload, when the record is read, then a typed corruption/provider failure is returned without silently treating corruption as an absent record or overwriting it.

Data Requirements:

- Exact `DocumentTypeIdentity`, `DocumentKey.PartitionKey` and `RowKey`, serialized/transformed bytes, logical/stored hashes and lengths, transform descriptors, opaque ETag, UTC timestamps, expiration, and copied scalar properties from existing stored-document models.
- The provider stores supplied bytes unchanged. Files are not promised to be readable JSON when transforms are enabled.

Notes:

- Dependencies: `DocumentStoreClient<T>` remains responsible for serialization, transforms, validation, and logical integrity verification.
- Risks: lost transform descriptors or changed type identities make persisted records unreadable. Persist identity and metadata explicitly.

### Story 4: Preserve atomic record mutations

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Cross-process/server publication, reader lifetime, locking, and recovery require the R2 proof before implementation readiness.
- INVEST Check: E/S need the shared-access proof; split the implementation into coordination, publication, and recovery tasks before estimation.
- User Story: As an application developer, I want conditional mutations to preserve complete records so that concurrent calls cannot lose updates or expose partial state.

Acceptance Criteria:

1. Given two conditional mutations using the same current ETag, when both run concurrently through different processes or servers on a healthy qualified filesystem, then at most one succeeds and the other returns a conflict without changing the winner.
2. When a content write or properties/expiration update commits, then readers observe matching content and metadata from one generation, with the expected new ETag and timestamps. In Durable mode, success is acknowledged only after the qualified persistence protocol completes.
3. Given a properties-only update, when it succeeds, then content bytes, content hashes, and creation time remain unchanged while modification time and ETag reflect the update. The provider does not read, rehash, or rewrite the unchanged payload.
4. Given a process termination or connection failure before or during publication, when the store becomes accessible, then each affected key resolves to the previous or new complete committed record. An unpublished new key remains absent, and temporary files never appear as records.
5. Given a conditional delete with a stale ETag, when deletion is attempted, then the current record remains intact; deletion of an already absent key follows the existing idempotent contract.

Data Requirements:

- Persisted format version, original logical identity, generation identifier, complete metadata, and an unambiguous publication boundary.
- Cleanup tracks uncommitted or superseded files without deleting a generation held by an active reader on any participating process or server.

Notes:

- Dependencies: filesystem-backed coordination across all participating processes and servers, stream lifecycle handling, and existing Result errors and cancellation conventions.
- Scope: complete-record consistency across process crashes and network interruption on qualified filesystems in both modes. Durable mode additionally requires the qualified persistence protocol before success; Buffered mode explicitly permits loss of recent acknowledgements after host/storage failure. Exact power-loss guarantees and flush ordering still require qualification.
- Cancellation observed before publication aborts the mutation. A completed commit must not be reported as rolled back because cancellation arrived afterwards. Loss of a commit acknowledgement is an indeterminate outcome, covered by Story 7, not proof of rollback.

### Story 5: Query persisted keys and metadata

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Live pagination is agreed. The query support matrix, scan/memory limits, and concrete continuation/discovery implementation still need R3 proof.
- INVEST Check: E needs the R3 proof; no performance commitment is inferred from page size.
- User Story: As an application developer, I want supported listings and document queries so that filesystem stores remain usable through existing clients.

Acceptance Criteria:

1. Given an unchanged dataset, when supported listings are paged to completion, then they return deterministic logical-key order without omissions or duplicates and respect the requested page size.
2. When blob container/prefix listing or document exact/partition/row-prefix matching executes, then matching uses the existing logical-key comparison rules and excludes internal files.
3. Given a full scan or unsupported query shape without the required consent/capability, when it is requested, then existing validation returns a typed failure rather than silently scanning all data.
4. Given a supported document key-only listing, when it executes, then it reads metadata without reading stored payload bytes. Counts apply the same filters and visibility cutoff as matching queries.
5. Given an invalid, mismatched, or unsupported continuation token, when the next page is requested, then existing token validation rejects it without treating it as a new unfiltered query.

Data Requirements:

- Provider continuation state records a logical ordering position, never a caller-supplied filesystem path. Existing clients protect and bind public tokens.
- Pagination is live and advances in the existing logical-key order. Public document tokens retain the sequence's fixed visibility cutoff; they do not establish a frozen dataset.
- Exact matches target deterministic record paths. Prefix/full queries may inspect metadata and must advertise scan/filtering costs honestly.

Notes:

- Proposed first capabilities: efficient exact lookup; scan-based prefix matching and bounded key pages; document suffix/full scans only through existing explicit opt-ins; no native/server-side count claim.
- Both providers advertise expiration/retention support only with their implemented retention interface. For blobs, advertise expiration and retention sweep, not native backend lifecycle management. Document logical expiration remains enabled when hosted sweeping is disabled.
- A page-size limit bounds returned records, not filesystem entries inspected. An unindexed scan may inspect O(N) metadata per page, and P pages may inspect O(P*N). This is a model, not a measurement.
- Live continuation semantics are agreed as defined above. R3 must still set memory, metadata-scan, cancellation, and retention-discovery bounds and prove the forward traversal implementation. Offset paging over a changing directory listing does not satisfy the selected continuation design.

### Story 6: Confine storage and diagnose failures

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Depends on supported platforms and R2 path/format proof.
- INVEST Check: I/N/V/E/S/T pass for provider-owned directories without untrusted external filesystem mutation.
- User Story: As an application operator, I want filesystem failures and unsafe paths handled explicitly so that stored data stays isolated and failures are diagnosable.

Acceptance Criteria:

1. Given valid logical keys containing separators, Unicode, or case differences, when records are written, then key mapping preserves distinct identities on supported hosts without using keys directly as physical paths.
2. Given traversal attempts, absolute-path-like keys, or unsupported links/reparse points in managed directories, when storage is accessed, then no operation escapes the configured root.
3. Given disk-full, permission, sharing, or corrupt-record failures before publication, when an operation fails, then it returns the corresponding existing typed storage/provider error and preserves previously committed records. Uncertain publication outcomes follow Story 7.
4. Given abandoned temporary or superseded files, when bounded recovery/cleanup runs, then it removes only files proven unreferenced and leaves committed data and active readers intact.
5. Given incompatible root configuration or format, when health/initialization runs, then it reports the cause without logging payload bytes or automatically deleting data.

Data Requirements:

- Root marker and format version; encoded or hashed path components with original keys retained and checked; provider-owned temporary and generation filenames.
- UTC timestamps and all metadata needed for validation survive restarts.

Notes:

- Dependencies: OS/share permissions on provider-owned roots and qualified local/shared filesystems on Windows and Linux.
- Risks: hostile concurrent filesystem modification is outside the initial trust model. Every writer must follow the provider protocol; the provider does not replace operating-system access controls.
- Live-copy backup is not promised. The first documented backup procedure quiesces every participating writer and cleanup worker, then copies the complete root.

### Story 7: Coordinate shared access and survive network interruption

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: The shared coordination, stale-writer prevention, and indeterminate-outcome protocol require R1/R2 qualification on both SMB and NFS.
- INVEST Check: E/S need separate qualification tasks for each supported client/server combination. V/T are clear.
- User Story: As an application operator, I want multiple processes and servers to share a store safely so that deploying more application instances does not corrupt records or lose conditional updates.

Acceptance Criteria:

1. Given multiple compatible processes or servers, when they mutate one key, then create-only checks, ETag checks, and publication are coordinated through the shared filesystem. In-process semaphores alone cannot satisfy this criterion.
2. Given a successful committed mutation and no intervening mutation, when another healthy participant starts an exact read, then it observes that committed generation. Process-local caches cannot serve an older generation as current.
3. Given a crashed, disconnected, or paused writer whose lock has been lost, when another writer commits and the original resumes, then the original cannot publish using its obsolete authority or remove the new writer's coordination state.
4. Given lock contention or an unreachable share, when the configured coordination wait expires or cancellation is observed, then the operation reports a diagnosable contention/unavailable outcome and never falls back to uncoordinated I/O. OS calls that cannot be interrupted must not be advertised as strictly deadline-bounded.
5. Given a lost acknowledgement during publication, when commit success cannot be established safely, then the result identifies the outcome as indeterminate and leaves a complete record. The provider neither claims rollback nor blindly replays an overwrite; reconciliation follows the documented R2 procedure.

Data Requirements:

- Shared coordination identity per store/key, coordination format version, and operation/generation identity sufficient for the selected recovery procedure.
- Per-store lock-acquisition wait defaults to five seconds; zero enables fail-fast acquisition. Retention uses non-waiting candidate acquisition independently of the foreground default. Document network I/O timeout limits separately.
- Original logical key and authoritative persisted generation/ETag are revalidated after acquiring or reacquiring coordination. Local wall-clock timestamps do not establish exclusive ownership.

Notes:

- Dependencies: qualified filesystem/server lock and publication semantics, compatible OS/mount settings, and shared-store permissions.
- The agreed acquisition-wait policy applies across processes and hosts. Cancellation stops interruptible acquisition waits; no strict deadline is claimed for an OS call that cannot be interrupted.
- Do not reclaim a lock by deleting its file after a local timestamp timeout. Reclaim/reconnect must prove exclusion of obsolete writers through the qualified server-enforced protocol or another verified publication mechanism.
- Protect the interval between reading a manifest and acquiring its payload against concurrent cleanup. An in-memory reader reference count is insufficient across hosts.
- Physical cleanup must coordinate with all writers/readers, including in-progress staging. The first release may conservatively retain uncertain generations rather than risk deleting live data; its disk-growth cost must be documented.
- Retrying behaviors must not automatically replay non-idempotent mutations after an indeterminate outcome. R2 must define error classification, reconciliation, and integration with existing retry behaviors.
- Test network interruption, share-server restart, client reconnect, process pause/resume, competing cleanup, and clock differences. Network unavailability may reduce availability; it must not weaken per-record consistency.

### Story 8: Qualify high-performance disk access

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Balanced workload coverage and required performance/allocation benchmarks are agreed. R5 still needs named reference environments and baseline-informed numerical acceptance budgets. R2/R3 determine the consistency and query costs to include.
- INVEST Check: E/T require the R5 budgets; split provider microbenchmarks, end-to-end client runs, and local/network multi-process load qualification into independently runnable scenarios.
- User Story: As an application developer, I want benchmarked high-performance, low-allocation disk-backed blob and document operations so that a balanced workload does not incur avoidable scans, copies, contention, or garbage collection.

Acceptance Criteria:

1. Given an exact-key get, exists, upsert, properties update, or delete, when the store grows, then the operation resolves its record directly without enumerating unrelated records. Tracing identifies the file/metadata operations performed per request.
2. Given writes to distinct keys and a held mutation lock on one key, when another key is mutated, then it does not wait for that key's lock or a store-wide mutation lock. Large uploads stage content outside the exclusive publication section, then revalidate conditions before committing.
3. Given a streaming blob operation, when payload size increases at fixed concurrency, then provider transfer-buffer memory remains bounded independently of payload size. Measurements account for every full payload pass required by integrity checks and do not add avoidable buffering, rereads, or rehashes.
4. Given a qualified local, SMB, or NFS configuration, when the agreed balanced benchmark suite executes through existing clients with production consistency settings, then successful-operation throughput, p95/p99 latency, allocated bytes per operation, GC/memory, and lock-wait time meet the R5 budgets; errors, conflicts, retries, and timeouts are reported separately.
5. Given a claimed optimization, when equivalent before/after runs are repeated with verified content and equal durability, concurrency, and cache settings, then the claimed improvement exceeds the observed run-to-run variation and the report identifies the limiting resource. Faster failures or weaker consistency do not count as improvement.

Data Requirements:

- Workload definition: payload sizes/distribution, record count, key distribution, operation mix, client/process/server concurrency, and same-key contention ratio.
- Environment definition: OS/runtime, CPU/RAM, disk/filesystem, network capacity/latency, share server, protocol version, mount/cache settings, and commit/flush policy.
- Measurement output: verified successful operations and bytes, elapsed time, latency distributions, errors/conflicts/retries/timeouts, peak memory/allocations, lock wait/hold time, and file/metadata operation counts. State metric scope, such as per process or aggregate across client hosts.
- Capture run count, median/range across runs, warm/cold cache conditions, and the measured bottleneck. Store execution evidence outside the documentation tree.

Notes:

- Exercise small-record random access, large sequential blob transfer, metadata-only operations, queries, independent-key concurrency, and contended same-key mutations separately. Report local disk, SMB, and NFS independently.
- Compare provider-level cost with end-to-end client cost to distinguish persistence overhead from serialization, transforms, and integrity verification. A raw-file baseline uses equivalent publication/durability rules; a plain unsafe overwrite is not an equivalent baseline.
- Profile filesystem opens, metadata reads, flushes, lock operations, and network round trips. Bounded record reads do not imply constant latency as directory sizes or network contention grow.
- Consider sharded record directories, incremental copying, bounded concurrency, and tuned I/O buffers as design candidates. Confirm their effects by measurement rather than treating them as automatic optimizations.
- Keep listing/cleanup off exact-key request paths. Any query or cleanup index must preserve the shared-write and recovery contract and requires an explicit design decision.
- Specialized batch APIs remain deferred. If R5 identifies multi-key throughput as a primary workload, revisit that scope before finalizing performance claims.

### Story 9: Reuse existing TTL and retention cleanup

- Status: Implemented
- Qualification: Available local checks are recorded in the execution evidence; outstanding environment and performance gates are tracked in the [qualification checklist](../../storage-filesystem-qualification.md).
- Ready: No
- Ready Reason: Retention scope and reuse are agreed. R2 must prove recovery and concurrent expiration updates; R3/R5 must bound discovery/deletion work and its foreground performance impact.
- INVEST Check: V/T are clear. E/S require the expiration-candidate layout and shared-cleanup proof; implement provider adapters separately after that proof.
- User Story: As an application operator, I want filesystem stores to use existing optional expiration and retention settings so that due records are deleted without deploying another scheduler.

Acceptance Criteria:

1. Given the standard Blob Storage or Document Storage registration with hosted retention enabled, when a due item is swept, then the existing background service invokes the filesystem provider through its existing retention interface and removes the expired record without a filesystem-specific scheduler.
2. Given no expiration on an item, when retention sweeps run, then the item remains; given hosted retention disabled, when the expiration passes, then hosted physical cleanup does not run. Document reads still honor their visibility cutoff, while blob reads retain their existing semantics until physical deletion.
3. Given configured batch size, maximum batches, and inter-batch delay, when a sweep runs, then it honors the existing request bounds, applies the documented bounded candidate-discovery policy, and supports cancellation without enumerating via public client listing APIs or loading payloads to find expired records.
4. Given concurrent sweepers or a writer extending/clearing expiration, when a candidate is processed, then the provider rechecks the authoritative generation and expiration under shared mutation coordination before deletion. Already-deleted records are idempotent outcomes, and an updated record is not deleted from stale candidate information.
5. Given a process crash, share interruption, or restart during cleanup, when access recovers and sweeps continue, then recoverable due records are revisited without unsafe replay, committed non-expired records and active readers remain safe, and returned counts/deleted keys include only confirmed deletions. Indeterminate outcomes follow Story 7.

Data Requirements:

- Reuse `BlobUpload.ExpiresAt`, `BlobPropertiesUpdate.ExpiresAt`, document `ExpirationChange`, persisted UTC expiration, and the existing retention request/result types.
- Preserve each request's inclusive cutoff, named blob store or typed document namespace, batch bounds, and diagnostics fields.
- Return confirmed `DeletedKeys` and counts so existing diagnostics and permalink change processing continue to work. Blob results also preserve existing skipped/batch reporting; document results preserve `HasMore` semantics.
- Expiration candidate metadata/checkpoints are internal filesystem state. Authoritative committed record metadata determines eligibility; stale candidate entries never authorize deletion.

Notes:

- Dependencies: `IBlobStoreRetentionProvider`, `IDocumentStoreRetentionProvider`, `StorageRetentionOptions`, `BlobRetentionBackgroundService`, and `DocumentRetentionBackgroundService`. Existing named/keyed registration exposes providers to the workers.
- Keep existing defaults and configuration, including `Enabled`, startup delay, sweep interval, batch limits, delay, and stop timeout. Expiration is a deletion eligibility time, not a guarantee of deletion at that exact instant.
- Prove a bounded provider-side discovery path, such as expiration buckets or a resumable metadata traversal, before selecting the persisted layout. Deletion batch limits alone do not bound the cost of discovering due records. Do not perform an unbounded full-store scan every sweep.
- Candidate maintenance and recovery must not permanently miss an item after an expiration update or crash. Measure discovery, candidate repair, and deletion costs with R5's foreground workloads on local disk, SMB, and NFS.
- Coordinate deletion and later content reclamation with readers on all hosts. Removing a published record does not permit deleting files still required by an active download.
- Skip busy candidates without waiting for the foreground five-second acquisition budget. Revisit them in later sweeps without starving unrelated candidates, and preserve each existing result type's progress/skipped semantics.
- Keep physical expiration cleanup separate in meaning from abandoned-generation cleanup, while sharing the proven coordination/reclamation utilities. Do not add another hosted worker for either as part of this plan.

## Implementation sequence

1. Fix the R1 qualification matrix and R5 workload/budgets, then prove R2/R3 with disposable local/SMB/NFS experiments and contract-focused tests, including bounded expiration discovery and concurrent deletion. Measure coordination/publication and retention costs before freezing the layout. Use separate client processes and two independent client hosts for network tests. Record configurations and execution evidence outside the documentation tree.
2. Add the optional filesystem infrastructure package and the shared path/publication/coordination utilities proven necessary for the first blob implementation. Cross-process and cross-server safety are prerequisites, not a later retrofit.
3. Implement Blob Storage and pass the existing provider contract suite plus real-disk failure, restart, concurrent-process, and concurrent-server checks.
4. Implement Document Storage using the proven persistence component and pass equivalent contracts, transform, expiration, and query checks. Run both providers' contracts on Windows and Linux for local disk and qualified shares, including mixed Windows/Linux clients sharing one store.
5. Implement `IBlobStoreRetentionProvider` and `IDocumentStoreRetentionProvider` using existing hosted retention services/options. Verify optional expiration, disabled sweeping, batch bounds, candidate recovery, concurrent sweepers, expiration extension/clearing, and active-reader safety across Windows/Linux local disks and qualified shares.
6. Run reproducible performance qualification through the existing clients on each supported configuration, with retention active as well as idle. Tune measured bottlenecks without weakening concurrency, integrity, or durability semantics.
7. Add registration, health integration, fictional usage documentation, and the operating/backup limitations. No existing provider is migrated or changed as part of this work.

## Non-functional notes

- Compatibility: public client/provider signatures and behavior pipelines remain unchanged.
- Dependencies: no database or separate distributed coordination service in the proposed design. The shared filesystem server is the coordination authority. If its qualified semantics cannot meet the contract, revisit the design instead of weakening guarantees silently.
- Platforms: Windows supports local disks and qualified SMB shares; Linux supports local disks and qualified SMB/NFS shares. Qualify every required cell of the matrix. Record OS/runtime, protocol/client version, server implementation, and mount/cache/locking settings. Include cross-platform shared-store compatibility and mixed Windows/Linux concurrency over SMB. A startup probe is necessary validation but cannot prove all partition/recovery behavior.
- Performance: high performance is a release criterion, not only a future optimization. Streaming blob transfer memory does not grow with blob size; exact-key operations do not enumerate the store; unrelated keys do not share a mutation lock. Query/cleanup work must support cancellation and explicit bounds decided in R3. R5 sets the numerical throughput, latency, and memory budgets for each qualified environment before implementation readiness.
- Recovery: all mutation failure paths preserve a complete published record, but a lost commit acknowledgement can leave the caller uncertain whether a mutation committed. Durable acknowledgement is the default; Buffered is a per-store opt-in with the same atomicity/locking rules. Power-loss durability and storage-controller guarantees must be qualified separately from process-crash tests.
- Retention: reuse the existing hosted scheduling and batch configuration. Provider-side discovery and deletion must be bounded, safe across nodes, recoverable, and included in performance qualification. No item is automatically expired unless expiration is configured.
- Verification: test through existing clients as well as provider contracts, using actual temporary directories, independent processes, two network client hosts, and controlled disconnect/reconnect/server-restart experiments. No implementation tests were run during this planning task. Local tests alone cannot qualify shared-network support.

## Agreed decisions and remaining qualification

- R1: Host/protocol scope, one-protocol-per-store access, and acquisition-wait policy are agreed: Windows uses local disk or SMB; Linux uses local disk, SMB, or NFS. Windows NFS and concurrent SMB/NFS access to one store are out of scope. Concurrent local processes and concurrent servers are required, including mixed Windows/Linux clients over SMB. Normal mutations wait up to five seconds for authority by default, configurable per store, with cancellation and typed busy/timeout failures; retention skips busy keys and revisits them later. Remaining qualification work is to select/test the precise protocol/client/server/mount combinations. SMB 3.x and NFSv4.1+ on Linux are proposed baselines, not already verified support.
- R2: Acknowledgement policy and layout ownership are agreed: Durable by default, Buffered as an explicit per-store opt-in, with identical atomicity and coordination semantics; providers own physical directories/filenames, and original logical keys remain in APIs/metadata. Select and prove shared coordination, stale-writer prevention, on-disk publication, cross-host reader/delete lifetime, crash/reconnect recovery, indeterminate-outcome reconciliation, retry classification, error mapping, metadata bounds, and supported format. Include expiration candidate maintenance/recovery and concurrent expiration changes during sweeping. Prove flush/publication ordering and specify each target's power-loss durability claim, including previously buffered content. Identify whether BCL APIs suffice for each target before committing to BCL-only implementation.
- R3: Live pagination is agreed, with deterministic logical-key ordering and forward-position continuation rather than directory offsets. Concurrent changes can affect later pages; unchanged datasets return every match once. Preserve existing public token binding and document visibility cutoffs without a frozen dataset or snapshot total count. Fix supported query shapes, scan/memory bounds, cancellation behavior, and the concrete traversal implementation. Define bounded retention candidate discovery and progress across sweeps, independently of public query scans. Confirm workload expectations before any throughput or scale claim.
- R4: Direct I/O is implemented in the optional `Infrastructure.FileSystem` package through `FileSystemBlobStoreProvider` and `FileSystemDocumentStoreProvider`.
- R5: Balanced workload support and mandatory high-performance/low-allocation benchmarks are agreed across all supported operations and host/storage combinations. Use the initial input matrix and measurement rules above. Select named reference local/SMB/NFS environments and establish baselines, then set minimum successful-operation throughput, maximum p95/p99 latency, allocated-bytes/GC/memory budgets, and acceptable contention limits before optimization comparisons and release sign-off. Set the measurement noise threshold and decision criteria before comparing design candidates. At specification time no benchmark had run. Current local measurement state is recorded in the execution handover; full reference acceptance remains pending.

The agreed policies are implemented. Deployment qualification remains incomplete under this PRD. R1 needs concrete environment qualification, R2 needs remote and hardware proof of the implemented persistence/coordination candidate, R3 needs full matrix validation of the implemented query/discovery bounds, and R5 needs reference environments and baseline-informed acceptance budgets. These are follow-up qualification requirements, not requests to reapprove the agreed product policies. ADR-0037 is accepted and implemented; see the implementation state below and the separate qualification checklist.

## Implementation state

The user explicitly authorized full implementation and revised sequencing so unavailable shares do not block code. Both providers, native Durable/Buffered publication, durable deletion tombstones, persistent shared ownership, bounded resumable retention, reader/stage-safe reclamation, existing worker/registration integration and portable tests are implemented. See [the filesystem provider reference](../../storage-filesystem-providers.md) for the actual format-2 primitives, operating procedures and remaining limits. The implementation consolidates proposed helper types without changing public client/provider signatures.

Code completion, available local verification and deployment qualification are separate states. The plan and its execution handover record actual commands, failed candidates and measurements. The earlier statements in this specification describing unrun planning work are historical; current evidence is maintained in that execution trail. Shared-owner recovery requires explicit all-host quiescence, reconciliation and operator action, never automatic timed takeover. A server lacking required namespace/persistence operations fails clearly. These constraints preserve safety at the cost of availability after a crashed/uncertain owner.

The stories and implementation plan are Implemented. This records delivery of the providers, integration, retention, diagnostics and verification tooling; it does not assert that every deployment/performance acceptance criterion has passed. Windows SMB, Linux SMB/NFS, mixed-client host behavior, reconnect/server restart, storage power-loss and full reference performance acceptance remain pending in the [qualification checklist](../../storage-filesystem-qualification.md). Share tests have executable opt-in bodies and are inactive by default. No remote pass is fabricated. ADR-0037 is accepted and implemented.
