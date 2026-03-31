# Domain Events Feature Documentation

> Capture business-significant events in aggregates and publish side effects outside the domain model.

[TOC]

## Overview

Domain events capture business-significant things that already happened inside an aggregate, such as `CustomerCreatedDomainEvent` or `SubscriptionCancelledDomainEvent`. They let the aggregate record the fact while keeping side effects, projections, messaging, and notifications outside the aggregate itself.

In bITdevKit, aggregates collect events in their `DomainEvents` collection and repository behaviors publish them after persistence. That gives you two common operating modes:

- direct in-process publication for simple, immediate reactions
- reliable outbox-backed publication for side effects that must survive restarts or transient failures

This page focuses on classic domain events raised by aggregates in the `Domain.*` model. Event-sourced aggregates are documented separately in [Event Sourcing](./features-event-sourcing.md).

When domain events are persisted through the outbox or forwarded into asynchronous messaging flows, their payloads depend on the shared serializer infrastructure documented in [Common Serialization](./common-serialization.md).

## Challenges

- Aggregates should express business state changes without knowing which handlers react to them.
- Side effects such as notifications, cache updates, or integration messages should not run inside aggregate methods.
- Publishing must happen after persistence, not before.
- Some reactions can run immediately, while others need durable retryable delivery.

## Solution

The devkit uses a small set of building blocks:

- `DomainEventBase` and related base types model immutable domain events.
- Aggregates register events through `DomainEvents.Register(...)`.
- `RepositoryDomainEventPublisherBehavior<TEntity>` publishes events directly after repository persistence.
- `RepositoryOutboxDomainEventBehavior<TEntity, TContext>` stores events in an outbox table and lets a background worker publish them reliably later.
- Event handlers subscribe through the notifier infrastructure documented in [Requester and Notifier](./features-requester-notifier.md).

## Raising Domain Events

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

## Publication Modes

### Direct Publication

`RepositoryDomainEventPublisherBehavior<TEntity>` is the simplest option. After the repository saves the aggregate, it sends each registered event through the configured domain-event publisher and clears the aggregate's event collection.

This mode works well when:

- reactions are purely in-process
- you do not need durable retries
- a failed handler should fail the current unit of work immediately

Setup:

```csharp
services.AddNotifier()
    .AddHandlers();

services.AddEntityFrameworkRepository<Customer, CoreDbContext>()
    .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>();
```

### Reliable Outbox Publication

`RepositoryOutboxDomainEventBehavior<TEntity, TContext>` persists each registered domain event into an outbox table. A hosted background service later reads those rows, deserializes the events, and publishes them through the notifier.

This mode works well when:

- handlers trigger infrastructure side effects
- event delivery must survive application restarts
- retries and delayed processing are acceptable
- you want persistence and event recording to happen in one reliable flow

The outbox row stores event metadata such as event id, type name, serialized content, timestamps, correlation data, and processing state.

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
    .RetryCount(3));
```

The hosted service delays startup until the host is ready, periodically reads unprocessed rows, publishes the deserialized domain events, and updates the processing metadata.

## Processing Modes

`OutboxDomainEventOptions` supports two common processing styles:

- `Interval`: the hosted service polls the outbox on a configured interval
- `Immediate`: newly stored event ids are queued for near-immediate processing in addition to the hosted worker

Other useful options control startup delay, processing interval, retry count, serializer choice, batch size, and whether processed rows should be purged on startup.

## Handlers

Handlers remain ordinary domain-event handlers. They do not need to know whether the event reached them directly or through the outbox.

```csharp
public class CustomerCreatedHandler(ILoggerFactory loggerFactory)
    : DomainEventHandlerBase<CustomerCreatedDomainEvent>(loggerFactory)
{
    public override async Task Process(
        CustomerCreatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
    }
}
```

This separation is the main benefit of the feature: the publishing strategy can change without changing the handler contract.

## When To Use Which Mode

- Use direct publication when the reaction is local, lightweight, and should complete as part of the current flow.
- Use outbox-backed publication when the reaction touches infrastructure, needs retry behavior, or must not be lost after the aggregate was already persisted.

Many production modules standardize on the outbox behavior for aggregate persistence because it gives a safer default for post-persistence side effects.

## Relationship To Other Features

- [Domain](./features-domain.md) covers aggregates, typed ids, and fluent change patterns.
- [Domain Repositories](./features-domain-repositories.md) covers repository abstractions and decorator behaviors in general.
- [Requester and Notifier](./features-requester-notifier.md) covers the in-process handler infrastructure used by domain-event delivery.
- [Event Sourcing](./features-event-sourcing.md) covers the separate aggregate-event stream model and its own outbox flow.
