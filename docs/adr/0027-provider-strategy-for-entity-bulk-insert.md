# ADR-0027: Use Provider Strategies for Entity Bulk Insert

## Status

Accepted (registration and decorator decisions superseded by [ADR-0029](0029-independent-entity-bulk-inserter-decorator-behaviors.md))

> **Partially superseded by [ADR-0029](0029-independent-entity-bulk-inserter-decorator-behaviors.md).** The provider strategy, automatic provider registration, and provider isolation remain accepted. ADR-0029 defines independent `AddEntityFrameworkBulkInserter` registration and explicit decorators. ADR-0028 is retained as historical context only.

## Date

2026-07-10

## Context

`IEntityBulkInserter<TEntity>` provides an explicit high-performance write path for imports and similar workloads. The original preview implementation was a SQL Server class that combined Entity Framework metadata preparation with SQL Server-specific `SqlBulkCopy` execution. As a result, a PostgreSQL or SQLite implementation would either duplicate the EF metadata logic or introduce SQL Server connection, transaction, table-quoting, and option types into the shared `Infrastructure.EntityFramework` project.

The bulk-insert API must continue to be opt-in and must not replace repository insert operations. The shared implementation must prepare the same entity semantics for every relational provider, while each provider retains control of its native write API, identifier quoting, and type encoding. Public contract ownership and behavior compatibility are superseded by ADR-0028.

DevKit database setup methods identify the configured provider through methods such as `AddSqlServerDbContext<TContext>`. Consumers register the terminal capability separately with `AddEntityFrameworkBulkInserter<TEntity, TContext>()`; a raw EF Core `UseSqlServer` call alone cannot safely register a DevKit-native write strategy without reflection-based assembly scanning.

## Decision

Use a provider strategy architecture with these boundaries:

- `Infrastructure.EntityFramework` owns the provider-neutral `IEntityBulkInsertProvider` strategy contract, options, prepared batch/column models, EF metadata mapping, and provider dispatch based on `DbContext.Database.ProviderName`. ADR-0028 assigns the application-facing `IEntityBulkInserter<TEntity>` contract to Domain.
- Every native provider strategy is stateless and receives the active `DbContext` and prepared batch for one insert call.
- Every DevKit `Add*DbContext<TContext>` method registers its provider strategy automatically with `TryAddEnumerable`. `AddSqlServerDbContext<TContext>` registers the SQL Server strategy; future PostgreSQL and SQLite setup methods will register their strategies when implemented.
- The typed shared terminal is registered by `AddEntityFrameworkBulkInserter<TEntity, TContext>()` and does not reference any native provider assembly.
- A missing or duplicate strategy is an explicit configuration failure. The implementation must not fall back to EF `AddRange` and `SaveChanges`.

## Rationale

The strategy keeps reusable EF metadata work in one provider-neutral implementation and confines native dependencies to their provider packages. Automatic registration preserves the existing consumer experience while allowing applications that reference multiple provider packages to use one unambiguous `.WithBulkInsert()` method.

## Consequences

### Positive

- Shared mapping behavior for owned references, generated values, value converters, and validation is implemented once.
- Adding a relational provider does not require changing a central provider switch or referencing its ADO.NET package from the shared project.
- Applications register a bulk inserter independently and receive the relevant writer from the configured EF provider.
- Missing provider support is visible immediately instead of producing a slower operation with different behavior.

### Negative

- Each provider package must maintain its automatic strategy-registration hook and native integration tests.
- A consumer that configures a context only through raw EF Core registration does not receive a native strategy automatically.
- Provider-specific options need a derived options type and must be interpreted by the matching native strategy.

### Neutral

- The shared provider contract exposes EF metadata and prepared provider values because the extension point belongs to infrastructure, not to Domain or Application.
- Native provider strategies are registered even when a particular application never registers a typed bulk inserter; they are stateless singleton registrations.

## Alternatives Considered

- **SQL Server-oriented base class**
  - Rejected because `DataTable`, `SqlBulkCopy`, SQL Server connection/transaction types, and bracket quoting are not reusable PostgreSQL or SQLite abstractions.

- **Central switch in the shared bulk inserter**
  - Rejected because every new provider would change and republish the shared package and would require the shared package to reference each native provider dependency.

- **Provider-specific `.With*BulkInsert()` methods**
  - Rejected because the consumer would need to repeat a provider choice already known by EF configuration, and multiple provider packages would create an inconsistent public API.

- **Identical `AddEntityFrameworkBulkInserter()` extensions in each provider package**
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
- [Current Domain bulk inserter contract](../../src/Domain/Repositories/IEntityBulkInserter.cs)
- [SQL Server bulk insert provider](../../src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs)
- [ADR-0029](0029-independent-entity-bulk-inserter-decorator-behaviors.md): Independent registration and explicit decorators

## Notes

### Implementation Files

- `src/Infrastructure.EntityFramework/Repositories/Bulk/IEntityBulkInsertProvider.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertOptions.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertBatch.cs`
- `src/Infrastructure.EntityFramework/Repositories/Bulk/EntityBulkInsertColumn.cs`
- `src/Infrastructure.EntityFramework.SqlServer/Repositories/Bulk/SqlServerEntityBulkInsertProvider.cs`
