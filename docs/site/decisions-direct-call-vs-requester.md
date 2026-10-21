---
title: Direct Call or Requester
---

<img class="bdk-doc-icon" src="assets/features/requester.svg" width="32" height="32" alt="" align="right">

# Direct Call or Requester

A direct call invokes a service or domain operation explicitly. Requester dispatches a typed request to its registered handler and wraps that handler with configured behaviors. Both run in the current process.

## Choose a direct call when

- the caller already has the appropriate service abstraction
- the operation is a focused implementation step inside a use case
- shared request behaviors are unnecessary for that operation
- an explicit call keeps the flow clearer than another request type and handler

A direct service call can still return `Result<T>`, accept cancellation, and use its own decorators. Direct calls do not require a different error contract.

## Choose Requester when

- the operation is a command or query with one handler owning the use case
- several entry points should execute the same configured behaviors
- validation, authorization, transaction, retry, or observability policies belong around the request
- the caller should depend on a request contract rather than a particular service method

Use `IRequester.SendAsync(...)`. The handler returns `Result<TResponse>`, including `Result<Unit>` when the command has no response value.

## Comparison

| Concern | Direct call | Requester |
| --- | --- | --- |
| Caller contract | Service interface or method | Typed command or query |
| Dispatch | Caller invokes the selected operation | Requester resolves the registered handler |
| Shared policies | Service implementation or explicitly composed decorators | Registered request behavior chain |
| Response | Defined by the service contract | `Result<TResponse>` |
| Typical use | Mapping, calculation, or reusable service work | A use case invoked by endpoints, jobs, or orchestration |

## Example

An endpoint sends an application-defined `ConfirmOrderRequest`. Its handler uses a repository, invokes the order's domain method, and saves the change.

Inside that handler, call a pricing service or mapping helper directly. Giving each internal method its own request type adds structure without necessarily adding a useful policy boundary.

## Preserve the behavior contract

Calling a request handler's `HandleAsync(...)` method directly bypasses Requester's behavior chain. Use `SendAsync(...)` when the caller needs that chain.

Requester executes the behaviors you register. A request type alone does not enable validation, authorization, or a transaction.

Choose Queueing or Jobs when the requirement is durable background execution. Switching a direct call to Requester does not add that durability.

## Related features

- [Requester and Notifier](reference/features-requester-notifier.md)
- [Application Commands and Queries](reference/features-application-commands-queries.md)
- [Where to Put Business Logic](decisions-business-logic.md)
- [Queueing or Jobs](decisions-queueing-vs-jobs.md)
