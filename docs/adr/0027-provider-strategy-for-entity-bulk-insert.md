# ADR-0027: Use Provider Strategies for Entity Bulk Insert

## Status

Accepted

## Date

2026-07-10

## Context

`IEntityBulkInserter<TEntity>` provides an explicit high-performance write path for imports and similar workloads. The current implementation is a SQL Server class that combines Entity Framework metadata preparation with SQL Server-specific `SqlBulkCopy` execution. As a result, a PostgreSQL or SQLite implementation would either duplicate the EF metadata logic or introduce SQL Server connection, transaction, table-quoting, and option types into the shared `Infrastructure.EntityFramework` project.

The bulk-insert API must continue to be opt-in and infrastructure-facing. It intentionally does not replace repository insert operations because it bypasses repository decorators, change tracking, domain events, outbox persistence, and `SaveChanges` interceptors. The shared API must prepare the same entity semantics for every relational provider, while each provider must retain control of its own native write API, transaction handling, identifier quoting, and type encoding.

Consumers should register a bulk inserter with `.WithBulkInsert()` without selecting a provider manually. DevKit database setup methods already identify the configured provider through methods such as `AddSqlServerDbContext<TContext>`. A raw EF Core `UseSqlServer` call alone cannot safely register a DevKit-native write strategy without reflection-based assembly scanning.

## Decision

Use a provider strategy architecture with these boundaries:

- `Infrastructure.EntityFramework` owns `IEntityBulkInserter<TEntity>`, the provider-neutral `IEntityBulkInsertProvider` strategy contract, options, prepared batch/column models, EF metadata mapping, and provider dispatch based on `DbContext.Database.ProviderName`.
- Every native provider strategy is stateless and receives the active `DbContext` and prepared batch for one insert call.
- Every DevKit `Add*DbContext<TContext>` method registers its provider strategy automatically with `TryAddEnumerable`. `AddSqlServerDbContext<TContext>` registers the SQL Server strategy; future PostgreSQL and SQLite setup methods will register their strategies when implemented.
- `.WithBulkInsert()` remains the single shared repository-facing registration method. It registers the typed shared orchestrator and does not reference any native provider assembly.
- A missing or duplicate strategy is an explicit configuration failure. The implementation must not fall back to EF `AddRange` and `SaveChanges`.

## Rationale

The strategy keeps reusable EF metadata work in one provider-neutral implementation and confines native dependencies to their provider packages. Automatic registration preserves the existing consumer experience while allowing applications that reference multiple provider packages to use one unambiguous `.WithBulkInsert()` method.

## Consequences

### Positive

- Shared mapping behavior for owned references, generated values, value converters, and validation is implemented once.
- Adding a relational provider does not require changing a central provider switch or referencing its ADO.NET package from the shared project.
- Applications continue to register a bulk inserter with `.WithBulkInsert()` and receive the relevant writer from the configured EF provider.
- Missing provider support is visible immediately instead of producing a slower operation with different behavior.

### Negative

- Each provider package must maintain its automatic strategy-registration hook and native integration tests.
- A consumer that configures a context only through raw EF Core registration does not receive a native strategy automatically.
- Provider-specific options need a derived options type and must be interpreted by the matching native strategy.

### Neutral

- The shared provider contract exposes EF metadata and prepared provider values because the extension point belongs to infrastructure, not to Domain or Application.
- Native provider strategies are registered even when a particular application never calls `.WithBulkInsert()`; they are stateless singleton registrations.

## Alternatives Considered

- **SQL Server-oriented base class**
  - Rejected because `DataTable`, `SqlBulkCopy`, SQL Server connection/transaction types, and bracket quoting are not reusable PostgreSQL or SQLite abstractions.

- **Central switch in the shared bulk inserter**
  - Rejected because every new provider would change and republish the shared package and would require the shared package to reference each native provider dependency.

- **Provider-specific `.With*BulkInsert()` methods**
  - Rejected because the consumer would need to repeat a provider choice already known by EF configuration, and multiple provider packages would create an inconsistent public API.

- **Identical `.WithBulkInsert()` extensions in each provider package**
  - Rejected because applications referencing multiple provider packages would face ambiguous extension-method resolution.

- **EF `AddRange` fallback for an unsupported provider**
  - Rejected because its tracking, interception, event, transaction, and performance semantics differ from the explicit native bulk path.

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Clean/Onion Architecture with Strict Layer Boundaries
- [ADR-0004](0004-repository-decorator-behaviors.md): Repository Pattern with Decorator Behaviors
- [ADR-0007](0007-entity-framework-core-code-first-migrations.md): Entity Framework Core with Code-First Migrations
- [ADR-0018](0018-dependency-injection-service-lifetimes.md): Dependency Injection & Service Lifetime Management

## References

- [Repository feature documentation](../features-domain-repositories.md)
- [Current bulk inserter contract](../../src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInserter.cs)
- [SQL Server bulk insert provider](../../src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs)

## Notes

### Implementation Files

- `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInsertProvider.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertBatch.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertColumn.cs`
- `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs`
