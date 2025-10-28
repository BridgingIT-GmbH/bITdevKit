![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you're working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

This repository includes the complete source code for the bITDevKit, along with a variety of sample
applications located in the ./examples folder within the solution. These samples serve as practical
demonstrations of how to leverage the capabilities of the bITDevKit in real-world scenarios. All
components are available
as [nuget packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [RELEASES](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/RELEASES.md).

Join us in advancing the world of software development with the bITDevKit!

---

# ResultOperationScope - Scoped Operation Pattern with Railway-Oriented Programming

## Overview

`ResultOperationScope<T, TOperation>` is a **generic scoped operation pattern** that provides a fluent API for wrapping Result chains within any operation requiring scoped resource management. While commonly demonstrated with database transactions, this pattern is applicable to **any operation that requires**:

- **Lazy Start**: Operation begins only when needed
- **Automatic Cleanup**: Resources are properly released
- **All-or-Nothing Semantics**: Either complete successfully or rollback/cleanup
- **Railway-Oriented Programming**: Clean error handling with short-circuiting

It implements the Railway-Oriented Programming pattern with automatic resource management, ensuring that all operations within the scope are either committed on success or rolled back on failure.

## Key Features

- **Lazy Operation Start**: Operation is only started when the first async operation is executed
- **Automatic Commit/Rollback**: Operation is automatically committed on success or rolled back on failure/exception
- **Fluent API**: Seamless chaining of operations with full async/await support
- **Railway-Oriented Programming**: Short-circuits on failure, continuing only on success path
- **Clean Architecture**: Abstract interface pattern allows any scoped operation implementation
- **Generic Pattern**: Works with transactions, locks, file operations, API sessions, sagas, and more

## Installation

The `ResultOperationScope` is part of the `BridgingIT.DevKit.Common.Results` package.

```bash
dotnet add package BridgingIT.DevKit.Common.Results
```

## Basic Usage

### Simple Transaction Example

```csharp
var result = await Result<User>.Success(user)
    // Start transaction scope (lazy - not started yet)
    .StartOperation(async ct => await transaction.BeginTransactionAsync(ct))
    // Set properties (sync operation - transaction still not started)
    .Tap(u => u.UpdatedAt = DateTime.UtcNow)
    // First async operation - TRANSACTION STARTS HERE
    .TapAsync(async (u, ct) =>
        await auditService.LogUpdateAsync(u.Id, ct), cancellationToken)
    // Validate business rules
    .EnsureAsync(async (u, ct) =>
        await permissionService.CanUpdateAsync(u),
        new UnauthorizedError(),
        cancellationToken)
    // Update in database (within transaction)
    .BindAsync(async (u, ct) =>
        await repository.UpdateResultAsync(u, ct), cancellationToken)
    // End transaction (commit on success, rollback on failure)
    .EndOperationAsync(
        commitAsync: async (tx, ct) => await tx.CommitAsync(ct),
        rollbackAsync: async (tx, ex, ct) => await tx.RollbackAsync(ct),
        cancellationToken);
```

### Complex Example: TodoItem Creation with Transaction

```csharp
protected override async Task<Result<TodoItemModel>> HandleAsync(
    TodoItemCreateCommand request,
    SendOptions options,
    CancellationToken cancellationToken) =>
    await Result<TodoItem>.Success(mapper.Map<TodoItemModel, TodoItem>(request.Model))
        // Start transaction scope using repository transaction
        .StartOperation(async ct => await transaction.BeginTransactionAsync(ct))
        // Set current user (sync)
        .Tap(e => e.UserId = currentUserAccessor.UserId)
        // Generate sequence number (first async - transaction starts here)
        .TapAsync(async (e, ct) =>
        {
            var seqResult = await numberGenerator.GetNextAsync("TodoItemSequence", "core", ct);
            e.Number = seqResult.IsSuccess ? (int)seqResult.Value : 0;
        }, cancellationToken)
        // Check business rules
        .UnlessAsync(async (e, ct) => await Rule
            .Add(RuleSet.IsNotEmpty(e.Title))
            .Add(RuleSet.NotEqual(e.Title, "todo"))
            .Add(new TitleShouldBeUniqueRule(e.Title, repository))
            .CheckAsync(ct), cancellationToken)
        // Register domain event
        .Tap(e => e.DomainEvents.Register(new TodoItemCreatedDomainEvent(e)))
        // Insert into database (within transaction)
        .BindAsync(async (e, ct) =>
            await repository.InsertResultAsync(e, ct), cancellationToken)
        // Set permissions
        .Tap(e =>
            new EntityPermissionProviderBuilder(permissionProvider)
                .ForUser(e.UserId)
                    .WithPermission<TodoItem>(e.Id, Permission.Read)
                    .WithPermission<TodoItem>(e.Id, Permission.Write)
                    .WithPermission<TodoItem>(e.Id, Permission.Delete)
                .Build())
        // Audit logging
        .Tap(e => Console.WriteLine("AUDIT"))
        // End transaction (commit on success, rollback on failure)
        .EndOperationAsync(
            commitAsync: async (tx, ct) => await tx.CommitAsync(ct),
            rollbackAsync: async (tx, ex, ct) => await tx.RollbackAsync(ct),
            cancellationToken)
        // Map entity back to model
        .Map(mapper.Map<TodoItem, TodoItemModel>);
```

## API Reference

### Starting an Operation Scope

```csharp
// With async operation factory
public static ResultOperationScope<T, TOperation> StartOperation<T, TOperation>(
    this Result<T> result,
    Func<CancellationToken, Task<TOperation>> startAsync)
    where TOperation : class

// With synchronously created operation
public static ResultOperationScope<T, TOperation> StartOperation<T, TOperation>(
    this Result<T> result,
    TOperation operation)
    where TOperation : class
```

### Available Operations

All standard Result operations are available within the scope:

- **Tap / TapAsync**: Execute side effects
- **Map / MapAsync**: Transform the value
- **Bind / BindAsync**: Chain Result-returning operations
- **Ensure / EnsureAsync**: Validate conditions (fails if predicate returns false)
- **UnlessAsync**: Validate conditions (fails if predicate returns true or if Rule validation fails)

### Ending an Operation Scope

```csharp
public async Task<Result<T>> EndOperationAsync(
    Func<TOperation, CancellationToken, Task> commitAsync,
    Func<TOperation, Exception, CancellationToken, Task> rollbackAsync = null,
    CancellationToken cancellationToken = default)
```

- **commitAsync**: Function called when Result is successful
- **rollbackAsync**: Optional function called when Result fails or exception occurs
- Returns the unwrapped `Result<T>`

## Transaction Interfaces

### IRepositoryTransaction<TEntity>

```csharp
public interface IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    // Legacy methods
    Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default);
    Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellationToken = default);

    // New method for explicit transaction control
    Task<IRepositoryTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
```

### IRepositoryTransactionScope

```csharp
public interface IRepositoryTransactionScope
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

### Entity Framework Implementation

```csharp
public class EntityFrameworkRepositoryTransaction<TEntity> : IRepositoryTransaction<TEntity>
{
    private readonly DbContext context;

    public async Task<IRepositoryTransactionScope> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EntityFrameworkRepositoryTransactionScope(transaction);
    }
}

internal class EntityFrameworkRepositoryTransactionScope(IDbContextTransaction transaction)
    : IRepositoryTransactionScope
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
        => await transaction.CommitAsync(cancellationToken);

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
        => await transaction.RollbackAsync(cancellationToken);
}
```

## Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                   ResultOperationScope Flow Diagram                          │
│              (Transaction-Wrapped Railway-Oriented Programming)              │
└─────────────────────────────────────────────────────────────────────────────┘

INPUT: TodoItemModel from request
   │
   ▼
┌────────────────────────────────────────────────────────────────────┐
│ 1. Result<TodoItem>.Success(entity)                                │
│    Create initial Result with mapped TodoItem entity               │
│    Status: Success | Transaction: Not Started                      │
└────────────────────────────────────────────────────────────────────┘
   │
   ▼
┌────────────────────────────────────────────────────────────────────┐
│ 2. StartOperation(() => transaction.BeginTransactionAsync())      │
│    Wrap Result in ResultOperationScope                            │
│    Transaction: Lazy (will start on first async operation)        │
│    Returns: ResultOperationScope<TodoItem, IRepositoryTransactionScope>│
└────────────────────────────────────────────────────────────────────┘
   │
   ▼
┌────────────────────────────────────────────────────────────────────┐
│ 3. Tap(e => e.UserId = currentUserAccessor.UserId)               │
│    Set current user ID on entity (SYNC operation)                 │
│    Transaction: Still lazy (not started yet)                      │
│    Continues: if IsSuccess                                         │
│    Short-circuits: if IsFailure                                    │
└────────────────────────────────────────────────────────────────────┘
   │
   ▼
┌────────────────────────────────────────────────────────────────────┐
│ 4. TapAsync(async (e, ct) => generate sequence number)           │
│    ⚡ TRANSACTION STARTS HERE (first async operation)              │
│    Transaction: ACTIVE - BeginTransactionAsync() called           │
│    Generate sequence number for TodoItem                           │
│    All subsequent operations are within transaction scope          │
└────────────────────────────────────────────────────────────────────┘
   │
   ▼
┌────────────────────────────────────────────────────────────────────┐
│ 5. UnlessAsync(async (e, ct) => Check Business Rules)            │
│    Validate:                                                       │
│      • Title is not empty                                          │
│      • Title is not "todo"                                         │
│      • Title is unique (check repository)                          │
│    If rules PASS: Continue                                         │
│    If rules FAIL: Result becomes Failure, continues to cleanup    │
└────────────────────────────────────────────────────────────────────┘
   │
   ├──[SUCCESS]──────────────────────────────────────────────────────┐
   │                                                                   │
   ▼                                                                   │
┌────────────────────────────────────────────────────────────────────┐
│ 6. Tap(e => Register Domain Event)                               │
│    Register TodoItemCreatedDomainEvent                            │
│    Transaction: ACTIVE                                             │
└────────────────────────────────────────────────────────────────────┘
   │                                                                   │
   ▼                                                                   │
┌────────────────────────────────────────────────────────────────────┐
│ 7. BindAsync(async (e, ct) => repository.InsertResultAsync)      │
│    Insert entity into database (within transaction)               │
│    Database write is part of transaction                          │
│    If insert fails: Result becomes Failure                        │
└────────────────────────────────────────────────────────────────────┘
   │                                                                   │
   ▼                                                                   │
┌────────────────────────────────────────────────────────────────────┐
│ 8. Tap(e => Set Permissions)                                     │
│    EntityPermissionProviderBuilder                                │
│      • Read permission                                             │
│      • Write permission                                            │
│      • Delete permission                                           │
└────────────────────────────────────────────────────────────────────┘
   │                                                                   │
   ▼                                                                   │
┌────────────────────────────────────────────────────────────────────┐
│ 9. Tap(e => Audit Log)                                           │
│    Log "AUDIT" message                                            │
└────────────────────────────────────────────────────────────────────┘
   │                                                                   │
   │                                                                   │
   └──────────────────────[JOIN]──────────────────────────────────────┤
                                                                       │
   ┌───────────────────────────────────────────────────────────[FAILURE]
   │
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 10. EndOperationAsync(commitAsync, rollbackAsync)                  │
│                                                                      │
│     Decision Point:                                                 │
│     ┌──────────────────────────────────────────────────────────┐  │
│     │ if (Result.IsSuccess)                                     │  │
│     │     └─> commitAsync(transaction, ct)                      │  │
│     │         └─> transaction.CommitAsync(ct)                   │  │
│     │             └─> All changes persisted to database         │  │
│     │                                                            │  │
│     │ else if (Result.IsFailure)                                │  │
│     │     └─> rollbackAsync(transaction, exception, ct)         │  │
│     │         └─> transaction.RollbackAsync(ct)                 │  │
│     │             └─> All changes discarded                     │  │
│     │                                                            │  │
│     │ catch (Exception ex)                                      │  │
│     │     └─> rollbackAsync(transaction, ex, ct)                │  │
│     │         └─> transaction.RollbackAsync(ct)                 │  │
│     │         └─> Return Result.Failure with exception error    │  │
│     └──────────────────────────────────────────────────────────┘  │
│                                                                      │
│     Transaction: COMMITTED or ROLLED BACK                           │
│     Scope exits, transaction is disposed                            │
└─────────────────────────────────────────────────────────────────────┘
   │
   ▼
┌────────────────────────────────────────────────────────────────────┐
│ 11. Map(entity => mapper.Map<TodoItem, TodoItemModel>)           │
│     Convert entity back to model for response                     │
│     Returns: Result<TodoItemModel>                                │
└────────────────────────────────────────────────────────────────────┘
   │
   ▼
OUTPUT: Result<TodoItemModel>
   │
   ├─ Success: TodoItemModel with all changes committed
   └─ Failure: Error details, all changes rolled back
```

## Key Concepts

### Railway-Oriented Programming
- Each operation is a "station" on a railway track
- Success path continues forward on the track
- Failure switches to failure track (short-circuits)
- All operations check `IsSuccess` before executing

### Lazy Operation Start
- Operation is NOT started at `StartOperation()`
- Operation starts at first `*Async` operation (e.g., `TapAsync`, `BindAsync`)
- Avoids unnecessary overhead for synchronous operations
- Improves performance by delaying operation creation

### Scoped Operations
- All operations after the first async call are within the scope
- Automatic commit on success
- Automatic rollback on failure or exception
- Guaranteed resource cleanup

### Fluent Chaining
- Each method returns `ResultOperationScope<T, TOperation>`
- Extension methods on `Task<ResultOperationScope>` enable seamless chaining
- No need for intermediate variables or `await` breaks
- Maintains readability while handling async operations

### Clean Architecture
- Application Layer uses abstraction (e.g., `IRepositoryTransaction`)
- No direct dependency on infrastructure details
- Infrastructure Layer provides concrete implementation
- Testable with null implementations for unit tests

## Error Handling

### Scenario 1: Business Rule Violation

```
UnlessAsync(check rules) → FAILS
  └─> Result becomes Failure with validation error
      └─> Remaining Tap/BindAsync operations skipped
          └─> EndOperationAsync sees IsFailure
              └─> rollbackAsync() called
                  └─> Returns Result<TodoItemModel>.Failure
```

### Scenario 2: Database Insert Failure

```
BindAsync(repository.InsertResultAsync) → Exception
  └─> ResultOperationScope catches exception
      └─> EndOperationAsync catch block triggered
          └─> rollbackAsync() called
              └─> Returns Result.Failure with exception
```

### Scenario 3: Permission Provider Failure

```
Tap(set permissions) → Exception
  └─> EndOperationAsync catch block triggered
      └─> rollbackAsync() called
          └─> All changes rolled back
              └─> Returns Result.Failure with exception
```

**⚠️ All changes are rolled back in ALL error scenarios**

## Component Interactions

```
CommandHandler
      │
      ├─> IRepositoryTransaction<TEntity>
      │         │
      │         └─> EntityFrameworkRepositoryTransaction
      │                   │
      │                   └─> DbContext.Database.BeginTransactionAsync()
      │                         │
      │                         └─> IDbContextTransaction
      │                               └─> IRepositoryTransactionScope
      │
      ├─> IGenericRepository<TEntity>
      │         └─> Operations [within transaction]
      │
      └─> Other Services
                └─> Operations [within transaction]

All operations happen within the same scope!
```

## Best Practices

### 1. Always Provide Rollback Handler

```csharp
// ✅ Good: Explicit rollback handling
.EndOperationAsync(
    commitAsync: async (tx, ct) => await tx.CommitAsync(ct),
    rollbackAsync: async (tx, ex, ct) => await tx.RollbackAsync(ct),
    cancellationToken)

// ⚠️ Avoid: No rollback handler
.EndOperationAsync(
    commitAsync: async (tx, ct) => await tx.CommitAsync(ct),
    cancellationToken: cancellationToken)
```

### 2. Keep Synchronous Operations Before First Async

```csharp
// ✅ Good: Sync operations before operation starts
.StartOperation(...)
.Tap(e => e.UpdatedAt = DateTime.UtcNow)  // Sync, no operation yet
.Tap(e => e.UpdatedBy = userId)            // Sync, no operation yet
.TapAsync(async (e, ct) => ...)            // Operation starts here

// ⚠️ Less optimal: Unnecessary early operation start
.StartOperation(...)
.TapAsync(async (e, ct) => ...)            // Operation starts immediately
.Tap(e => e.UpdatedAt = DateTime.UtcNow)   // Could have been before
```

### 3. Use Dependency Injection

```csharp
public class MyCommandHandler(
    IRepositoryTransaction<MyEntity> transaction,
    IGenericRepository<MyEntity> repository)
{
    // ✅ Abstraction injected, not concrete implementation
}
```

### 4. Handle Domain Events Within Scope

```csharp
// ✅ Good: Domain events registered within scope
.Tap(e => e.DomainEvents.Register(new EntityCreatedEvent(e)))
.BindAsync(async (e, ct) => await repository.InsertResultAsync(e, ct))
.EndOperationAsync(...)  // Events and entity committed together
```

### 5. Test with Null Implementations

```csharp
[Fact]
public async Task Should_Create_TodoItem_Successfully()
{
    // Arrange
    var transaction = new NullRepositoryTransaction<TodoItem>();
    var handler = new TodoItemCreateCommandHandler(
        mapper,
        repository,
        permissionProvider,
        numberGenerator,
        currentUserAccessor,
        transaction);

    // Act
    var result = await handler.HandleAsync(command, options, cancellationToken);

    // Assert
    result.IsSuccess.Should().BeTrue();
}
```

## Performance Considerations

- **Lazy Start**: Operation only begins when needed, reducing overhead
- **Connection Pooling**: Resources reused efficiently
- **Parallel Sync Ops**: Multiple `Tap()` operations before first async are fast
- **Short Scopes**: Keep scope as short as possible
- **Avoid Long Operations**: Don't perform long-running operations within scope

## Migration Guide

### Before: Manual Resource Management

```csharp
using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    entity.UserId = currentUserAccessor.UserId;
    await repository.InsertAsync(entity, cancellationToken);
    await permissionProvider.SetPermissionsAsync(entity.Id, permissions);
    await transaction.CommitAsync(cancellationToken);
}
catch (Exception ex)
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

### After: ResultOperationScope

```csharp
return await Result<Entity>.Success(entity)
    .StartOperation(async ct => await transaction.BeginTransactionAsync(ct))
    .Tap(e => e.UserId = currentUserAccessor.UserId)
    .BindAsync(async (e, ct) => await repository.InsertResultAsync(e, ct), cancellationToken)
    .TapAsync(async (e, ct) => await permissionProvider.SetPermissionsAsync(e.Id, permissions), cancellationToken)
    .EndOperationAsync(
        commitAsync: async (tx, ct) => await tx.CommitAsync(ct),
        rollbackAsync: async (tx, ex, ct) => await tx.RollbackAsync(ct),
        cancellationToken);
```

---

# Use Cases Beyond Database Transactions

While `ResultOperationScope` is commonly demonstrated with database transactions, this pattern is a **generic scoped operation pattern** applicable to many scenarios.

## Use Case Categories

### 1. Resource Management
- Database Transactions
- File System Operations
- Network Connections
- Memory Allocations

### 2. Distributed Systems
- Distributed Locks
- Message Queue Batches
- Saga/Workflow Orchestration
- External API Sessions

### 3. State Management
- Cache Operations
- Configuration Changes
- State Machine Transitions
- Session Management

### 4. Security & Auditing
- Security Context Elevation
- Audit Trail Scopes
- Multi-Factor Authentication Flows
- Permission Context

## Detailed Use Cases with Examples

### 1. File System Operations

**Scenario**: Create multiple files atomically - if any operation fails, cleanup all created files.

```csharp
public interface IFileSystemScope
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public class FileSystemScope : IFileSystemScope
{
    private readonly List<string> createdFiles = new();
    private readonly List<string> createdDirectories = new();

    public void TrackFile(string filePath) => createdFiles.Add(filePath);
    public void TrackDirectory(string dirPath) => createdDirectories.Add(dirPath);

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Files are already written, just clear tracking
        createdFiles.Clear();
        createdDirectories.Clear();
        return Task.CompletedTask;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        // Delete all created files and directories
        foreach (var file in createdFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        foreach (var dir in createdDirectories.OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

// Usage
var result = await Result<ExportData>.Success(exportData)
    .StartOperation(ct => Task.FromResult<IFileSystemScope>(new FileSystemScope()))
    .Tap(data =>
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        fileScope.TrackDirectory(tempDir);
        data.TempDirectory = tempDir;
    })
    .TapAsync(async (data, ct) =>
    {
        var dataFile = Path.Combine(data.TempDirectory, "data.json");
        await File.WriteAllTextAsync(dataFile, JsonSerializer.Serialize(data), ct);
        fileScope.TrackFile(dataFile);
    }, cancellationToken)
    .EndOperationAsync(
        commitAsync: async (fs, ct) => await fs.CommitAsync(ct),
        rollbackAsync: async (fs, ex, ct) => await fs.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: All files created atomically, automatic cleanup on failure, no orphaned temp files

---

### 2. Distributed Lock Management

**Scenario**: Acquire distributed lock, perform work, release lock on success or failure.

```csharp
public interface IDistributedLockScope
{
    string LockId { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Process order with distributed lock
var result = await Result<Order>.Success(order)
    .StartOperation(async ct =>
    {
        var lockId = Guid.NewGuid().ToString();
        var acquired = await lockService.AcquireLockAsync(
            resource: $"order:{order.Id}",
            lockId: lockId,
            expiry: TimeSpan.FromMinutes(5),
            ct);

        if (!acquired)
            throw new LockAcquisitionException($"Could not acquire lock for order {order.Id}");

        return new RedisDistributedLock(redis, $"order:{order.Id}", lockId);
    })
    .EnsureAsync(async (o, ct) =>
        await orderValidator.ValidateAsync(o, ct),
        new ValidationError("Order validation failed"),
        cancellationToken)
    .BindAsync(async (o, ct) =>
        await inventoryService.ReserveItemsAsync(o.Items, ct), cancellationToken)
    .BindAsync(async (o, ct) =>
        await paymentService.ProcessPaymentAsync(o.Payment, ct), cancellationToken)
    // Lock is automatically released on success or failure
    .EndOperationAsync(
        commitAsync: async (lock, ct) => await lock.CommitAsync(ct),
        rollbackAsync: async (lock, ex, ct) => await lock.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: Guaranteed lock release (no deadlocks), prevents concurrent processing, clean error handling

---

### 3. Message Queue Batch Operations

**Scenario**: Publish multiple messages as a batch - commit batch on success, discard on failure.

```csharp
public interface IMessageBatchScope
{
    void AddMessage(Message message);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Publish order events
var result = await Result<Order>.Success(order)
    .StartOperation(ct => Task.FromResult<IMessageBatchScope>(
        new ServiceBusMessageBatch(serviceBusClient.CreateSender("orders"))))
    .Tap(o => batch.AddMessage(new OrderCreatedEvent(o.Id, o.CustomerId)))
    .Tap(o => batch.AddMessage(new InventoryReservedEvent(o.Id, o.Items)))
    .Tap(o => batch.AddMessage(new PaymentProcessedEvent(o.Id, o.Payment.Amount)))
    // Publish all messages atomically (or discard all on failure)
    .EndOperationAsync(
        commitAsync: async (batch, ct) => await batch.CommitAsync(ct),
        rollbackAsync: async (batch, ex, ct) => await batch.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: All messages published atomically, no partial message publishing, better performance with batching

---

### 4. Multi-Step Cache Update

**Scenario**: Update multiple cache entries, commit all changes or rollback if any update fails.

```csharp
public interface ICacheTransactionScope
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Update related cache entries
var result = await Result<Product>.Success(product)
    .StartOperation(ct => Task.FromResult<ICacheTransactionScope>(
        new RedisCacheTransaction(redis)))
    .TapAsync(async (p, ct) =>
    {
        var key = $"product:{p.Id}";
        var original = await cache.GetAsync(key, ct);
        cacheTransaction.StageUpdate(key, JsonSerializer.Serialize(p), original);
    }, cancellationToken)
    // Commit all cache updates or rollback
    .EndOperationAsync(
        commitAsync: async (tx, ct) => await tx.CommitAsync(ct),
        rollbackAsync: async (tx, ex, ct) => await tx.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: Cache consistency maintained, no stale cache entries, atomic cache updates

---

### 5. External API Session Management

**Scenario**: Establish session with external API, perform operations, close session properly.

```csharp
public interface IApiSessionScope
{
    string SessionToken { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Process payment with external gateway
var result = await Result<Payment>.Success(payment)
    .StartOperation(async ct =>
    {
        var session = new PaymentGatewaySession(httpClient);
        await session.InitializeAsync(ct);
        return session;
    })
    .EnsureAsync(async (p, ct) =>
        await paymentValidator.ValidateAsync(p, ct),
        new ValidationError("Invalid payment details"),
        cancellationToken)
    .BindAsync(async (p, ct) =>
        await gatewayClient.AuthorizeAsync(session.SessionToken, p, ct),
        cancellationToken)
    // Session is committed on success, rolled back on failure
    .EndOperationAsync(
        commitAsync: async (session, ct) => await session.CommitAsync(ct),
        rollbackAsync: async (session, ex, ct) => await session.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: Proper session cleanup, no hanging sessions, transactional API operations

---

### 6. Saga/Workflow Orchestration

**Scenario**: Execute multi-step workflow with compensation logic for rollback.

```csharp
public interface ISagaScope
{
    void RegisterCompensation(Func<CancellationToken, Task> compensation);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Book trip (flight + hotel + car) with compensations
var result = await Result<TripBooking>.Success(new TripBooking())
    .StartOperation(ct => Task.FromResult<ISagaScope>(new SagaOrchestrator()))
    .BindAsync(async (booking, ct) =>
    {
        var flight = await flightService.BookAsync(booking.FlightDetails, ct);
        saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
        booking.FlightConfirmation = flight.ConfirmationNumber;
        return Result<TripBooking>.Success(booking);
    }, cancellationToken)
    .BindAsync(async (booking, ct) =>
    {
        var hotel = await hotelService.BookAsync(booking.HotelDetails, ct);
        saga.RegisterCompensation(async ct => await hotelService.CancelAsync(hotel.Id, ct));
        booking.HotelConfirmation = hotel.ConfirmationNumber;
        return Result<TripBooking>.Success(booking);
    }, cancellationToken)
    // Commit all bookings or run compensations
    .EndOperationAsync(
        commitAsync: async (saga, ct) => await saga.CommitAsync(ct),
        rollbackAsync: async (saga, ex, ct) => await saga.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: Automatic compensation on failure, consistent state across services, implements Saga pattern elegantly

---

### 7. Security Context Elevation

**Scenario**: Temporarily elevate privileges, perform privileged operations, restore original context.

```csharp
public interface ISecurityContextScope
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Perform system maintenance tasks with elevated privileges
var result = await Result<SystemMaintenance>.Success(maintenance)
    .StartOperation(ct => Task.FromResult<ISecurityContextScope>(
        new ElevatedSecurityContext(
            userAccessor,
            SecurityContext.SystemAdministrator)))
    .TapAsync(async (m, ct) =>
        await databaseService.CleanupOrphanedRecordsAsync(ct),
        cancellationToken)
    .TapAsync(async (m, ct) =>
        await databaseService.RebuildIndexesAsync(ct),
        cancellationToken)
    // Context is automatically restored
    .EndOperationAsync(
        commitAsync: async (ctx, ct) => await ctx.CommitAsync(ct),
        rollbackAsync: async (ctx, ex, ct) => await ctx.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: Guaranteed privilege restoration, prevents privilege escalation leaks, auditable security operations

---

### 8. Audit Trail Scope

**Scenario**: Create audit scope, track operations, finalize audit trail on completion.

```csharp
public interface IAuditScope
{
    Guid AuditTrailId { get; }
    void LogOperation(string operation, object details);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

// Usage: Audit complex business operation
var result = await Result<Employee>.Success(employee)
    .StartOperation(ct => Task.FromResult<IAuditScope>(
        new AuditTrailScope(auditRepository)))
    .TapAsync(async (e, ct) =>
    {
        audit.LogOperation("UpdateSalary", new { OldSalary = e.Salary, NewSalary = newSalary });
        e.Salary = newSalary;
        await employeeRepository.UpdateAsync(e, ct);
    }, cancellationToken)
    // Finalize audit trail with success or failure status
    .EndOperationAsync(
        commitAsync: async (audit, ct) => await audit.CommitAsync(ct),
        rollbackAsync: async (audit, ex, ct) => await audit.RollbackAsync(ct),
        cancellationToken);
```

**Benefits**: Complete audit trail, tracks success and failure, compliance and traceability

---

## Summary Comparison Table

| Use Case | TOperation Type | Commit Action | Rollback Action | Key Benefit |
|----------|----------------|---------------|-----------------|-------------|
| **Database Transaction** | `IDbContextTransaction` | Persist changes | Discard changes | ACID guarantees |
| **File System** | `IFileSystemScope` | Keep files | Delete temp files | No orphaned files |
| **Distributed Lock** | `IDistributedLockScope` | Release lock | Release lock | No deadlocks |
| **Message Queue Batch** | `IMessageBatchScope` | Publish batch | Discard messages | Atomic publishing |
| **Cache Transaction** | `ICacheTransactionScope` | Write updates | Restore original | Cache consistency |
| **External API Session** | `IApiSessionScope` | Close session | Cancel session | Proper cleanup |
| **Saga/Workflow** | `ISagaScope` | Clear compensations | Run compensations | Distributed consistency |
| **Security Context** | `ISecurityContextScope` | Restore privileges | Restore privileges | No privilege leaks |
| **Audit Trail** | `IAuditScope` | Mark success | Mark failure | Complete traceability |

---

## Common Patterns

### Pattern 1: Resource Acquisition

```csharp
.StartOperation(async ct =>
{
    var resource = await AcquireResourceAsync(ct);
    return new ResourceScope(resource);
})
```

### Pattern 2: Staged Operations

```csharp
.TapAsync(async (entity, ct) =>
{
    scope.Stage(operation: "Step1", data: entity);
}, cancellationToken)
```

### Pattern 3: Compensation Registration

```csharp
.TapAsync(async (entity, ct) =>
{
    var result = await ExternalService.CreateAsync(entity, ct);
    scope.RegisterCompensation(async ct =>
        await ExternalService.DeleteAsync(result.Id, ct));
}, cancellationToken)
```

### Pattern 4: Finalization

```csharp
.EndOperationAsync(
    commitAsync: async (scope, ct) => await scope.FinalizeAsync(ct),
    rollbackAsync: async (scope, ex, ct) => await scope.CleanupAsync(ct),
    cancellationToken)
```

---

## Creating Custom Scopes

### Step 1: Define Interface

```csharp
public interface IMyCustomScope
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

### Step 2: Implement Scope

```csharp
public class MyCustomScope : IMyCustomScope
{
    // Track state and resources
    private readonly List<IDisposable> resources = new();

    public void Track(IDisposable resource) => resources.Add(resource);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Finalize operation
        await FinalizeAsync(cancellationToken);

        // Clean up resources
        foreach (var resource in resources)
            resource.Dispose();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        // Undo changes
        await UndoChangesAsync(cancellationToken);

        // Clean up resources
        foreach (var resource in resources)
            resource.Dispose();
    }
}
```

### Step 3: Use with ResultOperationScope

```csharp
var result = await Result<MyData>.Success(data)
    .StartOperation(ct => Task.FromResult<IMyCustomScope>(new MyCustomScope()))
    .TapAsync(async (d, ct) => /* operations */, cancellationToken)
    .EndOperationAsync(
        commitAsync: async (scope, ct) => await scope.CommitAsync(ct),
        rollbackAsync: async (scope, ex, ct) => await scope.RollbackAsync(ct),
        cancellationToken);
```

---

## Design Principles

1. **Single Responsibility**: Each scope manages one type of resource or operation
2. **Idempotency**: Commit/Rollback can be called multiple times safely
3. **Exception Safety**: Always clean up resources in finally blocks
4. **Lazy Initialization**: Start operation only when needed (first async call)
5. **Composition**: Multiple scopes can be nested if needed

---

## When to Use This Pattern

✅ **Use When:**
- Operations need all-or-nothing semantics
- Resources must be acquired and released
- Cleanup is required on failure
- Multiple steps must be atomic
- Rollback/compensation logic exists

❌ **Avoid When:**
- Simple, single-step operations
- No cleanup needed
- No rollback semantics
- Fire-and-forget scenarios

---

## Related Patterns

- **Unit of Work**: Similar concept for aggregating operations
- **Saga Pattern**: Distributed transaction management
- **Two-Phase Commit**: Distributed consensus protocol
- **Command Pattern**: Encapsulates operations with undo
- **Memento Pattern**: Save/restore object state

---

## Related Documentation

- [Railway-Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)

## License

MIT-License - Copyright BridgingIT GmbH
