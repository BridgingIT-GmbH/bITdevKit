# CQRS Examples: Commands, Queries, Handlers

This document shows CQRS pattern implementations extracted from the CustomerCreateCommand pattern.

## Command Structure

### ✅ CORRECT: Command with Nested Validator

```csharp
namespace MyApp.Application.Commands;

using BridgingIT.DevKit.Application.Commands;
using FluentValidation;

/// <summary>
/// Command to create a new Customer aggregate.
/// </summary>
public class CustomerCreateCommand(CustomerModel model) : RequestBase<Result<CustomerModel>>
{
    public CustomerModel Model { get; set; } = model;

    // ✅ Nested Validator class
    public class Validator : AbstractValidator<CustomerCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();

            this.RuleFor(c => c.Model.FirstName)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");

            this.RuleFor(c => c.Model.LastName)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");

            this.RuleFor(c => c.Model.Email)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}
```

**Reference**: (ADR-0009), (ADR-0011)

---

## Handler with Repository Pattern

### ❌ WRONG: Business Logic in Handler

```csharp
// ❌ Handler contains business logic
public class CustomerCreateCommandHandler
{
    private readonly IGenericRepository<Customer> repository;

    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken ct)
    {
        // ❌ Business logic in handler (should be in domain)
        if (string.IsNullOrWhiteSpace(request.Model.FirstName))
        {
            return Result<CustomerModel>.Failure("First name is required");
        }

        if (request.Model.LastName == "notallowed")
        {
            return Result<CustomerModel>.Failure("Invalid last name");
        }

        // ❌ Creating entity with object initializer
        var customer = new Customer
        {
            FirstName = request.Model.FirstName,
            LastName = request.Model.LastName
        };

        await repository.InsertAsync(customer, ct);
        return Result<CustomerModel>.Success(...);
    }
}
```

### ✅ CORRECT: Handler Delegates to Domain

```csharp
// ✅ Handler delegates to domain methods
public class CustomerCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Customer> repository,
    IMapper mapper)
    : RequestHandlerBase<CustomerCreateCommand, Result<CustomerModel>>(loggerFactory)
{
    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken cancellationToken)
    {
        // ✅ Delegate to domain factory method
        var entityResult = Customer.Create(
            request.Model.FirstName,
            request.Model.LastName,
            request.Model.Email,
            CustomerNumber.Create());

        if (entityResult.IsFailure)
        {
            return entityResult.Unwrap();
        }

        // ✅ Use repository abstraction
        await repository.InsertAsync(entityResult.Value, cancellationToken);

        // ✅ Map to DTO
        var model = mapper.Map<Customer, CustomerModel>(entityResult.Value);

        return Result<CustomerModel>.Success(model);
    }
}
```

**Reference**: (ADR-0011), (ADR-0012)

---

## IRequester Usage in Endpoints

### ❌ WRONG: Direct Handler Injection

```csharp
// ❌ Endpoint injects handler directly
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] CustomerCreateCommandHandler handler, // ❌ Direct handler
    CancellationToken ct)
{
    var result = await handler.Handle(command, ct); // ❌ Bypasses pipeline behaviors
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

### ✅ CORRECT: IRequester Delegation

```csharp
// ✅ Endpoint uses IRequester
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] IRequester requester, // ✅ IRequester injected
    CancellationToken ct)
{
    var result = await requester.SendAsync(command, ct); // ✅ Pipeline behaviors applied
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

**Reference**: (ADR-0005), (ADR-0014)

---

## Summary

**Key CQRS Patterns**:
- **Commands**: `[Entity][Action]Command`, nested `Validator` (ADR-0009, ADR-0011)
- **Handlers**: Delegate to domain, use repository abstractions (ADR-0011, ADR-0012)
- **IRequester**: Endpoints use `IRequester.SendAsync()` (ADR-0005)

**References**:
- **(ADR-0005)**: Requester/Notifier Mediator Pattern
- **(ADR-0009)**: FluentValidation Strategy
- **(ADR-0011)**: Application Logic in Commands & Queries
- **(ADR-0012)**: Domain Logic in Domain Layer
- **(ADR-0014)**: Minimal API Endpoints
