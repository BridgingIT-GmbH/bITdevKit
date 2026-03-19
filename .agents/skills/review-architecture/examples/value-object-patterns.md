# Value Object Patterns: WRONG vs CORRECT

This document shows common value object implementation mistakes and their corrections, extracted from the EmailAddress value object pattern.

## Pattern 1: Primitive Obsession

### ❌ WRONG: Using Primitive Types

```csharp
// ❌ Primitive obsession - using string for email
public class Customer : AggregateRoot<CustomerId>
{
    public string Email { get; private set; } // ❌ Primitive type

    public static Result<Customer> Create(string firstName, string lastName, string email)
    {
        // ❌ Email validation duplicated everywhere
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            return Result<Customer>.Failure("Invalid email");
        }

        return Result<Customer>.Success(new Customer(firstName, lastName, email));
    }

    public Result<Customer> ChangeEmail(string email)
    {
        // ❌ Validation duplicated again
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            return Result<Customer>.Failure("Invalid email");
        }

        this.Email = email;
        return Result<Customer>.Success(this);
    }
}
```

**Why This Is Wrong**:
- Validation logic duplicated across the codebase
- No type safety (can pass any string)
- Cannot add email-specific behavior
- Violates **ADR-0012** (Domain Logic in Domain Layer)

### ✅ CORRECT: Value Object with Encapsulation

```csharp
// ✅ Value object encapsulates email concept
namespace MyApp.Domain.Model;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Represents an immutable email address value object in the domain model.
/// </summary>
[DebuggerDisplay("Value={Value}")]
public class EmailAddress : ValueObject
{
    private EmailAddress() { } // ✅ Private constructor

    private EmailAddress(string value) => this.Value = value;

    // ✅ Private setter (immutable)
    public string Value { get; private set; }

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

// ✅ Use in aggregate
public class Customer : AuditableAggregateRoot<CustomerId>
{
    public EmailAddress Email { get; private set; } // ✅ Strongly-typed

    public static Result<Customer> Create(string firstName, string lastName, string email, CustomerNumber number)
    {
        // ✅ Validation centralized in EmailAddress.Create()
        var emailAddressResult = EmailAddress.Create(email);
        if (emailAddressResult.IsFailure)
        {
            return emailAddressResult.Unwrap();
        }

        return Result<Customer>.Success()
            .Ensure(/* other validation */)
            .Bind(_ => new Customer(firstName, lastName, emailAddressResult.Value, number));
    }
}
```

**Why This Is Correct** (ADR-0012):
- Validation centralized in one place
- Type-safe (cannot accidentally pass wrong string)
- Can add email-specific behavior
- Immutable by design

---

## Pattern 2: Mutable Value Objects

### ❌ WRONG: Value Object with Mutable State

```csharp
// ❌ Mutable value object
public class EmailAddress : ValueObject
{
    public string Value { get; set; } // ❌ Public setter

    public static EmailAddress Create(string value)
    {
        return new EmailAddress { Value = value }; // ❌ No validation
    }

    // ❌ Mutating method
    public void ChangeValue(string newValue)
    {
        this.Value = newValue; // ❌ Violates immutability
    }
}
```

**Why This Is Wrong**:
- Value objects should be immutable (defined by their values, not identity)
- Mutating methods break value semantics
- Cannot safely use as dictionary keys or in hash sets
- Violates value object pattern

### ✅ CORRECT: Immutable Value Object

```csharp
// ✅ Immutable value object
public class EmailAddress : ValueObject
{
    private EmailAddress() { } // Private constructor

    private EmailAddress(string value) => this.Value = value;

    // ✅ Private setter (immutable)
    public string Value { get; private set; }

    // ✅ Factory method (only way to create)
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

        return new EmailAddress(value);
    }

    // ✅ No mutating methods - create new instance instead
    public Result<EmailAddress> WithDomain(string newDomain)
    {
        var localPart = this.Value.Split('@')[0];
        return EmailAddress.Create($"{localPart}@{newDomain}"); // Returns new instance
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}
```

**Why This Is Correct**:
- Immutable by design (private setters, no mutating methods)
- Safe to use as dictionary keys or in collections
- Thread-safe
- Follows value object semantics

---

## Pattern 3: Missing Validation

### ❌ WRONG: No Validation in Factory Method

```csharp
// ❌ No validation
public class EmailAddress : ValueObject
{
    public string Value { get; private set; }

    public static EmailAddress Create(string value)
    {
        return new EmailAddress { Value = value }; // ❌ No validation!
    }
}

// ❌ Invalid emails can be created
var email = EmailAddress.Create("not-an-email"); // Succeeds!
var email2 = EmailAddress.Create(null); // Succeeds!
```

**Why This Is Wrong**:
- Invalid states can be created
- Business rules not enforced
- Defeats the purpose of value objects

### ✅ CORRECT: Validation in Factory Method

```csharp
// ✅ Validation in factory method
public class EmailAddress : ValueObject
{
    private EmailAddress(string value) => this.Value = value;

    public string Value { get; private set; }

    // ✅ Factory method validates before creation
    public static Result<EmailAddress> Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant(); // ✅ Normalize

        var ruleResult = Rule.Add(RuleSet.IsValidEmail(value)).Check();

        if (ruleResult.IsFailure)
        {
            return Result<EmailAddress>.Failure()
                .WithMessages(ruleResult.Messages)
                .WithErrors(ruleResult.Errors); // ✅ Clear error messages
        }

        return new EmailAddress(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}
```

**Why This Is Correct** (ADR-0002):
- Validation ensures only valid instances exist
- Returns `Result<T>` for composable error handling
- Normalization (trim, lowercase) applied consistently

---

## Pattern 4: Generic Naming

### ❌ WRONG: Generic Value Object Names

```csharp
// ❌ Generic names
public class Email : ValueObject { } // ❌ Not descriptive
public class Money : ValueObject { } // ❌ Too generic
public class Address : ValueObject { } // ❌ Could be confused with entity
```

**Why This Is Wrong**:
- Not descriptive enough
- Can be confused with entities or primitives
- Doesn't convey domain concept clearly

### ✅ CORRECT: Descriptive Value Object Names

```csharp
// ✅ Descriptive names
public class EmailAddress : ValueObject { } // ✅ Clear: email address
public class MoneyAmount : ValueObject { } // ✅ Clear: amount of money
public class PostalAddress : ValueObject { } // ✅ Clear: postal address (different from Address entity)
public class CustomerNumber : ValueObject { } // ✅ Clear: customer number
```

**Why This Is Correct**:
- Descriptive names convey domain concept
- Clear distinction from entities and primitives
- Self-documenting code

---

## Pattern 5: Value-Based Equality

### ❌ WRONG: Identity-Based Equality

```csharp
// ❌ Identity-based equality (default reference equality)
public class EmailAddress
{
    public string Value { get; private set; }

    // ❌ No equality override
}

// Problem:
var email1 = EmailAddress.Create("john@example.com");
var email2 = EmailAddress.Create("john@example.com");
email1 == email2 // ❌ False! (different instances)
```

**Why This Is Wrong**:
- Value objects should be equal if their values are equal
- Default reference equality compares instances, not values
- Breaks value semantics

### ✅ CORRECT: Value-Based Equality

```csharp
// ✅ Value-based equality via GetAtomicValues()
public class EmailAddress : ValueObject
{
    private EmailAddress(string value) => this.Value = value;

    public string Value { get; private set; }

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

        return new EmailAddress(value);
    }

    // ✅ Define atomic values for equality comparison
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value; // ✅ Value-based equality
    }
}

// Correct behavior:
var email1 = EmailAddress.Create("john@example.com").Value;
var email2 = EmailAddress.Create("john@example.com").Value;
email1 == email2 // ✅ True! (same value)
```

**Why This Is Correct**:
- Value objects compared by values, not identity
- `GetAtomicValues()` defines what constitutes equality
- Follows value object semantics

---

## Pattern 6: Implicit Operators (Optional)

### Example: Convenient Conversion

```csharp
// ✅ Implicit operators for convenience (optional)
public class EmailAddress : ValueObject
{
    private EmailAddress(string value) => this.Value = value;

    public string Value { get; private set; }

    // ✅ Implicit conversion to string
    public static implicit operator string(EmailAddress email) => email.Value;

    // ✅ Implicit conversion from string (validates)
    public static implicit operator EmailAddress(string value)
    {
        var result = Create(value);
        if (result.IsFailure)
        {
            var message = string.Join("; ", result.Messages ?? []);
            throw new ResultException(string.IsNullOrWhiteSpace(message) ? "Invalid email address." : message);
        }

        return result.Value;
    }

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

        return new EmailAddress(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}

// Usage:
string emailString = emailAddress; // ✅ Implicit conversion to string
EmailAddress email = "john@example.com"; // ✅ Implicit conversion from string (validates)
```

**Note**: Implicit operators are optional. Use them for convenience but ensure validation is preserved.

---

## Summary

**Key Patterns for Value Objects** (ADR-0012):
1. **Immutability**: Private setters, no mutating methods
2. **Factory method**: `Create()` returning `Result<T>` with validation
3. **Descriptive naming**: Use specific names (e.g., `EmailAddress`, not `Email`)
4. **Value-based equality**: Override `GetAtomicValues()` for comparison
5. **Self-contained**: No external dependencies
6. **Small and focused**: Represent a single domain concept

**References**:
- **(ADR-0002)**: Result Pattern for Error Handling
- **(ADR-0012)**: Domain Logic Encapsulation in Domain Layer
