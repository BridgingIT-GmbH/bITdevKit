---
title: Messaging vs Queueing
---

# Messaging vs Queueing

`Messaging` and `Queueing` are related, but they solve different problems.

## Choose Messaging when

- one event should fan out to multiple handlers
- producers should not care which consumers react
- the model is event-driven and publish/subscribe oriented
- integration or side effects should be loosely coupled

See:
[Messaging](reference/features-messaging.md)

## Choose Queueing when

- one work item should be handled by one logical consumer
- retries, waiting-for-handler behavior, or durable work dispatch matter
- background work needs operational visibility
- the model is work ownership, not event fan-out

See:
[Queueing](reference/features-queueing.md)

## Quick comparison

| Concern | Messaging | Queueing |
|---|---|---|
| Delivery style | Publish/subscribe | Single-consumer work dispatch |
| Typical fan-out | One-to-many | One-to-one |
| Best for | Events and reactions | Background work items |
| Consumer model | Multiple handlers may react | One handler owns one message type |
| Operational focus | Event propagation | Work processing and queue control |

## Practical rule of thumb

Use `Messaging` when something happened.

Use `Queueing` when something needs to be processed.
