# Event Sourcing

> Persist aggregates as immutable event streams and rebuild state through replay and snapshots.

[TOC]

## Overview

Event sourcing stores aggregate changes as an ordered stream of immutable aggregate events instead of only persisting the latest state. The current aggregate state is reconstructed by replaying those events, optionally accelerated through snapshots.

In bITdevKit, event sourcing is a separate feature from classic domain events:

- classic domain events describe post-persistence business occurrences on regular aggregates
- event sourcing models the aggregate itself as a stream of versioned aggregate events

This feature also includes its own outbox path for durable post-commit event distribution.

Persisted aggregate events, snapshots, and event-sourcing outbox payloads are closely tied to the shared serializer abstractions and conventions documented in [Common Serialization](./common-serialization.md).

## Challenges

State-based persistence keeps the latest aggregate state but does not retain the decisions that
produced it. Adding replay, optimistic stream versions, snapshots, and reliable downstream
publication separately creates several persistence and registration concerns for each aggregate.

## Solution

The event-sourcing packages define versioned aggregate events, an event-sourced aggregate base, and
an `IEventStore<TAggregate>` abstraction. Infrastructure persists streams and snapshots and can route
stored events through mediator requests, notifications, or the event-store outbox.

## Key Features

- Ordered, versioned aggregate-event streams
- Aggregate reconstruction through event replay
- Optional snapshots
- Stable persisted type names through `ImmutableNameAttribute`
- Configurable mediator and outbox publication modes
- Aggregate-specific store and projection registration

## Architecture

### Aggregate events

Aggregate events inherit from `AggregateEvent`, which extends the domain-event concept with aggregate versioning:

```csharp
[ImmutableName("person-created-v1")]
public class PersonCreated(Guid id, int version, string firstName, string lastName)
    : AggregateEvent(id, version)
{
    public string FirstName { get; } = firstName;
    public string LastName { get; } = lastName;
}
```

Each event belongs to exactly one aggregate instance and one aggregate version.

### Event-sourced aggregate root

Event-sourced aggregates inherit from `EventSourcingAggregateRoot`. They:

- receive new events through domain methods
- keep uncommitted events in `UnsavedEvents`
- replay historic events to rebuild state
- enforce sequential versioning while integrating events

The aggregate state is changed by applying events, not by directly mutating persistence-facing properties first.

### Event store

`IEventStore<TAggregate>` persists and reloads the event stream for one aggregate type. The default implementation writes events through infrastructure repositories and can rebuild an aggregate by replaying its stored stream.

Typical responsibilities are:

- append unsaved aggregate events
- load all events for one aggregate id
- rebuild the aggregate from stored events
- optionally use snapshots to avoid full replay every time

### Registration and immutable names

Stored events need stable names so previously persisted data can still be resolved even when CLR type names move. Automatic registration discovers concrete aggregates and aggregate events marked with `ImmutableNameAttribute` and maps their persisted names to runtime types.

## Use Cases

Use event sourcing when you need:

- a complete change history for an aggregate
- replayable state reconstruction
- explicit aggregate-version control
- projection building from event streams
- durable downstream distribution of stored aggregate events

It is usually a deliberate architectural choice rather than the default for every module.

## Basic Usage

Register the store context and one aggregate type:

```csharp
services.AddEventStoreContextSqlServer<AppEventStoreDbContext>(
    connectionString,
    nameOfMigrationsAssembly: typeof(AppEventStoreDbContext).Assembly.GetName().Name,
    defaultSchema: "events",
    eventStorePublishingModes: EventStorePublishingModes.AddToOutbox,
    maxRetryCount: 3,
    maxRetryDelaySeconds: 5);

services.RegisterAggregateAndProjectionRequestForEventStore<Person>(
    projectionRequestPublishingModes: EventStorePublishingModes.None,
    isSnapshotEnabled: true);
```

Create, save, and reload an aggregate through `IEventStore<Person>`:

```csharp
public sealed class CreatePerson(IEventStore<Person> eventStore)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var person = new Person(new PersonCreatedEvent("Lovelace", "Ada"));
        await eventStore.SaveEventsAsync(person, cancellationToken);

        var reloaded = await eventStore.GetAsync(person.Id, cancellationToken);
        if (reloaded is null)
        {
            Console.Error.WriteLine("The saved person could not be reloaded.");
            return;
        }

        Console.WriteLine($"{reloaded.Firstname} {reloaded.Lastname}, version {reloaded.Version}");
    }
}
```

The output after storing the creation event is:

```text
Ada Lovelace, version 1
```

## Setup

The feature spans `Domain.EventSourcing` plus infrastructure packages that provide storage and registration.

### 1. Register the event store

At the lower level, the EF-backed store can be registered directly:

```csharp
services.AddEfCoreEventStore<MyEventStoreDbContext>(
    defaultSchema: "dbo",
    eventStorePublishingModes: EventStorePublishingModes.AddToOutbox);
```

That registration wires the core event-store services, aggregate-event repositories, snapshot support, and the EF-backed event-store outbox infrastructure.

In a SQL Server host, the higher-level registration also configures the context:

```csharp
services.AddEventStoreContextSqlServer<MyEventStoreDbContext>(
    connectionString,
    nameOfMigrationsAssembly,
    defaultSchema: "dbo",
    eventStorePublishingModes: EventStorePublishingModes.AddToOutbox,
    maxRetryCount: 0,
    maxRetryDelaySeconds: 0);
```

### 2. Register aggregate-specific access

```csharp
services.RegisterAggregateAndProjectionRequestForEventStore<MyAggregate>(
    projectionRequestPublishingModes: EventStorePublishingModes.None,
    isSnapshotEnabled: true);
```

This makes `IEventStore<MyAggregate>` available and configures snapshot behavior for that aggregate type.

### 3. Use an event-store `DbContext`

The EF-backed store expects a DbContext derived from `EventStoreDbContext`, which already defines tables for:

- aggregate events
- snapshots
- the event-sourcing outbox

## Aggregate workflow

The typical event-sourced flow looks like this:

1. Load an aggregate by replaying its stored event stream, or from a snapshot plus newer events.
2. Execute a domain method that emits one or more new aggregate events.
3. Save the aggregate through `IEventStore<TAggregate>`.
4. Append the unsaved events to the event store.
5. Trigger any configured downstream publication or outbox handling.

That means the event stream is the source of truth and projections or read models are downstream consumers.

## Outbox support

Event sourcing has its own outbox path, separate from the classic `RepositoryOutboxDomainEventBehavior` used by regular repositories.

When `EventStorePublishingModes.AddToOutbox` is enabled:

- persisted aggregate events are also written to the event-store outbox
- a worker can later read those outbox messages
- downstream projection or integration processing can happen from durable stored messages instead of only from immediate in-process dispatch

Register the worker with:

```csharp
services.AddEfOutboxWorker();
```

`AddEfOutboxWorker()` registers `IOutboxWorkerService`; it does not start a hosted worker. Invoke
`DoWorkAsync()` from a scheduled job or another application-owned trigger. Each run processes
unhandled messages and marks a message as processed after the configured downstream actions succeed.

## Snapshots

Snapshots are an optimization, not the primary source of truth. The canonical history remains the aggregate event stream.

Enable snapshots when:

- aggregate streams grow large
- replay cost becomes noticeable
- you still want deterministic rebuilds when a full replay is necessary

If snapshots are disabled, the aggregate is rebuilt from the full stream every time it is loaded.

## Relationship to other features

- [Domain](./features-domain.md) covers regular aggregates, value objects, typed ids, and fluent change patterns.
- [Domain Events](./features-domain-events.md) covers domain events for regular aggregates and the repository-based domain-event outbox.
- [Requester and Notifier](./features-requester-notifier.md) documents the general in-process dispatch infrastructure used elsewhere in the kit.
