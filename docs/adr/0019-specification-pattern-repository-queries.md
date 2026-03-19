# ADR-0019: Specification Pattern for Repository Queries

## Status

Accepted

## Context

Applications frequently need to query data with:

- **Complex Filters**: Multiple conditions combined with AND/OR logic
- **Dynamic Queries**: User-provided filters that vary by request
- **Reusable Conditions**: Same query logic needed across multiple use cases
- **Ordering**: Sort by different fields, ascending or descending
- **Pagination**: Limit results for performance and UX
- **Eager Loading**: Include related entities to avoid N+1 queries
- **Maintainability**: Query logic scattered across repositories, handlers, services

Traditional approaches have issues:

**Direct DbContext/LINQ in Handlers**:

```csharp
var customers = await dbContext.Customers
    .Where(c => c.Status == CustomerStatus.Active)
    .Where(c => c.Email.Contains(searchTerm))
    .OrderBy(c => c.Name)
    .Skip(pageSize * pageNumber)
    .Take(pageSize)
    .ToListAsync();
```

- **Duplication**: Same query logic repeated across handlers
- **Testability**: Hard to unit test without database
- **Coupling**: Handler coupled to EF Core query syntax
- **Maintainability**: Business logic buried in LINQ

**Repository Methods for Every Query**:

```csharp
Task<List<Customer>> FindActiveCustomersByEmail(string email);
Task<List<Customer>> FindCustomersByStatusOrderedByName(CustomerStatus status);
Task<List<Customer>> FindCustomersCreatedAfter(DateTime date, int page, int pageSize);
```

- **Explosion**: One method per query combination
- **Inflexible**: Adding filter requires new method
- **Maintenance**: Changes require modifying repository interface

The application needed a query strategy that:

1. **Encapsulates query logic** in reusable, testable objects
2. **Supports dynamic composition** of filters, ordering, pagination
3. **Maintains abstraction** over data access technology
4. **Enables testing** without database
5. **Reduces duplication** across handlers
6. **Provides type safety** with compile-time checking

## Decision

Adopt the **Specification Pattern** with **FilterModel**, **SpecificationBuilder**, and **repository FindOptions** to encapsulate query logic in composable, reusable, testable specifications.

### FilterModel for User Input

```csharp
public class FilterModel
{
    public string? Filter { get; set; }        // "Status:Active AND Email:*@example.com"
    public string? OrderBy { get; set; }       // "+Name,-CreatedDate"
    public int? Page { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public string? Include { get; set; }       // "Orders,Addresses"
}
```

### Repository FindAllResultAsync with FilterModel

```csharp
public interface IGenericRepository<TEntity> where TEntity : class, IEntity
{
    Task<Result<IEnumerable<TEntity>>> FindAllResultAsync(
        FilterModel filter = null,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<TEntity>>> FindAllResultAsync(
        ISpecification<TEntity> specification,
        FindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);
}
```

### Query Handler Using FilterModel

```csharp
public class CustomerFindAllQueryHandler(
    IGenericRepository<Customer> repository,
    ILogger<CustomerFindAllQueryHandler> logger)
    : IRequestHandler<CustomerFindAllQuery, Result<IEnumerable<CustomerModel>>>
{
    public async Task<Result<IEnumerable<CustomerModel>>> Handle(
        CustomerFindAllQuery request,
        CancellationToken cancellationToken)
    {
        // FilterModel automatically converted to ISpecification + FindOptions
        var result = await repository.FindAllResultAsync(
            request.Filter,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result<IEnumerable<CustomerModel>>.Failure(result.Messages);
        }

        var models = result.Value.Adapt<IEnumerable<CustomerModel>>();
        return Result<IEnumerable<CustomerModel>>.Success(models);
    }
}
```

### Endpoint with FilterModel

```csharp
private async Task<IResult> FindCustomersAsync(
    [FromQuery] FilterModel filter,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    var query = new CustomerFindAllQuery(filter);
    var result = await requester.SendAsync(query, ct);

    return result.MapHttpOkAll();
}

// Usage:
// GET /api/core/customers
// GET /api/core/customers?filter=Status:Active
// GET /api/core/customers?filter=Email:*@example.com&orderBy=+Name&page=2&pageSize=20
```

### SpecificationBuilder for Complex Queries

```csharp
public class ActiveCustomersSpecification : Specification<Customer>
{
    public ActiveCustomersSpecification()
    {
        this.Builder
            .Where(c => c.Status == CustomerStatus.Active)
            .Include(c => c.Orders);
    }
}

// Usage in handler
var specification = new ActiveCustomersSpecification();
var result = await repository.FindAllResultAsync(
    specification,
    new FindOptions<Customer>
    {
        Order = new OrderOption<Customer>(c => c.Name),
        Take = 10
    });
```

### Custom Specification with Parameters

```csharp
public class CustomersByStatusSpecification : Specification<Customer>
{
    public CustomersByStatusSpecification(CustomerStatus status, DateTime? createdAfter = null)
    {
        this.Builder.Where(c => c.Status == status);

        if (createdAfter.HasValue)
        {
            this.Builder.Where(c => c.CreatedDate >= createdAfter.Value);
        }
    }
}

// Usage
var spec = new CustomersByStatusSpecification(
    CustomerStatus.Active,
    DateTime.UtcNow.AddDays(-30));

var result = await repository.FindAllResultAsync(spec);
```

### FindOptions for Ordering, Pagination, Includes

```csharp
var options = new FindOptions<Customer>
{
    // Ordering
    Order = new OrderOption<Customer>(c => c.Name, OrderDirection.Ascending),
    Orders = new[]
    {
        new OrderOption<Customer>(c => c.Status),
        new OrderOption<Customer>(c => c.CreatedDate, OrderDirection.Descending)
    },

    // Pagination
    Skip = 20,
    Take = 10,

    // Eager Loading
    Include = new IncludeOption<Customer>(c => c.Orders),
    Includes = new[]
    {
        new IncludeOption<Customer>(c => c.Addresses),
        new IncludeOption<Customer>(c => c.Orders.Select(o => o.Items))
    },

    // Tracking
    NoTracking = true
};

var result = await repository.FindAllResultAsync(specification, options);
```

### FilterModel String Format

FilterModel.Filter supports query expressions:

```text
// Single condition
Status:Active

// Multiple conditions (AND)
Status:Active AND Email:*@example.com

// OR conditions
Status:Active OR Status:Pending

// Wildcards
Email:*@example.com
Name:John*

// Comparisons
CreatedDate:>2024-01-01
Age:>=18

// Combined
Status:Active AND (Email:*@example.com OR Email:*@test.com)
```

OrderBy format:

```text
+Name           // Ascending
-CreatedDate    // Descending
+Name,-CreatedDate  // Multiple
```

Include format:

```text
Orders
Orders,Addresses
Orders.Items
```

## Rationale

### Why Specification Pattern

1. **Encapsulation**: Query logic encapsulated in single, reusable class
2. **Testability**: Specifications unit testable without database
3. **Composition**: Combine specifications with AND/OR logic
4. **Readability**: Descriptive class names document query intent
5. **Reusability**: Same specification used across handlers, jobs, services
6. **Type Safety**: Compile-time checking of property expressions
7. **Abstraction**: Decouples query logic from data access technology

### Why FilterModel

1. **User-Friendly**: Simple string syntax for API consumers
2. **Flexible**: Supports dynamic filters without code changes
3. **Standardized**: Consistent filtering across all endpoints
4. **Documentation**: OpenAPI documents filter syntax
5. **Conversion**: Automatically converted to ISpecification by repository

### Why Repository Integration

1. **Clean API**: Single `FindAllResultAsync` method supports both approaches
2. **Consistency**: Same Result\<T> pattern across queries
3. **Behaviors**: Decorator behaviors (logging, caching) work uniformly
4. **Abstraction**: Handler doesn't know if specification or FilterModel used

### Why FindOptions Separate from Specification

1. **Separation of Concerns**: Specification = WHAT to filter; FindOptions = HOW to return
2. **Reusability**: Same specification with different ordering/pagination
3. **Flexibility**: Change paging without modifying specification
4. **Testing**: Test specification logic independently of presentation concerns

## Consequences

### Positive

- **Reduced Duplication**: Query logic written once, reused everywhere
- **Improved Testability**: Specifications unit testable with simple asserts
- **Better Maintainability**: Changes to query logic localized to specification
- **Type Safety**: Compiler catches invalid property references
- **Flexibility**: FilterModel enables dynamic user-driven queries
- **Abstraction**: Handlers decoupled from EF Core specifics
- **Composability**: Combine specifications for complex queries
- **Documentation**: Specification class name documents query intent

### Negative

- **Learning Curve**: Team must learn specification pattern
- **Indirection**: More classes/abstractions than direct LINQ
- **Over-Engineering Risk**: Simple queries don't need specifications
- **FilterModel Parsing**: String parsing can fail at runtime (vs. compile-time)

### Neutral

- **Specification Location**: `<Module>.Application/Specifications/` or `<Module>.Domain/Specifications/`
- **FindOptions**: Optional, defaults work for most cases
- **FilterModel**: Opt-in per endpoint (not mandatory for all queries)

## Implementation Guidelines

### When to Use Specifications

**Use specifications for**:

- Complex multi-condition queries
- Reusable query logic across handlers
- Queries that vary by user input
- Domain-driven query concepts (e.g., "EligibleForDiscount")

*Don't use specifications for**:

- Simple single-key lookups: `FindByIdAsync(id)`
- One-off queries used in single handler
- Queries with no business logic (just projections)

### Specification Class Template

```csharp
namespace <Module>.Application.Specifications;

using BridgingIT.DevKit.Domain.Specifications;

/// <summary>
/// Specification for querying customers by status.
/// </summary>
public class CustomersByStatusSpecification : Specification<Customer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomersByStatusSpecification"/> class.
    /// </summary>
    /// <param name="status">The customer status to filter by.</param>
    public CustomersByStatusSpecification(CustomerStatus status)
    {
        this.Builder.Where(c => c.Status == status);
    }
}
```

### Composing Specifications

```csharp
// Combine with AND
var spec = new ActiveCustomersSpecification()
    .And(new CustomersByEmailSpecification("*@example.com"));

// Combine with OR
var spec = new CustomersByStatusSpecification(CustomerStatus.Active)
    .Or(new CustomersByStatusSpecification(CustomerStatus.Pending));

// Negate
var spec = new NotSpecification<Customer>(new InactiveCustomersSpecification());
```

### Using FindOptions

```csharp
// Ordering only
var options = new FindOptions<Customer>
{
    Order = new OrderOption<Customer>(c => c.Name)
};

// Pagination only
var options = new FindOptions<Customer>
{
    Skip = (pageNumber - 1) * pageSize,
    Take = pageSize
};

// Eager loading only
var options = new FindOptions<Customer>
{
    Includes = new[]
    {
        new IncludeOption<Customer>(c => c.Orders),
        new IncludeOption<Customer>(c => c.Addresses)
    }
};

// Combined
var options = new FindOptions<Customer>
{
    Order = new OrderOption<Customer>(c => c.CreatedDate, OrderDirection.Descending),
    Skip = 0,
    Take = 10,
    Include = new IncludeOption<Customer>(c => c.Orders),
    NoTracking = true
};

var result = await repository.FindAllResultAsync(specification, options);
```

### Testing Specifications

```csharp
[Fact]
public void ActiveCustomersSpecification_FiltersCorrectly()
{
    // Arrange
    var spec = new ActiveCustomersSpecification();
    var customers = new[]
    {
        Customer.Create("Active", "active@example.com", CustomerStatus.Active),
        Customer.Create("Inactive", "inactive@example.com", CustomerStatus.Inactive)
    };

    // Act
    var expression = spec.ToExpression();
    var filtered = customers.Where(expression.Compile()).ToList();

    // Assert
    filtered.Should().HaveCount(1);
    filtered[0].Status.Should().Be(CustomerStatus.Active);
}
```

### FilterModel in Integration Tests

```csharp
[Fact]
public async Task FindCustomers_WithStatusFilter_ReturnsMatchingCustomers()
{
    // Arrange
    await this.client.CreateCustomer("Alice", "alice@example.com", CustomerStatus.Active);
    await this.client.CreateCustomer("Bob", "bob@example.com", CustomerStatus.Inactive);

    // Act
    var response = await this.client.GetAsync(
        "/api/core/customers?filter=Status:Active&orderBy=+Name");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var customers = await response.Content.ReadFromJsonAsync<List<CustomerModel>>();
    customers.Should().HaveCount(1);
    customers[0].Name.Should().Be("Alice");
}
```

### Complex FilterModel Queries

```csharp
// Multiple AND conditions
?filter=Status:Active AND Email:*@example.com AND CreatedDate:>2024-01-01

// OR conditions
?filter=Status:Active OR Status:Pending

// Ordering
?orderBy=+Name,-CreatedDate

// Pagination
?page=2&pageSize=20

// Eager loading
?include=Orders,Addresses

// Combined
?filter=Status:Active&orderBy=+Name&page=1&pageSize=10&include=Orders
```

### Repository Method Signature

```csharp
// FilterModel overload (converts to specification internally)
Task<Result<IEnumerable<TEntity>>> FindAllResultAsync(
    FilterModel filter = null,
    CancellationToken cancellationToken = default);

// Specification overload (explicit control)
Task<Result<IEnumerable<TEntity>>> FindAllResultAsync(
    ISpecification<TEntity> specification,
    FindOptions<TEntity> options = null,
    CancellationToken cancellationToken = default);

// Specification with FindOptions from FilterModel
Task<Result<IEnumerable<TEntity>>> FindAllResultAsync(
    ISpecification<TEntity> specification,
    FilterModel filter = null,
    CancellationToken cancellationToken = default);
```

## Alternatives Considered

### Alternative 1: Direct LINQ in Handlers

```csharp
var customers = await dbContext.Customers
    .Where(c => c.Status == CustomerStatus.Active)
    .OrderBy(c => c.Name)
    .Skip(10)
    .Take(10)
    .ToListAsync();
```

**Rejected because**:

- Query logic duplicated across handlers
- Hard to unit test without database
- Handler coupled to EF Core
- No reusability

### Alternative 2: Repository Method Per Query

```csharp
Task<List<Customer>> FindActiveCustomersByEmail(string email);
Task<List<Customer>> FindCustomersByStatusOrderedByName(CustomerStatus status);
Task<List<Customer>> FindCustomersPaged(int page, int pageSize);
```

**Rejected because**:

- Method explosion (combinatorial)
- Inflexible (new query = new method)
- Maintenance burden
- Violates Open/Closed Principle

### Alternative 3: Query Object Pattern

```csharp
public class FindActiveCustomersQuery
{
    public string Email { get; set; }
    public int? Page { get; set; }
}

var query = new FindActiveCustomersQuery { Email = "test@example.com", Page = 1 };
var customers = await repository.ExecuteQuery(query);
```

**Rejected because**:

- Similar to FilterModel but less standardized
- Requires custom query handler infrastructure
- Specification pattern more idiomatic in DDD
- FilterModel provides consistent API across all endpoints

### Alternative 4: GraphQL

**Rejected because**:

- Overkill for CRUD scenarios
- Added complexity (GraphQL server, schema management)
- REST + FilterModel sufficient for requirements
- Team not familiar with GraphQL

## Related Decisions

- [ADR-0004](0004-repository-decorator-behaviors.md): Repository abstraction supports specifications
- [ADR-0011](0011-application-logic-in-commands-queries.md): Query handlers use specifications
- [ADR-0014](0014-minimal-api-endpoints-dto-exposure.md): Endpoints pass FilterModel to queries
- [ADR-0013](0013-unit-testing-high-coverage-strategy.md): Specifications unit testable

## References

- [Specification Pattern (Martin Fowler)](https://www.martinfowler.com/apsupp/spec.pdf)
- [bITdevKit Specifications](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/domain-specifications.md)
- [Repository Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)

## Notes

### Specification Location

**Domain Specifications** (business concepts):

- Location: `src/Modules/<Module>/<Module>.Domain/Specifications/`
- Examples: `EligibleForDiscountSpecification`, `HighValueCustomerSpecification`
- Purpose: Express domain concepts independent of application

**Application Specifications** (query-specific):

- Location: `src/Modules/<Module>/<Module>.Application/Specifications/`
- Examples: `CustomersByStatusSpecification`, `RecentOrdersSpecification`
- Purpose: Support specific application queries

### FilterModel Parsing

FilterModel strings parsed by `SpecificationBuilder`:

```csharp
// Internal conversion (handled by repository)
var specification = SpecificationBuilder<Customer>.Create(filterModel.Filter);
var orderOptions = OrderOptionBuilder<Customer>.Create(filterModel.OrderBy);
var includeOptions = IncludeOptionBuilder<Customer>.Create(filterModel.Include);

var options = new FindOptions<Customer>
{
    Orders = orderOptions,
    Includes = includeOptions,
    Skip = (filterModel.Page - 1) * filterModel.PageSize,
    Take = filterModel.PageSize
};
```

### Performance Considerations

**Efficient**:

```csharp
// Specification composed at handler level, executed as single SQL query
var spec = new ActiveCustomersSpecification();
var result = await repository.FindAllResultAsync(spec);
```

**Inefficient**:

```csharp
// Loading all customers then filtering in memory
var allCustomers = await repository.FindAllAsync();
var filtered = allCustomers.Where(c => c.Status == CustomerStatus.Active);
```

### Common Pitfalls

**Executing specification in memory**:

```csharp
var customers = await repository.FindAllAsync();
var spec = new ActiveCustomersSpecification();
var filtered = customers.Where(spec.ToExpression().Compile()); // In-memory!
```

**Pass specification to repository**:

```csharp
var spec = new ActiveCustomersSpecification();
var result = await repository.FindAllResultAsync(spec); // Executes in database
```

X **Over-abstracting simple queries**:

```csharp
// Don't need specification for simple ID lookup
var spec = new CustomerByIdSpecification(customerId);
var result = await repository.FindAllResultAsync(spec);

// Just use FindByIdAsync
var result = await repository.FindOneResultAsync(customerId);
```

### Best Practices

1. **Name specifications descriptively**: `ActiveCustomersSpecification`, not `Spec1`
2. **Make specifications immutable**: Set conditions in constructor
3. **Test specifications independently**: Unit test without repository
4. **Use FindOptions for presentation concerns**: Don't put ordering in specification
5. **Prefer FilterModel for user-driven queries**: Specifications for business logic
6. **Document FilterModel syntax**: Include examples in OpenAPI docs
7. **Keep specifications focused**: One business concept per specification
