# Domain Repositories Feature Documentation

[TOC]

## Overview

### Challenges

### Solution

### Use Cases

## Usage

## Appendix A: Optimistic Concurrency Support

### Overview
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

### 1. Enable Concurrency Support
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

#### 2. Configure Entity Framework Mapping
Configure the concurrency token in your entity configuration:

```csharp
public class TodoItemEntityTypeConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        // Configure concurrency token
        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
            
        // Other configuration...
    }
}
```

### How It Prevents Data Conflicts (Repository)

1. When an entity is retrieved, its current `ConcurrencyVersion` is tracked
2. During updates, the repository:
   - Generates a new version GUID
   - Includes the original version in the update condition
   - Only updates if the database version matches the original version

### Example Usage

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

- Only available with Entity Framework repositories
- May require additional application logic to handle conflict resolution

The concurrency support provides a robust way to handle simultaneous updates while maintaining data integrity in your application. It's particularly useful in scenarios with multiple users working on the same data simultaneously.

---

## Appendix B: Sequence Number Generation Support

### Overview
The sequence number generation feature allows developers to generate unique, auto-incrementing numbers for business identifiers (such as order numbers or invoice IDs) directly from the database. This is particularly useful when you need reliable, thread-safe sequencing that integrates with the DbContext. The implementation supports SQL Server, PostgreSQL, SQLite (with emulation) and an in-memory option for testing.

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

#### 1. Define Sequences in DbContext
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

#### 2. Register in DI
Register the appropriate generator for your database provider using the provided extensions. The generator is typically scoped to match the DbContext lifetime.

```csharp
// In ConfigureServices
services.AddDbContext<YourDbContext>(options => options.UseSqlServer(connectionString))
    .WithSequenceNumberGenerator(new SequenceNumberGeneratorOptions 
    {
        LockTimeout = TimeSpan.FromSeconds(60)
    });

// For PostgreSQL
services.AddDbContext<YourDbContext>(options => options.UseNpgsql(connectionString))
    .WithSequenceNumberGenerator();

// For SQLite
services.AddDbContext<YourDbContext>(options => options.UseSqlite(connectionString))
    .WithSequenceNumberGenerator();

// For in-memory testing (no DbContext dependency)
services.AddScoped<ISequenceNumberGenerator, InMemorySequenceNumberGenerator>();
```

#### Provider-Specific Notes
SQL Server and PostgreSQL use native sequences for full support, including increment steps and bounds. SQLite emulates basic sequencing via a system table, while the in-memory option is ideal for unit tests and requires manual configuration in test setup.

### Usage
Inject `ISequenceNumberGenerator` into your services and use it to generate numbers. Operations return `Result<T>` for safe error handling.

#### Basic Generation
```csharp
public class OrderService
{
    private readonly ISequenceNumberGenerator generator;
    private readonly YourDbContext context;

    public OrderService(ISequenceNumberGenerator generator, YourDbContext context)
    {
        generator = generator;
        context = context;
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

#### Additional Operations
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
- **Entity Convention**: Generate based on entity type (e.g., "OrderSequence").
  ```csharp
  var numberResult = await generator.GetNextForEntityAsync<Order>("CoreSchema");
  ```

The generator ensures thread-safety with internal locking and supports Result-based error handling for issues like missing sequences or timeouts.

### Benefits and Limitations
This feature provides reliable sequencing integrated with your DbContext, making it easy to generate business IDs without relying on entity primaries. It's thread-safe and works across providers, though SQLite has limited emulation (basic increment only). For high-volume use, consider batch operations to minimize database calls. In tests, the in-memory generator allows fast, isolated verification without a real database.