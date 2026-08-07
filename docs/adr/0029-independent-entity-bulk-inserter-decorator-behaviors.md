# ADR-0029: Register Entity Bulk Inserters Independently with Explicit Decorators

## Status

Accepted

## Date

2026-07-28

## Context

ADR-0028 moved `IEntityBulkInserter<TEntity>` to Domain, but coupled registration to
`IGenericRepository<TEntity>` through `.WithBulkInsert()`. It also derived native behavior
from repository descriptors and compatibility analysis. This made the high-throughput,
root-table operation depend on unrelated repository configuration, introduced implicit
semantics, and retained a repository fallback whose tracking and performance characteristics
differed from the native operation.

Bulk insertion needs a clear capability boundary: callers should be able to choose its write
semantics at composition time, see their order, and receive a typed failure when their EF
provider has no native implementation. The SQL Server writer must remain atomic with the
outbox when the outbox decorator owns the transaction.

## Decision

- Register the terminal operation independently with
  `AddEntityFrameworkBulkInserter<TEntity, TContext>(options)`.
- Keep `IEntityBulkInserter<TEntity>` in `Domain.Repositories`; keep EF mapping, terminal
  transaction handling, and provider dispatch in Infrastructure; keep native APIs in provider
  packages.
- Build behavior chains only from explicit, ordered `.WithBehavior(...)` calls. The first
  registered decorator is outermost.
- Provide Domain decorators for cancellation, tracing, logging, metrics, audit state,
  concurrency, created domain events, event metrics, and direct event publication. Provide
  the EF outbox decorator in Infrastructure because it needs `DbContext` and the outbox
  context contract. Provide native ChangeHistory capture as a separate Infrastructure
  decorator because it needs `DbContext`, `IChangeHistoryContext`, and explicit
  `ChangeHistoryOptions` opt-in.
- Do not inspect repository decorators or EF interceptors, do not retain descriptor metadata,
  and do not provide an adapter, lifecycle registry, compatibility analysis, or repository
  fallback.
- Use typed unsupported inserters for providers without a native writer. They return
  `EntityBulkInsertPreconditionError`; they never use row-by-row insertion.
- The terminal and outbox decorators own transactions only when no caller transaction exists.
  A caller-owned transaction is never committed, rolled back, or disposed by either component.
  Direct domain-event publication is non-atomic and must not be combined with the outbox
  decorator.

## Rationale

Independent registration makes the performance and semantic trade-off visible at the
composition root. Explicit decorators preserve the familiar repository decorator model while
avoiding inference from a different capability. Typed unsupported behavior makes provider
availability deterministic and prevents a hidden change from native bulk writes to tracked
row-by-row persistence.

## Consequences

### Positive

- Repository and bulk-inserter lifecycles are independent and easy to reason about.
- Applications choose exactly which bulk-insert semantics execute and in which order.
- SQL Server root, ChangeHistory, and outbox writes are atomic when the outer decorator owns the transaction.
- Unsupported provider use is explicit and typed.

### Negative

- Consumers must register bulk behavior chains separately from repository behavior chains.
- Existing `.WithBulkInsert()` and descriptor-based preview registrations are source-breaking.
- Direct publication remains a deliberate non-atomic option and requires an application-level
  decision.

### Neutral

- `IGenericRepository<TEntity>.InsertSetAsync` remains the appropriate path for tracked
  aggregate persistence and graph cascades.
- Native mapping remains limited to root-table writes, including same-table owned references.

## Alternatives Considered

- **Infer bulk behavior from repository configuration**
  - Preserves one registration location but makes native semantics implicit and couples two
    persistence capabilities.
  - Rejected because the behavior order and transaction ownership cannot be made clear.

- **Keep a descriptor/adapter compatibility layer**
  - Smooths the preview transition but leaves obsolete registrations and ambiguous ownership.
  - Rejected because the feature has no supported compatibility requirement.

- **Fall back to repository insertion for unsupported providers or semantics**
  - Offers broad functional coverage but silently changes throughput, tracking, and generated
    value behavior.
  - Rejected in favor of typed precondition failures.

## Related Decisions

- [ADR-0004](0004-repository-decorator-behaviors.md): Repository Pattern with Decorator Behaviors
- [ADR-0006](0006-outbox-pattern-domain-events.md): Outbox Pattern for Domain Events
- [ADR-0027](0027-provider-strategy-for-entity-bulk-insert.md): Use Provider Strategies for Entity Bulk Insert
- [ADR-0028](0028-domain-entity-bulk-insert-behavior-pipeline.md): Historical compatibility-pipeline decision

## References

- [Repository feature documentation](../features-domain-repositories.md)
- [Domain bulk inserter contract](../../src/Domain/Repositories/IEntityBulkInserter.cs)
- [Entity Framework terminal](../../src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs)
- [Entity Framework outbox decorator](../../src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInserterOutboxDomainEventBehavior.cs)

## Migration

Replace repository-attached `.WithBulkInsert()` registration with an independent
`AddEntityFrameworkBulkInserter<TEntity, TContext>()` call. Re-register only the needed
bulk decorators. For an atomic outbox chain, register the outbox decorator before audit,
ChangeHistory, concurrency, and domain-event creation decorators. Native ChangeHistory is
not inferred from repository create capture; configure `.CaptureBulkInserts(...)` explicitly.
