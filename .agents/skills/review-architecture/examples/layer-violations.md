# Layer Violations: Common Boundary Violations

This document shows common layer boundary violations and their fixes.

## Violation 1: Application → Infrastructure (DbContext)

### ❌ WRONG: DbContext in Application Handler

```csharp
// ❌ Application references Infrastructure
namespace MyApp.Application.Commands;

using MyApp.Infrastructure.EntityFramework; // ❌ Application → Infrastructure

public class CustomerCreateCommandHandler
{
    private readonly CoreModuleDbContext context; // ❌ DbContext in Application

    public async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...);
        this.context.Customers.Add(customer.Value); // ❌ EF Core API in Application
        await this.context.SaveChangesAsync(ct);
        return Result<CustomerId>.Success(customer.Value.Id);
    }
}
```

### ✅ CORRECT: Repository Abstraction

```csharp
// ✅ Application uses repository abstraction
namespace MyApp.Application.Commands;

using BridgingIT.DevKit.Domain.Repositories; // ✅ Domain abstraction

public class CustomerCreateCommandHandler
{
    private readonly IGenericRepository<Customer> repository; // ✅ Abstraction

    public async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...);
        await this.repository.InsertAsync(customer.Value, ct); // ✅ Repository method
        return Result<CustomerId>.Success(customer.Value.Id);
    }
}
```

**Reference**: (ADR-0001), (ADR-0004)

---

## Violation 2: Domain → Application

### ❌ WRONG: Domain References Application

```csharp
// ❌ Domain references Application
namespace MyApp.Domain.CustomerAggregate;

using MyApp.Application.Commands; // ❌ Domain → Application

public class Customer : AggregateRoot<CustomerId>
{
    public CustomerCreatedCommand ToCommand() // ❌ Domain knows about commands
    {
        return new CustomerCreatedCommand(this.FirstName, this.LastName);
    }
}
```

### ✅ CORRECT: Application Creates Commands from Domain

```csharp
// ✅ Application creates commands, not Domain
namespace MyApp.Application.Commands;

public class CustomerCreateCommandHandler
{
    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken ct)
    {
        var entityResult = Customer.Create(...); // ✅ Domain stays pure

        // ✅ Application maps domain to DTO
        var model = mapper.Map<Customer, CustomerModel>(entityResult.Value);
        return Result<CustomerModel>.Success(model);
    }
}
```

**Reference**: (ADR-0001), (ADR-0012)

---

## Violation 3: Cross-Module Direct References

### ❌ WRONG: CoreModule References OrderModule

```csharp
// ❌ Cross-module reference
namespace MyApp.Modules.CoreModule.Application.Commands;

using MyApp.Modules.OrderModule.Domain.Model; // ❌ CoreModule → OrderModule

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

### ✅ CORRECT: Integration Events

```csharp
// ✅ Use integration events
namespace MyApp.Modules.CoreModule.Application.Commands;

using BridgingIT.DevKit.Application.Notifier; // ✅ bITdevKit abstraction

public class CustomerCreateCommandHandler
{
    private readonly INotifier notifier;

    public async Task<Result<CustomerId>> Handle(CustomerCreateCommand request, CancellationToken ct)
    {
        var customer = Customer.Create(...);
        await repository.InsertAsync(customer.Value, ct);

        // ✅ Publish integration event (OrderModule handles it)
        await notifier.PublishAsync(
            new CustomerCreatedIntegrationEvent(customer.Id, customer.Email),
            ct);

        return Result<CustomerId>.Success(customer.Value.Id);
    }
}
```

**Reference**: (ADR-0003), (ADR-0005)

---

## Summary

**Layer dependency rules** (ADR-0001):
- Domain → (nothing)
- Application → Domain
- Infrastructure → Application + Domain
- Presentation → Application (via IRequester)

**References**:
- **(ADR-0001)**: Clean/Onion Architecture
- **(ADR-0003)**: Modular Monolith Architecture
- **(ADR-0004)**: Repository Pattern
- **(ADR-0005)**: Requester/Notifier
- **(ADR-0012)**: Domain Logic in Domain Layer
