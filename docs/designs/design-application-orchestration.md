---
status: draft
---

# Design Document: Orchestration Feature (Application.Orchestration)

> This design document outlines the architecture and behavior of the orchestration feature within the application. It defines the core concepts, execution model, control flow capabilities, triggers, reliability mechanisms, observability features, identity management, versioning considerations, testing strategies, and typical use cases for orchestrations.

## Introduction

The orchestration feature provides a code-first framework for defining, executing, and managing long-running processes within the application.

Orchestrations are persistent, stateful, and designed to support complex coordination scenarios, including human interaction, event-driven transitions, and fault-tolerant execution.

---

## Terminology

| Term                       | Description                                                                                                            |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| **Orchestration**          | A definition that describes a long-running process composed of states, steps, and transitions.                         |
| **Orchestration Instance** | A runtime execution of an orchestration definition.                                                                    |
| **State**                  | A logical phase within an orchestration where a set of steps is executed.                                              |
| **Step**                   | The smallest unit of execution within a state that produces an outcome controlling progression.                        |
| **Outcome**                | The result of a step that determines execution behavior (e.g. Continue, Retry, Wait, Complete).                        |
| **Transition**             | A movement from one state to another based on conditions, signals, or outcomes.                                        |
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

  * Each orchestration instance progresses through a sequence of steps.
  * Steps operate on a shared orchestration context.

* **Long-Running Support**

  * Orchestrations can be paused, resumed, and persisted across application restarts.

* **Extensibility**

  * The framework is extensible, allowing custom step types, triggers, and persistence providers.
  * A REST API is provided for orchestration management and monitoring based on a common data provider model.

---

## Execution Model

* **Step-Oriented Execution**

  * An orchestration progresses by executing steps within its current state.

* **Outcome-Driven Progression**

  * Each step produces an outcome that determines how execution proceeds.

* **Dependency Injection**

  * Orchestration steps support dependency injection for integration with application services.

* **Concurrency**

  * Multiple orchestration instances can execute concurrently.
  * Optional configuration allows restricting execution to a single instance per orchestration definition.

* **Scalability**

  * Designed for concurrent and distributed execution. Leases and locking mechanisms ensure consistency in multi-instance scenarios.

---

## Control Flow Capabilities

Orchestrations support common control flow constructs:

* Conditional branching (`if / else`)
* Switch/case logic
* Parallel branches
* Loops (including recurrence and `do-until`)

---

## Triggers

* **Event-Based Triggers**

  * Transitions can be driven by internal or external signals.

* **Time-Based Triggers**

  * Scheduled or delayed transitions are supported.

* **Human Interaction**

  * Manual steps (e.g., approvals) can pause execution until completed.

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

  * All state transitions and step executions are logged.

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

---

## Identity & Correlation

* Each orchestration instance has a **correlation ID** for tracking across system boundaries.

---

## Versioning (TBD)

* Orchestration definitions may support versioning.
* New versions should not affect already running instances.

---

## Testing

* Orchestrations are fully testable.
* Unit testing of:

  * Transitions
  * Step logic
  * Control flow

---

## Execution Lifecycle

An orchestration instance progresses through a well-defined lifecycle from creation to completion. Execution is state-driven and step-outcome-driven.

### Lifecycle Phases

* **Created**

  * A new orchestration instance is initialized.

* **Running**

  * The orchestration actively executes steps.

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

### Step Outcomes

Each orchestration step produces an outcome that controls progression.

If no explicit outcome is returned, the default outcome is **Continue**.

Supported outcomes:

* **Continue**

  * Proceed with the next step or progression.

* **Retry**

  * Re-execute the current step according to retry policy.

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
    * an explicit wait step

* **Paused**

  * An externally imposed interruption.
  * Triggered via administrative or operational actions.

---

### Waiting Semantics

Waiting is a first-class concept and can be introduced in two ways:

* **Outcome-based**

  * A step returns `Wait` dynamically during execution.

* **Declarative**

  * A dedicated wait step defines the pause structurally in the orchestration.

Both approaches result in identical runtime behavior.

---

### State Transitions

* Transitions are explicitly defined.

* Triggered by:

  * Step outcomes
  * Conditions
  * Signals
  * Time-based events

* Step outcomes control execution behavior.

* State transitions control business progression.

---

## Orchestration Context

Each orchestration instance operates with a dedicated, typed orchestration context designed for the orchestration definition.

The orchestration context is a shared execution object available throughout the lifecycle of the orchestration.

### Context Responsibilities

The orchestration context contains:

* **Execution metadata**

  * Orchestration name
  * Execution identifier
  * Correlation identifier
  * Start and completion timestamps
  * Current step name
  * Execution counters and derived runtime information

* **Typed orchestration data**

  * Optional data specific to the orchestration definition
  * Defined as a strongly-typed object by the orchestration definition implementing a common interface or base class

* **Execution-scoped properties**

  * A property bag for unstructured metadata

### Context Characteristics

* Orchestration-specific data is optional.
* The orchestration context itself is always present.
* Steps interact exclusively through the context.
* No step input or output parameters are supported; all data is accessed via the context.

---

## Execution Semantics

* **Deterministic Behavior**

  * Given the same inputs and definition, execution is predictable.

* **Idempotency Expectation**

  * Steps should tolerate re-execution.

* **Pause and Resume**

  * Orchestrations may wait as part of normal execution or be paused externally.
  * Resumption continues from the persisted point.

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
