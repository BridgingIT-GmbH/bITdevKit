# ADR-0003: Modular Monolith Architecture

## Status

Accepted

## Context

When building enterprise applications, teams often face a choice between monolithic and microservices architectures. Each has significant tradeoffs:

**Traditional Monoliths**:

- Tight coupling between features
- Shared database leading to implicit dependencies
- Difficult to extract features into services later
- All-or-nothing deployment
- Teams stepping on each other's code

**Microservices**:

- Operational complexity (distributed systems, service discovery, tracing)
- Network latency and partial failures
- Distributed transactions and data consistency challenges
- Infrastructure overhead for small/medium teams
- Premature optimization if domain boundaries are unclear

The application needed an architecture that:

1. Provides isolation and clear boundaries between features
2. Enables independent development by different teams/developers
3. Maintains operational simplicity (single deployment unit)
4. Supports future extraction to microservices if needed
5. Scales development without scaling infrastructure complexity

## Decision

Adopt a **Modular Monolith** architecture where the application is organized into self-contained **modules** (vertical slices), each deployed as a single application.

### Module Characteristics

Each module (e.g., `CoreModule`) is a **vertical slice** containing:

- **Domain Layer**: Business logic specific to the module
- **Application Layer**: Use cases and workflows
- **Infrastructure Layer**: Persistence (own DbContext) and integrations
- **Presentation Layer**: API endpoints and module registration

### Module Structure

```
src/Modules/<ModuleName>/
├── <Module>.Domain/              # Business logic
├── <Module>.Application/         # Commands, Queries, Handlers
├── <Module>.Infrastructure/      # DbContext, Repositories
└── <Module>.Presentation/        # Endpoints, Module registration
```

### Module Isolation Rules

1. **Self-Contained**: Each module has its own DbContext and database schema
2. **No Direct References**: Modules cannot reference other modules' internal layers
3. **Communication**: Modules communicate via:
   - **Contracts**: Public interfaces (optional `.Contracts` projects)
   - **Integration Events**: Async communication through message bus
   - **Public APIs**: HTTP endpoints if needed
4. **Independent Evolution**: Modules can evolve independently

### Host Composition

- **Presentation.Web.Server**: Composition root that wires all modules together
- **Program.cs**: Registers modules via `AddModules().WithModule<CoreModuleModule>()`
- **Single Deployment**: All modules deployed as one ASP.NET Core application

## Rationale

1. **Simplicity**: Single deployment, single database, no distributed system complexity
2. **Clear Boundaries**: Modules enforce boundaries like microservices but without network overhead
3. **Team Scalability**: Teams can work on different modules with minimal conflicts
4. **Flexibility**: Can extract modules to microservices later if needed (each has independent data store)
5. **Performance**: In-process communication (no network latency, serialization overhead)
6. **Operational Simplicity**: One application to deploy, monitor, and debug
7. **Transaction Support**: Can use database transactions across modules if needed (same process)
8. **Cost Effective**: Single infrastructure footprint for small/medium teams

## Consequences

### Positive

- Clear module boundaries prevent coupling and tangled dependencies
- Each module can be developed, tested, and understood independently
- Single deployment simplifies CI/CD pipelines (no orchestration needed)
- In-process communication is fast and doesn't require distributed tracing initially
- Modules can be extracted to microservices when requirements justify it
- Architecture tests enforce module isolation boundaries
- Operational complexity remains low (no service mesh, API gateway, distributed tracing requirements)

### Negative

- Still a shared runtime (one module crashing can affect others)
- Cannot scale modules independently (though can use separate instances with routing)
- Module discipline required (no direct references between modules)
- More projects to manage than traditional monolith

### Neutral

- Each module has its own database schema (via separate DbContext)
- Modules registered explicitly in `Program.cs`
- Host application (`Presentation.Web.Server`) acts as composition root

## Alternatives Considered

- **Alternative 1: Traditional Monolith (Single Project)**
  - Rejected because boundaries erode over time without enforcement
  - No clear separation between features
  - Cannot extract features to services later

- **Alternative 2: Microservices from Day One**
  - Rejected because of operational complexity and infrastructure cost
  - Premature optimization before domain boundaries are well understood
  - Network latency and distributed transaction challenges

- **Alternative 3: Shared Database Monolith**
  - Rejected because shared database creates implicit coupling
  - Difficult to extract modules later (shared schema)
  - Schema migrations affect all features simultaneously

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Layering within each module
- [ADR-0005](0005-requester-notifier-mediator-pattern.md): In-process communication mechanism
- [ADR-0006](0006-outbox-pattern-domain-events.md): Event-driven communication between modules

## References

- [bITdevKit Modules Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-modules.md)
- [README - Modular Monolith Structure](../../README.md#modular-monolith-structure)
- [README - Module System](../../README.md#module-system-vertical-slices)
- [CoreModule README - Overview](../../src/Modules/CoreModule/CoreModule-README.md#overview)

## Notes

### Module Registration Pattern

Each module implements `WebModuleBase` and registers its services:

```csharp
public class CoreModuleModule : WebModuleBase("CoreModule")
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Register DbContext
        services.AddSqlServerDbContext<CoreModuleDbContext>(...);

        // Register repositories
        services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
            .WithBehavior<RepositoryTracingBehavior<Customer>>()
            .WithBehavior<RepositoryLoggingBehavior<Customer>>();

        // Register endpoints
        services.AddEndpoints<CustomerEndpoints>();

        return services;
    }
}
```

### Host Registration (Program.cs)

```csharp
builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModuleModule>()
    .WithModuleContextAccessors()
    .WithRequestModuleContextAccessors();
```

### Module Communication Patterns

**Synchronous** (within same module):

```csharp
// Command → Handler → Repository
await requester.SendAsync(new CustomerCreateCommand(model));
```

**Asynchronous** (cross-module):

```csharp
// Domain Event → Outbox → Integration Event → Other Module Handler
customer.DomainEvents.Register(new CustomerCreatedDomainEvent(customer));
```

### Future Microservices Extraction

If a module needs to be extracted to a microservice:

1. Module already has isolated database schema (own DbContext)
2. Module already has independent layers (Domain, Application, Infrastructure, Presentation)
3. Change in-process commands to HTTP/gRPC calls
4. Change domain events to message bus (RabbitMQ, Azure Service Bus)
5. Deploy module independently

### Current Modules

- **CoreModule**: Customer management domain (demonstrated in this example)
- *Future modules*: Can add InventoryModule, OrderModule, etc. following the same pattern

### Implementation Files

- **Module definition**: `src/Modules/CoreModule/CoreModule.Presentation/CoreModuleModule.cs`
- **Host registration**: `src/Presentation.Web.Server/Program.cs`
- **Module structure**: `src/Modules/CoreModule/` (Domain, Application, Infrastructure, Presentation)
