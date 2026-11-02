# Filesystem deployment qualification

Implementation status: **Implemented**, 2026-11-02.

Qualification status: **Pending for the profiles below**.

The filesystem providers, shared File Storage/monitoring infrastructure, retention integration, diagnostics, tests and probe tooling are implemented. This checklist tracks environment-specific acceptance separately from that implementation closure. An implemented provider or executable probe is not evidence that a deployment passed qualification.

## Remaining qualification

| Profile or check | Status | Required evidence |
| --- | --- | --- |
| Windows SMB and Linux SMB/NFS | Pending; no disposable shares available | Actual client/server versions, protocol and mount settings; independent-host create/update/delete races and stale-writer exclusion. |
| Mixed Windows/Linux SMB clients | Pending | Shared-store visibility, reader lifetime and conditional mutation behavior across both hosts. |
| Disconnect/reconnect, crashed ownership and server restart | Pending | Controlled failure/recovery runs, acknowledgement reconciliation and explicit all-host quiescence before operator recovery. |
| Storage power loss | Pending | Hardware/server persistence and complete-generation recovery evidence for Durable acknowledgement, distinct from process-kill tests. |
| Full reference performance matrix | Pending | Named local/share environments, raw-I/O controls with equivalent guarantees, numerical acceptance budgets, repeated size/cardinality/concurrency runs, allocation/GC data and measured contention. |

These checks retain the acceptance targets from the requirements and implementation plan. Record unavailable prerequisites explicitly. Do not change an unavailable result into a pass or declare a share Local to bypass protocol requirements. Windows NFS remains unsupported.

## Available verification

Windows and Linux local filesystem suites, local process/fault/recovery probes, repeated local load measurements and package checks were exercised during implementation. Their results cover their recorded environments and source revisions. They do not replace the pending profiles above.

The subsequent EF blob and dashboard verification passed 85 unit/model/read tests, 123 SQL Server/PostgreSQL/SQLite EF blob contract tests and 27 load-runner tests. Live EF blob dashboard reads at 16 workers completed 15 measured runs with zero errors and complete temporary-item cleanup. Those are application-environment EF measurements, not filesystem-share or bulk-read qualification.

## Run the follow-up checks

Use the existing qualification configuration and probe commands in the [filesystem provider guide](storage-filesystem-providers.md). Local tests use owned directories under the host OS temporary folder. Share tests remain disabled by default and skip host-inapplicable cases. Enable share/fault profiles only when their disposable roots and required peer hosts exist.

For each executed profile, retain the source revision, OS/client/server/mount configuration, run identifiers, command, exit status, case counts and artifacts. Keep execution evidence outside the documentation tree. Close a qualification item only after its required cases pass; preserve failed and unavailable results.

## Related documents

- [Provider requirements](prd/storage/prd-0000-STORAGE-filesystem-blob-and-document-providers.md)
- [Implementation plan](../plan/pln-feature-filesystem-storage-providers-1.md)
- [ADR-0037](adr/0037-filesystem-storage-provider-composition.md)
- [ADR-0038](adr/0038-shared-filesystem-infrastructure-for-file-storage.md)
