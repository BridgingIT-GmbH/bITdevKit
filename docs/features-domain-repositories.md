# Domain Repositories

> Access aggregates through type-safe repositories with rich querying, paging, and loading options.

[TOC]

## Overview

The Domain Repositories feature provides generic read and write contracts with specifications,
ordering, paging, projection, and eager-loading options. Infrastructure packages implement those
contracts for different stores.

## Challenges

When working with data access layers in domain-driven applications, developers face several challenges:

- **Repetitive CRUD Operations**: Writing the same create, read, update, delete operations for each entity type
- **Complex Include Paths**: Loading nested related entities requires verbose and error-prone string-based paths
- **Type Safety**: Lack of compile-time checking when specifying navigation properties to include
- **Query Composition**: Difficulty in building reusable, composable queries across different contexts
- **Abstraction Leakage**: Data access concerns bleeding into domain logic

## Solution

The repository pattern implementation provides:

- **Generic Repository Interface**: `IGenericRepository<TEntity>` for common CRUD operations
- **FindOptions**: A fluent API for building complex queries with filtering, ordering, paging, and includes
- **Type-Safe Includes**: `IncludeOption<TEntity, TProperty>` with `ThenInclude` support for nested navigation properties
- **Multiple Implementations**: Entity Framework, Cosmos DB, LiteDB, and in-memory repositories
- **Specification Pattern**: Reusable query specifications for complex business rules

This page focuses on how repositories consume specifications. For the specification model itself, see [Domain Specifications](./features-domain-specifications.md).

## Key Features

- Separate read-only and read-write repository contracts
- Specifications and `FindOptions<TEntity>` for query composition
- Typed reference and collection include chains
- Projection, ordering, paging, hierarchy, and distinct options
- Set-based updates and deletes where the provider supports them
- Explicit provider-native bulk insertion with opt-in behaviors
- Optimistic concurrency and sequence-number support

## Architecture

`IGenericReadOnlyRepository<TEntity>` owns query, projection, existence, and count operations.
`IGenericRepository<TEntity>` adds insert, update, upsert, delete, and set-based mutation methods.
`FindOptions<TEntity>` shapes queries while `ISpecification<TEntity>` supplies selection criteria.
Infrastructure implementations translate these contracts to Entity Framework, Cosmos DB, LiteDB,
or an in-memory context. Repository and bulk-inserter behaviors add cross-cutting semantics.

## Use Cases

- Loading entities with deeply nested navigation properties (e.g., Customer → Orders → OrderItems → Product)
- Building reusable query options across different handlers
- Implementing eager loading strategies to avoid N+1 query problems
- Creating type-safe data access layers that prevent runtime errors

## Basic Usage

Use the result-based extensions when a missing entity or provider exception should be handled as a
`Result<TEntity>`:

```csharp
await new CustomerReader(repository)
	.PrintNameAsync(customerId, CancellationToken.None);

public sealed class CustomerReader(IGenericReadOnlyRepository<Customer> repository)
{
	public async Task PrintNameAsync(
		CustomerId customerId,
		CancellationToken cancellationToken)
	{
		var result = await repository.FindOneResultAsync(
			customerId,
			cancellationToken: cancellationToken);

		if (result.IsFailure)
		{
			var details = result.Messages.Concat(
				result.Errors.Select(error => error.Message));
			Console.Error.WriteLine(string.Join(Environment.NewLine, details));
			return;
		}

		Console.WriteLine(result.Value.Name);
	}
}
```

For a stored customer named Ada, the output is:

```text
Ada
```

### Basic repository operations

```csharp
public sealed class CustomerService(IGenericRepository<Customer> repository)
{
    public async Task<Customer> GetCustomerAsync(Guid id, CancellationToken ct)
    {
        return await repository.FindOneAsync(id, cancellationToken: ct);
    }

    public async Task<IEnumerable<Customer>> GetAllCustomersAsync(CancellationToken ct)
    {
        return await repository.FindAllAsync(cancellationToken: ct);
    }
}
```

### Including related entities

Use `IncludeOption` to eagerly load related entities:

```csharp
// Simple include
var options = new FindOptions<Customer>()
    .AddInclude(new IncludeOption<Customer, Address>(c => c.BillingAddress));

var customers = await repository.FindAllAsync(options, cancellationToken);
```

### Nested includes with `ThenInclude`

The `ThenInclude` feature enables typed chaining of navigation properties for loading nested entity
graphs. The repository issues one logical query; the provider can translate it into one or more
database commands, for example when Entity Framework split queries are enabled.

#### Reference navigation properties

For single-reference navigation properties (e.g., Customer → Address → City → Country):

```csharp
var options = new FindOptions<Customer>()
    .AddInclude(new IncludeOption<Customer, Address>(c => c.BillingAddress)
        .ThenInclude(a => a.City)
        .ThenInclude(c => c.Country));

var customers = await repository.FindAllAsync(options, cancellationToken);
// Loads: Customer → BillingAddress → City → Country
```

#### Collection navigation properties

For collection navigation properties (e.g., Customer → Orders → OrderItems → Product):

```csharp
var options = new FindOptions<Customer>()
    .AddInclude(new IncludeOption<Customer, ICollection<Order>>(c => c.Orders)
        .ThenInclude(o => o.OrderItems)
        .ThenInclude(i => i.Product));

var customers = await repository.FindAllAsync(options, cancellationToken);
// Loads: Customer → Orders → OrderItems → Product
```

#### Multiple include chains

You can add multiple include chains to load different navigation paths:

```csharp
var options = new FindOptions<Order>()
    .AddInclude(new IncludeOption<Order, Address>(o => o.ShippingAddress)
        .ThenInclude(a => a.City)
        .ThenInclude(c => c.Country))
    .AddInclude(new IncludeOption<Order, ICollection<OrderItem>>(o => o.OrderItems)
        .ThenInclude(i => i.Product)
        .ThenInclude(p => p.Category));

var orders = await repository.FindAllAsync(options, cancellationToken);
// Loads both: Order → ShippingAddress → City → Country
//         and: Order → OrderItems → Product → Category
```

#### E-commerce order query

```csharp
public class OrderQueryHandler
{
    private readonly IGenericRepository<Order> orderRepository;

    public async Task<IEnumerable<Order>> GetOrdersWithFullDetailsAsync(
        CancellationToken cancellationToken)
    {
        var options = new FindOptions<Order>()
            // Include customer and their billing address details
            .AddInclude(new IncludeOption<Order, Customer>(o => o.Customer)
                .ThenInclude(c => c.BillingAddress)
                .ThenInclude(a => a.City))
            // Include order items and product details
            .AddInclude(new IncludeOption<Order, ICollection<OrderItem>>(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Supplier))
            // Include payment information
            .AddInclude(new IncludeOption<Order, Payment>(o => o.Payment)
                .ThenInclude(p => p.PaymentMethod));

        return await orderRepository.FindAllAsync(options, cancellationToken);
    }
}
```

#### Key points

- **Type safety**: All navigation properties are validated at compile time.
- **Fluent API**: Chain multiple `ThenInclude` calls for deep nesting.
- **Generic type parameters**: Specify both `TEntity` and `TProperty` in `IncludeOption<TEntity, TProperty>`.
- **Collection support**: Works with reference properties (`Address`, `Customer`) and collection properties (`ICollection<Order>`, `IEnumerable<OrderItem>`).
- **Multiple chains**: Combine multiple include chains in one `FindOptions` instance.
- **Performance**: Avoids per-entity lazy-loading queries; the command count depends on provider configuration.

### Combining with other options

You can combine includes with filtering, ordering, and paging:

```csharp
var options = new FindOptions<Customer>()
    .AddInclude(new IncludeOption<Customer, ICollection<Order>>(c => c.Orders)
        .ThenInclude(o => o.OrderItems))
    .AddOrder(new OrderOption<Customer>(c => c.Name));

options.Skip = 0;
options.Take = 20;
options.Distinct = new DistinctOption<Customer>(c => c.Id);

var activeCustomers = new Specification<Customer>(c => c.IsActive);
var pagedCustomers = await repository.FindAllAsync(
    activeCustomers,
    options,
    cancellationToken);
```

### Projection with include options

Projection methods accept the same `FindOptions<TEntity>`. Entity Framework can translate navigation
access inside a projection without an explicit include and may ignore includes that do not affect
the projected shape. Use includes here only when the selected provider needs them.

```csharp
var options = new FindOptions<Order>()
    .AddInclude(new IncludeOption<Order, Customer>(o => o.Customer)
        .ThenInclude(c => c.BillingAddress));

var customerNames = await repository.ProjectAllAsync(
    o => o.Customer.Name,
    options,
    cancellationToken);
```

### Bulk updates and deletes

Use `UpdateSetAsync` and `DeleteSetAsync` for set-based operations that run directly in the repository provider without loading each entity instance first.

Set-based operations suit administrative tasks and background jobs that need to change many rows
without materializing the corresponding entities.

```csharp
var affected = await repository.UpdateSetAsync(
    set => set
        .Set(c => c.IsActive, false)
        .Set(c => c.LastName, "Archived")
        .Set(c => c.LoginCount, c => c.LoginCount + 1),
    cancellationToken: cancellationToken);

var deleted = await repository.DeleteSetAsync(cancellationToken: cancellationToken);
```

You can limit the affected rows with one or more specifications:

```csharp
var affected = await repository.UpdateSetAsync(
    new CustomerIsInactiveSpecification(),
    set => set
        .Set(c => c.IsActive, false)
        .Set(c => c.LoginCount, c => c.LoginCount + 1),
    cancellationToken: cancellationToken);

var deleted = await repository.DeleteSetAsync(
[
    new CustomerCountrySpecification("DE"),
    new CustomerIsInactiveSpecification()
], cancellationToken: cancellationToken);
```

Expression-based forwarding overloads are also available through `RepositoryExtensions`:

```csharp
var affected = await repository.UpdateSetAsync(
    c => c.IsActive,
    set => set
        .Set(c => c.LastName, "Archived")
        .Set(c => c.LoginCount, c => c.LoginCount + 1),
    cancellationToken: cancellationToken);

var deleted = await repository.DeleteSetAsync(
    c => !c.IsActive,
    cancellationToken: cancellationToken);
```

`FilterModel`-based extension overloads translate the query filters into specifications before forwarding them to the repository:

```csharp
var filter = new FilterModel
{
    Filters =
    [
        new FilterCriteria
        {
            Field = nameof(Customer.IsActive),
            Operator = FilterOperator.Equal,
            Value = false
        }
    ]
};

var affected = await repository.UpdateSetAsync(
    filter,
    set => set
        .Set(c => c.LastName, "Archived")
        .Set(c => c.LoginCount, c => c.LoginCount + 1),
    cancellationToken: cancellationToken);

var deleted = await repository.DeleteSetAsync(filter, cancellationToken: cancellationToken);
```

#### Key points for bulk set operations

- Filtering for `UpdateSetAsync` and `DeleteSetAsync` comes from specification instances, including specifications generated from a `FilterModel`.
- `FindOptions` continue to shape the query only; they do not define the `WHERE` clause.
- `UpdateSetAsync` supports both constant assignments such as `.Set(c => c.IsActive, false)` and computed assignments such as `.Set(c => c.LoginCount, c => c.LoginCount + 1)`.
- `EntityFrameworkGenericRepository<TEntity>` and `InMemoryRepository<TEntity>` provide implementations for repository bulk updates and deletes.
- Other repository implementations expose the same API for consistency but currently throw `NotImplementedException`.
- With Entity Framework, set-based operations execute directly in the database and do not synchronize already tracked entities in the current `DbContext`. If you need the updated database state immediately afterwards, re-query using `NoTracking` or use a fresh context/repository instance.

### Explicit provider bulk inserts

`IEntityBulkInserter<TEntity>` is a Domain-owned, infrastructure-implemented preview capability for high-volume root-row ingestion. It is deliberately independent of `IGenericRepository<TEntity>.InsertSetAsync`: normal repository insertion remains the right choice when callers need EF tracking, graph cascades, or database-generated values returned to input objects.

For a compatible native operation, bulk insert guarantees:

- mapping preflight before database work;
- one provider invocation, with provider batches contained inside one operation transaction;
- root-table-only writes, including same-table non-JSON owned references;
- an exact `Result<long>` inserted-row count and cancellation propagation;
- detached input entities with no identity, default, computed, or rowversion hydration; and
- explicitly selected behavior semantics only; repository decorators and EF interceptors are not inspected or copied.

Register the repository and bulk inserter independently:

- `AddSqlServerDbContext<TContext>()` registers the stateless SQL Server native strategy automatically.
- `AddEntityFrameworkBulkInserter<TEntity, TContext>()` registers the typed terminal operation and its explicitly selected decorators.

```csharp
services.AddSqlServerDbContext<CoreDbContext>(options => options
    .UseConnectionString(connectionString));

services.AddEntityFrameworkRepository<TodoItem, CoreDbContext>()
    .WithTransactions();

services.AddEntityFrameworkBulkInserter<TodoItem, CoreDbContext>(
    new SqlServerEntityBulkInsertOptions
    {
        BatchSize = 5_000,
        SqlBulkCopyOptions = SqlBulkCopyOptions.TableLock
    })
    .WithBehavior<EntityBulkInserterCancellationBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterTracingBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterLoggingBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterMetricsBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterOutboxDomainEventBehavior<TodoItem, CoreDbContext>>()
    .WithBehavior<EntityBulkInserterChangeHistoryBehavior<TodoItem, CoreDbContext>>()
    .WithBehavior<EntityBulkInserterAuditStateBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterConcurrencyBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterDomainEventBehavior<TodoItem>>()
    .WithBehavior<EntityBulkInserterDomainEventMetricsBehavior<TodoItem>>();
```

`WithBehavior` calls are ordered from outermost to innermost. Register the outbox decorator before ChangeHistory, mutation, and event decorators so it owns the transaction enclosing the native write, ChangeHistory rows, and outbox save. Native ChangeHistory capture also requires `.CaptureBulkInserts(...)` on the tracked entity. Do not combine the outbox decorator with `EntityBulkInserterDomainEventPublisherBehavior<TEntity>`. Direct publication is intentionally non-atomic with the native write.

| Decorator | Entity requirement | Main dependency |
| --- | --- | --- |
| Cancellation, tracing, logging, metrics | None | Cancellation token, `ActivitySource`, `ILoggerFactory`, or optional `IMetricsService` |
| Audit state | `IAuditable` | Optional `ICurrentUserAccessor` |
| Concurrency | `IConcurrency` | None |
| Created domain event and event metrics | `IAggregateRoot` | Optional `IMetricsService` for metrics |
| Outbox domain events | `IAggregateRoot` | `TContext : IOutboxDomainEventContext`, optional queue/options |
| ChangeHistory | `IEntity` | `TContext : IChangeHistoryContext`, `ChangeHistoryOptions` with explicit bulk-insert capture |
| Direct domain-event publisher | `IAggregateRoot` | `IDomainEventPublisher` |

Then inject `IEntityBulkInserter<TEntity>` directly into application or presentation orchestration code, for example a DataPorter completion interceptor. The consumer needs only the Domain repository namespace for the contract:

```csharp
public sealed class TodoItemBulkImportInterceptor(
    IMapper mapper,
    ICurrentUserAccessor currentUserAccessor,
    IEntityBulkInserter<TodoItem> bulkInserter)
    : IImportRowInterceptor<TodoItemModel>
{
    public Task<RowInterceptionDecision> BeforeImportAsync(
        ImportRowContext<TodoItemModel> context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RowInterceptionDecision.Continue());
    }

    public async Task<Result> AfterImportCompletedAsync(
        ImportCompletionContext<TodoItemModel> context,
        CancellationToken cancellationToken = default)
    {
        if (context.Result.HasErrors || context.Result.SuccessfulRows == 0)
        {
            return Result.Success();
        }

        var entities = context.Result.Data
            .Select(model =>
            {
                var entity = mapper.Map<TodoItemModel, TodoItem>(model);
                entity.UserId = currentUserAccessor.UserId;
                // Native bulk insert writes the TodoItem root table only.
                entity.Steps.Clear();
                return entity;
            })
            .ToList();

        var bulkResult = await bulkInserter.InsertAsync(entities, cancellationToken);

        return bulkResult.IsSuccess
            ? Result.Success()
            : Result.Failure().WithErrors(bulkResult.Errors);
    }
}
```

The DoFiesta sample uses this pattern in a DataPorter `AfterImportCompletedAsync` interceptor: the file is parsed and validated first, then the successful todo rows are mapped to `TodoItem` entities and written with `IEntityBulkInserter<TodoItem>`.

#### Options and unsupported providers

`EntityBulkInsertOptions` is provider-neutral and applies to every current or future strategy:

- `BatchSize`
- `CommandTimeout`
- `AssignSequentialGuidKeys`
- `KeepGeneratedIdentityValues`

For SQL Server, pass the optional derived `SqlServerEntityBulkInsertOptions` to `AddEntityFrameworkBulkInserter`. Its `SqlBulkCopyOptions` keeps SQL Server-specific flags such as `TableLock`. Do not configure `KeepIdentity` or `UseInternalTransaction`: `KeepGeneratedIdentityValues` controls identity preservation and the SQL Server strategy uses the active EF transaction.

The DevKit provider setup method is required for automatic native-provider registration. This raw EF setup intentionally has no native fallback:

```csharp
services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(connectionString));
services.AddEntityFrameworkBulkInserter<TodoItem, CoreDbContext>();
```

When `IEntityBulkInserter<TodoItem>.InsertAsync(...)` runs, it returns a failed `Result<long>` with a typed provider error naming the entity, active EF provider, and registered providers. PostgreSQL and SQLite provider packages currently register typed unsupported inserters, which return `EntityBulkInsertPreconditionError` instead of silently falling back to row-by-row insertion.

Native mapping rejects populated owned or non-owned navigations, owned collections with rows, separate-table or JSON ownership, multi-table inheritance, tracked inputs, duplicate object references, and required shadow properties without an explicit shadow-value provider. Null or empty navigations are allowed because they preserve the root-only contract.

#### Transactions, cancellation, and outbox

The terminal opens an EF transaction when no caller transaction exists. All native batches commit or roll back together. The outbox decorator follows the same rule and encloses its root write plus outbox persistence in one transaction. If `DbContext.Database.CurrentTransaction` is already active, both participate without committing or rolling it back; the caller owns the final outcome.

Cancellation is rethrown as `OperationCanceledException` and an owned transaction is rolled back. In immediate outbox mode, events are queued only after an outbox-owned commit. With a caller-owned transaction, use interval polling because the decorator cannot know when the caller commits; aggregate events are not cleared early.

#### Key points for explicit bulk inserts

- The current SQL Server strategy uses `Microsoft.Data.SqlClient.SqlBulkCopy`; no commercial bulk-insert package is required.
- Bulk insert is opt-in and provider-native. It does not replace `IGenericRepository<TEntity>.InsertSetAsync`.
- Only explicitly registered decorators execute; repository decorators and EF interceptors are not inspected or copied.
- The shared mapper writes aggregate-root table columns, flattens same-table owned reference values, and generates primitive or typed GUID ids when needed.
- The shared orchestrator dispatches by exact `DbContext.Database.ProviderName`; it never infers a provider from a connection string.
- Native inputs stay detached. Store-generated identity, default, computed, and rowversion values are not copied back.
- Use this API for imports, seed data, generated records, queue or log batches, and other large inserts where root-table-only writes are acceptable.

#### Create a new provider

To add PostgreSQL, SQLite, or another relational provider without modifying the shared orchestrator:

1. Create or update the provider assembly so it references `Infrastructure.EntityFramework` and its EF Core/ADO.NET provider packages.
2. Implement the stateless non-generic `IEntityBulkInsertProvider`. Set `ProviderName` to the provider's exact `DbContext.Database.ProviderName` value and implement native writing from `EntityBulkInsertBatch<TEntity>`.
3. Keep provider-native connections, transactions, identifier quoting, wire formats, and provider-specific option enums in that provider assembly. Do not duplicate EF metadata mapping, generated-value assignment, or `Result<long>` conversion from the shared layer.
4. Update every DevKit `Add*DbContext<TContext>` overload for that provider to use `TryAddEnumerable` and register one singleton `IEntityBulkInsertProvider` implementation.
5. Add a derived options type only when native options are needed; derive it from `EntityBulkInsertOptions` and keep the shared options provider-neutral.
6. Add terminal contract tests plus provider integration tests for mappings, value conversion, generated values, transactions, identities, native options, and registration.

## Appendix A: Optimistic concurrency support

### Concurrency overview

The repository implementation provides built-in optimistic concurrency control to handle scenarios where multiple users might attempt to modify the same entity simultaneously. This feature helps prevent the "lost update" problem, where one user's changes could accidentally overwrite another user's modifications.

```mermaid
sequenceDiagram
    participant User1 as User 1
    participant User2 as User 2
    participant Repo as Repository
    participant DB as Database

    User1->>Repo: Get TodoItem (Version=A)
    User2->>Repo: Get TodoItem (Version=A)

    User1->>Repo: Update TodoItem
    Note over Repo: Generate new Version B
    Repo->>DB: Save (Version A→B)
    DB-->>Repo: Success

    User2->>Repo: Update TodoItem
    Note over Repo: Generate new Version C
    Repo->>DB: Save (Version A→C)
    DB-->>Repo: Concurrency Exception
    Note over User2: Must refresh and retry
```

### Implementation

#### 1. Enable concurrency support

To enable concurrency control for an entity, implement the `IConcurrency` interface:

```csharp
public class TodoItem : AuditableAggregateRoot<TodoItemId>, IConcurrency
{
    // Entity properties
    public string Title { get; set; }
    public TodoStatus Status { get; set; }

    // Concurrency token
    public Guid ConcurrencyVersion { get; set; }
}
```

#### 2. Configure Entity Framework mapping

Configure the concurrency token in your entity configuration:

```csharp
public class TodoItemEntityTypeConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        // Configure concurrency token
        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        // Other configuration...
    }
}
```

### How repository concurrency prevents conflicts

1. When an entity is retrieved, its current `ConcurrencyVersion` is tracked
2. During updates, the repository:
   - Generates a new version GUID
   - Includes the original version in the update condition
   - Only updates if the database version matches the original version

### Example usage

```csharp
public async Task UpdateTodoItemAsync(TodoItem item)
{
    try
    {
        await _repository.UpdateAsync(item);
    }
    catch (DbUpdateConcurrencyException)
    {
        // Handle the conflict - typically by:
        // 1. Informing the user
        // 2. Reloading the latest data
        // 3. Allowing the user to merge changes
    }
}
```

### Benefits

- Database-agnostic implementation using GUIDs as versions
- Automatic version management
- No additional database locks required
- Transparent to application code
- Works with disconnected entities

### Limitations

- Entity Framework, Cosmos DB, and in-memory repositories support `IConcurrency`; provider behavior and exception types differ
- May require additional application logic to handle conflict resolution

Concurrency tokens detect stale writes. The application remains responsible for presenting the
conflict, reloading current state, and deciding whether to retry or merge changes.

---

## Appendix B: Sequence number generation support

### Sequence overview

The sequence number generator creates unique, incrementing business identifiers such as order or
invoice numbers. It supports SQL Server and PostgreSQL native sequences, SQLite emulation, and an
in-memory implementation for tests.

```mermaid
sequenceDiagram
    participant App as Application Service
    participant Gen as Sequence Generator
    participant DB as Database

    App->>Gen: GetNextAsync("OrderNumbers")
    Gen->>DB: Check existence
    DB-->>Gen: Exists
    Gen->>DB: NEXT VALUE FOR OrderNumbers
    DB-->>Gen: 1001
    Gen-->>App: Result<long>.Success(1001)

    Note over App,DB: Thread-safe with internal locking

    App->>Gen: GetSequenceInfoAsync("OrderNumbers")
    Gen->>DB: Query metadata
    DB-->>Gen: {Current: 1001, Increment: 1, ...}
    Gen-->>App: Result<SequenceInfo>
```

### Setup

To use sequence generation, first define sequences in your DbContext and register the generator in dependency injection (DI).

#### 1. Define sequences in `DbContext`

Configure sequences in the `OnModelCreating` method of your DbContext. This step is provider-specific.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasSequence<int>("OrderNumbers", "CoreSchema")
        .StartsAt(1000)
        .IncrementsBy(5);
    // Add more sequences as needed

    base.OnModelCreating(modelBuilder);
}
```

Apply database migrations to create the sequences (e.g., `dotnet ef migrations add AddSequences` and `dotnet ef database update`).

#### 2. Register in dependency injection

Register the appropriate generator for your database provider using the provided extensions. The generator is typically scoped to match the DbContext lifetime.

```csharp
services.AddSqlServerDbContext<YourDbContext>(connectionString)
    .WithSequenceNumberGenerator(new SequenceNumberGeneratorOptions
    {
        LockTimeout = TimeSpan.FromSeconds(60)
    });

// For PostgreSQL
services.AddPostgresDbContext<YourDbContext>(connectionString)
    .WithSequenceNumberGenerator();

// For SQLite
services.AddSqliteDbContext<YourDbContext>(connectionString)
    .WithSequenceNumberGenerator();

// For in-memory testing
services.AddInMemoryDbContext<YourDbContext>()
    .WithSequenceNumberGenerator();
```

#### Provider-specific notes

SQL Server and PostgreSQL use native sequences, including increment steps and bounds. SQLite
emulates basic sequencing through a system table. Configure each sequence explicitly when using the
in-memory implementation.

### Usage

Inject `ISequenceNumberGenerator` into your services and use it to generate numbers. Operations return `Result<T>` for safe error handling.

#### Basic generation

```csharp
public class OrderService
{
    private readonly ISequenceNumberGenerator generator;
    private readonly YourDbContext context;

    public OrderService(ISequenceNumberGenerator generator, YourDbContext context)
    {
        this.generator = generator;
        this.context = context;
    }

    public async Task<Result<Order>> CreateOrderAsync(Order order, CancellationToken ct = default)
    {
        var numberResult = await generator.GetNextAsync("OrderNumbers", "CoreSchema", ct);
        if (numberResult.IsFailure)
        {
            return Result<Order>.Failure().WithErrors(numberResult.Errors);
        }

        order.OrderNumber = numberResult.Value;
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);

        return Result<Order>.Success(order);
    }
}
```

#### Additional operations

- **Metadata Query**: Retrieve details like current value.

  ```csharp
  var infoResult = await generator.GetSequenceInfoAsync("OrderNumbers");
  if (infoResult.IsSuccess)
  {
      Console.WriteLine($"Current: {infoResult.Value.CurrentValue}");
  }
  ```

- **Reset**: Restart the sequence (e.g., for administrative tasks).

  ```csharp
  await generator.ResetSequenceAsync("OrderNumbers", 1000);
  ```

- **Batch Generation**: Get multiple sequences in one call.

  ```csharp
  var results = await generator.GetNextMultipleAsync(new[] { "OrderNumbers", "InvoiceNumbers" });
  if (results.IsSuccess)
  {
      order.OrderNumber = results.Value["OrderNumbers"];
  }
  ```

- **Entity convention**: Use the same sequence-name convention as `GetNextForEntityAsync<TEntity>`.

  ```csharp
  var numberResult = await generator.GetNextAsync("OrderSequence", "CoreSchema");
  ```

`GetNextForEntityAsync<TEntity>` is available on concrete generators derived from
`SequenceNumberGeneratorBase<TContext>`, but it is not part of `ISequenceNumberGenerator`.

The generator ensures thread-safety with internal locking and supports Result-based error handling for issues like missing sequences or timeouts.

### Benefits and limitations

Sequence generation keeps business identifiers separate from entity primary keys. Operations are
serialized by internal locks, but SQLite supports only basic emulation. Use batch operations to
reduce database calls. The in-memory generator supports isolated tests without a database.
