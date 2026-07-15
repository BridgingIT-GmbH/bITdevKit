---
goal: Refactor entity bulk insert into a provider-neutral Entity Framework extension point
version: 1.2
date_created: 2026-07-10
last_updated: 2026-07-10
owner: bITdevKit
status: 'Complete'
tags: [refactor, architecture, entity-framework, repositories, bulk-insert]
---

# Introduction

![Status: Complete](https://img.shields.io/badge/status-Complete-brightgreen)

This plan separates provider-neutral Entity Framework bulk-insert orchestration from provider-native database writing. The shared `Infrastructure.EntityFramework` project will own `.WithBulkInsert()`, the public bulk-insert port, option validation, entity metadata mapping, generated-value preparation, provider selection, and consistent error handling. Each DevKit `Add*DbContext<TContext>` provider setup automatically registers its native strategy; the orchestrator selects that strategy from `DbContext.Database.ProviderName` when an insert runs. The `Infrastructure.EntityFramework.SqlServer` project will retain only SQL Server concerns such as `SqlConnection`, `SqlTransaction`, `SqlBulkCopy`, SQL Server identifier quoting, and `SqlBulkCopyOptions`. A future provider will be added by implementing and registering the provider strategy in its own provider assembly without modifying the shared orchestrator. Because the feature is unreleased, the refactor will directly remove superseded SQL Server types and APIs instead of carrying migration aliases.

## 1. Requirements & Constraints

- **REQ-001**: Preserve `IEntityBulkInserter<TEntity>.InsertAsync(IEnumerable<TEntity>, CancellationToken)` as the infrastructure-facing API used by current consumers.
- **REQ-002**: Move entity normalization, EF model lookup, navigation validation, same-table owned-reference flattening, generated/computed column filtering, value-converter application, sequential GUID assignment, and concurrency-version assignment into `src/Infrastructure.EntityFramework/Repositories/Bulk/`.
- **REQ-003**: Add a provider strategy contract whose implementation is selected by the exact value of `DbContext.Database.ProviderName`.
- **REQ-004**: Adding a future provider must require only a provider implementation, an automatic registration hook in that provider's `Add*DbContext<TContext>` methods, optional provider-specific options, tests, and documentation in that provider package; it must not require a `switch` or provider-name edit in the shared orchestrator.
- **REQ-005**: Preserve current successful SQL Server behavior for flat entities, primitive and typed generated GUID keys, EF value converters, same-table owned references, ambient EF transactions, automatic internal transactions, batching, timeouts, and inserted-row counts.
- **REQ-006**: Preserve explicit rejection of non-owned navigations and populated owned collections; the failure must identify the unsupported navigation names.
- **REQ-007**: Return `Result<long>.Success(0)` for an empty or null-normalized input sequence without opening a connection or invoking a provider.
- **REQ-008**: Return a failed `Result<long>` containing an actionable `NotSupportedException` when no registered provider supports the current EF provider name; do not silently fall back to row-by-row `AddRange`/`SaveChanges`.
- **REQ-009**: Detect and fail on duplicate provider registrations for the same EF provider name so DI configuration errors cannot produce nondeterministic selection.
- **REQ-010**: Define provider-neutral options for batch size, command timeout, sequential GUID assignment, concurrency-version assignment, and preservation of generated identity values.
- **REQ-011**: Keep `SqlBulkCopyOptions` and every `Microsoft.Data.SqlClient` type inside `Infrastructure.EntityFramework.SqlServer`.
- **REQ-012**: Keep `.WithBulkInsert()` as the only repository-facing registration API. Move it to `Infrastructure.EntityFramework` so its signature and behavior are identical for SQL Server, PostgreSQL, SQLite, and future relational providers.
- **REQ-013**: Centralize exception-to-`Result<long>` conversion in the shared orchestrator and continue rethrowing `OperationCanceledException`.
- **REQ-014**: Document the complete steps required to add PostgreSQL or SQLite support, including provider name, native write API, transaction use, identifier quoting, options, registration, and contract tests.
- **REQ-015**: Each DevKit provider setup method must register its native bulk strategy automatically. `AddSqlServerDbContext<TContext>` must register the SQL Server strategy; future `AddPostgresDbContext<TContext>` and `AddSqliteDbContext<TContext>` implementations must register their strategies when those writers exist.
- **REQ-016**: The shared orchestrator must select the strategy using `DbContext.Database.ProviderName`; it must never parse or infer the provider from a connection string.
- **CON-001**: Do not place the bulk-insert port on `IGenericRepository<TEntity>`; direct native bulk insert intentionally bypasses repository decorators, domain events, outbox behavior, EF tracking, and `SaveChanges` interceptors.
- **CON-002**: Do not implement PostgreSQL or SQLite native writers in this refactor. Their implementations are separate follow-up features built against the new contract.
- **CON-003**: Do not introduce a commercial or cross-provider bulk library. SQL Server continues to use `Microsoft.Data.SqlClient.SqlBulkCopy`.
- **CON-004**: Do not expose `DataTable`, `SqlBulkCopy`, provider-specific connections, provider-specific transactions, or provider-specific option enums from the shared provider contract.
- **CON-005**: Preserve the existing service lifetime from `EntityFrameworkRepositoryBuilderContext<TEntity, TContext>` when registering the typed orchestrator. Provider strategies must be stateless singleton services that receive the active `DbContext` and prepared batch per call.
- **CON-006**: Keep repository-wide `dotnet build` and `dotnet test` commands sequential to avoid transient MSBuild output-lock failures.
- **CON-007**: Verification requires the .NET SDK version pinned in `global.json` (`10.0.301`); install that SDK before executing build or test tasks because the inspected machine currently has `10.0.203` as its newest .NET 10 SDK.
- **CON-008**: Automatic native-strategy registration is guaranteed when the consumer uses the DevKit provider setup methods such as `AddSqlServerDbContext<TContext>`. A consumer that configures EF only through raw `AddDbContext<TContext>(options => options.UseSqlServer(...))` must receive a clear missing-provider failure instead of reflection-based assembly scanning or a silent fallback.
- **GUD-001**: Use composition with a provider strategy and shared mapping descriptor instead of an abstract base class whose protected members would couple every provider to SQL Server-oriented row materialization.
- **GUD-002**: Keep all new public classes, interfaces, methods, and properties documented with XML comments and usage examples.
- **GUD-003**: Use exact provider matching, ordinal comparison, structured logging, and the existing `Result<T>`/`ExceptionError` conventions.
- **PAT-001**: Follow the existing split between `Infrastructure.EntityFramework` and the provider assemblies `Infrastructure.EntityFramework.SqlServer`, `Infrastructure.EntityFramework.Postgres`, and `Infrastructure.EntityFramework.Sqlite`.
- **PAT-002**: Represent a prepared insert as provider-neutral table metadata plus ordered column descriptors and entity value accessors; let each native provider choose its own wire representation.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Record the provider strategy decision and establish provider-neutral contracts.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `docs/adr/0027-provider-strategy-for-entity-bulk-insert.md` with status `Accepted`. Document the context, the provider strategy decision, automatic strategy registration through DevKit `Add*DbContext<TContext>` methods, shared-versus-provider responsibility table, positive and negative consequences, failure behavior, and rejected alternatives: one shared SQL-oriented base class, a shared provider-name `switch`, provider-specific `With*BulkInsert` methods, duplicate `.WithBulkInsert()` extensions in every provider assembly, and silent `AddRange` fallback. Add ADR-0027 to `docs/adr/README.md`. | ✅ | 2026-07-10 |
| TASK-002 | Move the neutral members of `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/EntityBulkInsertOptions.cs` into a new `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs`. Define `BatchSize = 1_000`, `CommandTimeout = 30`, `AssignSequentialGuidKeys = true`, `AssignConcurrencyVersions = true`, and `KeepGeneratedIdentityValues = false`. Validate `BatchSize > 0` and `CommandTimeout >= 0` before provider dispatch. | ✅ | 2026-07-10 |
| TASK-003 | Add `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInsertProvider.cs`. Define the non-generic strategy contract `string ProviderName { get; }` and `Task<long> InsertAsync<TEntity>(DbContext context, EntityBulkInsertBatch<TEntity> batch, CancellationToken cancellationToken = default) where TEntity : class`. Document that strategies are stateless, are registered once by their provider package, receive the active context per call, throw provider-native exceptions, and rely on the shared orchestrator to convert those exceptions to `Result<long>`. | ✅ | 2026-07-10 |
| TASK-004 | Add `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertBatch.cs` and `EntityBulkInsertColumn.cs`. `EntityBulkInsertBatch<TEntity>` must contain the EF `IEntityType`, schema name, table name, an `IReadOnlyList<TEntity>`, an ordered `IReadOnlyList<EntityBulkInsertColumn<TEntity>>`, and the validated `EntityBulkInsertOptions`. `EntityBulkInsertColumn<TEntity>` must contain the column name, provider CLR type, generated-value metadata, and a value accessor that applies the EF `ValueConverter`. Do not include a provider-native connection, transaction, quoted table name, or `DataTable`. | ✅ | 2026-07-10 |

### Implementation Phase 2

- GOAL-002: Extract reusable EF metadata preparation from the SQL Server writer.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-005 | Create `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilder.cs` by extracting `EnsureSupportedNavigations`, `AssignClientGeneratedValues`, `CreatePropertyMappings`, `ShouldInclude`, `GetProviderValue`, `IsMappedToSameTable`, `HasCollectionItems`, `IsGuidProviderProperty`, `IsDefaultGuidKey`, `SetGuidKey`, and provider CLR-type resolution from `SqlServerEntityBulkInserter<TEntity, TContext>`. Make the builder depend only on EF relational metadata, `IConcurrency`, `EntityId<Guid>`, `GuidGenerator`, and neutral options. | ✅ | 2026-07-10 |
| TASK-006 | In `EntityBulkInsertMappingBuilder<TEntity>.Build(DbContext, IReadOnlyList<TEntity>, EntityBulkInsertOptions)`, resolve `IEntityType` from `DbContext.Model`, reject an unmapped entity, validate navigations, assign configured client-generated values, flatten only owned references mapped to the root table, omit shadow/computed columns, omit store-generated identity columns unless `KeepGeneratedIdentityValues` is true, and reject duplicate writable column names using `StringComparer.OrdinalIgnoreCase`. Return `EntityBulkInsertBatch<TEntity>`. | ✅ | 2026-07-10 |
| TASK-007 | Add focused tests in `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilderTests.cs` for flat mappings, same-table owned references, null owned references, value converters, computed/generated column exclusion, identity preservation, primitive GUID assignment, typed `EntityId<Guid>` assignment, concurrency-version assignment, duplicate columns, non-owned navigations, populated owned collections, entity-not-in-model, and no-writable-column failures. | ✅ | 2026-07-10 |

### Implementation Phase 3

- GOAL-003: Add the shared orchestrator and deterministic provider dispatch.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Add `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs`. Implement `IEntityBulkInserter<TEntity>` in `EntityFrameworkEntityBulkInserter<TEntity, TContext>` using `TContext`, `EntityBulkInsertMappingBuilder<TEntity>`, the closed-generic `EntityBulkInsertConfiguration<TEntity, TContext>`, `IEnumerable<IEntityBulkInsertProvider>`, and `ILogger`. Normalize input once, return zero for no items, validate options, select exactly one strategy whose `ProviderName` equals `TContext.Database.ProviderName` with `StringComparison.Ordinal`, build the batch, invoke `provider.InsertAsync(this.context, batch, cancellationToken)`, and convert non-cancellation exceptions to `Result<long>.Failure().WithError(new ExceptionError(ex))`. | ✅ | 2026-07-10 |
| TASK-009 | Make missing-provider and duplicate-provider errors deterministic in `EntityFrameworkEntityBulkInserter<TEntity, TContext>`. The missing-provider message must contain the entity type, current EF provider name, and registered provider names. The duplicate-provider message must contain the duplicate EF provider name and implementation type names. | ✅ | 2026-07-10 |
| TASK-010 | Add `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertConfiguration.cs` and `ServiceCollectionExtensions.cs`. Define the public `.WithBulkInsert(EntityBulkInsertOptions options = null)` extension on `EntityFrameworkRepositoryBuilderContext<TEntity, TContext>` in the shared project. The internal `EntityBulkInsertConfiguration<TEntity, TContext>` must wrap the neutral or provider-derived options so different entity/context registrations can use different settings. Register this configuration, `EntityBulkInsertMappingBuilder<TEntity>`, and `EntityFrameworkEntityBulkInserter<TEntity, TContext>` using the repository lifetime. Do not register or reference a concrete database provider from this method. | ✅ | 2026-07-10 |
| TASK-011 | Add `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserterTests.cs` with fake non-generic provider strategies. Verify empty input bypasses mapping/provider invocation, exact `Database.ProviderName` selection, missing-provider failure, duplicate-provider failure, provider exception conversion, cancellation propagation, active-DbContext propagation, inserted count propagation, and preservation of scoped/transient/singleton orchestrator lifetime. | ✅ | 2026-07-10 |

### Implementation Phase 4

- GOAL-004: Convert SQL Server into the first provider strategy without changing its database behavior.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-012 | Add `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertOptions.cs` deriving from the neutral `EntityBulkInsertOptions`. Move `SqlBulkCopyOptions` into this type. Do not add `BulkCopyTimeout` or any obsolete migration member. Document that `CommandTimeout` configures `SqlBulkCopy.BulkCopyTimeout`, `KeepGeneratedIdentityValues` is the only supported identity-preservation setting, and callers must not include `KeepIdentity` or `UseInternalTransaction` in `SqlBulkCopyOptions`. | ✅ | 2026-07-10 |
| TASK-013 | Create `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs` by moving only native-write responsibilities out of `SqlServerEntityBulkInserter<TEntity, TContext>`. Implement the non-generic `IEntityBulkInsertProvider` with provider name `Microsoft.EntityFrameworkCore.SqlServer`. In generic `InsertAsync<TEntity>`, consume the supplied `DbContext` and `EntityBulkInsertBatch<TEntity>`, materialize ordered column descriptors into a `DataTable`, delimit SQL Server schema/table identifiers, use the active `SqlConnection` and ambient `SqlTransaction`, and preserve `UseInternalTransaction` when no ambient transaction exists. Do not copy EF metadata discovery, navigation validation, generated-value assignment, column inclusion decisions, or value-converter logic into the provider. | ✅ | 2026-07-10 |
| TASK-014 | Delete `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInserter.cs` after its native writer logic has moved to the non-generic `SqlServerEntityBulkInsertProvider`. Update every construction, type reference, XML example, and test to resolve the shared `IEntityBulkInserter<TEntity>` orchestrator or, when testing the native boundary directly, construct `SqlServerEntityBulkInsertProvider`. Do not retain a wrapper or obsolete type. | ✅ | 2026-07-10 |
| TASK-015 | Delete `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/ServiceCollectionExtensions.cs` after `.WithBulkInsert()` moves to the shared project. Update every overload of `AddSqlServerDbContext<TContext>` in `src/Infrastructure.EntityFramework.SqlServer/ServiceCollectionExtensions.cs` to call one private `TryAddSqlServerEntityBulkInsertProvider(IServiceCollection)` helper. The helper must use `TryAddEnumerable` to register one singleton `IEntityBulkInsertProvider` implemented by `SqlServerEntityBulkInsertProvider`, so repeated SQL Server DbContext registrations do not create duplicate strategies. | ✅ | 2026-07-10 |
| TASK-016 | In `SqlServerEntityBulkInsertProvider.InsertAsync<TEntity>`, interpret `batch.Options` as `SqlServerEntityBulkInsertOptions` when the caller supplied that derived type and otherwise use `SqlBulkCopyOptions.Default`. Before opening the connection, reject configured `KeepIdentity` and `UseInternalTransaction` flags with an `ArgumentException` that names the neutral replacement behavior. `KeepGeneratedIdentityValues` controls mapping inclusion and adds `KeepIdentity`; an ambient EF transaction omits `UseInternalTransaction`; no ambient transaction adds `UseInternalTransaction`; caller-supplied non-owned flags such as `TableLock` remain unchanged. | ✅ | 2026-07-10 |

### Implementation Phase 5

- GOAL-005: Verify SQL Server parity and demonstrate that another provider can be added without shared-code changes.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Rename `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/SqlServerEntityBulkInserterTests.cs` to `SqlServerEntityBulkInsertProviderTests.cs`. Register the context through `AddSqlServerDbContext<StubDbContext>()`, register the repository through `.WithBulkInsert()`, and resolve `IEntityBulkInserter<TEntity>` for end-to-end tests. Preserve the current flat-entity, same-table owned-reference, owned-collection rejection, generated GUID, row-count, transaction, and registration assertions; add automatic strategy registration, identity-preservation, forbidden-option validation, and provider-specific option translation coverage. | ✅ | 2026-07-10 |
| TASK-018 | Add a test-only non-generic provider in `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/TestEntityBulkInsertProvider.cs` with a non-SQL Server provider name. Register it through `IEntityBulkInsertProvider` and prove the shared orchestrator dispatches to it without any change to `EntityFrameworkEntityBulkInserter<TEntity, TContext>`. This is the executable extension-point contract for future PostgreSQL and SQLite writers. | ✅ | 2026-07-10 |
| TASK-019 | Add `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertArchitectureTests.cs`. Assert that types in the shared `Repositories.Bulk` namespace do not reference `Microsoft.Data.SqlClient`, `SqlBulkCopy`, `Npgsql`, `Microsoft.Data.Sqlite`, or `System.Data.SQLite`. Read the four EF project files and assert that provider projects reference `Infrastructure.EntityFramework.csproj` while the shared project does not reference a provider project. | ✅ | 2026-07-10 |

### Implementation Phase 6

- GOAL-006: Document the provider extension recipe and complete sequential verification.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-020 | Update `docs/features-domain-repositories.md`: rename the section to `Explicit Provider Bulk Inserts`; explain that `AddSqlServerDbContext<TContext>` registers the native strategy while `.WithBulkInsert()` registers the typed shared orchestrator; list neutral versus optional SQL Server options; preserve the warnings about bypassed repository behaviors; document the clear failure for raw `AddDbContext(...UseSqlServer(...))` when no DevKit provider strategy was registered; and add a `Create a New Provider` checklist with exact strategy and automatic-registration steps. | ✅ | 2026-07-10 |
| TASK-021 | Keep `.WithBulkInsert()` in `examples/DoFiesta/DoFiesta.Presentation.Web.Server/Modules/Core/CoreModule.cs` and update `examples/DoFiesta/DoFiesta-README.md` to explain automatic strategy selection from the `CoreDbContext` EF provider. Continue injecting `IEntityBulkInserter<TodoItem>` in `TodoItemBulkImportPersistenceInterceptor`. | ✅ | 2026-07-10 |
| TASK-022 | Run `dotnet build src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj --no-restore`, then `dotnet build src/Infrastructure.EntityFramework.SqlServer/Infrastructure.EntityFramework.SqlServer.csproj --no-restore`, then `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --no-restore --filter FullyQualifiedName~EntityBulkInsert`, then `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SqlServerEntityBulkInsertProviderTests`, and finally `dotnet build --no-restore`. Run each command only after the previous command completes. | ✅ | 2026-07-10 |
| TASK-023 | Search the repository with `rg -n "EntityBulkInsertOptions|SqlBulkCopyOptions|WithSqlServerBulkInsert|SqlServerEntityBulkInserter|IEntityBulkInsertProvider" src tests examples docs --glob '!docs/site/**'`. Confirm all shared code is provider-neutral, `WithSqlServerBulkInsert` and `SqlServerEntityBulkInserter` have zero matches, `.WithBulkInsert()` is declared only in the shared project, each native strategy is registered only by its provider setup, no current branch consumer is missed, and no generated `docs/site/reference/` file was edited. | ✅ | 2026-07-10 |

## 3. Alternatives

- **ALT-001**: Introduce `EntityBulkInserterBase<TEntity, TContext>` and let every provider inherit the SQL Server implementation. Rejected because `DataTable`, identity flags, identifier quoting, connections, transactions, and wire formats differ materially between `SqlBulkCopy`, PostgreSQL binary `COPY`, and SQLite prepared commands; inheritance would make those SQL Server assumptions part of the provider contract.
- **ALT-002**: Keep all providers in one shared class with a `switch` on `DbContext.Database.ProviderName`. Rejected because every new provider would require modifying and republishing the shared package and would pull provider dependencies across assembly boundaries.
- **ALT-003**: Register each native writer directly as `IEntityBulkInserter<TEntity>` with no shared orchestrator. Rejected because input normalization, mapping, generated values, failure handling, and unsupported-navigation behavior would be duplicated and could drift between providers.
- **ALT-004**: Fall back to `DbSet.AddRange` plus `SaveChangesAsync` when no native provider exists. Rejected because the fallback has different tracking, interceptor, domain-event, performance, and transaction semantics and would hide deployment misconfiguration.
- **ALT-005**: Adopt a third-party cross-provider bulk library. Rejected because it adds a new dependency and licensing/behavior surface when the current requirement is a controlled extension seam around provider-native APIs.
- **ALT-006**: Implement PostgreSQL and SQLite writers in the same refactor. Rejected to keep this change focused on the foundation and SQL Server parity; each native provider requires separate performance, transaction, type, identity, and integration-test decisions.
- **ALT-007**: Define the same `.WithBulkInsert()` extension independently in every provider assembly. Rejected because a consumer referencing multiple provider packages would get ambiguous extension-method resolution and provider selection would occur at compile time instead of from the configured `DbContext`.
- **ALT-008**: Detect the provider by parsing the connection string. Rejected because connection strings are not a reliable provider contract; `DbContext.Database.ProviderName` is authoritative after EF configures the context.

## 4. Dependencies

- **DEP-001**: `src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj` and its existing `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Relational` references for metadata and relational table mappings.
- **DEP-002**: `src/Infrastructure.EntityFramework.SqlServer/Infrastructure.EntityFramework.SqlServer.csproj` and its existing `Microsoft.Data.SqlClient` and `Microsoft.EntityFrameworkCore.SqlServer` references for `SqlBulkCopy` execution.
- **DEP-003**: `BridgingIT.DevKit.Common.Result<T>`, `ExceptionError`, `GuidGenerator`, `EntityId<Guid>`, and `IConcurrency` for current result and generated-value behavior.
- **DEP-004**: `Microsoft.Extensions.DependencyInjection` `TryAddEnumerable`, the DevKit `Add*DbContext<TContext>` provider setup methods, and `EntityFrameworkRepositoryBuilderContext<TEntity, TContext>.Lifetime` for automatic strategy registration, provider discovery, and orchestrator lifetime parity.
- **DEP-005**: `tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj` for shared mapping/dispatcher tests and `tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj` plus the SQL Server test container for native writer tests.
- **DEP-006**: .NET SDK `10.0.301`, as required by `global.json`, before implementation verification can succeed.
- **DEP-007**: A future PostgreSQL provider should use `Npgsql.EntityFrameworkCore.PostgreSQL` and Npgsql binary `COPY`; a future SQLite provider should use the repository's selected SQLite ADO.NET stack and batched prepared commands. These are follow-up dependencies, not dependencies of this refactor.

## 5. Files

- **FILE-001**: `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInserter.cs` — preserved public consumer port and clarified provider-neutral documentation.
- **FILE-002**: `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInsertProvider.cs` — new provider strategy contract.
- **FILE-003**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserter.cs` — new shared orchestrator and provider dispatcher.
- **FILE-004**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs` — new neutral options and validation.
- **FILE-005**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertBatch.cs` — new prepared-operation model.
- **FILE-006**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertColumn.cs` — new provider-neutral ordered column descriptor.
- **FILE-007**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilder.cs` — extracted EF metadata, value preparation, and mapping logic.
- **FILE-008**: `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertConfiguration.cs` — closed entity/context option wrapper.
- **FILE-009**: `src/Infrastructure.EntityFramework/Repositories/Bulk/ServiceCollectionExtensions.cs` — shared lifetime-aware orchestrator registration.
- **FILE-010**: `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/EntityBulkInsertOptions.cs` — removed after its neutral members move to the shared project and its SQL Server members move to `SqlServerEntityBulkInsertOptions`.
- **FILE-011**: `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertOptions.cs` — SQL Server-only option type.
- **FILE-012**: `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInserter.cs` — deleted after native writer responsibilities move to the provider strategy.
- **FILE-013**: `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs` — SQL Server native provider strategy.
- **FILE-014**: `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/ServiceCollectionExtensions.cs` — deleted after `.WithBulkInsert()` moves to the shared project.
- **FILE-015**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertMappingBuilderTests.cs` — shared mapping contract tests.
- **FILE-016**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityFrameworkEntityBulkInserterTests.cs` — dispatcher, error, cancellation, and lifetime tests.
- **FILE-017**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/TestEntityBulkInsertProvider.cs` — test-only proof that providers can be added without shared edits.
- **FILE-018**: `tests/Infrastructure.UnitTests/EntityFramework/Repositories/Bulk/EntityBulkInsertArchitectureTests.cs` — provider dependency boundary guard.
- **FILE-019**: `tests/Infrastructure.IntegrationTests/EntityFramework/Repositories/SqlServerEntityBulkInsertProviderTests.cs` — renamed SQL Server parity, validation, and option translation tests.
- **FILE-020**: `docs/adr/0027-provider-strategy-for-entity-bulk-insert.md` — architecture decision.
- **FILE-021**: `docs/adr/README.md` — ADR index entry.
- **FILE-022**: `docs/features-domain-repositories.md` — consumer API, automatic strategy selection, semantics, and provider-extension documentation.
- **FILE-023**: `examples/DoFiesta/DoFiesta.Presentation.Web.Server/Modules/Core/CoreModule.cs` — provider-neutral `.WithBulkInsert()` example.
- **FILE-024**: `examples/DoFiesta/DoFiesta-README.md` — example documentation update.
- **FILE-025**: `src/Infrastructure.EntityFramework.SqlServer/ServiceCollectionExtensions.cs` — automatic SQL Server bulk-strategy registration from every `AddSqlServerDbContext<TContext>` overload.

## 6. Testing

- **TEST-001**: Shared mapping builds ordered provider values for flat entities and applies EF value converters.
- **TEST-002**: Shared mapping flattens same-table owned references, handles a null owned reference, and does not flatten separately mapped owned rows.
- **TEST-003**: Shared mapping omits computed and store-generated identity columns by default and includes identity values only when `KeepGeneratedIdentityValues` is enabled.
- **TEST-004**: Shared preparation assigns sequential primitive/typed GUID keys and concurrency versions only when their neutral options are enabled.
- **TEST-005**: Shared validation rejects non-owned navigations, populated owned collections, duplicate columns, missing model types, and mappings without writable columns.
- **TEST-006**: The orchestrator selects exactly one provider by exact EF provider name and reports missing or duplicate strategies deterministically.
- **TEST-007**: The orchestrator returns zero for empty input, converts provider exceptions to failed results, rethrows cancellation, and returns the provider's inserted count.
- **TEST-008**: DI registration gives the typed orchestrator the singleton, transient, or scoped lifetime requested by the repository builder, while each provider strategy is registered once as a stateless singleton.
- **TEST-009**: SQL Server writes flat and same-table-owned values, generated GUIDs, converted values, and requested identity values correctly.
- **TEST-010**: SQL Server reuses an ambient EF `SqlTransaction`; without one it adds `UseInternalTransaction`; both paths preserve caller-supplied non-transaction `SqlBulkCopyOptions`.
- **TEST-011**: A test-only provider can be registered once and selected for multiple entity/context pairs without changing the shared orchestrator or mapping builder.
- **TEST-012**: The architecture guard prevents provider-native client types from entering the shared bulk-insert namespace.
- **TEST-013**: Existing DoFiesta DataPorter bulk import continues to resolve `IEntityBulkInserter<TodoItem>` after registration changes.
- **TEST-014**: Raw EF-only `AddDbContext<TContext>(options => options.UseSqlServer(...))` plus `.WithBulkInsert()` returns the documented missing-provider failure, proving that the implementation neither scans assemblies nor silently falls back.

## 7. Risks & Assumptions

- **RISK-001**: Although the feature is unreleased, branch-local examples, documentation, or tests can still reference the old SQL Server types. Mitigate with the repository-wide zero-match checks in TASK-023.
- **RISK-002**: `KeepGeneratedIdentityValues` does not have identical semantics across SQL Server, PostgreSQL, and SQLite. Define it narrowly as "include mapped store-generated identity values supplied by the caller" and require each provider to document unsupported cases.
- **RISK-003**: Different providers require different identifier quoting, type handling, and native row encodings. Keep raw table/schema names and provider CLR values in the shared batch and perform quoting/encoding only in the provider.
- **RISK-004**: Repeated `AddSqlServerDbContext<TContext>` calls can attempt to register the same SQL Server strategy. Use `TryAddEnumerable` with one concrete implementation descriptor, test repeated registration, and keep deterministic duplicate-provider detection in the orchestrator for incorrectly composed third-party providers.
- **RISK-005**: Shared value preparation mutates entities when assigning GUID keys or concurrency versions before the native write. Preserve current behavior, document it, and test failure/cancellation cases so callers understand the mutation boundary.
- **RISK-006**: `SqlBulkCopyOptions` exposes `KeepIdentity` and `UseInternalTransaction` even though the abstraction owns those decisions. Reject both flags before opening the connection and test the exact error messages so contradictory configuration cannot reach native execution.
- **RISK-007**: Native writers have different performance characteristics and transaction APIs. Require provider-specific benchmarks and integration tests before PostgreSQL or SQLite support is declared production-ready.
- **RISK-008**: Full verification is blocked until SDK `10.0.301` is available on the implementation machine.
- **ASSUMPTION-001**: `DbContext.Database.ProviderName` remains the authoritative provider discriminator and uses the known exact values `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, and `Microsoft.EntityFrameworkCore.Sqlite`.
- **ASSUMPTION-002**: `IEntityBulkInserter<TEntity>` remains an infrastructure-only, opt-in high-performance port and is not expected to reproduce repository behaviors.
- **ASSUMPTION-003**: The first implementation phase targets one relational table per operation; populated owned collections and multi-table aggregate inserts remain outside the contract.
- **ASSUMPTION-004**: Provider implementations can consume the shared ordered column descriptors efficiently enough to create their native row stream without requiring a shared `DataTable` contract.
- **ASSUMPTION-005**: The bulk-insert feature has not been released or consumed from a published package, so this refactor may remove and rename its current public types without a compatibility layer.
- **ASSUMPTION-006**: Consumers seeking automatic provider registration use the DevKit `AddSqlServerDbContext<TContext>`, `AddPostgresDbContext<TContext>`, or `AddSqliteDbContext<TContext>` methods rather than raw EF-only `AddDbContext` registration.

## 8. Related Specifications / Further Reading

- [Repository feature documentation](../docs/features-domain-repositories.md)
- [Clean/Onion architecture decision](../docs/adr/0001-clean-onion-architecture.md)
- [Repository pattern decision](../docs/adr/0004-repository-decorator-behaviors.md)
- [Entity Framework Core decision](../docs/adr/0007-entity-framework-core-code-first-migrations.md)
- [Dependency injection lifetime decision](../docs/adr/0018-dependency-injection-service-lifetimes.md)
- [Current shared bulk-insert port](../src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInserter.cs)
- [Current SQL Server bulk inserter to refactor](../src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInserter.cs)
- [SQL Server bulk-copy API documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlbulkcopy)
- [Npgsql binary COPY documentation](https://www.npgsql.org/doc/copy.html)
- [SQLite transaction documentation](https://www.sqlite.org/lang_transaction.html)
