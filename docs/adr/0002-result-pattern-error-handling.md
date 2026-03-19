# ADR-0002: Result Pattern for Error Handling

## Status

Accepted

## Context

Traditional error handling in .NET relies heavily on exceptions. While exceptions work for truly exceptional circumstances, using them for expected failure scenarios (validation errors, business rule violations, not-found scenarios) creates several problems:

- **Performance overhead**: Exception throwing and stack unwinding is expensive
- **Control flow obscurity**: Success/failure paths are not explicit in method signatures
- **Exception-driven development**: Code that expects failures uses try-catch for control flow
- **Lost context**: Stack traces may not provide business-level context for failures
- **Testing complexity**: Testing error paths requires catching exceptions
- **Composition difficulty**: Chaining operations with exceptions requires nested try-catch blocks

The application needed an error handling approach that:

1. Makes success/failure explicit in method signatures
2. Enables functional composition of operations
3. Provides rich error context without performance penalties
4. Supports railway-oriented programming patterns
5. Improves readability and testability

## Decision

Adopt the **Result Pattern** (also known as Railway-Oriented Programming) using bITdevKit's `Result<T>` type for all operations that can fail in expected ways.

### Result Type Structure

```csharp
public class Result<T>
{
    public T Value { get; }                      // Success value
    public bool IsSuccess { get; }               // Success indicator
    public bool IsFailure { get; }               // Failure indicator
    public IEnumerable<IResultMessage> Messages { get; }  // Informational messages
    public IEnumerable<IResultError> Errors { get; }      // Error details
}
```

### Usage Pattern

- **Domain methods** return `Result<T>` for operations that can fail due to business rules
- **Application handlers** compose Results using functional operators
- **Exceptions** are reserved for truly exceptional circumstances (system failures, bugs)

### Key Operators

- **`Bind()`**: Transform success value
- **`BindAsync()`**: Async transformation
- **`BindResult()`**: Chain operations returning Results
- **`Ensure()`**: Inline validation (fails on false)
- **`Unless()`/`UnlessAsync()`**: Business rule checking
- **`Map()`**: Transform to different type
- **`Tap()`**: Execute side effects without changing result
- **`Log()`**: Logging extension

## Rationale

1. **Explicit Contracts**: Method signatures communicate failure possibility (`Result<Customer>` vs `Customer`)
2. **Railway Oriented Programming**: Once a failure occurs, subsequent operations are automatically skipped
3. **Functional Composition**: Chain operations cleanly without nested try-catch blocks
4. **Performance**: Avoids exception overhead for expected failures
5. **Testability**: Easy to assert success/failure and inspect error details
6. **Rich Error Context**: Errors carry business-meaningful messages, not just stack traces
7. **Consistent Pattern**: Same approach across all layers (Domain, Application)

## Consequences

### Positive

- Method signatures explicitly document success/failure scenarios
- Operations can be composed functionally using `Bind`, `Map`, `Ensure`, etc.
- No performance penalty for expected failures (validation, business rules)
- Error context is business-oriented (e.g., "Email already exists") not technical stack traces
- Test assertions are straightforward: `result.ShouldBeSuccess()` or `result.ShouldBeFailure()`
- Railway pattern prevents executing subsequent operations after failures
- Consistent error handling across entire codebase

### Negative

- Learning curve for developers unfamiliar with functional patterns
- More verbose than simple return values or exceptions
- Requires discipline to propagate Results correctly (not unwrap prematurely)
- IntelliSense discoverability of Result operators may require familiarity

### Neutral

- Domain factory methods return `Result<T>` instead of throwing exceptions
- Application handlers use Result composition pipelines
- Exceptions still used for system-level failures (database unavailable, configuration errors)

## Alternatives Considered

- **Alternative 1: Traditional Exception-Based Error Handling**
  - Rejected because exceptions are expensive for expected failures
  - Try-catch control flow is harder to reason about and compose
  - Signatures don't communicate failure modes

- **Alternative 2: Tuple Return Types `(bool success, T value, string error)`**
  - Rejected because tuples lack semantic meaning and discoverability
  - No support for functional composition
  - Difficult to carry multiple errors or messages

- **Alternative 3: OneOf/Union Types**
  - Rejected because they require explicit pattern matching on every call site
  - Less ergonomic composition than Result operators

- **Alternative 4: Nullable Reference Types with null for failure**
  - Rejected because null provides no error context
  - Cannot distinguish different failure reasons
  - Null-based APIs are error-prone

## Related Decisions

- [ADR-0011](0011-application-logic-in-commands-queries.md): Application handlers compose Results
- [ADR-0012](0012-domain-logic-in-domain-layer.md): Domain methods return Results
- [ADR-0001](0001-clean-onion-architecture.md): Result pattern used across all layers

## References

- [bITdevKit Results Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-results.md)
- [README - Result Pattern](../../README.md#result-pattern-railway-oriented-programming)
- [CoreModule README - Handler Deep Dive](../../src/Modules/CoreModule/CoreModule-README.md#handler-implementation-example)
- [Railway Oriented Programming - Scott Wlaschin](https://fsharpforfunandprofit.com/rop/)

## Notes

### Railway-Oriented Programming Concept

Once a step fails, all subsequent steps are skipped and the failure flows directly to the end:

```
[Start] → [Validation] → [Business Rule] → [Persistence] → [Mapping] → [Success]
             ↓               ↓                 ↓
           [Failure] ←──────┴─────────────────┘
```

### Domain Example: Customer Creation

```csharp
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    var emailAddressResult = EmailAddress.Create(email);
    if (emailAddressResult.IsFailure)
        return emailAddressResult.Unwrap();

    return Result<Customer>.Success()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName), "Invalid name: first name required")
        .Ensure(_ => !string.IsNullOrWhiteSpace(lastName), "Invalid name: last name required")
        .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
        .Tap(e => e.DomainEvents.Register(new CustomerCreatedDomainEvent(e)));
}
```

### Application Handler Example: Command Pipeline

```csharp
protected override async Task<Result<CustomerModel>> HandleAsync(
    CustomerCreateCommand request,
    SendOptions options,
    CancellationToken cancellationToken) =>
        await Result<CustomerModel>
            .Bind(() => new Context(request.Model))
            .Ensure(ctx => ctx.Model.FirstName != ctx.Model.LastName, "First and last name cannot be same")
            .UnlessAsync(ValidateBusinessRules, cancellationToken)
            .BindResultAsync(GenerateSequenceNumber, CaptureNumber, cancellationToken)
            .Bind(CreateEntity)
            .BindResultAsync(PersistEntity, CaptureEntity, cancellationToken)
            .Log(logger, "Customer {Id} created", r => [r.Value.Entity.Id])
            .Map(ToModel);
```

### When to Use Results vs Exceptions

- **Use Result<T> for**:
  - Validation failures
  - Business rule violations
  - Not found scenarios
  - Concurrency conflicts
  - Any expected failure scenario

- **Use Exceptions for**:
  - Database connection failures
  - Configuration errors
  - System-level failures
  - Programming bugs (null references, index out of range)
  - Truly exceptional circumstances

### Testing with Results

```csharp
[Fact]
public async Task CreateCustomer_WithInvalidEmail_ReturnsFailure()
{
    var command = new CustomerCreateCommand(
        new CustomerModel { FirstName = "John", LastName = "Doe", Email = "invalid" });

    var result = await requester.SendAsync(command);

    result.ShouldBeFailure();
    result.Errors.Should().Contain(e => e.Message.Contains("email"));
}
```

### Result Operators Quick Reference

| Operator | Purpose | Example |
|----------|---------|---------|
| `Bind()` | Transform value | `.Bind(x => ProcessValue(x))` |
| `BindAsync()` | Async transform | `.BindAsync(x => ProcessAsync(x))` |
| `BindResult()` | Chain Results | `.BindResult(CreateEntity)` |
| `Ensure()` | Inline validation | `.Ensure(x => x > 0, "Must be positive")` |
| `Unless()` | Business rules | `.Unless(rule.Check())` |
| `Map()` | Convert type | `.Map(entity => ToModel(entity))` |
| `Tap()` | Side effects | `.Tap(x => logger.LogInfo(x))` |
| `Log()` | Structured logging | `.Log(logger, "Created {Id}", r => r.Id)` |

### Implementation Files

- **Result abstraction**: bITdevKit `Result<T>` type
- **Domain usage**: `src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Customer.cs`
- **Application usage**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommandHandler.cs`
- **Test assertions**: `tests/Modules/CoreModule/CoreModule.UnitTests/Application/Commands/CustomerCreateCommandHandlerTests.cs`
