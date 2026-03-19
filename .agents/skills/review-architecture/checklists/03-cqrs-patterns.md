# Checklist: CQRS Patterns

This checklist helps verify that Command Query Responsibility Segregation (CQRS) patterns are correctly implemented in the application layer.

## Command/Query Naming (🟡 IMPORTANT)

**ADR-0011 (Application Logic in Commands & Queries)**: Commands and queries represent the use cases of the application. Consistent naming makes the codebase navigable and self-documenting. The naming convention clearly distinguishes commands (state-changing operations) from queries (read-only operations).

### Command Naming

**Pattern**: `[Entity][Action]Command`

- [ ] Follows pattern: `CustomerCreateCommand`, `OrderPlaceCommand`, `InvoiceGenerateCommand`
- [ ] NOT: `CreateCustomerRequest`, `Create`, `CustomerCreate`
- [ ] Located in `<Module>.Application/Commands/` folder
- [ ] File name matches class name

### Query Naming

**Pattern**: `[Entity][Action]Query`

- [ ] Follows pattern: `CustomerFindAllQuery`, `OrderGetByIdQuery`, `InvoiceFindOverdueQuery`
- [ ] NOT: `GetAllCustomersQuery`, `FindCustomer`, `CustomerQuery`
- [ ] Located in `<Module>.Application/Queries/` folder
- [ ] File name matches class name

### Examples

```csharp
// ✅ CORRECT: Command naming
public class CustomerCreateCommand(CustomerModel model) : RequestBase<CustomerModel>
{
    public CustomerModel Model { get; set; } = model;
}

public class CustomerUpdateCommand(Guid id, CustomerModel model) : RequestBase<Result<CustomerModel>>
{
    public Guid Id { get; set; } = id;
    public CustomerModel Model { get; set; } = model;
}

public class CustomerDeleteCommand(CustomerId id) : RequestBase<Result>
{
    public CustomerId Id { get; set; } = id;
}
```

```csharp
// ✅ CORRECT: Query naming
public class CustomerFindAllQuery : RequestBase<Result<IEnumerable<CustomerModel>>>
{
}

public class CustomerFindOneQuery(Guid id) : RequestBase<Result<CustomerModel>>
{
    public Guid Id { get; set; } = id;
}
```

```csharp
// ❌ WRONG: Poor naming
public class CreateCustomerRequest(...) // ❌ "Request" suffix, wrong order
public class GetCustomer(...) // ❌ Missing "Query" suffix
public class Customer(...) // ❌ Ambiguous, no action
```

**Reference**: ADR-0011

## Handler Patterns

**ADR-0011**: Handlers orchestrate use cases by coordinating domain objects and infrastructure abstractions. They should NOT contain business logic; instead, they delegate to domain methods and services.

### Handler Naming

**Pattern**: `[Entity][Command|Query]Handler` or `[CommandName]Handler`

- [ ] Follows pattern: `CustomerCreateCommandHandler`, `CustomerFindAllQueryHandler`
- [ ] NOT: `CreateHandler`, `CustomerHandler`

### Handler Location

- [ ] Co-located with command/query (same folder)
- [ ] Can be in same file or separate file
- [ ] Named `[CommandName]Handler.cs` if separate file

### Handler Structure

- [ ] Derives from `RequestHandlerBase<TRequest, TResponse>`
- [ ] Overrides `Handle()` method
- [ ] Injects repository abstractions (e.g., `IGenericRepository<Customer>`), NOT DbContext
- [ ] Delegates to domain methods (e.g., `Customer.Create()`)
- [ ] Returns `Result<T>` or `Result`
- [ ] Accepts `CancellationToken` parameter

### Example: Command Handler

```csharp
// ✅ CORRECT: Handler delegates to domain
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

        var entity = entityResult.Value;

        // ✅ Add addresses (if any)
        if (request.Model.Addresses?.Any() == true)
        {
            foreach (var addressModel in request.Model.Addresses)
            {
                var addressResult = entity.AddAddress(
                    addressModel.Name,
                    addressModel.Line1,
                    addressModel.Line2,
                    addressModel.PostalCode,
                    addressModel.City,
                    addressModel.Country,
                    addressModel.IsPrimary);

                if (addressResult.IsFailure)
                {
                    return addressResult.Unwrap();
                }
            }
        }

        // ✅ Use repository abstraction
        await repository.InsertAsync(entity, cancellationToken);

        // ✅ Map to DTO
        var model = mapper.Map<Customer, CustomerModel>(entity);

        return Result<CustomerModel>.Success(model);
    }
}
```

### Common Violations

```csharp
// ❌ WRONG: Business logic in handler
public class CustomerCreateCommandHandler
{
    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken cancellationToken)
    {
        // ❌ Business logic in handler (should be in domain)
        if (string.IsNullOrWhiteSpace(request.Model.FirstName) || 
            string.IsNullOrWhiteSpace(request.Model.LastName))
        {
            return Result<CustomerModel>.Failure("Name is required");
        }

        // ❌ Creating entity with constructor instead of factory method
        var entity = new Customer
        {
            FirstName = request.Model.FirstName,
            LastName = request.Model.LastName,
            Email = request.Model.Email
        };

        await repository.InsertAsync(entity, cancellationToken);
        return Result<CustomerModel>.Success(...);
    }
}
```

**Reference**: ADR-0011, ADR-0012 (Domain Logic in Domain Layer)

## Validator Patterns (🟡 IMPORTANT)

**ADR-0009 (FluentValidation Strategy)**: FluentValidation provides a fluent interface for building strongly-typed validation rules. Validators are nested within commands/queries for easy discoverability and co-location.

### Validator Structure

- [ ] Nested class named `Validator` inside command/query
- [ ] Derives from `AbstractValidator<TCommand>` or `AbstractValidator<TQuery>`
- [ ] Validation rules defined in constructor
- [ ] Uses FluentValidation DSL (`.NotNull()`, `.NotEmpty()`, `.Must()`, `.When()`, etc.)
- [ ] Custom validation messages with `.WithMessage()`
- [ ] Complex validation rules use `.Must()` with delegate

### Example

```csharp
// ✅ CORRECT: Nested validator with FluentValidation
public class CustomerCreateCommand(CustomerModel model) : RequestBase<CustomerModel>
{
    public CustomerModel Model { get; set; } = model;

    // ✅ Nested Validator class
    public class Validator : AbstractValidator<CustomerCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();

            this.RuleFor(c => c.Model.Id).MustBeDefaultOrEmptyGuid()
                .WithMessage("Must be empty.");

            this.RuleFor(c => c.Model.FirstName)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");

            this.RuleFor(c => c.Model.LastName)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");

            this.RuleFor(c => c.Model.Email)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");

            // ✅ Complex validation rule
            this.RuleFor(c => c.Model.Addresses)
                .Must(addresses => addresses == null || addresses.Count(a => a.IsPrimary) <= 1)
                .WithMessage("Only one address can be marked as primary");

            // ✅ Child rules for collections
            this.RuleForEach(c => c.Model.Addresses).ChildRules(address =>
            {
                address.RuleFor(a => a.Line1)
                    .NotEmpty().WithMessage("Address line 1 is required")
                    .MaximumLength(256).WithMessage("Address line 1 must not exceed 256 characters");

                address.RuleFor(a => a.City)
                    .NotEmpty().WithMessage("City is required")
                    .MaximumLength(100).WithMessage("City must not exceed 100 characters");
            });
        }
    }
}
```

### Validator Registration

Validators are automatically discovered and registered by the pipeline behavior:

```csharp
// ✅ Registered in module
services.AddRequester(o => o
    .WithBehavior<ValidationPipelineBehavior>() // ✅ Validates commands/queries
    .WithBehavior<ModuleScopeBehavior>()
    .WithBehavior<RetryPipelineBehavior>()
    .WithBehavior<TimeoutPipelineBehavior>());
```

### Common Violations

```csharp
// ❌ WRONG: Validator in separate file
public class CustomerCreateCommandValidator : AbstractValidator<CustomerCreateCommand> // ❌ Not nested
{
    // ...
}
```

```csharp
// ❌ WRONG: Manual validation in handler
public class CustomerCreateCommandHandler
{
    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken cancellationToken)
    {
        // ❌ Manual validation (should be in Validator class)
        if (string.IsNullOrWhiteSpace(request.Model.FirstName))
        {
            return Result<CustomerModel>.Failure("First name is required");
        }

        // ...
    }
}
```

**Reference**: ADR-0009

## IRequester Usage (🟡 IMPORTANT)

**ADR-0005 (Requester/Notifier Mediator Pattern)**: IRequester decouples the presentation layer from application handlers. Endpoints should use `IRequester.SendAsync()` to delegate to handlers, not instantiate handlers directly.

### Checklist

- [ ] Endpoints inject `IRequester` (not handlers directly)
- [ ] Endpoints call `await requester.SendAsync(command, ct)`
- [ ] No direct handler instantiation in endpoints
- [ ] Pipeline behaviors applied automatically (validation, retry, timeout)

### Example: Endpoint Using IRequester

```csharp
// ✅ CORRECT: Endpoint delegates to IRequester
public class CustomerEndpoints : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/customers").WithTags("Customers");

        group.MapPost("", this.CreateCustomerAsync)
            .WithName("CreateCustomer");
    }

    private async Task<IResult> CreateCustomerAsync(
        [FromBody] CustomerCreateCommand command,
        [FromServices] IRequester requester, // ✅ Inject IRequester
        CancellationToken ct)
    {
        var result = await requester.SendAsync(command, ct); // ✅ Delegate to handler
        return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
    }
}
```

### Common Violations

```csharp
// ❌ WRONG: Direct handler instantiation
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] CustomerCreateCommandHandler handler, // ❌ Direct injection
    CancellationToken ct)
{
    var result = await handler.Handle(command, ct); // ❌ Bypasses pipeline behaviors
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

**Reference**: ADR-0005, ADR-0014 (Minimal API Endpoints)

## Pipeline Behavior Attributes (Optional)

**ADR-0005**: Pipeline behaviors provide cross-cutting concerns like validation, retry, and timeout. These are optional attributes on commands/queries.

### Available Attributes

- [ ] `[ValidationBehavior]`: Executes FluentValidation validators
- [ ] `[RetryBehavior]`: Retries failed operations
- [ ] `[TimeoutBehavior]`: Enforces timeout on operations

### Example

```csharp
// ✅ Optional: Explicit retry and timeout
[RetryBehavior(Attempts = 3)]
[TimeoutBehavior(Timeout = "00:00:30")]
public class CustomerUpdateCommand(...) : RequestBase<Result<CustomerModel>>
{
    // ...
}
```

**Note**: These attributes are optional. The pipeline behaviors are registered globally and apply based on Result<T> failures and configuration.

**Reference**: ADR-0005

## Summary

**CQRS patterns provide clear separation** between read and write operations, making the codebase easier to navigate and maintain.

**Key takeaways**:
- **Commands**: `[Entity][Action]Command` pattern, nested `Validator` class
- **Queries**: `[Entity][Action]Query` pattern
- **Handlers**: Derive from `RequestHandlerBase`, delegate to domain, use repository abstractions
- **Validators**: Nested class using FluentValidation, automatically executed by pipeline
- **IRequester**: Endpoints use `IRequester.SendAsync()` to decouple from handlers

**ADRs Referenced**:
- **ADR-0005**: Requester/Notifier (Mediator) Pattern
- **ADR-0009**: FluentValidation Strategy
- **ADR-0011**: Application Logic in Commands & Queries
- **ADR-0012**: Domain Logic Encapsulation in Domain Layer
- **ADR-0014**: Minimal API Endpoints with DTO Exposure
