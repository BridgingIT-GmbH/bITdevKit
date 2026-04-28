# Queueing Feature Documentation

> Process single-consumer work items through in-process or durable queue brokers with retained-message inspection, retry/archive controls, and queue/type pause-resume control.

[TOC]

## Overview

Queueing provides an application-level abstraction for background work that must be processed by exactly one logical handler per queued message type. It complements Messaging rather than replacing it:

- Messaging is for pub/sub fan-out where multiple handlers may react to the same event.
- Queueing is for work dispatch where one queued item must be processed once by one handler when a compatible handler is available.

The current queueing implementation ships with several brokers:

- `InProcessQueueBroker` for local, process-bound work distribution and tests.
- `EntityFrameworkQueueBroker<TContext>` for durable SQL-backed processing with renewable leases and runtime-safe competing consumers.
- `RabbitMQQueueBroker` for broker-backed durable queue processing with manual acknowledgement, retry, and dead-letter semantics.
- `ServiceBusQueueBroker` for Azure Service Bus queue transport with manual complete/abandon/dead-letter semantics.

The feature also includes an operational web endpoint surface for queue broker summary, subscription inspection, waiting-message inspection, and queue/type pause-resume control.

## Challenges

- Single-consumer semantics: one handler per queued message type while still allowing multiple host instances to compete for work.
- Delayed handler availability: work can be enqueued before a handler is registered and should wait instead of failing.
- Durability: persisted messages need retries, leases, expiration, and dead-lettering behavior without depending on the messaging feature.
- Operational control: support engineers need visibility into waiting work, active subscriptions, and queue pause state.

## Solution

- Contracts: `IQueueMessage`, `IQueueMessageHandler<TMessage>`, `IQueueBroker`, and `IQueueBrokerService` provide a queue-specific API.
- Runtime: `QueueingService` is the single hosted service for the feature and applies subscriptions after host startup.
- Providers: brokers implement `QueueBrokerBase`; provider-specific background work plugs into the single runtime through `IQueueBrokerBackgroundProcessor`.
- Operations: `IQueueBrokerService` and `Presentation.Web.Queueing` expose broker inspection and operational controls, including retained-message queries, retry/archive actions, waiting-message inspection, purge, and queue/type pause-resume management.

## Architecture

```mermaid
sequenceDiagram
 actor Producer
 participant Broker as IQueueBroker
 participant Behaviors as Enqueue Behaviors
 participant Transport as Broker Provider
 participant Worker as QueueingService / Provider Worker
 participant Handler as IQueueMessageHandler<T>

 Producer->>Broker: Enqueue(message)
 Broker->>Behaviors: Execute enqueue pipeline
 Behaviors->>Transport: Persist or dispatch work item
 Worker->>Transport: Claim item (if durable)
 Worker->>Handler: Handle(message)
 alt success
  Worker->>Transport: Mark succeeded
 else no handler yet
  Worker->>Transport: Leave waiting for handler
 else failure
  Worker->>Transport: Retry or dead-letter
 end
```

## Core Contracts

- `IQueueBroker` ([src/Application.Queueing/IQueueBroker.cs](src/Application.Queueing/IQueueBroker.cs))
  - Subscribe/unsubscribe queue handlers.
  - Enqueue messages and optionally wait for provider-specific persistence confirmation.
  - Process messages through the shared queue dispatch pipeline.
- `IQueueBrokerService` ([src/Application.Queueing/IQueueBrokerService.cs](src/Application.Queueing/IQueueBrokerService.cs))
  - Inspect queue summary, subscriptions, and waiting messages.
  - Pause or resume queues and specific message types.
- `QueueBrokerBase` ([src/Application.Queueing/QueueBrokerBase.cs](src/Application.Queueing/QueueBrokerBase.cs))
  - Validates messages, runs behaviors, resolves handlers, and enforces queue semantics.
- `QueueingService` ([src/Application.Queueing/QueueingService.cs](src/Application.Queueing/QueueingService.cs))
  - The single hosted service for the whole feature.

## Getting Started

### In-process broker

```csharp
builder.Services.AddQueueing(builder.Configuration, context =>
 context.WithSubscription<OrderQueuedMessage, OrderQueuedHandler>())
  .WithInProcessBroker(new InProcessQueueBrokerConfiguration
  {
   MaxDegreeOfParallelism = 1,
   EnsureOrdered = true
 })
 .AddEndpoints();
```

### Entity Framework broker

```csharp
builder.Services.AddDbContext<AppDbContext>(...);

builder.Services.AddQueueing(builder.Configuration, context =>
 context.WithSubscription<OrderQueuedMessage, OrderQueuedHandler>())
  .WithEntityFrameworkBroker<AppDbContext>(new EntityFrameworkQueueBrokerConfiguration
  {
   AutoSave = true,
   ProcessingInterval = TimeSpan.FromSeconds(15),
   LeaseDuration = TimeSpan.FromSeconds(30)
  })
  .AddEndpoints(options => options.RequireAuthorization());
```

### Azure Service Bus broker

```csharp
builder.Services.AddQueueing(builder.Configuration, context =>
 context.WithSubscription<OrderQueuedMessage, OrderQueuedHandler>())
  .WithServiceBusBroker(new ServiceBusQueueBrokerConfiguration
  {
   ConnectionString = configuration["Queueing:ServiceBus:ConnectionString"],
   QueueNamePrefix = "bit",
   AutoCreateQueue = true,
   MaxConcurrentCalls = 8,
   MaxDeliveryAttempts = 5
  })
  .AddEndpoints(options => options.RequireAuthorization());
```

Your `DbContext` must implement `IQueueingContext`:

```csharp
public class AppDbContext : DbContext, IQueueingContext
{
 public DbSet<QueueMessage> QueueMessages { get; set; }
}
```

### Define a queue message and handler

```csharp
public sealed class OrderQueuedMessage(Guid orderId) : QueueMessageBase
{
 public Guid OrderId { get; } = orderId;
}

public sealed class OrderQueuedHandler : IQueueMessageHandler<OrderQueuedMessage>
{
 public Task Handle(OrderQueuedMessage message, CancellationToken cancellationToken)
 {
  // process one logical work item
  return Task.CompletedTask;
 }
}
```

### Enqueue work

```csharp
public sealed class OrdersService(IQueueBroker queueBroker)
{
 public Task QueueOrderAsync(Guid orderId, CancellationToken cancellationToken)
 {
  return queueBroker.Enqueue(new OrderQueuedMessage(orderId), cancellationToken);
 }
}
```

## Operational Endpoints

The retained-message operational surface lives in [src/Presentation.Web.Queueing/QueueingEndpoints.cs](src/Presentation.Web.Queueing/QueueingEndpoints.cs).

When you reference `Presentation.Web.Queueing`, you can register it directly from the fluent queueing builder:

```csharp
builder.Services.AddQueueing(builder.Configuration)
  .WithSubscription<OrderQueuedMessage, OrderQueuedHandler>()
  .WithEntityFrameworkBroker<AppDbContext>()
  .AddEndpoints(options => options
    .GroupPath("/api/_system/queueing")
    .GroupTag("_System.Queueing")
    .RequireAuthorization());
```

If you prefer separate registration, the existing `builder.Services.AddQueueingEndpoints(options => options.RequireAuthorization())` helper is also available.

Routes:

- `GET /api/_system/queueing/stats`
- `GET /api/_system/queueing/subscriptions`
- `GET /api/_system/queueing/messages`
- `GET /api/_system/queueing/messages/{id}`
- `GET /api/_system/queueing/messages/{id}/content`
- `GET /api/_system/queueing/messages/stats`
- `GET /api/_system/queueing/messages/waiting?take=50`
- `POST /api/_system/queueing/messages/{id}/retry`
- `POST /api/_system/queueing/messages/{id}/lease/release`
- `POST /api/_system/queueing/messages/{id}/archive`
- `DELETE /api/_system/queueing/messages`
- `POST /api/_system/queueing/queues/{queueName}/pause`
- `POST /api/_system/queueing/queues/{queueName}/resume`
- `POST /api/_system/queueing/types/{type}/pause`
- `POST /api/_system/queueing/types/{type}/resume`
- `POST /api/_system/queueing/types/{type}/circuit/reset`

All brokers implement the same `IQueueBrokerService` operational contract. The in-process and RabbitMQ brokers expose it over runtime-tracked items, while the Entity Framework broker adds durable retained history plus archive-aware filtering and lease management.

### RabbitMQ semantics

The RabbitMQ queue broker maps queueing semantics to RabbitMQ work queues using the **default exchange** and **manual acknowledgement**.

**Queue topology:**

- One RabbitMQ queue is created per registered queue message type.
- The queue name defaults to the message type name (e.g., `OrderQueuedMessage`), with optional `QueueNamePrefix` and `QueueNameSuffix`.
- The broker uses the **default exchange** (`""`) and publishes with the queue name as the **routing key**. RabbitMQ routes messages directly to the queue with the matching name.

```mermaid
flowchart LR
  P[Publisher] -->|routingKey=OrderQueuedMessage| E[Default Exchange]
  E --> Q1[Queue: OrderQueuedMessage]
  E --> Q2[Queue: InvoiceQueuedMessage]
  Q1 --> C1[Consumer A]
  Q1 --> C2[Consumer B <br/>(competing)]
  Q2 --> C3[Consumer C]
```

**Competing consumers:**

- Multiple application instances that use the same `QueueNamePrefix`/`QueueNameSuffix` and subscribe to the same message type consume from the **same queue**.
- RabbitMQ round-robins messages across all connected consumers on that queue.
- A message is delivered to **exactly one consumer** at a time.

**Acknowledgement and retry:**

- Consumption uses **manual ack** (`autoAck: false`).
- On success: the broker sends `BasicAck` and the message is removed from the queue.
- On failure: the broker implements retry by **republishing** the message with an incremented `x-attempt-count` header, then acks the original message. If republish fails, it falls back to `BasicNack(requeue: true)`.
- After `MaxDeliveryAttempts` is exceeded, the broker sends `BasicNack(requeue: false)` and the message is dropped (dead-lettered). No separate dead-letter exchange is configured by default.
- If a handler throws an unexpected exception, the broker nacks with requeue so the message is retried.

**Waiting for handler:**

- The broker declares the queue on both publish and subscribe. This means messages can be enqueued before any handler subscribes.
- When no consumer is connected, messages accumulate in the RabbitMQ queue.
- When a consumer connects, RabbitMQ delivers the backlog.

**Pause/resume:**

- When a queue or message type is paused, the broker detects this in `OnMessageAsync` and sends `BasicNack(requeue: true)`.
- The message remains in the RabbitMQ queue and will be redelivered when the consumer resumes processing.

**Expiration:**

- The broker checks `message.Timestamp + MessageExpiration` against UTC now before invoking the handler.
- If expired, the message is nacked without requeue and tracked as `Expired`.
- The AMQP `Expiration` property is also set on publish, so RabbitMQ can drop expired messages that have not yet been delivered.

**EnqueueAndWait:**

- `EnqueueAndWait` enables publisher confirms (`ConfirmSelect`) and calls `WaitForConfirmsOrDie` with a 30-second timeout. This guarantees the message has been persisted by RabbitMQ before the call returns.

**Operational visibility:**

- The RabbitMQ broker service (`RabbitMQQueueBrokerService`) tracks recent messages in memory (bounded to 10,000 items with LRU eviction).
- `GetMessagesAsync`, `GetSummaryAsync`, `GetMessageStatsAsync`, pause/resume, and purge are supported through this in-memory tracker.
- Unlike the Entity Framework broker, there is no durable retained history. Restarting the application clears the operational tracker (the messages themselves remain in RabbitMQ).
All brokers implement the same `IQueueBrokerService` operational contract. The in-process broker exposes it over runtime-tracked items, the Entity Framework broker adds durable retained history plus archive-aware filtering and lease management, and the Service Bus broker provides lightweight in-memory operational tracking.

For Entity Framework, the most relevant broker-specific retention options are:

- `AutoArchiveAfter` to archive terminal messages automatically after a retention period.
- `AutoArchiveStatuses` to limit auto-archival to specific terminal states such as `Succeeded`, `DeadLettered`, or `Expired`.

## Runtime Behavior

- Duplicate handlers fail fast. A second handler for the same queue message type is rejected.
- Missing handlers produce `WaitingForHandler` instead of immediate failure.
- Durable providers use at-least-once delivery semantics; handlers should remain idempotent.
- `AddQueueing(...)` may be called from multiple modules. Registrations accumulate, but queueing still uses one hosted service.

### Multi-host Deployment Notes

- `EntityFrameworkQueueBroker<TContext>` is intended to support **multiple host instances competing for work** against the same durable store.
- For real multi-host deployments, prefer **SQL Server** or **PostgreSQL** so lease claim and renewal can use efficient conditional updates in the database.
- Queueing still provides **at-least-once** delivery semantics. The goal is one logical owner at a time, not an exactly-once execution guarantee.
- A queued item can be reprocessed if a host crashes after side effects but before finalize, or if lease ownership changes after expiry.
- Queue handlers should therefore be **idempotent** and safe to execute more than once for the same `MessageId`.
- Set `LeaseDuration` longer than normal handler execution time and `LeaseRenewalInterval` low enough that healthy workers renew ownership before expiry.
- `SQLite` is suitable for local/dev and lightweight durable scenarios, but it is **not the recommended storage engine for distributed multi-host queue processing**.
- Workers verify `LockedBy` before finalizing state. If another node took ownership, the older worker skips finalization rather than overwriting the newer lease owner.

## Relation To Messaging

Use Messaging when one event should fan out to many handlers. Use Queueing when one work item should be owned by one handler execution. The APIs are intentionally similar so the developer experience stays familiar, but the runtime semantics are different.
