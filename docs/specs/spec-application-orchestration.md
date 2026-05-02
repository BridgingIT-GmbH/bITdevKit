---
status: draft
---

# Design Specification: Stateful Orchestration Feature (Application.Orchestration)

> This design document outlines the architecture and behavior of the new orchestration feature within the application. It defines the core concepts, execution model, control flow capabilities, triggers, reliability mechanisms, observability features, identity management, versioning considerations, testing strategies, and typical use cases for orchestrations.

## Introduction

The orchestration feature provides a code-first framework for defining, executing, and managing long-running processes within the application.

Orchestrations are persistent, stateful, and designed to support complex coordination scenarios, including human interaction, event-driven transitions, and fault-tolerant execution.

The orchestration model is state-machine-oriented. States represent stable phases of a long-running process, while activities perform work within a state. Transitions move an orchestration instance between states based on outcomes, conditions, signals, or time-based triggers.

The feature intentionally combines state-machine semantics with selected workflow capabilities such as activity execution, waiting, retries, human interaction, and compensation.

Disclaimer: this feature is not a full blown workflow engine or a business process modeling tool. It is a code-centric framework for structuring long-running processes in a maintainable and testable way. Please evaluate carefully if this feature is a good fit for your specific use case, as it can be opinionated in its execution model and may not be suitable for all scenarios. Alternatives: Elsa, Camunda, Dapr Workflows, MassTransit Courier, Temporal, Durable Functions, etc.

---

## Terminology

| Term                       | Description                                                                                                            |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| **Orchestration**          | A definition that describes a long-running process composed of states, activities, and transitions.                    |
| **Orchestration Instance** | A runtime execution of an orchestration definition.                                                                    |
| **State**                  | A logical phase within an orchestration where a set of activities is executed.                                         |
| **Activity**               | The smallest unit of execution within a state that produces an outcome controlling progression.                        |
| **Outcome**                | The result of an activity that determines execution behavior (e.g. Continue, Retry, Wait, Complete).                   |
| **Transition**             | A movement from one state to another based on conditions, signals (events) or outcomes.                                |
| **Orchestration Context**  | The shared execution object containing runtime metadata, optional orchestration data, and execution-scoped properties. |

---

## Core Characteristics

* **Persistence**

  * Orchestration instances and their state are persisted via a provider model.
  * State is serializable and can be queried and updated by the application.

* **Code-First Definition**

  * Orchestrations are defined in code using a fluent API or classes.
  * No visual designer is provided.
  * No JSON or YAML definition format is provided.

* **State Machine Model**

  * Orchestrations consist of states and transitions.
  * Transitions are triggered by:

    * External signals (events)
    * Programmatic interaction
    * Time-based triggers (timeouts or schedules)

* **Task-Based Execution**

  * Each orchestration instance progresses through a sequence of activities.
  * Activities operate on a shared orchestration context.

* **Long-Running Support**

  * Orchestrations can be paused, resumed, and persisted across application restarts.

* **Extensibility**

  * The framework is extensible, allowing custom activity types, triggers, and persistence providers.
  * A REST API is provided for orchestration management and monitoring based on a common data provider model.

---

## Execution Model

* **Activity-Oriented Execution**

  * An orchestration progresses by executing activities within its current state.

* **Outcome-Driven Progression**

  * Each activity produces an outcome that determines how execution proceeds.
  * If no explicit outcome is returned, the default outcome is **Continue**.

* **Dependency Injection**

  * Orchestration activities support dependency injection for integration with application services.

* **Concurrency**

  * Multiple orchestration instances can execute concurrently.
  * Optional configuration allows restricting execution to a single instance per orchestration definition.

* **Scalability**

  * Designed for concurrent and distributed execution. Leases and locking mechanisms ensure consistency in multi-instance scenarios.

---

## Control Flow Capabilities

Orchestrations support common control flow structures to enable complex process definitions:

* Conditional branching (`if / else`)
* Switch/case logic (decision points)
* Parallel branches
* Loops (including recurrence and `do-until`)
* Waiting and pausing (both orchestration-controlled and externally imposed)
* Event-driven transitions (reacting to signals/events to trigger state changes)
* Time-based transitions (scheduling future state changes or timeouts)
* Human interaction (activities that require manual completion, such as approvals)
* Error handling and retries (built-in support for retry policies and error handling strategies)
* Compensation (optional support for compensation actions in rollback scenarios, following the SAGA pattern)

Note: the control flow capabilities are designed to be composable and flexible, allowing developers to structure their orchestrations in a way that best fits their specific process requirements.

---

## Triggers

* **Event-Based Triggers**

  * Transitions can be driven by internal or external signals.
  * Signals can carry data and are correlated to specific orchestration instances.
  * Signal handlers can be defined to react to specific events and trigger state transitions.

* **Time-Based Triggers**

  * Scheduled or delayed transitions are supported.
  * Orchestrations can define timeouts or future execution points to trigger state changes.
  * Time-based triggers can be used for scenarios like waiting for a certain duration, scheduling future tasks, or implementing retry delays.

* **Human Interaction**

  * Manual activities (e.g., approvals) can pause execution until completed.
  * The orchestration can wait for external input or actions before proceeding.
  * Human interaction activities can be designed to integrate with user interfaces or notification systems to facilitate manual completion.
  * The framework can provide mechanisms for tracking and managing pending human interaction activities, allowing administrators to monitor and intervene if necessary.

---

## Reliability & Resilience

* **Failure Handling**

  * Built-in retry mechanisms for transient failures.
  * Configurable error handling strategies.

Exceptions thrown by activities result in a Failed state unless handled by configured retry or error policies.
The outcome model is used for controlled execution flow; failures are represented through exceptions and error handling strategies.

* **Compensation (Optional)**

  * Support for compensation actions (SAGA pattern) for rollback scenarios (TBD).

---

## Observability & Management

* **Auditing**

  * All state transitions and activity executions are logged.

* **Monitoring**

  * Orchestration instances can be inspected at runtime.

* **Administration API (endpoints)**

  * REST API for:

    * Querying orchestration instances
    * Inspecting state
    * Triggering/Signaling transitions
    * Cancelling orchestrations
    * Pausing and resuming orchestrations

* **Extensibility**

  * A dashboard UI is not provided but can be built on top of the API endpoints.
  * see [https://docs.diagrid.io/develop/diagrid-dashboard/](https://docs.diagrid.io/develop/diagrid-dashboard/)

---

## Identity & Correlation

* Each orchestration instance has a **correlation ID** for tracking across system boundaries start to end.
* Each orchestration execution should create tracing spans using an ActivitySource
* Tracing should be implemented using an ActivitySource, creating spans for:
  * orchestration start
  * each activity execution
* Correlation ID is included in all logs and telemetry for the orchestration instance.

---

## Versioning

* Versioning of orchestration definitions is currently not a required capability.
* The system is designed under the assumption that definitions evolve in a controlled manner without the need to maintain multiple active versions.
* If future requirements demand it, versioning can be introduced as an extension without impacting the core execution model.

---

## Testing

* Orchestrations are fully testable (xunit) with the help of test helper utilities to setup, run and assert orchestration instances in unit tests.
* Unit testing of:

  * Transitions
  * Activity logic
  * Control flow

---

## Execution Lifecycle

An orchestration instance progresses through a well-defined lifecycle from creation to completion. Execution is state-driven and activity-outcome-driven.

### Lifecycle Phases

* **Created**

  * A new orchestration instance is initialized.

* **Running**

  * The orchestration actively executes activities.

* **Waiting**

  * Execution is paused as part of normal orchestration behavior.
  * process is intentionally waiting for orchestration input/time/event.

* **Paused**

  * Execution is suspended by an external action.
  * operator/admin temporarily suspends execution regardless of orchestration logic.

While in Paused state:

* No activities are executed
* No time-based triggers are processed
* Signals are accepted but do not advance execution until resumed

Resuming transitions the orchestration back to its previous logical state (typically Running or Waiting).

* **Resuming**

  * Execution continues from a previously waiting or paused state.

* **Completed**

  * The orchestration has ended successfully.
  * This occurs either implicitly when execution reaches the natural end, or explicitly via a `Complete` outcome.

* **Failed**

  * The orchestration encountered a non-recoverable error.

* **Terminated**

  * The orchestration was ended explicitly via a `Terminate` outcome.
  * controlled hard stop from orchestration logic/admin.

* **Cancelled**

  * The orchestration was stopped externally or via a `Cancel` outcome.

---

### Activity Outcomes

Each orchestration activity produces an outcome that controls progression.

If no explicit outcome is returned, the default outcome is **Continue**.

Supported outcomes:

* **Continue**

  * Proceed with the next activity or progression.

* **Retry**

  * Re-execute the current activity according to retry policy.

* **Break**

  * End the current loop or branch.
  * Does not terminate the orchestration itself.

* **Wait**

  * Pause execution and move into the Waiting phase.

* **Cancel**

  * End orchestration execution explicitly in a cancelled state.

* **Complete**

  * End orchestration execution successfully.

* **Terminate**

  * End orchestration execution explicitly in a non-successful manner.
  * May include an optional reason.

---

### Waiting and Pausing

Execution interruption is modeled in two distinct ways:

* **Waiting**

  * An orchestration-controlled pause.
  * Introduced by:

    * a `Wait` outcome, or
    * an explicit wait activity

* **Paused**

  * An externally imposed interruption.
  * Triggered via administrative or operational actions.

---

### Waiting Semantics

Waiting is a first-class concept and can be introduced in two ways:

* **Outcome-based**

  * An activity returns `Wait` dynamically during execution.

* **Declarative**

  * A dedicated wait activity defines the pause structurally in the orchestration.

Both approaches result in identical runtime behavior.

---

### State Transitions

* Transitions are explicitly defined.

* Triggered by:

  * Activity outcomes
  * Conditions
  * Signals
  * Time-based events

* Activity outcomes control execution behavior.

* State transitions control business progression.

---

## Orchestration Context

Each orchestration instance operates with a dedicated, typed orchestration context designed for the orchestration definition.

The orchestration context is a shared execution object available throughout the lifecycle of the orchestration.

Upon starting a new orchestration instance, an optional strongly-typed data object can be provided. This data is defined by the orchestration definition and is accessible via the context during execution.

### Context Responsibilities

The orchestration context contains:

* **Execution metadata**

  * Orchestration name
  * Execution identifier
  * Correlation identifier
  * Start and completion timestamps
  * Current activity name
  * Execution counters and derived runtime information

* **Typed orchestration data**

  * Optional data specific to the orchestration definition
  * Defined as a strongly-typed object by the orchestration definition implementing a common interface or base class

* **Execution-scoped properties**

  * A property bag for unstructured metadata

### Context Characteristics

* Orchestration-specific data is optional.
* The orchestration context itself is always present.
* Activities interact exclusively through the context.
* No activity input or output parameters are supported; all data is accessed via the context.
  * This approach simplifies persistence and execution consistency, but requires all data dependencies to be modeled explicitly in the orchestration context.

---

## Execution Semantics

* **Deterministic Behavior**

  * Execution is driven by persisted state and explicit activity outcomes.
Activities may interact with external systems; therefore idempotency is required to ensure safe re-execution.

* **Idempotency Expectation**

  * Activities should tolerate re-execution.

* **Pause and Resume**

  * Orchestrations may wait as part of normal execution or be paused externally.
  * Resumption continues from the persisted point.

* **Programmatic API**

  * Orchestrations can be started directly from application code.
  * Supports passing initial context.
  * Returns the orchestration instance identifier for tracking.
  * Supports awaiting completion or specific outcomes.

### Starting Orchestrations

The orchestration feature supports three distinct mechanisms for starting execution, each suited to different runtime scenarios:

* **Execute**

  * Synchronous, inline execution within the current context.
  * Suitable for short-running orchestrations that need to execute immediately and return results directly to the caller.
  * Runs inline in the caller's execution flow without dispatching to the background runtime.
  * Returns a task that can be awaited for completion and result retrieval.
  * Blocks the caller until execution completes.
  * Easy for testing and debugging due to synchronous nature.
  * Should be restricted to orchestrations that are expected to complete inline without entering a waiting/blocking state.
  * If execution reaches a Waiting or Paused condition during Execute, an exception is thrown indicating that the orchestration cannot complete inline.

* **Dispatch**

  * Asynchronous, background execution via the orchestration runtime.
  * Suitable for long-running orchestrations that may involve waiting, human interaction
  * Returns immediately without waiting for completion
  * Queues orchestration for background execution
  * Does not block the caller
  * Provides the orchestration instance identifier for tracking and correlation
  * Supports distributed processing

* **DispatchAndWait**

  * A hybrid approach that dispatches the orchestration for background execution while allowing the caller to await its completion or specific outcomes.
  * Combines asynchronous execution with synchronous waiting capabilities.
  * Blocks the caller until execution completes or until the awaited condition is reached.
  * Suitable when background processing is required but the result or a specific orchestration milestone is still needed by the caller.
  * Can optionally wait for completion, specific outcomes, or one or more specific states.

supports:

* Cancellation via CancellationToken
* Optional timeout
* Waiting for:
  * completion
  * one or more states
  * one or more outcomes

---

## Execution Constraints

* Orchestrations can be configured to:

  * Allow concurrent execution
  * Restrict execution to a single active instance (DisallowConcurrentExecution)

---

## Typical Use Cases

* Order processing
* Approval orchestrations
* Data processing pipelines
* Long-running business transactions

---

## Example Orchestration: Order Approval Process

This example shows a long-running order approval process with validation, human approval, timeout handling, payment reservation, and completion.

### Scenario

An order above a configured value requires manual approval before payment is reserved and the order is confirmed.

### States

| State              | Purpose                                   |
| ------------------ | ----------------------------------------- |
| Created            | Initialize and validate the order.        |
| AwaitingApproval   | Wait for an approval or rejection signal. |
| PaymentReservation | Reserve payment after approval.           |
| Confirmed          | Final successful state.                   |
| Rejected           | Final business rejection state.           |
| Cancelled          | Final externally cancelled state.         |

### Context Data

```csharp
public sealed class OrderApprovalData : IOrchestrationData
{
    public string OrderId { get; set; } = default!;
    public decimal OrderAmount { get; set; }
    public string CustomerId { get; set; } = default!;
    public bool RequiresApproval { get; set; }
    public string? ApprovalUserId { get; set; }
    public string? RejectionReason { get; set; }
    public string? PaymentReservationId { get; set; }
}
````

### Definition Sketch

```csharp
public sealed class OrderApprovalOrchestration
    : Orchestration<OrderApprovalData>
{
    public override void Define(IOrchestrationBuilder<OrderApprovalData> builder)
    {
        builder
            .State("Created", state => state // sequential activities example
                .Activity<ValidateOrderActivity>() // custom activity example
                .Activity<DetermineApprovalRequirementActivity>() // custom activity that sets RequiresApproval in context
                .TransitionTo("AwaitingApproval", ctx => ctx.Data.RequiresApproval)
                .TransitionTo("PaymentReservation", ctx => !ctx.Data.RequiresApproval))

            .State("AwaitingApproval", state => state // waiting for external signal example
                .Activity(async (context, cancellationToken) => // inline activity example
                {
                    // Inline activity: create approval task
                    var approvalService = context.Services.GetRequiredService<IApprovalService>();

                    var taskId = await approvalService.CreateApprovalTaskAsync(
                        context.Data.OrderId,
                        context.Data.OrderAmount,
                        cancellationToken);

                    context.Properties["ApprovalTaskId"] = taskId;

                    return OrchestrationOutcome.Continue(); // execution continues to wait for signals after this activity
                })

                .WaitForSignal("OrderApproved", signal => signal // signal-driven transition example
                    .MapToContext((ctx, payload) =>
                    {
                        ctx.Data.ApprovalUserId = payload.ApprovedBy;
                    })
                    .TransitionTo("PaymentReservation"))
                .WaitForSignal("OrderRejected", signal => signal
                    .MapToContext((ctx, payload) =>
                    {
                        ctx.Data.RejectionReason = payload.Reason; // mapping of signal data to context
                    })
                    .TransitionTo("Rejected"))
                .TimeoutAfter(TimeSpan.FromDays(3)) // time-based transition example
                    .TransitionTo("Rejected"))

            .State("PaymentReservation", state => state // activity with retry policy example
                .Activity<ReservePaymentActivity>(activity => activity
                    .Retry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10)))
                .TransitionTo("Confirmed"))

            .State("Confirmed", state => state // completion example
                .Activity<SendConfirmationActivity>()
                .Complete())

            .State("Rejected", state => state // termination example
                .Activity<SendRejectionNotificationActivity>()
                .Terminate("Order was rejected or approval timed out."))

            .OnCancel(cancel => cancel // cancellation handling example
                .Activity<CancelApprovalTaskActivity>()
                .Activity<ReleaseReservedResourcesActivity>());
    }
}
```

### Activity Example

```csharp
public sealed class ReservePaymentActivity
    : IOrchestrationActivity<OrderApprovalData>
{
    private readonly IPaymentService paymentService;

    public ReservePaymentActivity(IPaymentService paymentService)
    {
        this.paymentService = paymentService;
    }

    public async Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<OrderApprovalData> context,
        CancellationToken cancellationToken)
    {
        var reservation = await this.paymentService.ReserveAsync(
            context.Data.OrderId,
            context.Data.OrderAmount,
            cancellationToken);

        context.Data.PaymentReservationId = reservation.Id;

        return OrchestrationOutcome.Continue();
    }
}
```

### Signal Example

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "OrderApproved",
    new OrderApprovedSignal
    {
        ApprovedBy = currentUser.Id
    },
    cancellationToken);
```

### Execution Examples

```csharp
var instanceId = await orchestrations.DispatchAsync(
    new OrderApprovalData
    {
        OrderId = order.Id,
        CustomerId = order.CustomerId,
        OrderAmount = order.TotalAmount
    },
    cancellationToken);
```

```csharp
var result = await orchestrations.DispatchAndWaitAsync(
    new OrderApprovalData
    {
        OrderId = order.Id,
        CustomerId = order.CustomerId,
        OrderAmount = order.TotalAmount
    },
    waitFor: WaitFor.State("Confirmed", "Rejected"),
    timeout: TimeSpan.FromSeconds(30),
    cancellationToken);
```

### Behavior Summary

* The orchestration starts in `Created`.
* The order is validated.
* If approval is not required, it continues directly to `PaymentReservation`.
* If approval is required, it enters `AwaitingApproval`.
* While waiting, execution is persisted and can survive restarts.
* An `OrderApproved` signal moves the instance to `PaymentReservation`.
* An `OrderRejected` signal moves the instance to `Rejected`.
* If no signal arrives within three days, the instance is rejected.
* Payment reservation is retried up to three times.
* Successful payment reservation moves the instance to `Confirmed`.
* Cancellation invokes cleanup activities.

---

## Example Orchestration: Telephone Call State Machine

This example shows a pure state-machine-style orchestration where external signals drive all state transitions.

### Scenario

A telephone call moves through different states based on user actions and call events. Some transitions also execute side effects, such as starting or stopping hold music.

### States

| State          | Purpose                                                  |
| -------------- | -------------------------------------------------------- |
| OffHook        | The phone is active but no call is ringing or connected. |
| Ringing        | A dialed call is waiting to connect.                     |
| Connected      | The call is active.                                      |
| OnHold         | The call is connected but placed on hold.                |
| PhoneDestroyed | Terminal state after the phone is destroyed.             |

### Signals

| Signal                 | Purpose                                                       |
| ---------------------- | ------------------------------------------------------------- |
| CallDialed             | Starts ringing.                                               |
| HungUp                 | Ends the current call flow and returns to OffHook.            |
| CallConnected          | Moves a ringing call to connected.                            |
| LeftMessage            | Ends the connected call after leaving a message.              |
| PlacedOnHold           | Moves a connected call to OnHold and starts hold music.       |
| TakenOffHold           | Moves an on-hold call back to Connected and stops hold music. |
| PhoneHurledAgainstWall | Terminates the call because the phone is destroyed.           |

### Context Data

```csharp
public sealed class TelephoneCallData : IOrchestrationData
{
    public string CallId { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public bool IsOnHold { get; set; }
    public bool IsDestroyed { get; set; }
}
````

### Definition Sketch

```csharp
public sealed class TelephoneCallOrchestration
    : Orchestration<TelephoneCallData>
{
    public override void Define(IOrchestrationBuilder<TelephoneCallData> builder)
    {
        builder
            .State("OffHook", state => state // initial state
                .WhenSignal("CallDialed")
                    .TransitionTo("Ringing"))

            .State("Ringing", state => state // signal-driven transitions
                .WhenSignal("HungUp")
                    .TransitionTo("OffHook")

                .WhenSignal("CallConnected")
                    .TransitionTo("Connected"))

            .State("Connected", state => state // multiple signal-driven transitions with inline activity
                .WhenSignal("LeftMessage")
                    .TransitionTo("OffHook")

                .WhenSignal("HungUp")
                    .TransitionTo("OffHook")

                .WhenSignal("PlacedOnHold", signal => signal
                    .Activity(async (context, cancellationToken) =>
                    {
                        var phoneService = context.Services.GetRequiredService<IPhoneService>();

                        await phoneService.PlayMuzakAsync(
                            context.Data.CallId,
                            cancellationToken);

                        context.Data.IsOnHold = true;

                        return OrchestrationOutcome.Continue();
                    })
                    .TransitionTo("OnHold")))

            .State("OnHold", state => state // inline activity with dependency injection example
                .WhenSignal("TakenOffHold", signal => signal
                    .Activity(async (context, cancellationToken) =>
                    {
                        var phoneService = context.Services.GetRequiredService<IPhoneService>();

                        await phoneService.StopMuzakAsync(
                            context.Data.CallId,
                            cancellationToken);

                        context.Data.IsOnHold = false;

                        return OrchestrationOutcome.Continue();
                    })
                    .TransitionTo("Connected"))

                .WhenSignal("HungUp")
                    .TransitionTo("OffHook")

                .WhenSignal("PhoneHurledAgainstWall", signal => signal
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.IsDestroyed = true;

                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("PhoneDestroyed")))

            .State("PhoneDestroyed", state => state // terminal state with termination reason example
                .Terminate("The phone was destroyed."));
    }
}
```

### Starting the Orchestration

```csharp
var instanceId = await orchestrations.DispatchAsync(
    new TelephoneCallData
    {
        CallId = Guid.NewGuid().ToString("N"),
        PhoneNumber = "+49 123 456789"
    },
    cancellationToken);
```

### Signal Examples

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "CallDialed",
    cancellationToken);
```

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "CallConnected",
    cancellationToken);
```

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "PlacedOnHold",
    cancellationToken);
```

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "TakenOffHold",
    cancellationToken);
```

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "HungUp",
    cancellationToken);
```

### Example Flow

```text
OffHook
  -- CallDialed --> Ringing
  -- CallConnected --> Connected
  -- PlacedOnHold --> OnHold
  -- TakenOffHold --> Connected
  -- HungUp --> OffHook
```

### Destroyed Phone Flow

```text
OffHook
  -- CallDialed --> Ringing
  -- CallConnected --> Connected
  -- PlacedOnHold --> OnHold
  -- PhoneHurledAgainstWall --> PhoneDestroyed
```

### Behavior Summary

* The orchestration starts in `OffHook`.
* `CallDialed` moves the instance to `Ringing`.
* `HungUp` while ringing returns the instance to `OffHook`.
* `CallConnected` moves the instance to `Connected`.
* `LeftMessage` or `HungUp` while connected returns the instance to `OffHook`.
* `PlacedOnHold` runs an inline activity that starts hold music, then moves the instance to `OnHold`.
* `TakenOffHold` runs an inline activity that stops hold music, then moves the instance back to `Connected`.
* `HungUp` while on hold returns the instance to `OffHook`.
* `PhoneHurledAgainstWall` marks the phone as destroyed and moves the instance to `PhoneDestroyed`.
* `PhoneDestroyed` is terminal and ends the orchestration with a `Terminate` outcome.

---

## XML documentation and examples

All public code symbols introduced by this feature should include XML documentation comments:

* public classes
* public records
* public interfaces
* public enums
* public properties
* public methods

For public or client-facing symbols, the XML comments should also include usage examples where that improves discoverability.
