---
status: draft
---

# Design Specification: Requester/Notifier Integration Activities for Orchestrations (Application.Orchestrations)

> This design document defines the first built-in outbound integration activities for the orchestration feature. This phase extends the orchestration activity model with requester-backed and notifier-backed activities.

[TOC]

## Introduction

The orchestration feature is the devkit's stateful, durable, long-running process runtime.

This specification defines a set of built-in integration activities that let an orchestration step invoke another feature explicitly.

The integration model is:

- an orchestration activity invokes another feature through its public abstraction
- the operation is visible in the orchestration definition
- orchestration context is mapped explicitly into the target payload and metadata
- the orchestration continues after the target feature accepts the work, unless a different activity contract explicitly waits

---

## Relationship To Core Orchestration Spec

This specification builds directly on the orchestration feature defined in [spec-application-orchestration.md](/f:/projects/bit/bITdevKit/docs/specs/spec-application-orchestration.md:1).

The main orchestration spec already defines a built-in activity catalog and an activity-centered integration model.

### Existing built-in helpers in the current implementation

The current implementation already exposes built-in orchestration activity helpers such as:

- `LogActivity(...)`
- `TransformActivity(...)`
- `DecisionActivity(...)`
- `WaitActivity(...)`
- `StartChildOrchestrationActivity(...)`
- `Parallel(...)`
- `Loop(...)`
- `ApprovalActivity(...)`
- `HumanTaskActivity(...)`

These are documented in [features-orchestrations.md](/f:/projects/bit/bITdevKit/docs/features-orchestrations.md:359) and implemented in [OrchestrationAdvancedWorkflow.cs](/f:/projects/bit/bITdevKit/src/Application.Orchestrations/Workflow/OrchestrationAdvancedWorkflow.cs:905).

### Built-in activities already described by the core orchestration spec

The core orchestration spec already includes a built-in activity catalog that contains:

- `QueryActivity`
- `CommandActivity`
- `SignalActivity`
- `ExecuteJobActivity`
- `DispatchJobActivity`
- `ScheduleJobActivity`

See [spec-application-orchestration.md](/f:/projects/bit/bITdevKit/docs/specs/spec-application-orchestration.md:1930).

### Activities defined by this specification

This specification standardizes the first requester/notifier-based integration helpers:

- `QueryActivity`
- `CommandActivity`
- `SendRequestActivity`
- `PublishNotificationActivity`

These activities extend the same built-in activity model rather than introducing a separate orchestration interaction model.

Messaging, queueing, and jobs integration activities remain future work and are explicitly out of scope for this implementation phase.

---

## Goals

The orchestration integration activity layer is intended to satisfy the following goals.

### 1. Keep orchestration behavior explicit

If an orchestration sends a request or publishes a notification, that action should be visible directly in the orchestration definition.

### 2. Reuse the activity authoring model

Integrations should look and feel like other built-in orchestration activities rather than like hidden runtime adapters.

### 3. Map orchestration context into target payloads explicitly

Each activity should derive payload, metadata, correlation, and other target-specific values from `OrchestrationContext<TData>`.

### 4. Default to fire-and-continue

These integration activities should, by default, complete once the target feature accepts the operation. They should not wait for downstream business completion or downstream handler results.

### 5. Preserve feature boundaries

Requester and Notifier must continue to own their own runtime behavior, dispatch semantics, retries, and observability.

### 6. Preserve orchestration observability

The orchestration should be able to record that the outbound action was requested, accepted, failed, or retried as part of normal activity history.

---

## Non-goals

This specification intentionally does not define:

- inbound feature adapters that automatically start orchestrations by default
- direct provider-table access from orchestration to downstream feature persistence
- hidden fire-and-forget helper methods directly on `OrchestrationContext<TData>`
- synchronous "wait for downstream system to really finish" semantics for requests or notifications

The target is explicit activity-based integration through the orchestration runtime.

---

## Integration Activity Model

The integration activity model is:

- orchestrations call other features through first-class built-in activities
- those activities use public abstractions of the target feature
- orchestration context is mapped to payload and metadata through explicit lambdas
- the activity returns `Continue` after the target feature accepts the operation

The baseline behavior is:

- query, then continue
- command, then continue
- send request, then continue
- publish notification, then continue

When a workflow needs to wait for an external result, it should model that through normal orchestration primitives such as:

- signals
- waiting states
- timers
- child orchestrations
- explicit wait/signal composition where appropriate

---

## Integration Principles

All orchestration integration activities must preserve the core orchestration contracts:

- integrations are modeled as activities, not as hidden orchestration-context helper methods
- activity configuration is context-centered: payload, metadata, correlation, idempotency, and target names are derived from `OrchestrationContext<TData>`
- the activity itself is part of normal orchestration execution, history, retries, and compensation semantics
- activities call public feature abstractions rather than provider tables or transport-specific internals
- activities must tolerate the target feature not being registered in the current application composition
- recoverable failures must be representable through `Result`-based or explicit activity outcome handling
- integrations must be testable through the orchestration harness with fakes or substitutes for target feature abstractions
- correlation id, causation id, module/tenant metadata, and orchestration instance id should flow into target feature metadata where available
- orchestration state must remain owned by the orchestration runtime; target features must remain owners of their own dispatch/runtime state

Additional integration-activity-specific principles:

- the default integration mode is accept-and-continue, not wait-for-completion
- if a target feature later supports waiting patterns, those should be explicit distinct activities, not hidden behavior of the fire-and-continue variants
- target payload mapping should be strongly typed wherever possible
- activity-level retries should retry the request to the target feature, not infer downstream business completion
- when a required target abstraction is not registered, the activity must fail in a controlled way and surface an explicit error through normal orchestration activity failure handling

### Optional registration behavior

Integration activities depend on optional feature registrations such as:

- `INotifier`
- `IRequester`

These registrations must remain optional.

Therefore:

- the presence of an integration activity in an orchestration definition must not force the host application to register every possible target feature
- the activity should resolve the required target abstraction at execution time
- if the abstraction is not registered, the activity must not crash the runtime with an unhandled `InvalidOperationException` or DI resolution failure
- instead, it must fail the activity in a controlled, diagnosable way that is visible in orchestration history and can be handled through normal orchestration retry, transition, wait, compensation, cancel, or terminate behavior
- the error message should clearly identify which target feature or abstraction was missing

Example failure shape:

```text
Outbound integration failed: IRequester is not registered for QueryActivity<GetOrderSummaryQuery>.
```

The exact runtime representation may be:

- a failed `Result`
- a failed activity result mapped to orchestration failure handling
- a terminate/cancel/failure-oriented `OrchestrationOutcome`

The normative requirement is controlled, diagnosable activity failure rather than silent skip behavior or an infrastructure crash.

---

## Low-Coupling Abstraction Placement

The dependency shape should remain disciplined.

- `Application.Orchestrations` owns the orchestration DSL, runtime, activity authoring surface, and orchestration-specific execution/history behavior
- Requester and Notifier integrations may live directly in `Application.Orchestrations` because their abstractions already live in Common
- Messaging, Queueing, and Jobs integrations should remain optional future extensions when they would otherwise introduce package coupling
- provider packages continue to own durable providers for their own features
- presentation packages continue to own endpoints and operational surfaces

Current package shape for this phase:

- `Application.Orchestrations`
  - built-in activity contracts
  - `QueryActivity`
  - `CommandActivity`
  - `SendRequestActivity`
  - `PublishNotificationActivity`

Later optional packages may add messaging, queueing, and jobs activity families without changing the request/notifier activity model introduced here.

The low-level activity abstractions must not reference:

- Entity Framework
- ASP.NET Core
- hosted services
- transport-specific broker implementations
- provider tables

---

## Common Authoring Model

All integration activities should follow a common shape.

At authoring time, a developer should be able to configure:

- the target request or notification type
- payload mapping from orchestration context
- optional context-property mapping
- optional correlation id mapping
- optional request or publish options customization
- optional response mapping back into orchestration context for requester-based activities

Conceptually:

1. Build the target payload from `context.Data` and/or `context.Properties`.
2. Build optional request/publish context metadata from orchestration state.
3. Call the public feature abstraction.
4. Map the successful response back into orchestration context when the activity supports a response.
5. Return `Continue` after successful acceptance.

### Candidate base contracts

Example shape:

```csharp
public interface IOutboundOrchestrationActivityBuilder<TData, TPayload>
    where TData : class, IOrchestrationData
{
    IOutboundOrchestrationActivityBuilder<TData, TPayload> Payload(
        Func<OrchestrationContext<TData>, TPayload> payloadFactory);

    IOutboundOrchestrationActivityBuilder<TData, TPayload> CorrelationId(
        Func<OrchestrationContext<TData>, string> correlationIdFactory);

    IOutboundOrchestrationActivityBuilder<TData, TPayload> CausationId(
        Func<OrchestrationContext<TData>, string> causationIdFactory);

    IOutboundOrchestrationActivityBuilder<TData, TPayload> IdempotencyKey(
        Func<OrchestrationContext<TData>, string> idempotencyKeyFactory);

    IOutboundOrchestrationActivityBuilder<TData, TPayload> Metadata(
        Action<OrchestrationContext<TData>, IDictionary<string, object>> metadataFactory);
}
```

This is illustrative only. The normative requirement is a consistent, context-centered builder style across activities.

---

## Built-In Activity Catalog

This implementation phase includes:

- `QueryActivity`
- `CommandActivity`
- `SendRequestActivity`
- `PublishNotificationActivity`

`QueryActivity`, `CommandActivity`, and `SendRequestActivity` are all requester-backed activities. `PublishNotificationActivity` is notifier-backed.

Requester-based activities may map a successful response back into orchestration context. Notification publication completes once `INotifier` returns success.

The remaining sections below are future-looking design notes for additional outbound integrations. They are not part of the implemented surface in this phase.

---

## Publish Message Activity

`PublishMessageActivity` publishes a message through the public Messaging abstraction and continues once the broker accepts the publish operation.

It is intended for integration events or application-level pub/sub dispatch triggered from orchestration state.

### Requirements

- the activity must use the public Messaging abstraction, not broker provider tables
- the activity depends on Messaging registration and must remain optional
- if Messaging is not registered, the activity must fail with a clear error outcome/history entry rather than crashing orchestration execution
- orchestration context must be mappable to a typed `IMessage` payload
- orchestration metadata should be mappable into message properties where appropriate
- orchestration instance id, orchestration name, current state, and correlation id should be easy to propagate into message metadata
- the activity should complete when the message is accepted for publication, not when downstream message handlers finish
- activity retries should retry publication acceptance only

### Example shape

```csharp
state.PublishMessageActivity<InvoicePaidMessage>(activity => activity
    .Payload(context => new InvoicePaidMessage
    {
        InvoiceId = context.Data.InvoiceId,
        Amount = context.Data.Amount
    })
    .CorrelationId(context => context.CorrelationId)
    .Metadata((context, properties) =>
    {
        properties["OrchestrationInstanceId"] = context.InstanceId;
        properties["OrchestrationName"] = context.OrchestrationName;
        properties["CurrentState"] = context.CurrentState;
    }));
```

### Accepted result mapping

If the messaging abstraction exposes accepted publish metadata later, the activity may support:

- storing a broker publish id
- storing accepted UTC
- storing publication target name

But this is optional.

---

## Send Queue Message Activity

`SendQueueMessageActivity` enqueues a queue message through the public Queueing abstraction and continues once the queue broker accepts the message.

It is intended for explicit delegation of durable unit-of-work processing from an orchestration step.

### Requirements

- the activity must use the public Queueing abstraction, not queue provider tables
- the activity depends on Queueing registration and must remain optional
- if Queueing is not registered, the activity must fail with a clear error outcome/history entry rather than crashing orchestration execution
- orchestration context must be mappable to a typed `IQueueMessage` payload
- queue name or equivalent target naming should be configurable when the queueing feature supports it
- orchestration instance id, orchestration name, current state, and correlation id should be easy to propagate into queue message metadata
- the activity should complete when the queue accepts the message, not when the queue handler finishes processing
- activity retries should retry enqueue acceptance only

### Example shape

```csharp
state.SendQueueMessageActivity<GenerateInvoiceQueueMessage>(activity => activity
    .Payload(context => new GenerateInvoiceQueueMessage
    {
        InvoiceId = context.Data.InvoiceId
    })
    .CorrelationId(context => context.CorrelationId)
    .Metadata((context, properties) =>
    {
        properties["OrchestrationInstanceId"] = context.InstanceId;
        properties["CurrentState"] = context.CurrentState;
    }));
```

---

## Publish Notification Activity

`PublishNotificationActivity` publishes an in-process notification through `INotifier` and continues once notification publication has completed according to the notifier contract.

Because Notifier is in-process pub/sub, this activity is for orchestration-local fan-out or coordination within the application boundary.

### Requirements

- the activity must use `INotifier`
- the activity depends on Notifier registration and must remain optional
- if Notifier is not registered, the activity must fail with a clear error outcome/history entry rather than crashing orchestration execution
- orchestration context must be mappable to a typed `INotification` payload
- orchestration instance id, orchestration name, current state, and correlation id should be easy to propagate into notification context/metadata
- the activity completes after notifier publication returns
- the activity does not wait for later external business consequences beyond the notifier contract itself

### Example shape

```csharp
state.PublishNotificationActivity<CustomerReviewRequestedNotification>(activity => activity
    .Payload(context => new CustomerReviewRequestedNotification
    {
        CustomerId = context.Data.CustomerId,
        OrderId = context.Data.OrderId
    })
    .CorrelationId(context => context.CorrelationId));
```

---

## Send Request Activity

`SendRequestActivity` sends a request through `IRequester`.

Requester is request/response by nature, so this integration is the least naturally "fire-and-continue" of the set. The feature should still support an orchestration activity for sending a request, but the design must stay honest about Requester semantics.

### Requirements

- the activity must use `IRequester`
- the activity depends on Requester registration and must remain optional
- if Requester is not registered, the activity must fail with a clear error outcome/history entry rather than crashing orchestration execution
- orchestration context must be mappable to a typed `IRequest<T>` payload
- the default experience should favor fire-and-continue orchestration authoring where feasible
- if the current Requester abstraction requires a response, the activity may still ignore the returned value after successful send completion
- the activity must not pretend that the request was accepted asynchronously if the underlying Requester call is still request/response
- if a true no-wait requester-dispatch abstraction is introduced later, the activity may bind to that explicit abstraction instead

### Example shape

```csharp
state.SendRequestActivity<StartCustomerSyncCommand, Result>(activity => activity
    .Payload(context => new StartCustomerSyncCommand
    {
        CustomerId = context.Data.CustomerId
    })
    .CorrelationId(context => context.CorrelationId)
    .OnSucceeded(context =>
    {
        context.Data.LastSyncRequestUtc = DateTimeOffset.UtcNow;
    }));
```

### Design note

This activity is slightly different from the others because Requester is not a broker or queue. It is still useful as an orchestration-visible integration step, but its exact "no wait" semantics depend on future Requester abstraction choices.

---

## Trigger Job Activity

`TriggerJobActivity` requests background job execution and continues once the scheduler accepts the job occurrence.

This is the outbound job integration direction for orchestrations.

The main orchestration spec already foresees `ExecuteJobActivity`, `DispatchJobActivity`, and `ScheduleJobActivity`. This specification focuses on the fire-and-continue form.

### Requirements

- the activity must use public Jobs abstractions, not scheduler provider tables
- the activity depends on Jobs registration and must remain optional
- if Jobs is not registered, the activity must fail with a clear error outcome/history entry rather than crashing orchestration execution
- orchestration context must be mappable to typed job data and job metadata
- orchestration instance id, orchestration name, current state, and correlation id should be easy to propagate into job metadata/properties
- the activity should complete after the job occurrence is accepted
- the activity should not wait for job completion unless the developer explicitly chooses a wait-oriented job activity
- accepted job occurrence id should be storable back into orchestration context when configured

### Example shape

```csharp
state.TriggerJobActivity<SendReminderJob, SendReminderData>(activity => activity
    .Payload(context => new SendReminderData
    {
        OrderId = context.Data.OrderId,
        ApproverId = context.Data.ApproverId
    })
    .CorrelationId(context => context.CorrelationId)
    .IdempotencyKey(context => $"reminder:{context.InstanceId}:{context.Data.ApproverId}")
    .OnAccepted((context, result) =>
    {
        context.Data.ReminderOccurrenceId = result.OccurrenceId;
    }));
```

### Alignment with the core orchestration spec

If the Jobs feature keeps `DispatchJobActivity` as the canonical name, then `TriggerJobActivity` may be a documentation-level name for that same accept-and-continue behavior rather than a separate runtime concept.

---

## Shared Mapping Rules

All integration activities should follow the same mapping rules.

### Payload mapping

- payloads should be built from `context.Data` and, when useful, `context.Properties`
- strongly typed payload mapping is preferred
- serialization-backed fallback mapping is acceptable only as a secondary option

### Metadata mapping

The following values should be easy to project into target-feature metadata:

- `context.InstanceId`
- `context.OrchestrationName`
- `context.CurrentState`
- `context.CorrelationId`
- current activity name when relevant
- business identifiers from `context.Data`

### Idempotency

Where the target feature supports idempotency or stable message identity, the activity should allow an explicit idempotency key mapping.

### Accepted callbacks

Activities may optionally expose accepted callbacks so the orchestration can retain outbound references, for example:

- published message id
- queue message id
- job occurrence id
- request sent UTC

Example shape:

```csharp
state.PublishMessageActivity<OrderSubmittedMessage>(activity => activity
    .Payload(context => new OrderSubmittedMessage(context.Data.OrderId))
    .OnAccepted((context, result) =>
    {
        context.Properties["OutboundOrderSubmittedMessageId"] = result.MessageId;
    }));
```

---

## Runtime Behavior

Outbound integration activities must still behave like normal orchestration activities.

That means:

- they execute under normal orchestration activity execution rules
- they are visible in history
- they may use activity-level retry configuration
- they may participate in compensation where that is meaningful
- they must not bypass orchestration persistence or history recording

If an integration activity fails:

- the activity may return a failure-oriented outcome
- the orchestration may retry the activity
- the orchestration may transition, wait, compensate, cancel, or terminate according to normal orchestration rules

The target feature remains owner of downstream processing after acceptance.

---

## Testing Requirements

These activities must be testable through normal orchestration tests.

The target test experience should support:

- substituting fake `IMessageBroker`, `IQueueBroker`, `INotifier`, `IRequester`, and Jobs abstractions
- asserting payload mapping from orchestration context
- asserting metadata propagation
- asserting accepted callbacks write expected values back into orchestration context
- asserting orchestration retries or failure behavior when the target abstraction rejects the operation

At minimum, tests should cover:

- payload mapping correctness
- correlation and metadata propagation
- accept-and-continue behavior
- failure handling and retry behavior
- controlled activity failure when the target feature abstraction is not registered
- storage of accepted identifiers back into orchestration context when configured

---

## Open Design Decisions

The following decisions should be resolved during implementation planning:

- whether `TriggerJobActivity` becomes a distinct activity name or is just the fire-and-continue interpretation of `DispatchJobActivity`
- whether `SendRequestActivity` should stay request/response in its first implementation or wait for a true fire-and-continue requester abstraction
- whether Message and Queueing activities live in optional orchestration integration packages or in core with optional service activation
- whether accepted result models should be standardized across all integration activities

---

## Summary

This specification defines integration activities as part of the orchestration activity model.

The recommended model is:

- orchestrations explicitly invoke other features through activities
- orchestration context is explicitly mapped to target payloads and metadata
- the built-in activity returns after the target feature accepts the operation
- downstream completion is modeled separately through signals, waits, timers, or explicit wait-oriented activities

These activities extend the existing orchestration authoring and runtime model while keeping process definitions explicit, durable, and easy to reason about.
