# ADR-0011: Application Logic in Commands/Queries

## Status

Accepted

## Context

In layered architectures following DDD principles, there is often confusion about where to place different types of logic. Without clear boundaries, codebases suffer from:

- **Bloated Domain Entities**: Entities with infrastructure concerns (calling repositories, external services)
- **Anemic Domain Models**: All logic in services, entities become simple data holders
- **Inconsistent Placement**: Similar logic scattered between domain and application layers
- **Tight Coupling**: Domain entities dependent on infrastructure
- **Testing Complexity**: Cannot test domain logic independently of infrastructure

The application needs clear separation between:

1. **Workflow orchestration and coordination** (application concern)
2. **Business rules and invariants** (domain concern)

## Decision

**Application logic is handled in Commands/Queries and their Handlers** in the Application layer.

### Application Logic Includes:

**Use Case Orchestration**:

- Coordinating multiple domain operations in a workflow
- Sequencing operations (generate number → create entity → persist → map)
- Transaction boundary definition

**Cross-Aggregate Coordination**:

- Working with multiple repositories
- Coordinating changes across different aggregates
- Ensuring consistency across aggregate boundaries

**External Service Integration**:

- Calling sequence number generators
- Sending notifications/emails
- Integrating with external APIs

**Infrastructure Interaction**:

- Repository operations (find, insert, update, delete)
- Unit of work management
- Query specification composition

**Data Transformation**:

- Mapping between domain entities and DTOs
- Transforming external service responses
- Assembling response models

**Pipeline Composition**:

- Using Result pattern to chain operations
- Error handling and recovery
- Logging and telemetry

## Rationale

1. **Single Responsibility**: Domain focuses on invariants; Application focuses on workflows
2. **Testability**: Handlers can be unit tested with mocked dependencies
3. **Reusability**: Domain logic reused across different use cases
4. **Framework Independence**: Domain remains pure; Application uses bITdevKit features
5. **Clear Boundaries**: Developers know where to add new logic
6. **Result Pattern Composition**: Handlers use Result railway for clean error flow
7. **Separation of Concerns**: Domain doesn't know about persistence, external services, or DTOs

## Consequences

### Positive

- Clear separation between domain invariants and workflow orchestration
- Domain entities remain focused on business rules
- Handlers provide explicit documentation of use case workflows
- Easy to add cross-cutting concerns via pipeline behaviors
- Testable orchestration logic independent of domain
- Consistent pattern across all features in codebase
- Domain can be reused in different application contexts

### Negative

- Some duplication between handlers (mitigated by base classes and Result helpers)
- Developers must understand where to place logic (learning curve)
- More classes to maintain (command + handler + validator per use case)

### Neutral

- Handlers follow functional composition style using Result pattern
- Each command/query represents a single use case
- Context pattern used to accumulate state across pipeline steps

## Alternatives Considered

- **Alternative 1: Domain Services for All Logic**
  - Rejected because it leads to anemic domain models with unclear responsibilities
  - Domain services would need infrastructure dependencies (repositories, external services)

- **Alternative 2: Rich Domain Entities with All Logic**
  - Rejected because domain would need to know about infrastructure concerns
  - Entities would become bloated with orchestration logic
  - Violates single responsibility principle

- **Alternative 3: Application Services (Traditional Service Layer)**
  - Rejected in favor of CQRS-style commands/queries for better separation
  - Commands/Queries provide explicit contracts and better discoverability

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Clean Architecture defines layer boundaries
- [ADR-0002](0002-result-pattern-error-handling.md): Result Pattern used for composing application logic
- [ADR-0005](0005-requester-notifier-mediator-pattern.md): Requester executes commands/queries
- [ADR-0012](0012-domain-logic-in-domain-layer.md): Complementary decision defining domain responsibilities

## References

- [bITdevKit Application Commands & Queries](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-application-commands-queries.md)
- [README - Request Processing Flow](../../README.md#request-processing-flow)
- [CoreModule README - Handler Implementation](../../src/Modules/CoreModule/CoreModule-README.md#handler-implementation-example)
- [Martin Fowler - Anemic Domain Model](https://martinfowler.com/bliki/AnemicDomainModel.html)

## Notes

### Handler Implementation Pattern

```csharp
public class CustomerCreateCommandHandler(
    ILogger<CustomerCreateCommandHandler> logger,
    IMapper mapper,
    IGenericRepository<Customer> repository,
    ISequenceNumberGenerator numberGenerator,
    TimeProvider timeProvider)
    : RequestHandlerBase<CustomerCreateCommand, CustomerModel>(logger)
{
    // APPLICATION LOGIC: Orchestration
    protected override async Task<Result<CustomerModel>> HandleAsync(
        CustomerCreateCommand request,
        SendOptions options,
        CancellationToken cancellationToken) =>
            await Result<CustomerModel>
                // 1. Create context (application concern)
                .Bind<CustomerCreateContext>(() => new(request.Model))

                // 2. Inline validation (application concern)
                .Ensure((ctx) => ctx.Model.FirstName != ctx.Model.LastName,
                    new ValidationError("Firstname cannot be same as lastname"))

                // 3. Business rules (delegates to domain)
                .UnlessAsync(async (ctx, ct) => await Rule
                    .Add(RuleSet.IsNotEmpty(ctx.Model.FirstName))
                    .Add(new EmailShouldBeUniqueRule(ctx.Model.Email, repository))
                    .CheckAsync(ct), cancellationToken: cancellationToken)

                // 4. Generate sequence number (application concern - external service)
                .BindResultAsync(this.GenerateSequenceAsync, this.CaptureNumber, cancellationToken)

                // 5. Create aggregate (domain factory method)
                .Bind(this.CreateEntity)

                // 6. Persist (application concern - infrastructure)
                .BindResultAsync(this.PersistEntityAsync, this.CapturePersistedEntity, cancellationToken)
                .Log(logger, "Customer {Id} created", r => [r.Value.Entity.Id])

                // 7. Map to DTO (application concern)
                .Map(this.ToModel);

    // Helper methods demonstrate application concerns
    private async Task<Result<CustomerNumber>> GenerateSequenceAsync(CustomerCreateContext ctx, CancellationToken ct) =>
        await numberGenerator.NextAsync(timeProvider.GetUtcNow().Year, ct);

    private Result<CustomerCreateContext> CreateEntity(CustomerCreateContext ctx)
    {
        // Call domain factory method
        var createResult = Customer.Create(ctx.Model.FirstName, ctx.Model.LastName, ctx.Model.Email, ctx.Number);
        if (createResult.IsFailure)
            return createResult.Unwrap();

        ctx.Entity = createResult.Value;
        return ctx;
    }

    private async Task<Result<Customer>> PersistEntityAsync(CustomerCreateContext ctx, CancellationToken ct) =>
        await repository.InsertResultAsync(ctx.Entity, ct).AnyContext();

    private CustomerModel ToModel(CustomerCreateContext ctx) =>
        mapper.Map<Customer, CustomerModel>(ctx.Entity);
}
```

### Context Pattern for State Accumulation

The **Context pattern** accumulates state across pipeline steps:

```csharp
private class CustomerCreateContext(CustomerModel model)
{
    public CustomerModel Model { get; init; } = model;      // Input
    public CustomerNumber Number { get; set; }              // From sequence generator
    public Customer Entity { get; set; }                    // From domain factory
}
```

This avoids nested closures and makes state flow explicit.

### Query Handler Example (Simpler)

Queries focus on reading data and mapping:

```csharp
public class CustomerFindAllQueryHandler(
    IMapper mapper,
    IGenericRepository<Customer> repository)
    : RequestHandlerBase<CustomerFindAllQuery, IEnumerable<CustomerModel>>
{
    protected override async Task<Result<IEnumerable<CustomerModel>>> HandleAsync(
        CustomerFindAllQuery request,
        SendOptions options,
        CancellationToken cancellationToken) =>
            await repository
                .FindAllResultAsync(request.Filter, cancellationToken: cancellationToken)
                .Map(mapper.Map<Customer, CustomerModel>);
}
```

### Application vs Domain Responsibility Matrix

| Concern | Layer | Example |
|---------|-------|---------|
| Sequence number generation | Application | `numberGenerator.NextAsync()` |
| Email format validation | Domain | `EmailAddress.Create()` |
| Aggregate creation | Domain | `Customer.Create()` |
| Persistence | Application | `repository.InsertResultAsync()` |
| DTO mapping | Application | `mapper.Map<Customer, CustomerModel>()` |
| Business rule checking | Domain (definition) | `EmailShouldBeUniqueRule` |
| Rule orchestration | Application (execution) | `Rule.Add(...).CheckAsync()` |
| Domain event registration | Domain | `customer.DomainEvents.Register(...)` |
| Cross-aggregate coordination | Application | Multiple repository calls in handler |
| Transaction boundaries | Application | Handler defines unit of work |

### Update Handler Example

Update handlers demonstrate coordination:

```csharp
protected override async Task<Result<CustomerModel>> HandleAsync(...)
{
    return await Result<CustomerModel>
        // 1. Find existing (application concern)
        .BindResultAsync(async ct => await repository.FindOneResultAsync(request.Model.Id, ct))

        // 2. Check concurrency (application concern)
        .Ensure(entity => entity.ConcurrencyVersion.ToString() == request.Model.ConcurrencyToken,
            new ConcurrencyError("Entity was modified by another user"))

        // 3. Change name (domain method)
        .Bind(entity => entity.ChangeName(request.Model.FirstName, request.Model.LastName))

        // 4. Persist (application concern)
        .BindResultAsync(async (entity, ct) => await repository.UpdateResultAsync(entity, ct))

        // 5. Map (application concern)
        .Map(mapper.Map<Customer, CustomerModel>);
}
```

### Handler Checklist

When creating a new handler:

1. V Inherit from `RequestHandlerBase<TRequest, TResponse>`
2. V Inject dependencies (logger, mapper, repository, domain services)
3. V Use context pattern if multiple steps accumulate state
4. V Validate input with `Ensure` and `Unless` (fail fast)
5. V Call domain methods (factories, behavior methods)
6. V Persist via repository Result methods
7. V Map aggregate to DTO before returning
8. V Write unit tests for happy path and error scenarios

### Implementation Files

- **Command**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommand.cs`
- **Handler**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommandHandler.cs`
- **Query**: `src/Modules/CoreModule/CoreModule.Application/Queries/CustomerFindAllQuery.cs`
- **Tests**: `tests/Modules/CoreModule/CoreModule.UnitTests/Application/Commands/CustomerCreateCommandHandlerTests.cs`
