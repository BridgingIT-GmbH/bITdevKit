# Messaging Feature Documentation

> Decouple producers and consumers with resilient asynchronous messaging and outbox-backed delivery.

[TOC]

## Overview

Messaging provides asynchronous publish/subscribe communication between parts of your application and across modules. It decouples producers from consumers, improves resilience, enables eventual consistency, and scales background work without blocking request flows.

Messaging payloads and outbox messages build on the shared serializer abstractions and JSON conventions documented in [Common Serialization](./common-serialization.md), while correlation and trace instrumentation are closely related to [Common Observability / Tracing](./common-observability-tracing.md).

The feature now supports an Entity Framework backed broker for durable, database-local message transport and an accompanying operational endpoint surface for inspecting, retrying, archiving, and purging persisted broker messages from your server application.

## Challenges

- Coupling: Direct calls create tight dependencies between components and modules.
- Reliability: Ensuring delivery with durable storage, retries, and redelivery semantics.
- Ordering: Understanding when processing order is guaranteed vs. best-effort.
- Expiration: Dropping stale messages safely via TTL/expiration policies.
- Observability: Correlation, tracing, metrics, and structured logging across hops.
- Transport differences: In-process, Entity Framework, RabbitMQ, and Azure Service Bus behave differently.

## Solution

- Abstractions: `IMessage`, `IMessageHandler<T>`, and `IMessageBroker` decouple publishers from subscribers.
- Behaviors: Publisher and handler behavior pipelines add cross-cutting concerns (module scoping, metrics, retry, timeout, chaos) consistently.
- Operations: `IMessageBrokerService` and the web messaging endpoints expose persisted broker state for support and diagnostics.
- Execution model: publish → transport → process → handle (sequence diagram below).

## Architecture

The broker is the central interface used by producers to publish messages and by infrastructure to dispatch them to subscribed handlers. The flow below shows the end-to-end path, including behavior pipelines and transport specifics.

```mermaid
sequenceDiagram
    actor Producer
    participant Broker as IMessageBroker
    participant PubBeh as Publisher Behaviors
    participant Transport as Broker (InProcess/EntityFramework/RabbitMQ/ServiceBus/AzureQueueStorage)
    participant Proc as Process(MessageRequest)
    participant HandBeh as Handler Behaviors
    participant Handler as IMessageHandler<T>

    Producer->>Broker: Publish(message, ct)
    Broker->>PubBeh: Execute pipeline
    PubBeh->>Transport: Send/Enqueue(message)
    Transport-->>Transport: Consume/Receive
    Transport->>Proc: Process(MessageRequest)
    Proc->>HandBeh: Execute pipeline
    HandBeh->>Handler: Handle(message, ct)
    alt Service Bus
      Handler-->>Transport: Complete on success / Abandon on failure
    else Entity Framework
      note right of Transport: Persisted broker rows, worker leases, retries, and operational endpoints
     else RabbitMQ
       note right of Transport: Fanout exchange; all queues receive all messages; auto-ack; no broker redelivery
     else Azure Queue Storage
       note right of Transport: One queue per message type; visibility timeout retry; delete on success
     else InProcess
       note right of Transport: Immediate in-memory completion
     end
```

## Core Contracts

- `IMessageBroker` ([src/Application.Messaging/IMessageBroker.cs](src/Application.Messaging/IMessageBroker.cs))
  - **Subscribe<TMessage,THandler>() / Subscribe(Type,Type):** Bind a message type to a handler type.
  - **Unsubscribe<TMessage,THandler>() / Unsubscribe(Type,Type) / Unsubscribe():** Remove bindings.
  - **Publish(IMessage, CancellationToken):** Validate and run publisher behaviors, then enqueue/send via the broker.
  - **Process(MessageRequest):** Resolve subscriptions, run handler behaviors, invoke each `IMessageHandler<T>`.
- `IMessageBrokerService` ([src/Application.Messaging/IMessageBrokerService.cs](src/Application.Messaging/IMessageBrokerService.cs))
  - Query persisted broker messages, payloads, handler states, and aggregate statistics.
  - Retry a full message or a single failed handler, release a lease, archive terminal rows, and purge old rows.
- `MessageRequest`: Envelope carrying the message and cancellation for processing (created by brokers when messages are consumed).
- Validation & serialization: Message validation (FluentValidation) and serialization are configured via the messaging builder.

## Getting Started

### DI setup

Minimal example that adds behaviors, the outbox, the Entity Framework broker, and the operational endpoints:

```csharp
// In Program.cs or your composition root
builder.Services.AddMessaging(builder.Configuration, o => o.StartupDelay("00:00:30"))
  // Register messages and handlers
  .WithSubscription<UserRegisteredMessage, UserRegisteredHandler>()
  // Publisher/handler behavior pipelines
  .WithBehavior<RetryMessageHandlerBehavior>()
  .WithBehavior<TimeoutMessageHandlerBehavior>()
  // Choose a broker
  .WithEntityFrameworkBroker<AppDbContext>()
  // Optional operational endpoints from Presentation.Web.Messaging
  .AddEndpoints(options => options.RequireAuthorization());
  //.WithInProcessBroker();
  //.WithRabbitMQBroker();
  //.WithServiceBusBroker();
  //.WithAzureQueueStorageBroker();
```

If you prefer separate registration, the existing `builder.Services.AddMessagingEndpoints(options => options.RequireAuthorization())` helper is also available.

### Define a message and handler

```csharp
public sealed record UserRegisteredMessage(Guid UserId, string Email) : IMessage;

public sealed class UserRegisteredHandler : IMessageHandler<UserRegisteredMessage>
{
  private readonly ILogger<UserRegisteredHandler> logger;
  public UserRegisteredHandler(ILogger<UserRegisteredHandler> logger) => this.logger = logger;

  public Task Handle(UserRegisteredMessage message, CancellationToken cancellationToken)
  {
    logger.LogInformation("Welcome email scheduled for {UserId} ({Email})", message.UserId, message.Email);
    return Task.CompletedTask;
  }
}
```

### Subscribe a handler

Subscribe during startup (e.g., in a hosted startup task or module initialization).

```csharp
public sealed class MessagingSubscriptionsStartupTask : IHostedService
{
  private readonly IMessageBroker broker;
  public MessagingSubscriptionsStartupTask(IMessageBroker broker) => this.broker = broker;

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await broker.Subscribe<UserRegisteredMessage, UserRegisteredHandler>();
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Subscribe during program  initialization (e.g. in a module's `IModule.Register`):

```csharp
services.AddMessaging(configuration)
  .WithSubscription<UserRegisteredMessage, UserRegisteredHandler>();
```

### Publish a message

```csharp
public sealed class RegistrationService
{
  private readonly IMessageBroker broker;
  public RegistrationService(IMessageBroker broker) => this.broker = broker;

  public async Task RegisterAsync(Guid userId, string email, CancellationToken ct)
  {
    // ... domain work ...
    await broker.Publish(new UserRegisteredMessage(userId, email), ct);
  }
}
```

## Outbox (Reliability)

Use the transactional outbox to achieve “at least once” delivery: domain changes and an outbox record are persisted in the same transaction, and a background worker publishes messages from the outbox until processed.

- Entity: [src/Domain.Outbox/Message/OutboxMessage.cs](src/Domain.Outbox/Message/OutboxMessage.cs)
  - Fields: `AggregateId`, `AggregateType`, `EventType`, `Aggregate`, `AggregateEvent`, `TimeStamp`, `IsProcessed`, `RetryAttempt`, `MessageId`.
- Registration: `.WithOutbox<TContext>(...)` wires the publisher behavior, hosted service, and worker. See [src/Infrastructure.EntityFramework/Messaging/Outbox/ServiceCollectionExtensions.cs](src/Infrastructure.EntityFramework/Messaging/Outbox/ServiceCollectionExtensions.cs).
  - Common options: `ProcessingInterval`, `StartupDelay` (advanced: `ProcessingModeImmediate`, use cautiously).

Outbox flow:

```mermaid
sequenceDiagram
    participant Domain as Domain/DbContext
    participant Tx as Transaction
    participant Table as OutboxMessage table
    participant Service as OutboxMessageService
    participant Worker as OutboxMessageWorker<TContext>
    participant Broker as IMessageBroker
    participant Transport as Broker
    participant Handler as Subscribed Handler(s)

    Domain->>Tx: Begin
    Domain->>Tx: Persist aggregate changes
    Domain->>Table: Insert OutboxMessage(IsProcessed=false)
    Tx-->>Domain: Commit
    Service-->>Worker: Tick (StartupDelay/Interval)
    Worker->>Table: Fetch unprocessed
    Worker->>Broker: Publish(deserialized message)
    Broker->>Transport: Send/Enqueue
    Transport-->>Handler: Deliver/Process
    alt success
      Worker->>Table: Mark IsProcessed=true
    else failure
      Worker->>Table: Increment RetryAttempt
    end
```

Best practices:

- Prefer scheduled processing; enable `ProcessingModeImmediate` only when validated in your environment.
- Make handlers idempotent; use `MessageId` to deduplicate when necessary.
- Monitor retries and consider DLQ/alerting for persistent failures.
- Set appropriate TTL/expiration and durability settings per transport.

## Broker Implementations

### InProcessMessageBroker

- Ordered, single-threaded handling using TPL Dataflow (`ActionBlock` with `EnsureOrdered=true`).
- Options: `ProcessDelay` (simulated work), `MessageExpiration` (drop before processing).
- See [src/Application.Messaging/Brokers/InProcessMessageBroker.cs](src/Application.Messaging/Brokers/InProcessMessageBroker.cs).

### EntityFrameworkMessageBroker

- Persists each published message into the current `DbContext` via `IMessagingContext.BrokerMessages` and processes it asynchronously through a background worker.
- Uses provider-neutral optimistic concurrency (`ConcurrencyVersion`) plus renewable leases (`LockedBy`, `LockedUntil`) to coordinate multi-node workers safely.
- Stores per-handler execution state inside the broker row, enabling aggregate status, handler-level retry, expiration, dead-lettering, and auto-archiving.
- Exposes the persisted work through `IMessageBrokerService` and the optional server endpoints from `Presentation.Web.Messaging`.
- Supports **runtime pause/resume per message type** via `MessageBrokerControlState`. When a type is paused, the worker skips messages of that type without claiming leases; resumed types pick up pending work on the next tick.
- See [src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBroker{TContext}.cs](src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBroker{TContext}.cs), [src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBrokerWorker{TContext}.cs](src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBrokerWorker{TContext}.cs), and [src/Presentation.Web.Messaging/MessagingEndpoints.cs](src/Presentation.Web.Messaging/MessagingEndpoints.cs).

Multi-host deployment notes:

- The Entity Framework broker is designed for **multi-host active processing** when backed by a relational provider such as **SQL Server** or **PostgreSQL**.
- Delivery semantics are **at-least-once**, not exactly-once. If a host crashes after side effects but before finalization, or if a lease expires and another worker reclaims the row, the same message may be processed again.
- Handlers should therefore be **idempotent** and use `MessageId` or domain-level deduplication where duplicate side effects matter.
- Tune `LeaseDuration` to exceed normal handler duration, and keep `LeaseRenewalInterval` comfortably below that lease.
- `SQLite` is supported for local/dev and lightweight scenarios, but it is **not the recommended backing store for real multi-host deployments**.
- Workers only finalize state when they still own the lease. If ownership changed, the worker skips finalization rather than overwriting newer state.

Entity Framework broker flow:

```mermaid
sequenceDiagram
    participant Producer
    participant Broker as EntityFrameworkMessageBroker
    participant Table as __Messaging_BrokerMessages
    participant Worker as EntityFrameworkMessageBrokerWorker
    participant Handler as IMessageHandler<T>
    participant Api as Messaging Endpoints

    Producer->>Broker: Publish(message)
    Broker->>Table: Insert BrokerMessage
    Worker->>Table: Claim pending row lease
    Worker->>Handler: Process subscribed handlers
    alt success
      Worker->>Table: Mark handler/message succeeded
    else retryable failure
      Worker->>Table: Leave message pending or failed handler state
    end
    Api->>Table: Query/retry/archive/purge persisted rows
```

### RabbitMQMessageBroker

The RabbitMQ messaging broker uses a **single fanout exchange** (default name: `messaging`). Every message published to this exchange is broadcast to **all bound queues**, regardless of message type. Each subscriber gets its own queue bound to the exchange. The broker then filters messages at the consumer by looking up the actual message type from the `Type` AMQP header, so only handlers for that type are invoked.

- **Exchange:** one fanout exchange per configured `ExchangeName`.
- **Queues:** one queue per subscriber (not per message type). The queue name defaults to a random value unless you set `QueueName`. Use `QueueNameSuffix` for test isolation.
- **Binding:** each queue is bound to the exchange using the message type name as the routing key. Because the exchange is fanout, the routing key does not restrict delivery; it is used only for binding consistency.
- **Acknowledgement:** auto-ack (`autoAck: true`). Messages are acknowledged by RabbitMQ as soon as they are delivered to the consumer. **Handler failures do not trigger broker redelivery.** Use handler-level retry behaviors (e.g., `RetryMessageHandlerBehavior`) and design handlers to be idempotent.
- **Durability:** `IsDurable` controls exchange durability and message persistence (`Persistent` flag). Queue flags `ExclusiveQueue` and `AutoDeleteQueue` default to `true`, which means queues are deleted when the consumer disconnects. For production multi-host scenarios, set `ExclusiveQueue = false` and `AutoDeleteQueue = false`, and provide a stable `QueueName` so that all instances of the same application share a queue.
- **Expiration:** per-message TTL via AMQP `Expiration` property.
- **Correlation:** `CorrelationId` populated from Activity baggage when present.
- **ProcessDelay:** artificial delay before invoking handlers (useful for testing or throttling).
- See [src/Infrastructure.RabbitMQ/Messaging/RabbitMQMessageBroker.cs](src/Infrastructure.RabbitMQ/Messaging/RabbitMQMessageBroker.cs).

RabbitMQ topology (fanout exchange with one queue per subscriber):

```mermaid
flowchart LR
  P[Publisher] -->|Publish any message| E[Fanout Exchange 'messaging']
  E -->|broadcast| Q1[Queue A <br/>(Module A)]
  E -->|broadcast| Q2[Queue B <br/>(Module B)]
  E -->|broadcast| Q3[Queue C <br/>(Module C)]
  Q1 --> C1[Consumer filters <br/>by message type]
  Q2 --> C2[Consumer filters <br/>by message type]
  Q3 --> C3[Consumer filters <br/>by message type]
```

**Important behaviors:**

1. **Every subscriber receives every message.** Because the exchange is fanout, `Queue A`, `Queue B`, and `Queue C` all receive a copy of every published message. The consumer for `Queue A` deserializes the message using the type declared in the `Type` AMQP header and then runs `Process`, which dispatches only to handlers registered for that message type. If `Queue A` has no handler for the message type, `Process` completes without invoking any handler.

2. **Competing consumers for the same application.** If you run three replicas of the same module and want them to compete (one message handled by exactly one replica), all replicas must use the **same queue name**. The default behavior (random queue name + exclusive + auto-delete) creates a unique queue per instance, which means every replica receives every message. To enable competing consumers, set a stable `QueueName` and disable exclusivity:
   ```csharp
   .WithRabbitMQBroker(new RabbitMQMessageBrokerConfiguration
   {
       ConnectionString = "...",
       ExchangeName = "messaging",
       QueueName = "my-module-queue",
       ExclusiveQueue = false,
       AutoDeleteQueue = false,
       IsDurable = true
   })
   ```

3. **Multi-type on the same exchange is safe.** You can subscribe `HandlerA` for `MessageA` and `HandlerB` for `MessageB` on the same broker instance. Both message types flow through the same exchange. Each consumer deserializes using the correct type from the header, so cross-type handling does not occur.

4. **No broker-level retry.** Because consumption uses auto-ack, a handler exception does not return the message to RabbitMQ. The message is considered delivered and done. Always use `RetryMessageHandlerBehavior` or make handlers idempotent if retry is required.

### ServiceBusMessageBroker

- Topic per message name with optional `TopicScope` suffix; subscription per consumer; topics/subscriptions created if missing.
- TTL: defaults to ~60 minutes unless overridden.
- On success: completes messages. On failure: abandons messages so they can be redelivered.
- Correlation: `CorrelationId` populated from Activity baggage when present.
- See [src/Infrastructure.Azure.ServiceBus/ServiceBusMessageBroker.cs](src/Infrastructure.Azure.ServiceBus/ServiceBusMessageBroker.cs).

Service Bus topology (topic/subscriptions):

```mermaid
flowchart LR
  T[Topic - message type + scope] --> S1[Subscription Consumer A]
  T --> S2[Subscription Consumer B]
  T --> Sn[Subscription Consumer N]
```

### AzureQueueStorageMessageBroker

Because Azure Queue Storage does not support native topics or subscriptions, this broker emulates pub/sub by creating **one queue per message type**. When a message is published, it is sent to the queue for that message type. The broker starts a single background poller per message type that receives messages using visibility timeout semantics. When a message is successfully received, `Process` dispatches it to **all subscribed handlers** for that message type, achieving fan-out behavior.

- **Queue per message type:** the queue name is derived from the message type name (e.g., `myeventmessage`). Use `QueueNamePrefix`/`QueueNameSuffix` to isolate environments or tests.
- **Polling consumer:** messages are received via `ReceiveMessagesAsync` with a configurable `VisibilityTimeout`. If processing fails, the message becomes visible again after the timeout expires and will be re-delivered.
- **Single poller per message type:** even when multiple handlers subscribe to the same message type, only one polling loop runs. All handlers are invoked in-process for each received message.
- **Delete on success:** messages are deleted from the queue after `Process` completes successfully.
- **TTL:** `MessageExpiration` controls the time-to-live for messages in the queue (default: 7 days).
- **Auto-create:** queues are created automatically at runtime when `AutoCreateQueue` is `true`.
- **Correlation:** `CorrelationId` populated from Activity baggage when present.
- **See:** [src/Infrastructure.Azure.Storage/Messaging/AzureQueueStorageMessageBroker.cs](src/Infrastructure.Azure.Storage/Messaging/AzureQueueStorageMessageBroker.cs).

Azure Queue Storage topology (one queue per message type, shared by all handlers):

```mermaid
flowchart LR
  P[Publisher] -->|Publish MyEvent| Q[Queue: myeventmessage]
  Q -->|Poll + Process| H1[Handler A]
  Q -->|Poll + Process| H2[Handler B]
```

**Important behaviors:**

1. **All handlers for a message type are invoked for every message.** Because there is only one queue per message type, each received message is dispatched to every subscribed handler. There is no competing-consumer behavior *between* handlers for the same message type.

2. **Competing consumers across instances.** If you run multiple instances of the same application with the same `QueueNamePrefix`/`QueueNameSuffix`, they will compete for messages from the same queue. Each message will be handled by exactly one instance, but all handlers registered on that instance will still be invoked.

3. **Visibility timeout provides implicit retry.** If a handler throws or the process crashes before the message is deleted, the message will become visible again after `VisibilityTimeout` and will be reprocessed. There is no explicit dead-letter queue; messages that repeatedly fail will continue to retry until their TTL expires.

4. **No ordering guarantees.** Azure Queue Storage does not guarantee FIFO ordering, especially when multiple consumers are polling the same queue.

## Configuration & Options

- InProcess: `ProcessDelay`, `MessageExpiration`.
- Entity Framework: `StartupDelay`, `ProcessingInterval`, `ProcessingDelay`, `ProcessingCount`, `LeaseDuration`, `LeaseRenewalInterval`, `MaxDeliveryAttempts`, `MessageExpiration`, `AutoArchiveAfter`, `AutoArchiveStatuses`.
- RabbitMQ: `HostName`/`ConnectionString`, `ExchangeName`, `QueueName`/`QueueNameSuffix`, `IsDurable`, `ExclusiveQueue`, `AutoDeleteQueue`, `MessageExpiration`, `ProcessDelay`, `Retries`.
- Service Bus: `ConnectionString`, `TopicScope`, `MessageExpiration` (TTL).
- Azure Queue Storage: `ConnectionString`, `QueueNamePrefix`/`QueueNameSuffix`, `AutoCreateQueue`, `MaxConcurrentCalls`, `VisibilityTimeout`, `PollingInterval`, `MessageExpiration`, `ProcessDelay`.
- Naming/routing: message type name is used for routing; `TopicScope` adds a suffix to Service Bus topics; `QueueNamePrefix`/`QueueNameSuffix` isolate Azure Queue Storage queues.

Entity Framework broker configuration example:

```json
{
  "Messaging": {
    "StartupDelay": "00:00:10",
    "EntityFramework": {
      "Enabled": true,
      "StartupDelay": "00:00:05",
      "ProcessingInterval": "00:00:05",
      "ProcessingDelay": "00:00:00.100",
      "ProcessingCount": 10,
      "LeaseDuration": "00:00:30",
      "LeaseRenewalInterval": "00:00:10",
      "MaxDeliveryAttempts": 3,
      "MessageExpiration": "7.00:00:00",
      "AutoArchiveAfter": "14.00:00:00",
      "AutoArchiveStatuses": [ "Succeeded", "Expired", "DeadLettered" ]
    }
  }
}
```

RabbitMQ broker configuration example:

```json
{
  "Messaging": {
    "RabbitMQ": {
      "ConnectionString": "amqp://guest:guest@localhost:5672/",
      "ExchangeName": "messaging",
      "QueueName": "my-module-queue",
      "IsDurable": true,
      "ExclusiveQueue": false,
      "AutoDeleteQueue": false,
      "MessageExpiration": "1.00:00:00",
      "ProcessDelay": 0
    }
  }
}
```

Azure Queue Storage broker configuration example:

```json
{
  "Messaging": {
    "AzureQueueStorage": {
      "ConnectionString": "UseDevelopmentStorage=true",
      "QueueNamePrefix": "bit",
      "QueueNameSuffix": "prod",
      "AutoCreateQueue": true,
      "MaxConcurrentCalls": 1,
      "VisibilityTimeout": "00:00:30",
      "PollingInterval": "00:00:01",
      "MessageExpiration": "7.00:00:00",
      "ProcessDelay": 0
    }
  }
}
```

To use the Entity Framework broker, your `DbContext` must implement `IMessagingContext`:

```csharp
public class AppDbContext : DbContext, IMessagingContext
{
  public DbSet<BrokerMessage> BrokerMessages { get; set; }
}
```

## Operational Endpoints

When you add `Presentation.Web.Messaging`, the server can expose an operational API for persisted broker messages.

- Base route: `/api/_system/messaging/messages`
- `GET /stats`: aggregate statistics for the persisted working set.
- `GET /summary`: broker runtime summary including capabilities and paused types.
- `GET /subscriptions`: active message type to handler registrations.
- `GET /waiting`: messages published with no handler registrations.
- `GET /`: filterable list of broker messages.
- `GET /{id}`: message details with optional handler states.
- `GET /{id}/content`: stored payload content.
- `POST /{id}/retry`: retry all retryable handler work for a message.
- `POST /{id}/handlers/retry`: retry one failed/expired/dead-lettered handler entry.
- `POST /{id}/lease/release`: release the current worker lease.
- `POST /{id}/archive`: archive a terminal broker row.
- `POST /types/{type}/pause`: pause processing for a message type.
- `POST /types/{type}/resume`: resume processing for a message type.
- `DELETE /`: purge rows by age and optional status filters.

These endpoints are intended for support and operations workflows. In production, prefer enabling them behind authorization and limiting access to privileged roles or policies.

## Reliability & Observability

- Idempotency: design handlers to be safe on re-execution; deduplicate via `MessageId` if required.
- Entity Framework durability: messages survive process restarts in the application database and can be retried or archived without broker-specific infrastructure.
- Durability: enable persistent messages and durable queues (RabbitMQ) and rely on persisted topics/subscriptions (Service Bus).
- Ordering: guaranteed with InProcess; not guaranteed across distributed consumers for RabbitMQ/Service Bus.
- Expiration/TTL: prevent processing stale data; in-process broker drops expired messages before processing, while the Entity Framework broker expires rows based on `MessageExpiration`.
- Retries/redelivery: prefer handler retry behaviors; the Entity Framework broker also supports operational retries through stored handler state; Service Bus will redeliver after abandon; RabbitMQ auto-ack means no redelivery on failures.
- Correlation/tracing: propagate correlation via Activity baggage; instrument via OpenTelemetry.
- Multi-host EF guidance: prefer SQL Server/PostgreSQL for active-active worker deployments; treat SQLite as a local/lightweight option rather than a distributed broker store.
- **Runtime pause/resume:** use the operational endpoints to pause processing for specific message types during maintenance or incidents. Paused messages remain in `Pending` state and are automatically eligible for processing once resumed.

## Testing

- InProcess broker for unit/integration tests: deterministic ordering and simple setup.
- Entity Framework broker tests: validate claim/finalize, lease renewal, retry state transitions, and endpoint operations with focused broker and store-service tests, including SQLite, SQL Server, and PostgreSQL integration coverage for the durable worker paths.
- RabbitMQ broker tests: validate publish/subscribe, multi-type filtering, exchange isolation, and handler invocation. Run against a local RabbitMQ container.
  - [tests/Infrastructure.IntegrationTests/RabbitMQ/Messaging/RabbitMQMessageBrokerTests.cs](tests/Infrastructure.IntegrationTests/RabbitMQ/Messaging/RabbitMQMessageBrokerTests.cs)
- Azure Queue Storage broker tests: validate publish/subscribe, multi-handler fan-out, message type isolation, no-subscriber behavior, and batch handling. Run against Azurite (local Azure Storage emulator).
  - [tests/Infrastructure.IntegrationTests/Azure.Storage/Messaging/AzureQueueStorageMessageBrokerTests.cs](tests/Infrastructure.IntegrationTests/Azure.Storage/Messaging/AzureQueueStorageMessageBrokerTests.cs)
- Transport-backed integration tests: run RabbitMQ/Service Bus/Azure Queue Storage locally (containers/emulators), ensure subscriptions exist before publishing, and assert side-effects and idempotency.

## Minimal Examples

- Switch brokers via DI (single lines): `.WithInProcessBroker()`, `.WithEntityFrameworkBroker<AppDbContext>()`, `.WithRabbitMQBroker()`, `.WithServiceBusBroker()`, `.WithAzureQueueStorageBroker()`.
- Subscribe in startup and publish from application services (see snippets above).

## Appendix A — Behaviors

Behaviors wrap the publish and handle pipelines to add cross-cutting concerns consistently. You can compose multiple behaviors; registration order defines execution order (outermost first).

- Publisher behaviors: implement `IMessagePublisherBehavior` and wrap `Publish(...)`.
- Handler behaviors: implement `IMessageHandlerBehavior` and wrap `Handle(...)`.

Common built-ins include module scoping, metrics, retry, timeout, and (optionally) chaos. Add them via the messaging builder’s `.WithBehavior<TBehavior>()` method.

### Creating a custom publisher behavior

```csharp
using BridgingIT.DevKit.Application.Messaging;

public sealed class CustomAuditMessagePublisherBehavior : MessagePublisherBehaviorBase
{
  public CustomAuditMessagePublisherBehavior(ILoggerFactory loggerFactory) : base(loggerFactory) { }

  public override async Task Publish<TMessage>(
    TMessage message,
    CancellationToken cancellationToken,
    MessagePublisherDelegate next)
    where TMessage : IMessage
  {
    Logger.LogInformation("Publishing {MessageType} with id {MessageId}", typeof(TMessage).Name, message.MessageId);
    await next();
  }
}
```

Registration:

```csharp
builder.Services.AddMessaging(builder.Configuration)
  .WithBehavior<CustomAuditMessagePublisherBehavior>();
```

### Creating a custom handler behavior

```csharp
using BridgingIT.DevKit.Application.Messaging;

public sealed class CustomLatencyMessageHandlerBehavior : MessageHandlerBehaviorBase
{
  public CustomLatencyMessageHandlerBehavior(ILoggerFactory loggerFactory) : base(loggerFactory) { }

  public override async Task Handle<TMessage>(
    TMessage message,
    CancellationToken cancellationToken,
    object handler,
    MessageHandlerDelegate next)
    where TMessage : IMessage
  {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
      await next();
    }
    finally
    {
      sw.Stop();
      Logger.LogInformation("Handled {MessageType} in {Elapsed} ms", typeof(TMessage).Name, sw.ElapsedMilliseconds);
    }
  }
}
```

Registration:

```csharp
builder.Services.AddMessaging(builder.Configuration)
  .WithBehavior<CustomLatencyMessageHandlerBehavior>();
```

Notes:

- Keep behaviors side-effect free and fast; avoid blocking operations.
- Prefer metrics/logging/tracing here instead of duplicating logic in every handler.
- Ordering matters: register from outermost to innermost wrapper.

### Built-in behavior matrix (brief)

| Behavior | Pipeline | Purpose | Recommended use |
|---|---|---|---|
| ModuleScopeMessagePublisherBehavior | Publisher | Propagate module context across publish pipeline | Always for multi-module apps |
| ModuleScopeMessageHandlerBehavior | Handler | Propagate module context into handlers | Always for multi-module apps |
| MetricsMessagePublisherBehavior | Publisher | Emit publish counters/timers | Recommended in all environments |
| MetricsMessageHandlerBehavior | Handler | Emit handler counters/timers | Recommended in all environments |
| RetryMessageHandlerBehavior | Handler | Retry transient failures in handlers | Use when handlers call unreliable external systems; ensure idempotency |
| TimeoutMessageHandlerBehavior | Handler | Enforce a time budget for handling | Use to prevent runaway handlers; set sensible defaults |
| ChaosExceptionMessageHandlerBehavior | Handler | Fault injection for resilience testing | Use only in test/staging to validate recovery |
| OutboxMessagePublisherBehavior (EF Outbox) | Publisher | Persist messages to transactional outbox | Use when reliability is required; pair with Outbox worker |
