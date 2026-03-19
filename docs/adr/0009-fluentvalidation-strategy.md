# ADR-0009: FluentValidation Strategy

## Status

Accepted

## Context

The application requires input validation for commands and queries to ensure data integrity and provide early failure detection. Two distinct validation scenarios exist:

### Technical Requirements

- **Input Validation**: Validate command/query DTOs before handler execution (format, length, nullability, business rules)
- **Domain Validation**: Enforce invariants within domain entities (aggregate consistency, value object constraints)
- **Pipeline Integration**: Automatically validate inputs without manual validation code in handlers
- **Error Messages**: Provide clear, actionable error messages to API consumers
- **Performance**: Validation must execute efficiently (minimal overhead)
- **Testability**: Validation rules should be unit-testable in isolation

### Business Requirements

- **Fail Fast**: Reject invalid requests before database access or expensive operations
- **User Experience**: Return specific validation errors (not generic "bad request")
- **Consistency**: All commands/queries follow same validation pattern
- **Compliance**: Enforce field length limits matching database schema constraints

### Design Challenges

- **Validation Location**: Where should validation occur? (Controller, Handler, Domain, Pipeline)
- **Duplication**: Input validation vs domain validation may check similar rules (e.g., "email required")
- **Nested Objects**: Commands contain nested collections (e.g., Customer with Addresses)
- **Framework Choice**: FluentValidation vs DataAnnotations vs manual validation

### Related Decisions

- **ADR-0005**: Requester/Notifier Mediator Pattern - Commands pass through validation pipeline behavior
- **ADR-0011**: Application Logic in Commands/Queries - Handlers assume inputs are pre-validated
- **ADR-0012**: Domain Logic in Domain Layer - Domain enforces invariants separately from input validation

## Decision

Use **FluentValidation** for **input validation** in the Application layer, integrated via **ValidationPipelineBehavior**, with validators nested inside command/query classes.

### How It Works

#### 1. Validator Nested Inside Command/Query

Each command/query contains nested `Validator` class inheriting from `AbstractValidator<T>`:

```csharp
// src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommand.cs (lines 13-67)
public class CustomerCreateCommand(CustomerModel model) : RequestBase<CustomerModel>
{
    public CustomerModel Model { get; set; } = model;

    /// <summary>Validation rules for <see cref="CustomerCreateCommand"/> using FluentValidation.</summary>
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

            // Nested collection validation
            this.RuleFor(c => c.Model.Addresses)
                .Must(addresses => addresses == null || addresses.Count(a => a.IsPrimary) <= 1)
                .WithMessage("Only one address can be marked as primary");

            this.RuleForEach(c => c.Model.Addresses).ChildRules(address =>
            {
                address.RuleFor(a => a.Line1)
                    .NotEmpty().WithMessage("Address line 1 is required")
                    .MaximumLength(256).WithMessage("Address line 1 must not exceed 256 characters");

                address.RuleFor(a => a.City)
                    .NotEmpty().WithMessage("City is required")
                    .MaximumLength(100).WithMessage("City must not exceed 100 characters");

                address.RuleFor(a => a.Country)
                    .NotEmpty().WithMessage("Country is required")
                    .MaximumLength(100).WithMessage("Country must not exceed 100 characters");
            });
        }
    }
}
```

#### 2. Update Command Example

Similar pattern for update commands with different ID validation:

```csharp
// src/Modules/CoreModule/CoreModule.Application/Commands/CustomerUpdateCommand.cs (lines 13-67)
public class CustomerUpdateCommand(CustomerModel model) : RequestBase<CustomerModel>
{
    public CustomerModel Model { get; set; } = model;

    public class Validator : AbstractValidator<CustomerUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();

            this.RuleFor(c => c.Model.Id).MustNotBeDefaultOrEmptyGuid() // ← Different from Create
                .WithMessage("Invalid guid.");

            this.RuleFor(c => c.Model.FirstName)
                .NotNull().NotEmpty().WithMessage("Must not be empty.");

            // ... rest of validation rules
        }
    }
}
```

#### 3. Validation Pipeline Behavior

Automatically validates all commands/queries before handler execution:

```csharp
// src/Presentation.Web.Server/ProgramExtensions.cs (lines 33-41)
public static RequesterBuilder WithDefaultBehaviors(this RequesterBuilder builder)
{
    return builder
        .WithBehavior(typeof(TracingBehavior<,>))
        .WithBehavior(typeof(ModuleScopeBehavior<,>))
        .WithBehavior(typeof(ValidationPipelineBehavior<,>))  // ← Validates before handler
        .WithBehavior(typeof(RetryPipelineBehavior<,>))
        .WithBehavior(typeof(TimeoutPipelineBehavior<,>));
}
```

**Pipeline Execution Order**:

1. `TracingBehavior` - Start span
2. `ModuleScopeBehavior` - Set module context
3. **`ValidationPipelineBehavior`** - Validate input (fails fast if invalid)
4. `RetryPipelineBehavior` - Retry transient failures
5. `TimeoutPipelineBehavior` - Enforce timeout
6. **Handler** - Execute business logic (assumes valid input)

#### 4. Domain Validation (Separate from Input Validation)

Domain entities enforce invariants independently:

```csharp
// Domain layer validation (in Customer.cs factory method)
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    return Result<Customer>.Success()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName), "First name is required")
        .Ensure(_ => !string.IsNullOrWhiteSpace(lastName), "Last name is required")
        .Ensure(_ => email is not null, "Email is required")
        .Bind(_ => new Customer(firstName, lastName, email, number));
}

// Value object validation (in EmailAddress.cs)
public static Result<EmailAddress> Create(string value)
{
    return Result<EmailAddress>.Success()
        .Ensure(_ => !string.IsNullOrWhiteSpace(value), "Email is required")
        .Ensure(_ => value.Contains('@'), "Email must contain @")
        .Bind(_ => new EmailAddress(value));
}
```

### Validation Layers Distinction

| Concern | Input Validation (FluentValidation) | Domain Validation (Result Pattern) |
|---------|-------------------------------------|-----------------------------------|
| **Location** | Application layer (Commands/Queries) | Domain layer (Entities/Value Objects) |
| **Purpose** | Validate external inputs (DTOs) | Enforce domain invariants |
| **When** | Before handler execution (pipeline) | During entity creation/modification |
| **What** | Format, length, nullability, business rules | Aggregate consistency, value object constraints |
| **Errors** | `ValidationError` with field names | `Error` / `ValidationError` with domain messages |
| **Example** | "FirstName must not be empty" | "Customer must have at least one address" |

### Custom Validation Extensions

Project uses custom validators for common patterns:

```csharp
// Custom FluentValidation extension methods (from bITdevKit)
this.RuleFor(c => c.Model.Id).MustBeDefaultOrEmptyGuid()         // For Create commands
this.RuleFor(c => c.Model.Id).MustNotBeDefaultOrEmptyGuid()      // For Update commands
```

## Rationale

### Why FluentValidation Over DataAnnotations?

1. **Expressiveness**: Fluent API is more readable than attributes (`RuleFor(x => x.Email).NotEmpty()` vs `[Required]`)
2. **Testability**: Validators are classes that can be unit tested independently
3. **Complex Rules**: Supports conditional validation, cross-property validation, nested collections
4. **Separation of Concerns**: Validation logic separated from DTO classes (not polluted with attributes)
5. **Custom Validators**: Easy to create reusable custom validation rules
6. **Error Messages**: Better control over error message formatting and localization
7. **Async Support**: Supports async validation rules (e.g., database lookups)

### Why Nested Validators?

1. **Discoverability**: Validators live next to commands/queries (easy to find)
2. **Co-Location**: Related code stays together (command + validation)
3. **Convention**: Consistent pattern across all commands/queries
4. **Namespace Clarity**: No need for separate `Validators` folder/namespace

### Why Pipeline Behavior?

1. **Automatic Validation**: No manual validation code in handlers (DRY principle)
2. **Fail Fast**: Invalid requests rejected before handler execution (no wasted work)
3. **Consistent Error Format**: All validation errors returned in same format
4. **Separation of Concerns**: Handlers focus on business logic, not input checking

### Why Separate Domain Validation?

1. **Domain Purity**: Domain layer doesn't depend on FluentValidation library
2. **Rich Domain Model**: Entities enforce their own invariants regardless of how they're created
3. **Defensive Programming**: Domain protects itself even if Application layer validation is bypassed
4. **Testing**: Domain validation testable without FluentValidation infrastructure

## Consequences

### Positive

- **Developer Productivity**: No manual validation code in handlers; validators auto-discovered
- **Fail Fast**: Invalid requests rejected before expensive operations (database, external APIs)
- **Testability**: Validators unit-testable in isolation; handlers can assume valid inputs
- **Consistency**: All commands/queries follow same validation pattern
- **Maintainability**: Validation rules co-located with commands/queries (easy to update)
- **Error Quality**: Specific field-level errors returned to API consumers (not generic "bad request")
- **Separation of Concerns**: Handlers focus on orchestration, not input checking

### Negative

- **Duplication Risk**: Input validation and domain validation may check similar rules (e.g., "email required")
- **Two Validation Layers**: Developers must understand difference between input validation (FluentValidation) and domain validation (Result pattern)
- **Learning Curve**: Developers must learn FluentValidation API (RuleFor, WithMessage, etc.)
- **Nested Class Convention**: Some developers prefer separate validator files
- **Async Validators**: Async validation rules (e.g., database lookups) can slow down request processing

### Neutral

- **Library Dependency**: Requires FluentValidation NuGet package (widely used, stable)
- **Validation Errors Format**: FluentValidation returns specific format; may need custom mapper for API responses
- **Performance**: Validation adds minimal overhead (~1ms for typical validators)

## Alternatives Considered

### 1. DataAnnotations (Attributes)

**Description**: Use `[Required]`, `[MaxLength]`, `[EmailAddress]` attributes on DTO properties.

**Example**:

```csharp
public class CustomerCreateCommand
{
    [Required]
    [MaxLength(128)]
    public string FirstName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
```

**Pros**:

- Simple: Built into .NET framework
- No additional library required
- ASP.NET Core automatically validates attributes

**Cons**:

- **Limited Expressiveness**: Hard to express complex rules (conditional validation, cross-property)
- **Not Testable**: Can't unit test validators in isolation
- **Pollutes DTOs**: Validation attributes clutter DTO classes
- **Nested Objects**: Poor support for validating nested collections
- **Custom Validators**: Hard to create reusable custom validators

**Rejected Because**: Too limited for complex validation scenarios; hard to test and maintain.

### 2. Manual Validation in Handlers

**Description**: Write validation code directly in command/query handlers.

**Example**:

```csharp
public async Task<Result<CustomerModel>> Handle(CustomerCreateCommand request)
{
    // Manual validation
    if (string.IsNullOrWhiteSpace(request.Model.FirstName))
        return Result<CustomerModel>.Failure("First name is required");

    if (string.IsNullOrWhiteSpace(request.Model.Email))
        return Result<CustomerModel>.Failure("Email is required");

    // ... business logic
}
```

**Pros**:

- Full control over validation logic
- No additional library required

**Cons**:

- **Massive Boilerplate**: Every handler must validate inputs manually
- **Inconsistency**: Different handlers validate differently
- **Hard to Test**: Validation logic mixed with business logic
- **Not Reusable**: Can't share validation rules across handlers
- **Fail Slow**: Validation happens after pipeline behaviors (timeout, retry)

**Rejected Because**: Massive code duplication and maintenance burden; violates DRY principle.

### 3. Separate Validator Classes (Not Nested)

**Description**: Create separate validator classes in dedicated folder.

**Example**:

```csharp
// Validators/CustomerCreateCommandValidator.cs
public class CustomerCreateCommandValidator : AbstractValidator<CustomerCreateCommand>
{
    public CustomerCreateCommandValidator() { /* rules */ }
}

// Commands/CustomerCreateCommand.cs
public class CustomerCreateCommand : RequestBase<CustomerModel>
{
    public CustomerModel Model { get; set; }
}
```

**Pros**:

- Clear separation: validators in own folder
- Traditional FluentValidation pattern

**Cons**:

- **Discoverability**: Hard to find validator for specific command (different file)
- **Namespace Clutter**: Requires separate `Validators` namespace/folder
- **More Files**: 2 files per command instead of 1

**Rejected Because**: Nested validators provide better discoverability with no downside.

### 4. Only Domain Validation (No Input Validation)

**Description**: Skip FluentValidation; rely entirely on domain validation.

**Pros**:

- Single validation layer (simpler)
- Domain enforces all rules

**Cons**:

- **Late Failure**: Errors discovered after pipeline behaviors execute (timeout, retry wasted)
- **Poor Error Messages**: Domain errors may not map cleanly to API field errors
- **Expensive Validation**: Domain validation may require database access (not suitable for early validation)

**Rejected Because**: Fail-fast principle requires early input validation before expensive operations.

## Related Decisions

- **ADR-0002**: Result Pattern - Domain validation returns `Result<T>` instead of throwing exceptions
- **ADR-0005**: Requester/Notifier Mediator Pattern - ValidationPipelineBehavior integrates with mediator pipeline
- **ADR-0011**: Application Logic in Commands/Queries - Handlers assume inputs are validated by pipeline
- **ADR-0012**: Domain Logic in Domain Layer - Domain enforces invariants independently of input validation

## References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [bITdevKit Requester/Notifier Behaviors](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-requester-notifier.md#part-3-pipeline-behaviors)
- Project Documentation: `README.md` (Application Layer section)
- Module Documentation: `src/Modules/CoreModule/CoreModule-README.md` (Commands/Queries)

## Notes

### Key Implementation Files

```
src/Modules/CoreModule/CoreModule.Application/
├── Commands/
│   ├── CustomerCreateCommand.cs           # Lines 18-67: Validator class
│   ├── CustomerUpdateCommand.cs           # Lines 18-67: Validator class
│   ├── CustomerDeleteCommand.cs           # Validator class
│   └── CustomerUpdateStatusCommand.cs     # Validator class
├── Queries/
│   └── CustomerFindOneQuery.cs            # Validator class
└── CoreModuleConfiguration.cs             # Lines 59+: Configuration validator

src/Presentation.Web.Server/
└── ProgramExtensions.cs                   # Lines 33-41: Pipeline behavior registration
```

### Common Validation Rules

**Null/Empty Checks**:

```csharp
this.RuleFor(c => c.Model.FirstName)
    .NotNull().NotEmpty().WithMessage("Must not be empty.");
```

**Length Constraints** (match database schema):

```csharp
this.RuleFor(c => c.Model.FirstName)
    .MaximumLength(128).WithMessage("Must not exceed 128 characters");
```

**Guid Validation** (custom extensions):

```csharp
this.RuleFor(c => c.Model.Id).MustBeDefaultOrEmptyGuid()      // For Create
this.RuleFor(c => c.Model.Id).MustNotBeDefaultOrEmptyGuid()   // For Update
```

**Nested Collection Validation**:

```csharp
// Collection-level rule
this.RuleFor(c => c.Model.Addresses)
    .Must(addresses => addresses == null || addresses.Count(a => a.IsPrimary) <= 1)
    .WithMessage("Only one address can be marked as primary");

// Item-level rules
this.RuleForEach(c => c.Model.Addresses).ChildRules(address =>
{
    address.RuleFor(a => a.Line1).NotEmpty();
    address.RuleFor(a => a.City).NotEmpty().MaximumLength(100);
});
```

**Conditional Validation**:

```csharp
this.RuleFor(c => c.Model.DateOfBirth)
    .LessThan(DateTime.Today)
    .When(c => c.Model.DateOfBirth.HasValue)
    .WithMessage("Date of birth must be in the past");
```

### Testing Validators

**Unit Test Example**:

```csharp
[Fact]
public void Validator_ShouldFail_WhenFirstNameEmpty()
{
    // Arrange
    var command = new CustomerCreateCommand(new CustomerModel { FirstName = "" });
    var validator = new CustomerCreateCommand.Validator();

    // Act
    var result = validator.Validate(command);

    // Assert
    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.PropertyName == "Model.FirstName");
}
```

### Validation vs Domain Validation Decision Tree

**Use FluentValidation (Input Validation) For**:

- Format validation (email format, phone format)
- Length constraints (matching database schema)
- Required fields (null/empty checks)
- Cross-field validation (password confirmation)
- Collection validation (nested objects)
- Business rules that don't require domain knowledge

**Use Result Pattern (Domain Validation) For**:

- Aggregate consistency ("Customer must have at least one address")
- Value object constraints ("Email must be valid email address")
- Business invariants ("Order total must match line items")
- State transitions ("Can only cancel pending orders")
- Complex business rules ("Premium customers get 20% discount")

### Error Response Format

**FluentValidation Errors**:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "Model.FirstName": ["Must not be empty."],
    "Model.Email": ["Must not be empty."]
  }
}
```

**Domain Validation Errors**:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Domain validation failed",
  "status": 400,
  "detail": "Customer must have at least one primary address"
}
```

### Pipeline Behavior Order Rationale

1. **Tracing** first - captures entire request span
2. **ModuleScope** - sets context for validation/handler
3. **Validation** - fails fast before expensive operations
4. **Retry** - only retry after validation passes
5. **Timeout** - only timeout after validation passes
6. **Handler** - executes with guaranteed valid input

### Common Pitfalls

**X Don't validate in handler**:

```csharp
public async Task<Result<CustomerModel>> Handle(CustomerCreateCommand request)
{
    if (string.IsNullOrWhiteSpace(request.Model.FirstName))  // X Should be in Validator
        return Result<CustomerModel>.Failure("First name is required");
}
```

**V Do validate in nested Validator**:

```csharp
public class CustomerCreateCommand : RequestBase<CustomerModel>
{
    public class Validator : AbstractValidator<CustomerCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model.FirstName).NotEmpty();  // V Correct
        }
    }
}
```

**X Don't duplicate domain validation in FluentValidation**:

```csharp
// X Duplicates domain logic
this.RuleFor(c => c.Model.Email)
    .Must(email => email.Contains('@'))  // X Domain already validates this
    .WithMessage("Email must contain @");
```

**V Do validate format/structure only**:

```csharp
// V Input validation only
this.RuleFor(c => c.Model.Email)
    .NotEmpty().WithMessage("Email is required");

// V Let domain validate semantics
var emailResult = EmailAddress.Create(request.Model.Email); // Domain validates format
```

### Async Validation Considerations

FluentValidation supports async rules, but use sparingly:

```csharp
// WARNING Use with caution (database lookup in validation)
this.RuleFor(c => c.Model.Email)
    .MustAsync(async (email, ct) =>
    {
        var exists = await repository.ExistsAsync(e => e.Email == email, ct);
        return !exists; // Email must be unique
    })
    .WithMessage("Email already exists");
```

**Better approach**: Check uniqueness in handler (after cheap validations pass):

```csharp
// V Check uniqueness in handler
var existingCustomer = await repository.FindOneAsync(c => c.Email == email);
if (existingCustomer != null)
    return Result<CustomerModel>.Failure("Email already exists");
```

### Future Considerations

- **Localization**: Add localized error messages via FluentValidation's localization support
- **Conditional Validators**: Add validators that apply only in specific contexts (e.g., admin vs user)
- **Cross-Module Validation**: Share common validators across modules via shared library
- **Validation Caching**: Cache validator instances for better performance
