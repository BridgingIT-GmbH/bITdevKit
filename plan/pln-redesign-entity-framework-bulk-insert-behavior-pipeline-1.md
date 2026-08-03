---
goal: Redesign Entity Framework bulk insert as a Domain capability with explicit behavior compatibility
version: 1.1
date_created: 2026-07-17
last_updated: 2026-07-22
owner: bITdevKit
status: 'In progress'
tags: [refactor, architecture, entity-framework, repositories, bulk-insert, compatibility]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In_progress-yellow)

This plan moves `IEntityBulkInserter<TEntity>` to `BridgingIT.DevKit.Domain.Repositories`, retains `SqlBulkCopy` behind infrastructure abstractions, and adds an explicit compatibility analyzer plus a dedicated bulk-behavior pipeline. The feature is preview and has no consumers, so the existing infrastructure interface is deleted in the same change; no obsolete forwarder, type-forwarding shim, namespace alias, or dual DI registration is added. Every configured repository behavior and EF persistence feature is classified before database work as native-equivalent, insert-irrelevant, explicit repository fallback, or rejection. Fallback is opt-in and observable.

Repository findings were refreshed at commit `ece8e706` on branch `feature/repository-insert-range` on 2026-07-21. The branch no longer contains the earlier ChangeHistory repository behavior, so ChangeHistory is not part of the current compatibility inventory. The implementation task must repeat the inventory search before coding and add any newly introduced behavior to the matrix before enabling native execution.

## 1. Requirements & Constraints

- **REQ-001**: Add `BridgingIT.DevKit.Domain.Repositories.IEntityBulkInserter<TEntity>` with `Task<Result<long>> InsertAsync(IEnumerable<TEntity>, CancellationToken)`; Domain must not reference EF Core, SqlClient, or infrastructure options.
- **REQ-002**: Delete `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInserter.cs` and update all repository-owned consumers in the same change. This preview API has no external consumers and receives no compatibility bridge.
- **REQ-003**: Keep `EntityFrameworkEntityBulkInserter<TEntity,TContext>`, `IEntityBulkInsertProvider`, EF metadata mapping, transaction orchestration, and provider options in infrastructure packages.
- **REQ-004**: Keep bulk insertion as a separate optional capability; do not add it to `IGenericRepository<TEntity>`.
- **REQ-005**: Preserve one provider invocation and one `SqlBulkCopy.WriteToServerAsync` invocation per operation. `BatchSize` may split the wire transfer internally but must not cause repository or EF insertion per entity.
- **REQ-006**: Record all configured repository behaviors in deterministic order. Compatibility discovery must work when `.WithBulkInsert()` appears before later `.WithBehavior(...)` calls.
- **REQ-007**: Analyze behaviors, interceptors, provider, model shape, graph values, tracking state, transaction state, retry strategy, and registration ambiguity before mutating entities or opening a connection.
- **REQ-008**: Default `UnsupportedFeaturePolicy` to `Fail`. Permit `RepositoryFallback` only through explicit bulk options; tag fallback in the result, structured log, metric, and activity.
- **REQ-009**: Fallback calls the fully decorated `IGenericRepository<TEntity>.InsertSetAsync` exactly once and runs no bulk behaviors, preventing duplicate audit, events, metrics, or outbox work.
- **REQ-010**: Automatically provide bulk equivalents for configured audit, cancellation, concurrency, logging, metrics, tracing, domain-event creation, domain-event metrics, and repository outbox behaviors.
- **REQ-011**: Mark include, include-path, order, specification, no-tracking, and soft-delete decorators as insert-irrelevant only because their current `InsertSetAsync` implementations forward without write-side logic.
- **REQ-012**: Direct domain-event publication requires explicit repository fallback or rejection. Native bulk does not claim atomic publication semantics.
- **REQ-013**: Native outbox support writes root rows and outbox rows in one database transaction and clears domain events only after successful completion.
- **REQ-014**: Native mode is root-table-only. Allow scalar properties and non-JSON owned references mapped to the same table. Reject populated non-owned navigations, populated owned collections, separate-table owned types, JSON ownership, TPT, TPC, and other multi-table writes before provider execution.
- **REQ-015**: Support table/schema mapping, EF value converters, numeric/string enum provider values, supported client-generated GUID keys, same-table owned references, TPH discriminator values, and deterministic shadow values.
- **REQ-016**: Reject required writable shadow properties without an EF metadata constant or registered `IEntityBulkInsertShadowValueProvider<TEntity>`.
- **REQ-017**: Native entities remain detached. Reject instances already tracked by the active context. Do not hydrate database-generated identity, default, computed, or row-version values.
- **REQ-018**: `KeepGeneratedIdentityValues` includes only actual identity columns and applies `SqlBulkCopyOptions.KeepIdentity`; it must not include every `ValueGenerated.OnAdd` property.
- **REQ-019**: The dispatcher creates or joins one EF transaction above the provider. It must never expose `UseInternalTransaction` on the public native path.
- **REQ-020**: Reuse `Database.CurrentTransaction` without committing or rolling back caller-owned transactions. Reject `System.Transactions.Transaction.Current` in the first release before opening the connection.
- **REQ-021**: Do not automatically retry native bulk. Reject an inserter-owned operation when `CreateExecutionStrategy().RetriesOnFailure` is true; caller-owned retry scopes own idempotency.
- **REQ-022**: Roll back an inserter-owned transaction on provider, transactional postbehavior, or cancellation failure. Rethrow `OperationCanceledException` after rollback and failure hooks.
- **REQ-023**: Prevent partial completion across `BatchSize` boundaries by binding `SqlBulkCopy` to the one external EF transaction.
- **REQ-024**: Preserve exact provider selection from `DbContext.Database.ProviderName`, deterministic missing/duplicate-provider errors, `BatchSize`, `CommandTimeout`, and supported `SqlBulkCopyOptions`.
- **REQ-025**: Materialize once, treat a null enumerable as empty, filter null elements, return success zero for an empty batch, and reject duplicate object references by reference equality.
- **REQ-026**: Repeated `.WithBulkInsert()` is idempotent only for the same entity, context, and equivalent options. Reject conflicting options or multiple contexts because the unkeyed Domain interface cannot select one deterministically.
- **REQ-027**: Support scoped and transient lifetimes aligned with the DbContext. Reject singleton registration because `DbContext` is not thread-safe.
- **REQ-028**: Add XML documentation and usage examples to every new public type/member. Update ADRs, feature guides, samples, README, and `CHANGELOG.md`.
- **SEC-001**: Never log entities, audit identities, connection strings, SQL row values, or event payloads. Log only operation id, entity type, count, provider, execution mode, behavior classification, and transaction ownership.
- **CON-001**: PostgreSQL and SQLite placeholder providers remain explicit not-supported strategies; native writers are outside this plan.
- **CON-002**: Do not introduce a commercial or cross-provider bulk library.
- **CON-003**: Run repository-wide build and test commands sequentially to avoid shared `obj/ref` races.
- **PAT-001**: Preserve Clean Architecture direction: Application -> Domain contract -> EF dispatcher -> provider contract -> SQL Server provider.
- **PAT-002**: Preserve decorator ordering: before hooks use repository registration order; after/failure hooks use reverse order.

### A. Current-state analysis

1. `AddEntityFrameworkRepository<TEntity,TContext>` registers the first `IGenericRepository<TEntity>` with `TryAdd*` and returns an `EntityFrameworkRepositoryBuilderContext<TEntity,TContext>` (`src/Infrastructure.EntityFramework/Repositories/ServiceCollectionExtensions.cs:15-42`). The mapped `TEntity,TDatabaseEntity,TContext` overload has the same builder shape but maps to a different EF entity (`:44-78`).
2. `RepositoryBuilderContext<TEntity>` stores decorators only as private `Action<IServiceCollection>` instances and reverses them during Scrutor rebuilding (`src/Domain/Repositories/RepositoryBuilderContext.cs:23,113-169,172-212`). It exposes no behavior metadata to bulk registration.
3. `.WithBulkInsert(options)` uses `TryAdd` for closed configuration, mapping builder, and the infrastructure interface with the repository lifetime (`src/Infrastructure.EntityFramework/Repositories/Bulk/ServiceCollectionExtensions.cs:40-76`). First registration wins silently for the same entity.
4. SQL Server `Add*DbContext<TContext>` overloads register the stateless provider as singleton (`src/Infrastructure.EntityFramework.SqlServer/ServiceCollectionExtensions.cs:31,58,143,236-239`). PostgreSQL and SQLite register placeholder strategies.
5. `EntityFrameworkEntityBulkInserter.InsertAsync` materializes/filter-null input, returns zero when empty, validates options, selects an exact provider, builds mapping, invokes the provider once, rethrows cancellation, and converts other exceptions to `ExceptionError` (`src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs:79-118`). It never resolves the repository or decorators.
6. `EntityBulkInsertMappingBuilder.Build` discovers table/schema, rejects every configured non-owned navigation regardless of runtime value, rejects only populated owned collections, mutates GUID/concurrency values, flattens same-table owned references, omits all shadow properties, and applies converters (`src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilder.cs:54-106,109-166,168-241`). It does not explicitly validate JSON ownership, separate-table null ownership, inheritance, required shadows, or tracked instances.
7. `ShouldInclude` omits `OnAddOrUpdate` and includes any `OnAdd` property when `KeepGeneratedIdentityValues` is true (`EntityBulkInsertMappingBuilder.cs:216-225`), which is broader than identity preservation.
8. `SqlServerEntityBulkInsertProvider` creates a `DataTable`, opens/closes `SqlConnection` directly, creates one `SqlBulkCopy`, and calls `WriteToServerAsync` once (`src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs:56-103`). It reuses `CurrentTransaction`; otherwise it adds `UseInternalTransaction` (`:132-150`). With a nonzero batch size, that cannot guarantee operation-wide atomicity or include outbox rows.
9. Normal `EntityFrameworkGenericRepository<TEntity>.InsertSetAsync` assigns configured concurrency versions, calls `DbSet.AddRange`, optionally calls `SaveChangesAsync`, and returns tracked entities (`src/Infrastructure.EntityFramework/Repositories/EntityFrameworkGenericRepository{TEntity}.cs:114-142`). The mapped overload converts to `TDatabaseEntity` and maps generated values back after save (`EntityFrameworkGenericRepository{TEntity,TDatabaseEntity}.cs:98-127`). These semantics define explicit fallback, not native behavior.
10. The repository behavior inventory at this commit is audit, cancellation, concurrency, domain-event creation, domain-event metrics, direct publisher, logging, metrics, tracing, include, include-path, order, specification, no-tracking, soft-delete, and read-only logging. The EF repository outbox behavior is separate. No repository validation, write authorization, tenant-write, or ChangeHistory behavior exists at this commit; unknown custom decorators must therefore fail or fallback.
11. Audit mutates `AuditState.SetCreated` before inner insertion (`RepositoryAuditStateBehavior.cs:260-272`). Domain-event creation registers one `EntityCreatedDomainEvent<TEntity>` before inner insertion (`RepositoryDomainEventBehavior.cs:221-237`). Direct publisher publishes and clears after inner insertion (`RepositoryDomainEventPublisherBehavior.cs:227-241`).
12. Outbox calls the inner insert first, then serializes and optionally saves each entity's events, queues immediate ids, and clears events (`src/Infrastructure.EntityFramework/Repositories/Outbox/RepositoryOutboxDomainEventBehavior.cs:243-256,306-337`). It is atomic only when an outer transaction already covers both saves; native bulk currently bypasses it entirely.
13. DevKit SQL Server, PostgreSQL, SQLite, and Cosmos options register interceptor types, including `CommandLoggerInterceptor`, through provider `ServiceCollectionExtensions`. Direct `SqlBulkCopy` bypasses EF command and SaveChanges interception; its direct connection operations also bypass EF connection/transaction interception.
14. Existing unit tests cover empty input, provider selection/failure, cancellation, lifetimes, mapping, converters, identity, same-table owned references, generated GUIDs, concurrency, and navigation rejection. SQL Server integration tests cover native rows, active EF transactions, identity, provider registration, options, and placeholder failures. Missing coverage includes behavior parity, outbox atomicity, cross-batch rollback, interceptor classification, tracking, inheritance, shadows, retries, and explicit fallback.

Current classification:

- **Executed directly**: input normalization, option validation, exact provider selection, table/schema lookup, primitive/typed GUID generation, concurrency assignment, same-table owned flattening, converters, one provider call, batch/timeout/options, current EF transaction reuse, cancellation propagation, and exception-to-result conversion.
- **Partially supported**: owned types, generated values, defaults/computed/rowversion omission, enums, transaction atomicity, and mapping validation.
- **Bypassed**: all repository decorators, audit current-user population, domain events, outbox, repository observability, EF tracking/cascades, `SaveChanges`, and EF interceptors.
- **Unsupported or unproven**: shadow tenant/discriminator values, inheritance, JSON/separate-table ownership, aggregate graphs, tracked input, ambient transactions, retries, duplicate instances, multiple contexts, and custom behaviors/interceptors.

### B. Compatibility matrix

| Feature or behavior | Normal repository | Current bulk | Required target | Proposed implementation | Mode | Required proof |
|---|---|---|---|---|---|---|
| Domain abstraction | Domain repository contract | Infrastructure interface | Domain-only injection | Add Domain interface; delete unused preview interface | Native | Architecture/DI compile tests |
| Audit/current user | `SetCreated(GetByValue())` | Bypassed | Equivalent when configured | Automatic bulk audit behavior using same options/accessor | Native | Username/email/id SQL tests |
| Concurrency decorator/base repository | Configured generator before insert | Global sequential GUID | Assign exactly once | Closed config carries repository generator; semantic deduplication | Native | One generator call/entity |
| Cancellation decorator | Early check then forward | Provider token only | Check before all expensive work | Core early check plus compatible adapter classification | Native | Pre/mid-write cancellation |
| Logging | Per-entity repository log | Aggregate debug logs | Safe operation log | Bulk logging behavior; never log payload | Native | Structured log assertions |
| Metrics | Tracks insert-set | Bypassed | Bulk count/duration/failure | Bulk metric behavior with mode/provider tags | Native | Meter listener tests |
| Tracing | Repository activity | Bypassed | One bulk activity | Bulk tracing behavior around full operation | Native | Activity order/status |
| Domain-event creation | Registers created event before insert | Bypassed | Register once in configured order | Automatic prebehavior | Native | Event count/order |
| Domain-event metrics | Reads events before inner | Bypassed | Preserve descriptor order | Automatic prebehavior | Native | Creation-before/after metrics |
| Direct event publisher | Publishes after save and clears | Bypassed | No false atomicity | Default rejection; explicit decorated-repository fallback | Fallback/reject | Provider-not-called test |
| Repository outbox | Stores after inner; outer tx needed for atomicity | Bypassed | Root and outbox atomic | Bulk postwrite behavior inside owned transaction | Native | Real SQL rollback tests |
| Include/include-path | Query shaping; insert forwards | Bypassed | Insert-irrelevant | Known descriptors classified `InsertNoOp` | Native | Classification tests |
| Order/specification | Query shaping; insert forwards | Bypassed | Insert-irrelevant | Known descriptors classified `InsertNoOp` | Native | Classification tests |
| No-tracking | Read option; insert forwards | Always detached | Explicit detached contract | `InsertNoOp`; reject tracked input | Native | ChangeTracker assertions |
| Soft-delete | Read/delete logic; insert forwards | Bypassed | No insert action | `InsertNoOp` | Native | Classification test |
| Read-only logging | Read-only repository only | Not involved | Not applicable | Exclude from write descriptors | Native | Registration test |
| Custom validation/access behavior | Custom decorator possible | Silently bypassed | Never bypass | Require explicit adapter or fallback | Fallback/reject | Deny/validation tests |
| ActiveEntity behaviors | Separate pipeline | Bypassed | No implied parity | Explicit bulk behavior or fallback when declared | Fallback/reject | Classification test |
| Tenant CLR value | EF writes CLR property | Written if mapped | Require deterministic value | Caller/bulk behavior supplies value; preflight validates | Native | Tenant SQL test |
| Required shadow/tenant value | EF/interceptor may supply | Always omitted | Deterministic source | Metadata constant or shadow-value provider | Native/reject | Missing/provider tests |
| Query filter | Does not filter insert | Ignored | Insert-irrelevant; validate discriminator | Analyze referenced write values | Native/reject | Tenant/filter test |
| SaveChanges interceptor | Can mutate/veto/save related rows | Never runs | No silent bypass | Known explicit adapter or repository fallback; unknown rejects | Fallback/reject | Custom interceptor tests |
| Command interceptor/logger | Observes EF commands | SqlBulkCopy bypasses | Known equivalent only | Bulk logging substitutes built-in logger; unknown falls back/rejects | Native/fallback | Known/custom tests |
| Query/materialization interceptor | Query-only | Not invoked | Insert-irrelevant | Known interface types classified `InsertNoOp` | Native | Classification test |
| Connection/transaction interceptor | EF APIs invoke | Direct connection bypass | Preserve callbacks | Dispatcher opens/begins/commits through EF APIs | Native | Callback tests |
| Raw non-DevKit DbContext setup | Arbitrary interceptor/options | No provenance | Never assume safety | Missing registration descriptor causes fallback/reject | Fallback/reject | Raw setup test |
| EF tracking | `AddRange` tracks/fixups | Detached | Remain detached | Reject active tracked instances; never attach | Native | State tests |
| Same-table owned reference | Table splitting | Flattened | Preserve | Explicit same-store/non-JSON validation | Native | Null/value SQL tests |
| JSON-owned reference | EF serializes JSON | Unproven/omitted | Reject first release | Detect JSON container mapping | Reject/fallback | No-write test |
| Separate-table owned reference | EF inserts dependent | Silently ignored | Reject first release | Detect multiple store objects | Reject/fallback | No-write test |
| Owned collection | EF cascades rows | Rejects only populated | Reject populated graph | Value-sensitive graph analyzer | Reject/fallback | Empty/populated tests |
| Non-owned navigation/cascade | EF traverses graph | Rejects even null/empty | Root-only | Allow null/empty; reject populated | Reject/fallback | Ref/collection tests |
| TPH discriminator | EF supplies shadow value | Omitted | Support | Constant accessor from EF metadata | Native | Base/derived SQL tests |
| TPT/TPC/multi-table | EF emits multiple writes | Unvalidated | Reject first release | Store-object count analysis | Reject/fallback | No-partial-write tests |
| Value converter | EF converts | Converter applied | Preserve | Provider-value accessor | Native | Custom converter SQL test |
| Enum | Numeric/string provider mapping | Only proven with converter | Provider CLR representation | Converter or underlying provider normalization | Native | Numeric/string tests |
| Table/schema mapping | EF relational metadata | Supported | Preserve | Store-object metadata + provider quoting | Native | Escaped schema/table test |
| GUID/typed GUID key | EF generator | Sequential assigned | Preserve configured generator | Preprocessor after compatibility check | Native | Primitive/typed tests |
| Identity/KeepIdentity | EF generates/hydrates | Omit or preserve | Preserve option; no hydration | Include identity only; provider adds `KeepIdentity` | Native | On/off SQL tests |
| Default/computed column | EF omits/hydrates | Omitted, not returned | Omit; no hydration | Explicit store-generation classification | Native | Fresh-context assertions |
| Rowversion | EF generates/hydrates | Omitted | Omit; no hydration | Explicit metadata classification | Native | DB/entity state test |
| Required unsatisfied column | EF may supply value | May omit incorrectly | Fail before write | Nullability/default/generation preflight | Reject/fallback | Constraint preflight |
| Active EF transaction | Save enlists | Reused for SQL transaction | Reuse; caller owns outcome | Dispatcher joins `CurrentTransaction` | Native | Commit/rollback tests |
| No active transaction | Save is call-atomic | Internal transaction per batch | One operation transaction | Dispatcher begins EF transaction around provider/outbox | Native | Later-batch rollback |
| Ambient `TransactionScope` | EF may enlist | Unproven | Deterministic first release | Detect and reject | Reject/fallback | Unit test |
| Execution strategy/retry | EF retry unit | Not used explicitly | Avoid replay | Reject owned operation when retries enabled | Reject | Fake strategy test |
| Provider error/result | Repository extensions convert | `ExceptionError` | Stage-aware result | Typed compatibility/provider/postcommit errors | Native/fallback | Exact error tests |
| Provider selection | N/A | Exact name | Preserve | Keep strategy registry | Native | Existing tests |
| Batch/timeout/options | EF batching | Passed to SqlBulkCopy | Preserve | Neutral + SQL-derived options | Native | Factory/options tests |
| Multiple contexts/entity | First `TryAdd` wins | Silent ambiguity | Reject | Registration descriptors validate conflicts | Reject | DI tests |
| Lifetime | Scoped/transient/singleton accepted | Mirrors lifetime | Scoped/transient only | Registration validation | Native | Lifetime tests |
| Empty/null input | Empty result | Success zero | Preserve | Early return before resolution/hooks | Native | Existing tests |
| Null elements | Filtered | Filtered | Preserve | Materialize/filter once | Native/fallback | Count test |
| Duplicate instance | Tracking semantics vary | Written twice | Reject | Reference-equality preflight | Reject | Provider-not-called test |
| Mapped `TDatabaseEntity` repository | Maps and maps back | Looks for wrong model type | Preserve only via repository | Registration descriptor requires fallback | Fallback/reject | Mapper fallback test |
| PostgreSQL/SQLite placeholders | Normal EF works | Explicit not supported | Preserve explicit status | Keep providers unchanged | Reject/fallback | Placeholder tests |

### C. Target architecture

#### C.1 Dependency direction and ownership

```text
Application handler
  -> BridgingIT.DevKit.Domain.Repositories.IEntityBulkInserter<TEntity> [Domain]
  -> EntityFrameworkEntityBulkInserter<TEntity,TContext>                [Infrastructure.EntityFramework]
     -> compatibility analyzer + bulk behavior pipeline                 [Infrastructure.EntityFramework]
     -> IEntityBulkInsertProvider                                       [Infrastructure.EntityFramework]
  -> SqlServerEntityBulkInsertProvider                                  [Infrastructure.EntityFramework.SqlServer]
  -> Microsoft.Data.SqlClient.SqlBulkCopy                               [Microsoft.Data.SqlClient]
```

Domain owns only the application-facing operation and provider-neutral repository behavior registration metadata. Infrastructure owns EF configuration, behavior adapters, mapping, execution context, results used inside the pipeline, transaction policy, and provider contracts.

#### C.2 Public contracts

```csharp
namespace BridgingIT.DevKit.Domain.Repositories;

public interface IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    Task<Result<long>> InsertAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryBehaviorDescriptor(
    Type BehaviorType,
    int RegistrationOrder,
    RepositoryBehaviorRegistrationKind RegistrationKind);
```

`RepositoryBuilderContext<TEntity>` exposes an append-only `IReadOnlyList<RepositoryBehaviorDescriptor>`. Each `WithBehavior` overload records a descriptor before rebuilding Scrutor decorators. Factory registrations retain their declared `TBehavior`; unknown runtime semantics remain unsupported until adapted explicitly.

```csharp
namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public interface IEntityBulkInsertBehavior<TEntity>
    where TEntity : class, IEntity
{
    int Order { get; }
    ValueTask<Result> BeforeInsertAsync(EntityBulkInsertContext<TEntity> context, CancellationToken cancellationToken);
    ValueTask<Result> AfterWriteAsync(EntityBulkInsertContext<TEntity> context, EntityBulkInsertResult result, CancellationToken cancellationToken);
    ValueTask<Result> AfterInsertAsync(EntityBulkInsertContext<TEntity> context, EntityBulkInsertResult result, CancellationToken cancellationToken);
    ValueTask OnInsertFailedAsync(EntityBulkInsertContext<TEntity> context, EntityBulkInsertFailure failure, CancellationToken cancellationToken);
}

public sealed record EntityBulkInsertResult(
    long InsertedCount,
    EntityBulkInsertExecutionMode ExecutionMode,
    bool TransactionOwned,
    bool IsCommitted);

public enum EntityBulkInsertUnsupportedFeaturePolicy
{
    Fail = 0,
    RepositoryFallback = 1
}
```

`EntityBulkInsertContext<TEntity>` contains an operation id, read-only entity list, EF mapping analysis, execution mode, transaction ownership, and an operation-local keyed state dictionary. `EntityBulkInsertFailure` contains stage, primary errors, exception when present, cancellation flag, and committed flag. These infrastructure types have no SQL Server dependency.

`BeforeInsertAsync` runs ascending by `Order`; `AfterWriteAsync`, `AfterInsertAsync`, and failure hooks run descending. A failed `Result` short-circuits. Failure-hook exceptions are appended without replacing the primary error. Built-in observability hooks are non-throwing. Semantic keys prevent automatic and explicit versions of audit, concurrency, events, or outbox from running twice.

```csharp
public interface IEntityBulkInsertRepositoryBehaviorAdapter<TEntity>
    where TEntity : class, IEntity
{
    bool CanAdapt(RepositoryBehaviorDescriptor descriptor);
    EntityBulkInsertBehaviorRegistration CreateRegistration(RepositoryBehaviorDescriptor descriptor);
}

public interface IEntityBulkInsertShadowValueProvider<TEntity>
    where TEntity : class, IEntity
{
    bool TryGetValue(EntityBulkInsertShadowPropertyContext<TEntity> context, out object value);
}
```

The compatibility analyzer returns one immutable `EntityBulkInsertExecutionPlan` containing classifications, mapping analysis, ordered behaviors, provider, transaction policy, and either native, fallback, or rejected mode. Unknown behavior/interceptor types are never classified as no-op.

#### C.3 Provider and registration contracts

Keep `IEntityBulkInsertProvider.InsertAsync<TEntity>(DbContext, EntityBulkInsertBatch<TEntity>, CancellationToken)`. Add a documented relational precondition: the dispatcher supplies an active transaction. SQL Server rejects a missing transaction, removes its `UseInternalTransaction` path, and stays stateless.

```csharp
public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext> WithBulkInsert<TEntity, TContext>(
    this EntityFrameworkRepositoryBuilderContext<TEntity, TContext> context,
    EntityBulkInsertOptions options = null)
    where TEntity : class, IEntity
    where TContext : DbContext;

public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext> WithBulkInsertBehavior<TEntity, TContext, TBehavior>(
    this EntityFrameworkRepositoryBuilderContext<TEntity, TContext> context,
    int order,
    string semanticKey = null)
    where TBehavior : class, IEntityBulkInsertBehavior<TEntity>;

public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext> WithBulkInsertShadowValueProvider<TEntity, TContext, TProvider>(
    this EntityFrameworkRepositoryBuilderContext<TEntity, TContext> context)
    where TProvider : class, IEntityBulkInsertShadowValueProvider<TEntity>;
```

Explicit bulk behaviors require an integer order. Ties use DI registration sequence. Their semantic key defaults to the implementation type name and can be supplied explicitly when replacing built-in semantics. Duplicate semantic keys fail configuration validation.

### D. Behavior execution sequence

1. Throw when the cancellation token is already canceled.
2. Materialize once, filter nulls, return success zero for empty input, and reject duplicate references.
3. Resolve the single entity/context registration and exact provider.
4. Run side-effect-free compatibility preflight across repository descriptors, registered interceptors, provider/options, repository kind, tracking state, model/store objects, graph values, shadows, store generation, transaction state, ambient transaction, and retry strategy.
5. If preflight selects explicit fallback, emit fallback result/log/metric/activity metadata, call the fully decorated `InsertSetAsync(items, token)` once, and return its materialized count. Resolve no bulk behaviors.
6. If preflight rejects, return one `EntityBulkInsertCompatibilityError` listing every incompatible feature and remedy before mutation or connection work.
7. Resolve automatic and explicit bulk behaviors, deduplicate semantic keys, and sort by order then DI sequence.
8. Run before hooks ascending. On failure, run failure hooks only for entered behaviors, descending.
9. Apply core client GUID and configured concurrency generation exactly once, then finalize provider-value accessors so audit mutations are visible.
10. Open the connection through EF. Reuse `CurrentTransaction`; otherwise verify retries are disabled, begin one EF transaction, and mark it owned. Reject ambient `TransactionScope`.
11. Invoke the provider once. SQL Server binds one `SqlBulkCopy` to the active `SqlTransaction`, applies batch size, timeout, allowed options, mappings, and calls `WriteToServerAsync` once.
12. Run `AfterWriteAsync` descending inside the owned transaction. Outbox serializes all events, adds all outbox rows, and performs one filtered `SaveChangesAsync<OutboxDomainEvent>`.
13. Commit only an inserter-owned transaction. Never commit or roll back a caller-owned transaction on success.
14. Clear outboxed domain events after successful owned commit. Queue immediate outbox ids only after owned commit; with caller-owned transactions rely on interval polling to avoid enqueue-before-commit.
15. Run `AfterInsertAsync` descending. A custom error after owned commit returns `EntityBulkInsertPostCommitError` with `IsCommitted=true`.
16. Return `Result<long>.Success(insertedCount)` plus non-error execution messages.
17. On precommit failure, roll back/dispose only the owned transaction, run failure hooks descending, retain the primary error, and rethrow cancellation after cleanup.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Establish the single Domain contract and behavior registration metadata.

| Task | Description | Completed | Date |
|---|---|---|---|
| TASK-001 | Add `src/Domain/Repositories/IEntityBulkInserter.cs` with the C.2 contract, XML documentation, and application-only usage example. | ✅ | 2026-07-21 |
| TASK-002 | Add `RepositoryBehaviorDescriptor.cs` and `RepositoryBehaviorRegistrationKind.cs`; modify all `RepositoryBuilderContext<TEntity>.WithBehavior` overloads to record ordered descriptors while preserving current decorator order. | ✅ | 2026-07-21 |
| TASK-003 | Delete the infrastructure interface and update dispatcher, registration, tests, examples, and docs to the Domain namespace. Gate completion with a stale-reference `rg` search. | ✅ | 2026-07-21 |
| TASK-004 | Add ADR-0028 and mark ADR-0027 partially superseded where it assigns the public port to infrastructure or permits behavior bypass. | ✅ | 2026-07-21 |

### Implementation Phase 2

- GOAL-002: Build deterministic compatibility planning before side effects.

| Task | Description | Completed | Date |
|---|---|---|---|
| TASK-005 | Add compatibility enums, typed errors, execution-plan records, behavior adapter contract/registry, and `EntityBulkInsertCompatibilityAnalyzer<TEntity,TContext>`. | ✅ | 2026-07-21 |
| TASK-006 | Register every built-in matrix classification. Map audit, cancellation, concurrency, observability, event creation/metrics, and outbox; mark query-only decorators no-op; require fallback for direct publisher and mapped repositories; reject unknown types. | ✅ | 2026-07-21 |
| TASK-007 | Add closed DevKit DbContext/provider/interceptor descriptors to SQL Server, PostgreSQL, SQLite, and Cosmos registration paths. Absence of the DevKit marker requires fallback/rejection. | ✅ | 2026-07-21 |
| TASK-008 | Add explicit bulk behavior and shadow-value-provider extensions with semantic-key duplicate validation. | ✅ | 2026-07-21 |
| TASK-009 | Validate repeated registrations, multiple contexts, conflicting options, repository kind, provider duplicates, and scoped/transient lifetime. | ✅ | 2026-07-21 |

### Implementation Phase 3

- GOAL-003: Implement behavior-equivalent native preprocessing and postprocessing.

| Task | Description | Completed | Date |
|---|---|---|---|
| TASK-010 | Implement pipeline ordering, short-circuiting, reverse cleanup, cancellation, and primary-error preservation. | ✅ | 2026-07-22 |
| TASK-011 | Implement audit behavior with current-user name/email/id parity and core concurrency generation without duplication. | ✅ | 2026-07-22 |
| TASK-012 | Implement domain-event creation/metrics plus logging, metrics, and tracing behaviors at operation cardinality. | ✅ | 2026-07-22 |
| TASK-013 | Extract deterministic event-to-outbox projection and implement bulk outbox staging/saving/clearing/queue timing inside the transaction. | ✅ | 2026-07-22 |

### Implementation Phase 4

- GOAL-004: Harden mapping, graph, tracking, shadow, and generated-value behavior.

| Task | Description | Completed | Date |
|---|---|---|---|
| TASK-014 | Split mapping into side-effect-free `Analyze` and postbehavior `Build` stages; reject all unsupported model/value shapes before mutation. | ✅ | 2026-07-22 |
| TASK-015 | Add value-sensitive graph rules and explicit JSON, separate-table, TPT/TPC, and multi-table detection. | ✅ | 2026-07-22 |
| TASK-016 | Add CLR/owned/constant-discriminator/shadow-provider accessors and required-shadow validation. | ✅ | 2026-07-22 |
| TASK-017 | Correct identity/default/computed/rowversion classification and enum provider normalization; retain detached/no-hydration contract. | ✅ | 2026-07-22 |
| TASK-018 | Add tracked-instance and duplicate-reference validation while preserving null/empty behavior. | ✅ | 2026-07-22 |

### Implementation Phase 5

- GOAL-005: Make native SQL Server execution atomic and deterministic.

| Task | Description | Completed | Date |
|---|---|---|---|
| TASK-019 | Rewrite dispatcher orchestration to follow sequence D with typed execution modes/errors and exactly one path. | ✅ | 2026-07-22 |
| TASK-020 | Move connection and transaction ownership to EF APIs; implement current transaction reuse, owned rollback, retry-strategy rejection, and ambient-transaction rejection. | ✅ | 2026-07-22 |
| TASK-021 | Require the active SQL transaction in `SqlServerEntityBulkInsertProvider`; remove `UseInternalTransaction`; preserve one write, KeepIdentity, TableLock, batch size, timeout, and cancellation. | ✅ | 2026-07-22 |
| TASK-022 | Preserve PostgreSQL/SQLite placeholder behavior and exact missing/duplicate provider errors. | ✅ | 2026-07-22 |

### Implementation Phase 6

- GOAL-006: Complete tests, documentation, and preview release notes.

| Task | Description | Completed | Date |
|---|---|---|---|
| TASK-023 | Add all unit, architecture, SQL Server integration, and performance coverage in section H; execute top-level build/tests sequentially. | ✅ | 2026-07-22 |
| TASK-024 | Update DoFiesta to import only the Domain contract. Retain `entity.Steps.Clear()` as the intentional root-table-only boundary. | ✅ | 2026-07-22 |
| TASK-025 | Update ADRs, repository/DataPorter guides, README, XML examples, and changelog using section I. | ✅ | 2026-07-22 |
| TASK-026 | Run repository-wide stale-reference checks and verify no source file outside section E changed unintentionally. | ✅ | 2026-07-22 |

## 3. Alternatives

- **ALT-001**: Reuse repository decorators directly — rejected because their inner chain terminates in `AddRange`/`SaveChanges`; it cannot reach native bulk without duplicating or deconstructing decorator semantics.
- **ALT-002**: Add bulk insertion to `IGenericRepository<TEntity>` — rejected because it forces an optional provider capability onto every repository and blurs tracked aggregate persistence with native root-table ingestion.
- **ALT-003**: Keep current bypass behavior and document it — rejected because configured write semantics must not be silently ignored.
- **ALT-004**: Always fallback — rejected because silent performance/tracking changes violate the explicit native contract.
- **ALT-005**: Always reject domain events/outbox — rejected because event creation is in-memory and outbox rows can be saved atomically inside the external transaction.
- **ALT-006**: Automatically publish events after native commit — rejected because publication failure would report failure after durable insertion.
- **ALT-007**: Attach native entities after write — rejected because identity/default/computed/rowversion values are not hydrated and attaching as Added risks duplicate insertion.
- **ALT-008**: Return generated values — rejected by the count-only contract and native performance goal; it requires a separate staging/output API.
- **ALT-009**: Accept aggregate graphs with one bulk call per table — deferred to a separate graph-bulk design requiring dependency ordering and generated FK propagation.
- **ALT-010**: Invoke SaveChanges interceptors manually — rejected because their event data and guarantees are coupled to EF change tracking and SaveChanges.
- **ALT-011**: Retain `UseInternalTransaction` — rejected because it cannot cover all native batches plus outbox in one operation transaction.
- **ALT-012**: Automatically run the EF retry strategy — rejected until an operation-id and verification contract makes replay idempotent.
- **ALT-013**: Keep an obsolete infrastructure interface — rejected because the preview feature has no consumers; a bridge creates needless API and DI ambiguity.

## 4. Dependencies

- **DEP-001**: Domain already references `Common.Results`; no new Domain package dependency is needed for `Result<long>`.
- **DEP-002**: Add an explicit `Infrastructure.EntityFramework.csproj` reference to `Domain.csproj`; do not rely on its current transitive dependency.
- **DEP-003**: Keep `Microsoft.Data.SqlClient` and SQL Server EF dependencies confined to `Infrastructure.EntityFramework.SqlServer`.
- **DEP-004**: Reuse `RepositoryAuditStateBehaviorOptions`, `ICurrentUserAccessor`, `IOutboxDomainEventContext`, outbox options/serializer/queue, and filtered outbox SaveChanges support.
- **DEP-005**: Reuse the existing SQL Server integration fixture/Testcontainers setup.
- **DEP-006**: Provider semantics require an external transaction because internal transactions are per batch; retrying manually controlled transactions requires an idempotent retry unit.

## 5. Files

### E. File-by-file change plan

| File | Action | Main change/dependency impact | Compatibility and tests |
|---|---|---|---|
| `src/Domain/Repositories/IEntityBulkInserter.cs` | Add | Domain application-facing contract | Domain API and handler-injection tests |
| `src/Domain/Repositories/RepositoryBehaviorDescriptor.cs` | Add | Provider-neutral behavior metadata | Ordering/classification tests |
| `src/Domain/Repositories/RepositoryBehaviorRegistrationKind.cs` | Add | Distinguish type and factory registrations | Factory descriptor tests |
| `src/Domain/Repositories/RepositoryBuilderContext.cs` | Modify | Record ordered behavior descriptors | Existing decorator order regression tests |
| `src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj` | Modify | Add direct Domain project reference | Architecture dependency test |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInserter.cs` | Delete | Remove unused preview infrastructure contract | Build plus stale-reference check |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs` | Modify | Add fail/fallback policy and clarify generated/transaction semantics | Defaults/validation tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertConfiguration.cs` | Modify | Carry context/options/repository/behavior metadata | Multi-registration tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/ServiceCollectionExtensions.cs` | Modify | Register only Domain interface, analyzer, pipeline, adapters, and extensions | DI/lifetime tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs` | Rewrite | Implement sequence D and typed errors | Dispatcher/fallback/transaction tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilder.cs` | Refactor | Side-effect-free analysis plus finalized mapping | Expanded mapping tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInsertProvider.cs` | Modify docs/guard contract | Require active transaction for relational public execution | Provider contract tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertBatch.cs` | Modify | Carry finalized mapping and transaction preconditions | Provider tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertColumn.cs` | Modify | Support CLR, owned, constant, and shadow provider accessors | Shadow/converter tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/DbContextRegistrationDescriptor.cs` | Add | Mark supported DevKit provider/context registration | Raw-EF rejection tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/DbContextInterceptorDescriptor.cs` | Add | Record configured interceptor types by context | Interceptor tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkRepositoryRegistrationDescriptor.cs` | Add | Distinguish direct and mapped repository registrations | Mapped fallback tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/IEntityBulkInsertBehavior.cs` | Add | Public custom behavior extension point | Contract/order tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBehaviorContext.cs` | Add | Immutable per-operation state | Isolation tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertResult.cs` | Add | Count/mode/transaction/commit state | Result propagation tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertFailure.cs` | Add | Stage-aware cleanup input | Failure tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertExecutionMode.cs` | Add | Native/fallback mode | Mode tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBehaviorRegistration.cs` | Add | Ordered semantic-key registration | Duplicate-key tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertBehaviorPipeline.cs` | Add | Hook execution and reverse cleanup | Pipeline tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertAuditStateBehavior.cs` | Add | Current-user audit parity | Audit tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertDomainEventBehavior.cs` | Add | Created-event registration | Event tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertDomainEventMetricsBehavior.cs` | Add | Event metric parity | Metric ordering tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertOutboxBehavior.cs` | Add | Transactional outbox staging/save/clear | Atomicity tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertLoggingBehavior.cs` | Add | Safe operation-level logs | Logging tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertMetricsBehavior.cs` | Add | Duration/count/failure metrics | Meter tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Behaviors/EntityBulkInsertTracingBehavior.cs` | Add | One activity with bounded tags | Activity tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/EntityBulkInsertCompatibility.cs` | Add | Native/no-op/fallback/reject classifications | Matrix tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/EntityBulkInsertCompatibilityError.cs` | Add | Aggregate actionable preflight failures | Error tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/EntityBulkInsertPostCommitError.cs` | Add | Mark committed custom-hook failure | Committed-state tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/EntityBulkInsertExecutionPlan.cs` | Add | Frozen single-path preflight result | No-double-execution tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/IEntityBulkInsertRepositoryBehaviorAdapter.cs` | Add | Repository-to-bulk adapter contract | Adapter tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/EntityBulkInsertRepositoryBehaviorAdapterRegistry.cs` | Add | Known adapter selection and duplicate checks | Unknown/duplicate tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/BuiltInRepositoryBehaviorAdapters.cs` | Add | All built-in behavior mappings/no-ops | Exhaustive matrix tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Compatibility/EntityBulkInsertCompatibilityAnalyzer.cs` | Add | Full side-effect-free preflight | Analyzer tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/EntityBulkInsertMappingAnalysis.cs` | Add | Root-table/model/store-generation analysis | Mapping matrix tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/IEntityBulkInsertShadowValueProvider.cs` | Add | Deterministic shadow extension point | Tenant/shadow tests |
| `src/Infrastructure.EntityFramework/Repositories/Bulk/Mapping/EntityBulkInsertShadowPropertyContext.cs` | Add | Entity/property/context input for shadow values | Value-provider tests |
| `src/Infrastructure.EntityFramework/Repositories/ServiceCollectionExtensions.cs` | Modify | Register direct/mapped repository descriptors | Repository-kind tests |
| `src/Infrastructure.EntityFramework/Repositories/Outbox/OutboxDomainEventCollector.cs` | Add | Shared deterministic event-to-outbox projection | Projection parity tests |
| `src/Infrastructure.EntityFramework/Repositories/Outbox/RepositoryOutboxDomainEventBehavior.cs` | Modify | Use collector; retain current normal-repository transaction behavior | Existing outbox tests |
| `src/Infrastructure.EntityFramework/Repositories/Outbox/ServiceCollectionExtensions.cs` | Modify | Register shared collector | DI tests |
| `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs` | Modify | Require external transaction; remove internal path | SQL atomicity/options tests |
| `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertOptions.cs` | Modify docs/validation | Preserve native flags; forbid transaction ownership flags | Option tests |
| `src/Infrastructure.EntityFramework.SqlServer/ServiceCollectionExtensions.cs` | Modify | Add context/interceptor descriptors; retain singleton provider | DI/interceptor tests |
| `src/Infrastructure.EntityFramework.Postgres/ServiceCollectionExtensions.cs` | Modify | Add context/interceptor descriptors; placeholder unchanged | Metadata/placeholder tests |
| `src/Infrastructure.EntityFramework.Sqlite/ServiceCollectionExtensions.cs` | Modify | Add context/interceptor descriptors; placeholder unchanged | Metadata/placeholder tests |
| `src/Infrastructure.EntityFramework.Cosmos/ServiceCollectionExtensions.cs` | Modify | Add descriptor and explicit native incompatibility | Missing-provider tests |
| `tests/Domain.UnitTests/Repositories/EntityBulkInserterContractTests.cs` | Add | Verify assembly, namespace, signature, dependencies | Unit |
| `tests/Application.UnitTests/Repositories/EntityBulkInserterInjectionTests.cs` | Add | Handler compiles against Domain interface and fake | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserterTests.cs` | Expand | Dispatch, path selection, transaction/failure/cancellation | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilderTests.cs` | Expand | Full mapping/graph/shadow/generated matrix | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertArchitectureTests.cs` | Expand | Domain ownership and provider boundaries | Architecture |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertBehaviorPipelineTests.cs` | Add | Hook order, cleanup, cancellation, postcommit errors | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertCompatibilityAnalyzerTests.cs` | Add | Exhaustive behavior/interceptor/model classification | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertBuiltInBehaviorTests.cs` | Add | Audit/events/outbox/observability semantics | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertServiceCollectionExtensionsTests.cs` | Add | Domain DI, lifetimes, order, conflicts, idempotence | Unit |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/TestEntityBulkInsertProvider.cs` | Modify | Capture transaction/batch and inject staged failures | Test support |
| `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Outbox/RepositoryDomainEventOutboxBehaviorTests.cs` | Expand | Shared collector parity | Unit |
| `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/SqlServerEntityBulkInsertProviderTests.cs` | Expand | Real mapping/options/atomicity/rollback/cancellation | SQL Server |
| `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/PlaceholderEntityBulkInsertProviderTests.cs` | Modify | Resolve Domain interface; retain explicit failures | Integration |
| `benchmarks/Application.Benchmarks/EntityBulkInsertPipelineBenchmarks.cs` | Add | Preprocessing pipeline benchmarks at 1/1k/10k rows | Manual benchmark |
| `docs/adr/0028-domain-entity-bulk-insert-behavior-pipeline.md` | Add | Record capability/pipeline/fallback/transaction decision | Documentation review |
| `docs/adr/0027-provider-strategy-for-entity-bulk-insert.md` | Modify | Mark public ownership/bypass statements superseded | Link review |
| `docs/adr/README.md` | Modify | Index ADR-0028 and supersession | Link/order check |
| `docs/features-domain-repositories.md` | Rewrite bulk section | Guarantees/matrix/fallback/transactions/tracking | Example compile check |
| `docs/features-application-dataporter.md` | Modify | Direct Domain injection and usage guidance | Example compile check |
| `examples/DoFiesta/DoFiesta.Presentation.Web.Server/Modules/Core/DataPorter/TodoItemBulkImportPersistenceInterceptor.cs` | Modify | Import Domain contract; retain root-only step clearing | Example build |
| `examples/DoFiesta/DoFiesta-README.md` | Modify | Explain Domain injection and root-only restriction | Doc check |
| `README.md` | Modify | Link public bulk capability guidance | Doc check |
| `CHANGELOG.md` | Modify | Record preview-breaking namespace move and no alias | Release review |

### F. Dependency-injection plan

- `.WithBulkInsert()` registers one closed `EntityBulkInsertConfiguration<TEntity,TContext>`, repository descriptor, analyzer, mapping analyzer/builder, adapter registry, pipeline, and `BridgingIT.DevKit.Domain.Repositories.IEntityBulkInserter<TEntity>` mapped to `EntityFrameworkEntityBulkInserter<TEntity,TContext>` with `context.Lifetime`.
- Register exactly one public service contract for the entity. Do not expose the concrete dispatcher as another public resolution path and do not register the deleted infrastructure interface.
- Automatic bulk behaviors use the repository lifetime. Audit/outbox dependencies remain scoped/transient and are never captured by singleton providers.
- Provider strategies remain stateless singleton `TryAddEnumerable` registrations. Exact runtime selection still uses `DbContext.Database.ProviderName`.
- Repository descriptors are mutable only while configuring services; the resolved closed configuration snapshots the final descriptor list so `.WithBulkInsert()` may appear before later `.WithBehavior(...)` calls.
- Automatic behavior registrations preserve repository order. Explicit behavior order is mandatory; ties use DI sequence. Duplicate semantic keys fail validation.
- Each DevKit `Add*DbContext<TContext>` overload records provider family and interceptor types. Raw EF registration lacks the marker and cannot enter native mode.
- Repeated identical bulk registration is idempotent. Different contexts or options fail with an error naming both registrations.
- Scoped is default/recommended. Transient is supported only with a transient context. Singleton throws during `.WithBulkInsert()` registration.

### G. Compatibility and migration strategy

1. Treat this as a preview API replacement. The user confirmed on 2026-07-17 that no consumer uses the feature.
2. Add the Domain interface and delete the infrastructure interface in the same commit, leaving one canonical contract.
3. Make the dispatcher implement the Domain interface directly and register only it.
4. Update all repository source, examples, tests, XML comments, ADRs, and feature guides to `BridgingIT.DevKit.Domain.Repositories`.
5. Add the direct Infrastructure.EntityFramework -> Domain project/package reference and ship both packages in the same next preview version.
6. Add no `[Obsolete]` type, type forwarder, compatibility shim, namespace alias, or second DI contract.
7. Gate release with `rg` searches proving no infrastructure-interface path/namespace reference remains outside historical release documentation.
8. `CHANGELOG.md` labels the namespace move as preview-breaking and documents fail-by-default compatibility, explicit fallback, detached/no-generated-values behavior, singleton rejection, root-only graphs, and stronger transactions.

## 6. Testing

### H. Testing strategy

#### H.1 Unit and architecture tests

- **TEST-001**: Assert the generic contract is in the Domain assembly/namespace, has the exact `Task<Result<long>>` signature, and Domain references neither EF Core nor SqlClient.
- **TEST-002**: Compile an Application handler using only the Domain interface and a fake implementation.
- **TEST-003**: Resolve one Domain service descriptor for scoped/transient registration; reject singleton; assert the infrastructure assembly exports no `IEntityBulkInserter<TEntity>` interface.
- **TEST-004**: Configure repository behaviors before and after `.WithBulkInsert()` and assert descriptor/pipeline order: before ascending, after/failure descending.
- **TEST-005**: Assert a before failure prevents mutation, mapping finalization, connection, provider, and later hooks; failure-hook errors do not replace the primary error.
- **TEST-006**: Test every built-in matrix classification, unknown custom behavior, explicit adapter, default failure, and explicit fallback.
- **TEST-007**: Test audit username/email/id values, one timestamp mutation/entity, and no double audit during fallback.
- **TEST-008**: Assert concurrency generation occurs exactly once with and without its repository descriptor.
- **TEST-009**: Test event creation/order/metrics, outbox staging, clear-on-success, retain-on-failure, and no duplicate generic event.
- **TEST-010**: Test logging/tracing/metrics success/failure/fallback tags and absence of payload/PII.
- **TEST-011**: Classify built-in logger, query/materialization, connection/transaction, custom command, and custom SaveChanges interceptors exactly as matrix B specifies.
- **TEST-012**: Test shadows, custom shadow provider, TPH, TPT/TPC, JSON/separate-table ownership, null/empty/populated navigations, enums, converters, escaped identifiers, identity/default/computed/rowversion, and required-column preflight.
- **TEST-013**: Test tracked inputs, duplicate references, multiple contexts, conflicting repeated options, null enumerable/elements, and empty batch.
- **TEST-014**: Test retrying execution strategy and ambient `TransactionScope` rejection before provider work.
- **TEST-015**: Test provider/compatibility/postcommit error types, cancellation rethrow, failure aggregation, and fallback result message.
- **TEST-016**: For 10,000 entities assert one provider invocation and zero repository, `AddRange`, or `SaveChanges` calls.

#### H.2 Real SQL Server integration tests

- **TEST-017**: Resolve the Domain interface and insert flat rows through one `SqlBulkCopy` with batch size, timeout, TableLock, and exact count.
- **TEST-018**: Persist configured AuditState current-user values and dates, including same-table owned audit state.
- **TEST-019**: Persist root and outbox rows in one transaction, propagate correlation metadata, and clear events only on success.
- **TEST-020**: Force outbox serialization/save failure after native write and assert zero root/outbox rows after owned rollback.
- **TEST-021**: Fail after at least one batch-size chunk and assert zero rows, proving operation atomicity.
- **TEST-022**: Reuse an active EF transaction; caller commit persists and caller rollback removes rows; inserter never commits caller-owned state.
- **TEST-023**: Cancel a large write and assert owned rollback leaves zero rows and cancellation is rethrown.
- **TEST-024**: Verify same-table owned/null, required shadow provider, TPH discriminator, converters, enums, and escaped identifiers.
- **TEST-025**: Verify identity preserve on/off, defaults, computed values, and rowversion in a fresh context; inputs remain detached and unhydrated.
- **TEST-026**: Verify populated owned/non-owned graphs, JSON/separate-table ownership, and TPT/TPC fail before transaction/provider work.
- **TEST-027**: Configure direct publisher, mapped repository, and custom SaveChanges interceptor; assert default rejection and explicit decorated fallback.
- **TEST-028**: Assert EF connection/transaction interceptor callbacks surround native open/begin/commit/rollback.

#### H.3 Performance regression protection

- **TEST-029**: Structural test with 100,000 compatible entities: one provider call, zero repository calls, and no per-entity service resolution.
- **TEST-030**: BenchmarkDotNet preprocessing benchmarks for 1, 1,000, and 10,000 flat/audited entities; record the initial median/allocation baseline in the PR artifact.
- **TEST-031**: Opt-in SQL benchmark comparing native and `InsertSetAsync` at 10,000 rows. Native must retain one `WriteToServerAsync` and remain within 20% of the pre-redesign median on the same runner/database; investigate variance rather than failing shared CI on timing alone.

### I. Documentation plan

- Rewrite the bulk section in `docs/features-domain-repositories.md` as a Domain-visible, infrastructure-implemented optional capability with the complete supported/fallback/rejected matrix.
- Define guarantees: one native provider call, root-table-only contract, preflight, operation transaction, count result, no silent fallback, and detached entities.
- List automatic equivalents: configured audit, cancellation, concurrency, operation observability, domain-event creation/metrics, and repository outbox.
- List fallback/rejection features: direct publisher, mapped `TDatabaseEntity`, unknown validation/access decorators, unknown SaveChanges/command interceptors, raw EF registration, and unsafe retry strategies.
- List unsupported mappings: populated graphs, separate-table/JSON ownership, TPT/TPC, required shadow without provider, tracked inputs, duplicates, and ambient transactions.
- Explain that generated identity/default/computed/rowversion values are not returned; use normal `InsertSetAsync` when tracking, graph cascades, interceptor semantics, or returned database values are required.
- Document transaction ownership, caller-owned transaction behavior, cancellation rollback, immediate-outbox polling rule, and possible `IsCommitted=true` postcommit error.
- Update DataPorter/DoFiesta examples to inject only the Domain interface and document `Steps.Clear()` as root-only intent.
- Add XML examples to every new public contract and update ADR-0027 links/ownership. ADR-0028 records behavior classification, pipeline ordering, fallback, and external transaction decisions.
- Record the clean preview-breaking namespace move in `CHANGELOG.md`; explicitly state that no compatibility alias exists.

## 7. Risks & Assumptions

### J. Risks and resolved decisions

There are no blocking unresolved decisions. The following decisions are fixed for implementation; excluded capabilities require separate plans.

| Decision | Repository evidence | Plan decision | Excluded follow-up |
|---|---|---|---|
| Preview API ownership | Feature is preview and confirmed unused | Delete infrastructure contract; expose Domain only | No compatibility bridge |
| Domain events/outbox | Creation is in-memory; outbox needs one encompassing transaction | Support creation and outbox natively; bulk always saves staged outbox rows | Harmonize normal outbox AutoSave separately |
| Unsupported behavior default | Current path silently bypasses all decorators | Fail by default; explicit observable fallback | No silent mode |
| Tracking | Native does not attach or hydrate | Stay detached; reject tracked input | Attach/hydration API |
| Generated values | Count-only result; SqlBulkCopy has no output mapping | Do not return generated values | Provider-specific output contract |
| Aggregate graphs | Current builder rejects/omits incompletely | Allow null/empty navigations; root-table only | Multi-table graph bulk API |
| Audit | DoFiesta configures audit after bulk registration | Auto-enable equivalent from final descriptors | None |
| Execution strategy | Retrying non-idempotent bulk can replay after ambiguous commit | Reject owned native execution when retries enabled | Operation-id/idempotency design |
| Partial completion | Internal SQL transaction is batch-scoped | External EF transaction across provider/outbox | None |
| Direct publisher | Publishes after persistence and can fail after commit | Fallback/reject; never auto-map | Explicit custom non-atomic hook |
| Interceptor discovery | DevKit records interceptor types; raw setup has no provenance | Require DevKit marker; raw setup fallback/reject | Broader raw EF discovery |
| Ambient transaction | Current behavior untested | Reject before connection | Ambient support plan |
| Multiple contexts | Current `TryAdd` silently keeps first | Reject ambiguity | Named/keyed capability |
| Singleton lifetime | Current tests permit it despite DbContext thread-unsafety | Reject and document preview break | None |
| Repository metadata | Current list contains private executable actions only | Add public provider-neutral descriptors | No `InternalsVisibleTo` bridge |
| Normal outbox atomicity | Code stores after inner unless outer transaction exists | Guarantee bulk atomicity; correct normal docs only | Separate normal-repository fix |

- **RISK-001**: Audit, ids, versions, and events are in-memory mutations that remain after provider rollback. Complete all compatibility analysis first and document mutation-on-failure.
- **RISK-002**: `DataTable` duplicates batch values in memory. Preserve current performance now; design an `IDataReader` provider separately for extreme batch sizes.
- **RISK-003**: Factory/custom decorators may hide semantics. Unknown types fail/fallback until explicitly adapted.
- **RISK-004**: Immediate outbox enqueue is unsafe before a caller-owned transaction commits; interval polling changes notification latency but avoids phantom ids.
- **RISK-005**: Moving connection/transaction ownership to EF changes callback/log behavior and exposes retry incompatibility; integration tests must cover it.
- **RISK-006**: Removing the infrastructure interface is source/binary breaking by design. Confirmed lack of consumers makes this acceptable; build and stale-reference gates protect the repository.
- **ASSUMPTION-001**: The user confirmed the bulk-insert feature is preview and unused; no backward-compatibility bridge is required.
- **ASSUMPTION-002**: PostgreSQL/SQLite native writers remain outside scope and their placeholder failures remain valid.
- **ASSUMPTION-003**: SQL Server integration infrastructure can create temporary models/tables needed for forced failure and inheritance cases.
- **ASSUMPTION-004**: No ChangeHistory repository behavior exists at commit `ece8e706`; if reintroduced before implementation, it is unknown/fallback-required until added to matrix B and tested.

## 8. Related Specifications / Further Reading

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/adr/0001-clean-onion-architecture.md`
- `docs/adr/0002-result-pattern-error-handling.md`
- `docs/adr/0004-repository-decorator-behaviors.md`
- `docs/adr/0006-outbox-pattern-domain-events.md`
- `docs/adr/0007-entity-framework-core-code-first-migrations.md`
- `docs/adr/0017-integration-testing-strategy.md`
- `docs/adr/0018-dependency-injection-service-lifetimes.md`
- `docs/adr/0027-provider-strategy-for-entity-bulk-insert.md`
- `docs/features-domain-repositories.md`
- `docs/features-domain-events.md`
- `plan/pln-refactor-entity-bulk-insert-provider-abstraction-1.md`
- [Microsoft Learn: SqlBulkCopy BatchSize](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlbulkcopy.batchsize)
- [Microsoft Learn: EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [Microsoft Learn: EF Core connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)
- [Microsoft Learn: EF Core interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)
