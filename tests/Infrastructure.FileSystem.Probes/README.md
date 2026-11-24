# Filesystem probes

This executable runs the existing blob and document storage probes and the advanced Application.Caching production probes. Run commands from the repository root. Each run writes evidence to `.audit/filesystem-storage/<run-id>/` and uses an isolated marked scratch directory.

## Run the production cache checks

Run ordinary typed, negative, binary, node-local source-sharing, and clear checks through `IApplicationCache` and the complete filesystem provider:

```powershell
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-contract
```

Terminate independent workers at real production publication boundaries:

```powershell
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-crash
```

The parent stops workers after durable credit reservation, payload staging, index staging, journal flush, and manifest publication. The probe verifies immutable pinned readers, unrelated-shard progress, retained uncertain credit, explicit offline recovery, and complete committed generations. Other cases verify exact and absent-key invalidation fences, a torn uncommitted journal tail, and checked authoritative-manifest corruption. Corrupt committed metadata keeps the root fenced until explicit reset. Recovery never selects older data to hide that damage.

The legacy `cache-prototype` command and `CacheCommitRecoveryPrototypeTests` remain available. They now call `FileSystemCacheStoreProvider` and `FileSystemCacheRecovery`; the duplicated experimental commit algorithm has been removed. The nine original case identifiers are preserved. Same-shard exclusion is asserted at boundaries where a native shard transaction actually holds authority. Early staging can coexist with other admitted work. Manifest publication holds the global quota guard, so its paused case checks retained checked control state and rejected takeover instead of claiming unrelated writes can pass that atomic boundary.

```powershell
dotnet test tests/Infrastructure.UnitTests --filter FullyQualifiedName~CacheCommitRecoveryPrototypeTests
dotnet test tests/Infrastructure.UnitTests --filter FullyQualifiedName~CacheProductionProbeTests
```

Scratch data remains available for inspection. The crash suite uses the persistent shared-marker protocol on local scratch storage. Passing it proves those local process-termination cases. It does not qualify a mounted share, another host, disconnects, reconnects, or power-loss durability.

Run the ordinary cold-key source-sharing contract across two and four independent local operating-system processes:

```powershell
dotnet test tests/Infrastructure.UnitTests --filter FullyQualifiedName~CacheMultiProcessTests
```

Each process makes four concurrent `GetOrLoadAsync` calls against one local filesystem root. Parent barriers keep source callbacks open until the selected sharing contract is observable. `SharedStore` peers first confirm their native interest in the owner's flight; the total source count must be one. `NodeLocal` keeps one source per process open before any publication, so its total must equal the process count. Every process must return the expected value, and the parent verifies distinct process IDs and each callback count. These checks use the `Local` profile and prove local process behavior only.

The same test group includes two-process authority outage, killed-owner replacement, strict hot validation, tracked filter/clear restart, old binary readers under shared quota, concurrent clear/publication fencing, unretained typed versus binary handoff, and owner-caller departure with remote interest. Each host uses ordinary Application Caching APIs and the native filesystem provider. The tests verify the actual child PIDs, source counts, retained versions, and complete byte delivery. Held source and native-interest barriers establish ordering without a latency threshold.

Run one contract directly with an isolated OS TEMP workspace:

```powershell
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --local true --contract hot-invalidation
```

Available contract names are `authority-outage`, `owner-loss`, `hot-invalidation`, `tracked-invalidation`, `tracked-clear`, `reader-quota`, `clear-publication`, `unretained-handoff`, and `source-departure`. Killed-owner and tracked-restart cases confirm actual process exit before advancing only test UTC validity by one minute; cancellation timers and actual work still use real elapsed time. The reader/quota case explicitly selects Reject eviction and a one-current-entry provider limit, while its old reader holds separately charged retired bytes. The local command requires no SMB/NFS profile. It does not test remote mounts, network recovery, or power loss.

## Run controller and peer cache contracts

The controller/peer command runs the same advanced contract actors on two hosts. Each host selects its native profile from the existing external qualification configuration. Both profiles use the same `environment.sharedProfileId`, point at the same share, and declare the actual controller/peer machine identities. Linux peers must also have distinct kernel boot identities. Set `BDK_FILESYSTEM_SHARE_TESTS=1` and `BDK_FILESYSTEM_QUALIFICATION_CONFIG` on both hosts. Credentials belong to the operating system, not this configuration.

Choose one fresh N-format GUID and start both roles from their repository roots:

```powershell
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --profile HOST_PROFILE --role controller --contract hot-invalidation --run-id RUN_ID
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --profile HOST_PROFILE --role peer --contract hot-invalidation --run-id RUN_ID
```

The nine advanced contract names above use one actor process on each host. `owner-loss`, `tracked-invalidation`, and `tracked-clear` terminate only the controller's owned child process and confirm its exit before the controlled lease-time advance. The shared-role coordinator verifies the actual actor host/PID pairs, results and callback counts. Run IDs cannot be reused, and both roles must select the same contract, process count, scope and authority protocol.

Use `cold-load` for two or four independent application processes. Four processes place two actors on each host. Both `SharedStore` and `NodeLocal` are explicit source scopes:

```powershell
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --profile HOST_PROFILE --role controller --contract cold-load --nodes 4 --scope SharedStore --run-id RUN_ID
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --profile HOST_PROFILE --role peer --contract cold-load --nodes 4 --scope SharedStore --run-id RUN_ID
```

Use `--local true` instead of `--profile HOST_PROFILE` on both roles to execute this role transport on one machine's OS TEMP directory. The ordinary test command exercises all nine advanced contracts and four source-scope/process-count combinations through this path:

```powershell
dotnet test tests/Infrastructure.UnitTests --filter FullyQualifiedName~CacheSharedRoleTests
```

Local transport evidence is labelled local. It does not qualify a share or another host. With share tests disabled, a requested shared run exits with status 2 before touching a share.

### Acknowledge an actual disconnect and restoration

Run both shared roles with `--contract disconnect-reconnect`. This case keeps both cache hosts alive and holds an expensive controller source across the external fault. The original source/wait budget is ten minutes; the overall protocol deadline is twelve minutes. The command never executes operating-system fault scripts.

Each role prints its local control directory and writes `armed.json` under OS TEMP at `bdk-filesystem-probes/RUN_ID/cache-fault-ROLE/`. Wait until **both hosts** are armed. Then apply the actual server/mount disconnection externally and place `disconnected.json` in each host's local control directory. Each acknowledgment contains only the matching run identity:

```json
{ "runId": "RUN_ID" }
```

The command requires actual shared-sentinel I/O failure or a bounded outstanding native read, plus a failed shared-cache acquisition with zero new source callbacks. An acknowledgment with an accessible share fails the case. Control files stay on local TEMP so disconnected clients can report progress.

Wait for `unavailable.checked.json` on both hosts. Restore the actual share externally, then place `restored.json` with the same run identity in each local control directory. The original outstanding native read must end. Both hosts verify the unchanged retained baseline, the peer establishes a fresh fenced publication, and the old held source must not obtain a publication version. Final reads must return the peer's retained value.

This case produces a passed result only after those actual observations. Local mode reports disconnect qualification unavailable. Process termination and mount disconnection do not establish power-loss durability; a physical/VM/server power-loss profile requires its own external execution evidence.

## Inspect and recover an advanced-cache root

Use the cache commands only for advanced cache format. The existing `recover` command belongs to blob and document storage.

1. Inspect the checked root identity and record its store ID:

   ```powershell
   dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-inspect --root "C:\scratch\cache-root" --access Local --durability Durable
   ```

2. Stop or independently fence every reader, writer, sweeper, and recovery process on every participating host. Wait for native operations that ignored caller cancellation. Drain delayed server operations and replays. A timeout or expired application lease does not establish this condition.

3. Pass the independently inspected store ID and explicit global-quiescence assertion:

   ```powershell
   dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-recover --root "C:\scratch\cache-root" --store-id INSPECTED_32_CHARACTER_ID --quiescent true --access Local --durability Durable
   ```

4. Inspect `cache-recovery.json`. `Completed=true` means newly constructed providers can reopen the verified root. Recovery advances the durable generation before cleanup, reconciles proven credit/publication outcomes, and verifies committed payload hashes. `RequiresReset=true` keeps the root unavailable and requires a separate explicit discard decision.

5. To discard the whole disposable cache after that decision, repeat the identity and quiescence checks and opt in to reset:

   ```powershell
   dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-reset --root "C:\scratch\cache-root" --store-id INSPECTED_32_CHARACTER_ID --quiescent true --reset true --access Local --durability Durable
   ```

   `cache-recover --reset true` invokes the same explicit reset API. Reset reports the discarded shard count and creates a new store identity. It is disabled unless `--reset true` is present.

6. Construct new provider instances and check readiness before reopening traffic. Earlier instances and continuations belong to the old durable recovery generation.

All offline commands require the actual access and durability profile. Defaults are `Local`, `Durable`, 10 GiB storage, and 10,000,000 entries. If the root uses different quotas, pass `--max-storage-bytes BYTES` and `--max-entries COUNT` to match its checked header. Do not change the configured root profile to bypass a mismatch. Exit code 0 means completion, 1 means a rejected or failed command, and 2 means unavailable qualification or a recovery outcome requiring explicit reset.

## Run an opt-in two-host share case

Use the existing external qualification configuration. It must name two distinct hosts, the mounted share, client/server/protocol versions, mount settings, and a common `environment.sharedProfileId`. Keep credentials in OS-managed access. The configuration rejects credential-bearing values.

Set `BDK_FILESYSTEM_SHARE_TESTS=1` and `BDK_FILESYSTEM_QUALIFICATION_CONFIG` on each host. Start the same run ID with the matching local profile and role:

```powershell
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --profile smb-controller --role controller --run-id SAME_32_CHARACTER_ID
dotnet run --project tests/Infrastructure.FileSystem.Probes -- cache-shared --profile smb-peer --role peer --run-id SAME_32_CHARACTER_ID
```

The existing native validator rejects a local directory masquerading as SMB or NFS. Role barriers check configured machine identities and reject same-kernel Linux containers. The real shared provider must confirm the controller's source authority and the peer's interest, retain one source result, and complete the peer without a duplicate source callback. Marked shared evidence remains available if either host fails or times out.

Without explicit opt-in, the command reports `unavailable` before accessing a share. The two-host case does not inject disconnects, reconnects, stale-server replay, or power loss. Those fault and placement profiles require separate controlled qualification on the actual client/server/mount/durability matrix. Never report a local or same-kernel run as evidence for those profiles.

Existing `qualify`, `local-probe`, `load`, `verify-results`, blob/document `recover`, and worker modes remain available.