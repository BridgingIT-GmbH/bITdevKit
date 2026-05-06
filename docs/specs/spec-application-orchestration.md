---
status: draft
---

# Design Specification: Stateful Orchestration Feature (Application.Orchestration)

> This design document outlines the architecture and behavior of the new orchestration feature within the application. It defines the core concepts, execution model, control flow capabilities, triggers, reliability mechanisms, observability features, identity management, versioning considerations, testing strategies and typical use cases for orchestrations.

## Introduction

The orchestration feature provides a code-first framework for defining, executing and managing long-running processes within the application.

Orchestrations are persistent, stateful and designed to support complex coordination scenarios, including human interaction, event-driven transitions and fault-tolerant execution.

The orchestration model is state-machine-oriented. States represent stable phases of a long-running process, while activities perform work within a state. Transitions move an orchestration instance between states based on outcomes, conditions, signals, or time-based triggers.

The feature intentionally combines state-machine semantics with selected workflow capabilities such as activity execution, waiting, retries, human interaction and compensation.

The main feature implementation is intended to live in the `Application.Orchestration` project.
The durable Entity Framework implementation is intended to live in `Infrastructure.EntityFramework/Orchestration` project.
The web endpoint surface for orchestration administration and query access is intended to live in `Presentation.Web.Orchestration` project.

Disclaimer: this feature is not a full blown workflow engine or a business process modeling tool. It is a code-centric framework for structuring long-running processes in a maintainable and testable way. Please evaluate carefully if this feature is a good fit for your specific use case, as it can be opinionated in its execution model and may not be suitable for all scenarios. Alternatives: Elsa, Camunda, Dapr Workflows, MassTransit Courier, Temporal, Durable Functions, etc.

---

## Capability Layers

This specification describes the full target capability of the orchestration feature.
To keep the implementation understandable and maintainable, the feature is organized into capability layers. These layers do not change the execution model; they describe how the complete feature can be built and reasoned about incrementally.

### Foundation Layer

The foundation layer contains the minimum capabilities required for a durable stateful orchestration runtime.

It includes:

* Code-first orchestration definitions
* States, activities, outcomes and transitions
* Typed orchestration context
* Context snapshot persistence
* Orchestration instance persistence
* Durable execution history
* Exclusive instance leases
* Inline execution
* Background dispatch
* Signal delivery
* Waiting
* Durable timers
* Basic lifecycle operations:
  * start
  * signal
  * cancel
  * terminate

### Operational Layer

The operational layer contains the capabilities required to inspect, manage and operate orchestrations in real systems.

It includes:

* Query service
* Current instance inspection
* Context snapshot inspection
* Execution history inspection
* Signal and timer inspection
* Metrics queries
* Administration endpoints
* Pause and resume
* Archive and purge
* Maintenance and repair operations
* Dashboard-ready query contracts

### Advanced Workflow Layer

The advanced workflow layer contains higher-level orchestration capabilities built on top of the foundation model.

It includes:

* Parallel branches
* Loops
* Compensation
* Child orchestrations
* Human task helpers
* Approval activity
* Built-in activity catalog
* Advanced retry and recovery patterns

### Provider Layer

The provider layer contains durable infrastructure implementations.

The first full provider is the Entity Framework provider.

It includes:

* Entity Framework persistence implementation
* `IOrchestrationContext` DbContext integration
* Instance snapshot tables
* Execution history tables
* Signal inbox tables
* Timer tables
* Lease metadata
* Query and metrics support

Alternative providers may be added later, provided they preserve the same runtime behavior and persistence contracts.

### Layering Rule

Higher layers must not weaken or bypass the foundation layer.

All orchestration behavior, including advanced activities, compensation, timers, signals, pause/resume and administration actions, must still follow:

* durable boundary persistence
* lease-protected state mutation
* append-only execution history
* context snapshot persistence
* explicit state transition semantics

---

## XML documentation and examples

All public/protected code symbols introduced by this feature should include XML documentation comments:

* classes
* records
* interfaces
* enums
* properties
* methods

For public or client-facing symbols, the XML comments should also include usage examples where that improves usability.

---

## Terminology

| Term                       | Description                                                                                                            |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| **Orchestration**          | A definition that describes a long-running process composed of states, activities and transitions.                    |
| **Orchestration Instance** | A runtime execution of an orchestration definition.                                                                    |
| **State**                  | A logical phase within an orchestration where a set of activities is executed.                                         |
| **Activity**               | The smallest unit of execution within a state that produces an outcome controlling progression.                        |
| **Outcome**                | The result of an activity that determines execution behavior (e.g. Continue, Retry, Wait, Complete).                   |
| **Transition**             | A movement from one state to another based on conditions, signals (events) or outcomes.                                |
| **Orchestration Context**  | The shared execution object containing runtime metadata, optional orchestration data and execution-scoped properties. |

---

## Core Characteristics

* **Persistence**

  * Orchestration instances and their state are persisted via a provider model.
  * The first durable implementation shall be based on Entity Framework and integrate into a project-specific application `DbContext` through a marker-interface capability contract.
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

  * Orchestrations can be paused, resumed and persisted across application restarts.

* **Extensibility**

  * The framework is extensible, allowing custom activity types, triggers and persistence providers.
  * A REST API is provided for orchestration management and monitoring based on a common data provider model.

---

## Execution Model

The orchestration runtime is a durable, state-driven execution engine.

Its execution contract is defined by the following rules:

* **State-oriented execution**

  * An orchestration instance is always in exactly one current business state.
  * The current business state is persisted durably.
  * Activities, signal handlers, timers and transitions are always evaluated relative to the persisted current state.

* **Sequential activity execution inside a state**

  * Activities inside a state execute in declaration order unless a parallel branch construct is used explicitly.
  * The default model is sequential and deterministic.
  * The runtime shall not execute two activities of the same sequential state concurrently.

* **Outcome-driven progression**

  * Each activity produces an outcome that determines how execution proceeds.
  * If no explicit outcome is returned, the default outcome is **Continue**.
  * Outcomes influence execution flow; transitions influence state progression.

* **Durable progression points**

  * The runtime shall persist the orchestration instance and its latest full context snapshot whenever execution crosses a durable boundary.
  * Durable boundaries include:
    * instance creation
    * initial-state entry
    * completion of each activity attempt
    * acceptance and mapping of a signal
    * transition from one state to another
    * entering Waiting
    * entering Paused
    * resuming from Waiting or Paused
    * scheduling or consuming a timer
    * terminal completion (`Completed`, `Cancelled`, `Failed`, `Terminated`)
  * Each persisted durable boundary shall be represented by append-only execution history records.

* **Dependency injection**

  * Orchestration activities support dependency injection for integration with application services.
  * Dependency resolution happens at execution time within a DI scope owned by the orchestration runtime.

* **Concurrency**

  * Multiple orchestration instances can execute concurrently.
  * Optional configuration allows restricting execution to a single instance per orchestration definition or per configured concurrency key.

* **Scalability**

  * The feature is designed for concurrent and distributed execution.
  * Leases and locking mechanisms ensure that only one worker advances a specific orchestration instance at a time.

### Lease Contract

The orchestration runtime shall support execution on multi-node systems.

To ensure correctness in a distributed environment, every state-mutating execution action shall run under an exclusive orchestration-instance lease.

This includes at least:

* each activity attempt
* each signal-handler execution
* each timer-consumption action
* each transition application
* each compensation action
* each pause, resume, cancel, terminate, or complete action

The lease contract is:

* a lease is acquired per orchestration instance, not globally for the whole system
* at most one worker may hold the active lease for a given orchestration instance at a time
* a worker must acquire the lease before executing any state-mutating action
* the worker must still hold a valid lease when persisting the result of that action
* leases are time-bound and renewable so another node can continue after node failure
* lease owner identity and lease expiration metadata are persisted or otherwise durably coordinated
* if a worker loses its lease, it must stop advancing the instance immediately
* a new worker may resume only from the latest persisted orchestration snapshot
* leases prevent concurrent advancement of the same orchestration instance, but they do not by themselves deduplicate two separately submitted logical signals
* prevention of repeated business handling for duplicate logical signals relies on persisted signal identity, optional caller-supplied idempotency keys, and current-state validation during signal processing

### Execution Algorithm

The runtime shall advance an orchestration instance using the following high-level algorithm:

1. Load the orchestration definition and the latest persisted instance snapshot.
2. Acquire an exclusive lease for the instance.
3. Rehydrate the typed orchestration context from the persisted snapshot.
4. If the instance is newly created, enter the configured initial state and persist that state entry immediately.
5. Execute one eligible state-mutating action while holding the lease.
6. Persist the outcome of that action together with the updated full context snapshot.
7. Renew or re-check the lease before performing the next state-mutating action.
8. Evaluate the resulting outcome and the state's transition definitions.
9. If a transition is selected, persist a transition record, update the current state and persist the new full context snapshot before continuing.
10. If execution must wait for a signal or timer, persist the waiting reason plus any pending timer registrations, then release the lease.
11. If execution reaches a terminal outcome, persist the final status, final context snapshot and completion metadata, then release the lease.
12. If execution cannot continue because no valid transition, wait, or terminal condition exists, mark the instance as `Failed` with a descriptive orchestration-definition error.

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
* Compensation (explicit support for compensation actions in rollback scenarios, following the SAGA pattern)

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
  * Failures and retries shall be durable; retry state may not exist only in memory.

Exceptions thrown by activities are treated as execution failures of the current activity attempt.

The runtime shall:

* persist the failed activity attempt, including exception metadata suitable for diagnostics
* evaluate the activity-level or state-level retry policy
* persist retry scheduling metadata before the next attempt is eligible
* mark the orchestration `Failed` when no retry or recovery path remains

The outcome model is used for controlled execution flow.

Business rejection or expected negative business paths should be modeled explicitly through state, data and transitions rather than through exceptions.

Exceptions are intended for technical or unexpected failures.

* **Idempotency**

  * Activities shall be designed to tolerate re-execution.
  * Because the engine persists snapshots between steps and may resume after partial infrastructure failure, an already-started activity may be attempted again.
  * Idempotency is therefore a required authoring concern, not an optional optimization.

* **Compensation**

  * Compensation actions are supported for rollback-style scenarios.
  * Successful compensable activities shall register durable compensation entries.
  * Compensation executes in reverse registration order unless configured otherwise.
  * Compensation execution and compensation outcomes shall themselves be persisted as execution history.

### Persistence Contract

Durability is a core property of the orchestration feature.

The runtime shall persist both the latest orchestration snapshot and the execution history as the orchestration moves along.

This is a mandatory requirement, including:

* every state transition
* every persisted context mutation after a completed activity attempt
* every accepted signal
* every timer registration and timer consumption
* every pause, resume, cancellation, failure, compensation step and terminal completion

The durable model shall include at least:

* **Instance snapshot**

  * instance identifier
  * orchestration definition name
  * current lifecycle status
  * current business state name
  * current activity name when applicable
  * correlation identifier
  * concurrency key when configured
  * started/completed timestamps
  * latest serialized full orchestration context
  * audit metadata including `CreatedDate`, `UpdatedDate`, `CreatedBy` and `UpdatedBy`
  * version/concurrency token for optimistic updates

* **Execution history**

  * append-only records describing state entry, activity execution, retries, transitions, waits, signals, timers, compensation, pause/resume and terminal outcomes
  * each record includes UTC timestamp, instance identifier, event type and relevant metadata
  * history records shall include enough audit metadata to identify when the action was recorded and, when available, which user initiated or caused the action

* **Signal inbox**

  * persisted signal records correlated to an orchestration instance
  * payload, signal name, received timestamp, processing status and idempotency key when provided
  * audit metadata including `CreatedDate`, `UpdatedDate`, `CreatedBy` and `UpdatedBy`

* **Timer records**

  * persisted due time, trigger kind, target state or continuation metadata and consumption status
  * audit metadata including `CreatedDate`, `UpdatedDate`, `CreatedBy` and `UpdatedBy`

* **Instance-owned compensation state**

  * persisted reverse-order compensation stack for successfully completed compensable work
  * stored as part of the orchestration instance's durable state rather than as a separate top-level persistence root

The runtime must always be able to reconstruct the latest execution point from persisted state without requiring replay of in-memory-only mutations.

### Auditability Contract

The orchestration feature shall provide lightweight auditability over persisted orchestration operations and records.

The auditability contract is:

* top-level persisted orchestration records shall store `CreatedDate` and `UpdatedDate`
* top-level persisted orchestration records should also store `CreatedBy` and `UpdatedBy` when a current user identity is available
* this applies at least to orchestration instances, persisted signals and persisted timers
* execution history shall capture the action timestamp and, when available, the user identity associated with the recorded orchestration action
* audit metadata shall be updated as orchestration state changes, signals are accepted/finalized, timers are scheduled/consumed and maintenance actions are performed

The runtime and provider may use `ICurrentUserAccessor` to resolve the current user identity for these audit fields.

`ICurrentUserAccessor` is optional:

* the orchestration feature must work correctly when `ICurrentUserAccessor` is not registered
* the orchestration feature must work correctly when a current user is not authenticated or no user identifier can be resolved
* in those cases the runtime/provider shall continue gracefully and leave `CreatedBy` and `UpdatedBy` empty or use a provider-defined system identity value

Auditability is a persistence and operations concern. It does not require every internal runtime helper or execution method to carry audit state explicitly as part of its own public contract.

### Retention, Archival and Purge

The orchestration feature shall support retained operational history together with explicit archival and purge operations.

This requirement applies to:

* orchestration instances
* execution history
* persisted signals
* persisted timers
* compensation state retained as part of orchestration instances and execution history

The retention contract is:

* completed and non-active orchestration data may remain queryable for operational support and dashboard scenarios
* archival and purge are explicit lifecycle/maintenance operations, not implicit loss of history
* archival and purge behavior shall be exposed consistently through the administration/query surface and the persistence provider
* providers may retain active and archived data differently, but archived data must remain inspectable until purged

The purge contract is:

* purge shall support at least age-based deletion
* purge should support filtering by status and archived state where meaningful
* purge operations shall be explicit maintenance actions

The Entity Framework provider shall support retained orchestration history, archival-oriented inspection and purge operations in the same overall operational style already used by the queueing and messaging features.

### Persistence Provider Model

The durable layer shall be exposed through a provider model.

The orchestration runtime, authoring model and public orchestration services should depend on orchestration persistence abstractions rather than directly on Entity Framework types.

The provider model shall allow alternative persistence implementations later without changing the orchestration execution contract.

The provider contract shall cover at least:

* instance snapshot storage and update
* execution history append
* signal inbox storage and state updates
* timer storage and consumption
* persistence of compensation state as part of orchestration instances
* lease acquisition, renewal and release
* orchestration-instance querying for runtime and administration

Provider implementations must preserve the same observable orchestration behavior, especially:

* lease exclusivity semantics
* durable-boundary persistence rules
* append-only history behavior
* snapshot-based context persistence
* deterministic signal and timer processing rules

### Persistence Abstractions

The persistence provider model should expose a small set of focused orchestration persistence abstractions rather than one oversized storage service.

The runtime should depend on an orchestration persistence provider that groups dedicated stores/services for execution and querying, including metrics queries.

The persistence abstraction set shall distinguish between:

* **Execution-facing abstractions**
  * used by the orchestration runtime to advance instances safely
* **Operations-facing abstractions**
  * used by application services, administration APIs, dashboards and support tooling

### Execution-Facing Persistence Abstractions

The runtime needs the following minimum abstractions.

* **`IOrchestrationInstanceStore`**

  * Creates a new orchestration instance snapshot.
  * Loads the latest snapshot for a specific instance.
  * Persists the latest orchestration snapshot, including lifecycle status, current business state, current activity and serialized full context.
  * Applies optimistic concurrency checks when persisting snapshot changes.

* **`IOrchestrationLeaseStore`**

  * Acquires an exclusive lease for a specific orchestration instance.
  * Renews an existing lease.
  * Releases a lease explicitly.
  * Verifies current lease ownership when finalizing a state-mutating action.

* **`IOrchestrationHistoryStore`**

  * Appends durable execution history records.
  * Stores transition, activity, retry, waiting, signal, timer, compensation, pause/resume and terminal lifecycle events.
  * Preserves append-only semantics.

* **`IOrchestrationSignalStore`**

  * Persists incoming signals before processing.
  * Loads processable signals for a specific orchestration instance and current state.
  * Marks signals as processed, ignored, rejected, or failed.
  * Enforces signal idempotency using persisted signal identity and optional idempotency keys.

* **`IOrchestrationTimerStore`**

  * Persists newly scheduled timers and timeouts.
  * Loads due timers eligible for consumption.
  * Marks timers as consumed, cancelled, or obsolete.
  * Supports deterministic ordering when multiple due timers exist for the same orchestration instance.

* **`ISerializer`**

  * Serializes the orchestration context into the durable snapshot format.
  * Deserializes the persisted snapshot back into the typed orchestration context.
  * Keeps context persistence provider-neutral and avoids leaking storage representation into the runtime.
  * The orchestration feature shall use `BridgingIT.DevKit.Common.ISerializer`.
  * If no serializer is configured explicitly, the default shall be `BridgingIT.DevKit.Common.SystemTextJsonSerializer`.

### Operations-Facing Persistence Abstractions

The feature also needs read/query abstractions over persisted orchestration state for support, monitoring, metrics and APIs.

* **`IOrchestrationQueryStore`**

  * Returns the current orchestration instance summary by instance identifier.
  * Returns the current orchestration context snapshot by instance identifier.
  * Returns paged orchestration-instance lists with filters such as:
    * orchestration name
    * lifecycle status
    * current business state
    * correlation identifier
    * concurrency key
    * started/completed time range
  * Returns execution history for a specific orchestration instance.
  * Returns persisted signal and timer records for inspection when supported by the provider contract.
  * Returns aggregated metrics derived from persisted orchestration data.
  * Supports at least:
    * instance counts by lifecycle status
    * instance counts by orchestration definition
    * active waiting/paused/running counts
    * completion/failure/cancellation counts
    * oldest waiting instance timestamps
    * average and percentile duration metrics when the provider can compute them efficiently
    * retry counts and timer/signal processing counts when available from persisted history

Metrics may be computed directly from durable storage, from provider-maintained summary tables, or from provider-maintained projections, as long as the metrics remain derived from persisted orchestration data rather than worker-local memory.

### Abstraction Shape

The abstractions should be composable and provider-neutral.

The provider model shall follow this responsibility shape:

```csharp
public interface IOrchestrationPersistenceProvider
{
    IOrchestrationInstanceStore Instances { get; }
    IOrchestrationLeaseStore Leases { get; }
    IOrchestrationHistoryStore History { get; }
    IOrchestrationSignalStore Signals { get; }
    IOrchestrationTimerStore Timers { get; }
    IOrchestrationQueryStore Queries { get; }
    ISerializer Serializer { get; }
}
```

Equivalent implementations are allowed, but the feature shall preserve these responsibilities.

Execution-facing abstractions should not depend on HTTP or UI concerns.

Operations-facing abstractions should not require callers to understand provider-internal table layouts.

### Query and Metrics Contracts

Query and metrics support are first-class feature requirements, not optional diagnostics add-ons.

The persistence abstraction model shall support at least these application-facing scenarios:

* load the current status of an orchestration instance
* load the current business state of an orchestration instance
* load the latest persisted orchestration context snapshot
* inspect execution history and transition history
* inspect pending or processed signals
* inspect pending, consumed, overdue, or obsolete timers
* list active, waiting, paused, completed, failed, cancelled, or terminated orchestrations with paging and filtering
* retrieve aggregated counts and duration-oriented metrics for operational dashboards

The current status and current context snapshot are mandatory provider capabilities.

### Mandatory Provider Capabilities

Any persistence provider that claims runtime execution support for orchestration shall implement these mandatory capabilities:

* create, load and update orchestration instance snapshots with optimistic concurrency protection
* acquire, renew, validate and release orchestration-instance leases
* append execution history records
* persist, query and finalize signals according to the signal-processing contract
* persist, query and finalize timers according to the timer-processing contract
* persist and load compensation state as part of orchestration instances
* serialize and deserialize orchestration contexts through `ISerializer`
* expose current-instance query capabilities for:
  * current lifecycle status
  * current business state
  * latest persisted context snapshot
  * execution history
* expose filtered list queries for:
  * orchestration definition name
  * lifecycle status
  * current business state
  * correlation identifier
  * concurrency key
  * date range
* expose aggregated metrics queries required by the query contract

Providers may add richer provider-specific queries, but they may not weaken the mandatory capabilities above.

For avoidance of doubt:

* current status is mandatory
* current context snapshot is mandatory
* execution history is mandatory
* filtered list queries are mandatory
* aggregated metrics queries are mandatory

### Dashboard and Endpoint Query Contract

The persistence layer and endpoint layer shall together support building a rich operational dashboard for orchestration monitoring and support.

The dashboard contract shall support:

* listing running and past orchestration instances
* inspecting a specific orchestration instance in detail
* loading the latest persisted context snapshot for a specific instance
* loading execution history, transition history, signals and timers for a specific instance
* loading overall metrics and filtered metrics across orchestration definitions

The query/filter contract shall support at least:

* date range filters for started/completed/updated activity
* lifecycle status filters
* orchestration definition name filters
* current business state filters
* correlation identifier filters
* concurrency key filters
* free-text or exact identifier lookup by orchestration instance identifier
* paging and sorting for list endpoints

The endpoint contract should expose enough data to support views such as:

* active/running workflows
* waiting/paused workflows
* completed/failed/cancelled/terminated workflows
* instance detail with current context snapshot
* instance history timeline
* overall counts, durations, retries, signal activity and timer activity

The query and endpoint model should be designed so that a provider-backed dashboard can examine both current state and retained history without requiring direct access to provider-internal tables.

### Entity Framework First Implementation

The first durable provider shall be implemented with Entity Framework.

Its design contract is:

* persistence integrates into a host/project-specific `DbContext` rather than requiring a separate framework-owned orchestration `DbContext`
* the host `DbContext` opts into orchestration persistence by implementing an orchestration Entity Framework marker/capability interface
* the host `DbContext` owns the tables for:
  * orchestration instance snapshots
  * execution history records
  * persisted signals
  * persisted timers
  * lease metadata when stored in the same database
* the Entity Framework provider is responsible for translating the storage-agnostic provider contract into relational persistence operations
* the runtime above the provider must not assume SQL Server-specific or Entity Framework-specific behavior
* Entity Framework is the first implementation, not the persistence abstraction itself

The Entity Framework provider should use normal devkit patterns for:

* `DbContext`-based persistence
* optimistic concurrency via concurrency tokens
* transactional updates where multiple durable records must move together
* query support for administration and monitoring endpoints
* provider-controlled `DbContext` scope management through `IServiceProvider`
* optional resolution of `ICurrentUserAccessor` through `IServiceProvider` for audit metadata population

The Entity Framework provider is also the first full-fidelity operational provider.

It should expose the complete query and metrics abstraction surface over the SQL-backed orchestration data owned by the host application's `DbContext`.

The durable Entity Framework implementation is intended to live in `Infrastructure.EntityFramework/Orchestration`, while the core orchestration authoring/runtime contracts remain in `Application.Orchestration`.

### Entity Framework DbContext Integration Contract

The Entity Framework provider shall follow the same composition model already used by existing EF-backed devkit features such as messaging, queueing, notifications, file storage and logging.

This means:

* a project-specific application `DbContext` may implement multiple feature capability interfaces at once
* orchestration persistence must be able to live in that same single application `DbContext`
* the orchestration feature must not force consumers to introduce a second technical `DbContext` only for orchestration persistence

The orchestration EF provider shall therefore use a marker/capability interface pattern similar to:

```csharp
public interface IOrchestrationContext
{
    DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }
    DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }
    DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }
    DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }
}
```

And the consuming application may compose it into its own context together with other feature contracts:

```csharp
public class AppDbContext : DbContext,
    IMessagingContext,
    IQueueingContext,
    IOrchestrationContext
{
    public DbSet<BrokerMessage> BrokerMessages { get; set; }
    public DbSet<QueueMessage> QueueMessages { get; set; }
    public DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }
    public DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }
    public DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }
    public DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }
}
```

Registration and implementation patterns should follow the existing feature style, for example:

* EF orchestration services use generic registration constrained as `where TContext : DbContext, IOrchestrationContext`
* the provider reads and writes orchestration rows through the application's own `DbContext`
* the provider controls its own `DbContext` scope by leveraging the DI `IServiceProvider`
* for normal runtime operation, the provider should resolve scoped `TContext` instances from `IServiceProvider` rather than depending on a caller-owned `DbContext` lifetime
* migrations and schema evolution are owned by the consuming application's `DbContext` and migration workflow

This keeps orchestration persistence aligned with the devkit's established EF integration model and enables a single application database context to host both domain data and selected system-feature data.

### Entity Framework Model Conventions

The EF-first orchestration persistence types shall follow the same style as existing EF-backed system types such as `BrokerMessage`.

The conventions are:

* persistence model type names must not use the suffix `Entity`
* the EF persistence types should define their table, key, index, column, length, required and concurrency configuration directly through annotations on the type and its properties
* the goal is that each persistence type fully describes its own EF Core mapping in the type definition
* serializer selection is infrastructure configuration, not type-level mapping

The annotation-based model should include the relevant EF Core attributes where needed, for example:

* `[Table(...)]`
* `[Key]`
* `[Index(...)]`
* `[Required]`
* `[MaxLength(...)]`
* `[Column(...)]`
* `[ConcurrencyCheck]`
* `[NotMapped]`

Provider-specific persistence types such as `OrchestrationInstance`, `OrchestrationHistory`, `OrchestrationSignal` and `OrchestrationTimer` should therefore be self-describing EF models rather than relying on separate fluent configuration as the primary mapping contract.

### Entity Framework Registration API

The EF integration should expose a public registration API that matches the provider model and the runtime/query service model.

The registration API shall follow the existing devkit builder style:

```csharp
services.AddOrchestrations()
    .WithOrchestration<OrderApprovalOrchestration>()
    .WithOrchestration<TelephoneCallOrchestration>()
    .WithEntityFramework<AppDbContext>(options => options
        .UseSerializer(new SystemTextJsonSerializer()))
    .AddEndpoints(options => options
        .Prefix("/api/_system/orchestrations"));
```

The endpoint implementation behind this registration model is intended to live in `Presentation.Web.Orchestration`.

The public registration contract shall support:

* registering orchestration definitions
* registering the EF persistence provider against `TContext`
* registering runtime/background services needed for dispatch, signal consumption, timer consumption and lease-driven execution
* registering query services, including metrics queries
* registering optional administration/dashboard endpoints
* allowing an explicit `ISerializer` override while defaulting to `SystemTextJsonSerializer`

The EF registration contract should use generic constraints in the existing feature style:

* `where TContext : DbContext, IOrchestrationContext`

The registration API should not require consumers to manually wire low-level stores one by one for the common EF-backed scenario.

It should provide a single high-level registration entry point for the standard orchestration runtime plus the EF persistence provider.

Schema selection is not part of the orchestration EF registration API.

Schema/table mapping is owned by the application's `DbContext` model and the orchestration persistence types themselves.

---

## Observability & Management

* **Auditing**

  * All state transitions and activity executions are logged.
  * All state transitions and activity executions are also represented in durable execution history.

* **Monitoring**

  * Orchestration instances can be inspected at runtime.
  * Monitoring must be based on persisted orchestration data rather than on worker-local memory.

* **Administration API (endpoints)**

  * REST API for:

    * Querying orchestration instances
    * Inspecting state
    * Triggering/Signaling transitions
    * Cancelling orchestrations
    * Pausing and resuming orchestrations
    * Inspecting transition history and context snapshots
    * Archiving retained orchestration records when supported by the provider model
    * Purging retained orchestration data
    * Maintenance and repair actions for stuck or operationally impaired orchestrations

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

### Testing Utilities

The feature shall provide workflow-focused test utilities so orchestration authors can test definitions and runtime behavior with low friction.

The testing utility contract shall support at least:

* creating and running orchestration instances in tests
* supplying initial orchestration data/context easily
* executing orchestrations inline for deterministic tests
* dispatching orchestrations in a test runtime when background-style behavior is needed
* sending signals to test instances
* advancing or controlling time for timer and waiting scenarios
* asserting current status, current state, current context snapshot and execution history
* asserting retries, waits, transitions, terminal outcomes and compensation behavior
* substituting fake or in-memory persistence providers where suitable
* substituting fake lease/timer/signal infrastructure where suitable

The testing surface should make common orchestration tests easy without requiring full application hosting or real infrastructure unless the test explicitly needs integration coverage.

The intended testing surface should provide a small orchestration test harness around the normal runtime contract, for example:

* a test builder or fixture for creating an orchestration test runtime
* in-memory persistence suitable for unit tests
* deterministic inline execution helpers
* a controllable test clock for timers and waits
* helper methods for sending signals, loading instance state, and asserting execution history

The test harness should make tests readable in terms of workflow behavior rather than infrastructure setup.

Typical unit-test usage should support a shape like:

```csharp
var harness = OrchestrationTestHarness.Create()
    .WithOrchestration<OrderApprovalOrchestration>()
    .Build();

var dispatch = await harness.DispatchAsync(
    new OrderApprovalData
    {
        OrderId = "ORD-1001",
        CustomerId = "CUST-42",
        OrderAmount = 2500m
    });

var instanceId = dispatch.Value;

await harness.SignalAsync(
    instanceId,
    "OrderApproved",
    new OrderApprovedSignal
    {
        ApprovedBy = "manager-1"
    });

var instance = await harness.GetAsync(instanceId);

instance.Value.CurrentState.ShouldBe("Confirmed");
instance.Value.Status.ShouldBe("Completed");
```

The feature should also make it easy to test workflows without a full background runtime.

For example, a simple inline unit test should support a shape like:

```csharp
public sealed class OrderApprovalOrchestrationTests
{
    [Fact]
    public async Task Execute_should_wait_for_approval_and_complete_after_signal()
    {
        var harness = OrchestrationTestHarness.Create()
            .WithOrchestration<OrderApprovalOrchestration>()
            .Build();

        var dispatch = await harness.DispatchAsync(
            new OrderApprovalData
            {
                OrderId = "ORD-1001",
                CustomerId = "CUST-42",
                OrderAmount = 2500m
            });

        dispatch.IsSuccess.ShouldBeTrue();

        var instanceId = dispatch.Value;

        var waiting = await harness.GetAsync(instanceId);
        waiting.Value.CurrentState.ShouldBe("AwaitingApproval");
        waiting.Value.Status.ShouldBe("Waiting");

        await harness.SignalAsync(
            instanceId,
            "OrderApproved",
            new OrderApprovedSignal
            {
                ApprovedBy = "manager-1"
            });

        var completed = await harness.GetAsync(instanceId);
        completed.Value.CurrentState.ShouldBe("Confirmed");
        completed.Value.Status.ShouldBe("Completed");

        var history = await harness.GetHistoryAsync(instanceId);
        history.Value.ShouldContain(e => e.EventType == "SignalProcessed");
        history.Value.ShouldContain(e => e.EventType == "Transition");
    }
}
```

This example is illustrative of the intended test-authoring experience. The normative requirement is that the orchestration feature provide enough test utilities to make state assertions, signal progression, history assertions and timer/wait testing straightforward in ordinary unit tests.

---

## Execution Lifecycle

An orchestration instance progresses through a well-defined lifecycle from creation to completion. Execution is state-driven and activity-outcome-driven.

### Lifecycle Phases

* **Created**

  * A new orchestration instance is initialized.
  * The initial instance snapshot is persisted before any business activity executes.

* **Running**

  * The orchestration actively executes activities.
  * Exactly one worker may hold the execution lease for the instance while it is advancing.

* **Waiting**

  * Execution is paused as part of normal orchestration behavior.
  * process is intentionally waiting for orchestration input/time/event.
  * The waiting reason, waiting timestamp and any registered timers or expected signals are persisted durably.

* **Paused**

  * Execution is suspended by an external action.
  * operator/admin temporarily suspends execution regardless of orchestration logic.

While in Paused state:

* No activities are executed
* No time-based triggers are processed
* Signals are accepted but do not advance execution until resumed
* The pause reason and pause metadata are persisted

Resuming transitions the orchestration back to its previous logical state (typically Running or Waiting).

* **Resuming**

  * Execution continues from a previously waiting or paused state.
  * Resume is itself a persisted lifecycle event.

* **Completed**

  * The orchestration has ended successfully.
  * This occurs either implicitly when execution reaches the natural end, or explicitly via a `Complete` outcome.

* **Failed**

  * The orchestration encountered a non-recoverable error.
  * The failure reason and failing execution point are persisted.

* **Terminated**

  * The orchestration was ended explicitly via a `Terminate` outcome.
  * controlled hard stop from orchestration logic/admin.

* **Cancelled**

  * The orchestration was stopped externally or via a `Cancel` outcome.
  * Cancellation is persisted before cancellation handlers or cleanup activities begin.

---

### Activity Outcomes

Each orchestration activity produces an outcome that controls progression.

If no explicit outcome is returned, the default outcome is **Continue**.

Supported outcomes:

* **Continue**

  * Proceed with the next activity or progression.
  * After the activity completes, the updated full context snapshot is persisted before the next activity or transition is evaluated.

* **Retry**

  * Re-execute the current activity according to retry policy.
  * Retry intent and retry scheduling metadata are persisted before the next attempt becomes eligible.

* **Break**

  * End the current loop or branch.
  * Does not terminate the orchestration itself.

* **Wait**

  * Pause execution and move into the Waiting phase.
  * Waiting metadata and the latest full context snapshot are persisted before the execution lease is released.

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
    * a declarative signal wait or timer wait

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

Waiting may target:

* one or more named signals
* one or more timers or timeouts
* a human-interaction completion event
* a declarative predicate satisfied by a future signal payload

When entering Waiting, the runtime shall persist:

* the waiting kind
* the current state and full context snapshot
* eligible signal names or timer metadata
* optional timeout metadata
* the UTC timestamp when waiting began

---

### State Transitions

Transitions are explicitly defined.

They are triggered by:

* activity outcomes
* conditions
* signals
* time-based events

Activity outcomes control execution behavior.

State transitions control business progression.

The transition contract is:

* transitions are evaluated in declaration order unless an explicit priority model is introduced later
* at most one transition may be taken for a single transition-evaluation point
* the selected transition must be persisted as a dedicated transition history record
* after a transition is selected, the target state's entry is persisted before target-state activities begin
* if multiple transitions would match at the same evaluation point, the first matching transition wins
* if no transition matches and the state is not terminal and not waiting, the instance fails with a configuration/execution error
* transition evaluation and transition application execute under the orchestration-instance lease

Terminal behavior should be explicit.

A state should end in one of these ways:

* transition to another state
* enter Waiting
* emit a terminal outcome (`Complete`, `Cancel`, `Terminate`)
* end a local loop/branch and continue within its containing construct

### Signal Processing Contract

Signals are first-class, durable inputs to an orchestration instance.

The runtime shall process signals using the following rules:

* every received signal is persisted before it is processed
* signals are correlated to a specific orchestration instance
* signals may carry typed payload data
* when a signal handler or signal wait accesses a typed payload, the payload type shall be declared explicitly at the authoring point, for example through `WaitForSignal<TPayload>(...)` or `WhenSignal<TPayload>(...)`
* when no payload is needed by the workflow definition, the non-generic signal form should be used to avoid unnecessary signal payload types like `WaitForSignal(...)` or `WhenSignal(...)`
* signal payload-to-context mapping happens under the instance lease and is persisted as part of the next context snapshot
* when the instance is `Paused`, signals may be accepted and persisted but may not advance execution until resumed
* signals are always evaluated against the latest persisted current state of the orchestration instance, not against the state the sender expected
* when the current state has no matching signal handler, the signal is rejected or recorded as ignored at that point in time
* if the workflow has already advanced and the current state no longer matches the signal's intended wait or handler, the signal is rejected or recorded as ignored at that point in time
* an unmatched signal is not buffered for later consumption by a future state
* signal processing is idempotent by signal record identity and optional caller-supplied idempotency key
* the runtime shall prevent concurrent re-processing of the same persisted signal record
* when two separate signal submissions represent the same logical business signal, duplicate prevention is guaranteed only when a persisted signal identity match, an idempotency key match, or current-state rejection prevents repeated handling

### Signal Authoring Contract

Signal-driven workflow behavior shall be explicit in the authoring model.

The signal authoring model should distinguish between:

* `WhenSignal(...)`
  * Defines a signal-driven transition or signal-driven behavior within the current state.
  * Used when the current state reacts immediately to a received signal.
  * May perform inline signal-driven work, map signal payload data into context, and then transition or otherwise continue according to the configured signal behavior.
* `WaitForSignal(...)`
  * Defines an explicit waiting condition for one or more signals.
  * Used when the orchestration should enter or remain in a waiting condition until a matching signal arrives.
  * May map signal payload data into context and then release the wait through a transition or other configured continuation.

The authoring contract is:

* `WhenSignal(...)` is state-reaction oriented
* `WaitForSignal(...)` is waiting-condition oriented
* both forms participate in the same durable signal-processing rules
* both forms may be non-generic when the workflow definition does not consume signal payload data
* both forms shall require explicit payload typing when the workflow definition consumes typed payload data
* both forms may map payload data into orchestration context before transition evaluation or continuation
* both forms execute under the orchestration-instance lease when processing the matching signal

The intended authoring shape is therefore:

```csharp
.WhenSignal("PlacedOnHold", signal => signal
    .Activity((context, cancellationToken) =>
    {
        context.Data.IsOnHold = true;
        return Task.FromResult(OrchestrationOutcome.Continue());
    })
    .TransitionTo("OnHold"))
```

and:

```csharp
.WaitForSignal<OrderApprovedSignal>("OrderApproved", signal => signal
    .MapToContext((context, payload) =>
    {
        context.Data.ApprovalUserId = payload.ApprovedBy;
    })
    .TransitionTo("PaymentReservation"))
```

### Timer Processing Contract

Timers and timeouts are durable triggers, not in-memory delays.

The runtime shall process timers using the following rules:

* every scheduled timer is persisted with due UTC timestamp and trigger metadata
* timer due times are evaluated in UTC
* when a timer becomes due, the timer is consumed under the orchestration instance lease
* timer consumption is recorded in execution history
* a timer may cause a transition, release a waiting condition, or schedule a retry attempt
* paused orchestrations do not advance on due timers until resumed, but due timers remain persisted and observable
* if a timer becomes overdue while the orchestration is `Paused`, paused time does not extend that timer
* when the orchestration resumes, an overdue timer that is still relevant shall fire immediately on resume under the orchestration-instance lease
* if multiple timers are due for the same orchestration instance at the same evaluation point, they are evaluated in deterministic order
* once a waiting condition or state transition makes a persisted timer no longer relevant, that timer is marked as consumed, cancelled, or obsolete and may not fire later
* timer registration, timer consumption and timer-driven transition application all execute under the orchestration-instance lease

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
  * Current business state name
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
* Activity execution is always context-centered.
  * Custom code activities may read and mutate context directly.
  * Reusable built-in or typed activities may offer configuration hooks, but those hooks still operate against the shared orchestration context rather than separate activity input/output objects.
  * Activity results are not modeled as separate durable channels; any data needed later must be written into the orchestration context and is then persisted with the next snapshot.
* The latest full orchestration context snapshot is persisted durably as execution moves forward.
* Context persistence is snapshot-based.
  * The runtime persists the latest full serializable context state at each durable boundary rather than relying only on event replay.
* Context types should remain serialization-friendly and deterministic.
  * They should not hold live service references, unmanaged resources, or non-durable ambient state.

### Context Persistence Rules

The orchestration context is part of the durable runtime contract.

The runtime shall:

* serialize and persist the full context snapshot on instance creation
* serialize and persist the full context snapshot after each completed activity attempt
* serialize and persist the full context snapshot after signal payload mapping
* serialize and persist the full context snapshot after each state transition
* serialize and persist the final full context snapshot on terminal completion

The runtime may additionally persist intermediate snapshots for diagnostics or recovery, but the checkpoints above are the minimum required contract.

---

## Execution Semantics

* **Deterministic Behavior**

  * Execution is driven by persisted state, persisted context snapshots and explicit activity outcomes.
  * The next execution decision must be derivable from durable state.

Activities may interact with external systems; therefore idempotency is required to ensure safe re-execution.

* **Idempotency Expectation**

  * Activities should tolerate re-execution.
  * Signal handling should also be idempotent.

* **Pause and Resume**

  * Orchestrations may wait as part of normal execution or be paused externally.
  * Resumption continues from the persisted point.

* **Programmatic API**

  * Orchestrations can be started directly from application code.
  * Supports passing initial context.
  * Returns the orchestration instance identifier for tracking.
  * Supports awaiting completion or specific outcomes.

### Runtime Service API

The feature shall expose a clear application-facing service API for orchestration execution and control.

Client-facing orchestration service methods shall follow the devkit Result pattern as described in `features-results.md`.

Public orchestration runtime and query methods shall therefore return `Result`, `Result<T>` or `ResultPaged<T>` so that callers can inspect success, failure, messages and errors explicitly instead of inferring business/runtime failure from exceptions or ad-hoc status handling. More details here: [Results Feature](../features-results.md)

This requirement applies to the application-facing orchestration runtime and query services. It does not require internal runtime components, persistence providers, execution helpers or other non-client-facing implementation methods to use the Result pattern internally.

The public service surface should distinguish between:

* **runtime/control operations**
  * start, signal, pause, resume, cancel, terminate and wait
* **query operations**
  * load current status, current context, history, signals, timers, filtered lists and aggregated metrics

The runtime and query service shape shall be:

```csharp
public interface IOrchestrationService
{
    Task<Result<OrchestrationExecuteResult>> ExecuteAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> DispatchAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default);

    Task<Result<OrchestrationWaitResult>> DispatchAndWaitAsync<TOrchestration, TData>(
        TData data,
        OrchestrationWaitFor waitFor = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<Result> SignalAsync(
        Guid instanceId,
        string signalName,
        object payload = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<Result> PauseAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> ResumeAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> TerminateAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);
}

public interface IOrchestrationQueryService
{
    Task<Result<OrchestrationInstanceModel>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<Result<OrchestrationContextSnapshotModel>> GetContextAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<ResultPaged<OrchestrationInstanceModel>> QueryAsync(OrchestrationQueryRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrchestrationHistoryModel>>> GetHistoryAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrchestrationSignalModel>>> GetSignalsAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrchestrationTimerModel>>> GetTimersAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<Result<OrchestrationMetricsModel>> GetMetricsAsync(OrchestrationMetricsRequest request = null, CancellationToken cancellationToken = default);
}
```

The following request and value models are part of the contract. They are returned inside the appropriate Result wrapper on the public client-facing API.

```csharp
public sealed class OrchestrationQueryRequest
{
    public string OrchestrationName { get; set; }
    public IReadOnlyList<string> Statuses { get; set; }
    public IReadOnlyList<string> States { get; set; }
    public string CorrelationId { get; set; }
    public string ConcurrencyKey { get; set; }
    public DateTimeOffset? StartedFrom { get; set; }
    public DateTimeOffset? StartedTo { get; set; }
    public DateTimeOffset? CompletedFrom { get; set; }
    public DateTimeOffset? CompletedTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string SortBy { get; set; } = "StartedUtc";
    public bool SortDescending { get; set; } = true;
}

public sealed class OrchestrationWaitFor
{
    public bool Completion { get; set; }
    public IReadOnlyList<string> States { get; set; }
    public IReadOnlyList<string> Outcomes { get; set; }
}

public sealed class OrchestrationExecuteResult
{
    public Guid InstanceId { get; set; }
    public string Status { get; set; }
    public string CurrentState { get; set; }
    public string Outcome { get; set; }
    public string CorrelationId { get; set; }
    public string ContextJson { get; set; }
}

public sealed class OrchestrationWaitResult
{
    public Guid InstanceId { get; set; }
    public string Status { get; set; }
    public string CurrentState { get; set; }
    public string Outcome { get; set; }
    public bool TimedOut { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }
}

public sealed class OrchestrationMetricsRequest
{
    public string OrchestrationName { get; set; }
    public IReadOnlyList<string> Statuses { get; set; }
    public IReadOnlyList<string> States { get; set; }
    public DateTimeOffset? StartedFrom { get; set; }
    public DateTimeOffset? StartedTo { get; set; }
    public DateTimeOffset? CompletedFrom { get; set; }
    public DateTimeOffset? CompletedTo { get; set; }
}

public sealed class OrchestrationInstanceModel
{
    public Guid InstanceId { get; set; }
    public string OrchestrationName { get; set; }
    public string Status { get; set; }
    public string CurrentState { get; set; }
    public string CurrentActivity { get; set; }
    public string CorrelationId { get; set; }
    public string ConcurrencyKey { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public string UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public DateTimeOffset LastUpdatedUtc { get; set; }
}

public sealed class OrchestrationContextSnapshotModel
{
    public Guid InstanceId { get; set; }
    public string OrchestrationName { get; set; }
    public string Status { get; set; }
    public string CurrentState { get; set; }
    public DateTimeOffset SnapshotUtc { get; set; }
    public string ContextType { get; set; }
    public string ContextJson { get; set; }
}

public sealed class OrchestrationHistoryModel
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string EventType { get; set; }
    public string State { get; set; }
    public string Activity { get; set; }
    public string Message { get; set; }
    public string DataJson { get; set; }
}

public sealed class OrchestrationSignalModel
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string SignalName { get; set; }
    public string ProcessingStatus { get; set; }
    public string IdempotencyKey { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public string UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }
    public DateTimeOffset ReceivedUtc { get; set; }
    public DateTimeOffset? ProcessedUtc { get; set; }
    public string PayloadJson { get; set; }
}

public sealed class OrchestrationTimerModel
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string TimerKind { get; set; }
    public string ProcessingStatus { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public string UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }
    public DateTimeOffset DueUtc { get; set; }
    public DateTimeOffset? ProcessedUtc { get; set; }
    public string MetadataJson { get; set; }
}

public sealed class OrchestrationMetricsModel
{
    public long TotalCount { get; set; }
    public long RunningCount { get; set; }
    public long WaitingCount { get; set; }
    public long PausedCount { get; set; }
    public long CompletedCount { get; set; }
    public long FailedCount { get; set; }
    public long CancelledCount { get; set; }
    public long TerminatedCount { get; set; }
    public double? AverageDurationSeconds { get; set; }
    public DateTimeOffset? OldestWaitingStartedUtc { get; set; }
    public IReadOnlyDictionary<string, long> CountsByOrchestration { get; set; }
    public IReadOnlyDictionary<string, long> CountsByState { get; set; }
}
```

### Runtime Service Operation Contract

The runtime/control service shall support at least:

* inline execution for orchestrations that complete without waiting
* background dispatch for long-running orchestrations
* dispatch plus await for completion, states, or outcomes
* signal delivery with optional typed payload and idempotency key
* pause, resume, cancel and terminate operations
* querying current status after any control operation

Client-facing runtime/control operations shall report business/runtime failure through Result failures rather than requiring callers to infer failure from exceptions or partial state.

Examples include:

* missing orchestration instances
* invalid lifecycle operations such as resume on a non-paused instance
* duplicate or rejected starts due to orchestration execution constraints
* rejected or ignored signals
* inline execute requests that reach a waiting or paused condition and therefore cannot complete inline
* wait requests that cannot satisfy the requested condition because the orchestration has already reached an incompatible terminal state

Inline execute requests that cannot complete inline shall use a distinct error contract, for example a dedicated `CannotCompleteInlineError`.

That error contract should make it clear that:

* the orchestration instance may already have been created and durably persisted
* the orchestration may currently be healthy in `Waiting` or another non-terminal persisted state
* the failure applies to inline completion semantics, not necessarily to orchestration execution as a whole

Unexpected technical faults may still surface as exceptions. The Result requirement applies to expected client-visible runtime and business outcomes.

The query service shall support at least:

* current instance summary
* current business state
* latest persisted context snapshot
* execution history
* signal inspection
* timer inspection
* filtered/paged orchestration lists
* overall instance counts by lifecycle status
* counts by orchestration definition
* waiting/paused/running counts
* completion/failure/cancellation/termination counts
* duration metrics
* retry, signal and timer activity metrics where available

Client-facing query operations shall also use Result wrappers so callers can distinguish successful reads, not-found conditions, invalid filters and provider/query failures through the standard Result contract.

### Endpoint API Contract

The feature shall expose optional administration endpoints built on top of the query service.

These endpoints may map service-layer `Result`, `Result<T>` and `ResultPaged<T>` values into HTTP responses using the devkit's established Result-to-HTTP mapping approach.

The endpoint surface shall support:

* instance list queries with paging, sorting and filtering
* instance detail queries
* context snapshot queries
* history, signal and timer detail queries
* aggregated metrics queries
* control actions such as signal, pause, resume, cancel and terminate
* maintenance actions such as archive, purge and repair operations when supported by the provider

The endpoint filter model shall support at least:

* date range
* lifecycle status
* orchestration definition name
* current business state
* correlation identifier
* concurrency key

The endpoint layer should be sufficient to power a rich dashboard without requiring any additional private persistence access path.

The endpoint design shall align with the existing messaging and queueing operational endpoints.

That means:

* successful read endpoints return `200 OK`
* successful control endpoints return `200 OK` with a descriptive success message or result payload
* missing orchestration instances return `404 Not Found` with a plain text message
* invalid request bodies or query parameters return `400 Bad Request` using `ProblemDetails`
* valid requests that conflict with the current orchestration state return `409 Conflict` using `ProblemDetails`
* unexpected failures return `500 Internal Server Error` using `ProblemDetails`

The endpoint contract shall expose routes shaped like:

```text
GET    /api/_system/orchestrations
GET    /api/_system/orchestrations/{instanceId}
GET    /api/_system/orchestrations/{instanceId}/context
GET    /api/_system/orchestrations/{instanceId}/history
GET    /api/_system/orchestrations/{instanceId}/signals
GET    /api/_system/orchestrations/{instanceId}/timers
GET    /api/_system/orchestrations/metrics
POST   /api/_system/orchestrations/{instanceId}/signal
POST   /api/_system/orchestrations/{instanceId}/pause
POST   /api/_system/orchestrations/{instanceId}/resume
POST   /api/_system/orchestrations/{instanceId}/cancel
POST   /api/_system/orchestrations/{instanceId}/terminate
POST   /api/_system/orchestrations/{instanceId}/archive
POST   /api/_system/orchestrations/{instanceId}/repair/release-lease
POST   /api/_system/orchestrations/{instanceId}/repair/requeue-timers
DELETE /api/_system/orchestrations
```

Query parameters for `GET /api/_system/orchestrations` shall support at least:

* `orchestrationName`
* `statuses`
* `states`
* `correlationId`
* `concurrencyKey`
* `startedFrom`
* `startedTo`
* `completedFrom`
* `completedTo`
* `skip`
* `take`
* `sortBy`
* `sortDescending`

Query parameters for `GET /api/_system/orchestrations/metrics` shall support the same filter subset that is meaningful for aggregated metrics.

The `POST /signal` request body shall support at least:

```csharp
public sealed class SignalRequest
{
    public string SignalName { get; set; }
    public string IdempotencyKey { get; set; } // prevent duplicate signals when needed
    public object Payload { get; set; }
}
```

The `POST /pause`, `POST /cancel` and `POST /terminate` request bodies shall support at least:

```csharp
public sealed class ReasonRequest
{
    public string Reason { get; set; }
}
```

The `DELETE /api/_system/orchestrations` endpoint shall support purge-style query parameters comparable to the retained operational endpoints used by queueing and messaging, for example:

* `olderThan`
* `statuses`
* `isArchived`

The endpoint response contract shall follow this pattern:

* `GET /api/_system/orchestrations`
  * `200 OK` with `ResultPaged<OrchestrationInstanceModel>`
  * `500 Internal Server Error` with `ProblemDetails`
* `GET /api/_system/orchestrations/{instanceId}`
  * `200 OK` with `OrchestrationInstanceModel`
  * `404 Not Found` with plain text message
  * `500 Internal Server Error` with `ProblemDetails`
* `GET /api/_system/orchestrations/{instanceId}/context`
  * `200 OK` with `OrchestrationContextSnapshotModel`
  * `404 Not Found` with plain text message
  * `500 Internal Server Error` with `ProblemDetails`
* `GET /api/_system/orchestrations/{instanceId}/history`
  * `200 OK` with `IEnumerable<OrchestrationHistoryModel>`
  * `404 Not Found` with plain text message when the orchestration instance does not exist
  * `500 Internal Server Error` with `ProblemDetails`
* `GET /api/_system/orchestrations/{instanceId}/signals`
  * `200 OK` with `IEnumerable<OrchestrationSignalModel>`
  * `404 Not Found` with plain text message when the orchestration instance does not exist
  * `500 Internal Server Error` with `ProblemDetails`
* `GET /api/_system/orchestrations/{instanceId}/timers`
  * `200 OK` with `IEnumerable<OrchestrationTimerModel>`
  * `404 Not Found` with plain text message when the orchestration instance does not exist
  * `500 Internal Server Error` with `ProblemDetails`
* `GET /api/_system/orchestrations/metrics`
  * `200 OK` with `OrchestrationMetricsModel`
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/signal`
  * `200 OK` with success message or accepted signal result
  * `404 Not Found` with plain text message
  * `400 Bad Request` with `ProblemDetails` when the request body is invalid
  * `409 Conflict` with `ProblemDetails` when the orchestration exists but cannot accept the signal in its current state
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/pause`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the orchestration is already terminal or already paused
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/resume`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the orchestration is not in a resumable state
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/cancel`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the orchestration is already terminal
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/terminate`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the orchestration is already terminal
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/archive`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the orchestration is not archivable in its current state
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/repair/release-lease`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the repair action is not valid in the current state
  * `500 Internal Server Error` with `ProblemDetails`
* `POST /api/_system/orchestrations/{instanceId}/repair/requeue-timers`
  * `200 OK` with success message
  * `404 Not Found` with plain text message
  * `409 Conflict` with `ProblemDetails` when the repair action is not valid in the current state
  * `500 Internal Server Error` with `ProblemDetails`
* `DELETE /api/_system/orchestrations`
  * `200 OK` with success message
  * `500 Internal Server Error` with `ProblemDetails`

The `ProblemDetails` contract shall use stable orchestration-specific `type` values, for example:

* `/problems/orchestrations/validation`
* `/problems/orchestrations/not-found`
* `/problems/orchestrations/invalid-state`
* `/problems/orchestrations/concurrency-conflict`
* `/problems/orchestrations/unsupported-operation`

### Authoring Contract

The authoring model shall be explicit and strongly structured.

At minimum:

* each orchestration definition has exactly one logical initial state
* state names are unique within an orchestration definition
* signal names are explicit and case-sensitivity behavior must be consistent within the feature
* transitions are declared explicitly and evaluated predictably
* terminal states use explicit terminal directives rather than implicit fall-through
* state activities are sequential by default
* parallelism, looping, waiting and compensation are opt-in constructs and must be visible in the definition

Authoring rules:

* a state may define:
  * activities
  * transitions
  * signal handlers
  * timer/timeout handlers
  * explicit terminal behavior
* a state without activities may still be valid if it is purely signal-driven, timer-driven, or terminal
* a non-terminal state must have at least one valid way to progress:
  * another transition
  * a signal wait
  * a timer wait
  * a loop/branch continuation
* definitions that cannot progress deterministically should fail validation at startup or registration time

### Activity Model

Activities remain the primary execution unit inside orchestration states.

The activity model shall support three authoring styles:

* **Custom code activities**
  * Implement orchestration-specific logic directly against the orchestration context.
  * Remain the escape hatch for arbitrary integration and business logic.
* **Reusable activities**
  * Provide a structured contract for well-known or product-specific activity categories such as HTTP calls, queries, logging, notifications or starting child orchestrations.
  * May define configuration for context access, retry, timeout, compensation and behavior-specific options.
* **Inline activities**
  * Allow short orchestration-local logic blocks when defining the state machine.

All activity styles shall remain context-centered.

The common contract is:

* the activity reads the data it needs from the current orchestration context
* the activity may call configured code during execution to mutate the orchestration context
* any data produced by the activity that must survive later workflow steps shall be written into the orchestration context
* that context mutation becomes durable through the normal context snapshot persistence rules

Signal-driven authoring shall also be explicit about payload typing.

If a signal handler or signal wait accesses a typed payload, the payload type shall be declared explicitly in the workflow definition rather than inferred implicitly from a callback parameter.

If the workflow definition does not consume signal payload data, the non-generic signal form should be preferred.

For example, the intended signal authoring shape is:

```csharp
.WaitForSignal<OrderApprovedSignal>("OrderApproved", signal => signal
    .MapToContext((context, payload) =>
    {
        context.Data.ApprovalUserId = payload.ApprovedBy;
    })
    .TransitionTo("PaymentReservation"))
```

This keeps signal names and signal payload contracts clear and type-safe at the point of orchestration definition.

### Built-In Activity Catalog

The feature should provide a built-in set of ready-to-use activities for recurring technical and workflow scenarios.

The built-in catalog should include at least:

* `LogActivity`
  * Writes structured log entries based on the current orchestration context.
* `HttpActivity`
  * Executes outbound HTTP requests and may update context from the response.
* `QueryActivity`
  * Executes a query/read operation and may write the returned data into context.
* `CommandActivity`
  * Executes an application command/action and may update context based on the command result.
* `WaitActivity`
  * Represents a declarative wait, delay, timeout or scheduled resume point.
* `StartOrchestrationActivity`
  * Starts another orchestration and may optionally wait for its completion, state or outcome.

The catalog may additionally include activities such as:

* `SignalActivity`
  * Sends a signal to another orchestration instance and may record signal-related state in the orchestration context.
* `ApprovalActivity`
  * Represents a specialized business approval step that records approval state in workflow context and waits for approve or reject resolution.
* `HumanTaskActivity`
  * Represents a broader manual interaction step that records task state in workflow context and waits for user-driven completion or resolution.
* `DecisionActivity`
  * Evaluates decision logic against the current orchestration context and may update context or choose the next orchestration outcome.
* `TransformActivity`
  * Performs a pure context-to-context data transformation step used to derive, normalize or enrich orchestration data without requiring external integration.

`StartOrchestrationActivity` should be treated as a first-class built-in activity type.

Its contract should support at least:

* starting another orchestration using data read from the parent context
* optionally storing the child orchestration instance identifier or other returned child reference data back into the parent context
* optionally waiting for child completion, child states, or child outcomes
* persisting the parent wait condition durably when the parent orchestration is configured to wait for the child

`ApprovalActivity` should represent a specialized business approval step.

Its contract should support at least:

* recording approval metadata and current approval state in the orchestration context
* moving the orchestration into a waiting state until an approval-related decision or signal is received
* resuming with an approval or rejection outcome that can be reflected in the orchestration context
* persisting approval resolution metadata such as resolution timestamp, resolver identity when available, and resolution comment or reason when provided
* supporting explicit rejection and optional expiration through normal timer/waiting composition

It does not require a separate orchestration-specific durable approval table or approval persistence root.

The approval resolution contract is:

* approval or rejection is returned to the orchestration through a durable runtime interaction
* the baseline progression mechanism shall be a mapped orchestration signal, even if higher-level helpers or product-specific APIs wrap that interaction
* when the resolution is received, the orchestration context is updated with the decision and related metadata before the next transition is evaluated
* resolution handling follows the same lease-protected, persisted execution rules as other signal or wait-release mechanisms
* approval-related UI or worklist behavior may be implemented from orchestration context and history data or integrated with an application-specific task/approval subsystem

For example, the authoring model should support:

```csharp
state.ApprovalActivity(activity => activity
    .Title(context => $"Approve order {context.Data.OrderId}")
    .AssignedToRole("SalesManager")
    .ApprovedSignal("OrderApproved")
    .RejectedSignal("OrderRejected")
    .OnApproved((context, decision) =>
    {
        context.Data.Approved = true;
        context.Data.ApprovalDecision = "Approved";
        context.Data.ApprovedBy = decision.UserId;
    })
    .OnRejected((context, decision) =>
    {
        context.Data.Approved = false;
        context.Data.ApprovalDecision = "Rejected";
        context.Data.RejectionReason = decision.Reason;
        context.Data.RejectedBy = decision.UserId;
    }));
```

and progression should work through the ordinary orchestration signal surface, for example:

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "OrderApproved",
    new ApprovalDecisionSignal
    {
        UserId = currentUser.UserId,
        Comment = "Approved for preferred customer."
    },
    cancellationToken: cancellationToken);
```

`HumanTaskActivity` should represent a broader manual interaction step.

Its contract should support at least:

* recording task metadata and current task state in the orchestration context
* moving the orchestration into a waiting state until the task is completed, cancelled, expired or otherwise resolved
* allowing non-approval manual interactions such as review, confirmation, correction, enrichment or document-related tasks
* persisting task resolution metadata such as resolution timestamp, resolver identity when available, task outcome and optional completion notes

It does not require a separate orchestration-specific durable human-task table or human-task persistence root.

The human-task resolution contract is:

* task completion, cancellation, expiration or other resolution is returned to the orchestration through a durable runtime interaction
* the baseline progression mechanism shall be a mapped orchestration signal, even if higher-level helpers or product-specific APIs wrap that interaction
* when the resolution is received, the orchestration context is updated with the task outcome and related metadata before the next transition is evaluated
* expiration and escalation may be implemented through composition with timers, signals and ordinary orchestration logic rather than requiring a separate execution model inside the activity
* task-related UI or worklist behavior may be implemented from orchestration context and history data or integrated with an application-specific task subsystem

For example, the authoring model should support:

```csharp
state.HumanTaskActivity(activity => activity
    .Title(context => $"Review customer data for order {context.Data.OrderId}")
    .Description("Please verify and correct missing customer information.")
    .AssignedToRole("Backoffice")
    .CompletedSignal("CustomerReviewCompleted")
    .CancelledSignal("CustomerReviewCancelled")
    .OnCompleted((context, task) =>
    {
        context.Data.CustomerReviewCompleted = true;
        context.Data.CustomerReviewComment = task.Comment;
        context.Data.CustomerReviewedBy = task.UserId;
    })
    .OnCancelled((context, task) =>
    {
        context.Data.CustomerReviewCompleted = false;
        context.Data.CustomerReviewComment = task.Comment;
    }));
```

and progression should work through the ordinary orchestration signal surface, for example:

```csharp
await orchestrations.SignalAsync(
    instanceId,
    "CustomerReviewCompleted",
    new HumanTaskResolutionSignal
    {
        UserId = currentUser.UserId,
        Comment = "Customer data corrected and verified."
    },
    cancellationToken: cancellationToken);
```

`TransformActivity` should be pure by contract.

Its contract should support at least:

* reading orchestration context
* computing derived or normalized values
* writing those derived values back into orchestration context

It should not:

* call external systems
* depend on non-durable ambient state
* produce side effects outside the orchestration context

Built-in activities should support convenience methods in the orchestration DSL in addition to the generic activity registration form.

That means both of these styles should be valid:

```csharp
state.Activity<LogActivity>(activity => activity
    .Warning("Order {OrderId} requires manual approval"));
```

and:

```csharp
state.LogActivity(activity => activity
    .Warning("Order {OrderId} requires manual approval"));
```

Convenience methods are authoring sugar over the same runtime activity contract. They do not introduce a separate execution model.

The activity authoring model should support richer built-in activity configuration for recurring technical integrations.

`HttpActivity` should support configuration of request construction, headers, timeout, response handling and orchestration outcome selection while remaining fully context-centered.

Its normative capability contract is:

* selecting HTTP method
* constructing target URL from orchestration context
* configuring headers from orchestration context
* configuring request body from orchestration context when applicable
* configuring timeout behavior
* classifying acceptable and non-acceptable status codes
* handling successful responses
* handling non-success responses
* handling transport or serialization exceptions
* updating orchestration context from response data
* selecting the resulting orchestration outcome

The following code shape is illustrative of the intended authoring experience, not a requirement for the exact public method names:

```csharp
state.HttpActivity(activity => activity
    .Name("ReservePayment")
    .Post()
    .Url(context => $"payments/orders/{context.Data.OrderId}/reservations")
    .Header("Authorization", context => $"Bearer {context.Data.PaymentToken}")
    .Header("Accept", _ => "application/json")
    .Body(context => new
    {
        orderId = context.Data.OrderId,
        customerId = context.Data.CustomerId,
        amount = context.Data.OrderAmount,
        currency = context.Data.Currency
    })
    .Timeout(TimeSpan.FromSeconds(30))
    .AcceptStatus(HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Accepted)
    .OnBeforeSend((context, request) =>
    {
        context.Data.LastHttpRequestUtc = DateTimeOffset.UtcNow;
        context.Data.LastHttpOperation = "ReservePayment";
    })
    .OnSuccess((context, response) =>
    {
        context.Data.LastHttpStatusCode = (int)response.StatusCode;

        var body = response.GetBody<ReservePaymentResponse>();

        context.Data.PaymentReservationId = body.ReservationId;
        context.Data.PaymentStatus = body.Status;
        context.Data.PaymentReservedUtc = body.ReservedUtc;
    })
    .OnFailure((context, response) =>
    {
        context.Data.LastHttpStatusCode = (int)response.StatusCode;
        context.Data.LastError = $"Payment reservation failed with status {(int)response.StatusCode}";
    })
    .OnException((context, exception) =>
    {
        context.Data.LastError = exception.Message;
        context.Data.LastExceptionType = exception.GetType().Name;
    })
    .Outcome((context, response) =>
    {
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return OrchestrationOutcome.Wait(TimeSpan.FromMinutes(1));
        }

        if (response.IsSuccessStatusCode)
        {
            return OrchestrationOutcome.Continue();
        }

        if ((int)response.StatusCode >= 500)
        {
            return OrchestrationOutcome.Retry();
        }

        return OrchestrationOutcome.Terminate("Payment reservation was rejected.");
    }));
```

This example illustrates the intended pattern:

* request data is derived from the orchestration context
* headers and body are configured declaratively
* response handling writes durable business data back into the orchestration context
* orchestration flow control remains outcome-based
* no separate activity input/output channel is introduced

### Activity Extensibility Contract

The orchestration feature shall allow applications to define their own reusable activities for better integration into product-specific workflows and language.

The extensibility contract is:

* custom reusable activities use the same runtime execution contract as built-in activities
* custom reusable activities are resolved through dependency injection like other orchestration activities
* custom reusable activities execute against the same shared orchestration context model
* custom reusable activities may expose their own fluent configuration API for definition-time behavior
* custom reusable activities may apply retry, timeout, wait, compensation and outcome semantics through the normal orchestration activity contract
* custom reusable activities may be packaged by an application or module as reusable building blocks across multiple orchestrations

The implementation model should therefore be shaped so that:

* built-in activities are ordinary activity implementations provided by the framework
* framework-provided convenience methods such as `.LogActivity(...)` and `.HttpActivity(...)` are wrappers over the same underlying activity registration model as `.Activity<TActivity>(...)`
* applications may implement their own activity classes and register them without requiring special runtime treatment
* applications may add their own DSL extension methods that wrap `.Activity<TActivity>(...)` so orchestration definitions can use product language such as `.ReserveWarehouseSlotActivity(...)` or `.CreateInvoiceActivity(...)`

This ensures the activity system is open for product-specific integration without fragmenting the runtime model.

The authoring model should support product-defined DSL extensions shaped like:

```csharp
public static IOrchestrationStateBuilder<TData> ReserveWarehouseSlotActivity<TData>(
    this IOrchestrationStateBuilder<TData> state,
    Action<ReserveWarehouseSlotActivityOptions<TData>> configure = null)
{
    return state.Activity<ReserveWarehouseSlotActivity<TData>>(configure);
}
```

Applications should therefore be able to build both:

* custom reusable activity implementations
* custom convenience methods that expose those activities in product-specific orchestration language

### Definition Validation Contract

Orchestration definitions shall be validated at registration or startup time before they are allowed to run.

Validation shall fail for at least these cases:

* no initial state is defined
* more than one initial state is defined
* duplicate state names exist
* a transition targets a state that does not exist
* a timer or timeout references invalid or incomplete trigger metadata
* a terminal state also declares outgoing transitions, waits, or timer handlers
* a non-terminal state has no deterministic progression path
* two definitions in the same state create ambiguous unconditional handlers for the same signal or timer event without deterministic ordering

Validation may additionally warn or fail for:

* unreachable states
* duplicate handler names
* conflicting timeout declarations inside the same waiting scope
* loops or branch joins whose completion rules cannot be determined statically

### Loop and Parallel Semantics

Control-flow constructs remain part of the full orchestration model.

Their contracts are:

* **Loops**

  * Loop iteration state is persisted.
  * Breaking a loop exits only that loop scope.
  * Loop counters and loop-local state that affect execution must be durable.

* **Parallel branches**

  * Parallel branches are explicit branch scopes, not implicit concurrent execution of ordinary state activities.
  * Each branch has durable progress tracking.
  * Join behavior must be explicit, such as wait-for-all or wait-for-any.
  * Branch completion and join resolution are persisted as execution history.

The authoring DSL for parallel branches shall be explicit.

The parallel authoring shape shall support code like:

```csharp
builder.State("Work", state => state
    .Parallel(parallel => parallel
        .Branch("Inventory", branch => branch
            .Activity<ReserveInventoryActivity>())
        .Branch("Payment", branch => branch
            .Activity<ReservePaymentActivity>())
        .JoinAll()
        .TransitionTo("Reserved")));
```

Parallel contract details:

* each branch has a stable branch name
* each branch has its own durable progress state
* branch scopes may be logically active at the same time, but state-mutating branch execution remains governed by the single orchestration-instance lease
* at most one worker may persist branch or parent-state mutations for the orchestration instance at a time
* a worker advancing one branch must hold the orchestration-instance lease for the duration of that persisted branch action
* branch actions may be scheduled independently, but they are serialized at persistence-mutation boundaries by the orchestration-instance lease
* the parent instance may not advance beyond the join until the join condition is satisfied
* branch failure, retry, waiting and completion are recorded independently and then reflected in the parent orchestration history

This means the feature supports logical parallelism with durable branch state, while preserving single-instance mutation safety in multi-node environments.

### Compensation Semantics

Compensation is part of the orchestration authoring and runtime model.

The compensation authoring shape shall support code like:

```csharp
builder.State("Reserve", state => state
    .Activity<ReserveInventoryActivity>(activity => activity
        .CompensateWith<ReleaseInventoryActivity>())
    .Activity<ReservePaymentActivity>(activity => activity
        .CompensateWith<ReleasePaymentActivity>())
    .TransitionTo("Reserved"));
```

Compensation contract details:

* compensation is explicitly declared by the author
* a compensation entry is registered only after the forward activity completes successfully
* compensation entries are persisted in execution order and executed in reverse order unless configured otherwise
* compensation execution runs under the orchestration-instance lease
* compensation execution may itself succeed, fail, or retry
* compensation execution may not buffer unmatched signals for later states
* compensation execution is reflected through normal orchestration instance state and execution history
* if compensation fails irrecoverably, the default outcome is: continue attempting remaining compensation entries and then mark the orchestration `Failed`

Compensation failure policies shall be explicit and limited to a small declarative set:

* `ContinueRemainingThenFail`
  * continue executing remaining compensation entries and then mark the orchestration `Failed`
  * this is the default policy
* `StopImmediatelyAndFail`
  * stop compensation execution on the first irrecoverable compensation failure and mark the orchestration `Failed`
* `ContinueRemainingThenTerminate`
  * continue executing remaining compensation entries and then mark the orchestration `Terminated`

The authoring DSL shall support configuring the compensation failure policy explicitly, for example:

```csharp
builder.OnCompensation(options => options
    .FailurePolicy(CompensationFailurePolicy.ContinueRemainingThenFail));
```

### Starting Orchestrations

The orchestration feature supports three distinct mechanisms for starting execution, each suited to different runtime scenarios:

* **Execute**

  * Synchronous, inline execution within the current context.
  * Suitable for short-running orchestrations that need to execute immediately and return results directly to the caller.
  * Runs inline in the caller's execution flow without dispatching to the background runtime.
  * Returns a `Task<Result<OrchestrationExecuteResult>>` that can be awaited for completion and result retrieval.
  * Blocks the caller until execution completes.
  * Easy for testing and debugging due to synchronous nature.
  * Should be restricted to orchestrations that are expected to complete inline without entering a waiting/blocking state.
  * If execution reaches a Waiting or Paused condition during Execute, the call shall return a failed `Result<OrchestrationExecuteResult>` using the distinct inline-incompletion error contract.
  * Inline execution still persists the instance, state transitions and context snapshots according to the normal durability contract.

* **Dispatch**

  * Asynchronous, background execution via the orchestration runtime.
  * Suitable for long-running orchestrations that may involve waiting, human interaction
  * Returns a `Task<Result<Guid>>` immediately without waiting for completion
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
  * Returns a `Task<Result<OrchestrationWaitResult>>`.

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
  * Restrict execution to a single active instance per configured business concurrency key

---

## Typical Use Cases

* Order processing
* Approval orchestrations
* Data processing pipelines
* Long-running business transactions

---

## Example Orchestration: Order Approval Process

This example shows a long-running order approval process with validation, human approval, timeout handling, payment reservation and completion.

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

                .WaitForSignal<OrderApprovedSignal>("OrderApproved", signal => signal // signal-driven transition example
                    .MapToContext((ctx, payload) =>
                    {
                        ctx.Data.ApprovalUserId = payload.ApprovedBy;
                    })
                    .TransitionTo("PaymentReservation"))
                .WaitForSignal<OrderRejectedSignal>("OrderRejected", signal => signal
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
var signalResult = await orchestrations.SignalAsync(
    instanceId,
    "OrderApproved",
    new OrderApprovedSignal
    {
        ApprovedBy = currentUser.Id
    },
    cancellationToken: cancellationToken);

if (signalResult.IsFailure)
{
    // inspect signalResult.Errors / signalResult.Messages
}
```

### Execution Examples

```csharp
var dispatchResult = await orchestrations.DispatchAsync(
    new OrderApprovalData
    {
        OrderId = order.Id,
        CustomerId = order.CustomerId,
        OrderAmount = order.TotalAmount
    },
    cancellationToken);

if (dispatchResult.IsFailure)
{
    // inspect dispatchResult.Errors / dispatchResult.Messages
    return;
}

var instanceId = dispatchResult.Value;
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
    cancellationToken: cancellationToken);

if (result.IsSuccess && !result.Value.TimedOut)
{
    var finalState = result.Value.CurrentState;
}
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
var dispatchResult = await orchestrations.DispatchAsync(
    new TelephoneCallData
    {
        CallId = Guid.NewGuid().ToString("N"),
        PhoneNumber = "+49 123 456789"
    },
    cancellationToken);

if (dispatchResult.IsFailure)
{
    // inspect dispatchResult.Errors / dispatchResult.Messages
    return;
}

var instanceId = dispatchResult.Value;
```

### Signal Examples

```csharp
var dialedResult = await orchestrations.SignalAsync(
    instanceId,
    "CallDialed",
    cancellationToken: cancellationToken);
```

```csharp
var connectedResult = await orchestrations.SignalAsync(
    instanceId,
    "CallConnected",
    cancellationToken: cancellationToken);
```

```csharp
var holdResult = await orchestrations.SignalAsync(
    instanceId,
    "PlacedOnHold",
    cancellationToken: cancellationToken);
```

```csharp
var resumeHoldResult = await orchestrations.SignalAsync(
    instanceId,
    "TakenOffHold",
    cancellationToken: cancellationToken);
```

```csharp
var hungUpResult = await orchestrations.SignalAsync(
    instanceId,
    "HungUp",
    cancellationToken: cancellationToken);
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
