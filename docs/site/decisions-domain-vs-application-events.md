---
title: Domain Events or Application Events
---

<img class="bdk-doc-icon" src="assets/features/domain-events.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/application-events.svg" width="32" height="32" alt="" align="right">

# Domain Events or Application Events

Choose based on who owns the fact. A domain event records a meaningful aggregate transition. An application event announces something the application workflow decided to publish.

## Choose domain events when

- an aggregate's own operation causes the business change
- the fact remains meaningful regardless of which endpoint or job invoked the operation
- reactions belong outside the aggregate
- configured repository publication should connect the change to persistence

The aggregate registers the event in `DomainEvents`. Repository behaviors publish it directly after persistence or store it in the domain-event outbox.

## Choose application events when

- a service or handler completes a use case and publishes a follow-up signal
- the event concerns coordination rather than an aggregate's state transition
- several in-process handlers may react
- the application controls when publication happens and handles its result

Application events use `INotifier`. They can be authored with `NotificationBase` and manual handlers, or with the `[Event]` source generator.

## Comparison

| Concern | Domain event | Application event |
| --- | --- | --- |
| Owner | Aggregate | Application service, handler, or endpoint |
| Meaning | A business-significant aggregate change | An application fact or follow-up signal |
| Registration | Aggregate's `DomainEvents` collection | Explicit `INotifier.PublishAsync(...)` call |
| Publication | Configured repository behavior after persistence | At the point the application chooses |
| Delivery | Direct publication or configured outbox | Notifier's in-process publication modes |

## Example

An aggregate registers a `CustomerCreatedDomainEvent` when creation succeeds. An application reaction can publish a `WelcomeEmailRequestedEvent` after it determines that a welcome message is appropriate.

These events describe different things and can coexist. Avoid publishing the same business fact from both the aggregate and the application service.

## Delivery and consistency

The event's category does not guarantee durable delivery. Application-event publication through Notifier is in-process. Domain-event outbox delivery requires the configured outbox and a transaction that includes aggregate and outbox persistence.

Use domain methods and explicit requests for rules that must pass before a state change. Event handlers react to a fact or signal; they do not replace the aggregate's invariant checks.

## Related features

- [Domain Events](reference/features-domain-events.md)
- [Application Events](reference/features-application-events.md)
- [When to Use Domain Events](decisions-domain-events.md)
- [Requester or Notifier](decisions-requester-vs-notifier.md)
