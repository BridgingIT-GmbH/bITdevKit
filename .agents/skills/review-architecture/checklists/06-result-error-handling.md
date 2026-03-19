# Checklist: Result & Error Handling

This checklist helps verify proper use of the Result<T> pattern for error handling throughout the application.

## Result<T> Pattern Enforcement (🔴 CRITICAL)

**ADR-0002 (Result Pattern for Error Handling)**: The Result<T> pattern makes success/failure explicit in method signatures and enables functional composition of operations. It should be used for all expected failures (validation errors, business rule violations, not-found scenarios), while exceptions are reserved for truly exceptional circumstances (system failures, bugs, null reference errors).

### When to Use Result<T>

- [ ] Domain methods return `Result<T>` or `Result` for operations that can fail
- [ ] Application handlers return `Result<T>` or `Result`
- [ ] Factory methods (`Create()`) return `Result<TEntity>` or `Result<TValueObject>`
- [ ] Change methods (`ChangeName()`, `ChangeEmail()`) return `Result<TEntity>`
- [ ] Business rule violations return `Result.Failure("error message")`
- [ ] NOT-FOUND scenarios return `Result.Failure<T>(new NotFoundResultError())`
- [ ] Validation errors return `Result.Failure(new ValidationError("message"))`

### When to Use Exceptions

- [ ] **System failures**: Database unavailable, network timeout, out of memory
- [ ] **Programming errors**: Null reference, index out of range, invalid cast
- [ ] **Framework exceptions**: ASP.NET Core pipeline exceptions
- [ ] **NOT for business rules**: Use `Result<T>` instead

### Example: Domain Method with Result<T>

```csharp
// ✅ CORRECT: Factory method returns Result<T>
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    var emailAddressResult = EmailAddress.Create(email);
    if (emailAddressResult.IsFailure)
    {
        return emailAddressResult.Unwrap(); // ✅ Propagate failure
    }

    return Result<Customer>.Success()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
            new ValidationError("Invalid name: both first and last name must be provided")) // ✅ Validation error
        .Ensure(_ => lastName != "notallowed",
            new ValidationError("Invalid last name: 'notallowed' is not permitted")) // ✅ Business rule
        .Ensure(_ => email != null,
            new ValidationError("Email cannot be null"))
        .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
        .Tap(e => e.DomainEvents.Register(new CustomerCreatedDomainEvent(e)));
}
```

### Common Violations

```csharp
// ❌ WRONG: Throwing exception for business rule
public static Customer Create(string firstName, string lastName, string email, CustomerNumber number)
{
    if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
    {
        throw new ValidationException("Name is required"); // ❌ Exception for expected failure
    }

    if (lastName == "notallowed")
    {
        throw new BusinessRuleException("Invalid last name"); // ❌ Exception for business rule
    }

    return new Customer(firstName, lastName, email, number);
}
```

```csharp
// ❌ WRONG: Returning null instead of Result
public static Customer Create(string firstName, string lastName, string email)
{
    if (string.IsNullOrWhiteSpace(firstName))
    {
        return null; // ❌ Null return (caller must check for null)
    }

    return new Customer(firstName, lastName, email);
}
```

**Reference**: ADR-0002

## Result Unwrapping (🟡 IMPORTANT)

**ADR-0002**: Result<T> should be unwrapped using functional operators (`.Bind()`, `.Ensure()`, `.Tap()`) or `.Match()`, not manual `if/else` checks.

### Functional Operators

- [ ] **`.Bind(func)`**: Transform success value (e.g., map Customer to CustomerModel)
- [ ] **`.BindAsync(func)`**: Async transformation
- [ ] **`.BindResult(func)`**: Chain operations returning Results
- [ ] **`.Ensure(predicate, error)`**: Inline validation (fails if predicate returns false)
- [ ] **`.Tap(action)`**: Execute side effects without changing result (e.g., register domain events)
- [ ] **`.Map(func)`**: Transform to different type
- [ ] **`.Match(onSuccess, onFailure)`**: Handle both success and failure paths

### Example: Functional Composition

```csharp
// ✅ CORRECT: Using .Bind() and .Ensure() for composition
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    var emailAddressResult = EmailAddress.Create(email);
    if (emailAddressResult.IsFailure)
    {
        return emailAddressResult.Unwrap();
    }

    return Result<Customer>.Success()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
            new ValidationError("Invalid name"))
        .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number)) // ✅ Bind to create entity
        .Tap(e => e.DomainEvents.Register(new CustomerCreatedDomainEvent(e))); // ✅ Tap for side effect
}
```

### Example: .Match() for Explicit Handling

```csharp
// ✅ CORRECT: Using .Match() for explicit success/failure handling
var result = Customer.Create(firstName, lastName, email, number);

return result.Match(
    onSuccess: customer => 
    {
        // Handle success
        return Result<CustomerModel>.Success(this.mapper.Map<Customer, CustomerModel>(customer));
    },
    onFailure: () => 
    {
        // Handle failure
        return Result<CustomerModel>.Failure(result.Errors);
    });
```

### Common Violations

```csharp
// ❌ WRONG: Manual if/else instead of functional operators
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    // ❌ Manual if/else checks instead of .Ensure()
    if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
    {
        return Result<Customer>.Failure("Invalid name");
    }

    if (lastName == "notallowed")
    {
        return Result<Customer>.Failure("Invalid last name");
    }

    var customer = new Customer(firstName, lastName, email, number);
    return Result<Customer>.Success(customer);
}
```

**Reference**: ADR-0002

## HTTP Mapping (🔴 CRITICAL)

**ADR-0014 (Minimal API Endpoints with DTO Exposure)**: Result<T> must be mapped to HTTP responses using bITdevKit extension methods, not manual checks.

### Extension Methods

- [ ] **`.MapHttpOk()`**: 200 OK for successful queries
- [ ] **`.MapHttpOkAll()`**: 200 OK for collections
- [ ] **`.MapHttpCreated(location)`**: 201 Created for successful commands that create resources
- [ ] **`.MapHttpNoContent()`**: 204 No Content for successful commands that don't return data
- [ ] **`.ProducesResultProblem()`**: Produces RFC 7807 problem details for failures

### Example: HTTP Mapping

```csharp
// ✅ CORRECT: Using extension methods for HTTP mapping
public override void Map(IEndpointRouteBuilder app)
{
    var group = app.MapGroup("api/customers").WithTags("Customers");

    // ✅ POST: MapHttpCreated for 201 Created
    group.MapPost("", async ([FromBody] CustomerCreateCommand command, [FromServices] IRequester requester, CancellationToken ct) =>
    {
        var result = await requester.SendAsync(command, ct);
        return result.MapHttpCreated(r => $"/api/customers/{r.Id}"); // ✅ 201 Created on success, problem details on failure
    });

    // ✅ GET: MapHttpOk for 200 OK
    group.MapGet("{id:guid}", async ([FromRoute] Guid id, [FromServices] IRequester requester, CancellationToken ct) =>
    {
        var query = new CustomerFindOneQuery(id);
        var result = await requester.SendAsync(query, ct);
        return result.MapHttpOk(); // ✅ 200 OK on success, problem details on failure
    });

    // ✅ PUT: MapHttpOk for 200 OK (returns updated resource)
    group.MapPut("{id:guid}", async ([FromRoute] Guid id, [FromBody] CustomerModel model, [FromServices] IRequester requester, CancellationToken ct) =>
    {
        var command = new CustomerUpdateCommand(id, model);
        var result = await requester.SendAsync(command, ct);
        return result.MapHttpOk(); // ✅ 200 OK with updated resource
    });

    // ✅ DELETE: MapHttpNoContent for 204 No Content
    group.MapDelete("{id:guid}", async ([FromRoute] Guid id, [FromServices] IRequester requester, CancellationToken ct) =>
    {
        var command = new CustomerDeleteCommand(new CustomerId(id));
        var result = await requester.SendAsync(command, ct);
        return result.MapHttpNoContent(); // ✅ 204 No Content on success, problem details on failure
    });

    // ✅ GET collection: MapHttpOkAll for 200 OK
    group.MapGet("", async ([FromServices] IRequester requester, CancellationToken ct) =>
    {
        var query = new CustomerFindAllQuery();
        var result = await requester.SendAsync(query, ct);
        return result.MapHttpOkAll(); // ✅ 200 OK with collection
    });
}
```

### Common Violations

```csharp
// ❌ WRONG: Manual if/else for HTTP mapping
group.MapPost("", async ([FromBody] CustomerCreateCommand command, [FromServices] IRequester requester, CancellationToken ct) =>
{
    var result = await requester.SendAsync(command, ct);

    // ❌ Manual if/else instead of .MapHttpCreated()
    if (result.IsSuccess)
    {
        return Results.Created($"/api/customers/{result.Value.Id}", result.Value);
    }
    else
    {
        return Results.BadRequest(result.Errors);
    }
});
```

**Reference**: ADR-0002, ADR-0014

## Error Messages (🟢 SUGGESTION)

**ADR-0002**: Error messages should be clear, actionable, and business-focused, not technical implementation details.

### Checklist

- [ ] Error messages are descriptive (not just "Validation failed")
- [ ] Error messages indicate what went wrong and how to fix it
- [ ] Use specific error types: `ValidationError`, `NotFoundError`, `ConflictError`
- [ ] Avoid exposing internal implementation details (e.g., "DbContext SaveChanges failed")
- [ ] Include relevant context (e.g., "Customer with ID {id} not found")

### Example: Clear Error Messages

```csharp
// ✅ CORRECT: Clear, actionable error messages
public static Result<EmailAddress> Create(string value)
{
    value = value?.Trim()?.ToLowerInvariant();

    var ruleResult = Rule.Add(RuleSet.IsValidEmail(value)).Check();

    if (ruleResult.IsFailure)
    {
        return Result<EmailAddress>.Failure()
            .WithMessages(ruleResult.Messages)
            .WithErrors(ruleResult.Errors); // ✅ Clear validation error from rule
    }

    return new EmailAddress(value);
}

public Result<Customer> ChangeName(string firstName, string lastName)
{
    return this.Change()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
            "Invalid name: both first and last name must be provided") // ✅ Explains what's wrong and what's needed
        .Ensure(_ => lastName != "notallowed",
            "Invalid last name: 'notallowed' is not permitted") // ✅ Specific business rule violation
        .Set(e => e.FirstName, firstName)
        .Set(e => e.LastName, lastName)
        .Register(e => new CustomerUpdatedDomainEvent(e))
        .Apply();
}
```

### Error Types

- [ ] **`ValidationError`**: Input validation failures (format, required fields, length)
- [ ] **`NotFoundResultError`**: Entity not found by ID
- [ ] **`ConflictError`**: State conflicts (e.g., duplicate email, concurrency violation)
- [ ] **`BusinessRuleError`**: Business rule violations

**Reference**: ADR-0002

## Result Chaining in Handlers

**ADR-0002**: Handlers should compose Results from multiple operations using chaining.

### Example: Handler with Result Chaining

```csharp
// ✅ CORRECT: Handler chains Results from domain operations
public class CustomerCreateCommandHandler : RequestHandlerBase<CustomerCreateCommand, Result<CustomerModel>>
{
    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken cancellationToken)
    {
        // ✅ Create customer (Result<Customer>)
        var entityResult = Customer.Create(
            request.Model.FirstName,
            request.Model.LastName,
            request.Model.Email,
            CustomerNumber.Create());

        if (entityResult.IsFailure)
        {
            return entityResult.Unwrap(); // ✅ Propagate failure
        }

        var entity = entityResult.Value;

        // ✅ Add addresses (each returns Result<Customer>)
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
                    return addressResult.Unwrap(); // ✅ Propagate failure
                }
            }
        }

        // ✅ Persist to repository
        await this.repository.InsertAsync(entity, cancellationToken);

        // ✅ Map to DTO
        var model = this.mapper.Map<Customer, CustomerModel>(entity);

        return Result<CustomerModel>.Success(model);
    }
}
```

**Reference**: ADR-0002, ADR-0011

## Summary

**Result<T> pattern is CRITICAL** for explicit error handling and functional composition. It makes success/failure paths clear in method signatures and eliminates exception-driven control flow for expected failures.

**Key takeaways**:
- **Use Result<T>**: For all expected failures (validation, business rules, not-found)
- **Use Exceptions**: Only for truly exceptional circumstances (system failures, bugs)
- **Functional operators**: Use `.Bind()`, `.Ensure()`, `.Tap()` for composition
- **HTTP mapping**: Use `.MapHttpOk()`, `.MapHttpCreated()`, `.MapHttpNoContent()`
- **Clear error messages**: Descriptive, actionable, business-focused

**ADRs Referenced**:
- **ADR-0002**: Result Pattern for Error Handling
- **ADR-0011**: Application Logic in Commands & Queries
- **ADR-0014**: Minimal API Endpoints with DTO Exposure
