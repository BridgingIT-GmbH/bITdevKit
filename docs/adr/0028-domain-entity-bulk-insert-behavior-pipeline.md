# ADR-0028: Expose Entity Bulk Insert as a Domain Capability with Explicit Behavior Compatibility

## Status

Superseded by [ADR-0029](0029-independent-entity-bulk-inserter-decorator-behaviors.md)

## Date

2026-07-21

## Context

The provider-strategy design in [ADR-0027](0027-provider-strategy-for-entity-bulk-insert.md) separated reusable Entity Framework mapping from provider-native database writes. Its application-facing `IEntityBulkInserter<TEntity>` contract remained in `Infrastructure.EntityFramework`, however, so an application command handler had to reference an infrastructure package to request an operation that is conceptually a persistence capability.

The native path also bypasses every configured repository decorator and Entity Framework `SaveChanges` interceptor. That is appropriate for some query-only behaviors but unsafe for write semantics such as current-user audit state, domain-event creation, and outbox persistence. A consumer cannot determine from `.WithBulkInsert()` which configured semantics are preserved. Silent fallback to `InsertSetAsync` would avoid some correctness gaps but would unexpectedly change performance, tracking, and generated-value behavior.

The bulk-insert feature is preview and has no consumers as of this decision. A clean public-contract move is therefore preferable to maintaining two interfaces or a compatibility shim. Native SQL Server throughput, provider isolation, and one native write operation per input batch remain requirements.

## Decision

Adopt these boundaries and execution rules:

- `BridgingIT.DevKit.Domain.Repositories` owns `IEntityBulkInserter<TEntity>`. The contract exposes only entities, cancellation, and `Result<long>`; it contains no Entity Framework, SQL Server, provider, mapping, option, or transaction types.
- Delete the preview infrastructure interface in the same change. Do not add an obsolete forwarder, type forwarder, namespace alias, or second DI registration.
- Keep bulk insert separate from `IGenericRepository<TEntity>` as an optional capability registered through `.WithBulkInsert()`.
- Keep provider dispatch, EF metadata mapping, behavior adapters, transaction orchestration, and pipeline contracts in `Infrastructure.EntityFramework`; keep `SqlBulkCopy` and SQL Server options in `Infrastructure.EntityFramework.SqlServer`.
- Record repository behavior descriptors in configuration order. Before entering the native path, classify every configured behavior and relevant EF interceptor as native-equivalent, insert-irrelevant, explicit repository fallback, or rejected.
- Default unsupported semantics to failure. Permit fallback to the fully decorated `IGenericRepository<TEntity>.InsertSetAsync` only when explicitly configured, and expose that execution mode in results and observability.
- Implement native-equivalent audit, cancellation, concurrency, domain-event creation/metrics, outbox, logging, metrics, and tracing through a dedicated ordered bulk-behavior pipeline. Unknown custom write behaviors are never silently ignored.
- Limit the first native mapping contract to one root table, including same-table non-JSON owned references. Reject populated aggregate graphs, owned collections, separate-table/JSON ownership, and multi-table inheritance before database work.
- Execute all SQL Server batches and transactional postbehaviors inside one EF transaction owned by the dispatcher or supplied by the caller. The provider does not use per-batch internal transactions.
- Leave native entities detached and do not hydrate database-generated values.

## Rationale

A Domain-owned capability lets Application code depend on the operation without depending on its EF implementation. A separate optional capability preserves the distinction between tracked aggregate persistence and high-throughput root-table ingestion. Explicit classification makes correctness and performance choices visible, while a dedicated pipeline can reproduce compatible in-memory and transactional behavior without invoking one repository insert per entity.

The existing provider strategy remains useful: it confines native APIs and provider options to provider packages. This ADR changes public ownership and behavior/transaction guarantees, not the provider-selection boundary.

## Consequences

### Positive

- Application handlers inject a Domain contract without referencing Entity Framework or SQL Server packages.
- Exactly one public bulk-insert contract and DI registration exist.
- Configured write semantics are executed, explicitly classified as irrelevant, explicitly routed to fallback, or rejected before insertion.
- Native SQL Server performance remains based on one `SqlBulkCopy.WriteToServerAsync` operation rather than per-entity repository calls.
- One external EF transaction prevents partial completion across native batches and can include outbox rows.
- Provider-specific dependencies and options remain isolated.

### Negative

- The namespace/type move is source- and binary-breaking, which is accepted because the preview feature has no consumers.
- Repository configuration requires additional provider-neutral behavior metadata.
- Some existing custom decorators and interceptors require an adapter, explicit fallback, or rejection.
- Native insertion intentionally does not provide tracking, graph cascades, or generated-value hydration.
- Compatibility analysis adds setup and per-operation preprocessing before the native write.

### Neutral

- `InsertSetAsync` remains the normal path for full repository, tracking, cascade, and `SaveChanges` semantics.
- PostgreSQL and SQLite provider strategies remain explicit placeholders until provider-native writers are implemented.
- Provider strategies remain stateless singleton registrations; entity/context orchestration follows the repository lifetime.

## Alternatives Considered

- **Keep the infrastructure contract and document behavior bypasses**
  - Benefits: no public move and minimal implementation work.
  - Drawbacks: Application remains coupled to infrastructure and configured write semantics remain unsafe to infer.
  - Rejected because documentation cannot provide missing audit, event, outbox, or transaction guarantees.

- **Add bulk insert to `IGenericRepository<TEntity>`**
  - Benefits: one persistence abstraction and direct access to repository configuration.
  - Drawbacks: every repository/provider must expose an optional capability, and tracked aggregate semantics become conflated with native root-table ingestion.
  - Rejected because bulk insertion has materially different support and performance constraints.

- **Reuse repository decorators directly around the native provider**
  - Benefits: no second behavior model and automatic reuse of existing code.
  - Drawbacks: repository decorators forward to an inner repository that terminates in `AddRange`/`SaveChanges`; their before/after semantics cannot be safely extracted at runtime.
  - Rejected because it either loses native performance or duplicates execution.

- **Retain an obsolete forwarding infrastructure interface**
  - Benefits: temporary source compatibility for preview consumers.
  - Drawbacks: two public contracts, namespace ambiguity, dual DI aliasing, and a later removal task.
  - Rejected because the feature has no consumers and the compatibility cost has no corresponding benefit.

- **Always fall back when semantics are unsupported**
  - Benefits: maximizes functional compatibility with normal repositories.
  - Drawbacks: silently changes throughput, tracking, graph persistence, and generated-value behavior.
  - Rejected in favor of fail-by-default with explicitly configured, observable fallback.

## Implementation Status

The decision is implemented across the Domain contract, ordered repository descriptors, compatibility analysis and adapters, native behavior pipeline, hardened root-table mapping, EF-owned transaction orchestration, provider strategies, tests, samples, and feature documentation.

Success criteria for the completed decision are:

- Application code resolves the Domain contract without EF or SQL Server references.
- No relevant configured behavior or interceptor is silently bypassed.
- Unsupported graphs and mappings fail before provider work.
- Native SQL Server insertion performs one provider/native-write operation and is atomic across batches and outbox rows.
- Fallback is explicit, observable, and invokes the decorated repository once.

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Clean/Onion Architecture with Strict Layer Boundaries
- [ADR-0004](0004-repository-decorator-behaviors.md): Repository Pattern with Decorator Behaviors
- [ADR-0006](0006-outbox-pattern-domain-events.md): Outbox Pattern for Domain Events
- [ADR-0007](0007-entity-framework-core-code-first-migrations.md): Entity Framework Core with Code-First Migrations
- [ADR-0018](0018-dependency-injection-service-lifetimes.md): Dependency Injection & Service Lifetime Management
- [ADR-0027](0027-provider-strategy-for-entity-bulk-insert.md): Use Provider Strategies for Entity Bulk Insert

## References

- [Domain bulk inserter contract](../../src/Domain/Repositories/IEntityBulkInserter.cs)
- [Shared EF dispatcher](../../src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs)
- [SQL Server provider](../../src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs)
- [Implementation plan](../../plan/pln-redesign-entity-framework-bulk-insert-behavior-pipeline-1.md)

## Notes

This record is retained as history. Its repository-descriptor, compatibility-classification, behavior-adapter, and repository-fallback decisions are superseded by ADR-0029. The Domain-owned contract and provider-isolation decisions remain in force.

The provider-name strategy and automatic provider registration from ADR-0027 remain accepted. This ADR supersedes only ADR-0027 statements that assign the application-facing contract to Infrastructure or define repository-behavior bypass as the final public guarantee.
