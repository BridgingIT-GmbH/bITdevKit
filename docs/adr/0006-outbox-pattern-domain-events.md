# ADR-0006: Outbox Pattern for Domain Events

## Status

Accepted

## Context

Domain-Driven Design relies on domain events to maintain consistency between aggregates and trigger side effects. However, publishing events has significant challenges:

**Challenges with Direct Event Publishing**:

- **Dual Write Problem**: Saving entity and publishing event are separate operations (can fail independently)
- **Lost Events**: Event publish fails after entity saved → event lost, system inconsistent
- **Partial Failures**: Entity saved but event not published → downstream systems never notified
- **No Transactional Guarantee**: Cannot atomically save entity + publish event to message broker
- **Ordering Issues**: Events may arrive out of order if published immediately
- **Idempotency**: Same event may be published multiple times on retries

**Requirements**:

1. Guarantee events are published if and only if entity changes are persisted
2. Maintain event ordering (events published in the order they occurred)
3. Support at-least-once delivery semantics
4. Allow event processing to be delayed/batched for performance
5. Provide event audit trail (which events were published when)

## Decision

Adopt the **Outbox Pattern** using bITdevKit's `RepositoryOutboxDomainEventBehavior` to ensure reliable, transactional domain event delivery.

### Pattern Mechanics

1. **Event Registration**: Domain aggregates register events in memory

   ```csharp
   customer.DomainEvents.Register(new CustomerCreatedDomainEvent(customer));
   ```

2. **Outbox Persistence**: Repository behavior extracts events and persists to `OutboxDomainEvents` table in same transaction as entity

   ```csharp
   services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
       .WithBehavior<RepositoryOutboxDomainEventBehavior<Customer, CoreModuleDbContext>>();
   ```

3. **Outbox Worker**: Background service polls outbox table, publishes events via notifier, marks as processed

   ```csharp
   services.AddSqlServerDbContext<CoreModuleDbContext>()
       .WithOutboxDomainEventService(o => o
           .ProcessingInterval("00:00:30")      // Poll every 30 seconds
           .ProcessingModeImmediate()           // Forward to notifier immediately
           .PurgeOnStartup());                  // Clean old processed events
   ```

### Outbox Table Structure

```sql
CREATE TABLE OutboxDomainEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(512) NOT NULL,
    AggregateId NVARCHAR(256) NOT NULL,
    AggregateType NVARCHAR(512) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,            -- Serialized event JSON
    OccurredOn DATETIMEOFFSET NOT NULL,
    ProcessedOn DATETIMEOFFSET NULL,          -- NULL = pending
    ProcessingAttempts INT DEFAULT 0,
    ErrorMessage NVARCHAR(MAX) NULL
);
```

### Execution Flow

```
1. Handler calls repository.InsertResultAsync(customer)
2. RepositoryOutboxDomainEventBehavior:
   a. Extracts customer.DomainEvents
   b. Serializes each event to JSON
   c. Inserts OutboxDomainEvent records
3. EF Core SaveChangesAsync() commits:
   a. Customer entity INSERT
   b. OutboxDomainEvent records INSERT
   [Both in same transaction - atomicity guaranteed]
4. customer.DomainEvents.Clear()
5. OutboxWorker (background service):
   a. SELECT * FROM OutboxDomainEvents WHERE ProcessedOn IS NULL
   b. Deserialize each event
   c. notifier.PublishAsync(event)
   d. UPDATE OutboxDomainEvents SET ProcessedOn = NOW()
```

## Rationale

1. **Transactional Guarantee**: Entity and events saved in same database transaction (atomicity)
2. **Reliability**: Events cannot be lost (persisted durably before processing)
3. **At-Least-Once Delivery**: Worker retries failed events until processed
4. **Ordering**: Events processed in `OccurredOn` order
5. **Audit Trail**: Complete history of events in outbox table
6. **Decoupling**: Event publishing happens asynchronously (doesn't slow down request)
7. **Idempotency**: Event handlers should be idempotent (may receive same event twice)

## Consequences

### Positive

- Zero event loss (events persisted with entity in same transaction)
- Guaranteed eventual consistency (events will be processed)
- Complete audit trail of all domain events in database
- Event processing decoupled from request handling (better performance)
- Can replay events by marking ProcessedOn = NULL
- Failed event processing doesn't fail entity persistence
- Can batch event processing for efficiency

### Negative

- Events processed asynchronously (eventual consistency, not immediate)
- Outbox table grows over time (requires purging old events)
- Outbox worker adds complexity (background service to manage)
- Small performance overhead (extra inserts per entity save)
- Event handlers must be idempotent (may receive duplicates on retries)

### Neutral

- Events processed by polling (configurable interval, e.g., 30 seconds)
- Old processed events purged on startup (configurable retention)
- Processing mode can be immediate or batched

## Alternatives Considered

- **Alternative 1: Direct Event Publishing (In-Process)**
  - Rejected due to dual write problem (entity saved but event publish fails)
  - No transactional guarantee between persistence and event delivery
  - Events lost on publish failure

- **Alternative 2: Two-Phase Commit (2PC)**
  - Rejected due to complexity and poor performance
  - Requires distributed transaction coordinator
  - Not supported by many message brokers

- **Alternative 3: Change Data Capture (CDC)**
  - Rejected because it's database-specific and infrastructure-heavy
  - Requires external tooling (Debezium, etc.)
  - Less explicit than outbox (developers don't see event flow)

- **Alternative 4: Event Sourcing**
  - Rejected because it's a much larger architectural change
  - Requires storing all state as events (not just domain events)
  - More complex than needed for this use case

## Related Decisions

- [ADR-0004](0004-repository-decorator-behaviors.md): Outbox is a repository behavior
- [ADR-0005](0005-requester-notifier-mediator-pattern.md): Notifier publishes outbox events to handlers
- [ADR-0012](0012-domain-logic-in-domain-layer.md): Domain aggregates register events

## References

- [bITdevKit Domain Events Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-domain-events.md)
- [README - Request Processing Flow](../../README.md#request-processing-flow)
- [CoreModule README - Domain Events](../../src/Modules/CoreModule/CoreModule-README.md#domain-events-in-coremodule)
- [Outbox Pattern - Martin Fowler](https://microservices.io/patterns/data/transactional-outbox.html)

## Notes

### Configuration Example

```csharp
// CoreModuleModule.cs

// 1. Register repository with outbox behavior
services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
    .WithBehavior<RepositoryOutboxDomainEventBehavior<Customer, CoreModuleDbContext>>();

// 2. Configure outbox worker
services.AddSqlServerDbContext<CoreModuleDbContext>()
    .WithOutboxDomainEventService(o => o
        .ProcessingInterval("00:00:30")       // Poll every 30 seconds
        .ProcessingModeImmediate()            // Forward immediately (vs batched)
        .StartupDelay("00:00:15")             // Wait 15 seconds before first poll
        .PurgeOnStartup());                   // Delete old processed events on startup
```

### Domain Event Registration

```csharp
// Customer.cs
public static Result<Customer> Create(...)
{
    var customer = new Customer(firstName, lastName, email, number);
    customer.DomainEvents.Register(new CustomerCreatedDomainEvent(customer));
    return customer;
}

public Result<Customer> ChangeEmail(string email)
{
    // ... validation ...
    this.Email = emailResult.Value;
    this.DomainEvents.Register(new CustomerUpdatedDomainEvent(this), replace: true);
    return this;
}
```

### Event Handler

```csharp
public class CustomerCreatedDomainEventHandler :
    DomainEventHandlerBase<CustomerCreatedDomainEvent>
{
    public override async Task Process(
        CustomerCreatedDomainEvent notification,
        CancellationToken ct)
    {
        // Send welcome email
        // Update read model
        // Trigger external integration

        // IMPORTANT: Handlers must be idempotent (may be called multiple times)
    }
}
```

### Outbox Table Query Examples

**Pending Events**:

```sql
SELECT * FROM OutboxDomainEvents
WHERE ProcessedOn IS NULL
ORDER BY OccurredOn;
```

**Failed Events** (requires retry):

```sql
SELECT * FROM OutboxDomainEvents
WHERE ProcessedOn IS NULL
  AND ProcessingAttempts > 3
  AND ErrorMessage IS NOT NULL;
```

**Event History for Aggregate**:

```sql
SELECT EventType, OccurredOn, ProcessedOn
FROM OutboxDomainEvents
WHERE AggregateId = '123e4567-e89b-12d3-a456-426614174000'
ORDER BY OccurredOn;
```

### Idempotency Considerations

Event handlers must be idempotent because:

- Outbox worker may crash mid-processing (event marked processed but handler didn't complete)
- Network failures may cause retries
- Manual replay of events for debugging

**Idempotent Handler Example**:

```csharp
public override async Task Process(CustomerCreatedDomainEvent notification, CancellationToken ct)
{
    // Check if already processed
    var existing = await _readModelRepo.FindByIdAsync(notification.Model.Id, ct);
    if (existing != null)
    {
        _logger.LogInformation("Event already processed, skipping");
        return;
    }

    // Process event
    await _readModelRepo.InsertAsync(new CustomerReadModel(notification.Model), ct);
}
```

### Processing Modes

**ProcessingModeImmediate** (default):

- Events forwarded to notifier as soon as discovered
- Lower latency (near real-time)
- More frequent polling

**ProcessingModeBatched**:

- Events batched before forwarding
- Better throughput for high-volume scenarios
- Configurable batch size

### Monitoring & Troubleshooting

**Check Pending Events**:

```csharp
var pendingCount = await dbContext.OutboxDomainEvents
    .Where(e => e.ProcessedOn == null)
    .CountAsync();
```

**Manually Replay Event**:

```sql
UPDATE OutboxDomainEvents
SET ProcessedOn = NULL, ProcessingAttempts = 0, ErrorMessage = NULL
WHERE Id = '...';
```

**Purge Old Events**:

```sql
DELETE FROM OutboxDomainEvents
WHERE ProcessedOn < DATEADD(day, -30, GETDATE());
```

### Implementation Files

- **Behavior config**: `src/Modules/CoreModule/CoreModule.Presentation/CoreModuleModule.cs`
- **Outbox table**: `src/Modules/CoreModule/CoreModule.Infrastructure/EntityFramework/Migrations/`
- **DbContext interface**: `CoreModuleDbContext : IOutboxDomainEventContext`
- **Event registration**: `src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Customer.cs`
- **Event handlers**: `src/Modules/CoreModule/CoreModule.Application/Events/`
