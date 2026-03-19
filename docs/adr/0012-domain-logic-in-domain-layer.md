# ADR-0012: Domain Logic in Domain Layer

## Status

Accepted

## Context

In Domain-Driven Design, the domain layer is the heart of the application. However, there's often confusion about what belongs in the domain versus application layers:

**Common Mistakes**:

- **Anemic Domain Models**: Entities with only getters/setters, all logic in services
- **Bloated Domain**: Entities with infrastructure concerns (repositories, external APIs)
- **Unclear Boundaries**: Business rules scattered between domain and application
- **Testing Difficulties**: Cannot test business rules independently

**Requirements**:

1. Domain must enforce **invariants** (rules that must always be true)
2. Domain must encapsulate **business rules** (domain-specific constraints)
3. Domain must remain **framework-agnostic** and **persistence-ignorant**
4. Domain must be **testable** without infrastructure
5. Domain must **communicate intent** through rich domain model

## Decision

**Domain logic (invariants and business rules) belongs in the Domain layer** within Aggregates, Value Objects, and Business Rules.

### Domain Logic Includes:

**Invariants** (must always be true):

- Value Object validation (e.g., email format, non-negative prices)
- Aggregate consistency rules (e.g., order must have at least one item)
- State transition rules (e.g., can't retire an already retired customer)

**Business Rules**:

- Domain-specific constraints (e.g., email must be unique)
- Calculation logic (e.g., order total calculation)
- Domain policies (e.g., discount eligibility)

**Domain Behavior**:

- Aggregate factory methods (e.g., `Customer.Create()`)
- State change methods (e.g., `customer.ChangeEmail()`)
- Domain event registration

**Domain Events**:

- Significant business occurrences (e.g., `CustomerCreatedDomainEvent`)
- Event registration when state changes

## Rationale

1. **Single Source of Truth**: Business rules live in one place (domain layer)
2. **Encapsulation**: Aggregates protect invariants, enforce rules
3. **Testability**: Domain logic testable without infrastructure
4. **Ubiquitous Language**: Domain model reflects business terminology
5. **Reusability**: Domain logic reused across different use cases
6. **Framework Independence**: Domain doesn't depend on ASP.NET, EF Core, or any framework
7. **Clear Intent**: Domain methods express business operations, not technical CRUD

## Consequences

### Positive

- Business rules enforced consistently (cannot be bypassed)
- Domain logic testable independently of infrastructure
- Rich domain model that expresses business concepts
- Aggregates maintain invariants automatically
- Domain can be reused in different application contexts
- Clear separation between business logic and infrastructure
- Domain code is easy to understand (reflects business language)

### Negative

- Requires discipline to keep domain pure (no infrastructure dependencies)
- Learning curve for developers unfamiliar with DDD
- More complex than anemic domain models (getters/setters only)
- May feel over-engineered for simple CRUD scenarios

### Neutral

- Domain methods return `Result<T>` for operations that can fail
- Factory methods preferred over public constructors
- Value Objects are immutable
- Private setters enforce encapsulation

## Alternatives Considered

- **Alternative 1: Anemic Domain Model (All Logic in Services)**
  - Rejected because it leads to procedural programming, not object-oriented
  - Business rules scattered across multiple services
  - Entities become simple data containers with no behavior

- **Alternative 2: Active Record Pattern**
  - Rejected because it couples domain to persistence infrastructure
  - Entities would have `Save()`, `Delete()` methods (infrastructure concern)
  - Violates Clean Architecture dependency rules

- **Alternative 3: Transaction Script Pattern**
  - Rejected because it doesn't scale well with complexity
  - No reusable domain model
  - Business logic scattered across procedural scripts

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Domain layer has no outward dependencies
- [ADR-0002](0002-result-pattern-error-handling.md): Domain methods return Results
- [ADR-0011](0011-application-logic-in-commands-queries.md): Complementary decision defining application responsibilities

## References

- [bITdevKit Domain Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-domain.md)
- [bITdevKit Domain Events](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-domain-events.md)
- [README - Domain Layer](../../README.md#domain-layer-core)
- [CoreModule README - Key Building Blocks](../../src/Modules/CoreModule/CoreModule-README.md#key-building-blocks)
- [Eric Evans - Domain-Driven Design](https://www.domainlanguage.com/ddd/)
- [Martin Fowler - Anemic Domain Model](https://martinfowler.com/bliki/AnemicDomainModel.html)

## Notes

### Aggregate Root Example

```csharp
[TypedEntityId<Guid>]
public class Customer : AuditableAggregateRoot<CustomerId>, IConcurrency
{
    private readonly List<Address> addresses = [];

    private Customer() { } // EF Core constructor

    // Private constructor enforces factory method usage
    private Customer(string firstName, string lastName, EmailAddress email, CustomerNumber number)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
        this.Number = number;
    }

    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public EmailAddress Email { get; private set; }
    public CustomerNumber Number { get; private set; }
    public CustomerStatus Status { get; private set; } = CustomerStatus.Lead;
    public IReadOnlyCollection<Address> Addresses => this.addresses.AsReadOnly();

    // DOMAIN LOGIC: Factory method with invariants
    public static Result<Customer> Create(
        string firstName,
        string lastName,
        string email,
        CustomerNumber number)
    {
        var emailAddressResult = EmailAddress.Create(email);
        if (emailAddressResult.IsFailure)
            return emailAddressResult.Unwrap();

        return Result<Customer>.Success()
            // Invariant: Names must not be empty
            .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
                new ValidationError("Invalid name: both first and last name must be provided"))
            // Invariant: Last name cannot be "notallowed"
            .Ensure(_ => lastName != "notallowed",
                new ValidationError("Invalid last name: 'notallowed' is not permitted"))
            .Ensure(_ => email != null, new ValidationError("Email cannot be null"))
            .Ensure(_ => number != null, new ValidationError("Number cannot be null"))
            .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
            // Register domain event
            .Tap(e => e.DomainEvents.Register(new CustomerCreatedDomainEvent(e)));
    }

    // DOMAIN LOGIC: State change method with invariants
    public Result<Customer> ChangeName(string firstName, string lastName)
    {
        return this.Change()
            .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
                "Invalid name: both first and last name must be provided")
            .Ensure(_ => lastName != "notallowed",
                "Invalid last name: 'notallowed' is not permitted")
            .Set(e => e.FirstName, firstName)
            .Set(e => e.LastName, lastName)
            .Register(e => new CustomerUpdatedDomainEvent(e))
            .Apply();
    }

    // DOMAIN LOGIC: Email change with validation
    public Result<Customer> ChangeEmail(string email)
    {
        return this.Change()
            .Set(e => e.Email, EmailAddress.Create(email))
            .Register(e => new CustomerUpdatedDomainEvent(e))
            .Apply();
    }
}
```

### Value Object Example

Value Objects enforce invariants through validation:

```csharp
public class EmailAddress : ValueObject
{
    private EmailAddress(string value)
    {
        this.Value = value;
    }

    public string Value { get; private set; }

    // DOMAIN LOGIC: Validation and creation
    public static Result<EmailAddress> Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant();

        // Invariant: Email cannot be empty
        if (string.IsNullOrWhiteSpace(value))
            return Result<EmailAddress>.Failure()
                .WithError(new ValidationError("Email cannot be empty"));

        // Invariant: Email must contain @
        if (!value.Contains("@"))
            return Result<EmailAddress>.Failure()
                .WithError(new ValidationError("Invalid email format"));

        return new EmailAddress(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}
```

### Business Rule Example

Complex business rules can be extracted to rule classes:

```csharp
public class EmailShouldBeUniqueRule(string email, IGenericRepository<Customer> repository)
    : IBusinessRule
{
    public string Message => "Email address already exists";

    public async Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        var specification = new Specification<Customer>(e =>
            e.Email.Value.ToLowerInvariant() == email.ToLowerInvariant());

        var result = await repository.FindAllResultAsync(
            specification,
            cancellationToken: cancellationToken);

        return result.IsSuccess && !result.Value.Any();
    }
}
```

### Enumeration Example

Enumerations provide type-safe bounded sets:

```csharp
public class CustomerStatus : Enumeration
{
    public static readonly CustomerStatus Lead = new(1, nameof(Lead), "Lead customer");
    public static readonly CustomerStatus Active = new(2, nameof(Active), "Active customer");
    public static readonly CustomerStatus Retired = new(3, nameof(Retired), "Retired customer");

    private CustomerStatus(int id, string name, string description = null)
        : base(id, name)
    {
        this.Description = description;
    }

    public string Description { get; private set; }
}
```

### Domain vs Application Responsibility Matrix

| Concern | Layer | Example |
|---------|-------|---------|
| Email format validation | Domain | `EmailAddress.Create(email)` |
| Email uniqueness check | Domain (rule definition) | `EmailShouldBeUniqueRule` |
| Email uniqueness execution | Application | `Rule.Add(new EmailShouldBeUniqueRule(...)).CheckAsync()` |
| Customer name validation | Domain | `Customer.Create()` invariants |
| Customer number generation | Application | `numberGenerator.NextAsync()` |
| Customer persistence | Application | `repository.InsertResultAsync()` |
| Aggregate creation | Domain | `Customer.Create()` |
| State transitions | Domain | `customer.ChangeName()` |
| Domain event registration | Domain | `DomainEvents.Register(...)` |
| Transaction coordination | Application | Handler orchestration |

### Testing Domain Logic

**Unit Tests** (no infrastructure needed):

```csharp
[Fact]
public void Create_WithValidData_ReturnsSuccess()
{
    var number = CustomerNumber.Create(2025, 100000).Value;
    var result = Customer.Create("John", "Doe", "john@example.com", number);

    result.ShouldBeSuccess();
    result.Value.FirstName.ShouldBe("John");
    result.Value.Email.Value.ShouldBe("john@example.com");
}

[Fact]
public void Create_WithInvalidEmail_ReturnsFailure()
{
    var number = CustomerNumber.Create(2025, 100000).Value;
    var result = Customer.Create("John", "Doe", "invalid-email", number);

    result.ShouldBeFailure();
    result.Errors.Should().Contain(e => e.Message.Contains("email"));
}

[Fact]
public void ChangeName_WithNotAllowedLastName_ReturnsFailure()
{
    var customer = CreateValidCustomer();
    var result = customer.ChangeName("John", "notallowed");

    result.ShouldBeFailure();
    result.Errors.Should().Contain(e => e.Message.Contains("notallowed"));
}
```

### Domain Patterns Checklist

When designing domain logic:

1. V Use static factory methods (e.g., `Create()`) instead of public constructors
2. V Return `Result<T>` from methods that can fail
3. V Use private setters to enforce encapsulation
4. V Register domain events for significant state changes
5. V Validate invariants in factory methods and state change methods
6. V Use Value Objects for concepts with validation rules
7. V Use Enumerations for bounded sets
8. V Extract complex rules to `IBusinessRule` implementations
9. V Keep domain pure (no infrastructure dependencies)
10. V Express business concepts through ubiquitous language

### What NOT to Put in Domain

X **Infrastructure Concerns**:

- Database queries (`DbContext`, `IQueryable`)
- HTTP calls to external services
- File system access
- Email sending

X **Application Concerns**:

- Mapping to DTOs
- Transaction management
- Command/Query orchestration
- Caching

X **Presentation Concerns**:

- HTTP status codes
- JSON serialization attributes
- View models

### Implementation Files

- **Aggregate**: `src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Customer.cs`
- **Value Object**: `src/Modules/CoreModule/CoreModule.Domain/Model/EmailAddress.cs`
- **Enumeration**: `src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/CustomerStatus.cs`
- **Business Rule**: `src/Modules/CoreModule/CoreModule.Domain/Rules/EmailShouldBeUniqueRule.cs`
- **Domain Event**: `src/Modules/CoreModule/CoreModule.Domain/Events/CustomerCreatedDomainEvent.cs`
- **Tests**: `tests/Modules/CoreModule/CoreModule.UnitTests/Domain/`
