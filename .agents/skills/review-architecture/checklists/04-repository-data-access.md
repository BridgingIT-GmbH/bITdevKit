# Checklist: Repository & Data Access

This checklist helps verify proper use of repository abstractions and data access patterns in the application layer.

## Repository Abstraction (🔴 CRITICAL)

**ADR-0004 (Repository Pattern with Decorator Behaviors)**: The repository pattern provides an abstraction over data access that enables the application layer to remain independent of the persistence mechanism. Decorator behaviors (logging, audit, domain events) are applied transparently through the repository interface.

### Checklist

- [ ] Application handlers inject `IGenericRepository<TEntity>`, NOT DbContext
- [ ] No `using Microsoft.EntityFrameworkCore` in Application layer
- [ ] No EF Core-specific methods (`.Include()`, `.AsNoTracking()`, `.FromSqlRaw()`) in Application layer
- [ ] Repository methods used: `InsertAsync()`, `UpdateAsync()`, `DeleteAsync()`, `FindAllAsync()`, `FindOneAsync()`
- [ ] Complex queries use specifications (not inline LINQ)

### Example: Correct Repository Usage

```csharp
// ✅ CORRECT: Application uses repository abstraction
namespace MyApp.Application.Commands;

using BridgingIT.DevKit.Domain.Repositories; // ✅ Domain abstraction
using MyApp.Domain.CustomerAggregate; // ✅ Domain reference

public class CustomerCreateCommandHandler : RequestHandlerBase<CustomerCreateCommand, Result<CustomerModel>>
{
    private readonly IGenericRepository<Customer> repository; // ✅ Repository abstraction
    private readonly IMapper mapper;

    public CustomerCreateCommandHandler(
        IGenericRepository<Customer> repository, // ✅ Inject abstraction
        IMapper mapper)
    {
        this.repository = repository;
        this.mapper = mapper;
    }

    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken cancellationToken)
    {
        var entityResult = Customer.Create(...); // ✅ Delegate to domain

        if (entityResult.IsFailure)
        {
            return entityResult.Unwrap();
        }

        await this.repository.InsertAsync(entityResult.Value, cancellationToken); // ✅ Repository method

        var model = this.mapper.Map<Customer, CustomerModel>(entityResult.Value);
        return Result<CustomerModel>.Success(model);
    }
}
```

### Common Violations

```csharp
// ❌ WRONG: DbContext in Application layer
namespace MyApp.Application.Commands;

using MyApp.Infrastructure.EntityFramework; // ❌ Application → Infrastructure dependency

public class CustomerCreateCommandHandler
{
    private readonly CoreModuleDbContext context; // ❌ Direct DbContext

    public CustomerCreateCommandHandler(CoreModuleDbContext context)
    {
        this.context = context;
    }

    public override async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = Customer.Create(...);
        this.context.Customers.Add(entity.Value); // ❌ EF Core API in Application
        await this.context.SaveChangesAsync(cancellationToken);
        return Result<CustomerModel>.Success(...);
    }
}
```

**Reference**: ADR-0001 (Clean/Onion Architecture), ADR-0004

## Specification Pattern (🟡 IMPORTANT)

**ADR-0019 (Specification Pattern for Repository Queries)**: Specifications encapsulate query logic into reusable, testable, composable objects. They enable complex queries without leaking EF Core details into the application layer.

### Checklist

- [ ] Complex queries (filtering, sorting, paging, includes) use specifications
- [ ] Specifications implement `ISpecification<T>`
- [ ] Specifications located in `<Module>.Application/Specifications/` or `<Module>.Domain/Specifications/`
- [ ] Used with repository: `repository.FindAllAsync(specification, cancellationToken)`

### Example: Specification Usage

```csharp
// ✅ CORRECT: Specification for complex query
public class CustomersByStatusSpecification : Specification<Customer>
{
    public CustomersByStatusSpecification(CustomerStatus status)
    {
        this.AddExpression(c => c.Status == status);
        this.AddInclude(c => c.Addresses); // ✅ Include navigation property
        this.AddOrdering(c => c.LastName);
    }
}

// ✅ Use in handler
public class CustomerFindAllQueryHandler
{
    public override async Task<Result<IEnumerable<CustomerModel>>> Handle(
        CustomerFindAllQuery request,
        CancellationToken cancellationToken)
    {
        var specification = new CustomersByStatusSpecification(CustomerStatus.Active);
        var entities = await this.repository.FindAllAsync(specification, cancellationToken);

        var models = this.mapper.Map<IEnumerable<Customer>, IEnumerable<CustomerModel>>(entities);
        return Result<IEnumerable<CustomerModel>>.Success(models);
    }
}
```

### Common Violations

```csharp
// ❌ WRONG: Inline LINQ query with EF Core specifics
public class CustomerFindAllQueryHandler
{
    public override async Task<Result<IEnumerable<CustomerModel>>> Handle(
        CustomerFindAllQuery request,
        CancellationToken cancellationToken)
    {
        // ❌ Inline query, hard to reuse and test
        var entities = await this.repository.FindAllAsync(
            c => c.Status == CustomerStatus.Active,
            cancellationToken);

        // ❌ Missing includes (potential N+1 problem)
        return Result<IEnumerable<CustomerModel>>.Success(...);
    }
}
```

**Reference**: ADR-0019

## N+1 Query Detection (🟡 IMPORTANT)

**ADR-0007 (Entity Framework Core with Code-First Migrations)**: N+1 query problems occur when navigation properties are accessed without eager loading, causing multiple database round-trips.

### Checklist

- [ ] Navigation properties loaded eagerly via specifications (`.AddInclude()`)
- [ ] No lazy loading enabled (`UseLazyLoadingProxies()` not used)
- [ ] Queries with navigation properties reviewed for performance
- [ ] Use profiling/logging to detect N+1 issues

### Example: N+1 Problem

```csharp
// ❌ WRONG: N+1 query problem
var customers = await repository.FindAllAsync(cancellationToken: ct);

foreach (var customer in customers)
{
    // ❌ Each iteration triggers a separate query for Addresses
    var addresses = customer.Addresses.ToList();
    foreach (var address in addresses)
    {
        Console.WriteLine(address.Line1);
    }
}
```

### Fix: Eager Loading

```csharp
// ✅ CORRECT: Eager loading with specification
public class CustomersWithAddressesSpecification : Specification<Customer>
{
    public CustomersWithAddressesSpecification()
    {
        this.AddInclude(c => c.Addresses); // ✅ Eager load Addresses
    }
}

var specification = new CustomersWithAddressesSpecification();
var customers = await repository.FindAllAsync(specification, cancellationToken: ct);

// ✅ No additional queries; Addresses already loaded
foreach (var customer in customers)
{
    var addresses = customer.Addresses.ToList();
    foreach (var address in addresses)
    {
        Console.WriteLine(address.Line1);
    }
}
```

**Reference**: ADR-0007, ADR-0019

## EF Core Configuration (Infrastructure Layer Only)

**ADR-0007**: Entity Framework Core configuration belongs exclusively in the Infrastructure layer.

### Checklist (for Infrastructure layer reviews)

- [ ] DbContext in `<Module>.Infrastructure/EntityFramework/`
- [ ] Entity configurations in `<Module>.Infrastructure/EntityFramework/Configurations/`
- [ ] Entity configurations implement `IEntityTypeConfiguration<T>`
- [ ] Migrations in `<Module>.Infrastructure/EntityFramework/Migrations/`
- [ ] No EF Core attributes in Domain layer ([Table], [Column], [Index])

### Example: Entity Configuration

```csharp
// ✅ CORRECT: EF Core configuration in Infrastructure
namespace MyApp.Infrastructure.EntityFramework.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.CustomerAggregate;

public class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(256);

        // ✅ Value object mapping
        builder.OwnsOne(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnName("Email");
        });

        // ✅ Navigation property configuration
        builder.HasMany(e => e.Addresses)
            .WithOne()
            .HasForeignKey("CustomerId")
            .OnDelete(DeleteBehavior.Cascade);

        // ✅ Index
        builder.HasIndex(e => e.Email);
    }
}
```

**Reference**: ADR-0007

## Repository Registration

**ADR-0004**: Repositories are registered with decorator behaviors for cross-cutting concerns.

### Checklist (for Infrastructure module registration)

- [ ] Repository registered via `.AddEntityFrameworkRepository<TEntity, TDbContext>()`
- [ ] Decorator behaviors chained: `.WithBehavior<RepositoryLoggingBehavior<T>>()`
- [ ] Typical decorators: logging, audit, domain events, tracing

### Example: Repository Registration

```csharp
// ✅ CORRECT: Repository registration with decorators
services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()
    .WithBehavior<RepositoryAuditBehavior<Customer>>()
    .WithBehavior<RepositoryDomainEventBehavior<Customer>>()
    .WithBehavior<RepositoryTracingBehavior<Customer>>();
```

**Reference**: ADR-0004

## Summary

**Repository abstractions are CRITICAL** for maintaining layer boundaries and enabling testability. Using DbContext directly in the application layer violates the Clean Architecture dependency rule.

**Key takeaways**:
- **Application layer**: Use `IGenericRepository<T>`, NOT DbContext
- **Specifications**: Encapsulate complex queries for reuse and testability
- **N+1 queries**: Use eager loading with `.AddInclude()` in specifications
- **EF Core configuration**: Belongs in Infrastructure layer only
- **Repository registration**: Use decorator behaviors for cross-cutting concerns

**ADRs Referenced**:
- **ADR-0001**: Clean/Onion Architecture with Strict Layer Boundaries
- **ADR-0004**: Repository Pattern with Decorator Behaviors
- **ADR-0007**: Entity Framework Core with Code-First Migrations
- **ADR-0019**: Specification Pattern for Repository Queries
