---
title: When to Use Domain Events
---

<img class="bdk-doc-icon" src="assets/features/domain-events.svg" width="32" height="32" alt="" align="right">

# When to Use Domain Events

A domain event records a business fact that already happened inside an aggregate. Use events when other parts of the application need to react after the aggregate is persisted.

## Use domain events when

- a state change has business meaning beyond the current method
- several independent handlers may react to the same fact
- the aggregate must remain unaware of notifications, projections, or integrations
- reactions need a separate delivery and retry policy

An aggregate registers events in its `DomainEvents` collection. Configured repository behaviors publish or store them after persistence.

## Use a direct call or request when

- the caller needs an immediate answer from one operation
- a rule must pass before the aggregate can change state
- the work is one step in the current use case
- adding an event would hide a required dependency between operations

For example, check whether an order can be canceled in the aggregate method. Raise an event after cancellation succeeds so separate handlers can update projections or notify the customer.

## Choose the publication mode

| Concern | Direct publication | Outbox publication |
| --- | --- | --- |
| Delivery | In-process handlers after the repository save | Persisted event rows handled by a background worker |
| Caller | Waits for the local publication flow | Does not wait for eventual handler completion |
| Recovery | No durable retry guarantee from direct publication | Retained rows support retries and recovery |
| Typical use | Lightweight local reactions | Side effects or integrations that need durable delivery |

Use `RepositoryDomainEventPublisherBehavior<TEntity>` for direct publication. Use `RepositoryOutboxDomainEventBehavior<TEntity, TContext>` for the outbox.

The outbox behavior saves the aggregate and adds event rows in separate operations. A surrounding database transaction must include both for an atomic save. The behavior alone does not guarantee that atomicity.

Handlers must tolerate delivery again after a retry or recovery. Make durable payloads serializable, with stable identifiers and the data each consumer needs.

## Example

After an order is confirmed, register an application-defined `OrderConfirmedDomainEvent`. One handler updates a read model and another arranges a customer notification.

If the notification must survive a process restart, use the outbox with the required transaction boundary. A direct handler failure can reach the caller after the aggregate save, so failure does not by itself mean that persistence was undone.

Domain-event publication does not make an aggregate event-sourced. Event Sourcing has its own event stream and persistence model.

## Related features

- [Domain Events](reference/features-domain-events.md)
- [Requester and Notifier](reference/features-requester-notifier.md)
- [Messaging or Queueing](decisions-messaging-vs-queueing.md) for delivery beyond the process
- [Event Sourcing](reference/features-event-sourcing.md)
