# Checklist: Domain Patterns (DDD)

This checklist helps verify that Domain-Driven Design patterns are correctly implemented in the domain layer.

## Aggregate Root Validation (🔴 CRITICAL)

**ADR-0012 (Domain Logic Encapsulation in Domain Layer)**: Aggregates are the primary pattern for enforcing business invariants and encapsulating business logic. Proper encapsulation (private setters, factory methods, change methods) ensures that business rules cannot be bypassed and that the domain model remains consistent. Violating encapsulation leads to anemic domain models where business logic leaks into application or presentation layers.

### Encapsulation Requirements

- [ ] All properties have **private setters**
- [ ] No public parameterless constructor (use private constructor + factory method)
- [ ] Factory method (e.g., `Create()`) returns `Result<TAggregate>`
- [ ] Factory method performs validation before creating instance
- [ ] Change methods (e.g., `ChangeName()`, `ChangeEmail()`) return `Result<TAggregate>`
- [ ] Change methods validate business rules before modifying state
- [ ] Collection properties expose `IReadOnlyCollection<T>`, not `List<T>` or `ICollection<T>`
- [ ] Private backing fields for collections (e.g., `private readonly List<Address> addresses = []`)
- [ ] No setters on navigation properties

### Factory Method Pattern

```csharp
// ✅ CORRECT: Factory method with validation
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
        .Tap(e => e.DomainEvents
            .Register(new CustomerCreatedDomainEvent(e))
            .Register(new EntityCreatedDomainEvent<Customer>(e)));
}
```

**Reference**: ADR-0012, ADR-0002 (Result Pattern)

### Change Method Pattern

```csharp
// ✅ CORRECT: Change method with validation
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
```

**Reference**: ADR-0012

### Common Violations

```csharp
// ❌ WRONG: Public setters (anemic domain model)
public class Customer : AggregateRoot<CustomerId>
{
    public string FirstName { get; set; } // ❌ Public setter
    public string LastName { get; set; } // ❌ Public setter
    public EmailAddress Email { get; set; } // ❌ Public setter
}
```

```csharp
// ❌ WRONG: Business logic in setter
public class Customer : AggregateRoot<CustomerId>
{
    private string lastName;

    public string LastName
    {
        get => this.lastName;
        set
        {
            if (value == "notallowed") // ❌ Business logic in setter
            {
                throw new ValidationException("Invalid last name");
            }
            this.lastName = value;
        }
    }
}
```

```csharp
// ❌ WRONG: Collection exposes mutable List
public class Customer : AggregateRoot<CustomerId>
{
    public List<Address> Addresses { get; set; } // ❌ Mutable collection
}
```

**Reference**: See [examples/aggregate-patterns.md](../examples/aggregate-patterns.md) for full WRONG vs CORRECT examples.

## Entity Validation

**ADR-0012**: Entities (non-aggregate roots) follow similar encapsulation rules as aggregates but are accessed only through their parent aggregate.

### Checklist

- [ ] All properties have private setters
- [ ] Change methods return `Result<TEntity>`
- [ ] No public parameterless constructor
- [ ] Entity has identity (`Id` property)
- [ ] Entity accessed only through aggregate root (not directly via repository)
- [ ] Collection properties expose `IReadOnlyCollection<T>`

### Example

```csharp
// ✅ CORRECT: Entity with encapsulation
public class Address : Entity<AddressId>
{
    private Address() { } // Private constructor

    private Address(string name, string line1, string line2, string postalCode, string city, string country, bool isPrimary)
    {
        this.Name = name;
        this.Line1 = line1;
        this.Line2 = line2;
        this.PostalCode = postalCode;
        this.City = city;
        this.Country = country;
        this.IsPrimary = isPrimary;
    }

    public string Name { get; private set; } // ✅ Private setter
    public string Line1 { get; private set; }
    public string Line2 { get; private set; }
    public string PostalCode { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }
    public bool IsPrimary { get; private set; }

    public static Result<Address> Create(string name, string line1, string line2, string postalCode, string city, string country, bool isPrimary)
    {
        return Result<Address>.Success()
            .Ensure(_ => !string.IsNullOrWhiteSpace(line1), "Address line 1 is required")
            .Ensure(_ => !string.IsNullOrWhiteSpace(city), "City is required")
            .Ensure(_ => !string.IsNullOrWhiteSpace(country), "Country is required")
            .Bind(_ => new Address(name, line1, line2, postalCode, city, country, isPrimary));
    }

    public Result<Address> ChangeName(string name)
    {
        return this.Change()
            .Set(e => e.Name, name)
            .Apply();
    }
}
```

**Reference**: ADR-0012

## Value Object Validation

**ADR-0012**: Value objects represent immutable domain concepts defined by their attributes rather than identity. They must be immutable and validated at creation.

### Checklist

- [ ] **Immutability**: All properties have private setters, no mutating methods
- [ ] **Factory method**: Static `Create()` method returns `Result<TValueObject>`
- [ ] **Validation**: All validation performed in factory method
- [ ] **Descriptive naming**: Use specific names (e.g., `EmailAddress`, not `Email`)
- [ ] **Equality**: Override `GetAtomicValues()` for value-based equality
- [ ] **No external dependencies**: Value objects should be self-contained
- [ ] **Small and focused**: Represent a single domain concept
- [ ] **Implicit operators** (optional): For convenient conversion to/from primitives

### Example

```csharp
// ✅ CORRECT: Value object with immutability and validation
public class EmailAddress : ValueObject
{
    private EmailAddress() { } // Private constructor

    private EmailAddress(string value) => this.Value = value;

    public string Value { get; private set; } // ✅ Private setter (immutable)

    // ✅ Factory method with validation
    public static Result<EmailAddress> Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant();

        var ruleResult = Rule.Add(RuleSet.IsValidEmail(value)).Check();

        if (ruleResult.IsFailure)
        {
            return Result<EmailAddress>.Failure()
                .WithMessages(ruleResult.Messages)
                .WithErrors(ruleResult.Errors);
        }

        return new EmailAddress(value); // Implicitly wrapped in Result.Success
    }

    // ✅ Implicit operator for convenience
    public static implicit operator string(EmailAddress email) => email.Value;

    // ✅ Value-based equality
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}
```

### Common Violations

```csharp
// ❌ WRONG: Primitive obsession (no value object)
public class Customer : AggregateRoot<CustomerId>
{
    public string Email { get; private set; } // ❌ Should be EmailAddress type
}
```

```csharp
// ❌ WRONG: Mutable value object
public class EmailAddress : ValueObject
{
    public string Value { get; set; } // ❌ Public setter violates immutability
}
```

```csharp
// ❌ WRONG: No validation in factory
public class EmailAddress : ValueObject
{
    public static EmailAddress Create(string value)
    {
        return new EmailAddress(value); // ❌ No validation
    }
}
```

**Reference**: See [examples/value-object-patterns.md](../examples/value-object-patterns.md) for full examples.

## Domain Event Validation

**ADR-0006 (Outbox Pattern for Domain Events)**: Domain events represent significant state changes in the domain and enable loose coupling between aggregates. They must be named in past tense to reflect that something has happened and should be published via the aggregate root.

### Checklist

- [ ] **Past tense naming**: `CustomerCreatedDomainEvent`, `OrderPlacedDomainEvent` (not `CustomerCreateDomainEvent`)
- [ ] **Suffix**: Ends with `DomainEvent`
- [ ] **Derives from**: `DomainEvent` or `DomainEventBase`
- [ ] **Record type**: Use `record` for concise immutable events
- [ ] **Minimal data**: Contains only data relevant to the event (IDs, key values)
- [ ] **Published from aggregate**: Events raised via `this.DomainEvents.Register(...)` in aggregate methods
- [ ] **Handled in Application**: Handlers implement `INotificationHandler<TDomainEvent>`

### Example

```csharp
// ✅ CORRECT: Domain event in past tense
public sealed record CustomerCreatedDomainEvent(Customer Customer) : DomainEvent;

public sealed record CustomerUpdatedDomainEvent(Customer Customer) : DomainEvent;

public sealed record CustomerDeletedDomainEvent(CustomerId CustomerId) : DomainEvent;
```

### Aggregate Registration

```csharp
// ✅ CORRECT: Registering domain events in aggregate
public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
{
    return Result<Customer>.Success()
        .Ensure(/* validation rules */)
        .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
        .Tap(e => e.DomainEvents
            .Register(new CustomerCreatedDomainEvent(e)) // ✅ Register domain event
            .Register(new EntityCreatedDomainEvent<Customer>(e)));
}
```

### Common Violations

```csharp
// ❌ WRONG: Present tense naming
public sealed record CustomerCreateDomainEvent(Customer Customer) : DomainEvent; // ❌ Not past tense

// ❌ WRONG: Missing DomainEvent suffix
public sealed record CustomerCreated(Customer Customer) : DomainEvent; // ❌ Missing suffix
```

**Reference**: ADR-0006

## Enumeration Validation

**ADR-0012**: Smart enumerations provide type-safe, extensible alternatives to primitive enums with additional behavior and context.

### Checklist

- [ ] Derives from `Enumeration` base class
- [ ] PascalCase static instances (e.g., `Active`, `Inactive`)
- [ ] Private constructor
- [ ] No mutable state
- [ ] Used for domain concepts, not technical flags

### Example

```csharp
// ✅ CORRECT: Smart enumeration
public sealed class CustomerStatus : Enumeration
{
    public static readonly CustomerStatus Lead = new(1, nameof(Lead));
    public static readonly CustomerStatus Active = new(2, nameof(Active));
    public static readonly CustomerStatus Inactive = new(3, nameof(Inactive));
    public static readonly CustomerStatus Retired = new(4, nameof(Retired));

    private CustomerStatus(int value, string name) : base(value, name)
    {
    }
}
```

### Usage

```csharp
// ✅ Use in domain model
public class Customer : AggregateRoot<CustomerId>
{
    public CustomerStatus Status { get; private set; } = CustomerStatus.Lead;

    public Result<Customer> ChangeStatus(CustomerStatus status)
    {
        return this.Change()
            .When(_ => status != null)
            .Set(e => e.Status, status)
            .Register(e => new CustomerUpdatedDomainEvent(e))
            .Apply();
    }
}
```

**Reference**: ADR-0012

## Strongly-Typed Entity IDs (🟡 IMPORTANT)

**ADR-0008 (Typed Entity IDs using Source Generators)**: Strongly-typed IDs prevent mixing entity IDs of different types (e.g., passing CustomerId where OrderId is expected) and provide compile-time type safety. They use source generators to reduce boilerplate and ensure consistency across the codebase.

### Checklist

- [ ] Uses `[TypedEntityId<Guid>]` attribute (or other type like `int`, `long`)
- [ ] Declared as `readonly partial struct`
- [ ] Named `[Entity]Id` (e.g., `CustomerId`, `OrderId`, `AddressId`)
- [ ] Used consistently: repositories, DTOs, API models, commands, queries
- [ ] Used in logging and error messages

### Example

```csharp
// ✅ CORRECT: Strongly-typed ID with source generator
namespace MyApp.Domain.CustomerAggregate;

[TypedEntityId<Guid>]
public readonly partial struct CustomerId;
```

### Usage

```csharp
// ✅ Use in aggregate
[TypedEntityId<Guid>]
public class Customer : AuditableAggregateRoot<CustomerId>, IConcurrency
{
    // CustomerId is the strongly-typed ID
}

// ✅ Use in commands
public sealed record CustomerDeleteCommand(CustomerId Id) : RequestBase<Result>;

// ✅ Use in repository
public interface ICustomerRepository
{
    Task<Customer> FindByIdAsync(CustomerId id, CancellationToken ct);
}
```

### Benefits

- **Compile-time safety**: Cannot pass `OrderId` where `CustomerId` is expected
- **IDE support**: IntelliSense shows correct type
- **Refactoring**: Safe renames and find-all-references
- **Self-documenting**: Method signature clearly shows `CustomerId`, not ambiguous `Guid`

### Common Violations

```csharp
// ❌ WRONG: Using Guid/int directly
public class Customer : AggregateRoot<Guid> // ❌ Not strongly-typed
{
    // ...
}

public sealed record CustomerDeleteCommand(Guid Id) : RequestBase<Result>; // ❌ Ambiguous
```

**Reference**: ADR-0008

## Summary

**Domain patterns are CRITICAL** for building a rich domain model that encapsulates business logic and enforces invariants. Violations lead to anemic domain models where business logic leaks into application/presentation layers.

**Key takeaways**:
- **Aggregates**: Private setters, factory methods, change methods, `Result<T>` returns
- **Value Objects**: Immutable, factory method with validation, value-based equality
- **Domain Events**: Past tense, registered in aggregates, handled in Application
- **Enumerations**: Smart enumerations with `Enumeration` base class
- **Strongly-Typed IDs**: `[TypedEntityId<Guid>]` for compile-time safety

**ADRs Referenced**:
- **ADR-0002**: Result Pattern for Error Handling
- **ADR-0006**: Outbox Pattern for Domain Events
- **ADR-0008**: Typed Entity IDs using Source Generators
- **ADR-0012**: Domain Logic Encapsulation in Domain Layer
