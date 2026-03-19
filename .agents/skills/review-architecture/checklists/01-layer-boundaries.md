# Checklist: Layer Boundaries

This checklist helps verify that layer dependencies flow **inward only** and that circular references are avoided in the Clean/Onion Architecture.

## Layer Dependency Rules (🔴 CRITICAL)

### Domain Layer (Innermost)

**Rule**: Domain layer has **ZERO** external dependencies except bITdevKit domain abstractions.

**ADR-0001 (Clean/Onion Architecture with Strict Layer Boundaries)**: The domain layer represents pure business logic and must remain independent of all infrastructure concerns. This ensures the domain can be tested in isolation, ported to different frameworks, and evolved without being constrained by technical implementation details. Any external dependency creates coupling that makes the system fragile and difficult to maintain.

#### Checklist

- [ ] No `using` statements referencing Application, Infrastructure, or Presentation namespaces
- [ ] No `using` statements referencing Entity Framework (`Microsoft.EntityFrameworkCore`)
- [ ] No `using` statements referencing ASP.NET Core (`Microsoft.AspNetCore`)
- [ ] No `using` statements referencing Mapster, FluentValidation, or other libraries
- [ ] Only allowed references: bITdevKit domain abstractions (`BridgingIT.DevKit.Domain`)
- [ ] No repository implementations (interfaces only, if any)
- [ ] No DbContext references
- [ ] No HTTP/API concerns (no `IResult`, `HttpContext`, etc.)

#### Example Violations

```csharp
// ❌ WRONG: Domain references Application
namespace MyApp.Domain.CustomerAggregate;

using MyApp.Application.Commands; // ❌ Domain → Application dependency

public class Customer : AggregateRoot<CustomerId>
{
    public CustomerCreatedCommand ToCommand() // ❌ Domain knows about commands
    {
        return new CustomerCreatedCommand(this.FirstName, this.LastName);
    }
}
```

```csharp
// ❌ WRONG: Domain references Infrastructure
namespace MyApp.Domain.CustomerAggregate;

using Microsoft.EntityFrameworkCore; // ❌ Domain → EF Core dependency

[Index(nameof(Email))] // ❌ EF Core attribute in Domain
public class Customer : AggregateRoot<CustomerId>
{
    // ...
}
```

```csharp
// ✅ CORRECT: Pure domain
namespace MyApp.Domain.CustomerAggregate;

using BridgingIT.DevKit.Domain; // ✅ Only bITdevKit domain abstractions

public class Customer : AuditableAggregateRoot<CustomerId>, IConcurrency
{
    private readonly List<Address> addresses = [];

    public IReadOnlyCollection<Address> Addresses => this.addresses.AsReadOnly();

    public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
    {
        // Pure business logic with Result<T>
    }
}
```

**Reference**: ADR-0001, ADR-0012 (Domain Logic in Domain Layer)

### Application Layer

**Rule**: Application layer may **only** reference Domain layer.

**ADR-0001 (Clean/Onion Architecture)** and **ADR-0011 (Application Logic in Commands & Queries)**: The application layer orchestrates use cases by coordinating domain objects and infrastructure abstractions. It should depend only on the domain layer and define abstractions (interfaces) for infrastructure to implement. This keeps application logic independent of technical implementation details like databases, web frameworks, or external services.

#### Checklist

- [ ] Only `using` statements reference Domain namespace
- [ ] No `using` statements referencing Infrastructure namespace
- [ ] No `using` statements referencing Presentation namespace
- [ ] No DbContext injected into handlers
- [ ] Uses repository abstractions (`IGenericRepository<T>`), not implementations
- [ ] No EF Core-specific code (no `.Include()`, `.AsNoTracking()`, etc.)
- [ ] No HTTP concerns (no `IResult`, `HttpContext`, status codes)
- [ ] No dependency on other modules' Application layers

#### Example Violations

```csharp
// ❌ WRONG: Application references Infrastructure
namespace MyApp.Application.Commands;

using MyApp.Infrastructure.EntityFramework; // ❌ Application → Infrastructure dependency

public class CustomerCreateCommandHandler
{
    private readonly CoreModuleDbContext context; // ❌ Direct DbContext usage

    public CustomerCreateCommandHandler(CoreModuleDbContext context)
    {
        this.context = context;
    }

    public async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...);
        this.context.Customers.Add(customer); // ❌ Application knows about EF Core
        await this.context.SaveChangesAsync(ct);
        return Result<CustomerId>.Success(customer.Id);
    }
}
```

```csharp
// ✅ CORRECT: Application uses repository abstraction
namespace MyApp.Application.Commands;

using BridgingIT.DevKit.Domain.Repositories; // ✅ Domain abstraction
using MyApp.Domain.CustomerAggregate; // ✅ Domain reference only

public class CustomerCreateCommandHandler : RequestHandlerBase<CustomerCreateCommand, Result<CustomerId>>
{
    private readonly IGenericRepository<Customer> repository; // ✅ Abstraction

    public CustomerCreateCommandHandler(IGenericRepository<Customer> repository)
    {
        this.repository = repository;
    }

    public override async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...); // ✅ Delegates to domain
        await this.repository.InsertAsync(customer, ct); // ✅ Uses abstraction
        return Result<CustomerId>.Success(customer.Id);
    }
}
```

**Reference**: ADR-0001, ADR-0004 (Repository Pattern), ADR-0011

### Infrastructure Layer

**Rule**: Infrastructure layer may reference Domain and Application layers.

**ADR-0001 (Clean/Onion Architecture)**: The infrastructure layer implements the abstractions defined by the application layer and provides concrete implementations for databases, external services, file systems, etc. It's acceptable for infrastructure to depend on both domain and application because it implements their contracts.

#### Checklist

- [ ] May reference Domain namespace
- [ ] May reference Application namespace
- [ ] Implements repository interfaces defined in Domain or Application
- [ ] Contains DbContext and EF Core configurations
- [ ] No reference to Presentation namespace
- [ ] No business logic (delegates to domain)

#### Example

```csharp
// ✅ CORRECT: Infrastructure implements abstractions
namespace MyApp.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore; // ✅ OK in Infrastructure
using MyApp.Domain.CustomerAggregate; // ✅ Domain reference
using BridgingIT.DevKit.Infrastructure.EntityFramework; // ✅ bITdevKit infrastructure

public class CoreModuleDbContext : ModuleDbContextBase
{
    public DbSet<Customer> Customers => this.Set<Customer>();
    public DbSet<Address> Addresses => this.Set<Address>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
```

**Reference**: ADR-0001, ADR-0007 (Entity Framework Core)

### Presentation Layer

**Rule**: Presentation layer may reference Application layer (via IRequester) but should minimize direct references to Domain.

**ADR-0014 (Minimal API Endpoints with DTO Exposure)** and **ADR-0005 (Requester/Notifier Mediator Pattern)**: The presentation layer provides thin adapters that translate HTTP requests into commands/queries and delegate to the application layer via IRequester. It should never contain business logic and should minimize direct domain references by using DTOs for API models.

#### Checklist

- [ ] References Application namespace (for commands/queries)
- [ ] Uses `IRequester.SendAsync()` to delegate to Application
- [ ] No business logic in endpoints
- [ ] No direct repository usage
- [ ] No DbContext usage
- [ ] Exposes DTOs, not domain entities directly
- [ ] Thin adapter pattern: map HTTP → Command → IRequester

#### Example

```csharp
// ✅ CORRECT: Thin adapter using IRequester
namespace MyApp.Presentation.Web.Endpoints;

using BridgingIT.DevKit.Application.Requester; // ✅ bITdevKit abstraction
using MyApp.Application.Commands; // ✅ Application reference

public class CustomerEndpoints : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/customers").WithTags("Customers");

        group.MapPost("", this.CreateCustomerAsync)
            .WithName("CreateCustomer")
            .Produces<CustomerId>(StatusCodes.Status201Created);
    }

    private async Task<IResult> CreateCustomerAsync(
        [FromBody] CustomerCreateCommand command,
        [FromServices] IRequester requester, // ✅ IRequester injected
        CancellationToken ct)
    {
        var result = await requester.SendAsync(command, ct); // ✅ Delegates to Application
        return result.MapHttpCreated(r => $"/api/customers/{r}"); // ✅ Maps Result to HTTP
    }
}
```

**Reference**: ADR-0001, ADR-0005, ADR-0014

## Cross-Module Dependencies (🔴 CRITICAL)

**Rule**: Modules should **NOT** directly reference each other. Use integration events or shared contracts instead.

**ADR-0003 (Modular Monolith Architecture)**: Each module in a modular monolith should be self-contained with clear boundaries. Direct references between modules create tight coupling that defeats the purpose of modularization and makes it difficult to extract modules into microservices later. Modules should communicate through integration events, shared contracts, or published interfaces.

### Checklist

- [ ] No `using` statements from one module's namespace to another module's namespace
- [ ] Module dependencies are one-way (e.g., CoreModule can depend on SharedKernel, but not vice versa)
- [ ] Cross-module communication uses integration events (INotifier)
- [ ] Shared concepts live in a SharedKernel or Common module
- [ ] No circular module dependencies

### Example Violations

```csharp
// ❌ WRONG: CoreModule references OrderModule directly
namespace MyApp.Modules.CoreModule.Application.Commands;

using MyApp.Modules.OrderModule.Domain.Model; // ❌ Cross-module reference

public class CustomerCreateCommandHandler
{
    public async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...);
        var order = new Order(customer.Id); // ❌ CoreModule knows about OrderModule internals
        // ...
    }
}
```

```csharp
// ✅ CORRECT: Use integration events
namespace MyApp.Modules.CoreModule.Application.Commands;

using BridgingIT.DevKit.Application.Notifier; // ✅ bITdevKit abstraction

public class CustomerCreateCommandHandler
{
    private readonly INotifier notifier;

    public async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...);
        await this.repository.InsertAsync(customer, ct);

        // ✅ Publish integration event (handled by OrderModule)
        await this.notifier.PublishAsync(
            new CustomerCreatedIntegrationEvent(customer.Id, customer.Email),
            ct);

        return Result<CustomerId>.Success(customer.Id);
    }
}
```

**Reference**: ADR-0003, ADR-0005

## Circular Reference Detection

### How to Detect Circular References

1. **Visual Studio**: Build will fail with error CS0246 or CS0104
2. **Manual inspection**: Check `using` statements for bidirectional references
3. **Architecture tests**: Use NetArchTest to enforce rules

### Example Architecture Test

```csharp
[Fact]
public void Domain_ShouldNotDependOn_Application()
{
    var result = Types.InAssembly(typeof(Customer).Assembly)
        .That()
        .ResideInNamespace("MyApp.Domain")
        .ShouldNot()
        .HaveDependencyOn("MyApp.Application")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}

[Fact]
public void Application_ShouldNotDependOn_Infrastructure()
{
    var result = Types.InAssembly(typeof(CustomerCreateCommand).Assembly)
        .That()
        .ResideInNamespace("MyApp.Application")
        .ShouldNot()
        .HaveDependencyOn("MyApp.Infrastructure")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

**Reference**: ADR-0013 (Unit Testing Strategy)

## Quick Verification Commands

```bash
# Check Domain layer has no external dependencies
rg "using.*Application" src/Modules/*/*.Domain/**/*.cs
rg "using.*Infrastructure" src/Modules/*/*.Domain/**/*.cs
rg "using Microsoft.EntityFrameworkCore" src/Modules/*/*.Domain/**/*.cs

# Check Application layer doesn't reference Infrastructure
rg "using.*Infrastructure" src/Modules/*/*.Application/**/*.cs
rg "DbContext" src/Modules/*/*.Application/**/*.cs

# Check for cross-module references
rg "using.*Modules\..*\.Application" src/Modules/*/Application/**/*.cs
```

## Summary

**Layer dependency rules are CRITICAL** to maintaining a clean architecture. Violations accumulate technical debt and make the codebase increasingly difficult to test, maintain, and evolve.

**Key takeaway**: Dependencies flow **inward only**:
- Domain → (nothing)
- Application → Domain
- Infrastructure → Application + Domain
- Presentation → Application (via IRequester)

**ADRs Referenced**:
- **ADR-0001**: Clean/Onion Architecture with Strict Layer Boundaries
- **ADR-0003**: Modular Monolith Architecture
- **ADR-0004**: Repository Pattern with Decorator Behaviors
- **ADR-0005**: Requester/Notifier (Mediator) Pattern
- **ADR-0007**: Entity Framework Core with Code-First Migrations
- **ADR-0011**: Application Logic in Commands & Queries
- **ADR-0012**: Domain Logic Encapsulation in Domain Layer
- **ADR-0014**: Minimal API Endpoints with DTO Exposure
