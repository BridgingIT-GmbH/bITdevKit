# Result Pattern Examples

This document shows proper use of Result<T> instead of exceptions for expected failures.

## Pattern 1: Exceptions for Business Rules

### ❌ WRONG: Throwing Exceptions

```csharp
// ❌ Exception for business rule violation
public static Customer Create(string name, string email)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        throw new ValidationException("Name is required"); // ❌ Exception for validation
    }

    if (email == "invalid@test.com")
    {
        throw new BusinessRuleException("Email not allowed"); // ❌ Exception for business rule
    }

    return new Customer(name, email);
}
```

### ✅ CORRECT: Result<T> Pattern

```csharp
// ✅ Result<T> for expected failures
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    var emailAddressResult = EmailAddress.Create(email);
    if (emailAddressResult.IsFailure)
    {
        return emailAddressResult.Unwrap();
    }

    return Result<Customer>.Success()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
            new ValidationError("Invalid name: both first and last name must be provided"))
        .Ensure(_ => lastName != "notallowed",
            new ValidationError("Invalid last name: 'notallowed' is not permitted"))
        .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
        .Tap(e => e.DomainEvents.Register(new CustomerCreatedDomainEvent(e)));
}
```

**Reference**: (ADR-0002)

---

## Pattern 2: Result Chaining

### ✅ CORRECT: Using .Ensure(), .Bind(), .Tap()

```csharp
// ✅ Functional composition with Result<T>
public Result<Customer> ChangeBirthDate(DateOnly? dateOfBirth)
{
    var currentDate = TimeProviderAccessor.Current.GetUtcNow().ToDateOnly();

    return this.Change()
        .When(_ => dateOfBirth.HasValue)
        .Ensure(_ => dateOfBirth <= currentDate,
            "Invalid date of birth: cannot be in the future")
        .Ensure(_ => dateOfBirth >= currentDate.AddYears(-150),
            "Invalid date of birth: age exceeds maximum")
        .Set(e => e.DateOfBirth, dateOfBirth)
        .Register(e => new CustomerUpdatedDomainEvent(e))
        .Apply();
}
```

**Reference**: (ADR-0002)

---

## Pattern 3: MapHttpCreated/MapHttpOk

### ✅ CORRECT: Mapping Result<T> to HTTP

```csharp
// ✅ POST: MapHttpCreated
group.MapPost("", async ([FromBody] CustomerCreateCommand command, [FromServices] IRequester requester, CancellationToken ct) =>
{
    var result = await requester.SendAsync(command, ct);
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
});

// ✅ GET: MapHttpOk
group.MapGet("{id:guid}", async ([FromRoute] Guid id, [FromServices] IRequester requester, CancellationToken ct) =>
{
    var query = new CustomerFindOneQuery(id);
    var result = await requester.SendAsync(query, ct);
    return result.MapHttpOk();
});

// ✅ DELETE: MapHttpNoContent
group.MapDelete("{id:guid}", async ([FromRoute] Guid id, [FromServices] IRequester requester, CancellationToken ct) =>
{
    var command = new CustomerDeleteCommand(new CustomerId(id));
    var result = await requester.SendAsync(command, ct);
    return result.MapHttpNoContent();
});
```

**Reference**: (ADR-0002), (ADR-0014)

---

## Summary

**Use Result<T>** for:
- Validation errors
- Business rule violations
- Not-found scenarios

**Use Exceptions** for:
- System failures (database down, network timeout)
- Programming errors (null reference, index out of bounds)

**References**:
- **(ADR-0002)**: Result Pattern for Error Handling
- **(ADR-0014)**: Minimal API Endpoints
