# .editorconfig Compliance Examples

This file explains how to comply with the project's `.editorconfig` rules, particularly the three **MANDATORY** ERROR-level rules that cause build failures if violated.

## Overview

The project uses `.editorconfig` to enforce code style consistency. Three rules are configured as **ERROR level** and **must** be followed:

1. **File-scoped namespaces** (`csharp_style_namespace_declarations = file_scoped:error`)
2. **Var usage** (`csharp_style_var_*:error`)
3. **Using directive placement** (`csharp_using_directive_placement = inside_namespace:error`)

These rules are automatically checked and enforced during build and CI/CD. Violations will cause compilation errors.

## How to Verify Compliance

### Check for Violations

```bash
# Dry run - reports violations without fixing
dotnet format --verify-no-changes

# Output shows violations:
# error WHITESPACE: Fix whitespace formatting. Replace 8 characters with '\n  '.
# error IDE0161: Convert to file-scoped namespace
```

### Auto-Fix Violations

```bash
# Automatically fix all .editorconfig violations
dotnet format

# Output:
# Formatted code file 'Customer.cs'.
# Formatted code file 'EmailAddress.cs'.
# Format complete.
```

### Verify in CI/CD

Add to your build pipeline:

```yaml
# .github/workflows/build.yml
- name: Check code formatting
  run: dotnet format --verify-no-changes
```

This prevents PRs with .editorconfig violations from being merged.

## Rule 1: File-Scoped Namespaces (🔴 CRITICAL - MANDATORY)

**Rule**: `csharp_style_namespace_declarations = file_scoped:error`

**Requirement**: All C# files must use file-scoped namespace syntax (not block-scoped).

### Violation Example

```csharp
// 🔴 CRITICAL VIOLATION: Block-scoped namespace
// File: Customer.cs
namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model {
    using BridgingIT.DevKit.Domain;
    
    public class Customer : AuditableAggregateRoot<CustomerId>
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
    }
}
```

**Compiler Error**:
```
error IDE0161: Convert to file-scoped namespace
```

### Fixed Example

```csharp
// ✅ CORRECT: File-scoped namespace
// File: Customer.cs
namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model;

using BridgingIT.DevKit.Domain;

public class Customer : AuditableAggregateRoot<CustomerId>
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
}
```

**Key Changes**:
- Replace `namespace Foo {` with `namespace Foo;`
- Remove closing brace `}`
- Reduce indentation by one level for all type declarations

**Benefits**:
- Reduces indentation (easier to read)
- Prevents accidentally placing code outside namespace
- Modern C# 10+ convention

### Common Violations

#### Multiple Namespaces in One File

```csharp
// 🔴 WRONG: Multiple namespaces (file-scoped allows only one)
namespace MyApp.Models;
public class Customer { }

namespace MyApp.Services;  // ERROR! Can't have two file-scoped namespaces
public class CustomerService { }

// ✅ CORRECT: Split into separate files
// File: Customer.cs
namespace MyApp.Models;
public class Customer { }

// File: CustomerService.cs
namespace MyApp.Services;
public class CustomerService { }
```

#### Nested Namespaces

```csharp
// 🔴 WRONG: Nested namespaces with file-scoped syntax
namespace MyApp;

namespace MyApp.Models;  // ERROR!
public class Customer { }

// ✅ CORRECT: Full namespace path
namespace MyApp.Models;
public class Customer { }
```

## Rule 2: Var Usage (🔴 CRITICAL - MANDATORY)

**Rules**:
- `csharp_style_var_elsewhere = true:error`
- `csharp_style_var_for_built_in_types = true:error`
- `csharp_style_var_when_type_is_apparent = true:error`

**Requirement**: Always use `var` for local variable declarations.

### Violation Examples

```csharp
// 🔴 CRITICAL VIOLATIONS: Explicit types
public void ProcessCustomers()
{
    // Violation 1: Explicit type for object instantiation
    Customer customer = new Customer();
    
    // Violation 2: Explicit type for built-in types
    int count = 42;
    string name = "John";
    bool isActive = true;
    
    // Violation 3: Explicit type for LINQ
    List<Customer> customers = context.Customers.ToList();
    IEnumerable<string> names = customers.Select(c => c.Name);
}
```

**Compiler Errors**:
```
error IDE0007: Use 'var' instead of explicit type
error IDE0007: Use 'var' instead of explicit type
error IDE0007: Use 'var' instead of explicit type
...
```

### Fixed Example

```csharp
// ✅ CORRECT: Use var everywhere
public void ProcessCustomers()
{
    // Correct: var for object instantiation
    var customer = new Customer();
    
    // Correct: var for built-in types
    var count = 42;
    var name = "John";
    var isActive = true;
    
    // Correct: var for LINQ
    var customers = context.Customers.ToList();
    var names = customers.Select(c => c.Name);
}
```

### When to Use Var

#### ✅ Object Instantiation

```csharp
// ✅ CORRECT
var customer = new Customer();
var list = new List<string>();
var dict = new Dictionary<Guid, Customer>();
```

#### ✅ Built-In Types

```csharp
// ✅ CORRECT
var count = 42;
var total = 100.50m;
var name = "John Doe";
var isValid = true;
```

#### ✅ LINQ Queries

```csharp
// ✅ CORRECT
var customers = context.Customers.Where(c => c.IsActive);
var names = customers.Select(c => c.Name).ToList();
var grouped = customers.GroupBy(c => c.City);
```

#### ✅ Method Return Values

```csharp
// ✅ CORRECT
var customer = GetCustomer();
var result = await repository.FindOneAsync(id);
var json = JsonSerializer.Serialize(customer);
```

### Exceptions (Rare)

There are very few cases where explicit types are preferred (but .editorconfig still enforces `var`):

```csharp
// In these cases, use var as required by .editorconfig:

// 1. Numeric literals where type isn't obvious
var value = 42;  // Could be int, long, short - use var anyway

// 2. Cast operations
var customer = (Customer)obj;  // Type is apparent from cast

// 3. When pattern matching makes type clear
if (result is Customer customer)  // 'customer' already typed by pattern
{
    // Use it
}
```

## Rule 3: Using Directive Placement (🔴 CRITICAL - MANDATORY)

**Rule**: `csharp_using_directive_placement = inside_namespace:error`

**Requirement**: `using` directives must be placed **inside** the namespace, not before it.

### Violation Example

```csharp
// 🔴 CRITICAL VIOLATION: Using directives outside namespace
// File: Customer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain;

namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model;

public class Customer : AuditableAggregateRoot<CustomerId>
{
    // ...
}
```

**Compiler Error**:
```
error IDE0065: 'using' directive placement
```

### Fixed Example

```csharp
// ✅ CORRECT: Using directives inside namespace
// File: Customer.cs
namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain;

public class Customer : AuditableAggregateRoot<CustomerId>
{
    // ...
}
```

**Key Changes**:
- Namespace declaration comes **first** (after license header/file header)
- All `using` directives come **after** the namespace declaration
- Blank line between namespace and usings (optional but recommended)

### Why It Matters

#### Prevents Naming Conflicts

```csharp
// Scenario: You have System.IO.File and a custom MyApp.IO.File

// With usings OUTSIDE namespace:
using System.IO;
using MyApp.IO;

namespace MyApp.Services;

public class FileService
{
    public void Process()
    {
        var file = new File();  // Ambiguous! Which File?
    }
}

// With usings INSIDE namespace (project standard):
namespace MyApp.Services;

using System.IO;
using MyApp.IO;

public class FileService
{
    public void Process()
    {
        var file = new File();  // Resolves to MyApp.IO.File (closer scope)
    }
}
```

### Using Organization

While not enforced by ERROR-level rules, organize usings as follows:

```csharp
namespace MyApp.Services;

// 1. System namespaces (sorted alphabetically)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

// 2. Third-party namespaces (sorted alphabetically)
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Application;

// 3. Project namespaces (sorted alphabetically)
using MyApp.Domain.Model;
using MyApp.Infrastructure;

public class CustomerService
{
    // ...
}
```

**Auto-organize**:
```bash
# Remove unused usings and sort
dotnet format
```

## Other Common .editorconfig Rules (Warning/Suggestion Level)

These rules don't cause build failures but are good practices:

### Expression-Bodied Members

```csharp
// 🟢 SUGGESTION: Could be simplified
public string GetFullName()
{
    return $"{FirstName} {LastName}";
}

// ✅ BETTER: Expression-bodied
public string GetFullName() => $"{FirstName} {LastName}";
```

### This Qualifier

```csharp
// Project preference: Use 'this.' for fields (not enforced as error)
public class Customer
{
    private readonly IRepository repository;
    
    public Customer(IRepository repository)
    {
        this.repository = repository;  // Preferred style
    }
    
    public void Save()
    {
        this.repository.Save(this);  // Use 'this.' prefix
    }
}
```

## Complete File Example (All Rules Applied)

```csharp
// ✅ CORRECT: Compliant with all mandatory .editorconfig rules
// File: Customer.cs

// License header (if applicable)
// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.GettingStarted.Modules.CoreModule.Domain.Model;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Domain;

/// <summary>
/// Represents a customer aggregate root in the domain model.
/// </summary>
public class Customer : AuditableAggregateRoot<CustomerId>
{
    private readonly List<Address> addresses = [];
    
    private Customer() { }
    
    private Customer(string firstName, string lastName, EmailAddress email)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
    }
    
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public EmailAddress Email { get; private set; }
    
    public IReadOnlyCollection<Address> Addresses => this.addresses.AsReadOnly();
    
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
}
```

**All rules followed**:
- ✅ File-scoped namespace (`namespace ...;`)
- ✅ Using directives inside namespace
- ✅ Var usage (not shown in this example as it's a class, not a method)
- ✅ Expression-bodied member (`Addresses` property)
- ✅ This qualifier (`this.FirstName`, `this.addresses`)

## Troubleshooting

### Problem: "dotnet format" Doesn't Fix Issues

**Solution 1**: Check .editorconfig file exists in repo root
```bash
# Verify .editorconfig exists
ls -la .editorconfig

# If missing, it won't be enforced
```

**Solution 2**: Ensure rule severity is set correctly
```ini
# In .editorconfig
[*.cs]
csharp_style_namespace_declarations = file_scoped:error
csharp_style_var_elsewhere = true:error
csharp_style_var_for_built_in_types = true:error
csharp_style_var_when_type_is_apparent = true:error
csharp_using_directive_placement = inside_namespace:error
```

**Solution 3**: Clean and rebuild
```bash
dotnet clean
dotnet format
dotnet build
```

### Problem: CI/CD Passes Locally But Fails on Server

**Cause**: Different .NET SDK versions or .editorconfig not committed

**Solution**:
```bash
# Ensure .editorconfig is committed
git add .editorconfig
git commit -m "Add .editorconfig"
git push

# Ensure consistent SDK version in global.json
cat global.json
```

### Problem: Too Many Violations to Fix Manually

**Solution**: Use `dotnet format` to auto-fix everything
```bash
# Auto-fix all violations in solution
dotnet format

# Check what was fixed
git diff

# Commit the formatting changes
git add -A
git commit -m "Apply .editorconfig formatting rules"
```

## Summary

.editorconfig compliance ensures code consistency:

✅ **File-scoped namespaces** (🔴 MANDATORY - use `namespace Foo;` syntax)  
✅ **Var usage** (🔴 MANDATORY - use `var` for all local variables)  
✅ **Using directives inside namespace** (🔴 MANDATORY - place after `namespace` declaration)  

**Quick compliance check**:
1. Run `dotnet format --verify-no-changes`
2. If violations found, run `dotnet format` to auto-fix
3. Commit the changes

**Auto-fix command**:
```bash
dotnet format
```

This automatically fixes all three MANDATORY rules plus other style violations.

**Prevent violations**:
- Configure your IDE (VS, VS Code, Rider) to respect `.editorconfig`
- Add `dotnet format --verify-no-changes` to CI/CD pipeline
- Run `dotnet format` before committing

**Reference**: See root `.editorconfig` file for complete rules and severity levels.
