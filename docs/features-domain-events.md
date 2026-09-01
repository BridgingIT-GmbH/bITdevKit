# Domain Events

> Capture business-significant events in aggregates and publish side effects outside the domain model.

[TOC]

## Overview

Domain events capture business-significant things that already happened inside an aggregate, such as `CustomerCreatedDomainEvent` or `SubscriptionCancelledDomainEvent`. They let the aggregate record the fact while keeping side effects, projections, messaging, and notifications outside the aggregate itself.

In bITdevKit, aggregates collect events in their `DomainEvents` collection and repository behaviors publish them after persistence. That gives you two common operating modes:

- direct in-process publication for simple, immediate reactions
- durable outbox-backed publication for side effects that must survive restarts or transient failures

This page focuses on classic domain events raised by aggregates in the `Domain.*` model. Event-sourced aggregates are documented separately in [Event Sourcing](./features-event-sourcing.md).

When domain events are persisted through the outbox or forwarded into asynchronous messaging flows, their payloads depend on the shared serializer infrastructure documented in [Common Serialization](./common-serialization.md).

## Challenges

- Aggregates should express business state changes without knowing which handlers react to them.
- Side effects such as notifications, cache updates, or integration messages should not run inside aggregate methods.
- Publishing must happen after persistence, not before.
- Some reactions can run immediately, while others need durable retryable delivery.
- Multi-node deployments need workers to coordinate ownership of persisted events.

## Solution

The devkit uses a small set of building blocks:

- `DomainEventBase` and related base types model immutable domain events.
- Aggregates register events through `DomainEvents.Register(...)`.
- `RepositoryDomainEventPublisherBehavior<TEntity>` publishes events directly after repository persistence.
- `RepositoryOutboxDomainEventBehavior<TEntity, TContext>` stores events in an outbox table for later background publication.
- `OutboxDomainEventWorker<TContext>` claims persisted rows with optimistic concurrency and renewable leases so multiple hosts can compete for work.
- Event handlers subscribe through the notifier infrastructure documented in [Requester and Notifier](./features-requester-notifier.md).

## Key Features

- Aggregate-owned event registration through `DomainEvents`
- Direct publication after repository persistence
- Entity Framework outbox persistence and background dispatch
- Interval and immediate processing modes
- Retry, batch, lease, and archive controls
- Multi-node processing through leases and optimistic concurrency
- Notifier-compatible domain-event handlers

## Architecture

An aggregate registers an `IDomainEvent`. A configured repository behavior acts after the inner
repository operation. The direct behavior sends each event through `IDomainEventPublisher` and then
clears the aggregate collection. The outbox behavior serializes events into `OutboxDomainEvent`
rows; `OutboxDomainEventWorker<TContext>` later claims and publishes eligible rows.

## Use Cases

- Run local projections or cache invalidation after an aggregate is persisted.
- Send notifications or integration messages without coupling them to aggregate methods.
- Retain failed deliveries for retry after a restart.
- Process one outbox from several application nodes.
- Keep processed event records for diagnostics before archiving or purging them.

## Basic Usage

This example changes aggregate state, handles a validation failure, and confirms that the aggregate
registered one event. A repository behavior publishes the event after persistence.

```csharp
public sealed class CustomerRenamedDomainEvent(Guid customerId, string name) : DomainEventBase
{
	public Guid CustomerId { get; } = customerId;
	public string Name { get; } = name;
}

public sealed class Customer : AggregateRoot<Guid>
{
	public string Name { get; set; }

	public Result<Customer> Rename(string name)
	{
		return this.Change()
			.Ensure(_ => !string.IsNullOrWhiteSpace(name), "A name is required.")
			.Set(customer => customer.Name, name.Trim())
			.Register(customer => new CustomerRenamedDomainEvent(customer.Id, customer.Name))
			.Apply();
	}
}

var customer = new Customer { Id = Guid.NewGuid(), Name = "Ada Lovelace" };
var result = customer.Rename("Grace Hopper");

if (result.IsFailure)
{
	Console.Error.WriteLine(
		string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
	return;
}

Console.WriteLine(customer.DomainEvents.GetAll().Single().GetType().Name);
```

Output:

```text
CustomerRenamedDomainEvent
```

## Raising domain events

Aggregates register events when a meaningful state transition happens:

```csharp
public sealed class CustomerCreatedDomainEvent(Customer customer) : DomainEventBase
{
    public Customer Customer { get; } = customer;
}

public class Customer : AuditableAggregateRoot<CustomerId>
{
    public static Result<Customer> Create(string firstName, string lastName, string email)
    {
        var emailResult = EmailAddress.Create(email);
        if (emailResult.IsFailure)
        {
            return emailResult.Unwrap();
        }

        var customer = new Customer(firstName, lastName, emailResult.Value);
        customer.DomainEvents.Register(new CustomerCreatedDomainEvent(customer));

        return customer;
    }
}
```

The aggregate only records the event. It does not publish it directly and it does not know who reacts to it.

## Publication modes

### Direct publication

`RepositoryDomainEventPublisherBehavior<TEntity>` is the simplest option. After the repository saves the aggregate, it sends each registered event through the configured domain-event publisher and clears the aggregate's event collection.

This mode works well when:

- reactions are purely in-process
- you do not need durable retries
- a failed handler should propagate to the repository caller after persistence

Setup:

```csharp
services.AddNotifier()
    .AddHandlers();

services.AddEntityFrameworkRepository<Customer, CoreDbContext>()
    .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>();
```

### Outbox publication

`RepositoryOutboxDomainEventBehavior<TEntity, TContext>` persists each registered domain event into an outbox table. A hosted background service later triggers the worker, which claims eligible rows, deserializes the events, and publishes them through the notifier.

This mode works well when:

- handlers trigger infrastructure side effects
- event delivery must survive application restarts
- retries and delayed processing are acceptable
- you can provide a transaction boundary that includes aggregate and outbox persistence

The outbox row stores event metadata such as event id, type name, serialized content, timestamps, correlation data, processing state, optimistic concurrency data, lease ownership, and archive state.

The repository behavior first calls the inner repository and then adds outbox rows. With the default
`AutoSave = true`, it can issue another `SaveChangesAsync` call for those rows. Aggregate persistence
and outbox persistence are therefore atomic only when a surrounding database transaction includes
both operations. Do not infer an atomic outbox guarantee from the decorator alone.

## Setup

### 1. Register handlers

Domain-event handlers still use the notifier infrastructure:

```csharp
services.AddNotifier()
    .AddHandlers();
```

### 2. Use the outbox repository behavior

Decorate the repository for aggregates that should write domain events into the outbox:

```csharp
services.AddEntityFrameworkRepository<Customer, CoreDbContext>()
    .WithBehavior<RepositoryOutboxDomainEventBehavior<Customer, CoreDbContext>>();
```

### 3. Make the DbContext expose the outbox set

The DbContext used by the repository must implement `IOutboxDomainEventContext`:

```csharp
public class CoreDbContext : DbContext, IOutboxDomainEventContext
{
    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }
}
```

### 4. Register the outbox service

```csharp
services.AddOutboxDomainEventService<CoreDbContext>(options => options
    .ProcessingMode(OutboxDomainEventProcessMode.Interval)
    .ProcessingInterval(TimeSpan.FromSeconds(30))
    .RetryCount(3)
    .LeaseDuration(TimeSpan.FromSeconds(30))
    .LeaseRenewalInterval(TimeSpan.FromSeconds(10))
    .AutoArchiveAfter(TimeSpan.FromHours(1)));
```

The hosted service delays startup until the host is ready, periodically triggers processing, and can archive processed rows once they are older than the configured retention threshold.

## Multi-node processing

The current outbox implementation supports multiple competing workers across different hosts.

- workers only scan rows that are not processed and not archived
- a worker must first claim a row by writing lease metadata such as `LockedBy`, `LockedUntil`, and a new `ConcurrencyVersion`
- long-running processing renews the lease periodically
- if a worker loses ownership, it does not persist the final state for that row
- when processing completes, the worker clears the lease and stores the resulting processing metadata

These ownership checks coordinate delivery when several nodes poll the same table.

## Processing modes

`OutboxDomainEventOptions` supports two common processing styles:

- `Interval`: the hosted service polls the outbox on a configured interval
- `Immediate`: newly stored event ids are queued for near-immediate processing in addition to the hosted worker

Other useful options control startup delay, processing interval, retry count, serializer choice, batch size, lease duration, lease renewal cadence, automatic archiving, and whether processed rows should be purged on startup.

## Archiving

Processed outbox rows can be retained without staying in the active worker set.

- `AutoArchiveAfter` defines how old a processed row must be before it becomes archivable
- `OutboxDomainEventService` triggers archiving during its scheduled work cycle
- archiving marks rows with `IsArchived` and `ArchivedDate`
- archived rows are ignored by normal processing scans

Archiving is different from purging: archiving keeps rows for diagnostics, while purging deletes them.

## Handlers

Handlers remain ordinary domain-event handlers. They do not need to know whether the event reached them directly or through the outbox.

```csharp
public class CustomerCreatedHandler(ILoggerFactory loggerFactory)
    : DomainEventHandlerBase<CustomerCreatedDomainEvent>(loggerFactory)
{
    public override bool CanHandle(CustomerCreatedDomainEvent notification) => true;

    public override async Task Process(
        CustomerCreatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
    }
}
```

Handlers use the same contract for direct and outbox-backed publication.

## When to use each mode

- Use direct publication when the reaction is local, lightweight, and should complete as part of the current flow.
- Use outbox-backed publication when the reaction touches infrastructure, needs retry behavior, or must not be lost after the aggregate was already persisted.

Modules that need retryable post-persistence side effects can standardize on the outbox behavior,
provided they also define the required transaction boundary.

## Relationship to other features

- [Domain](./features-domain.md) covers aggregates, typed ids, and fluent change patterns.
- [Domain Repositories](./features-domain-repositories.md) covers repository abstractions and decorator behaviors in general.
- [Requester and Notifier](./features-requester-notifier.md) covers the in-process handler infrastructure used by domain-event delivery.
- [Event Sourcing](./features-event-sourcing.md) covers the separate aggregate-event stream model and its own outbox flow.
