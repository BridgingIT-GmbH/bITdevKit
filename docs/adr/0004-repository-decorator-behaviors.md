# ADR-0004: Repository Pattern with Decorator Behaviors

## Status

Accepted

## Context

Data access in domain-driven applications requires careful design to maintain clean architecture boundaries while supporting common cross-cutting concerns:

**Challenges**:

- Domain layer must remain persistence-ignorant
- Cross-cutting concerns (logging, tracing, auditing, events) create code duplication
- Direct DbContext access in handlers violates abstraction boundaries
- Adding features like audit trails requires touching every persistence point
- Transaction and event handling needs to be consistent across all operations

**Requirements**:

1. Abstract data access behind repositories (domain doesn't know about EF Core)
2. Support cross-cutting concerns without code duplication
3. Maintain single responsibility principle (repository focuses on data access)
4. Enable consistent audit trails, logging, and event handling
5. Allow adding new concerns without modifying existing repository code

## Decision

Adopt the **Repository Pattern** with **Decorator Behaviors** using bITdevKit's generic repository and behavior chain infrastructure.

### Repository Abstraction

```csharp
public interface IGenericRepository<TEntity>
{
    Task<Result<TEntity>> InsertResultAsync(TEntity entity, CancellationToken cancellationToken);
    Task<Result<TEntity>> UpdateResultAsync(TEntity entity, CancellationToken cancellationToken);
    Task<Result<TEntity>> DeleteResultAsync(TEntity entity, CancellationToken cancellationToken);
    Task<Result<TEntity>> FindOneResultAsync(object id, CancellationToken cancellationToken);
    Task<Result<IEnumerable<TEntity>>> FindAllResultAsync(...);
}
```

### Behavior Chain Pattern

Behaviors wrap the repository using the **Decorator pattern**:

```
Handler
  → RepositoryTracingBehavior
    → RepositoryLoggingBehavior
      → RepositoryAuditStateBehavior
        → RepositoryOutboxDomainEventBehavior
          → EntityFrameworkGenericRepository
            → Database
```

### Configured Behaviors

1. **RepositoryTracingBehavior**: OpenTelemetry distributed tracing spans
2. **RepositoryLoggingBehavior**: Structured logging with duration measurement
3. **RepositoryAuditStateBehavior**: Automatic `CreatedBy`, `UpdatedBy`, `CreatedDate`, `UpdatedDate`
4. **RepositoryOutboxDomainEventBehavior**: Outbox pattern for domain events

### Registration Pattern

```csharp
services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
    .WithBehavior<RepositoryTracingBehavior<Customer>>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()
    .WithBehavior<RepositoryAuditStateBehavior<Customer>>()
    .WithBehavior<RepositoryOutboxDomainEventBehavior<Customer, CoreModuleDbContext>>();
```

## Rationale

1. **Abstraction**: Application handlers depend on `IGenericRepository<T>`, not concrete DbContext
2. **Decorator Pattern**: Behaviors add concerns without modifying core repository logic
3. **Composability**: Chain behaviors in any order, add/remove as needed
4. **Single Responsibility**: Each behavior has one concern (tracing, logging, audit, events)
5. **Consistency**: All persistence operations get same cross-cutting concerns automatically
6. **Open/Closed Principle**: Add new behaviors without modifying existing code
7. **Testability**: Can test handlers with in-memory/mock repositories
8. **Performance**: Behaviors only execute when needed (e.g., tracing only if enabled)

## Consequences

### Positive

- Domain and Application layers have no dependencies on EF Core or SQL Server
- Cross-cutting concerns applied consistently without code duplication
- Audit trails automatic for all entities (CreatedBy, UpdatedBy, timestamps)
- OpenTelemetry tracing integrated for all repository operations
- Structured logging with operation context and duration measurement
- Domain events reliably persisted via outbox pattern
- Easy to add new behaviors (e.g., caching, validation, notifications)
- Repository can be swapped for in-memory implementation in tests

### Negative

- Indirection through repository abstraction (one extra layer)
- Behavior chain adds slight overhead (typically negligible)
- More complex DI registration compared to direct DbContext usage
- Learning curve for developers unfamiliar with Decorator pattern

### Neutral

- Behavior execution order matters (configured from outer to inner)
- Repository abstractions from bITdevKit, not custom implementations
- Each entity type gets its own repository with configured behaviors

## Alternatives Considered

- **Alternative 1: Direct DbContext Access in Handlers**
  - Rejected because it couples Application layer to infrastructure (EF Core)
  - Cross-cutting concerns (audit, events) scattered across all handlers
  - Violates Clean Architecture dependency rules

- **Alternative 2: Custom Repository Per Aggregate**
  - Rejected due to code duplication for common CRUD operations
  - Each repository would need to reimplement cross-cutting concerns
  - More difficult to maintain consistency

- **Alternative 3: Aspect-Oriented Programming (AOP) with Interceptors**
  - Rejected because it's less explicit and harder to debug
  - Behavior configuration is less discoverable (attributes vs fluent API)
  - AOP frameworks add complexity and may not work with all DI containers

- **Alternative 4: Mediator Behaviors for Persistence**
  - Rejected because persistence concerns belong closer to data access layer
  - Repository behaviors are more granular (per-entity) than mediator behaviors (per-request)

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Repository keeps infrastructure isolated
- [ADR-0006](0006-outbox-pattern-domain-events.md): Outbox behavior enables reliable event delivery
- [ADR-0007](0007-ef-core-code-first-migrations.md): Repository abstracts EF Core details

## References

- [bITdevKit Repositories Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-domain-repositories.md)
- [README - Repository with Behaviors Pattern](../../README.md#repository-with-behaviors-pattern-decorator)
- [CoreModule README - Repository Behaviors](../../src/Modules/CoreModule/CoreModule-README.md#repository-behaviors-configuration)

## Notes

### Behavior Chain Execution Flow

**Insert Operation**:

```
1. Handler: repository.InsertResultAsync(customer)
2. TracingBehavior: Start span "Repository.Insert.Customer"
3. LoggingBehavior: Log "Inserting Customer entity"
4. AuditStateBehavior: Set customer.CreatedBy, customer.CreatedDate
5. OutboxBehavior: Extract CustomerCreatedDomainEvent → OutboxDomainEvent table
6. EFRepository: dbContext.SaveChangesAsync() [atomic transaction]
7. OutboxBehavior: Clear customer.DomainEvents
8. AuditStateBehavior: (no post-action)
9. LoggingBehavior: Log "Customer inserted (Duration: 45ms)"
10. TracingBehavior: End span with status
11. Handler: Receives Result<Customer>
```

### Behavior Configuration Example

```csharp
// CoreModuleModule.cs
services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
    .WithBehavior<RepositoryTracingBehavior<Customer>>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()
    .WithBehavior<RepositoryAuditStateBehavior<Customer>>()
    .WithBehavior<RepositoryOutboxDomainEventBehavior<Customer, CoreModuleDbContext>>();

services.AddScoped(_ => new RepositoryAuditStateBehaviorOptions
{
    SoftDeleteEnabled = false
});
```

### Usage in Application Handlers

```csharp
public class CustomerCreateCommandHandler(
    IGenericRepository<Customer> repository, // Abstract interface
    ...)
{
    protected override async Task<Result<CustomerModel>> HandleAsync(...)
    {
        return await Result<CustomerModel>
            .Bind(() => new Context(request.Model))
            .Bind(CreateEntity)
            .BindResultAsync(async (ctx, ct) =>
                await repository.InsertResultAsync(ctx.Entity, ct), // All behaviors execute
                CaptureEntity,
                cancellationToken)
            .Map(ToModel);
    }
}
```

### Testing with Repositories

**Unit Tests** (mock repository):

```csharp
var mockRepository = Substitute.For<IGenericRepository<Customer>>();
mockRepository.InsertResultAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
    .Returns(Result<Customer>.Success(customer));
```

**Integration Tests** (real repository with behaviors):

```csharp
var repository = serviceProvider.GetRequiredService<IGenericRepository<Customer>>();
var result = await repository.InsertResultAsync(customer, CancellationToken.None);
// Behaviors execute: tracing, logging, audit, outbox
```

### Behavior Ordering Rationale

The order is intentional:

1. **Tracing (outermost)**: Captures complete operation including all behaviors
2. **Logging**: Captures audit fields and event extraction
3. **Audit**: Sets metadata before event extraction
4. **Outbox (innermost)**: Extracts events after audit fields are set, within same transaction

### Available Behaviors

- `RepositoryTracingBehavior<T>`: OpenTelemetry distributed tracing
- `RepositoryLoggingBehavior<T>`: Structured logging with Serilog
- `RepositoryAuditStateBehavior<T>`: Audit fields (Created/Updated timestamps and user)
- `RepositoryOutboxDomainEventBehavior<T, TContext>`: Outbox pattern for reliable events
- `RepositoryDomainEventPublisherBehavior<T>`: Direct event publishing (alternative to outbox)

### Implementation Files

- **Repository abstraction**: bITdevKit `IGenericRepository<T>`
- **Behavior registration**: `src/Modules/CoreModule/CoreModule.Presentation/CoreModuleModule.cs`
- **Handler usage**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommandHandler.cs`
- **Behavior implementations**: bITdevKit infrastructure packages
