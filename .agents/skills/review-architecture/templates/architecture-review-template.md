# Architecture Review Comment Template

Use this template when providing architecture review feedback. Format feedback as comments with clear priority, issue description, rationale, suggested fix, and ADR reference.

## Template Structure

```
[PRIORITY] Category: Brief Title

**Location**: `file_path:line_number`

**Issue**: [Description of what's wrong]

**Why This Matters**: [Explanation of architectural impact referencing ADR]

**Suggested Fix**: [Specific recommendation with code example if applicable]

**Reference**: ADR-XXXX (Title)
```

---

## Example 1: Layer Boundary Violation (🔴 CRITICAL)

```
🔴 Layer Boundaries: Application Layer Uses DbContext Directly

**Location**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommandHandler.cs:15`

**Issue**: Handler injects `CoreModuleDbContext` directly and uses EF Core API (`context.Customers.Add()`), violating layer boundary rules.

**Why This Matters**: ADR-0001 (Clean/Onion Architecture) requires that the Application layer depend only on Domain abstractions, never Infrastructure. Direct DbContext usage creates tight coupling to EF Core and prevents the application from being tested independently of database infrastructure. This also violates ADR-0004 (Repository Pattern), which requires repository abstractions for data access.

**Suggested Fix**: Replace DbContext with `IGenericRepository<Customer>` abstraction:

```csharp
// ❌ WRONG
private readonly CoreModuleDbContext context;

public async Task<Result<CustomerId>> Handle(...)
{
    this.context.Customers.Add(customer);
    await this.context.SaveChangesAsync(ct);
}

// ✅ CORRECT
private readonly IGenericRepository<Customer> repository;

public async Task<Result<CustomerId>> Handle(...)
{
    await this.repository.InsertAsync(customer, ct);
}
```

**Reference**: ADR-0001 (Clean/Onion Architecture), ADR-0004 (Repository Pattern)
```

---

## Example 2: Domain Encapsulation Violation (🔴 CRITICAL)

```
🔴 Domain Patterns: Public Setters on Aggregate Root

**Location**: `src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Customer.cs:40-45`

**Issue**: Customer aggregate exposes public setters on `FirstName`, `LastName`, and `Email` properties, allowing external code to bypass business rules.

**Why This Matters**: ADR-0012 (Domain Logic in Domain Layer) requires aggregates to encapsulate business logic and enforce invariants through private setters and change methods. Public setters allow external code to modify state directly, bypassing validation and business rules. This leads to an anemic domain model where business logic leaks into application/presentation layers.

**Suggested Fix**: Use private setters and change methods returning `Result<T>`:

```csharp
// ❌ WRONG
public string FirstName { get; set; }
public string LastName { get; set; }

// ✅ CORRECT
public string FirstName { get; private set; }
public string LastName { get; private set; }

public Result<Customer> ChangeName(string firstName, string lastName)
{
    return this.Change()
        .Ensure(_ => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName),
            "Invalid name: both first and last name must be provided")
        .Set(e => e.FirstName, firstName)
        .Set(e => e.LastName, lastName)
        .Register(e => new CustomerUpdatedDomainEvent(e))
        .Apply();
}
```

**Reference**: ADR-0012 (Domain Logic in Domain Layer)
```

---

## Example 3: Result Pattern Violation (🔴 CRITICAL)

```
🔴 Error Handling: Exception Thrown for Business Rule Validation

**Location**: `src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Customer.cs:87`

**Issue**: `Create()` method throws `ValidationException` when last name is "notallowed", using exceptions for expected business rule violations.

**Why This Matters**: ADR-0002 (Result Pattern) requires using `Result<T>` for all expected failures (validation errors, business rule violations), reserving exceptions only for truly exceptional circumstances (system failures, bugs). Exceptions for control flow are expensive, obscure success/failure paths in method signatures, and make composition difficult.

**Suggested Fix**: Return `Result<Customer>` with validation failures:

```csharp
// ❌ WRONG
if (lastName == "notallowed")
{
    throw new ValidationException("Invalid last name");
}

// ✅ CORRECT
return Result<Customer>.Success()
    .Ensure(_ => lastName != "notallowed",
        new ValidationError("Invalid last name: 'notallowed' is not permitted"))
    .Bind(_ => new Customer(firstName, lastName, email, number));
```

**Reference**: ADR-0002 (Result Pattern for Error Handling)
```

---

## Example 4: CQRS Naming Violation (🟡 IMPORTANT)

```
🟡 CQRS Patterns: Command Naming Does Not Follow Convention

**Location**: `src/Modules/CoreModule/CoreModule.Application/Commands/CreateCustomerRequest.cs:10`

**Issue**: Command named `CreateCustomerRequest` instead of following `[Entity][Action]Command` pattern (`CustomerCreateCommand`).

**Why This Matters**: ADR-0011 (Application Logic in Commands/Queries) establishes naming conventions for commands and queries to maintain consistency and discoverability across the codebase. Inconsistent naming makes navigation difficult and violates team conventions.

**Suggested Fix**: Rename to `CustomerCreateCommand`:

```csharp
// ❌ WRONG
public class CreateCustomerRequest : RequestBase<Result<CustomerModel>>

// ✅ CORRECT
public class CustomerCreateCommand : RequestBase<Result<CustomerModel>>
```

**Reference**: ADR-0011 (Application Logic in Commands/Queries)
```

---

## Example 5: N+1 Query Problem (🟡 IMPORTANT)

```
🟡 Performance: Potential N+1 Query Problem

**Location**: `src/Modules/CoreModule/CoreModule.Application/Queries/CustomerFindAllQueryHandler.cs:25`

**Issue**: Query retrieves customers without eager loading `Addresses` navigation property, likely causing N+1 queries when addresses are accessed.

**Why This Matters**: N+1 query problems cause excessive database round-trips (one query for customers + N queries for each customer's addresses), severely impacting performance with large datasets. ADR-0019 (Specification Pattern) and ADR-0007 (Entity Framework Core) require using specifications with explicit `.AddInclude()` for navigation properties.

**Suggested Fix**: Create specification with eager loading:

```csharp
// ❌ WRONG
var customers = await repository.FindAllAsync(cancellationToken: ct);
// Each customer.Addresses access triggers separate query

// ✅ CORRECT
public class CustomersWithAddressesSpecification : Specification<Customer>
{
    public CustomersWithAddressesSpecification()
    {
        this.AddInclude(c => c.Addresses); // Eager load
    }
}

var spec = new CustomersWithAddressesSpecification();
var customers = await repository.FindAllAsync(spec, ct);
```

**Reference**: ADR-0019 (Specification Pattern), ADR-0007 (Entity Framework Core)
```

---

## Example 6: Missing OpenAPI Metadata (🟢 SUGGESTION)

```
🟢 Documentation: Missing OpenAPI Metadata on Endpoint

**Location**: `src/Modules/CoreModule/CoreModule.Presentation/Web/Endpoints/CustomerEndpoints.cs:22`

**Issue**: Endpoint missing `.WithName()`, `.WithSummary()`, and `.Produces<T>()` metadata for API documentation.

**Why This Matters**: ADR-0014 (Minimal API Endpoints) recommends including OpenAPI metadata for discoverability, automatic documentation generation (Swagger), and client code generation. While not blocking, this improves developer experience.

**Suggested Fix**: Add OpenAPI metadata:

```csharp
// ❌ MISSING
group.MapPost("", this.CreateCustomerAsync);

// ✅ IMPROVED
group.MapPost("", this.CreateCustomerAsync)
    .WithName("CreateCustomer")
    .WithSummary("Creates a new customer")
    .Produces<CustomerModel>(StatusCodes.Status201Created)
    .ProducesResultProblem();
```

**Reference**: ADR-0014 (Minimal API Endpoints with DTO Exposure)
```

---

## Priority Guidelines

- **🔴 CRITICAL**: Must fix before merge (layer violations, encapsulation breaks, exception misuse)
- **🟡 IMPORTANT**: Should fix soon (CQRS naming, N+1 queries, missing IRequester)
- **🟢 SUGGESTION**: Nice to have (OpenAPI metadata, XML docs)
