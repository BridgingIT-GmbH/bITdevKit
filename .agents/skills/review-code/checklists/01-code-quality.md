# Code Quality Checklist

Use this checklist to verify code quality standards in C#/.NET projects. This checklist covers naming conventions, code organization, method complexity, and adherence to project `.editorconfig` rules.

## Naming Conventions

- [ ] **Classes**: PascalCase (`CustomerService`, `EmailAddress`, `PaymentProcessor`)
- [ ] **Methods**: PascalCase (`GetCustomer`, `CalculateTotal`, `ProcessPayment`)
- [ ] **Properties**: PascalCase (`FirstName`, `IsActive`, `CreatedDate`)
- [ ] **Fields (private)**: camelCase with `this.` prefix (`this.customerRepository`, `this.logger`)
- [ ] **Local variables**: camelCase (`customerCount`, `emailAddress`, `isValid`)
- [ ] **Constants**: UPPERCASE with underscores (`MAX_RETRY_COUNT`, `DEFAULT_TIMEOUT`)
- [ ] **Interfaces**: Prefix with `I` (`ICustomerService`, `IRepository<T>`, `IMapper`)
- [ ] **Type parameters**: Single uppercase letter or `T` prefix (`T`, `TEntity`, `TKey`)
- [ ] **Async methods**: Suffix with `Async` (`GetCustomerAsync`, `SaveChangesAsync`)

### Naming Quality

- [ ] **Descriptive names**: Names clearly convey purpose (`customerRepository` not `repo`, `isEmailValid` not `flag`)
- [ ] **Avoid abbreviations**: Use full words unless widely understood (`id` is okay, `cust` is not)
- [ ] **Consistent terminology**: Use the same term throughout (don't mix `User`/`Person`/`Customer`)
- [ ] **Boolean naming**: Start with `Is`, `Has`, `Can`, `Should` (`IsActive`, `HasPermission`, `CanExecute`)

## File-Scoped Namespaces (🔴 CRITICAL - MANDATORY)

**Rule**: All C# files **must** use file-scoped namespace syntax (enforced by `.editorconfig`)

- [ ] **No block-scoped namespaces**: Files do not use `namespace Foo { }` syntax
- [ ] **File-scoped syntax used**: Files use `namespace Foo;` syntax (C# 10+)
- [ ] **One namespace per file**: Each file has exactly one namespace declaration
- [ ] **Namespace at top**: Namespace declaration is the first statement after license header

### Example

```csharp
// 🔴 WRONG: Block-scoped namespace (CRITICAL VIOLATION)
namespace MyApp.Services {
    public class PaymentService
    {
        public void ProcessPayment() { }
    }
}

// ✅ CORRECT: File-scoped namespace (MANDATORY)
namespace MyApp.Services;

public class PaymentService
{
    public void ProcessPayment() { }
}
```

**Why it matters**: File-scoped namespaces reduce indentation by one level, improve readability, and are the modern C# convention. The `.editorconfig` rule `csharp_style_namespace_declarations = file_scoped:error` enforces this.

**How to fix**: Convert all block-scoped namespaces to file-scoped. Run `dotnet format` to auto-fix.

## Var Usage (🔴 CRITICAL - MANDATORY)

**Rule**: **Always** use `var` for local variables (enforced by `.editorconfig`)

- [ ] **Var for new expressions**: `var customer = new Customer();` (not `Customer customer = new Customer();`)
- [ ] **Var for built-in types**: `var count = 42;` (not `int count = 42;`)
- [ ] **Var when type is apparent**: `var service = GetService();` when return type is clear
- [ ] **Var for LINQ expressions**: `var results = customers.Where(c => c.IsActive);`

### Example

```csharp
// 🔴 WRONG: Explicit type when obvious (CRITICAL VIOLATION)
Customer customer = new Customer();
int count = 42;
string name = "John";
List<string> names = new List<string>();

// ✅ CORRECT: Use var (MANDATORY)
var customer = new Customer();
var count = 42;
var name = "John";
var names = new List<string>();
```

**Why it matters**: `var` reduces verbosity, improves maintainability (when types change), and is the modern C# convention. The `.editorconfig` rules `csharp_style_var_*:error` enforce this.

**How to fix**: Replace explicit types with `var`. Run `dotnet format` to auto-fix.

## Using Directive Placement (🔴 CRITICAL - MANDATORY)

**Rule**: `using` directives **must** be placed inside the namespace (enforced by `.editorconfig`)

- [ ] **No using directives before namespace**: Usings are not placed at the top of the file
- [ ] **Usings inside namespace**: Usings appear after the namespace declaration
- [ ] **Organized usings**: System namespaces first, then third-party, then project namespaces
- [ ] **No unused usings**: Remove unused using directives

### Example

```csharp
// 🔴 WRONG: Using directives outside namespace (CRITICAL VIOLATION)
using System;
using System.Collections.Generic;

namespace MyApp.Services;

public class PaymentService { }

// ✅ CORRECT: Using directives inside namespace (MANDATORY)
namespace MyApp.Services;

using System;
using System.Collections.Generic;

public class PaymentService { }
```

**Why it matters**: Placement inside namespace prevents naming conflicts and follows project conventions. The `.editorconfig` rule `csharp_using_directive_placement = inside_namespace:error` enforces this.

**How to fix**: Move `using` directives after the namespace declaration. Run `dotnet format` to auto-fix.

## Method Size and Complexity (🟡 IMPORTANT)

- [ ] **Method length**: Methods are typically < 20 lines (excluding braces and whitespace)
- [ ] **Cyclomatic complexity**: Methods have complexity < 10 (fewer decision points)
- [ ] **Nesting depth**: Code has < 3 levels of nesting (if/for/while/try)
- [ ] **Single Responsibility**: Each method does one thing well
- [ ] **Extract complex logic**: Long methods are broken into smaller helper methods

### Example

```csharp
// 🟡 IMPORTANT: Method too complex (nesting depth = 4, complexity = 12)
public void ProcessOrder(Order order)
{
    if (order != null)
    {
        if (order.Items.Count > 0)
        {
            foreach (var item in order.Items)
            {
                if (item.Price > 0)
                {
                    // Complex logic here
                }
            }
        }
    }
}

// ✅ CORRECT: Extracted into smaller methods
public void ProcessOrder(Order order)
{
    if (!IsValidOrder(order))
        return;
    
    ProcessOrderItems(order.Items);
}

private bool IsValidOrder(Order order) =>
    order != null && order.Items.Count > 0;

private void ProcessOrderItems(IEnumerable<OrderItem> items)
{
    foreach (var item in items.Where(i => i.Price > 0))
    {
        ProcessItem(item);
    }
}
```

**Why it matters**: Smaller, focused methods are easier to understand, test, and maintain. High complexity indicates code that's prone to bugs.

## DRY Principle (🟡 IMPORTANT)

- [ ] **No duplicated code**: Same logic is not repeated in multiple places
- [ ] **Extracted common logic**: Shared logic is extracted into methods or classes
- [ ] **Reusable components**: Common patterns are abstracted into reusable utilities
- [ ] **Parameterized methods**: Similar methods with slight variations use parameters instead of duplication

### Example

```csharp
// 🟡 IMPORTANT: Duplicated validation logic
public Result ValidateCustomer(Customer customer)
{
    if (string.IsNullOrWhiteSpace(customer.FirstName))
        return Result.Failure("First name required");
    if (string.IsNullOrWhiteSpace(customer.LastName))
        return Result.Failure("Last name required");
    return Result.Success();
}

public Result ValidateEmployee(Employee employee)
{
    if (string.IsNullOrWhiteSpace(employee.FirstName))
        return Result.Failure("First name required");
    if (string.IsNullOrWhiteSpace(employee.LastName))
        return Result.Failure("Last name required");
    return Result.Success();
}

// ✅ CORRECT: Extracted common logic
public Result ValidatePerson(string firstName, string lastName)
{
    if (string.IsNullOrWhiteSpace(firstName))
        return Result.Failure("First name required");
    if (string.IsNullOrWhiteSpace(lastName))
        return Result.Failure("Last name required");
    return Result.Success();
}

public Result ValidateCustomer(Customer customer) =>
    ValidatePerson(customer.FirstName, customer.LastName);

public Result ValidateEmployee(Employee employee) =>
    ValidatePerson(employee.FirstName, employee.LastName);
```

**Why it matters**: Duplicated code increases maintenance burden. Bugs must be fixed in multiple places, and changes are error-prone.

## Magic Numbers and Strings (🟢 SUGGESTION)

- [ ] **No magic numbers**: Numeric literals (except 0, 1, -1) are extracted to named constants
- [ ] **No magic strings**: String literals used in logic are extracted to constants
- [ ] **Descriptive constant names**: Constants clearly describe what the value represents
- [ ] **Grouped constants**: Related constants are organized together (class or enum)

### Example

```csharp
// 🟢 SUGGESTION: Magic numbers and strings
public bool IsEligibleForDiscount(Customer customer)
{
    return customer.OrderCount > 10 && customer.TotalSpent > 1000.00m;
}

public string GetCustomerStatus(Customer customer)
{
    if (customer.IsActive && customer.LastOrderDate > DateTime.Now.AddMonths(-6))
        return "active";
    return "inactive";
}

// ✅ CORRECT: Named constants
private const int DISCOUNT_ELIGIBLE_ORDER_COUNT = 10;
private const decimal DISCOUNT_ELIGIBLE_TOTAL_SPENT = 1000.00m;
private const int ACTIVE_CUSTOMER_MONTHS_THRESHOLD = 6;

private const string CUSTOMER_STATUS_ACTIVE = "active";
private const string CUSTOMER_STATUS_INACTIVE = "inactive";

public bool IsEligibleForDiscount(Customer customer)
{
    return customer.OrderCount > DISCOUNT_ELIGIBLE_ORDER_COUNT 
        && customer.TotalSpent > DISCOUNT_ELIGIBLE_TOTAL_SPENT;
}

public string GetCustomerStatus(Customer customer)
{
    if (customer.IsActive && customer.LastOrderDate > DateTime.Now.AddMonths(-ACTIVE_CUSTOMER_MONTHS_THRESHOLD))
        return CUSTOMER_STATUS_ACTIVE;
    return CUSTOMER_STATUS_INACTIVE;
}
```

**Why it matters**: Named constants make code self-documenting, easier to change, and prevent typos in repeated values.

## Modern C# Features (🟢 SUGGESTION)

- [ ] **Expression-bodied members**: Use `=>` for simple methods/properties
- [ ] **Pattern matching**: Use pattern matching for type checks and value comparisons
- [ ] **Record types**: Use records for immutable data structures
- [ ] **Collection expressions**: Use `[...]` syntax for collections (C# 12+)
- [ ] **Primary constructors**: Use primary constructors for simple classes (C# 12+)
- [ ] **Null-coalescing operators**: Use `??`, `??=`, `?.` for null handling
- [ ] **String interpolation**: Use `$"..."` instead of `string.Format` or concatenation

### Example

```csharp
// 🟢 SUGGESTION: Could use modern C# features
public string GetFullName()
{
    return this.FirstName + " " + this.LastName;
}

public bool IsAdult()
{
    if (this.Age >= 18)
        return true;
    else
        return false;
}

// ✅ CORRECT: Modern C# features
public string GetFullName() => $"{this.FirstName} {this.LastName}";

public bool IsAdult() => this.Age >= 18;
```

**Why it matters**: Modern C# features make code more concise, readable, and maintainable. They reflect current language best practices.

## Code Organization

- [ ] **Logical grouping**: Related methods/properties are grouped together
- [ ] **Consistent ordering**: Fields → Constructors → Properties → Methods
- [ ] **Public before private**: Public members before private/internal
- [ ] **Static before instance**: Static members before instance members
- [ ] **One class per file**: Each file contains one primary class (nested classes okay)
- [ ] **Appropriate regions**: Use `#region` sparingly, only for large classes

## Comments and Documentation

- [ ] **Self-documenting code**: Code is clear without comments where possible
- [ ] **WHY not WHAT comments**: Comments explain why, not what the code does
- [ ] **No commented-out code**: Remove commented-out code (use version control)
- [ ] **TODO comments tracked**: TODO comments reference issue tracker or have owner
- [ ] **Updated comments**: Comments reflect current code (no stale comments)

## Error Handling

- [ ] **Appropriate exception types**: Throw specific exception types (not `Exception`)
- [ ] **Result<T> for expected failures**: Use Result pattern for business rule violations
- [ ] **Exceptions for unexpected failures**: Use exceptions for system errors only
- [ ] **No empty catch blocks**: Catch blocks always handle or log exceptions
- [ ] **Proper exception propagation**: Don't swallow exceptions without handling

## Summary

This checklist ensures code quality fundamentals:

✅ **Naming conventions** followed (PascalCase, camelCase, descriptive names)  
✅ **File-scoped namespaces** used (🔴 MANDATORY via `.editorconfig`)  
✅ **Var usage** enforced (🔴 MANDATORY via `.editorconfig`)  
✅ **Using directives inside namespace** (🔴 MANDATORY via `.editorconfig`)  
✅ **Method complexity** controlled (< 20 lines, < 10 complexity, < 3 nesting)  
✅ **DRY principle** applied (no duplicated logic)  
✅ **Magic numbers/strings** extracted to constants  
✅ **Modern C# features** utilized (expression-bodied members, pattern matching, etc.)  

**Use `dotnet format` to automatically fix .editorconfig violations.**

**Reference**: See `examples/clean-code-examples.md` for detailed WRONG vs CORRECT examples.
