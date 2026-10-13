---
title: Change History or Event Sourcing
---

# Change History or Event Sourcing

Change History records changes beside ordinary entity persistence. Event Sourcing makes the aggregate event stream the source of truth and reconstructs state from that stream.

## Choose Change History when

- regular repository-backed entities remain the application model
- support needs to inspect old and new property values, actors, and grouped changes
- the application needs controlled restoration of selected values
- history can be added around configured persistence operations

ChangeHistory stores property-level rows in `__ChangeHistory`. Include, exclude, redaction, and size policies determine which values are retained.

## Choose Event Sourcing when

- business events define how an aggregate reaches its current state
- the aggregate must be reconstructed by replaying stored events
- stream versions and event-driven projections belong to the persistence design
- the team can maintain persisted event contracts as the application evolves

`EventSourcingAggregateRoot` applies aggregate events. `IEventStore<TAggregate>` appends and reloads the stream. Snapshots can reduce replay work while the event stream remains canonical.

## Comparison

| Concern | Change History | Event Sourcing |
| --- | --- | --- |
| Source of truth | Current persisted entity state | Immutable aggregate event stream |
| Stored history | Property values and change sets | Versioned business events |
| State reconstruction | Entity loads through its repository | Aggregate replays events, optionally after a snapshot |
| Restoration | Explicit policies restore eligible selected values | Further domain operations append new events |
| Adoption | Configure capture around existing persistence | Design aggregate persistence around events |

## Example

Use Change History to show who edited a customer's address and which values changed. A configured restore policy can apply an eligible earlier value through domain-safe rules.

Use Event Sourcing when an aggregate's state is defined by events such as `OrderPlaced`, `ItemAdded`, and `OrderConfirmed`. Those application-defined events determine how replay rebuilds the order.

Property differences do not necessarily capture business intent. An audit trail alone does not provide an event-sourced aggregate model.

## Costs to consider

History capture adds storage and work to repository writes. Redacted, hashed, truncated, and summary values may not be eligible for restoration.

Event Sourcing adds replay, projection, and persisted-contract responsibilities. Stable event names and compatible serialization matter because older events remain part of the aggregate's state history.

## Related features

- [Change History](reference/features-domain-change-history.md)
- [Event Sourcing](reference/features-event-sourcing.md)
- [Domain Repositories](reference/features-domain-repositories.md)
- [When to Use Domain Events](decisions-domain-events.md)
