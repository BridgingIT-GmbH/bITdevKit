![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you are working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

This repository includes the complete source code for the bITDevKit, along with a variety of sample
applications located in the ./examples folder within the solution. These samples serve as practical
demonstrations of how to leverage the capabilities of the bITDevKit in real-world scenarios. All
components are available
as [nuget packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [CHANGELOG](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/CHANGELOG.md).

Join us in advancing the world of software development with the bITDevKit!

## Application.Queueing

Provides a queue-specific abstraction for single-consumer background work processing.

### Available Brokers

- **InProcessQueueBroker** - In-memory, process-bound queue for local work distribution and tests.
- **EntityFrameworkQueueBroker<TContext>** - Durable SQL-backed queue with lease-based competing consumers.
- **ServiceBusQueueBroker** - Azure Service Bus queue transport with manual complete / abandon / dead-letter semantics.

### Key Contracts

- `IQueueBroker` - Subscribe, enqueue, and process queue messages.
- `IQueueMessage` - Represents a unit of queued work.
- `IQueueMessageHandler<TMessage>` - Handles a specific queue message type.
- `IQueueBrokerService` - Operational inspection and control surface.

### Documentation

For detailed documentation, configuration examples, and usage guidance, see
[docs/features-queueing.md](../../docs/features-queueing.md).
