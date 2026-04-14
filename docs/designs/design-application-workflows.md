---
status: draft
---

# Design Document: Workflow Feature (Application.Workflows)

[TOC]

## 1. Introduction

The Workflow feature provides a code-first framework for defining, executing, and managing long-running business processes within the application.

Workflows are persistent, stateful, and designed to support complex orchestration scenarios, including human interaction, event-driven transitions, and fault-tolerant execution.

---

## 2. Core Characteristics

* **Persistence**

  * Workflow instances and their state are persisted via a provider model.
  * State is serializable and can be queried and updated by the application.

* **Code-First Definition**

  * Workflows are defined in code using a fluent API or classes.
  * No visual designer is provided.

* **State Machine Model**

  * Workflows consist of states and transitions.
  * Transitions are triggered by:

    * External signals (events)
    * Programmatic interaction
    * Time-based triggers like timeouts or schedules

* **Task-Based Execution**

  * Each workflow instance progresses through a sequence of steps.
  * Steps operate on a shared workflow context.

* **Long-Running Support**

  * Workflows can be paused, resumed, and persisted across application restarts.

* **Extensibility**

  * The framework is extensible, allowing custom step types, triggers, and persistence providers.
  * A REST API (endpoints) is provided for workflow management and monitoring based on a common data provider model.

---

## 3. Execution Model

* **Step-Oriented Execution**

  * A workflow progresses by executing steps within its current state.

* **Outcome-Driven Progression**

  * Each step produces an outcome that determines how execution proceeds.

* **Dependency Injection**

  * Workflow steps support dependency injection for integration with application services.

* **Concurrency**

  * Multiple workflow instances can execute concurrently.
  * Optional configuration allows restricting execution to a single instance per workflow definition.

* **Scalability**

  * Designed for concurrent and distributed execution. Leases and locking mechanisms ensure consistency in multi-instance scenarios.

---

## 4. Control Flow Capabilities

Workflows support common control flow constructs:

* Conditional branching (`if / else`)
* Switch/case logic
* Parallel branches
* Loops (including recurrence and `do-until`)

---

## 5. Triggers

* **Event-Based Triggers**

  * Transitions can be driven by internal or external signals.

* **Time-Based Triggers**

  * Scheduled or delayed transitions are supported.

* **Human Interaction**

  * Manual steps (e.g., approvals) can pause execution until completed.

---

## 6. Reliability & Resilience

* **Failure Handling**

  * Built-in retry mechanisms for transient failures.
  * Configurable error handling strategies.

* **Compensation (Optional)**

  * Support for compensation actions (SAGA pattern) for rollback scenarios (TBD).

---

## 7. Observability & Management

* **Auditing**

  * All state transitions and step executions are logged.

* **Monitoring**

  * Workflow instances can be inspected at runtime.

* **Administration API**

  * REST API for:

    * Querying workflow instances
    * Inspecting state
    * Triggering transitions
    * Cancelling workflows
    * Pausing and resuming workflows

* **Extensibility**

  * A Dashboard UI is not provided but can be built on top of the API.

---

## 8. Identity & Correlation

* Each workflow instance has a **correlation ID** for tracking across system boundaries.

---

## 9. Versioning (TBD)

* Workflow definitions may support versioning.
* New versions should not affect already running instances.

---

## 10. Testing

* Workflows are fully testable.
* Unit testing of:

  * Transitions
  * Step logic
  * Control flow

---

## 11. Execution Lifecycle

A workflow instance progresses through a well-defined lifecycle from creation to completion. Execution is state-driven and step-outcome-driven.

### 11.1 Lifecycle Phases

1. **Created**

   * A new workflow instance is initialized.

2. **Running**

   * The workflow actively executes steps.

3. **Waiting**

   * Execution is paused as part of normal workflow behavior.

4. **Paused**

   * Execution is suspended by an external action.

5. **Resuming**

   * Execution continues from a previously waiting or paused state.

6. **Completed**

   * The workflow has ended successfully.
   * This occurs either:

     * implicitly when execution reaches the natural end, or
     * explicitly via a `Complete` outcome.

7. **Failed**

   * The workflow encountered a non-recoverable error.

8. **Terminated**

   * The workflow was ended explicitly via a `Terminate` outcome.

9. **Cancelled**

   * The workflow was stopped externally or via a `Cancel` outcome.

---

### 11.2 Step Outcomes

Each workflow step produces an outcome that controls progression.

Supported outcomes:

* **Continue**

  * Proceed with the next step or progression.

* **Retry**

  * Re-execute the current step according to retry policy.

* **Break**

  * End the current loop or branch.
  * Does not terminate the workflow itself.

* **Wait**

  * Pause execution and move into the Waiting phase.

* **Cancel**

  * End workflow execution explicitly in a cancelled state.

* **Complete**

  * End workflow execution successfully.

* **Terminate**

  * End workflow execution explicitly in a non-successful manner.
  * May include an optional reason.

---

### 11.3 Waiting and Pausing

Execution interruption is modeled in two distinct ways:

* **Waiting**

  * A workflow-controlled pause.
  * Introduced by:

    * a `Wait` outcome, or
    * an explicit wait step

* **Paused**

  * An externally imposed interruption.
  * Triggered via administrative or operational actions.

---

### 11.4 Waiting Semantics

Waiting is a first-class concept and can be introduced in two ways:

* **Outcome-based**

  * A step returns `Wait` dynamically during execution.

* **Declarative**

  * A dedicated wait step defines the pause structurally in the workflow.

Both approaches result in identical runtime behavior.

---

### 11.5 State Transitions

* Transitions are explicitly defined.

* Triggered by:

  * Step outcomes
  * Conditions
  * Signals
  * Time-based events

* Step outcomes control execution behavior.

* State transitions control business progression.

---

## 12. Workflow Context

Each workflow instance operates with a dedicated typed workflow context designed for the workflow definition.

The workflow context is a shared execution object available throughout the lifecycle of the workflow.

### 12.1 Context Responsibilities

The workflow context contains:

* **Execution metadata**

  * Workflow name
  * Execution identifier
  * Correlation identifier
  * Start and completion timestamps
  * Current step name
  * Execution counters and derived runtime information

* **Typed workflow data**

  * Optional data specific to the workflow definition
  * Defined as a strongly-typed object by the workflow definition implementing a common interface or base class

* **Execution-scoped properties**

  * A property bag for unstructured metadata

### 12.2 Context Characteristics

* Workflow-specific data is optional.
* The default workflow context itself is always present.
* Steps interact exclusively through the context.
* No step input or output parameters are supported; all data is accessed via the context.

---

## 13. Execution Semantics

* **Deterministic Behavior**

  * Given the same inputs and definition, execution is predictable.

* **Idempotency Expectation**

  * Steps should tolerate re-execution.

* **Pause and Resume**

  * Workflows may wait as part of normal execution or be paused externally.
  * Resumption continues from the persisted point.

---

## 14. Execution Constraints

* Workflows can be configured to:

  * Allow concurrent execution
  * Restrict execution to a single active instance (DisallowConcurrentExecution)

---

## 15. Typical Use Cases

* Order processing
* Approval workflows
* Data processing pipelines
* Long-running business transactions
