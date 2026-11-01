---
id: PRD-0000
title: Filesystem Blob and Document Providers
slice: STORAGE
status: Pending
ticket:
---

# Product Requirements: Filesystem Blob and Document Providers

## Overview

Applications need persistent Blob Storage and Document Storage on local disks and shared network filesystems without a database or separate coordination service. Existing clients and their public contracts remain the application boundary. The new providers persist content and provider metadata in files under configured roots.

This is a planning document. No provider or registration API described here exists yet. The proposed implementation boundary and alternatives are recorded in [ADR-0037](../../adr/0037-filesystem-storage-provider-composition.md).

Direct file I/O is the agreed implementation direction. High performance on disk is a first-release requirement for both providers, including local disks and the qualified SMB/NFS deployments. Direct I/O enables control over access patterns and locking; it is not itself evidence of a speedup.

## Scope

- In scope: filesystem implementations of `IBlobStoreProvider` and `IDocumentStoreProvider`, registration, persisted metadata, concurrency conditions, streaming blobs, supported queries, restart recovery, shared-access recovery, contract tests, and reproducible performance qualification.
- Confirmed first deployment: multiple application processes sharing a local disk directory and multiple servers sharing a network filesystem. Multiple writers are supported in both cases.
- Windows and Linux are required application-host platforms. Both must support local disk and file shares; neither platform is a later-release addition.
- Both SMB and NFS are first-release targets. Support is qualified for explicit protocol versions, client/server implementations, and mount configurations; it is not a promise for every share that accepts file operations.
- Proposed qualification baselines are SMB 3.x and NFSv4.1 or later. The exact tested combinations remain open in R1. Neither target is deferred to a later release.
- Out of scope for the first release: unqualified protocol/mount combinations, cloud object-storage mounts, manual editing of stored files, database indexes, cross-record transactions, and migration from existing providers.
- Physical expiration sweeping, blob leases, specialized batch-read/write interfaces, and a public general-purpose filesystem abstraction are deferred. Capabilities must report these limits accurately.
- Both providers must persist expiration metadata. Document logical expiration is required independently of physical sweeping. Blob expiration retains its existing contract meaning.

The required application-host/storage matrix is:

| Application host | Local disk | SMB file share | NFS file share |
| --- | --- | --- | --- |
| Windows | Required | Required | Required with a qualified client/mount configuration |
| Linux | Required | Required | Required with a qualified client/mount configuration |

These are requirements, not completed qualification results. Protocol/client versions must be selected per host; the proposed NFSv4.1+ baseline does not assert that every host includes a compatible built-in client. Roots use host-appropriate paths, including Windows UNC paths or mounted shares where supported. Provider behavior must not depend on a particular drive letter, separator, or the physical filesystem's case rules.

## User roles

- Application developer: selects a provider without changing calling code.
- Application operator: configures roots and permissions, restarts the host, and backs up persistent storage.

## Stories

### Story 1: Register isolated persistent stores

- Status: Pending
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

Notes:

- Dependencies: current client factories, options, registration lifetimes, and startup/health patterns.
- Risks: aliases, case differences, UNC paths, and mount points can refer to the same physical store. Coordination must use shared store/key identity rather than a machine-local root string.
- Proposed package placement is an optional `Infrastructure.FileSystem` project referencing `Application.Storage`, using BCL file APIs and existing repository utilities.

### Story 2: Persist and stream blobs

- Status: Pending
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

- Status: Pending
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

- Status: Pending
- Ready: No
- Ready Reason: Cross-process/server publication, reader lifetime, locking, and recovery require the R2 proof before implementation readiness.
- INVEST Check: E/S need the shared-access proof; split the implementation into coordination, publication, and recovery tasks before estimation.
- User Story: As an application developer, I want conditional mutations to preserve complete records so that concurrent calls cannot lose updates or expose partial state.

Acceptance Criteria:

1. Given two conditional mutations using the same current ETag, when both run concurrently through different processes or servers on a healthy qualified filesystem, then at most one succeeds and the other returns a conflict without changing the winner.
2. When a content write or properties/expiration update commits, then readers observe matching content and metadata from one generation, with the expected new ETag and timestamps.
3. Given a properties-only update, when it succeeds, then content bytes, content hashes, and creation time remain unchanged while modification time and ETag reflect the update. The provider does not read, rehash, or rewrite the unchanged payload.
4. Given a process termination or connection failure before or during publication, when the store becomes accessible, then each affected key resolves to the previous or new complete committed record. An unpublished new key remains absent, and temporary files never appear as records.
5. Given a conditional delete with a stale ETag, when deletion is attempted, then the current record remains intact; deletion of an already absent key follows the existing idempotent contract.

Data Requirements:

- Persisted format version, original logical identity, generation identifier, complete metadata, and an unambiguous publication boundary.
- Cleanup tracks uncommitted or superseded files without deleting a generation held by an active reader on any participating process or server.

Notes:

- Dependencies: filesystem-backed coordination across all participating processes and servers, stream lifecycle handling, and existing Result errors and cancellation conventions.
- Scope: complete-record consistency across process crashes and network interruption on qualified filesystems. Power-loss durability is a separate guarantee requiring an explicit flush/OS/storage policy.
- Cancellation observed before publication aborts the mutation. A completed commit must not be reported as rolled back because cancellation arrived afterwards. Loss of a commit acknowledgement is an indeterminate outcome, covered by Story 7, not proof of rollback.

### Story 5: Query persisted keys and metadata

- Status: Pending
- Ready: No
- Ready Reason: The query support matrix, scan limits, and continuation behavior under concurrent changes need R3.
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
- Exact matches target deterministic record paths. Prefix/full queries may inspect metadata and must advertise scan/filtering costs honestly.

Notes:

- Proposed first capabilities: efficient exact lookup; scan-based prefix matching and bounded key pages; document suffix/full scans only through existing explicit opt-ins; no native/server-side count claim.
- A page-size limit bounds returned records, not filesystem entries inspected. An unindexed scan may inspect O(N) metadata per page, and P pages may inspect O(P*N). This is a model, not a measurement.
- R3 must set memory and scan/cancellation bounds and specify whether concurrent changes have weak continuation semantics. Snapshot pagination is not assumed.

### Story 6: Confine storage and diagnose failures

- Status: Pending
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

- Status: Pending
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
- Configurable coordination wait policy and documented network I/O timeout limits.
- Original logical key and authoritative persisted generation/ETag are revalidated after acquiring or reacquiring coordination. Local wall-clock timestamps do not establish exclusive ownership.

Notes:

- Dependencies: qualified filesystem/server lock and publication semantics, compatible OS/mount settings, and shared-store permissions.
- Do not reclaim a lock by deleting its file after a local timestamp timeout. Reclaim/reconnect must prove exclusion of obsolete writers through the qualified server-enforced protocol or another verified publication mechanism.
- Protect the interval between reading a manifest and acquiring its payload against concurrent cleanup. An in-memory reader reference count is insufficient across hosts.
- Physical cleanup must coordinate with all writers/readers, including in-progress staging. The first release may conservatively retain uncertain generations rather than risk deleting live data; its disk-growth cost must be documented.
- Retrying behaviors must not automatically replay non-idempotent mutations after an indeterminate outcome. R2 must define error classification, reconciliation, and integration with existing retry behaviors.
- Test network interruption, share-server restart, client reconnect, process pause/resume, competing cleanup, and clock differences. Network unavailability may reduce availability; it must not weaken per-record consistency.

### Story 8: Qualify high-performance disk access

- Status: Pending
- Ready: No
- Ready Reason: R5 must fix representative workloads, reference environments, and numerical acceptance budgets. R2/R3 determine the consistency and query costs that those measurements must include.
- INVEST Check: E/T require the R5 budgets; split local and network qualification into independently runnable benchmark scenarios.
- User Story: As an application developer, I want efficient disk-backed blob and document operations so that choosing filesystem persistence does not introduce avoidable scans, copies, or contention.

Acceptance Criteria:

1. Given an exact-key get, exists, upsert, properties update, or delete, when the store grows, then the operation resolves its record directly without enumerating unrelated records. Tracing identifies the file/metadata operations performed per request.
2. Given writes to distinct keys and a held mutation lock on one key, when another key is mutated, then it does not wait for that key's lock or a store-wide mutation lock. Large uploads stage content outside the exclusive publication section, then revalidate conditions before committing.
3. Given a streaming blob operation, when payload size increases at fixed concurrency, then provider transfer-buffer memory remains bounded independently of payload size. Measurements account for every full payload pass required by integrity checks and do not add avoidable buffering, rereads, or rehashes.
4. Given a qualified local, SMB, or NFS configuration, when the agreed benchmark workload executes through existing clients with production consistency settings, then successful-operation throughput, p95/p99 latency, allocations/memory, and lock-wait time meet the R5 budgets; errors, conflicts, retries, and timeouts are reported separately.
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

## Proposed implementation sequence

1. Fix the R1 qualification matrix and R5 workload/budgets, then prove R2/R3 with disposable local/SMB/NFS experiments and contract-focused tests. Measure coordination/publication costs before freezing the layout. Use separate client processes and two independent client hosts for network tests. Record configurations and execution evidence outside the documentation tree.
2. Add the optional filesystem infrastructure package and the shared path/publication/coordination utilities proven necessary for the first blob implementation. Cross-process and cross-server safety are prerequisites, not a later retrofit.
3. Implement Blob Storage and pass the existing provider contract suite plus real-disk failure, restart, concurrent-process, and concurrent-server checks.
4. Implement Document Storage using the proven persistence component and pass equivalent contracts, transform, expiration, and query checks. Run both providers' contracts on Windows and Linux for local disk and qualified shares, including mixed Windows/Linux clients sharing one store.
5. Run reproducible performance qualification through the existing clients on each supported configuration. Tune measured bottlenecks without weakening concurrency, integrity, or durability semantics.
6. Add registration, health integration, fictional usage documentation, and the operating/backup limitations. No existing provider is migrated or changed as part of this work.

## Non-functional notes

- Compatibility: public client/provider signatures and behavior pipelines remain unchanged.
- Dependencies: no database or separate distributed coordination service in the proposed design. The shared filesystem server is the coordination authority. If its qualified semantics cannot meet the contract, revisit the design instead of weakening guarantees silently.
- Platforms: Windows and Linux support is mandatory for both providers on local disks and qualified SMB/NFS shares. Qualify every cell of the required matrix. Record OS/runtime, protocol/client version, server implementation, and mount/cache/locking settings. Include cross-platform shared-store compatibility and mixed-client concurrency. A startup probe is necessary validation but cannot prove all partition/recovery behavior.
- Performance: high performance is a release criterion, not only a future optimization. Streaming blob transfer memory does not grow with blob size; exact-key operations do not enumerate the store; unrelated keys do not share a mutation lock. Query/cleanup work must support cancellation and explicit bounds decided in R3. R5 sets the numerical throughput, latency, and memory budgets for each qualified environment before implementation readiness.
- Recovery: all mutation failure paths preserve a complete published record, but a lost commit acknowledgement can leave the caller uncertain whether a mutation committed. Power-loss durability and storage-controller guarantees are not implied by process-crash tests.
- Verification: test through existing clients as well as provider contracts, using actual temporary directories, independent processes, two network client hosts, and controlled disconnect/reconnect/server-restart experiments. No implementation tests were run during this planning task. Local tests alone cannot qualify shared-network support.

## Open decisions and Definition of Ready

- R1: Deployment scope is confirmed: Windows and Linux hosts, local disks and file shares, concurrent local processes and concurrent servers, with both SMB and NFS in the first release. Select the precise protocol/client/server/mount combinations per host and coordination wait policy. SMB 3.x and NFSv4.1+ are proposed qualification baselines, not already verified support or a promise of built-in client availability.
- R2: Select and prove shared coordination, stale-writer prevention, on-disk publication, cross-host reader/delete lifetime, crash/reconnect recovery, indeterminate-outcome reconciliation, retry classification, error mapping, metadata bounds, and supported format. Specify the power-loss durability claim separately. Identify whether BCL APIs suffice for each target before committing to BCL-only implementation.
- R3: Fix supported query shapes, scan/memory bounds, cancellation behavior, and continuation behavior during mutations. Confirm workload expectations before any throughput or scale claim.
- R4: Direct I/O is agreed. Confirm the optional `Infrastructure.FileSystem` package and provider naming. Proposed names are `FileSystemBlobStoreProvider` and `FileSystemDocumentStoreProvider`.
- R5: Define representative workloads and reference local/SMB/NFS environments, then set minimum successful-operation throughput, maximum p95/p99 latency, memory/allocation budgets, and acceptable contention limits. Set the measurement noise threshold and decision criteria before comparing design candidates. No measured baseline or numerical performance target exists yet.

The feature is not ready for full implementation until R1-R3 and R5 have concrete answers. Direct I/O is agreed; the ADR remains Proposed because the publication/coordination design and qualified support matrix are not yet accepted.
