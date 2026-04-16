---
status: draft
---

# Design Document: Orchestration Feature (Application.Orchestration)

> This design document outlines the architecture and behavior of the new orchestration feature within the application. It defines the core concepts, execution model, control flow capabilities, triggers, reliability mechanisms, observability features, identity management, versioning considerations, testing strategies, and typical use cases for orchestrations.

## Introduction

The orchestration feature provides a code-first framework for defining, executing, and managing long-running processes within the application.

Orchestrations are persistent, stateful, and designed to support complex coordination scenarios, including human interaction, event-driven transitions, and fault-tolerant execution.

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

---

## Triggers

* **Event-Based Triggers**

  * Transitions can be driven by internal or external signals.

* **Time-Based Triggers**

  * Scheduled or delayed transitions are supported.

* **Human Interaction**

  * Manual activities (e.g., approvals) can pause execution until completed.

---

## Reliability & Resilience

* **Failure Handling**

  * Built-in retry mechanisms for transient failures.
  * Configurable error handling strategies.

* **Compensation (Optional)**

  * Support for compensation actions (SAGA pattern) for rollback scenarios (TBD).

---

## Observability & Management

* **Auditing**

  * All state transitions and activity executions are logged.

* **Monitoring**

  * Orchestration instances can be inspected at runtime.

* **Administration API**

  * REST API for:

    * Querying orchestration instances
    * Inspecting state
    * Triggering transitions
    * Cancelling orchestrations
    * Pausing and resuming orchestrations

* **Extensibility**

  * A dashboard UI is not provided but can be built on top of the API.
  * see [https://docs.diagrid.io/develop/diagrid-dashboard/](https://docs.diagrid.io/develop/diagrid-dashboard/)

---

## Identity & Correlation

* Each orchestration instance has a **correlation ID** for tracking across system boundaries.

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

* **Paused**

  * Execution is suspended by an external action.

* **Resuming**

  * Execution continues from a previously waiting or paused state.

* **Completed**

  * The orchestration has ended successfully.
  * This occurs either implicitly when execution reaches the natural end, or explicitly via a `Complete` outcome.

* **Failed**

  * The orchestration encountered a non-recoverable error.

* **Terminated**

  * The orchestration was ended explicitly via a `Terminate` outcome.

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

---

## Execution Semantics

* **Deterministic Behavior**

  * Given the same inputs and definition, execution is predictable.

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
  * Runs in the caller's context/process (same thread).
  * Returns a task that can be awaited for completion and result retrieval.
  * Blocks the caller until execution completes.
  * Easy for testing and debugging due to synchronous nature.
  * Should be restricted to orchestrations that are expected to complete inline without entering a waiting/blocking state.
  * If execution reaches a waiting/paused condition, the call should fail fast rather than silently converting into a long-running blocking operation.

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

--

## XML documentation and examples

All public code symbols introduced by this feature should include XML documentation comments:

* public classes
* public records
* public interfaces
* public enums
* public properties
* public methods

For public or client-facing symbols, the XML comments should also include usage examples where that improves discoverability.
