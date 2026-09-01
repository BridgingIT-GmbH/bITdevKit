---
title: Messaging or Queueing
---

# Messaging or Queueing

`Messaging` and `Queueing` are related, but they solve different problems.

## Choose Messaging when

- one event should fan out to multiple handlers
- producers should not care which consumers react
- the model is event-driven and publish/subscribe oriented
- integration or side effects should be loosely coupled

For details, see [Messaging](reference/features-messaging.md).

## Choose Queueing when

- one work item should be handled by one logical consumer
- retries, waiting-for-handler behavior, or durable work dispatch matter
- background work needs operational visibility
- the model is work ownership, not event fan-out

For details, see [Queueing](reference/features-queueing.md).

## Comparison

| Concern | Messaging | Queueing |
| --- | --- | --- |
| Delivery style | Publish/subscribe | Single-consumer work dispatch |
| Typical fan-out | One-to-many | One-to-one |
| Primary use | Events and reactions | Background work items |
| Consumer model | Multiple handlers may react | One handler owns one message type |
| Operational focus | Event propagation | Work processing and queue control |

## Decision rule

Use `Messaging` when something happened.

Use `Queueing` when something needs to be processed.
