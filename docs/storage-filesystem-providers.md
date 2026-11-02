# Filesystem storage providers

> Reference and operating procedures for the filesystem File Storage, Blob Storage and Document Storage providers.

[TOC]

## Implementation status

Implemented as of 2026-11-02, including the File Storage and monitoring migration. The implementation plan and requirements record code/tooling completion. Untested share, power-loss and full reference-performance acceptance remain in the separate [qualification checklist](storage-filesystem-qualification.md).

`Infrastructure.FileSystem` implements `IBlobStoreProvider`, `IDocumentStoreProvider` and ordinary-file `LocalFileStorageProvider` with direct file I/O. It also owns `LocalLocationHandler`. Existing client/provider contracts, transforms, behaviors, factories, health checks and retention workers remain in use.

## Ordinary File Storage and monitoring

Local File Storage and local monitoring have moved from Application into this package. Add its package reference and import `BridgingIT.DevKit.Infrastructure.FileSystem` for direct provider construction. Existing registration calls use extensions in `Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddFileStorage(factory => factory.RegisterProvider("reports", builder => builder.UseLocal("reports", root)));
services.AddFileMonitoring(builder => builder.UseLocal("inbound", root));
```

File Storage retains caller-selected paths and existing coordination semantics. It shares instrumented file opens, path helpers, native staged publication and operating-system error classification with the blob/document implementation. It does not use their hashed layout, root markers, TTL or authority protocol. Temporary writes use Buffered native replacement on successful stream disposal. Direct writes remain direct. Existing file errors are preserved alongside shared typed filesystem errors.

Each ordinary-file operation owns a lock lease. Timed-out or cancelled waits do not release another operation's lock. Entries remain shared until all holders and waiters return their leases. An open write stream retains its lease until disposal.

`FileSystemMetrics.Snapshot()` includes successful `fileReadOpens` and `fileWriteOpens` across ordinary files and payload files. Attempted opens and native publication counters are shared too. These are process observations, not measured syscalls or a transactional snapshot.

Application retains `IFileStorageProvider`, factories, behaviors, monitoring orchestration and provider-neutral `OpenWriteFileStream`. `Infrastructure.Windows` retains the Windows impersonation adapter and references this package. The shared package never references `Infrastructure.Windows`. Consumers must recompile after this assembly/namespace move.

For blob and document providers, code and local verification are separate from deployment qualification. Windows/Linux local operations are executable with default Durable acknowledgement. SMB and Linux NFS execute the shared ownership protocol. No SMB/NFS deployment, independent client-host recovery, server restart or power-loss configuration has been qualified. Windows NFS and other host OSes are unsupported. Declare the actual protocol; do not configure a mounted share as Local.

## Register the providers

```csharp
using BridgingIT.DevKit.Infrastructure.FileSystem;

var root = Path.Combine(storageBasePath, "catalog");
services.AddBlobStorage().WithFileSystemClient("catalog", root);
services.AddDocumentStorage().WithFileSystemClient<CatalogItem>(root);

public sealed class CatalogItem
{
    public string Name { get; set; }
}
```

`storageBasePath` is an absolute application-owned directory. Both providers can use one root. Root/access identity is immutable. Configure `AccessKind`, `Durability`, acquisition timeout and bounds through the optional `configureFileSystem` delegate. `CreateRoot=false` requires an existing compatible store. Initialization is checked on first access and cached per provider; applications must prevent external root replacement while running.

## Publication and durability

Format version 2 stores length-prefixed, ordinal UTF-8 logical identities through SHA-256 paths. Original identity remains in bounded metadata and is checked against the complete physical path. Blob paths prune by container; document paths prune by type and partition. Case, Unicode and logical path separators do not inherit OS filename comparison rules. Length prefixes are explicit big-endian integers; malformed Unicode identities are rejected before root access. Earlier experimental format-1 roots are rejected; there is no automatic migration.

Blob content streams through a pooled buffer into a unique immutable generation. The generation owner protects staging independently of the key publisher. Publication checks conditions again under per-key authority and atomically replaces `current.json`. Metadata changes retain the payload generation. Deletes publish minimal tombstones through the same commit protocol. They do not rely on durable unlink to prevent resurrection. Stable key coordination directories and tombstones remain on disk.

Durable is the default. Linux flushes file content, persists created directories through parent-directory fsync, publishes in the containing directory, then fsyncs that directory before success. Existing directory entries are also persisted before initialization use to cover a concurrent creator still completing its parent flush. No-replace installation requires native atomic `link`; source unlink is subsequent cleanup. Replacement uses native `rename`. Neither operation can fall back to copying or unchecked stat-then-rename. A Linux SMB/NFS filesystem lacking atomic hard-link support is unavailable for this protocol. A Durable metadata update flushes its referenced payload, including prior Buffered data. File fsync alone does not persist the directory entry. See [Linux fsync](https://www.man7.org/linux/man-pages/man2/fsync.2.html). The native install deliberately avoids [the .NET Move fallback paths](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs).

Windows uses `MoveFileExW(MOVEFILE_WRITE_THROUGH)` for new files/directories and a write-through source handle plus `SetFileInformationByHandle(FileRenameInfoEx)` with replace and POSIX semantics for replacement. It flushes the publication handle before Durable success. POSIX replacement preserves existing handles and avoids Win32 delete-pending replacement races. This requires a filesystem/server implementing these primitives. Unsupported operations fail clearly; there is no weaker replacement fallback. The persistence argument depends on the selected filesystem honoring write-through metadata semantics and the storage stack honoring flush. See [Windows write-through metadata](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-createfile2_extended_parameters), [rename flags](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information), and [MoveFileEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw). API success and process-restart tests do not prove host power-loss durability, controller behavior or remote stable storage.

Buffered explicitly permits loss of recent acknowledged metadata mutations after host failure. Payload staging, complete manifest files and directory initialization still use conservative persistence operations. Before removing superseded data, cleanup durably checkpoints the selected manifest unless this same authority scope already confirmed its Durable publication. It never infers that an older head is durable from the new provider configuration. Buffered overwrites/deletes that reclaim payloads can therefore pay Durable checkpoint costs; metadata-only Buffered updates without reclamation still permit namespace rollback. A failed checkpoint leaves old generations intact. It is not a promise of zero flushes or faster throughput.

## Public storage helpers

Both providers expose `Store` for trusted diagnostics, maintenance tools and explicit recovery. `FileSystemStore`, `FileSystemAuthority`, staged payloads, manifest and retention records, persistence helpers, scalar conversion, exception mapping and diagnostics are public and carry XML documentation with examples. Normal application calls use the blob or document client APIs.

`Store.Options` returns an independent configuration copy. Changing it does not reconfigure an active provider. `FileSystemStore.Json` is read-only. Authority handles and acquisition state remain private; acquire an authority object rather than constructing one. Low-level callers must use store-owned paths, hold matching live authority through publication, and follow each method's persistence preconditions. `Collect` normally uses its default `currentIsDurable: false`; setting it true requires a Durable publication confirmed within the same uninterrupted authority scope.

```csharp
var provider = new FileSystemBlobStoreProvider(options);
var configuration = provider.Store.Options;
var counters = FileSystemMetrics.Snapshot();
```

`Fault` invokes synchronous diagnostic callbacks within operations and can simulate failures. Leave it null for normal use. Writable instrumentation counters support isolated diagnostic resets through `Interlocked`; they never control storage correctness. Do not reset counters during a measurement. Public maintenance access does not establish global quiescence, qualify shared storage or make uncertain mutations safe to replay.

## Shared ownership and uncertain outcomes

Local access uses stable per-key exclusive file handles across cooperating processes. SMB/NFS uses a persistent namespace owner marker installed by atomic no-replace publication. Marker ownership does not expire and is not transferred when a network lock handle is revoked. Publication requires the live owner object; disposed authority cannot publish. Different keys have separate authority. Normal acquisition polls for up to five seconds by default, supports zero wait and cancellation. An outstanding native filesystem call may exceed that polling budget.

Existing blob/document retry behaviors recognise filesystem unavailable failures through the provider-neutral `ITransientStorageError.IsTransient` classification. Document authority acquisition timeouts also opt into retries because publication has not started. General document deadlines do not opt in. Retry budgets and blob upload stream replay restrictions still apply. Permission, capacity, configuration, corruption and metadata-limit errors remain outside this classification.

A failed/lost publication acknowledgement returns `BlobStoreIndeterminateOperationError` or `DocumentStoreIndeterminateOperationError` with mutation identity. Existing retry behaviors never replay these errors. Shared uncertain publication retains its owner marker and generation. Readers of metadata can inspect the current ETag/tombstone; mutations remain blocked. Never delete an aged owner, automatically take over after timeout, or replay an uncertain mutation. Recovery requires stopping every writer, reader, retention worker and recovery process on every host, reconciling the attempted mutation identity with `current.json`, draining or cancelling every old server operation/durable-session replay, then explicitly clearing retained ownership. Reconciliation must use fresh server-confirmed metadata, not an unverified disconnected client cache. If those conditions cannot be established, leave the owner marker in place.

The probe has an operator-only command after that quiescence and inspection:

```text
dotnet run --project tests/Infrastructure.FileSystem.Probes -c Release -- recover --root <absolute-store-root> --access Smb --quiescent true
```

`--quiescent true` also attests that old server operations are complete/cancelled and cannot replay. Recovery trusts that assertion; it cannot discover partitioned clients, drain remote server queues or reconcile mutation history. It retains committed records/tombstones, clears known owner markers and collects unreferenced data. Crashed shared owners sacrifice availability until this procedure. External modification of a live owned root, noncooperating writers, aliases accessing one root with different protocols, or automatic marker cleanup violate the protocol. Mixed Windows/Linux SMB and stale-client cache/reconnect semantics still need real server qualification.

## Queries, retention and reclamation

Queries keep at most Take+1 candidates in ordinal logical order and fail with a typed scan-bound error instead of returning an incomplete count. Metadata/key-only queries do not read payloads. Continuations retain existing client query/type/cutoff binding. Pages are live; concurrent changes can affect later pages. An opened record always uses one immutable generation.

Retention uses the existing blob/document hosted workers and options. A persisted virtual-radix traversal bounds discovery steps and manifest inspection per sweep, resumes across provider restarts and wraps to revisit busy keys, new records and changed expiration. It does not materialize the complete inventory or reject a store merely because inventory exceeds `MaxRetentionMetadataEntries`. A non-waiting cursor owner serializes sweep progress without a foreground store-wide lock. An uncertain shared checkpoint publication retains that cursor owner until explicit quiescent recovery. Expiration is rechecked under non-waiting key authority before tombstone publication. Clearing/extending expiration prevents stale discovery from deleting the record. Key authority is released before the configured delay between batches, so pacing does not keep a deleted key locked. OS directory enumeration may scan internally for a prefix; these managed work bounds are not a proven network-RPC or wall-time bound.

Mutation and visited-key sweep cleanup inspect bounded file entries and physically unlink superseded payloads, abandoned copy files and manifest staging. A generation owner prevents removal of live stages. Download opens occur under key authority; open handles use deletion sharing so old readers survive replace/delete and physical names can be reclaimed. Bytes remain allocated until the final OS handle closes. Uncertain shared generation owners remain for explicit quiescent recovery. No per-key in-process dictionary grows with historical keys. Physical key directories, tombstones and stable local authority files do accumulate with distinct keys; offline compaction/migration is not implemented. Many retained owners may delay bounded cleanup and need operator recovery.

Errors distinguish configuration/unsupported primitives, corruption, permissions, capacity, unavailable I/O, metadata bounds, ETag conflict, timeout and uncertain mutation. Roots must have application-only write access. All participating OS identities need compatible create, read, flush, delete and directory permissions; configure service identities/default ACLs through the OS, including NFS UID/GID mapping. Link checks reject symlinks/reparse points; they are not a sandbox against an attacker concurrently modifying root ancestors. Quiesce all participants before backing up the complete root; live backups are unqualified.

## Run local validation on Windows or Linux

Run commands sequentially from the repository root with .NET 10:

```text
dotnet test tests/Infrastructure.FileSystem.Tests/Infrastructure.FileSystem.Tests.csproj
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter FullyQualifiedName~Storage
dotnet run --project tests/Infrastructure.FileSystem.Probes -c Release -- local-probe
```

Local tests use `Path.GetTempPath()` and uniquely marked owned roots. Cleanup verifies exact ownership, containment and links. Windows/Linux applicability is decided at discovery; unsupported cases report skips before fixtures or share access. Common contracts run on both OSes. The focused project compile-links the same source tests and avoids unrelated Domain dependencies. Broad solution validation remains a separate check. Containers on one kernel are Linux runtime checks, not independent client hosts or network evidence. `local-probe` intentionally reports overall qualification unavailable even when its local cases pass.

## Enable share tests later

Copy `tests/Infrastructure.FileSystem.Probes/qualification.example.json` outside the repository and fill actual host/protocol/server/mount descriptors and disposable share scratch roots. Keep credentials in OS-managed storage. Set `BDK_FILESYSTEM_SHARE_TESTS=1` and `BDK_FILESYSTEM_QUALIFICATION_CONFIG` to the absolute external config. Run:

```text
dotnet test tests/Infrastructure.FileSystem.Tests/Infrastructure.FileSystem.Tests.csproj --filter Category=FileSystemShare
```

Without opt-in, discovery skips before share I/O. Enabled supported cases with missing, malformed or unmatched OS/protocol configuration fail. Scratch roots must already exist on observable SMB 3.x or Linux NFSv4.1+ filesystems. Qualification validates Windows remote protocol information or Linux filesystem type and mount version; a local/temp root declared as a share is refused before creating a workspace. Mount credentials/options are never copied into evidence. Windows NFS always skips. Single-host share races, ETag/readers and TTL cases are distinct from independent-host scenarios. The latter require the same `BDK_FILESYSTEM_SHARE_RUN_ID` GUID and `BDK_FILESYSTEM_SHARE_ROLE=controller` or `peer` on separately launched machines. Matching profiles use the same `environment.sharedProfileId` and actual machine names; launch both roles within 15 seconds. The controller verifies distinct OS machine identities and one create versus one conflict for both providers. Linux boot identities reject same-kernel containers. It also holds an independent peer reader through replace, metadata update and delete. Physical/VM placement, including Windows kernel independence, still needs external host qualification; machine names or container counts alone do not establish it. Missing peers fail. Marked share artifacts are retained for inspection.

Reconnect tests additionally require `BDK_FILESYSTEM_SHARE_FAULT_TESTS=1`, disposable infrastructure and `environment.faultBarrierRoot` outside the isolated storage path. Normal opt-in never restarts a server or changes a mount. The external driver consumes `<run>.request.json` at the before-publication boundary, isolates this client, and writes `<run>.outage-ready.json` with matching run/profile/peer and `status=isolated`. It heals independently of the blocked native call and writes `<run>.response.json` with matching identities, `status=healed`, `faultInjected=true`, matching `faultKind` and an existing absolute `protocolTracePath`. The test attempts publication during that outage, checks complete data/old reader behavior, and requires retained authority on uncertainty. A barrier alone does not prove an outage; inspect the independently collected trace. Separate `ServerRestart` cases request `faultKind=server-restart` from that preconfigured disposable driver; reconnect cases request `disconnect`. No destructive driver is supplied or invoked automatically.

## Measure local workloads and shared workloads

Build Release once, then run the executable without a concurrent build or competing benchmark:

```text
dotnet build tests/Infrastructure.FileSystem.Probes/Infrastructure.FileSystem.Probes.csproj -c Release
dotnet tests/Infrastructure.FileSystem.Probes/bin/Release/net10.0/BridgingIT.DevKit.Infrastructure.FileSystem.Probes.dll load --processes 2 --seconds 120 --warmup 30 --repetitions 5 --concurrency 4 --payload-bytes 65536
dotnet tests/Infrastructure.FileSystem.Probes/bin/Release/net10.0/BridgingIT.DevKit.Infrastructure.FileSystem.Probes.dll verify-results --run-id <printed-run-id>
```

The 17-case verified mix includes blob/document reads, writes, metadata, key-only queries/counts, typed compression, copy/move/multi-key helpers, same-key ETag contention, active retention and persistence-equivalent raw controls. Options select records, buffer-independent streamed payload sizes, concurrency and Durable/Buffered. `--document-bytes` independently selects raw document bytes and typed model ASCII bytes up to 1,040,000, leaving JSON overhead below the default typed limit. `--retention-convergence-seconds` declares the load case wait for resumable/busy sweeps (default 60); it is not a provider operation or release latency budget. Fixture/free-space checks run outside measurement. Artifacts record actual OS/access, process identities, successes, expected conflicts, errors, bytes, raw successful latencies, process-wide allocation/GC/memory and instrumented flush/move/discovery/authority counters. Counters are application observations, not measured syscalls/RPCs. Allocations include the runner and clients, not a claimed provider-only delta. p99 is omitted below 10,000 successful samples per case/repetition. Cache state is uncontrolled.

For genuine cross-host load, both hosts use external profiles and share opt-in. Launch `load --profile <profile-id> --role controller|peer --run-id <same-guid>` independently; actual machine identities and matching shared profile are enforced. Controller output indexes peer artifacts explicitly. Use `verify-results` against the exact controller run. Local multi-process load never counts as cross-host evidence. Existing BenchmarkDotNet `FileSystemStorageBenchmarks` also provides isolated read/write/metadata/document/key/retention operations with MemoryDiagnoser and both durability modes. Full cardinality/concurrency/size qualification and frozen comparative performance budgets remain pending; passing a local run does not pass those release gates.

## Related documentation

- [File Storage](features-storage-files.md)
- [Storage Monitoring](features-storage-monitoring.md)
- [Blob Storage](features-storage-blobs.md)
- [Document Storage](features-storage-documents.md)
- [Filesystem provider composition](adr/0037-filesystem-storage-provider-composition.md)
- [Shared filesystem infrastructure](adr/0038-shared-filesystem-infrastructure-for-file-storage.md)
