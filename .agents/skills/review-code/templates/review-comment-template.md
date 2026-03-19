# Review Comment Template

Use these templates for consistent, actionable code review comments. Each template includes: priority indicator, category, brief title, detailed explanation, suggested fix with code, and references.

## Template Structure

```markdown
[PRIORITY] Category: Brief Title

**Issue**: {Describe the problem specifically}
**Why This Matters**: {Explain the impact - security, performance, maintainability}
**Suggested Fix**:
```csharp
// Code example showing the correction
```
**Reference**: {Link to documentation, .editorconfig, ADR, or examples}
```

---

## 🔴 CRITICAL Comment Template

Use for issues that **must** be fixed before merge (security, correctness, data loss, .editorconfig ERROR violations).

### Template

```markdown
🔴 CRITICAL - {Category}: {Brief Title}

**Issue**: {Specific description of the problem with file and line reference}

**Why This Matters**: {Explain critical impact - security breach, data corruption, deadlock, etc.}

**Suggested Fix**:
```csharp
// 🔴 WRONG (current code):
{current problematic code}

// ✅ CORRECT (suggested fix):
{corrected code}
```

**Reference**: {Link to security checklist, .editorconfig rule, or documentation}
```

### Example 1: Hardcoded Secret

```markdown
🔴 CRITICAL - Security: Hardcoded Database Password

**Issue**: Line 42 in `DatabaseService.cs` contains a hardcoded connection string with a password.

```csharp
private const string ConnectionString = 
    "Server=prod.db.com;Database=CustomerDB;User Id=sa;Password=P@ssw0rd123!;";
```

**Why This Matters**: Hardcoded secrets in source code are a critical security vulnerability. They are:
- Visible in version control history
- Accessible to anyone with repo access
- Cannot be rotated without code changes
- Often accidentally exposed in logs or error messages

**Suggested Fix**:
```csharp
// 🔴 WRONG: Hardcoded password
private const string ConnectionString = 
    "Server=prod.db.com;Database=CustomerDB;User Id=sa;Password=P@ssw0rd123!;";

// ✅ CORRECT: Configuration-based
public class DatabaseService
{
    private readonly IConfiguration configuration;
    
    public DatabaseService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }
    
    public async Task ConnectAsync()
    {
        var connectionString = this.configuration.GetConnectionString("CustomerDatabase");
        // Use connectionString...
    }
}
```

**Storage**:
- Local dev: `appsettings.Development.json` (git-ignored)
- Production: Azure Key Vault or environment variables

**Reference**: See `examples/security-examples.md` - Example 1: Hardcoded Database Connection String
```

### Example 2: SQL Injection

```markdown
🔴 CRITICAL - Security: SQL Injection Vulnerability

**Issue**: Line 127 in `CustomerRepository.cs` constructs SQL query using string interpolation with user input.

```csharp
var sql = $"SELECT * FROM Customers WHERE Name = '{customerName}'";
```

**Why This Matters**: SQL injection can allow attackers to:
- Steal or modify data
- Delete entire tables
- Execute arbitrary database commands
- Gain unauthorized access

**Attack Example**:
User provides: `"'; DROP TABLE Customers; --"`
Result: `SELECT * FROM Customers WHERE Name = ''; DROP TABLE Customers; --'`

**Suggested Fix**:
```csharp
// 🔴 WRONG: String concatenation/interpolation (SQL injection risk)
var sql = $"SELECT * FROM Customers WHERE Name = '{customerName}'";
var customer = await context.Database.SqlQueryRaw<Customer>(sql).FirstOrDefaultAsync();

// ✅ CORRECT: Parameterized query
var sql = "SELECT * FROM Customers WHERE Name = {0}";
var customer = await context.Database.SqlQueryRaw<Customer>(sql, customerName).FirstOrDefaultAsync();

// ✅ BETTER: EF Core LINQ (safe by default)
var customer = await context.Customers
    .Where(c => c.Name == customerName)
    .FirstOrDefaultAsync();
```

**Reference**: See `examples/security-examples.md` - Example 4: SQL Injection Vulnerability
```

### Example 3: Blocking on Async Code

```markdown
🔴 CRITICAL - Performance: Blocking on Async Code (Deadlock Risk)

**Issue**: Line 89 in `CustomerController.cs` blocks on async code using `.Result`.

```csharp
var customer = service.GetCustomerAsync(id, CancellationToken.None).Result;
```

**Why This Matters**: Blocking on async code in ASP.NET Core can cause:
- **Deadlocks** in synchronization contexts
- Thread pool starvation
- Reduced application throughput
- Complete application hangs under load

**Suggested Fix**:
```csharp
// 🔴 WRONG: Blocking on async (.Result causes deadlocks)
public IActionResult GetCustomer(Guid id)
{
    var customer = service.GetCustomerAsync(id, CancellationToken.None).Result;
    return Ok(customer);
}

// ✅ CORRECT: Make method async and await
public async Task<IActionResult> GetCustomerAsync(Guid id, CancellationToken cancellationToken)
{
    var customer = await service.GetCustomerAsync(id, cancellationToken);
    return Ok(customer);
}
```

**Reference**: See `checklists/04-performance.md` - Blocking on Async Code section
```

### Example 4: .editorconfig Violation

```markdown
🔴 CRITICAL - Code Style: File-Scoped Namespace Violation

**Issue**: Lines 6-12 in `Customer.cs` use block-scoped namespace syntax, violating `.editorconfig` error-level rule.

```csharp
namespace MyApp.Domain.Model {
    public class Customer { }
}
```

**Why This Matters**: This violates the project's `.editorconfig` rule `csharp_style_namespace_declarations = file_scoped:error`. This will:
- Fail the build in CI/CD
- Block PR merge
- Create inconsistency in the codebase

**Suggested Fix**:
```csharp
// 🔴 WRONG: Block-scoped namespace (ERROR level violation)
namespace MyApp.Domain.Model {
    public class Customer { }
}

// ✅ CORRECT: File-scoped namespace (MANDATORY)
namespace MyApp.Domain.Model;

public class Customer { }
```

**Auto-fix**: Run `dotnet format` to automatically fix this violation.

**Reference**: See `examples/editorconfig-compliance.md` - File-Scoped Namespaces section
```

---

## 🟡 IMPORTANT Comment Template

Use for issues that **should** be addressed but may not block merge if there's a valid reason to defer.

### Template

```markdown
🟡 IMPORTANT - {Category}: {Brief Title}

**Issue**: {Specific description of the problem}

**Why This Matters**: {Explain impact - maintainability, test coverage, architecture}

**Suggested Fix**:
```csharp
// 🟡 CURRENT:
{current code}

// ✅ IMPROVED:
{improved code}
```

**Reference**: {Link to checklist or examples}
```

### Example 5: Missing Tests

```markdown
🟡 IMPORTANT - Testing: No Tests for New Feature

**Issue**: The new `Customer.Activate()` method added in this PR has no unit tests.

**Why This Matters**: Missing tests for new functionality means:
- Regressions can go undetected
- Business logic is not verified
- Future refactoring is risky
- Coverage decreases

**Suggested Fix**:

Add tests covering:
1. **Happy path**: Customer can be activated when valid
2. **Already active**: Returns failure when already active
3. **Missing email**: Returns failure when email is null
4. **Domain event**: Verifies `CustomerActivatedDomainEvent` is registered

**Example Test**:
```csharp
[Fact]
public void Should_ActivateCustomer_When_CustomerIsLead()
{
    // Arrange
    var customer = Customer.Create("John", "Doe", "john@example.com").Value;
    
    // Act
    var result = customer.Activate();
    
    // Assert
    result.IsSuccess.ShouldBeTrue();
    customer.Status.ShouldBe(CustomerStatus.Active);
    customer.DomainEvents.ShouldContain(e => e is CustomerActivatedDomainEvent);
}
```

**Reference**: See `checklists/03-testing.md` - Test Coverage section
```

### Example 6: N+1 Query Problem

```markdown
🟡 IMPORTANT - Performance: N+1 Query Problem

**Issue**: Lines 45-52 in `CustomerService.cs` execute a separate query for each customer's orders.

```csharp
foreach (var customer in customers)
{
    var orders = await context.Orders
        .Where(o => o.CustomerId == customer.Id)
        .ToListAsync();
}
```

**Why This Matters**: For 100 customers, this executes 101 database queries (1 for customers + 100 for orders). This:
- Causes severe performance degradation
- Increases database load
- Increases latency (network round-trips)

**Suggested Fix**:
```csharp
// 🟡 CURRENT: N+1 queries (101 queries for 100 customers)
var customers = await context.Customers.ToListAsync();
foreach (var customer in customers)
{
    var orders = await context.Orders
        .Where(o => o.CustomerId == customer.Id)
        .ToListAsync();
}

// ✅ IMPROVED: Single query with Include (1 query total)
var customers = await context.Customers
    .Include(c => c.Orders)
    .ToListAsync();

// Or with projection if only need count:
var customerData = await context.Customers
    .Select(c => new
    {
        Customer = c,
        OrderCount = c.Orders.Count
    })
    .ToListAsync();
```

**Reference**: See `checklists/04-performance.md` - Database Query Optimization section
```

---

## 🟢 SUGGESTION Comment Template

Use for non-blocking improvements that enhance code quality.

### Template

```markdown
🟢 SUGGESTION - {Category}: {Brief Title}

**Observation**: {What could be improved}

**Benefit**: {How this improves the code}

**Suggested Improvement**:
```csharp
// 🟢 CURRENT (works but could be better):
{current code}

// ✅ IMPROVED (cleaner/more modern):
{improved code}
```

**Reference**: {Link to examples or documentation}
```

### Example 7: Modern C# Features

```markdown
🟢 SUGGESTION - Readability: Could Use Expression-Bodied Member

**Observation**: Line 34 in `Customer.cs` uses a traditional method body for a simple return.

```csharp
public string GetFullName()
{
    return $"{this.FirstName} {this.LastName}";
}
```

**Benefit**: Expression-bodied syntax makes simple methods more concise and readable.

**Suggested Improvement**:
```csharp
// 🟢 CURRENT (works but verbose):
public string GetFullName()
{
    return $"{this.FirstName} {this.LastName}";
}

// ✅ IMPROVED (more concise):
public string GetFullName() => $"{this.FirstName} {this.LastName}";
```

**Reference**: See `examples/clean-code-examples.md` - Example 7: Expression-Bodied Methods
```

### Example 8: Missing XML Documentation

```markdown
🟢 SUGGESTION - Documentation: Missing XML Documentation on Public API

**Observation**: The public method `CalculateDiscount` in `PricingService.cs` has no XML documentation comments.

**Benefit**: XML documentation:
- Enables IntelliSense for consumers
- Documents expected behavior
- Explains parameters and return values
- Makes API self-documenting

**Suggested Improvement**:
```csharp
// 🟢 CURRENT: No documentation
public decimal CalculateDiscount(Customer customer)
{
    return customer.OrderCount > 10 ? 0.15m : 0.05m;
}

// ✅ IMPROVED: Complete XML documentation
/// <summary>
/// Calculates the discount rate for a customer based on their order history.
/// </summary>
/// <param name="customer">The customer to calculate discount for.</param>
/// <returns>
/// Discount rate as a decimal (0.15 for loyal customers with >10 orders, 0.05 otherwise).
/// </returns>
/// <remarks>
/// Business rule: Customers with more than 10 orders are considered "loyal customers"
/// and receive 15% discount. Standard customers receive 5% discount.
/// </remarks>
public decimal CalculateDiscount(Customer customer)
{
    const int LOYAL_CUSTOMER_THRESHOLD = 10;
    const decimal LOYAL_DISCOUNT_RATE = 0.15m;
    const decimal STANDARD_DISCOUNT_RATE = 0.05m;
    
    return customer.OrderCount > LOYAL_CUSTOMER_THRESHOLD 
        ? LOYAL_DISCOUNT_RATE 
        : STANDARD_DISCOUNT_RATE;
}
```

**Reference**: See `checklists/05-documentation.md` - XML Documentation section
```

---

## Summary

Review comment templates ensure consistency:

✅ **🔴 CRITICAL**: Security, correctness, .editorconfig ERROR violations - **must fix before merge**  
✅ **🟡 IMPORTANT**: Test coverage, architecture, performance - **discuss with author**  
✅ **🟢 SUGGESTION**: Readability, documentation, modern C# - **non-blocking improvements**  

**Every comment should include**:
1. Priority indicator (🔴🟡🟢)
2. Category and brief title
3. Specific issue description (file, line, code)
4. Why it matters (impact explanation)
5. Suggested fix with code examples (WRONG vs CORRECT)
6. Reference to checklist/examples/documentation

**Example workflow**:
1. Identify issue during review
2. Choose appropriate template based on severity
3. Fill in specific details
4. Provide corrected code example
5. Link to relevant documentation

This ensures all review comments are:
- **Specific** (exact location and code)
- **Contextual** (explains impact)
- **Actionable** (shows how to fix)
- **Educational** (links to resources)
