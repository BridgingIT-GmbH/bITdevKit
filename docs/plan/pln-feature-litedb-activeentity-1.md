---
goal: Implement the LiteDB ActiveEntity provider for COV-002 without changing ActiveEntity core
version: 1
date_created: 2026-11-19
last_updated: 2026-11-19
owner: bITdevKit maintainers
status: 'Completed'
tags: [feature, infrastructure, litedb, activeentity]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Implement `IActiveEntityEntityProvider<TEntity, TId>` in `Infrastructure.LiteDB`. Applications select LiteDB through the existing ActiveEntity configurator. Their entity classes, static APIs, callbacks, behaviors, and transaction entry points remain unchanged.

Implement the provider directly against `LiteDbContext` and native LiteDB APIs. Do not use `LiteDbGenericRepository` or `LiteDbReadOnlyGenericRepository`. ActiveEntity core and its contracts remain unchanged.

This is the implementation plan for COV-002 in the [LiteDB Storage Providers specification](../specs/spec-infrastructure-litedb-providers.md). Implemented for development use against `6.0.0-prerelease.355`. The provider and support APIs are public with XML documentation. ActiveEntity core, source generators, and the generic repository classes are unchanged. The broader LiteDB migration review and upstream production restriction remain open.

## 1. Requirements & Constraints

- **REQ-001**: Implement every live member of [IActiveEntityEntityProvider<TEntity, TId>](../../src/Domain/ActiveEntity/Provider/IActiveEntityEntityProvider.cs). This includes insert, update, upsert, delete, both set-operation overloads, all lookup, list, projection, ID, paged, existence, and count overloads, and the three transaction methods. Commented-out members are excluded.
- **REQ-002**: Return existing `Result`, `Result<T>`, and `ResultPaged<T>` types. Missing single records produce `NotFoundError`. Empty collections, zero counts, and false existence checks are successful results. Preserve failures when composing paged results instead of reading a failed result's `Value`.
- **REQ-003**: Register through `ActiveEntityConfigurator<TEntity, TId>.UseProviderFactory`. Keep the provider scoped. Reuse the existing behavior pipeline through normal ActiveEntity calls. The provider invokes the supplied `ActiveEntityCallbackOptions<TEntity, TId>` callbacks and does not invoke behaviors or publish events itself.
- **REQ-004**: Support primitive `Guid`, `string`, `int`, and `long` IDs and generated `EntityId<T>` IDs over those primitives. Normalize object and string lookup IDs before query translation. Persist typed IDs as scalar BSON identities, restore their concrete type, and use atomic native numeric ID allocation rather than an in-memory counter. Honor a configured `IEntityIdGenerator<TEntity>` for custom generation.
- **REQ-005**: Persist embedded entity state, including audit properties and `IConcurrency.ConcurrencyVersion`. Configure mappings in LiteDB infrastructure. Exclude pending `DomainEvents` and runtime state from BSON. Do not add LiteDB attributes or serialization code to ActiveEntity or its base classes.
- **REQ-006**: Combine multiple specifications with AND. Apply distinct selection and the complete ordered sequence before paging. Support projections and ID selection with the same filters and ordering as entity reads. Paged totals count the filtered, distinct selection before `Skip` and `Take`, including when the requested page is empty.
- **REQ-007**: Treat `NoTracking` as naturally satisfied by detached LiteDB reads. Embedded members are already loaded. Support explicit mapper-configured references through LiteDB includes. Return a failed result for unsupported query or include shapes instead of silently ignoring them. No EF relationship fixup, change tracker, or generic graph merge is required.
- **REQ-008**: When optimistic concurrency is enabled, atomically compare the stored and supplied `ConcurrencyVersion` before update or entity delete. A stale version returns `ConcurrencyError` without changing the record. Generate a fresh version for inserted and updated concurrency-aware entities, including set updates. Entities without `IConcurrency` remain supported.
- **REQ-009**: Implement real commit and rollback across awaited operations through both direct provider calls and the unchanged `ActiveEntity.WithTransactionAsync` overloads. Transaction state belongs to the scoped database session. Providers for the same database in that DI scope share the session. Independent scopes must never join each other's transaction.
- **REQ-010**: A standalone write commits only after its supplied callbacks succeed. A failed callback prevents commit of that write. Within an explicit transaction, failure marks the session as unable to commit until rollback. After-behaviors run outside a standalone provider call, as the existing core dictates. This plan does not promise rollback of external callback or behavior side effects.
- **REQ-011**: Use one named database owner per registration, default name `default`. Providers share its `LiteDbContext` and do not open or dispose competing connections. Initialize mappings and indexes once, preserve records on startup, and isolate entity collections. Reject Shared mode and duplicate registrations that claim the same canonical file under different owners.
- **REQ-012**: Honor cancellation before execution and during queue waits and bounded batches. Native LiteDB calls are synchronous and cannot be interrupted arbitrarily. Cleanup must roll back even when the caller token is already canceled. Do not return successful cancellation while a queued write continues in the background.
- **CON-001**: Runtime changes are confined to `src/Infrastructure.LiteDB/`. Do not modify `src/Domain/ActiveEntity/`, provider contracts, ActiveEntity behaviors, the query DSL, source generators, existing EF or in-memory providers, or Presentation code. Tests and documentation may change.
- **CON-002**: Keep project casing `Infrastructure.LiteDB` and assembly and namespace casing `BridgingIT.DevKit.Infrastructure.LiteDb`. Add no EF dependency, engine fork, patched package, compatibility framework, or unrelated provider.
- **CON-003**: Keep the pinned v6 package and its upstream risk acknowledgement guard. Only disposable-data tests acknowledge the development risk. The selected package remains unsuitable for production. Engine defects that prevent the required semantics block COV-002 under MIG-008.
- **CON-004**: Implement only the LiteDB lifetime, mapping, query, and transaction mechanics required by COV-002. Do not instantiate, inherit from, wrap, or delegate operations to `LiteDbGenericRepository<TEntity>` or `LiteDbReadOnlyGenericRepository<TEntity>`. Implement persistence directly through `LiteDbContext` and native LiteDB APIs. Standalone query helpers may be reused after verifying their semantics. Do not complete COV-001's generic repository stubs or implement the domain outbox under this plan.
- **PAT-002**: Prefer public provider support types and APIs over the internal modifier. Keep implementation details private. Document public types, constructors, parameters, return values, ownership, cancellation, and disposal with XML comments.
- **PAT-001**: Follow `AGENTS.md`, `.editorconfig`, xUnit, Shouldly, existing error types, options patterns, structured logging, and XML documentation for public APIs. Prefer BCL synchronization and queues. Run top-level builds and tests sequentially.

The following APIs are implemented in `Infrastructure.LiteDB`:

```csharp
services.AddLiteDbDatabase(options => options.ConnectionString = connectionString);
services.AddActiveEntity(configuration => configuration
    .For<LedgerEntry, Guid>()
    .UseLiteDbProvider());
```

Add a named overload `AddLiteDbDatabase(string name, Action<LiteDbDatabaseOptions> configure)`. `UseLiteDbProvider<TEntity, TId>` accepts an optional `Action<LiteDbActiveEntityProviderOptions<TEntity>>`. Provider options select `DatabaseName`, `CollectionName`, `IdGenerator`, `EnableOptimisticConcurrency`, and `VersionGenerator`. Use `default`, optimistic concurrency enabled, and `GuidGenerator.CreateSequential` as the respective defaults.

Database options hold `ConnectionString` and `ConfigureMapper`. Freeze registration and mapping settings before the owner opens its database. An omitted collection name is `ae_` plus the first 32 lowercase hexadecimal characters of SHA-256 over the UTF-8 string `assembly-simple-name:entity-full-name`. Closed generic and array arguments use the same recursive type identity without assembly versions. The assembly version is excluded. Validate explicit collection names and collisions before use.

Use native queries where they preserve the requested semantics. Bound result materialization and any local fallback for compound ordering, distinct selection, or computed sets with provider option `MaxMaterializedRows`, default `10000`, configurable to a positive integer. Probe one extra row and fail with `InvalidOperationError` when the limit is exceeded. Never truncate silently. Native scalar counts and existence checks do not need entity materialization. The limit and any rejected query shapes must be documented.

Paged methods default to `Skip = 0` and `Take = 10` when omitted. Derive page number as `Skip / Take + 1` and page size from the effective `Take`. Validate explicit paging values before execution. An unpaged method does not gain the paged method's default `Take`.

## 2. Implementation Steps

### Implementation Phase 1

- **GOAL-001**: Prove that the pinned engine can meet the existing asynchronous transaction and mapping contracts within provider scope. This phase is a prerequisite for all later phases.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | Add `tests/Infrastructure.IntegrationTests/LiteDB/ActiveEntity/LiteDbActiveEntityTestModels.cs` with fictional ActiveEntity models covering primitive and generated typed IDs, embedded values, audit state, concurrency state, and pending events. Add a Docker-free `LiteDbActiveEntityTestCollection.cs` with `DisableParallelization = true` and no `TestEnvironmentFixture`. Inspect existing EF and in-memory scenarios as references without changing their tests or copying their SQL fixture. | Yes | 2026-11-19 |
| TASK-002 | Add engine probes in `LiteDbActiveEntityEngineTests.cs`. Verify the selected package's native begin, commit, rollback, mapper, scalar ID, query, and cursor behavior. Exercise begin, writes, and completion on a dedicated thread while the caller awaits unrelated work. Check that all cursors close before transaction completion. These probes precede any claim of provider support. | Yes | 2026-11-19 |
| TASK-003 | Record the probe results in this plan. Require multi-write rollback, isolation from an unrelated caller, typed-ID round trips, persisted version values, and exclusion of pending events to pass. If the official package cannot provide them through the design below, mark COV-002 blocked in the parent spec and stop dependent implementation. Do not modify ActiveEntity or the engine to bypass the failure. | Yes | 2026-11-19 |

Phase 1 passes when the probes establish the required engine behavior. Probes do not replace the later tests through the unchanged ActiveEntity API.

### Implementation Phase 2

- **GOAL-002**: Add the minimal shared LiteDB owner, scoped session, mapping, and registration. Depends on Phase 1. Execute TASK-004 before TASK-005 through TASK-007. Those tasks must pass together before Phase 3.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-004 | Add `src/Infrastructure.LiteDB/Configuration/LiteDbDatabaseOptions.cs`, `LiteDbDatabaseServiceCollectionExtensions.cs`, `LiteDbDatabaseRegistry.cs`, and public `LiteDbDatabaseOwner.cs`. Register one keyed singleton owner per name and one keyed scoped session per name using existing .NET DI. The owner creates and owns the existing `Repositories/LiteDbContext.cs`, a private mapper, and its executor. Validate Direct mode, file ownership, duplicate names, and collection mappings. Use the container's service provider when resolving services. Do not call `BuildServiceProvider` during registration. | Yes | 2026-11-19 |
| TASK-005 | Add public `src/Infrastructure.LiteDB/Transactions/LiteDbDatabaseExecutor.cs`, `LiteDbDatabaseSession.cs`, and `LiteDbDatabaseTransaction.cs`. Implement the execution and ownership rules below with one dedicated native worker per database owner. Implement both existing `IDatabaseTransaction.ExecuteScopedAsync` overloads. Session disposal rolls back unfinished work. Owner shutdown drains accepted work, closes native state, stops the worker, and disposes its context. | Yes | 2026-11-19 |
| TASK-006 | Add `src/Infrastructure.LiteDB/ActiveEntity/LiteDbActiveEntityMapping.cs`. Configure scalar primitive and generated typed-ID mappings using the existing `EntityId<T>.Value` and generated static `Create(primitive)` factory, native numeric allocation, and explicit `_id` mapping. Freeze mapper changes before use, exclude pending domain events, preserve value and audit data, and validate configured reference mappings. Do not modify `BsonMapper.Global`. | Yes | 2026-11-19 |
| TASK-007 | Add `src/Infrastructure.LiteDB/ActiveEntity/LiteDbActiveEntityProviderOptions.cs`. Validate the options defined in section 1. Add owner and session registration, isolation, initialization, and disposal tests in `LiteDbActiveEntityRegistrationTests.cs`. The entity provider registration extension follows in TASK-012 after the concrete provider exists. | Yes | 2026-11-19 |

The transaction design is an adapter around native thread ownership, with these execution rules:

1. A bounded native command queue has capacity `256`. Commands contain synchronous database work only. They complete tasks with continuations scheduled asynchronously. Neither user callbacks nor `ExecuteScopedAsync` delegates run on the worker.
2. Each scoped session has an ownership token. Beginning an explicit transaction reserves the worker for that token until commit or rollback. Other sessions wait asynchronously before admission. The worker must never execute their native commands inside the reserved transaction.
3. Commands from the owning session run on the same worker thread across caller awaits. Materialize query results and dispose cursors on that thread. Never return a live LiteDB cursor to caller code.
4. A standalone write uses the same reservation for its native transaction and callback sequence. Calls made by a supplied callback through its provider carry the same operation token and join that operation. They must not wait on their own reservation or commit it early. Reject unrelated overlapping operations on that session with `InvalidOperationError` while its implicit write is active. Serialize native commands within an explicit session transaction.
5. `BeginTransactionAsync` starts native state and returns a non-null adapter. `ExecuteScopedAsync` invokes and awaits its delegate once, without retrying it or committing automatically. The existing core calls `CommitTransactionAsync` or `RollbackAsync` after the delegate returns. Repeated begin on the same active session joins it without creating a second native transaction. A completion call without active state returns `InvalidOperationError`. Check native completion return values. A false native commit must not produce a successful result.
6. On cancellation, callback failure, or native failure, preserve the original error, roll back on the owner thread, and release the reservation. Never continue a failed transaction as an implicitly committed write. Disposal performs the same cleanup without depending on the canceled caller token. A canceled command that has not started is removed or skipped before mutation.

Phase 2 passes when independent scopes share the owner but not transaction state, session reentry completes, scope disposal rolls back, and disposing one session scope leaves the database usable by other scopes. A caller that wants work to join a transaction must use its supplied context or the same DI-scoped session. Calls that independently create fresh scopes do not gain ambient transaction membership.

### Implementation Phase 3

- **GOAL-003**: Implement the complete ActiveEntity contract with LiteDB-local helpers. Depends on Phase 2. TASK-008 precedes TASK-009 and TASK-010. TASK-011 follows both.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-008 | Add `src/Infrastructure.LiteDB/ActiveEntity/LiteDbActiveEntityProvider.cs` implementing `IActiveEntityEntityProvider<TEntity, TId>` with the interface's `where TEntity : class, IEntity` constraint. Add public `LiteDbActiveEntityQuery.cs` for specification normalization, native filtering, includes, ordering, distinct selection, and paging. Keep shared terminal operations consistent across entity, projection, ID, count, and existence results. Validate unsupported shapes before mutation. | Yes | 2026-11-19 |
| TASK-009 | Implement all read overloads in the provider, including paged projections and ID results. Count paged totals before paging, propagate failed queries, retain typed IDs, and return detached values. Reuse `WhereExpressions` only after verifying it preserves the requested filters. Do not reuse `OrderByIf` without proving compound order precedence. Bound materialization and dispose every cursor inside executor work. | Yes | 2026-11-19 |
| TASK-010 | Implement insert, update, upsert, entity delete, and callback dispatch in the provider and its `LiteDbActiveEntityProvider.Write.cs` partial. Insert must not overwrite an existing record. Update of a missing record must not insert one. Return `ConflictError` or `NotFoundError` respectively. Determine the upsert action inside the same transaction as its write. Compare and advance concurrency versions atomically. Invoke callbacks in the order below and preserve callback errors. | Yes | 2026-11-19 |
| TASK-011 | Add public `src/Infrastructure.LiteDB/ActiveEntity/LiteDbActiveEntitySetOperations.cs` and implement both `UpdateSetAsync` and both `DeleteSetAsync` overloads. Reuse `EntityUpdateSet<TEntity>` for constant and computed assignments. Capture the bounded target selection before changing fields used by filtering, ordering, or paging. Apply the entire set in one native transaction, processing mutations in batches of `128` and returning the confirmed affected count as `long`. Reject identity changes and invalid assignments without partial publication. Join an explicit session transaction when present. | Yes | 2026-11-19 |
| TASK-012 | Implement provider `BeginTransactionAsync`, `CommitTransactionAsync`, and `RollbackAsync` through the scoped session. Add `src/Infrastructure.LiteDB/ActiveEntity/Configuration/ActiveEntityProviderConfiguratorExtensions.cs` and the `UseLiteDbProvider` extension through `UseProviderFactory`, resolving the selected keyed scoped session. Centralize failure conversion with existing `NotFoundError`, `ConflictError`, `ConcurrencyError`, `InvalidInputError`, `InvalidOperationError`, and `ExceptionError`. Preserve cancellation and transient-exception conventions, unrelated provider registrations, and structured logging without connection strings, passwords, or entity payloads. | Yes | 2026-11-19 |

For insert and update, invoke `BeforeUpsertAsync`, the relevant `BeforeInsertAsync` or `BeforeUpdateAsync`, the mutation, the matching after-callback, and `AfterUpsertAsync`. Upsert chooses the insert or update branch from the actual stored state. Delete invokes `BeforeDeleteAsync`, the mutation, and `AfterDeleteAsync`. Stop on callback failure and roll back before returning. Set operations do not manufacture per-entity callbacks or behavior invocations absent from their contract.

Phase 3 passes when every interface member has a working path and no placeholder throws remain in the new provider. The existing generic repository's unfinished methods remain outside this plan.

### Implementation Phase 4

- **GOAL-004**: Prove provider behavior through the unchanged ActiveEntity APIs and document support accurately. Depends on Phase 3. Complete TASK-013 before TASK-014 and TASK-015.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-013 | Complete the tests in section 6, including direct provider tests and public static and instance ActiveEntity calls. Add a test-only behavior recorder and validator to verify pipeline order and short-circuiting. Isolate and restore the process-wide ActiveEntity service provider using existing test patterns. Do not attach the SQL/container-backed `ActiveEntityTestEnvironmentCollection`. | Yes | 2026-11-19 |
| TASK-014 | Run the sequential checks below. Preserve the existing repository suite. Require no changes to ActiveEntity core or unrelated runtime projects and inspect the dependency graph for EF references. Verify shutdown releases files and worker threads. Record actual results and any environment-limited checks outside `docs/`. | Yes | 2026-11-19 |
| TASK-015 | Update `docs/features-infrastructure-litedb.md` and `docs/features-domain-activeentity.md` with the implemented registration APIs, supported mappings, query limits, transaction context usage, and upstream development restriction. Update COV-002 progress in the parent specification and this plan only after required tests pass. Do not mark the whole LiteDB extension complete. | Yes | 2026-11-19 |

Run these commands sequentially from the repository root with the SDK selected by `global.json`:

```shell
dotnet restore tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj
dotnet build src/Infrastructure.LiteDB/Infrastructure.LiteDB.csproj --no-restore
dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~LiteDB
dotnet test tests/Domain.UnitTests/Domain.UnitTests.csproj --filter FullyQualifiedName~ActiveEntity
git diff --check
```

The infrastructure filter must discover the new `LiteDB.ActiveEntity` tests and the existing `LiteDbGenericRepositoryTests`. Run the dependency-free existing in-memory ActiveEntity scenarios separately when their fixture permits it. Do not run SQL-backed provider tests without their required services. A focused pass using existing dependency outputs must be reported as such, without claiming a clean full rebuild.

### Verification completed on 2026-11-19

- The two engine probes passed before provider implementation. They proved native-thread ownership across caller awaits, isolation from an unrelated reader, atomic commit and rollback, typed identities, stored value preservation, and exclusion of pending events.
- The LiteDB integration filter passed all `110` cases: `57` new engine and ActiveEntity cases and `53` existing repository regressions. Coverage includes numeric allocation across concurrent scopes and reopen, encrypted files, direct reference includes, audit state, callback reentry, bounded set updates, cancellation during a second batch, native commit returning false, queue admission at capacity, and owner shutdown with an active transaction.
- The Domain ActiveEntity unit filter passed `10` cases. The existing in-memory integration class was not run separately because its collection uses the external-service test fixture. New LiteDB cases do not require that fixture or Docker.
- The focused LiteDB build, including XML documentation generation, passed with zero warnings and errors. New runtime APIs are public and documented. Source checks found no internal modifiers, generic repository dependencies, automatic upstream-risk acknowledgement, or EF dependency in the LiteDB runtime package.
- Validation used the repository-selected SDK, isolated compiler settings, and existing unchanged dependency outputs on Linux. A clean solution rebuild and a platform matrix remain unverified. Execution logs and TRX reports are kept outside the documentation tree.
- Completion establishes development functionality for COV-002. It does not complete the broader migration review, other LiteDB providers, or upstream production readiness. Hierarchy queries and `ThenInclude` return explicit failures. Materialization limits and the existing string-ID generator limitation are documented in the LiteDB Infrastructure guide.

## 3. Alternatives

- **ALT-001**: Use `LiteDbGenericRepository<TEntity>` as the ActiveEntity persistence adapter. Rejected by the agreed direction. Implement the provider directly against `LiteDbContext` and native LiteDB APIs, independently of COV-001 and the generic repository classes.
- **ALT-002**: Change ActiveEntity or its provider interface to fit LiteDB. Excluded by the agreed direction. Make no changes to ActiveEntity core, contracts, behaviors, query DSL, or source generators. Use the existing configurator and callback contracts as integration points.
- **ALT-003**: Call native begin and commit from arbitrary async continuations or wrap individual operations in `Task.Run`. Rejected because neither preserves native transaction thread ownership. An async semaphore alone does not establish that ownership.
- **ALT-004**: Serialize the entire asynchronous user delegate on the native worker, or block it with `.Result`. Rejected because callbacks can await provider operations, which would deadlock the worker. Only synchronous native commands run there.
- **ALT-005**: Ship CRUD while leaving transaction or set methods as selectable placeholders. Rejected because COV-002 requires the whole existing contract. Record upstream blockers explicitly instead.

## 4. Dependencies

- **DEP-001**: Official `LiteDB` package `6.0.0-prerelease.355`, source revision `b3c4406231908c6ca29f6d4b2769aa08351d6fc2`, already managed centrally. No package upgrade is planned here.
- **DEP-002**: Existing Domain contracts and APIs: `IActiveEntityEntityProvider`, `ActiveEntityConfigurator.UseProviderFactory`, `ActiveEntityCallbackOptions`, `IDatabaseTransaction`, `IEntityIdGenerator`, `EntityId<T>`, `IConcurrency`, `EntityUpdateSet`, specifications, and find options. They are read-only dependencies for this work.
- **DEP-003**: Existing `LiteDbContext`, Common results, options and logging helpers, .NET keyed DI, and BCL queues and synchronization. The project already references Domain. Add no separate ActiveEntity implementation package.
- **DEP-004**: The common v6 migration gate remains authoritative. Phase 1 proves the additional engine behavior needed for this provider. Unverified recovery or platform requirements and upstream production restrictions must remain visible in the parent spec.
- **DEP-005**: COV-003 outbox storage and other LiteDB providers remain separate work. Existing provider-neutral event publishing can run through its registered behavior. This plan does not add an EF-specific outbox behavior or duplicate event delivery.

## 5. Files

- **FILE-001**: Add database registration and ownership files under `src/Infrastructure.LiteDB/Configuration/` as listed in TASK-004.
- **FILE-002**: Add executor, scoped session, and transaction adapter files under `src/Infrastructure.LiteDB/Transactions/` as listed in TASK-005.
- **FILE-003**: Add provider, options, mapping, query, set-operation helper, and configurator extension under `src/Infrastructure.LiteDB/ActiveEntity/` as listed in TASK-006 through TASK-012.
- **FILE-004**: Add `LiteDbActiveEntityEngineTests.cs`, `LiteDbActiveEntityProviderTests.cs`, `LiteDbActiveEntityQueryTests.cs`, `LiteDbActiveEntitySetOperationTests.cs`, `LiteDbActiveEntityPipelineTests.cs`, and the fixture, registration, mapping, and transaction files under `tests/Infrastructure.IntegrationTests/LiteDB/ActiveEntity/`. Use that namespace for the test filter.
- **FILE-005**: Update `docs/features-infrastructure-litedb.md`, `docs/features-domain-activeentity.md`, `docs/specs/spec-infrastructure-litedb-providers.md`, and this plan after implementation verification.
- **FILE-006**: Read existing context and repository helpers, existing ActiveEntity core and providers, and existing test scenarios for reuse and comparison. Their runtime files remain unchanged under this plan. In particular, no `src/Domain/` change is allowed.

## 6. Testing

- **TEST-001**: CRUD preserves records after database reopen. Duplicate insert and missing update fail without mutation. Upsert reports the correct `RepositoryActionResult` and does not create duplicates under competing scopes. Missing lookup is `NotFoundError`; empty list and count remain successful.
- **TEST-002**: Primitive and generated typed GUID, string, integer, and long IDs round-trip through insert, lookup by ID and string representation, projections, ID lists, and reopen. Numeric allocation remains collision-free across scopes and reopen. Invalid IDs fail before query translation without modifying records. A configured ID generator is honored.
- **TEST-003**: Embedded graphs, UTC instants, decimals, binary values, audit fields, and configured references round-trip. Inspect raw BSON to prove `DomainEvents` is absent. Reopened entities do not acquire pending events. Separate mapper instances do not change each other's settings.
- **TEST-004**: All query overloads cover no specification, single and multiple specifications, null or empty specification collections, no matches, compound mixed-direction ordering, distinct selection, projections, IDs, and paging. Verify total count before paging, default page metadata, empty later pages, and propagation of query errors. A limit violation returns a failure rather than a truncated success.
- **TEST-005**: Set updates support constant and computed assignments, multiple specifications, ordered and paged target selection, zero matches, and confirmed counts. Updating a field used by the selection does not change the remaining targets. Invalid setters, identity changes, cancellation, and a later-row failure leave no partial committed set. Delete sets retain rows outside the captured selection.
- **TEST-006**: Two detached copies of an `IConcurrency` entity demonstrate one successful update and one stale failure with `ConcurrencyError`. A stale delete also fails. Set updates invalidate stale versions. Test entities without concurrency and the disabled-concurrency option. Failed writes do not leave a generated concurrency version on the caller entity as though it committed.
- **TEST-007**: Test callbacks directly and the behavior pipeline through unchanged `InsertAsync`, `UpdateAsync`, `UpsertAsync`, `DeleteAsync`, query DSL, and paged APIs. Verify callback and behavior order, short-circuiting validation, awaited callback reentry through its provider, and one invocation per stage. Preserve existing domain-event publishing behavior without serializing or publishing events a second time.
- **TEST-008**: Both unchanged `WithTransactionAsync` overloads commit multiple operations on success and roll them back on failed results and exceptions. Force caller continuation changes with asynchronous barriers. Test reads of the session's own writes, explicit begin and completion, both `ExecuteScopedAsync` overloads, repeated begin, and completion without active state.
- **TEST-009**: Hold one session transaction open while a second scope attempts access. Verify the second scope neither observes uncommitted data nor commits into the first transaction. Include providers for different entity types in the same DI-scoped session, callback reentry, unrelated overlapping calls during an implicit write, a failed session that cannot commit, native commit returning false, and abandoned session disposal. No race relies on timing sleeps.
- **TEST-010**: Test pre-canceled operations, cancellation while waiting for ownership or queue admission, cancellation between set batches, and shutdown with accepted work. Rollback and cleanup complete even with a canceled caller token. Assert that the file can reopen and no worker remains after owner disposal.
- **TEST-011**: Registration tests cover shared default owner, explicit additional database names, distinct entity collections, mapper initialization retry, invalid paths, duplicate file ownership, Shared-mode rejection, and mixed LiteDB and in-memory entity providers. Verify existing `ActiveEntityRegistry` discovery without editing Presentation.
- **TEST-012**: Run the existing LiteDB repository tests and Domain ActiveEntity unit tests unchanged. Check the runtime change list against CON-001 and the LiteDB package dependency graph against CON-002. Keep all new LiteDB tests independent of Docker, SQL, and cloud services.

## 7. Risks & Assumptions

- **RISK-001**: Native transaction ownership differs from the asynchronous ActiveEntity API. The dedicated executor is a DevKit adapter, not an upstream async feature. Phase 1 proved its use of the pinned engine. Recheck this behavior when changing the engine or executor.
- **RISK-002**: One native worker per database serializes provider database work and an explicit transaction can delay other sessions. Keep admission cancelable and bounded. Correctness comes first. Throughput measurement and a different scheduling strategy are follow-up work.
- **RISK-003**: LiteDB expression translation and mapper behavior differ from EF. The existing query helper repeatedly calls `OrderBy`, and the generic repository has no functioning include helper. Verify semantics rather than copying those paths unchanged. Unsupported required shapes prevent declaring full COV-002 support.
- **RISK-004**: Callback failure and commit failure must not leave the session reusable as if the write succeeded. Native commit errors can have uncertain outcomes. Return the failure, perform safe cleanup, and do not retry user delegates or report rollback success without evidence.
- **RISK-005**: ActiveEntity service-provider configuration and the prerelease acknowledgement are process-wide. New tests must isolate global configuration and run in a nonparallel collection without bringing up external services.
- **ASSUMPTION-001**: There are no existing LiteDB package consumers or historical storage models to support. The new provider stores stable models. Public provider-neutral contracts and core code remain fixed.
- **ASSUMPTION-002**: Embedded document persistence is the default. Applications that need references configure LiteDB mappings in the provider registration. Relational graph merge and tracking behavior are not added.
- **RISK-006**: The existing source generator emits duplicate members for string IDs. Tests use its generated Guid, int, and long identities and a handwritten `EntityId<string>` with the same public factory shape. String identity persistence passes without modifying the generator.
- **ASSUMPTION-003**: Production availability depends on a suitable upstream release. Passing this plan's tests on the pinned development package establishes development functionality only.

## 8. Related Specifications / Further Reading

- [LiteDB Storage Providers, COV-002 and REQ-016](../specs/spec-infrastructure-litedb-providers.md).
- [ActiveEntity feature guide](../features-domain-activeentity.md).
- [ActiveEntity provider contract](../../src/Domain/ActiveEntity/Provider/IActiveEntityEntityProvider.cs), [configurator extension point](../../src/Domain/ActiveEntity/Configuration/IActiveEntityConfigurator.cs), and [core transaction calls](../../src/Domain/ActiveEntity/ActiveEntity_Transactions.cs).
- [Existing EF provider](../../src/Infrastructure.EntityFramework/ActiveEntity/EntityFrameworkActiveEntityProvider.cs) and [in-memory provider](../../src/Domain/ActiveEntity/Provider/ActiveEntityInMemoryProvider.cs), for behavior comparison only.
- [Clean Architecture](../adr/0001-clean-onion-architecture.md) and [modular monolith boundaries](../adr/0003-modular-monolith-architecture.md).
- [Pinned upstream transaction implementation](https://github.com/litedb-org/LiteDB/blob/b3c4406231908c6ca29f6d4b2769aa08351d6fc2/LiteDB/Engine/Engine/Transaction.cs) and [database transaction API](https://github.com/litedb-org/LiteDB/blob/b3c4406231908c6ca29f6d4b2769aa08351d6fc2/LiteDB/Client/Database/LiteDatabase.cs). Both describe transactions owned by the calling thread. The provider design above is an inference from that constraint and must be verified by tests.
