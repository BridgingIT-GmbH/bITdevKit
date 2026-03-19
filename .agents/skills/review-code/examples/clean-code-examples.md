# Clean Code Examples

This file contains WRONG vs CORRECT examples for clean code practices in C#/.NET, with patterns extracted from the actual codebase.

## File-Scoped Namespaces (🔴 CRITICAL - MANDATORY)

**Rule**: All C# files must use file-scoped namespace syntax (enforced by `.editorconfig`).

### Example 1: Basic File-Scoped Namespace

```csharp
// 🔴 WRONG: Block-scoped namespace (CRITICAL VIOLATION)
namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model {
    public class PaymentService
    {
        public void ProcessPayment(decimal amount)
        {
            // Implementation
        }
    }
}

// ✅ CORRECT: File-scoped namespace (MANDATORY)
namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model;

public class PaymentService
{
    public void ProcessPayment(decimal amount)
    {
        // Implementation
    }
}
```

**Why it matters**: File-scoped namespaces reduce indentation by one level, improve readability, and are the modern C# 10+ convention. The `.editorconfig` enforces this as an ERROR-level rule.

### Example 2: File-Scoped Namespace with Using Directives

```csharp
// 🔴 WRONG: Block-scoped namespace with usings outside
using System;
using System.Collections.Generic;

namespace MyApp.Services {
    public class CustomerService { }
}

// ✅ CORRECT: File-scoped namespace with usings inside
namespace MyApp.Services;

using System;
using System.Collections.Generic;

public class CustomerService { }
```

**Note**: Using directives should be placed **inside** the namespace (see Using Directive Placement section).

## Var Usage (🔴 CRITICAL - MANDATORY)

**Rule**: Always use `var` for local variables (enforced by `.editorconfig`).

### Example 3: Var for Object Instantiation

```csharp
// 🔴 WRONG: Explicit type when obvious (CRITICAL VIOLATION)
public void ProcessCustomers()
{
    Customer customer = new Customer();
    List<string> names = new List<string>();
    Dictionary<Guid, Customer> customerMap = new Dictionary<Guid, Customer>();
}

// ✅ CORRECT: Use var (MANDATORY)
public void ProcessCustomers()
{
    var customer = new Customer();
    var names = new List<string>();
    var customerMap = new Dictionary<Guid, Customer>();
}
```

### Example 4: Var for Built-In Types

```csharp
// 🔴 WRONG: Explicit types for built-in types (CRITICAL VIOLATION)
public void CalculateTotal()
{
    int count = 42;
    string name = "John Doe";
    decimal total = 100.50m;
    bool isActive = true;
}

// ✅ CORRECT: Use var for built-in types (MANDATORY)
public void CalculateTotal()
{
    var count = 42;
    var name = "John Doe";
    var total = 100.50m;
    var isActive = true;
}
```

### Example 5: Var for LINQ Queries

```csharp
// 🔴 WRONG: Explicit type for LINQ (CRITICAL VIOLATION)
public void FilterCustomers()
{
    IEnumerable<Customer> activeCustomers = customers.Where(c => c.IsActive);
    List<Customer> customerList = customers.ToList();
}

// ✅ CORRECT: Use var for LINQ (MANDATORY)
public void FilterCustomers()
{
    var activeCustomers = customers.Where(c => c.IsActive);
    var customerList = customers.ToList();
}
```

**Why it matters**: `var` reduces verbosity, improves maintainability when types change, and is enforced by `.editorconfig` as ERROR-level rules.

## Using Directive Placement (🔴 CRITICAL - MANDATORY)

**Rule**: Using directives must be placed inside the namespace (enforced by `.editorconfig`).

### Example 6: Using Directives Inside Namespace

```csharp
// 🔴 WRONG: Using directives outside namespace (CRITICAL VIOLATION)
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.Services;

public class CustomerService { }

// ✅ CORRECT: Using directives inside namespace (MANDATORY)
namespace MyApp.Services;

using System;
using System.Collections.Generic;
using System.Linq;

public class CustomerService { }
```

**Why it matters**: Placement inside namespace prevents naming conflicts and follows project conventions enforced by `.editorconfig`.

## Expression-Bodied Members (🟢 SUGGESTION)

**Rule**: Use expression-bodied syntax (`=>`) for simple methods and properties.

### Example 7: Expression-Bodied Methods (from EmailAddress pattern)

```csharp
// 🟢 SUGGESTION: Could be simplified
public string GetFullName()
{
    return $"{this.FirstName} {this.LastName}";
}

public bool IsAdult()
{
    if (this.Age >= 18)
        return true;
    else
        return false;
}

// ✅ CORRECT: Expression-bodied members
public string GetFullName() => $"{this.FirstName} {this.LastName}";

public bool IsAdult() => this.Age >= 18;
```

### Example 8: Expression-Bodied Properties

```csharp
// 🟢 SUGGESTION: Could be simplified
public string FullName
{
    get
    {
        return $"{this.FirstName} {this.LastName}";
    }
}

public bool HasOrders
{
    get
    {
        return this.Orders.Count > 0;
    }
}

// ✅ CORRECT: Expression-bodied properties
public string FullName => $"{this.FirstName} {this.LastName}";

public bool HasOrders => this.Orders.Count > 0;
```

## Pattern Matching (🟢 SUGGESTION)

**Rule**: Use pattern matching for type checks and value comparisons.

### Example 9: Pattern Matching for Type Checks

```csharp
// 🟢 SUGGESTION: Old-style type checking
public void ProcessResult(object result)
{
    if (result is Customer)
    {
        var customer = (Customer)result;
        Console.WriteLine(customer.Name);
    }
}

// ✅ CORRECT: Pattern matching with type pattern
public void ProcessResult(object result)
{
    if (result is Customer customer)
    {
        Console.WriteLine(customer.Name);
    }
}
```

### Example 10: Switch Expressions

```csharp
// 🟢 SUGGESTION: Traditional switch statement
public string GetStatusDescription(CustomerStatus status)
{
    switch (status)
    {
        case CustomerStatus.Lead:
            return "Potential customer";
        case CustomerStatus.Active:
            return "Active customer";
        case CustomerStatus.Inactive:
            return "Inactive customer";
        default:
            return "Unknown";
    }
}

// ✅ CORRECT: Switch expression
public string GetStatusDescription(CustomerStatus status) => status switch
{
    CustomerStatus.Lead => "Potential customer",
    CustomerStatus.Active => "Active customer",
    CustomerStatus.Inactive => "Inactive customer",
    _ => "Unknown"
};
```

## Guard Clauses (🟢 SUGGESTION)

**Rule**: Use early returns (guard clauses) to reduce nesting.

### Example 11: Guard Clauses to Reduce Nesting

```csharp
// 🟢 SUGGESTION: Deep nesting
public Result ProcessCustomer(Customer customer)
{
    if (customer != null)
    {
        if (customer.IsActive)
        {
            if (customer.Email != null)
            {
                // Actual processing logic buried 3 levels deep
                return SendEmail(customer.Email);
            }
            else
            {
                return Result.Failure("Email is required");
            }
        }
        else
        {
            return Result.Failure("Customer is not active");
        }
    }
    else
    {
        return Result.Failure("Customer is null");
    }
}

// ✅ CORRECT: Guard clauses (early returns)
public Result ProcessCustomer(Customer customer)
{
    if (customer == null)
        return Result.Failure("Customer is null");

    if (!customer.IsActive)
        return Result.Failure("Customer is not active");

    if (customer.Email == null)
        return Result.Failure("Email is required");

    // Actual processing logic at base level (no nesting)
    return SendEmail(customer.Email);
}
```

**Why it matters**: Guard clauses reduce nesting, improve readability, and make the "happy path" more obvious.

## Private Setters (🟡 IMPORTANT - from Customer pattern)

**Rule**: Use private setters to enforce encapsulation and control state changes.

### Example 12: Encapsulation with Private Setters

```csharp
// 🔴 WRONG: Public setters (anemic domain model)
public class Customer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public CustomerStatus Status { get; set; }
}

// Usage: customer.Status = CustomerStatus.Active; // No validation, no business rules!

// ✅ CORRECT: Private setters with change methods (rich domain model)
public class Customer
{
    private Customer() { } // For ORM

    private Customer(string firstName, string lastName, EmailAddress email)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
        this.Status = CustomerStatus.Lead; // Default state
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public EmailAddress Email { get; private set; }
    public CustomerStatus Status { get; private set; }

    // Factory method with validation
    public static Result<Customer> Create(string firstName, string lastName, string email)
    {
        var emailResult = EmailAddress.Create(email);
        if (emailResult.IsFailure)
            return emailResult.Unwrap();

        return Result<Customer>.Success()
            .Ensure(_ => !string.IsNullOrWhiteSpace(firstName), new ValidationError("First name required"))
            .Ensure(_ => !string.IsNullOrWhiteSpace(lastName), new ValidationError("Last name required"))
            .Bind(_ => new Customer(firstName, lastName, emailResult.Value));
    }

    // Change method with business logic
    public Result Activate()
    {
        if (this.Status == CustomerStatus.Active)
            return Result.Failure(new BusinessRuleError("Customer is already active"));

        if (this.Email == null)
            return Result.Failure(new ValidationError("Cannot activate customer without email"));

        this.Status = CustomerStatus.Active;
        this.DomainEvents.Register(new CustomerActivatedDomainEvent(this));

        return Result.Success();
    }
}
```

**Why it matters**: Private setters prevent invalid state by forcing all changes through controlled methods that enforce business rules.

## Null-Conditional Operator (🟢 SUGGESTION)

**Rule**: Use null-conditional operators (`?.`, `??`) for safe null handling.

### Example 13: Null-Conditional and Null-Coalescing

```csharp
// 🟢 SUGGESTION: Explicit null checks
public string GetCustomerEmail(Customer customer)
{
    if (customer != null && customer.Email != null)
    {
        return customer.Email.Value;
    }
    return "No email";
}

public int GetOrderCount(Customer customer)
{
    if (customer != null && customer.Orders != null)
    {
        return customer.Orders.Count;
    }
    return 0;
}

// ✅ CORRECT: Null-conditional operators
public string GetCustomerEmail(Customer customer) =>
    customer?.Email?.Value ?? "No email";

public int GetOrderCount(Customer customer) =>
    customer?.Orders?.Count ?? 0;
```

## String Interpolation (🟢 SUGGESTION)

**Rule**: Use string interpolation (`$""`) instead of concatenation or `string.Format`.

### Example 14: String Interpolation

```csharp
// 🟢 SUGGESTION: String concatenation
public string GetCustomerSummary(Customer customer)
{
    return "Customer: " + customer.FirstName + " " + customer.LastName +
           " (Email: " + customer.Email.Value + ")";
}

// 🟢 SUGGESTION: string.Format
public string GetCustomerSummary(Customer customer)
{
    return string.Format("Customer: {0} {1} (Email: {2})",
        customer.FirstName, customer.LastName, customer.Email.Value);
}

// ✅ CORRECT: String interpolation
public string GetCustomerSummary(Customer customer) =>
    $"Customer: {customer.FirstName} {customer.LastName} (Email: {customer.Email.Value})";
```

## Collection Expressions (🟢 SUGGESTION - C# 12+)

**Rule**: Use collection expressions `[...]` for cleaner syntax.

### Example 15: Collection Expressions

```csharp
// 🟢 SUGGESTION: Traditional collection initialization
public List<string> GetValidStatuses()
{
    return new List<string> { "Active", "Inactive", "Lead" };
}

public int[] GetPriorityLevels()
{
    return new int[] { 1, 2, 3, 5, 8 };
}

// ✅ CORRECT: Collection expressions (C# 12+)
public List<string> GetValidStatuses()
{
    return ["Active", "Inactive", "Lead"];
}

public int[] GetPriorityLevels()
{
    return [1, 2, 3, 5, 8];
}
```

## Primary Constructors (🟢 SUGGESTION - C# 12+)

**Rule**: Use primary constructors for simple dependency injection.

### Example 16: Primary Constructors

```csharp
// 🟢 SUGGESTION: Traditional constructor with field assignment
public class CustomerService
{
    private readonly IGenericRepository<Customer> repository;
    private readonly IMapper mapper;

    public CustomerService(
        IGenericRepository<Customer> repository,
        IMapper mapper)
    {
        this.repository = repository;
        this.mapper = mapper;
    }
}

// ✅ CORRECT: Primary constructor (C# 12+)
public class CustomerService(
    IGenericRepository<Customer> repository,
    IMapper mapper)
{
    // Fields automatically created and assigned
    // Can reference repository and mapper directly

    public async Task<Result<Customer>> GetCustomerAsync(Guid id)
    {
        return await repository.FindOneAsync(id);
    }
}
```

## Summary

Clean code examples demonstrate project standards:

✅ **File-scoped namespaces** (🔴 CRITICAL - MANDATORY via `.editorconfig`)
✅ **Var usage everywhere** (🔴 CRITICAL - MANDATORY via `.editorconfig`)
✅ **Using directives inside namespace** (🔴 CRITICAL - MANDATORY via `.editorconfig`)
✅ **Expression-bodied members** (🟢 SUGGESTION - for simple methods/properties)
✅ **Pattern matching** (🟢 SUGGESTION - for type checks, switch expressions)
✅ **Guard clauses** (🟢 SUGGESTION - early returns reduce nesting)
✅ **Private setters** (🟡 IMPORTANT - encapsulation, business rules in change methods)
✅ **Null-conditional operators** (🟢 SUGGESTION - safe null handling with `?.`, `??`)
✅ **String interpolation** (🟢 SUGGESTION - `$""` over concatenation)
✅ **Collection expressions** (🟢 SUGGESTION - C# 12+ `[...]` syntax)
✅ **Primary constructors** (🟢 SUGGESTION - C# 12+ cleaner dependency injection)

**Reference to actual codebase patterns**:

- **File-scoped namespace**: See `EmailAddress.cs:6`, `Customer.cs:6`
- **Private setters**: See `Customer.cs:40-65`
- **Factory methods**: See `Customer.Create:86-99`, `EmailAddress.Create:69-84`
- **Expression-bodied members**: See `EmailAddress.cs:40`
- **Guard clauses**: See validation patterns throughout domain layer

**Auto-fix CRITICAL violations**: Run `dotnet format` to automatically fix file-scoped namespace, var usage, and using directive placement issues.
