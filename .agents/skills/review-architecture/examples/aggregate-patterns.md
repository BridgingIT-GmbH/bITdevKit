# Aggregate Patterns: WRONG vs CORRECT

This document shows common aggregate implementation mistakes and their corrections, extracted from actual codebase patterns.

## Pattern 1: Anemic Domain Model (Public Setters)

### ❌ WRONG: Public Setters Violate Encapsulation

```csharp
namespace MyApp.Domain.CustomerAggregate;

// ❌ Anemic domain model - no encapsulation
public class Customer : AggregateRoot<CustomerId>
{
    // ❌ Public setters allow bypassing business rules
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public EmailAddress Email { get; set; }
    public CustomerStatus Status { get; set; }
    public List<Address> Addresses { get; set; } // ❌ Mutable collection exposed

    // ❌ Parameterless public constructor
    public Customer()
    {
    }

    // ❌ Business logic leaks to application layer
}
```

**Why This Is Wrong**:
- Public setters allow external code to bypass validation and business rules
- No control over state changes; anyone can modify properties directly
- Business logic must be duplicated in application/presentation layers
- Violates **ADR-0012** (Domain Logic in Domain Layer)

### ✅ CORRECT: Rich Domain Model with Encapsulation

```csharp
namespace MyApp.Domain.CustomerAggregate;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Represents a customer aggregate root with personal details, email address, and status.
/// </summary>
[DebuggerDisplay("Id={Id}, Name={FirstName} {LastName}, Status={Status}")]
[TypedEntityId<Guid>]
public class Customer : AuditableAggregateRoot<CustomerId>, IConcurrency
{
    private readonly List<Address> addresses = []; // ✅ Private backing field

    private Customer() { } // ✅ Private parameterless constructor

    // ✅ Private constructor for controlled creation
    private Customer(string firstName, string lastName, EmailAddress email, CustomerNumber number)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
        this.Number = number;
    }

    // ✅ Private setters - controlled access
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public CustomerNumber Number { get; private set; }
    public EmailAddress Email { get; private set; }
    public CustomerStatus Status { get; private set; } = CustomerStatus.Lead;

    // ✅ Expose collection as read-only
    public IReadOnlyCollection<Address> Addresses => this.addresses.AsReadOnly();

    public Guid ConcurrencyVersion { get; set; }

    // ✅ Factory method with validation and Result<T>
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

    // ✅ Change methods with validation and Result<T>
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
}
```

**Why This Is Correct** (ADR-0012):
- Private setters prevent external modification
- Factory method (`Create`) enforces validation before creation
- Change methods (`ChangeName`) enforce business rules
- Collection exposed as `IReadOnlyCollection<T>`
- Domain events registered for significant state changes

---

## Pattern 2: Business Logic in Setters

### ❌ WRONG: Validation in Property Setters

```csharp
// ❌ Business logic in setter
public class Customer : AggregateRoot<CustomerId>
{
    private string lastName;

    public string LastName
    {
        get => this.lastName;
        set
        {
            // ❌ Validation in setter (hidden business logic)
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Last name cannot be empty");
            }

            if (value == "notallowed")
            {
                throw new BusinessRuleException("Invalid last name");
            }

            this.lastName = value;
        }
    }
}
```

**Why This Is Wrong**:
- Validation logic hidden in setters, not discoverable
- Throws exceptions for expected failures (violates **ADR-0002**)
- Cannot compose validation results
- Difficult to test

### ✅ CORRECT: Validation in Change Methods

```csharp
// ✅ Validation in explicit change method
public class Customer : AuditableAggregateRoot<CustomerId>
{
    public string LastName { get; private set; }

    // ✅ Explicit change method with Result<T>
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
}
```

**Why This Is Correct** (ADR-0002, ADR-0012):
- Validation explicit and discoverable
- Returns `Result<T>` instead of throwing exceptions
- Enables functional composition
- Domain event registered on successful change

---

## Pattern 3: Factory Method with Result<T>

### ❌ WRONG: Constructor with Exceptions

```csharp
// ❌ Public constructor with exceptions
public class Customer : AggregateRoot<CustomerId>
{
    public Customer(string firstName, string lastName, string email)
    {
        // ❌ Throws exceptions for validation
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name is required");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name is required");
        }

        if (!email.Contains("@"))
        {
            throw new ArgumentException("Invalid email format");
        }

        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
    }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}
```

**Why This Is Wrong**:
- Exceptions for expected failures (validation)
- Cannot compose or chain validation results
- Caller must use try-catch for control flow
- Violates **ADR-0002** (Result Pattern)

### ✅ CORRECT: Factory Method Returning Result<T>

```csharp
// ✅ Factory method with Result<T>
public class Customer : AuditableAggregateRoot<CustomerId>
{
    private Customer() { } // Private constructor

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

    // ✅ Factory method returning Result<Customer>
    public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
    {
        // ✅ Create value object first
        var emailAddressResult = EmailAddress.Create(email);
        if (emailAddressResult.IsFailure)
        {
            return emailAddressResult.Unwrap(); // Propagate failure
        }

        // ✅ Chain validation with .Ensure()
        return Result<Customer>.Success()
            .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
                new ValidationError("Invalid name: both first and last name must be provided"))
            .Ensure(_ => lastName != "notallowed",
                new ValidationError("Invalid last name: 'notallowed' is not permitted"))
            .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
            .Tap(e => e.DomainEvents.Register(new CustomerCreatedDomainEvent(e)));
    }
}
```

**Why This Is Correct** (ADR-0002):
- Returns `Result<Customer>` for composable error handling
- Validation failures explicit in method signature
- Enables chaining with `.Bind()`, `.Ensure()`, `.Tap()`
- Domain event registered on successful creation

---

## Pattern 4: Collection Handling

### ❌ WRONG: Mutable Collection Exposed

```csharp
// ❌ Exposes mutable collection
public class Customer : AggregateRoot<CustomerId>
{
    public List<Address> Addresses { get; set; } // ❌ Direct List access

    // ❌ External code can manipulate collection directly
    // customer.Addresses.Add(new Address(...)); // Bypasses business rules!
}
```

**Why This Is Wrong**:
- External code can add/remove items bypassing business rules
- Cannot enforce invariants on collection changes
- Cannot track changes or raise domain events
- Violates encapsulation

### ✅ CORRECT: Read-Only Collection with Add/Remove Methods

```csharp
// ✅ Encapsulated collection
public class Customer : AuditableAggregateRoot<CustomerId>
{
    private readonly List<Address> addresses = []; // ✅ Private backing field

    // ✅ Expose as IReadOnlyCollection
    public IReadOnlyCollection<Address> Addresses => this.addresses.AsReadOnly();

    // ✅ Controlled add method with validation
    public Result<Customer> AddAddress(
        string name, string line1, string line2,
        string postalCode, string city, string country,
        bool isPrimary = false)
    {
        return this.Change()
            .Add(e => this.addresses, Address.Create(name, line1, line2, postalCode, city, country, isPrimary))
            .Register(e => new CustomerUpdatedDomainEvent(e))
            .Apply();
    }

    // ✅ Controlled remove method
    public Result<Customer> RemoveAddress(AddressId addressId)
    {
        var address = this.addresses.FirstOrDefault(a => a.Id == addressId);
        if (address == null)
        {
            return Result<Customer>.Failure($"Address with ID {addressId} not found");
        }

        this.addresses.Remove(address);

        return this.Change()
            .Register(e => new CustomerUpdatedDomainEvent(e))
            .Apply();
    }
}
```

**Why This Is Correct** (ADR-0012):
- Private backing field prevents direct manipulation
- Read-only collection exposed publicly
- Add/remove methods enforce business rules
- Domain events raised on collection changes

---

## Pattern 5: Domain Events

### ❌ WRONG: No Domain Events

```csharp
// ❌ No domain events for state changes
public class Customer : AggregateRoot<CustomerId>
{
    public static Customer Create(string firstName, string lastName, string email)
    {
        return new Customer(firstName, lastName, email);
        // ❌ No domain event for creation
    }

    public void ChangeName(string firstName, string lastName)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        // ❌ No domain event for update
    }
}
```

**Why This Is Wrong**:
- Cannot react to state changes in other aggregates/modules
- Tight coupling if external logic needs to know about changes
- Violates **ADR-0006** (Outbox Pattern for Domain Events)

### ✅ CORRECT: Register Domain Events

```csharp
// ✅ Domain events registered for significant changes
public class Customer : AuditableAggregateRoot<CustomerId>
{
    public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
    {
        var emailAddressResult = EmailAddress.Create(email);
        if (emailAddressResult.IsFailure)
        {
            return emailAddressResult.Unwrap();
        }

        return Result<Customer>.Success()
            .Ensure(/* validation */)
            .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number))
            .Tap(e => e.DomainEvents
                .Register(new CustomerCreatedDomainEvent(e)) // ✅ Domain event for creation
                .Register(new EntityCreatedDomainEvent<Customer>(e)));
    }

    public Result<Customer> ChangeName(string firstName, string lastName)
    {
        return this.Change()
            .Ensure(/* validation */)
            .Set(e => e.FirstName, firstName)
            .Set(e => e.LastName, lastName)
            .Register(e => new CustomerUpdatedDomainEvent(e)) // ✅ Domain event for update
            .Apply();
    }
}
```

**Why This Is Correct** (ADR-0006):
- Domain events enable loose coupling
- Other aggregates/modules can react to changes
- Events published via outbox pattern for reliability

---

## Summary

**Key Patterns for Aggregates** (ADR-0012):
1. **Private setters** on all properties
2. **Factory methods** (`Create()`) returning `Result<T>`
3. **Change methods** returning `Result<T>` with validation
4. **Read-only collections** exposed as `IReadOnlyCollection<T>`
5. **Domain events** registered for significant state changes
6. **Strongly-typed IDs** using `[TypedEntityId<Guid>]` (ADR-0008)

**References**:
- **(ADR-0002)**: Result Pattern for Error Handling
- **(ADR-0006)**: Outbox Pattern for Domain Events
- **(ADR-0008)**: Typed Entity IDs using Source Generators
- **(ADR-0012)**: Domain Logic Encapsulation in Domain Layer
