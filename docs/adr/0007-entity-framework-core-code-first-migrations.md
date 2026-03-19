# ADR-0007: Entity Framework Core with Code-First Migrations

## Status

Accepted

## Context

The application requires a robust, type-safe Object-Relational Mapping (ORM) solution to persist domain aggregates and value objects to a relational database. The ORM must support:

### Technical Requirements

- **Type safety**: Compile-time checked queries and mappings to prevent runtime errors
- **Migration support**: Evolve database schema over time without manual SQL scripting
- **Modular isolation**: Each module manages its own database context and migrations independently
- **Performance**: Efficient query translation, connection pooling, change tracking
- **Value object mapping**: Convert domain value objects (e.g., `CustomerId`, `EmailAddress`) to database primitives
- **Complex mappings**: Support owned entities, inheritance, enumerations, sequences

### Business Requirements

- **Developer productivity**: Minimize boilerplate for CRUD operations and schema management
- **Maintainability**: Keep schema definition close to domain model for easier refactoring
- **Testability**: Support in-memory provider for fast integration tests
- **Schema versioning**: Track database changes explicitly via version-controlled migration files

### Design Challenges

- **Code-first vs Database-first**: Should schema be generated from code or code generated from schema?
- **Migration automation**: Should migrations apply automatically in development vs production?
- **DbContext scope**: Should there be one DbContext per module or shared across modules?
- **Convention vs Configuration**: Balance between magic conventions and explicit configuration

### Related Decisions

- **ADR-0001**: Clean/Onion Architecture dictates Infrastructure layer owns EF Core (not Domain)
- **ADR-0003**: Modular Monolith requires each module to have isolated DbContext
- **ADR-0004**: Repository pattern abstracts EF Core behind `IRepository<T>` interface
- **ADR-0012**: Domain layer defines entities; Infrastructure layer provides EF Core mappings

## Decision

Use **Entity Framework Core (EF Core)** as the primary ORM with **code-first migrations** and **one DbContext per module**.

### How It Works

#### 1. DbContext Per Module

Each module defines its own `DbContext` inheriting from `ModuleDbContextBase`:

```csharp
// src/Modules/CoreModule/CoreModule.Infrastructure/EntityFramework/CoreModuleDbContext.cs
public class CoreModuleDbContext(DbContextOptions<CoreModuleDbContext> options)
    : ModuleDbContextBase(options), IOutboxDomainEventContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(CodeModuleConstants.CustomerNumberSequenceName)
            .StartsAt(100000);
        base.OnModelCreating(modelBuilder); // applies configurations from assembly
    }
}
```

#### 2. Entity Type Configurations

Separate configuration classes define mappings using Fluent API:

```csharp
// src/Modules/CoreModule/CoreModule.Infrastructure/EntityFramework/Configurations/CustomerTypeConfiguration.cs
public class CustomerTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers")
            .HasKey(x => x.Id).IsClustered(false);

        // Value object conversions
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,                      // to database
                value => CustomerId.Create(value));  // from database

        builder.Property(d => d.Number)
            .HasConversion(
                number => number.Value,
                value => CustomerNumber.Create(value).Value)
            .HasMaxLength(256);

        // Owned entity (separate table)
        builder.OwnsMany(c => c.Addresses, ab =>
        {
            ab.ToTable("CustomersAddresses");
            ab.WithOwner().HasForeignKey("CustomerId");
            ab.HasKey(a => a.Id);
            // ... property configurations
        });

        // Audit properties (created/updated dates)
        builder.OwnsOneAuditState();
    }
}
```

#### 3. Design-Time DbContext Factory

Factory enables `dotnet ef` CLI tools to create DbContext during migration generation:

```csharp
// src/Modules/CoreModule/CoreModule.Infrastructure/EntityFramework/CoreModuleDbContextFactory.cs
public class CoreModuleDbContextFactory : SqlServerModuleDbContextFactory<CoreModuleDbContext>
{
    public CoreModuleDbContextFactory()
        : base(
            options: (builder, connectionString) =>
                builder.UseSqlServer(
                    connectionString,
                    sqlOptions => sqlOptions.MigrationsAssembly(
                        typeof(CoreModuleDbContext).Assembly.GetName().Name)))
    {
    }
}
```

#### 4. Module Registration

Register DbContext and migration services in module startup:

```csharp
// src/Modules/CoreModule/CoreModule.Presentation/CoreModuleModule.cs (lines 48-61)
services.AddSqlServerDbContext<CoreModuleDbContext>(o => o
        .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
        .UseLogger(true, true))
    .WithSequenceNumberGenerator()
    .WithDatabaseMigratorService(o => o
        .Enabled(environment.IsLocalDevelopment() || environment.IsContainerized()))
    .WithOutboxDomainEventService(o => o
        .ProcessingInterval("00:00:30")
        .ProcessingModeImmediate()
        .StartupDelay("00:00:15")
        .PurgeOnStartup());
```

#### 5. Code-First Migration Workflow

**Development Environment (Automatic)**:

```bash
# Add new migration
dotnet ef migrations add AddCustomerPhone --project CoreModule.Infrastructure

# Migrations auto-apply on startup via DatabaseMigratorService
# (enabled only in local/containerized environments)
```

**Production Environment (Manual)**:

```bash
# Generate SQL script for review
dotnet ef migrations script --idempotent --output migration.sql

# Apply via deployment pipeline (not on app startup)
```

#### 6. Generated Migrations

EF Core creates strongly-typed migration classes:

```csharp
// Migrations/20260112224142_AddCustomersAddresses.cs (excerpt)
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "CustomersAddresses",
        schema: "core",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            Line1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            // ...
            CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_CustomersAddresses", x => x.Id);
            table.ForeignKey("FK_CustomersAddresses_Customers_CustomerId",
                x => x.CustomerId, principalTable: "Customers", onDelete: ReferentialAction.Cascade);
        });
}
```

### Key Configuration Patterns

- **Value Object Conversions**: Use `HasConversion<TValueObject>` to map to primitives
- **Owned Entities**: Use `OwnsOne`/`OwnsMany` for value objects that need separate tables
- **Enumeration Support**: Use `EnumerationConverter<T>` for Enumeration pattern
- **Audit State**: Use `.OwnsOneAuditState()` extension to add CreatedDate/UpdatedDate
- **Sequences**: Configure via `modelBuilder.HasSequence<T>()` for sequential IDs
- **Concurrency Tokens**: Use `IsConcurrencyToken()` for optimistic concurrency

## Rationale

### Why EF Core Over Alternatives?

1. **Mature .NET Integration**: First-class support for .NET types, LINQ, async/await, dependency injection
2. **Strong Typing**: Compile-time query validation prevents SQL injection and typos
3. **Change Tracking**: Automatic detection of modified entities simplifies update logic
4. **Migration System**: Version-controlled schema evolution with rollback support
5. **Flexible Providers**: SQL Server today; swap to PostgreSQL/SQLite without application code changes
6. **Testability**: In-memory provider enables fast integration tests without database
7. **Community & Tooling**: Extensive documentation, Visual Studio integration, CLI tools

### Why Code-First Over Database-First?

1. **Domain-Driven Design Alignment**: Domain entities are source of truth; database is persistence detail
2. **Refactoring Safety**: Rename properties in C# → migration reflects change automatically
3. **Version Control**: Migration files track schema changes alongside code changes
4. **Testability**: In-memory provider works seamlessly with code-first models
5. **Cross-Platform**: No dependency on database-specific designer tools

### Why DbContext Per Module?

1. **Bounded Context Isolation**: Each module owns its schema; no cross-module table dependencies
2. **Independent Deployability**: Modules can evolve database schemas independently
3. **Migration Independence**: Adding module doesn't require migrating existing modules
4. **Schema Namespacing**: Each module uses its own database schema (e.g., `core.Customers`)

### Why Automatic Migrations in Dev Only?

1. **Developer Productivity**: Local changes auto-apply; no manual migration steps
2. **Production Safety**: Manual review catches breaking changes before deployment
3. **Rollback Control**: Production migrations can be rolled back via generated SQL scripts

## Consequences

### Positive

- **Productivity**: Developers write C# classes, not SQL DDL scripts
- **Type Safety**: Compile-time errors catch schema mismatches before runtime
- **Maintainability**: Schema changes tracked in source control via migration files
- **Testability**: In-memory provider enables fast integration tests (no SQL Server required)
- **Flexibility**: Switch database providers (SQL Server → PostgreSQL) without code changes
- **Consistency**: Fluent API ensures all modules follow same mapping conventions
- **Tooling**: Visual Studio and CLI tools simplify migration generation and review

### Negative

- **Learning Curve**: Developers must learn EF Core Fluent API and migration system
- **Magic Conventions**: EF Core applies conventions (e.g., cascading deletes) that may surprise developers
- **Performance Overhead**: Change tracking and query translation add CPU/memory cost vs raw SQL
- **Migration Conflicts**: Multiple developers creating migrations simultaneously can cause merge conflicts
- **Circular Dependencies**: If not careful, modules can reference each other's DbContexts (violates isolation)
- **N+1 Query Risk**: Lazy loading disabled by default; developers must use `.Include()` explicitly

### Neutral

- **Vendor Lock-In (Mitigated)**: Tied to EF Core API, but can swap database providers
- **Generated SQL Control**: EF Core generates SQL; developers lose fine-grained optimization control (can use raw SQL when needed)
- **Migration Size**: Many migrations over time can slow down database creation (can be squashed)

## Alternatives Considered

### 1. Dapper (Micro-ORM)

**Description**: Lightweight ORM providing simple mapping from SQL results to C# objects.

**Pros**:

- Performance: Minimal overhead; very fast queries
- Control: Write raw SQL for complex queries
- Simplicity: No change tracking or magic behavior

**Cons**:

- **No migrations**: Must write SQL DDL scripts manually
- **No type safety**: Queries are strings; typos caught at runtime
- **Boilerplate**: Manual mapping code for each query
- **No change tracking**: Must manually detect entity modifications

**Rejected Because**: Requires manual SQL migrations and lacks type safety, increasing maintenance burden.

### 2. NHibernate

**Description**: Mature ORM with extensive mapping capabilities and HQL query language.

**Pros**:

- Feature-rich: Second-level cache, lazy loading, complex mappings
- Mature: Battle-tested in enterprise applications

**Cons**:

- **XML configuration**: Mapping via XML files (less refactor-friendly)
- **Learning curve**: More complex API than EF Core
- **Community**: Smaller .NET community compared to EF Core
- **Tooling**: Fewer Visual Studio integrations

**Rejected Because**: EF Core provides equivalent features with better .NET ecosystem integration and LINQ support.

### 3. Database-First with EF Core

**Description**: Generate C# classes from existing database schema.

**Pros**:

- DBA Control: Database experts design schema; developers consume it
- Existing Schema: Works well when integrating with legacy databases

**Cons**:

- **Code regeneration**: Schema changes require regenerating C# classes
- **Domain alignment**: Generated classes may not match domain model structure
- **Value objects**: Hard to map value objects (e.g., `EmailAddress`) from scalar columns
- **Version control**: Difficult to track schema changes alongside code changes

**Rejected Because**: Conflicts with Domain-Driven Design principle that domain model is source of truth.

### 4. Manual ADO.NET

**Description**: Use `SqlConnection`, `SqlCommand`, and `DataReader` directly.

**Pros**:

- Full control: Write exact SQL for optimal performance
- No dependencies: No ORM framework required

**Cons**:

- **Massive boilerplate**: Manual connection management, parameter binding, result mapping
- **No migrations**: Must write SQL DDL scripts manually
- **SQL injection risk**: Prone to security vulnerabilities if not careful
- **Maintenance burden**: Huge amount of repetitive code

**Rejected Because**: Dramatically reduces developer productivity with no significant benefit for typical CRUD operations.

## Related Decisions

- **ADR-0001**: Clean/Onion Architecture - EF Core configurations live in Infrastructure layer
- **ADR-0003**: Modular Monolith Architecture - Each module has isolated DbContext
- **ADR-0004**: Repository Decorator Behaviors - Repository abstracts EF Core from Application layer
- **ADR-0006**: Outbox Pattern - Outbox events stored in same DbContext as aggregates
- **ADR-0008**: Typed Entity IDs - Value object conversions enable typed IDs in database
- **ADR-0012**: Domain Logic in Domain Layer - Domain defines entities; Infrastructure provides mappings

## References

- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [EF Core Migrations Overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [bITdevKit DbContext Extensions](https://github.com/BridgingIT-GmbH/bITdevKit/tree/main/docs)
- Project Documentation: `README.md` (sections: Database Setup, EF Core Migrations)
- Module Documentation: `src/Modules/CoreModule/CoreModule-README.md` (Infrastructure section)

## Notes

### Key Implementation Files

```
src/Modules/CoreModule/CoreModule.Infrastructure/
├── EntityFramework/
│   ├── CoreModuleDbContext.cs                     # DbContext definition
│   ├── CoreModuleDbContextFactory.cs              # Design-time factory for migrations
│   ├── Configurations/
│   │   └── CustomerTypeConfiguration.cs           # Fluent API mappings
│   └── Migrations/
│       ├── 20260109201723_Initial.cs              # Initial schema creation
│       ├── 20260112224142_AddCustomersAddresses.cs# Add Addresses table
│       └── CoreModuleDbContextModelSnapshot.cs    # Current schema snapshot
```

### Common Tasks

**Add Migration**:

```bash
pwsh -NoProfile -File .\bdk.ps1 -Task ef-migration-add -MigrationName AddCustomerPhone
```

**Apply Migrations**:

```bash
pwsh -NoProfile -File .\bdk.ps1 -Task ef-apply
```

**Update Database (Manual)**:

```bash
dotnet ef database update --project src/Modules/CoreModule/CoreModule.Infrastructure
```

**Generate SQL Script**:

```bash
dotnet ef migrations script --idempotent --output migration.sql --project CoreModule.Infrastructure
```

### Value Object Mapping Pattern

```csharp
// Value object in domain
public class EmailAddress : ValueObject
{
    public string Value { get; }
    private EmailAddress(string value) => Value = value;
    public static Result<EmailAddress> Create(string value) { /* validation */ }
}

// EF Core configuration
builder.Property(x => x.Email)
    .HasConversion(
        email => email.Value,                // C# → Database
        value => EmailAddress.Create(value).Value) // Database → C#
    .HasMaxLength(256);
```

### Migration Strategy by Environment

| Environment | Auto-Apply | Approval Process | Rollback Method |
|------------|-----------|------------------|-----------------|
| Local Development | V Yes (on startup) | None | Delete database, rerun |
| Containerized/Docker | V Yes (on startup) | None | Recreate container |
| CI/CD Pipeline | X No | PR review required | Git revert migration file |
| Staging | X No | Manual review | Run `Down()` migration |
| Production | X No | DBA approval required | Idempotent rollback script |

### Testing Strategy

- **Unit Tests**: Use in-memory provider (`UseInMemoryDatabase()`)
- **Integration Tests**: Use SQL Server test container or LocalDB
- **Repository Tests**: Mock `IRepository<T>` interface (don't test EF Core itself)

### Performance Considerations

- **Change Tracking**: Disabled for read-only queries (`.AsNoTracking()`)
- **Projections**: Use `.Select()` to load only needed columns
- **Eager Loading**: Use `.Include()` to avoid N+1 queries
- **Compiled Queries**: Cache frequently-used queries via `EF.CompileQuery()`
- **Connection Pooling**: Enabled by default; configure via connection string

### Schema Ownership

- Each module schema prefixed with module name (e.g., `core.Customers`)
- Shared tables (e.g., `__EFMigrationsHistory`) live in default schema
- Outbox tables live in module schema alongside aggregates

### Future Considerations

- **Multi-Tenancy**: Add tenant ID to queries via global query filters
- **Read Models**: Consider separate read-optimized DbContext for queries (CQRS)
- **Schema Squashing**: Periodically squash old migrations to reduce migration count
- **Database Sharding**: If scale requires, partition DbContext by aggregate root
