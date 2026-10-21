---
title: Requester or Notifier
---

<img class="bdk-doc-icon" src="assets/features/requester.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/notifier.svg" width="32" height="32" alt="" align="right">

# Requester or Notifier

Requester sends a command or query to one handler. Notifier publishes a notification to all matching handlers. Both execute through configurable in-process behavior pipelines.

## <img class="bdk-doc-icon" src="assets/features/requester.svg" width="24" height="24" alt=""> Choose Requester when

- one handler owns the operation
- the caller needs a value or an explicit success or failure result
- the message asks the application to perform a command or answer a query
- validation, authorization, transactions, or other behaviors belong around that use case

Use `IRequester.SendAsync(...)`. Request handlers return `Result<TResponse>`, including `Result<Unit>` for commands without a response value.

## <img class="bdk-doc-icon" src="assets/features/notifier.svg" width="24" height="24" alt=""> Choose Notifier when

- a notification announces a fact or change to interested handlers
- several handlers can react without the publisher choosing them
- reactions belong outside the code that caused the change
- the publication mode can express whether the caller waits for handler completion

Use `INotifier.PublishAsync(...)`. Awaited publication returns an aggregated `IResult`. A notification does not return one handler's business response value.

## Comparison

| Concern | Requester | Notifier |
| --- | --- | --- |
| Intent | Perform an operation or retrieve a result | Publish a notification |
| Dispatch | One selected handler | All matching handlers |
| Response | `Result<TResponse>` | Aggregated `IResult` for awaited publication |
| Typical use | Create a customer or query an order | Notify interested handlers after a change |
| Shared behavior | Validation, retries, timeouts, and custom pipeline behaviors | Validation, retries, timeouts, and custom pipeline behaviors |

## Publication and delivery

Notifier defaults to sequential execution and stops on the first handler failure. Concurrent execution runs handlers in parallel and aggregates their outcomes.

Fire-and-forget publication returns after scheduling work. Its success result does not report whether the handlers later completed successfully.

Notifier returns success when no handlers match. Use Requester for a required operation whose result the caller must check.

Requester and Notifier do not provide durable broker delivery by themselves. Use Messaging, Queueing, or a domain-event outbox when that delivery requirement matters.

## Example

Send an application-defined `ReserveStockRequest` through Requester because checkout needs a reservation result before it can continue.

Publish `StockReservedNotification` through Notifier when independent handlers need to update local projections or invalidate caches. If that fact belongs to an aggregate transition, consider a domain event and its configured publication behavior.

## Related features

- [Requester and Notifier](reference/features-requester-notifier.md)
- [When to Use Domain Events](decisions-domain-events.md)
- [Messaging or Queueing](decisions-messaging-vs-queueing.md)
