---
goal: Refactor Entity Bulk Inserter to Use Independent Decorator Behaviors
version: 1.1
date_created: 2026-07-27
last_updated: 2026-07-27
owner: bITdevKit
status: 'In Progress'
tags: [refactor, architecture, entity-framework, bulk-insert, dependency-injection, decorator, behaviors]
---

# Introduction

![Status: In Progress](https://img.shields.io/badge/status-In%20Progress-blue)

This plan removes the repository compatibility-analysis architecture introduced by `plan/pln-redesign-entity-framework-bulk-insert-behavior-pipeline-1.md` and replaces it with an independent BulkInserter decorator model. A bulk behavior implements `IEntityBulkInserter<TEntity>`, receives the next `IEntityBulkInserter<TEntity>` in its constructor or registration factory, and forwards `InsertAsync` after applying its own concern. Registration uses a dedicated `EntityBulkInserterBuilderContext<TEntity>` with the same three `WithBehavior(...)` forms and the same deterministic decorator ordering as `RepositoryBuilderContext<TEntity>`.

The BulkInserter no longer reads normal repository behavior descriptors, classifies repository behaviors or Entity Framework interceptors, creates native-equivalent adapters, or falls back to `IGenericRepository<TEntity>.InsertSetAsync`. Normal repository behavior and BulkInserter behavior are separate, explicitly configured decorator chains.

The refactor implements BulkInserter-owned AuditState, Cancellation, Concurrency, DomainEvent creation, DomainEvent metrics, DomainEvent publishing, Logging, Metrics, Tracing, and Entity Framework Outbox behaviors. These are real `IEntityBulkInserter<TEntity>` decorators, not adapters around repository behaviors. Provider selection, root-table mapping validation, sequential key preparation, and the default transaction remain responsibilities of the terminal Entity Framework BulkInserter. The BulkInserter Outbox behavior owns the encompassing transaction when configured so root rows and outbox rows commit or roll back atomically.

Repository findings were recorded at commit `5329d490` on branch `feature/repository-insert-range` on 2026-07-27. The implementation must refresh the stale-reference inventory before editing because the branch may advance after this plan is created.

## 1. Requirements & Constraints

- **REQ-001**: Retain `BridgingIT.DevKit.Domain.Repositories.IEntityBulkInserter<TEntity>` as the only application-facing bulk-insert contract.
- **REQ-002**: Add `EntityBulkInserterBuilderContext<TEntity>` with `WithBehavior<TBehavior>()`, `WithBehavior<TBehavior>(Func<IEntityBulkInserter<TEntity>, TBehavior>)`, and `WithBehavior<TBehavior>(Func<IEntityBulkInserter<TEntity>, IServiceProvider, TBehavior>)`.
- **REQ-003**: Constrain every BulkInserter behavior to `IEntityBulkInserter<TEntity>`. Do not retain or replace the lifecycle-hook contract `IEntityBulkInsertBehavior<TEntity>`.
- **REQ-004**: Apply behaviors as Scrutor decorators. The first configured behavior is the outermost behavior, matching `RepositoryBuilderContext<TEntity>`.
- **REQ-005**: Replace `.WithBulkInsert()` on `EntityFrameworkRepositoryBuilderContext<TEntity,TContext>` with `IServiceCollection.AddEntityFrameworkBulkInserter<TEntity,TContext>(EntityBulkInsertOptions options = null)`.
- **REQ-006**: Return `EntityBulkInserterBuilderContext<TEntity>` from `AddEntityFrameworkBulkInserter<TEntity,TContext>()` so behavior generic arguments are inferred by `.WithBehavior<TBehavior>()`.
- **REQ-007**: Make BulkInserter registration independent from `IGenericRepository<TEntity>`. The Entity Framework BulkInserter must neither resolve nor invoke a repository.
- **REQ-008**: Delete repository behavior descriptors and restore `RepositoryBuilderContext<TEntity>` to decorator registration only.
- **REQ-009**: Delete the complete BulkInserter compatibility layer, including compatibility classifications, decisions, analyzer, adapter registry, execution plan, execution modes, unsupported-feature policy, compatibility error, and repository fallback.
- **REQ-010**: Delete the current lifecycle-hook pipeline, resolver, behavior registrations, operation context, operation result, and failure context. Replace the lifecycle-hook built-ins with full-contract `IEntityBulkInserter<TEntity>` decorators.
- **REQ-011**: Do not automatically copy, adapt, or infer normal repository behaviors. Applications explicitly register the provided BulkInserter-owned decorators.
- **REQ-012**: Do not analyze Entity Framework interceptors. Document that provider-native bulk insertion bypasses `SaveChanges` and command interceptors unless an explicit BulkInserter decorator provides the required concern.
- **REQ-013**: Retain exact provider selection through `DbContext.Database.ProviderName`. Return a typed provider error for missing, duplicate, or `IsSupported == false` providers before opening a connection.
- **REQ-014**: Retain the current root-table mapping rules, shadow-value providers, duplicate-reference checks, detached-input requirement, value converters, identity handling, inheritance checks, and graph rejection.
- **REQ-015**: Retain `EntityBulkInsertMappingBuilder<TEntity>.Analyze(...)` and `.Build(...)`. These methods perform mapping preflight and batch construction; they are not compatibility analysis and must not be deleted.
- **REQ-016**: Retain rejection of ambient `System.Transactions.Transaction` and an inserter-owned transaction under a retrying EF execution strategy. Replace compatibility-named errors with a provider-neutral precondition error.
- **REQ-017**: Retain one Entity Framework transaction across the provider operation, owned rollback on failure or cancellation, reuse of caller-owned EF transactions, and no commit or rollback of caller-owned transactions. When the BulkInserter Outbox behavior is configured, it owns this transaction around both the inner native insert and outbox `SaveChangesAsync`.
- **REQ-018**: Retain one `IEntityBulkInsertProvider.InsertAsync(...)` invocation and one provider-native write invocation per successful operation.
- **REQ-019**: Retain scoped and transient registrations. Reject singleton registration because `DbContext` is not thread-safe.
- **REQ-020**: Reject a second `IEntityBulkInserter<TEntity>` registration for the same entity with an actionable exception. Do not silently choose between contexts or options and do not add keyed services in this refactor.
- **REQ-021**: Add `EntityBulkInserterBuilderContext<TEntity>.WithShadowValueProvider<TProvider>()` as the BulkInserter-owned registration API for `IEntityBulkInsertShadowValueProvider<TEntity>`.
- **REQ-022**: Reject duplicate shadow-value provider implementation types and preserve their registration order.
- **REQ-023**: Move concurrency-version assignment out of `EntityBulkInsertMappingBuilder<TEntity>` into `EntityBulkInserterConcurrencyBehavior<TEntity>`. Remove `AssignConcurrencyVersions` from `EntityBulkInsertOptions`. Keep sequential-GUID preparation in the mapping builder because key generation is a provider mapping concern, not a repository behavior.
- **REQ-024**: Remove `EntityBulkInsertOptions.UnsupportedFeaturePolicy` and remove it from `IsEquivalentTo(...)` or delete `IsEquivalentTo(...)` if it has no remaining caller.
- **REQ-025**: Delete DbContext, interceptor, and repository compatibility registration descriptors and remove their registrations from SQL Server, PostgreSQL, SQLite, Cosmos, and Entity Framework repository setup.
- **REQ-026**: Preserve PostgreSQL and SQLite placeholder providers and their explicit typed unsupported result.
- **REQ-027**: Preserve the Domain/Application dependency direction. Domain behavior registration must not reference Entity Framework, SQL Server, mapping, or provider types.
- **REQ-028**: Add XML documentation and usage examples to every new public type and member.
- **REQ-029**: Treat all removed BulkInserter compatibility and lifecycle types as preview-breaking deletions. Do not create obsolete aliases, forwarding types, namespace shims, or dual registrations.
- **REQ-030**: Update ADR-0028 and all user-facing documentation so no compatibility, automatic parity, or repository-fallback guarantee remains.
- **REQ-031**: Implement `EntityBulkInserterAuditStateBehavior<TEntity>` with its own `EntityBulkInserterAuditStateBehaviorOptions`. It must materialize once, apply `AuditState.SetCreated(...)` to `IAuditable` entities using `ICurrentUserAccessor`, and forward the same materialized collection exactly once.
- **REQ-032**: Implement `EntityBulkInserterCancellationBehavior<TEntity>` to throw before materialization or inner invocation when the supplied token is already canceled.
- **REQ-033**: Implement `EntityBulkInserterConcurrencyBehavior<TEntity>` to assign exactly one sequential concurrency version to every `IConcurrency` entity before forwarding.
- **REQ-034**: Implement `EntityBulkInserterDomainEventBehavior<TEntity>` to register one `EntityCreatedDomainEvent<TEntity>` for each `IAggregateRoot` before forwarding.
- **REQ-035**: Implement `EntityBulkInserterDomainEventMetricsBehavior<TEntity>` to count the domain events visible at its position in the decorator chain. Document that it must be registered after the event-creation behavior to count newly created events.
- **REQ-036**: Implement `EntityBulkInserterDomainEventPublisherBehavior<TEntity>` for aggregate roots. It calls the inner inserter first, publishes and clears events only after a successful result, preserves events on failure, and documents that publication is post-persistence and non-atomic.
- **REQ-037**: Implement `EntityBulkInserterLoggingBehavior<TEntity>` with payload-free start, success, and failure logs containing operation id, entity type, entity count, inserted count, and duration.
- **REQ-038**: Implement `EntityBulkInserterMetricsBehavior<TEntity>` with BulkInserter-specific total/current/failure/duration series. Do not reuse the normal repository `repositories_write` series.
- **REQ-039**: Implement `EntityBulkInserterTracingBehavior<TEntity>` with one activity around the full inner call and tags for operation id, entity type, count, inserted count, and result status.
- **REQ-040**: Implement `EntityBulkInserterOutboxDomainEventBehavior<TEntity,TContext>` in Infrastructure.EntityFramework. It must begin or join one EF transaction, invoke the inner native inserter once, persist collected outbox events before an owned commit, roll back an owned transaction on inner/outbox failure or cancellation, and clear/enqueue events only after an owned commit.
- **REQ-041**: For a caller-owned transaction, the Outbox behavior must add outbox rows to that transaction without committing, rolling back, disposing, clearing aggregate events, or immediate enqueueing. Interval polling remains responsible for later processing.
- **REQ-042**: Add a shared materialization helper used by all built-in BulkInserter decorators. If the incoming enumerable is already a normalized `IReadOnlyList<TEntity>`, return it without copying; otherwise filter nulls and materialize once. Every decorator forwards the returned collection.
- **REQ-043**: Provide XML examples showing explicit registration of all write-relevant BulkInserter behaviors in the recommended order: Cancellation, Tracing, Logging, Metrics, Outbox, AuditState, Concurrency, DomainEvent, and DomainEventMetrics.
- **REQ-044**: Do not create BulkInserter versions of query-only repository behaviors: Include, IncludePath, Order, Specification, NoTracking, SoftDelete, and read-only logging.
- **SEC-001**: BulkInserter core and example behaviors must not log entities, connection strings, column values, event payloads, audit identities, or other PII.
- **CON-001**: Do not add a commercial or cross-provider bulk library.
- **CON-002**: Do not change `IGenericRepository<TEntity>` or add bulk insertion to that interface.
- **CON-003**: Do not broaden native bulk mapping to aggregate graphs, multiple tables, JSON ownership, TPT, or TPC.
- **CON-004**: Run repository-wide build and test commands sequentially to avoid shared `obj/ref` races.
- **PAT-001**: Follow the decorator chain used by `RepositoryBuilderContext<TEntity>` and `DocumentStoreBuilderContext<T>`: record decorator actions, restore the original service descriptor, remove existing decorated descriptors, and reapply actions in reverse registration order.
- **PAT-002**: A behavior is responsible for exactly one concern and explicitly forwards to its inner `IEntityBulkInserter<TEntity>`.
- **PAT-003**: The concrete Entity Framework BulkInserter remains the terminal service in the behavior chain.
- **PAT-004**: Register observability behaviors outside the Outbox behavior so their completion observes outbox commit failures. Register entity-mutating and event-creation behaviors inside the Outbox behavior so their work occurs before the native insert and outbox collection.

### Target registration API

```csharp
services.AddEntityFrameworkRepository<TodoItem, CoreDbContext>()
    .WithTransactions()
    .WithBehavior<RepositoryMetricsBehavior<TodoItem>>()
    .WithBehavior<RepositoryTracingBehavior<TodoItem>>()
    .WithBehavior<RepositoryLoggingBehavior<TodoItem>>()
    .WithBehavior<RepositoryAuditStateBehavior<TodoItem>>()
    .WithBehavior<RepositoryOutboxDomainEventBehavior<TodoItem, CoreDbContext>>();

services.AddEntityFrameworkBulkInserter<TodoItem, CoreDbContext>(
        new SqlServerEntityBulkInsertOptions
        {
            BatchSize = 1_000
        })
    .WithBehavior<EntityBulkInserterCancellationBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterTracingBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterLoggingBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterMetricsBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterOutboxDomainEventBehavior<TodoItem, CoreDbContext>>()
    .WithBehavior<EntityBulkInserterAuditStateBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterConcurrencyBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterDomainEventBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterDomainEventMetricsBehavior<TodoItem>>()
    .WithShadowValueProvider<TodoItemTenantShadowValueProvider>();
```

### Target behavior shape

```csharp
public sealed class TodoItemBulkInsertValidationBehavior(
    IEntityBulkInserter<TodoItem> inner)
    : IEntityBulkInserter<TodoItem>
{
    public Task<Result<long>> InsertAsync(
        IEnumerable<TodoItem> entities,
        CancellationToken cancellationToken = default)
    {
        // Validate the batch or return a failed Result<long>.
        return inner.InsertAsync(entities, cancellationToken);
    }
}
```

### Target execution flow

```text
Application
  -> Cancellation -> Tracing -> Logging -> Metrics
    -> Outbox transaction behavior
      -> AuditState -> Concurrency -> DomainEvent -> DomainEventMetrics
        -> EntityFrameworkEntityBulkInserter<TEntity,TContext>
          -> mapping preflight and batch construction
          -> exact IEntityBulkInsertProvider
          -> provider-native bulk write
      -> outbox SaveChanges
      -> owned transaction commit
    -> Metrics -> Logging -> Tracing completion
```

No `IGenericRepository<TEntity>` instance participates in this flow.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Remove repository metadata that exists only for BulkInserter compatibility inference.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Refresh the current inventory with `rg -n "Compatibility|BehaviorDescriptors|RepositoryBehaviorDescriptor|RepositoryBehaviorRegistrationKind|DbContextRegistrationDescriptor|DbContextInterceptorDescriptor|EntityFrameworkRepositoryRegistrationDescriptor" src tests benchmarks docs examples README.md CHANGELOG.md`. Record newly discovered feature-owned references in this plan before deleting code. | x | 2026-07-27 |
| TASK-002 | Delete `src/Domain/Repositories/RepositoryBehaviorDescriptor.cs` and `src/Domain/Repositories/RepositoryBehaviorRegistrationKind.cs`. | x | 2026-07-27 |
| TASK-003 | Modify `src/Domain/Repositories/RepositoryBuilderContext.cs`: remove `behaviorDescriptors`, `BehaviorDescriptors`, and descriptor creation from all three `WithBehavior(...)` overloads; preserve existing decorator action storage and `RegisterBehaviors()` ordering. | x | 2026-07-27 |
| TASK-004 | Delete `tests/Domain.UnitTests/Repositories/RepositoryBuilderContextBehaviorDescriptorTests.cs`; keep and run existing repository decorator-order tests as regression coverage. | x | 2026-07-27 |
| TASK-005 | Delete `src/Infrastructure.EntityFramework/Repositories/Bulk/DbContextRegistrationDescriptor.cs`, `DbContextInterceptorDescriptor.cs`, and `EntityFrameworkRepositoryRegistrationDescriptor.cs`. | x | 2026-07-27 |
| TASK-006 | Modify `src/Infrastructure.EntityFramework/Repositories/ServiceCollectionExtensions.cs`: remove direct/mapped `EntityFrameworkRepositoryRegistrationDescriptor<TEntity>` registrations without changing repository service lifetimes or implementations. | x | 2026-07-27 |
| TASK-007 | Modify SQL Server, PostgreSQL, SQLite, and Cosmos `ServiceCollectionExtensions.cs` files: remove `RegisterBulkInsertCompatibilityMetadata<TContext>(...)` calls and methods; retain provider-specific `IEntityBulkInsertProvider` registration exactly where native or placeholder strategies are currently registered. | x | 2026-07-27 |

#### Phase 1 inventory refresh (2026-07-27)

Feature-owned metadata producers are `RepositoryBuilderContext<TEntity>`, the two Entity Framework repository registration overloads, and the SQL Server, PostgreSQL, SQLite, and Cosmos DbContext extension methods. The only dedicated Domain metadata test is `RepositoryBuilderContextBehaviorDescriptorTests`.

The search also found Phase 2 consumers that must remain only until the compatibility layer is deleted: `Bulk/Compatibility/EntityBulkInsertCompatibilityAnalyzer`, `Bulk/ServiceCollectionExtensions`, and the BulkInserter compatibility/service-registration unit tests. These follow-on references are intentionally not retained as replacement metadata APIs.

### Implementation Phase 2

- GOAL-002: Delete compatibility analysis, fallback, and lifecycle-hook behavior infrastructure.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Delete every file under `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/` except no file is retained in that directory. Move `EntityBulkInsertProviderError` to `src/Infrastructure.EntityFramework/Repositories/Bulk/Errors/EntityBulkInsertProviderError.cs` before deleting the directory because provider failures remain part of the core contract. | x | 2026-07-27 |
| TASK-009 | Add `src/Infrastructure.EntityFramework/Repositories/Bulk/Errors/EntityBulkInsertPreconditionError.cs` as the typed replacement for ambient-transaction, retry-strategy, unsupported-provider, and mapping-preflight failures that currently use compatibility terminology. The error must contain a stable stage and message but no entity payload or connection data. | x | 2026-07-27 |
| TASK-010 | Delete the old files `IEntityBulkInsertBehavior.cs`, `EntityBulkInsertBehaviorPipeline.cs`, `EntityBulkInsertBehaviorRegistration.cs`, `EntityBulkInsertBehaviorResolver.cs`, `EntityBulkInsertContext.cs`, `EntityBulkInsertFailure.cs`, and `EntityBulkInsertResult.cs` from `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/`. Delete the lifecycle-hook implementations from `EntityBulkInsertBuiltInBehaviors.cs` and `EntityBulkInsertOutboxBehavior.cs`; Phase 4 replaces them with full-contract decorators. | x | 2026-07-27 |
| TASK-011 | Delete `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertCompatibilityAnalyzerTests.cs` and `EntityBulkInsertBehaviorPipelineTests.cs`. Replace `EntityBulkInsertBuiltInBehaviorTests.cs` with the new Domain and Infrastructure decorator tests specified in Phase 4. | x | 2026-07-27 |
| TASK-012 | Modify `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs`: remove `UnsupportedFeaturePolicy`, `AssignConcurrencyVersions`, their XML examples, and their equivalence comparisons; retain provider-neutral mapping and provider options. | x | 2026-07-27 |
| TASK-013 | Delete `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertConfiguration.cs` and `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/EntityBulkInsertShadowValueProviderRegistration.cs`; replace mutable registration metadata with direct DI descriptors owned by the new builder. | x | 2026-07-27 |

### Implementation Phase 3

- GOAL-003: Add the independent BulkInserter decorator builder and registration API.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | Add `src/Domain/Repositories/EntityBulkInserterBuilderContext.cs` in namespace `Microsoft.Extensions.DependencyInjection`. Implement `Services`, `Lifetime`, optional `Configuration`, the three `WithBehavior(...)` overloads, original-descriptor restoration, decorated-descriptor removal, and reverse action application using `IEntityBulkInserter<TEntity>` as the decorated contract. | x | 2026-07-27 |
| TASK-015 | Add XML examples to `EntityBulkInserterBuilderContext<TEntity>` showing type registration, inner-only factory registration, and inner-plus-service-provider factory registration. | x | 2026-07-27 |
| TASK-016 | Rewrite `src/Infrastructure.EntityFramework/Repositories/Bulk/ServiceCollectionExtensions.cs`: delete `WithBulkInsert(...)`, `WithBulkInsertBehavior(...)`, compatibility registrations, repository validation, and configuration accessors; add `IServiceCollection.AddEntityFrameworkBulkInserter<TEntity,TContext>(EntityBulkInsertOptions options = null)`. | x | 2026-07-27 |
| TASK-017 | In `AddEntityFrameworkBulkInserter<TEntity,TContext>()`, validate arguments/options/lifetime, reject an existing `IEntityBulkInserter<TEntity>` descriptor, register `EntityBulkInsertMappingBuilder<TEntity>` with the builder lifetime, register the terminal `EntityFrameworkEntityBulkInserter<TEntity,TContext>` factory with the same lifetime, and return `EntityBulkInserterBuilderContext<TEntity>`. | x | 2026-07-27 |
| TASK-018 | Add `EntityBulkInserterBuilderContext<TEntity>.WithShadowValueProvider<TProvider>()` as an Infrastructure.EntityFramework extension or builder method that registers `IEntityBulkInsertShadowValueProvider<TEntity>` with the BulkInserter lifetime, rejects duplicate implementation types, and returns the same builder. Keep EF-specific shadow contracts out of Domain if implemented as an extension. | x | 2026-07-27 |
| TASK-019 | Update `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/IEntityBulkInsertShadowValueProvider.cs` XML examples to use `AddEntityFrameworkBulkInserter<TEntity,TContext>().WithShadowValueProvider<TProvider>()`. | x | 2026-07-27 |
| TASK-020 | Add `tests/Domain.UnitTests/Repositories/EntityBulkInserterBuilderContextTests.cs` with a fake terminal inserter and recording decorators. Assert type/factory/service-provider-factory registration, first-configured-outermost order, exactly-once terminal invocation, short-circuit behavior, and independence from `IGenericRepository<TEntity>`. | x | 2026-07-27 |
| TASK-021 | Rewrite `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertServiceCollectionExtensionsTests.cs` for the new `AddEntityFrameworkBulkInserter` API, scoped/transient resolution, singleton rejection, duplicate entity registration rejection, behavior lifetime, and ordered shadow providers. | x | 2026-07-27 |

### Implementation Phase 4

- GOAL-004: Implement the BulkInserter-owned behavior suite as independent full-contract decorators.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-022 | Add `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterBehaviorUtilities.cs` with one internal `Materialize<TEntity>(IEnumerable<TEntity>)` helper. Reuse an already normalized `IReadOnlyList<TEntity>`; otherwise filter nulls and materialize once. Built-in behaviors must pass the returned list to the inner inserter. | x | 2026-07-27 |
| TASK-023 | Add `EntityBulkInserterAuditStateBehaviorOptions.cs` and `EntityBulkInserterAuditStateBehavior.cs` in the Domain BulkInserter behavior folder. Mirror the normal repository's `AuditStateByType` selection, use `ICurrentUserAccessor`, initialize missing `AuditState`, call `SetCreated` once per `IAuditable`, and forward once. | x | 2026-07-27 |
| TASK-024 | Add `EntityBulkInserterCancellationBehavior.cs`. Check cancellation before materialization and delegate invocation; otherwise forward without changing the batch. | x | 2026-07-27 |
| TASK-025 | Add `EntityBulkInserterConcurrencyBehavior.cs`. Materialize, assign `GuidGenerator.CreateSequential()` exactly once to every `IConcurrency.ConcurrencyVersion`, and forward once. Remove concurrency assignment and its deduplication marker from `EntityBulkInsertMappingBuilder<TEntity>`. | x | 2026-07-27 |
| TASK-026 | Add `EntityBulkInserterDomainEventBehavior.cs`. Materialize, register one `EntityCreatedDomainEvent<TEntity>` for every `IAggregateRoot`, and forward once. | x | 2026-07-27 |
| TASK-027 | Add `EntityBulkInserterDomainEventMetricsBehavior.cs`. Materialize, count currently registered domain events using existing `Metrics` helpers, and forward once. Require registration after `EntityBulkInserterDomainEventBehavior<TEntity>` when newly created events must be counted. | x | 2026-07-27 |
| TASK-028 | Add `EntityBulkInserterDomainEventPublisherBehavior.cs`. For `TEntity : IAggregateRoot`, materialize and invoke the inner inserter; publish and clear domain events only when the result succeeds; retain events and propagate failure when inner insertion or publishing fails. Document non-atomic post-persistence publication and prohibit combining it with the Outbox behavior. | x | 2026-07-27 |
| TASK-029 | Add `EntityBulkInserterLoggingBehavior.cs`. Generate one operation id, log payload-free start/success/failure messages around `Inner.InsertAsync`, include duration and counts, rethrow cancellation, and preserve the original `Result<long>`. | x | 2026-07-27 |
| TASK-030 | Add `EntityBulkInserterMetricsBehavior.cs`. Track BulkInserter-specific total/current/failure/duration metrics around the full inner call, decrement current in `finally`, and preserve cancellation/result behavior. | x | 2026-07-27 |
| TASK-031 | Add `EntityBulkInserterTracingBehavior.cs`. Create one activity around the full inner call, set success/error/cancellation status, add only operation/entity/count/inserted-count tags, and dispose in `finally`. | x | 2026-07-27 |
| TASK-032 | Replace `EntityBulkInsertOutboxBehavior.cs` with `EntityBulkInserterOutboxDomainEventBehavior<TEntity,TContext>` implementing `IEntityBulkInserter<TEntity>`. If no EF transaction exists, open the connection when needed, begin a transaction, invoke inner once, collect/save outbox rows, commit, then clear events and enqueue immediate ids. Roll back and detach staged outbox rows on failure or cancellation. | x | 2026-07-27 |
| TASK-033 | In the Outbox behavior's caller-owned transaction path, invoke inner once, collect and save outbox rows into the active transaction, then return without commit/rollback/dispose/event clearing/immediate enqueue. | x | 2026-07-27 |
| TASK-034 | Add XML documentation to every public behavior and option with constructor usage and the recommended `.WithBehavior<...>()` chain. Do not reference repository behavior adapters or compatibility classifications. | x | 2026-07-27 |
| TASK-035 | Add `tests/Domain.UnitTests/Repositories/BulkInserter/EntityBulkInserterBehaviorTests.cs` covering AuditState username/email/user-id, cancellation, one concurrency assignment, event creation, event metrics order, direct publisher success/failure, logging redaction, metrics completion, tracing status, null filtering, and exactly-once forwarding. | x | 2026-07-27 |
| TASK-036 | Replace `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertBuiltInBehaviorTests.cs` with `EntityBulkInserterOutboxDomainEventBehaviorTests.cs` covering owned/caller transaction semantics, SaveChanges failure, rollback, event retention/clearing, enqueue timing, and exactly-once inner invocation. | x | 2026-07-27 |

### Implementation Phase 5

- GOAL-005: Simplify the terminal Entity Framework BulkInserter while preserving native safety and coordinating correctly with the explicit Outbox decorator.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-037 | Rewrite the constructor and fields of `EntityFrameworkEntityBulkInserter<TEntity,TContext>` to remove `EntityBulkInsertCompatibilityAnalyzer`, `EntityBulkInsertBehaviorResolver`, and `IGenericRepository<TEntity>`. Retain logger, context, mapping builder, options, and providers only. | x | 2026-07-28 |
| TASK-038 | Remove compatibility execution-plan selection and `ExecuteRepositoryFallbackAsync(...)`. Every non-empty operation uses the exact native provider or returns a typed failure; it never calls `InsertSetAsync`. | x | 2026-07-28 |
| TASK-039 | Use the shared BulkInserter materialization helper for cancellation, null filtering, one-time materialization, and empty-success behavior. Select the exact provider and reject `IsSupported == false` with `UnsupportedReason` before mapping, connection, or transaction work. | x | 2026-07-28 |
| TASK-040 | Keep mapping `Analyze(...)` followed by `Build(...)`. Convert thrown mapping/preflight exceptions to `EntityBulkInsertPreconditionError` without using compatibility types or terminology. Preserve `OperationCanceledException`. | x | 2026-07-28 |
| TASK-041 | Remove lifecycle pipeline calls from native execution. When no behavior-owned/caller-owned EF transaction exists, keep connection open/close, transaction begin/commit/rollback/dispose, provider invocation, cancellation rethrow, and typed provider failure aggregation. | x | 2026-07-28 |
| TASK-042 | When `DbContext.Database.CurrentTransaction` exists, invoke the provider inside that transaction and return without commit, rollback, disposal, or connection close. This path is used by both callers and `EntityBulkInserterOutboxDomainEventBehavior<TEntity,TContext>`. | x | 2026-07-28 |
| TASK-043 | Replace `ValidateNativeRuntimeCompatibility(...)` with `ValidatePreconditions(...)` returning `EntityBulkInsertPreconditionError` for ambient transactions and unsafe retry strategies. Allow an active EF transaction even when the execution strategy retries because the outer owner controls the retry unit. | x | 2026-07-28 |
| TASK-044 | Modify `EntityBulkInsertMappingBuilder<TEntity>` and related tests to remove core concurrency assignment. Preserve sequential GUIDs, mapping preflight, converters, shadows, inheritance/graph checks, identity options, and detached inputs. | x | 2026-07-28 |
| TASK-045 | Preserve current `SqlServerEntityBulkInsertProvider` external-transaction requirement, one `SqlBulkCopy.WriteToServerAsync` call, `KeepIdentity`, `TableLock`, batch size, timeout, and cancellation semantics. | x | 2026-07-28 |
| TASK-046 | Rewrite `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserterTests.cs`: remove analyzer/fallback/post-commit lifecycle cases; retain and adapt provider selection, unsupported placeholder, mapping preflight, core-owned transaction, decorator-owned transaction, rollback, cancellation, lifetime, and exactly-once provider tests. | x | 2026-07-28 |
| TASK-047 | Update `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertArchitectureTests.cs` to assert that the terminal implementation has no dependency on `IGenericRepository<TEntity>`, no exported compatibility/lifecycle-hook types remain, Domain owns the contract/builder/standard behaviors, and Infrastructure.EntityFramework owns only the EF Outbox behavior. | x | 2026-07-28 |

### Implementation Phase 6

- GOAL-006: Migrate examples, integration tests, ADRs, and user-facing documentation to the explicit BulkInserter behavior suite.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-048 | Update `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/SqlServerEntityBulkInsertProviderTests.cs` setup to call `services.AddEntityFrameworkBulkInserter<TEntity,TContext>(options)` separately from repository setup; remove assertions for deleted compatibility descriptors. | x | 2026-07-28 |
| TASK-049 | Add SQL Server integration coverage for the explicit AuditState, Concurrency, DomainEvent, and Outbox behaviors. Assert root and outbox rows share one transaction and a forced outbox failure rolls back both. | x | 2026-07-28 |
| TASK-050 | Update `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/PlaceholderEntityBulkInsertProviderTests.cs` to use the new registration API and assert typed unsupported-provider failures without metadata descriptor assertions. | x | 2026-07-28 |
| TASK-051 | Update DoFiesta `CoreModule.cs`: configure normal repository behaviors on the repository builder, then register the independent BulkInserter with Cancellation, Tracing, Logging, Metrics, Outbox, AuditState, Concurrency, DomainEvent, and DomainEventMetrics behaviors in the recommended order. | x | 2026-07-28 |
| TASK-052 | Update `docs/features-domain-repositories.md`: remove compatibility analyzer/fallback content and document each BulkInserter-owned behavior, constructor dependencies, recommended order, Outbox transaction semantics, direct-publisher non-atomicity, and explicit separation from repository behaviors. | x | 2026-07-28 |
| TASK-053 | Add `docs/adr/0029-independent-entity-bulk-inserter-decorator-behaviors.md`. Record the independent decorator chain, provided standard behaviors, Outbox-owned transaction, deletion of inference/fallback, and preview-breaking API migration. | x | 2026-07-28 |
| TASK-054 | Change ADR-0028 status to `Superseded`, link ADR-0029, and identify the superseded decisions: repository descriptors, compatibility classification, automatic adaptation, repository fallback, and lifecycle-hook pipeline. Preserve its historical context. | x | 2026-07-28 |
| TASK-055 | Update `docs/adr/README.md`, ADR-0027 related links, `examples/DoFiesta/DoFiesta-README.md`, `README.md`, and `CHANGELOG.md` for the new registration API and provided behavior suite. | x | 2026-07-28 |
| TASK-056 | Review `benchmarks/Application.Benchmarks/EntityBulkInsertPipelineBenchmarks.cs`. Rename the benchmark class/file only if `Pipeline` is misleading after lifecycle-pipeline removal; preserve mapping analysis/build benchmarks and add an opt-in decorator-chain overhead benchmark only if it can run without database I/O. | x | 2026-07-28 |

### Implementation Phase 7

- GOAL-007: Verify the deletion boundary, behavior parity, architecture, transaction atomicity, and provider functionality.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-057 | Run a stale-reference search for `EntityBulkInsertCompatibility`, `EntityBulkInsertExecutionMode`, `EntityBulkInsertExecutionPlan`, `EntityBulkInsertUnsupportedFeaturePolicy`, `IEntityBulkInsertRepositoryBehaviorAdapter`, `IEntityBulkInsertBehavior`, `WithBulkInsertBehavior`, `WithBulkInsert(`, `BehaviorDescriptors`, and deleted registration descriptor types. Allow references only in the superseded historical ADR and old plans. | x | 2026-07-28 |
| TASK-058 | Run `dotnet build` from the repository root. Resolve compile errors caused by preview API deletions without adding compatibility aliases. | x | 2026-07-28 |
| TASK-059 | Run repository-wide unit tests sequentially using `Solution - tests (unit)` or its repository-documented CLI equivalent. | x | 2026-07-28 |
| TASK-060 | Run repository-wide integration tests sequentially using `Solution - tests (integration)` or its repository-documented CLI equivalent. If SQL Server infrastructure is unavailable, record the exact skipped command and run all non-container integration tests. | x | 2026-07-28 |
| TASK-061 | Run targeted SQL Server BulkInserter integration tests and verify one native provider call, AuditState/concurrency/event mutations, atomic root/outbox persistence, owned rollback, caller transaction reuse, placeholder failure, identity options, and inserted-row count. | x | 2026-07-28 |
| TASK-062 | Review the final diff for unrelated formatting, generated artifacts, secrets, and accidental removal of mapping/provider/transaction protections or any requested BulkInserter behavior. | x | 2026-07-28 |

#### Phase 7 verification (2026-07-28)

- The stale-reference search returned no active source, test, example, benchmark, README, changelog, or feature-document references. Historical ADRs and plans retain intentional migration context only.
- `dotnet build --no-restore` succeeded from the repository root with zero warnings and errors.
- The sequential unit suites passed: Application (956), Common (2,128 plus 4 intentional benchmark skips), Domain (550), Infrastructure (263 plus 14 Windows-only skips), and Presentation (309).
- Domain integration tests passed (43). The full Application integration suite failed in unrelated file-monitoring/file-storage scenarios on this host (duplicate/missing filesystem events, POSIX path expectations, and Windows-authentication-only cases) and was stopped after it stalled. The full Infrastructure integration suite was stopped after extended optional-provider/container execution without a terminal summary; neither run reported a BulkInserter failure before stopping.
- `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --no-build --filter FullyQualifiedName~SqlServerEntityBulkInsertProviderTests` passed all 13 SQL Server BulkInserter tests, including provider invocation/count, mapping and identity options, audit/concurrency/domain-event/outbox mutation, rollback, caller transaction reuse, and placeholders.
- `git diff --check` passed. The final status contains only the planned BulkInserter refactor, its tests/docs/ADRs/benchmark rename, and no generated artifacts or secrets.

## 3. Alternatives

- **ALT-001**: Keep the compatibility analyzer and only add decorator behaviors. Rejected because repository inference, adapters, fallback, and independent decorator configuration would create two competing behavior systems.
- **ALT-002**: Keep `IEntityBulkInsertBehavior<TEntity>` lifecycle hooks and rename `.WithBulkInsertBehavior(...)` to `.WithBehavior(...)`. Rejected because this does not match the normal repository decorator model requested for the feature.
- **ALT-003**: Make `EntityBulkInserterBuilderContext<TEntity>` inherit `RepositoryBuilderContext<TEntity>`. Rejected because the two builders decorate different contracts and inheritance would reintroduce behavior-chain ambiguity.
- **ALT-004**: Keep `.WithBulkInsert()` on the repository builder. Rejected because it keeps registration coupled to a normal repository even though fallback and repository behavior inference are removed; it also makes `.WithBehavior(...)` ambiguous after the return type changes.
- **ALT-005**: Add bulk insertion to `IGenericRepository<TEntity>`. Rejected because native bulk insertion remains an optional capability with different tracking, graph, interceptor, and generated-value semantics.
- **ALT-006**: Delete the standard audit/concurrency/domain-event/outbox/observability implementations and provide only custom behavior infrastructure. Rejected because the requested design requires BulkInserter-owned equivalents of the write-relevant normal repository behaviors.
- **ALT-007**: Retain repository fallback behind an option. Rejected because the resolved `IEntityBulkInserter<TEntity>` must have one predictable native contract and must not silently change to tracked row-oriented persistence.
- **ALT-008**: Delete mapping `Analyze(...)` because it contains the word analyzer. Rejected because it is side-effect-free EF mapping preflight, not repository compatibility inference, and it protects the database from unsupported model/graph shapes.
- **ALT-009**: Allow multiple contexts through keyed BulkInserter services. Deferred because it changes the Domain injection contract and requires an explicit selection API.
- **ALT-010**: Keep Outbox persistence inside the terminal inserter. Rejected because Outbox is an optional concern and must be represented by its own BulkInserter behavior.
- **ALT-011**: Let the terminal inserter commit before the Outbox decorator saves. Rejected because root and outbox rows would no longer be atomic. The explicit Outbox decorator owns the encompassing transaction when no caller transaction exists.
- **ALT-012**: Automatically register every standard BulkInserter behavior. Rejected because behavior selection and order must remain explicit, just like normal repository behavior registration.

## 4. Dependencies

- **DEP-001**: `src/Domain/Repositories/IEntityBulkInserter.cs` remains the terminal contract for both implementations and decorators.
- **DEP-002**: Domain already contains `RepositoryBuilderContext<TEntity>` and references the DI/Scrutor functionality needed to implement the matching BulkInserter builder.
- **DEP-003**: `Scrutor.Decorate` and the existing `ServiceCollectionDescriptorExtensions.Find/IndexOf` helpers provide decorator registration behavior.
- **DEP-004**: `EntityBulkInsertMappingBuilder<TEntity>`, `EntityBulkInsertBatch<TEntity>`, and mapping/shadow contracts remain in `Infrastructure.EntityFramework`.
- **DEP-005**: `IEntityBulkInsertProvider` remains the provider-strategy boundary.
- **DEP-006**: SQL Server continues to depend on `Microsoft.Data.SqlClient.SqlBulkCopy`; PostgreSQL and SQLite retain placeholder implementations.
- **DEP-007**: Existing SQL Server integration fixtures and database setup remain the proof for transaction and write behavior.
- **DEP-008**: Reuse `AuditState`, `AuditStateByType`, `ICurrentUserAccessor`, `GuidGenerator`, `EntityCreatedDomainEvent<TEntity>`, `IDomainEventPublisher`, `Metrics`, and activity conventions from existing Domain repository behaviors without depending on those behavior classes.
- **DEP-009**: Reuse `OutboxDomainEventCollector`, `IOutboxDomainEventContext`, `IOutboxDomainEventQueue`, and `OutboxDomainEventOptions` for the EF-specific BulkInserter Outbox decorator.

## 5. Files

### Files to add

- **FILE-001**: `src/Domain/Repositories/EntityBulkInserterBuilderContext.cs` — generic BulkInserter decorator builder.
- **FILE-002**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterBehaviorUtilities.cs` — shared one-time materialization.
- **FILE-003**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterAuditStateBehaviorOptions.cs`.
- **FILE-004**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterAuditStateBehavior.cs`.
- **FILE-005**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterCancellationBehavior.cs`.
- **FILE-006**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterConcurrencyBehavior.cs`.
- **FILE-007**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterDomainEventBehavior.cs`.
- **FILE-008**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterDomainEventMetricsBehavior.cs`.
- **FILE-009**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterDomainEventPublisherBehavior.cs`.
- **FILE-010**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterLoggingBehavior.cs`.
- **FILE-011**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterMetricsBehavior.cs`.
- **FILE-012**: `src/Domain/Repositories/Behaviors/BulkInserter/EntityBulkInserterTracingBehavior.cs`.
- **FILE-013**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInserterOutboxDomainEventBehavior.cs`.
- **FILE-014**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Errors/EntityBulkInsertProviderError.cs` — relocated provider error.
- **FILE-015**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Errors/EntityBulkInsertPreconditionError.cs` — non-compatibility preflight error.
- **FILE-016**: `tests/Domain.UnitTests/Repositories/EntityBulkInserterBuilderContextTests.cs`.
- **FILE-017**: `tests/Domain.UnitTests/Repositories/BulkInserter/EntityBulkInserterBehaviorTests.cs`.
- **FILE-018**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInserterOutboxDomainEventBehaviorTests.cs`.
- **FILE-019**: `docs/adr/0029-independent-entity-bulk-inserter-decorator-behaviors.md`.

### Files to delete

- **FILE-020**: `src/Domain/Repositories/RepositoryBehaviorDescriptor.cs`.
- **FILE-021**: `src/Domain/Repositories/RepositoryBehaviorRegistrationKind.cs`.
- **FILE-022**: `src/Infrastructure.EntityFramework/Repositories/Bulk/DbContextRegistrationDescriptor.cs`.
- **FILE-023**: `src/Infrastructure.EntityFramework/Repositories/Bulk/DbContextInterceptorDescriptor.cs`.
- **FILE-024**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkRepositoryRegistrationDescriptor.cs`.
- **FILE-025**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertConfiguration.cs`.
- **FILE-026**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/EntityBulkInsertShadowValueProviderRegistration.cs`.
- **FILE-027**: Every file under `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/`; relocate `EntityBulkInsertProviderError` first.
- **FILE-028**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/IEntityBulkInsertBehavior.cs`.
- **FILE-029**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBehaviorPipeline.cs`.
- **FILE-030**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBehaviorRegistration.cs`.
- **FILE-031**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBehaviorResolver.cs`.
- **FILE-032**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertContext.cs`.
- **FILE-033**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertFailure.cs`.
- **FILE-034**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertResult.cs`.
- **FILE-035**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBuiltInBehaviors.cs`.
- **FILE-036**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertOutboxBehavior.cs`.
- **FILE-037**: `tests/Domain.UnitTests/Repositories/RepositoryBuilderContextBehaviorDescriptorTests.cs`.
- **FILE-038**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertCompatibilityAnalyzerTests.cs`.
- **FILE-039**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertBehaviorPipelineTests.cs`.
- **FILE-040**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertBuiltInBehaviorTests.cs`.

### Files to modify

- **FILE-041**: `src/Domain/Repositories/RepositoryBuilderContext.cs` — remove compatibility metadata only.
- **FILE-042**: `src/Infrastructure.EntityFramework/Repositories/ServiceCollectionExtensions.cs` — remove repository compatibility descriptors.
- **FILE-043**: `src/Infrastructure.EntityFramework/Repositories/Bulk/ServiceCollectionExtensions.cs` — independent registration, decorator builder, behavior dependencies, and shadow-provider API.
- **FILE-044**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs` — remove fallback policy and core concurrency option.
- **FILE-045**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilder.cs` — remove concurrency assignment while preserving mapping safety.
- **FILE-046**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs` — remove analyzer, fallback, and lifecycle hooks; retain default transaction.
- **FILE-047**: `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/IEntityBulkInsertShadowValueProvider.cs`.
- **FILE-048**: `src/Infrastructure.EntityFramework.SqlServer/ServiceCollectionExtensions.cs`.
- **FILE-049**: `src/Infrastructure.EntityFramework.Postgres/ServiceCollectionExtensions.cs`.
- **FILE-050**: `src/Infrastructure.EntityFramework.Sqlite/ServiceCollectionExtensions.cs`.
- **FILE-051**: `src/Infrastructure.EntityFramework.Cosmos/ServiceCollectionExtensions.cs`.
- **FILE-052**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertServiceCollectionExtensionsTests.cs`.
- **FILE-053**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserterTests.cs`.
- **FILE-054**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilderTests.cs`.
- **FILE-055**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertArchitectureTests.cs`.
- **FILE-056**: `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/SqlServerEntityBulkInsertProviderTests.cs`.
- **FILE-057**: `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/PlaceholderEntityBulkInsertProviderTests.cs`.
- **FILE-058**: `examples/DoFiesta/DoFiesta.Presentation.Web.Server/Modules/Core/CoreModule.cs`.
- **FILE-059**: `examples/DoFiesta/DoFiesta-README.md`.
- **FILE-060**: `docs/features-domain-repositories.md`.
- **FILE-061**: `docs/adr/0028-domain-entity-bulk-insert-behavior-pipeline.md`.
- **FILE-062**: `docs/adr/README.md`.
- **FILE-063**: `docs/adr/0027-provider-strategy-for-entity-bulk-insert.md`.
- **FILE-064**: `README.md`.
- **FILE-065**: `CHANGELOG.md`.
- **FILE-066**: `benchmarks/Application.Benchmarks/EntityBulkInsertPipelineBenchmarks.cs` only if naming or an added decorator-overhead benchmark requires adjustment.

## 6. Testing

- **TEST-001**: Resolve `IEntityBulkInserter<TEntity>` with no behaviors and assert the terminal Entity Framework implementation is invoked once.
- **TEST-002**: Register three type-based BulkInserter behaviors and assert call order is `first before -> second before -> third before -> terminal -> third after -> second after -> first after`.
- **TEST-003**: Repeat TEST-002 with the inner-only factory and service-provider factory overloads.
- **TEST-004**: Return a failed `Result<long>` from an outer behavior and assert inner behaviors, mapping builder, DbContext connection, transaction, and provider are not invoked.
- **TEST-005**: Assert BulkInserter behavior registration does not decorate or resolve `IGenericRepository<TEntity>`.
- **TEST-006**: Assert repository behaviors do not decorate `IEntityBulkInserter<TEntity>` and BulkInserter behaviors do not decorate `IGenericRepository<TEntity>`.
- **TEST-007**: Assert behavior instances use the same scoped/transient lifetime as the terminal BulkInserter and DbContext.
- **TEST-008**: Assert singleton BulkInserter registration throws before adding any service descriptors.
- **TEST-009**: Assert a second BulkInserter registration for the same entity throws and names the entity plus existing registration.
- **TEST-010**: Assert shadow providers resolve in registration order and duplicate implementation types are rejected.
- **TEST-011**: AuditState behavior sets created date and the selected username/email/user-id exactly once, initializes a missing `AuditState`, leaves non-auditable entities unchanged, and forwards the same materialized list once.
- **TEST-012**: Cancellation behavior prevents all downstream enumeration and invocation when canceled.
- **TEST-013**: Concurrency behavior assigns one non-empty sequential version per `IConcurrency` entity; the mapping builder does not assign a second version.
- **TEST-014**: DomainEvent behavior registers one created event per aggregate and none for non-aggregate entities.
- **TEST-015**: DomainEventMetrics behavior counts newly created events only when it is registered after DomainEvent behavior in the chain.
- **TEST-016**: DomainEventPublisher behavior publishes and clears after inner success, retains events after inner failure, returns publishing failure without retrying the insert, and is documented as non-atomic.
- **TEST-017**: Logging behavior emits start/success/failure without entity values, connection strings, event payloads, or audit identities.
- **TEST-018**: Metrics behavior records total/current/failure/duration exactly once for success, failed result, exception, and cancellation.
- **TEST-019**: Tracing behavior creates one activity with safe tags and correct success/error/cancellation status.
- **TEST-020**: Multiple built-in behaviors enumerate a generator once in total and every inner decorator plus terminal receives the same normalized list instance.
- **TEST-021**: Outbox behavior with no active transaction opens/begins once, invokes inner once, saves outbox rows, commits once, clears events, and enqueues immediate ids only after commit.
- **TEST-022**: Outbox behavior rolls back root and outbox writes on inner failed result, inner exception, cancellation, collector/serialization failure, and outbox `SaveChangesAsync` failure.
- **TEST-023**: Outbox behavior under a caller-owned transaction does not commit/rollback/dispose, does not clear events, and does not enqueue immediate ids.
- **TEST-024**: Recommended behavior order proves observability completion happens after Outbox commit and entity/event mutation happens before the terminal native write.
- **TEST-025**: Assert empty/null-filtered input returns success zero without provider, connection, or transaction work.
- **TEST-026**: Assert exact provider selection returns typed failures for missing, duplicate, and unsupported providers.
- **TEST-027**: Assert mapping preflight still rejects tracked entities, duplicate references, unsupported graphs, JSON/separate-table ownership, TPT/TPC, and missing required shadow values.
- **TEST-028**: Assert ambient transactions and unsafe retry strategies return `EntityBulkInsertPreconditionError` before provider execution.
- **TEST-029**: Assert provider exceptions return `EntityBulkInsertProviderError` and whichever component owns the transaction rolls it back.
- **TEST-030**: Assert cancellation is rethrown and owned transactions roll back with `CancellationToken.None` cleanup.
- **TEST-031**: Assert caller-owned EF transactions are reused and never committed, rolled back, or disposed by the terminal or Outbox behavior.
- **TEST-032**: Assert 10,000 entities result in one `IEntityBulkInsertProvider.InsertAsync(...)` call and no repository call.
- **TEST-033**: SQL Server integration test inserts rows using AuditState, Concurrency, DomainEvent, and Outbox behaviors and verifies root/outbox atomicity.
- **TEST-034**: SQL Server integration test inserts rows using batch size, timeout, `TableLock`, identity-preserve on/off, converters, same-table owned values, and exact count.
- **TEST-035**: SQL Server integration test forces provider and outbox failures after writing and proves zero root/outbox rows remain.
- **TEST-036**: PostgreSQL and SQLite placeholder integration tests return their explicit unsupported reason without attempting mapping or connection work.
- **TEST-037**: Architecture test proves no source type remains under `.Bulk.Compatibility`, no `IEntityBulkInsertBehavior<TEntity>` exists, the terminal has no repository/analyzer/resolver dependency, and every requested standard behavior implements `IEntityBulkInserter<TEntity>`.
- **TEST-038**: Stale-reference search proves removed public types and methods appear only in the superseded ADR and historical plans.
- **TEST-039**: Repository-wide build, unit tests, and integration tests pass sequentially.

## 7. Risks & Assumptions

- **RISK-001**: The standard BulkInserter behaviors are provided but remain opt-in. Audit, concurrency, events, outbox, logging, metrics, and tracing do not run unless their BulkInserter-owned decorators are explicitly registered.
- **RISK-002**: Outbox atomicity depends on decorator order. The Outbox behavior must wrap AuditState/Concurrency/DomainEvent behaviors and the terminal inserter, while observability behaviors should wrap Outbox. The recommended order and an integration test are mandatory.
- **RISK-003**: Provider-native writes bypass EF `SaveChanges` and command interceptors. Removing analysis makes this an explicit caller-owned trade-off rather than a dynamically rejected configuration.
- **RISK-004**: Deleting `.WithBulkInsert()` is a source break for preview consumers. Stale-reference checks, examples, changelog, and ADR updates mitigate accidental incomplete migration.
- **RISK-005**: Moving behavior registration into Domain adds another public builder type. The builder must remain provider-neutral and reference only the Domain contract plus DI/Scrutor abstractions.
- **RISK-006**: Deleting registration metadata reduces duplicate-context diagnostics. The replacement registration must reject any second unkeyed entity contract immediately and include enough service descriptor information for diagnosis.
- **RISK-007**: Mapping preflight currently uses the method name `Analyze`. An overbroad deletion could remove model/graph safety together with compatibility analysis; architecture and mapping tests must guard this boundary.
- **RISK-008**: Audit, concurrency, and domain-event behaviors mutate in-memory entities before the provider call. Rollback cannot undo these object mutations; document the behavior and retain events until a successful Outbox-owned commit.
- **RISK-009**: Direct domain-event publication happens after persistence and is not atomic. The publisher behavior must be explicitly documented as an alternative to Outbox and must not be combined with it.
- **RISK-010**: Every behavior that needs entity count or values can accidentally enumerate a streaming source. The shared materialization helper and generator-based tests must prove one enumeration across the complete built-in chain.
- **ASSUMPTION-001**: The BulkInserter remains a preview feature, so compatibility aliases and deprecation periods are not required.
- **ASSUMPTION-002**: The requested “own behavior” means the same Decorator pattern as repository behaviors: each behavior implements the complete feature contract and wraps an inner implementation.
- **ASSUMPTION-003**: Automatic copying of normal repository behaviors is intentionally removed, not replaced with a different inference mechanism.
- **ASSUMPTION-004**: Repository fallback is intentionally removed; callers needing repository semantics call `IGenericRepository<TEntity>.InsertSetAsync` directly.
- **ASSUMPTION-005**: Mapping preflight, provider dispatch, and transaction safety are core BulkInserter responsibilities and remain in scope.
- **ASSUMPTION-006**: PostgreSQL and SQLite native provider implementations remain outside this refactor.
- **ASSUMPTION-007**: “AuditState, Concurrency, DomainEventBehavior, etc.” includes the complete write-relevant standard set: AuditState, Cancellation, Concurrency, DomainEvent creation/metrics/publishing, Logging, Metrics, Tracing, and EF Outbox.

## 8. Related Specifications / Further Reading

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `plan/pln-redesign-entity-framework-bulk-insert-behavior-pipeline-1.md`
- `plan/pln-refactor-entity-bulk-insert-provider-abstraction-1.md`
- `docs/adr/0001-clean-onion-architecture.md`
- `docs/adr/0004-repository-decorator-behaviors.md`
- `docs/adr/0007-entity-framework-core-code-first-migrations.md`
- `docs/adr/0018-dependency-injection-service-lifetimes.md`
- `docs/adr/0027-provider-strategy-for-entity-bulk-insert.md`
- `docs/adr/0028-domain-entity-bulk-insert-behavior-pipeline.md`
- `docs/features-domain-repositories.md`
- `src/Domain/Repositories/RepositoryBuilderContext.cs`
- `src/Application.Storage/Documents/DocumentStoreBuilderContext.cs`
- `src/Domain/Repositories/IEntityBulkInserter.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs`
