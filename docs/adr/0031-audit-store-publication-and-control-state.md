# ADR-0031: Persist audit publication and lifecycle control state

## Status

Accepted. The additional technical control table was explicitly approved on 2026-10-30.

## Date

2026-10-30

## Context

Resumable audit extraction requires a committed prefix across independent writers. A position allocated before commit can appear below an already exported boundary. Retention must also invalidate affected windows and prevent delayed expired writes from restoring removed evidence. Store identity, epoch, progress, and availability must survive removal of the last execution.

The original layout specified three audit data tables. None provides an appropriate owner for durable control state after all captured records are removed. This decision explicitly adds one technical table while preserving those three data tables and their logical document representation.

## Decision

Add `__Auditing_StoreState` with a singleton key constrained to 1, stable StoreId, StoreEpoch, PublishedPosition, LifecycleGeneration, RemovedThroughPosition, RejectCompletedBeforeUtc, and ArchiveCompletedBeforeUtc. The archive watermark routes delayed finalized writes directly to the archive after maintenance has advanced its cutoff, including when no execution remains. One configured audit store owns one control row. Retention never deletes it. This is four physical tables in total. Control state contains no captured execution and is outside the document hash and integrity envelope.

Acquire a store-specific database transaction gate before reading control state. SQL Server uses transaction-owned `sp_getapplock`, PostgreSQL uses `pg_advisory_xact_lock`, and SQLite begins an immediate write transaction. Allocate position, append the entire graph, update progress, and commit under that gate. Extraction window creation and bounded batch validation coordinate with the same gate. Release database scopes before HTTP backpressure.

Archive and removal atomically update lifecycle and availability metadata. Fresh retained-only selections establish their current availability baseline. Earlier windows report affected gaps or membership changes rather than claiming successful completion. A restore that changes position meaning requires explicit epoch rotation before reopening producers/extraction. Automatic privileged restore detection is not promised.

Hosts own migrations, connection configuration, backups, restore procedures, and permissions. Runtime operations update rows rather than modify database schema.

## Alternatives

- Keep metadata on execution rows. Removing the last row removes its carrier; retaining a sentinel would invent an execution record.
- Use a pre-commit identity or local counter. Neither alone establishes a shared committed prefix or retains lifecycle/removal state.
- Change schema-level metadata on ordinary writes. Catalog annotations or view definitions require different backend mechanisms, runtime DDL permissions, and schema invalidation. No such mechanism was selected or proved suitable.
- Use an external metadata service. It adds a dependency and cannot share the audit database transaction without another coordination protocol.

## Consequences

The control row remains bounded and durable in an empty store. Complete graph publication, retention, and cursor validation share one transaction protocol. Deployment must include one additional technical table. Appends are serialized per configured audit store, which limits write throughput. The writer schedules bounded batches and gives each graph an independent transaction. Performance measurements and native backup/restore are required later gates. A control row or application lock does not authenticate records or detect privileged bypass.

## References

- [Auditing specification](../specs/spec-application-auditing.md), sections 10.4 and 12.6.
- [Microsoft application locks](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql).
- [PostgreSQL explicit locking](https://www.postgresql.org/docs/16/explicit-locking.html).
- [SQLite transactions](https://www.sqlite.org/lang_transaction.html).
