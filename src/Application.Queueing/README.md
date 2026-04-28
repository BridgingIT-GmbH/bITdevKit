![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================

# Application.Queueing

Provides a queue-specific abstraction for background work that must be processed by exactly one logical handler per queued message type. Queueing complements the messaging feature: messaging is for pub/sub fan-out, while queueing is for single-consumer work dispatch.

## Key Concepts

- **IQueueMessage** – the work item envelope with `MessageId`, `Timestamp`, `Properties`, and validation.
- **IQueueMessageHandler&lt;TMessage&gt;** – the single handler contract for a given queue message type.
- **IQueueBroker** – central abstraction for subscribing, enqueueing, and processing queue messages.
- **QueueBrokerBase** – base class for all queue brokers with behavior pipelines and subscription management.
- **IQueueBrokerService** – operational surface for inspecting broker state, pausing/resuming, retrying, and archiving.

## Providers

| Provider | Durability | Best For |
|---|---|---|
| `InProcessQueueBroker` | No | Local work, tests, simple apps |
| `EntityFrameworkQueueBroker<TContext>` | Yes | Durable SQL-backed queues with full operational history |
| `RabbitMQQueueBroker` | Yes | Broker-backed queues with competing consumers |

## Quick Start

```csharp
// Define a message and handler
public sealed class GenerateInvoiceQueueMessage(Guid invoiceId) : QueueMessageBase
{
    public Guid InvoiceId { get; } = invoiceId;
}

public sealed class GenerateInvoiceQueueHandler : IQueueMessageHandler<GenerateInvoiceQueueMessage>
{
    public Task Handle(GenerateInvoiceQueueMessage message, CancellationToken cancellationToken)
    {
        // process the invoice
        return Task.CompletedTask;
    }
}

// Register in Program.cs
builder.Services.AddQueueing(builder.Configuration)
    .WithSubscription<GenerateInvoiceQueueMessage, GenerateInvoiceQueueHandler>()
    .WithRabbitMQBroker(o => o
        .ConnectionString(configuration["Queueing:RabbitMQ:ConnectionString"])
        .QueueNamePrefix("bit")
        .IsDurable(true)
        .PrefetchCount(20));
```

## Documentation

- [Queueing Feature Documentation](../../docs/features-queueing.md)
- [Design Specification](../../docs/specs/spec-application-queueing-feature.md)
