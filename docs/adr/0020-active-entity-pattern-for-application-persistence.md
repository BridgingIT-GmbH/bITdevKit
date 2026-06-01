# ADR-0020: ActiveEntity Pattern for Application Persistence

## Status

Accepted

## Context

bITdevKit applications need a persistence style for command/query handlers, jobs, startup tasks, and examples. ADR-0004 documents repository abstractions with decorator behaviors. Several newer examples, including WeatherFiesta, use `ActiveEntity<TEntity, TId>` instead, where domain entities expose persistence operations such as `FindAllAsync`, `InsertAsync`, `UpdateAsync`, `UpsertAsync`, `DeleteAsync`, and `ExistsAsync`.

This choice affects architecture because Application handlers no longer inject `IGenericRepository<TEntity>`. Instead, entities resolve configured persistence providers through the ActiveEntity runtime registrations made in the outer composition root.

The team values:

- Small command/query handlers with less dependency injection noise.
- Consistent persistence behavior configured centrally per entity.
- Reuse of bITdevKit ActiveEntity behaviors for logging, audit state, soft delete, and domain event publishing.
- A pragmatic example style that remains easy to read.

Risks must be explicit because ActiveEntity can look like a classic Active Record pattern and may be mistaken for permission to put infrastructure concerns or business orchestration into entities.

## Decision

Prefer the bITdevKit **ActiveEntity pattern** for application persistence in modules that opt into it, including WeatherFiesta.

Application code may call ActiveEntity persistence methods directly:

```csharp
var citiesResult = await City.FindAllAsync(specification, null, cancellationToken);

var city = City.Create(name, country, countryCode, timeZone, location, externalId, elevation);
var insertResult = await city.InsertAsync(cancellationToken);
```

The composition root must register each ActiveEntity and its persistence provider:

```csharp
services.AddActiveEntity(cfg =>
{
    cfg.For<City, CityId>()
        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
        .AddLoggingBehavior()
        .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true)
        .AddDomainEventPublishingBehavior();
});
```

Domain entities still own business state and invariants through factories and change methods. ActiveEntity persistence does not allow:

- Direct `DbContext` use in Application.
- Infrastructure references in Domain.
- Business decisions in Presentation.
- Public setters for aggregate invariants when change methods are appropriate.

Repository abstractions remain valid for modules that need explicit persistence dependencies, custom repositories, or stronger test seams. ADR-0004 is not superseded; this ADR defines the preferred pattern for ActiveEntity-enabled modules.

## Alternatives Considered

### Inject `IGenericRepository<TEntity>` everywhere

Benefits:

- Explicit dependencies in handler signatures.
- Easier to substitute repositories in unit tests.
- Aligns with classic Clean Architecture repository guidance.

Drawbacks:

- Handler constructors/signatures grow quickly when use cases touch multiple entities.
- Repeated persistence plumbing obscures use-case flow.
- Entity-specific behaviors still need registration and decoration elsewhere.

### Direct `DbContext` in Application

Benefits:

- Maximum EF Core query flexibility.
- Familiar to many .NET developers.

Drawbacks:

- Violates Clean/Onion dependency direction.
- Couples Application to EF Core and Infrastructure.
- Makes persistence technology changes harder.

This remains rejected.

### Custom repository per aggregate

Benefits:

- Strong aggregate-specific API.
- Useful for complex persistence workflows.

Drawbacks:

- More boilerplate.
- Can create many narrow repository methods.
- Less consistent across examples.

Use only when an aggregate needs specialized persistence behavior that ActiveEntity plus specifications cannot express clearly.

## Consequences

Positive:

- Command/query handlers stay concise.
- ActiveEntity behavior registration centralizes persistence concerns.
- Application remains free from `DbContext` and EF Core references.
- Specification pattern still encapsulates query predicates and avoids ad-hoc query duplication.

Negative:

- Persistence dependency is less visible than injected repositories.
- Unit tests may need ActiveEntity provider setup or integration-style testing.
- Overuse can blur domain/persistence boundaries if entities gain orchestration logic.
- Static persistence methods require architectural discipline and review.

Guardrails:

- Keep business invariants in domain methods returning `Result<T>`.
- Keep use-case orchestration in commands/queries/jobs.
- Keep persistence provider registration in Presentation/Infrastructure composition.
- Prefer specifications for non-trivial queries.
- Batch queries where possible to avoid N+1 behavior.
- Do not reference Infrastructure from Domain or Application.

## Related ADRs

- ADR-0001: Clean/Onion Architecture
- ADR-0002: Result Pattern for Error Handling
- ADR-0004: Repository Pattern with Decorator Behaviors
- ADR-0011: Application Logic in Commands & Queries
- ADR-0012: Domain Logic in Domain Layer
- ADR-0019: Specification Pattern for Repository Queries
