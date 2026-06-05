---
status: draft
---

# Design Specification: Jobs Feature (Application.Jobs)

> This design document outlines the architecture and behavior of a new Jobs feature within the Application layer. It defines the core concepts, goals, non-goals, high-level architecture, core design principles, public API and configuration, implementation details, testing strategies, and typical use cases for the Jobs feature.

[TOC]

## Introduction

`Application.Jobs` is the planned devkit-native replacement for the existing Quartz-backed `Application.JobScheduling` feature. It provides application-hosted job scheduling for recurring, one-time, delayed, manual, and event-triggered work without exposing a third-party scheduler as the core programming model.

The feature is intended for background work that belongs inside the application boundary: operational maintenance, synchronization, notifications, reporting, file scans, cleanup tasks, and similar workloads that need reliable scheduling, dependency injection, execution history, retries, cancellation, observability, and operational control.

Unlike the current JobScheduling feature, the new Jobs feature owns its runtime model, persistence model, trigger model, and management APIs. Durable providers coordinate multi-node execution through provider-backed locks or leases, which allows horizontally scaled workers to process large workloads while avoiding a full clustered scheduler design.

The design keeps the public API provider-neutral. Cron handling uses a devkit-owned abstraction with a Cronos-backed default implementation, storage uses replaceable providers, and integrations with Requester, Notifier, Messaging, Queueing, and Orchestration use public feature abstractions. Requester and Notifier live in the Common namespace/package and may be referenced directly by Jobs; Messaging, Queueing, and Orchestration own their Jobs integration extensions inside their existing feature packages and may only use Jobs contracts from Common.Abstractions, without referencing the Jobs runtime package. This keeps the scheduler replaceable internally while giving application developers a stable `IJob`/`IJobExecutionContext` programming model.

---

## Capability Layers

This specification describes the target capability of the Jobs feature. To keep the implementation understandable and aligned with the orchestration feature, the scheduler is organized into capability layers. These layers do not change the programming model; they describe how the complete feature can be built, tested, and reasoned about incrementally.

### Foundation Layer

The foundation layer contains the minimum capabilities required to define jobs, triggers, and scheduler configuration without committing to a specific durable provider.

It includes:

- shared `IJob`/`IJob<TData>` and `IJobExecutionContext`/`IJobExecutionContext<TData>` contracts that can live in `Common.Abstractions`
- the `Application.Jobs` `JobBase`/`JobBase<TData>` base classes, concrete `JobExecutionContext` implementations and typed data access
- job definitions, trigger definitions, data, properties, groups, and modules
- fluent registration and appsettings merge behavior
- trigger types for manual, one-time, delayed, startup-delay, cron, calendar, and event-based scheduling
- multiple triggers per job
- the devkit cron abstraction and default Cronos-based implementation
- retry, timeout, priority, and concurrency configuration models
- behavior/decorator registration
- client-facing `Result`/`Result<T>` contracts
- in-memory provider support for local development, tests, and transient scenarios

### Engine Layer

The engine layer contains the runtime capabilities that turn job and trigger definitions into executable work.

It includes:

- hosted scheduler runtime
- due occurrence scanning and occurrence materialization
- bounded worker pool and batch dispatch
- background execution, inline dispatch, and dispatch-and-wait
- cancellation token propagation and interrupt/cancel requests
- retry, timeout, pause, resume, and failure handling
- previous-run context hydration for delta processing
- missed-job detection after downtime
- dependency and chaining execution rules
- host/scheduler instance targeting
- lease acquisition, renewal, release, expiration, and recovery through provider abstractions
- correlation id propagation into logs and telemetry

The engine must depend on scheduler abstractions and Common abstractions rather than on Entity Framework, Cronos, Messaging, Queueing, Orchestration, or endpoint-specific types. Direct references to Common Requester/Notifier abstractions are allowed.

### Provider Layer

The provider layer contains storage, locking, lease, query, and provider-specific infrastructure implementations.

The first full durable provider is the Entity Framework provider.

It includes:

- job and trigger runtime state, occurrence, dependency, batch, execution, and history persistence
- provider-backed lock/lease acquisition for due occurrences
- lease renewal and abandoned-lease recovery
- missed occurrence recovery
- atomic batch creation and attachment
- batch child membership and roll-up state maintenance
- previous-execution lookup
- execution-history retention and purge support
- query and metrics support for operational APIs
- serializer integration for context data and properties
- annotated EF entities included in the host application's normal migration flow
- `DbContext` integration through a capability interface such as `IJobsContext`

Alternative providers may be added later, provided they preserve the same observable runtime behavior, especially occurrence identity, lease exclusivity, retry semantics, batch atomicity, batch roll-up behavior and execution-history behavior.

### Operational Layer

The operational layer contains the capabilities required to inspect, manage, and operate jobs in real systems.

It includes:

- query services for jobs, triggers, occurrences, executions, leases, history, and metrics
- dashboard-ready query contracts
- optional REST endpoints
- optional operational UI/dashboard support
- enable, disable, pause and resume operations for code-registered jobs and triggers
- manual execution, cancel, interrupt, retry, archive and purge operations for materialized occurrences and history
- filtering, sorting, paging, and aggregate statistics
- authorization boundaries for endpoints and operational UI
- structured logging, metrics, tracing, and correlation
- diagnostics for lease ownership, stalled work, and failed provider operations

### Integration Layer

The integration layer contains optional outbound integrations and cross-feature integration points.

It includes:

- messaging integration (job publish message)
- queueing integration (job sends queue item)
- Requester integration (job send request)
- Notifier integration (job publish notification)
- Pipeline integration (job executes pipeline)
- orchestration integration (job starts orchestration)
- built-in devkit maintenance jobs
- scheduler and job xUnit harness integration
- data mapping from job data and properties into external messages/queue items/requests/notifications/orchestration starts
- shared job abstractions that may need to live in `Common.Abstractions` to avoid circular dependencies

Integrations for features outside Common must remain optional. The scheduler core may reference Requester and Notifier common abstractions directly, but it must remain usable when no Requester/Notifier services are registered. Messaging, Queueing, Orchestration, presentation endpoints and durable providers must remain optional.

See Appendix A for the target integration requirements.

### Layering Rule

Higher layers must not weaken or bypass the foundation and engine contracts.

All job behavior, including optional adapters, durable providers, management APIs, retry handling, timeout handling, previous-run lookup, and operational actions, must still follow:

- provider-neutral job and trigger definitions
- `Result`-based client-facing operations
- lease-protected occurrence execution for durable providers
- persisted execution history at meaningful runtime boundaries
- explicit cancellation/interrupt semantics
- replaceable cron and persistence abstractions
- observable runtime state through query services rather than direct private provider access

---

## XML documentation and examples

All public/protected code symbols introduced by this feature should include XML documentation comments:

- classes
- records
- interfaces
- enums
- properties
- methods

Public and protected symbols should be intentionally documented because they define the extension and implementation contract. Internal implementation details may use `internal`, and leaf types may use `sealed` when they are not intended for inheritance. Extensibility should be exposed through public interfaces, abstract base classes, builders, options and provider contracts rather than by making every implementation class inheritable. For public or client-facing symbols, the XML comments should also include usage examples where that improves usability.

---

## Glossary

This glossary captures the working terminology used by the Jobs feature. Names may still evolve during implementation, but the concepts should remain stable.

| Term                          | Meaning                                                                                                                                                  |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Job                           | A unit of background work implemented as an `IJob` or derived from the base `JobBase` class.                                                             |
| Job Definition                | The resolved in-memory representation of a registered job type, including stable name, display name, description, properties, defaults, data type, concurrency, retry, timeout and trigger definitions. |
| Job Name                      | A stable identifier for a job definition. It is used by configuration, management APIs, query APIs and persisted runtime records.                        |
| Job Display Name              | An optional human-readable name for dashboards, API clients and operational views. If omitted, the display name is resolved from the optional module name and dashified job type name. |
| Job Group                     | A grouping value used for scoping, querying, operations or migration compatibility with the existing JobScheduling feature. If no group is configured, the resolved group is `DEFAULT`. |
| Module                        | An optional module scope used when jobs are registered from modular application boundaries.                                                              |
| Trigger                       | A configured condition that can create job occurrences. A job can have multiple triggers.                                                                |
| Trigger Name                  | A stable identifier for a trigger within a job. Trigger names must be unique per job.                                                                    |
| Trigger Type                  | The trigger category, such as cron, one-time, delayed, startup-delay, manual, calendar or event-based.                                                   |
| Occurrence                    | A concrete scheduled execution request produced by a trigger. Durable providers persist occurrences before execution.                                    |
| Due Occurrence                | An occurrence whose scheduled execution time has arrived and is eligible for acquisition by a worker.                                                    |
| Missed Occurrence             | An occurrence that should have run while the scheduler was stopped, unavailable or unable to acquire work.                                               |
| Job Batch                     | A durable operational grouping of related occurrences that can be queried, monitored and controlled together. This is distinct from provider scan batch size or worker dispatch batch size. |
| Batch Child Occurrence        | An occurrence associated with a job batch and counted toward the batch's created, pending, processing, succeeded, failed, cancelled and finished totals. |
| Execution                     | One attempt to run a job occurrence. Retries create additional execution attempts for the same occurrence.                                                |
| Execution Record              | Durable attempt summary for one execution attempt, including status, timing, result/error summary, attempt number, and owning scheduler instance.         |
| Execution History             | Append-only lifecycle records describing occurrence transitions, execution attempt transitions, operator actions, retry scheduling, leases and diagnostics. |
| Previous Execution            | The immediately preceding execution attempt for the same occurrence. It is mainly useful when the current attempt is a retry.                             |
| Previous Successful Execution | The latest successful execution for the same job and trigger before the current occurrence, used for delta processing.                                    |
| JobExecutionContext           | The runtime context passed to a job through the `IJobExecutionContext` public contract. It exposes properties, typed data, previous run information, cancellation, messages, correlation and control operations. |
| Data                          | Structured input supplied by a code-registered trigger or manual dispatch and exposed to the job through `IJobExecutionContext<TData>` as `ctx.Data`. `Unit` represents no input data. |
| Properties                      | Non-input key/value information attached to jobs, triggers, occurrences or executions for filtering, diagnostics or runtime decisions.                 |
| Scheduler Instance            | One running scheduler host inside an application instance. It has an instance id used for leases, diagnostics, telemetry and `TargetInstances(...)` matching. |
| Worker                        | A scheduler runtime component or execution slot inside a scheduler instance that acquires due occurrences and dispatches job executions. It is not a public target identity. |
| Worker Pool                   | The bounded set of workers or execution slots inside a scheduler instance used to process jobs concurrently.                                             |
| Lease                         | A provider-backed ownership record that allows one scheduler instance to execute a due occurrence.                                                       |
| Lock                          | A provider-specific mechanism used to implement lease acquisition or exclusive updates.                                                                  |
| Lease Renewal                 | The act of extending ownership while a job is still running.                                                                                             |
| Lease Expiration              | The point at which an unrenewed lease is considered abandoned and can be recovered.                                                                      |
| Lease Recovery                | Releasing or reacquiring abandoned work after a host crash, shutdown or stalled execution.                                                               |
| Active Registration           | The resolved job and trigger model built from code-first fluent registration and attributes at application startup, with appsettings allowed only to override matching registrations. It is the source of truth for available jobs and triggers. |
| Runtime State                 | Durable mutable scheduler state associated with registered jobs or triggers, such as enable/disable overrides, pause state and trigger materialization watermarks. |
| Durable Provider              | A storage provider that persists runtime state, occurrences, executions, history and leases across process restarts.                                    |
| In-Memory Provider            | The default lightweight provider for tests, local development and transient workloads. It does not provide durable recovery across restarts.             |
| Entity Framework Provider     | The first durable provider, integrating scheduler persistence into an application `DbContext`.                                                           |
| `IJobsContext`        | Proposed capability interface implemented by an application `DbContext` to host scheduler persistence sets.                                              |
| Cron Trigger                  | A trigger based on a cron expression. The initial design supports 5-part cron and 6-part cron with seconds.                                              |
| Cron Engine                   | The devkit-owned abstraction responsible for cron parsing, validation and occurrence calculation.                                                        |
| Cronos Implementation         | The default cron engine implementation based on Cronos, hidden behind the devkit cron abstraction.                                                       |
| Serializer                    | The scheduler-wide `ISerializer` used for context data, trigger data, occurrence data, properties and retained diagnostic data. The default is `SystemTextJsonSerializer`. |
| Time Zone                     | The explicit `TimeZoneInfo` used when calculating cron occurrences. Scheduler calculations start from UTC instants.                                      |
| Calendar Trigger              | A trigger based on calendar-style schedules beyond direct cron expression strings.                                                                       |
| Manual Trigger                | A trigger that allows a job to be dispatched explicitly through services or operations APIs.                                                             |
| Delayed Trigger               | A trigger that creates an occurrence after a relative delay from activation, registration or dispatch.                                                   |
| Startup-Delay Trigger         | A trigger evaluated when the scheduler starts, after a configured delay.                                                                                 |
| Event-Based Trigger           | A trigger category reserved for application-defined or provider-defined event sources such as notifications, messages or queue activity when such trigger support is explicitly enabled. Requester remains an outbound job integration unless a separate request-observation adapter is explicitly designed. |
| Outbound Job Integration      | A job step that invokes another devkit feature, such as Messaging, Queueing, Requester, Notifier, or Orchestration, through the target feature's public abstraction. |
| Retry Policy                  | Configuration that determines whether failed executions are retried, how many times and with what delay/backoff.                                         |
| Timeout                       | A configured maximum runtime for an execution before cancellation or timeout handling is requested.                                                      |
| Priority                      | A scheduling hint used to order eligible work when multiple occurrences are due.                                                                         |
| Concurrency Limit             | A limit on simultaneous executions for a job, trigger, group or scheduler.                                                                               |
| Dependency                    | A persisted occurrence-level prerequisite that keeps an occurrence blocked until another occurrence reaches a configured terminal outcome.                 |
| Chaining                      | A relationship where completion of one job schedules or dispatches another job.                                                                          |
| Behavior                      | A decorator around job execution used for cross-cutting concerns such as logging, metrics, retries or validation.                                        |
| Exception Handler             | A registered component that handles unhandled job execution exceptions in a centralized way.                                                             |
| Dispatch                      | Programmatic request to execute a job immediately or in the background.                                                                                  |
| Dispatch And Wait             | Programmatic request to dispatch a job and wait for completion or a specific outcome.                                                                    |
| Pause                         | Operational action that temporarily stops scheduling or execution progression for a job, trigger or occurrence.                                          |
| Resume                        | Operational action that re-enables scheduling or execution progression after a pause.                                                                    |
| Cancel                        | Operational action that requests controlled cancellation of an occurrence or running execution.                                                          |
| Interrupt                     | Operational action that requests cancellation of a currently running execution.                                                                          |
| Purge                         | Explicit maintenance operation that deletes retained execution history or other eligible operational records.                                            |
| Operational Endpoints         | Optional HTTP endpoints for querying and managing jobs, triggers, executions, leases and history.                                                        |
| Operational UI                | Optional dashboard/UI built on top of query and management APIs.                                                                                         |
| Result Pattern                | The devkit `Result`/`Result<T>` model used by client-facing scheduler operations for explicit success/failure handling.                                  |
| Source-Level Migration        | Manual code migration from the existing `Application.JobScheduling` API to the new `Application.Jobs` API.                                               |
| Quartz Compatibility          | Source migration guidance for existing Quartz-backed JobScheduling users; runtime compatibility with Quartz stores is not required.                      |

---

## Core Characteristics

The Jobs feature is defined by the following core characteristics. These are the non-negotiable requirements and behaviors that should remain true across all providers, integrations and operational surfaces.

### Devkit-Native Scheduler

- Replaces the existing Quartz-backed `Application.JobScheduling` feature as the primary scheduling API.
- Does not expose Quartz, Hangfire, Cronos or any other third-party scheduler as the public programming model.
- Uses devkit-owned abstractions for jobs, triggers, cron handling, persistence, leases, queries and operations.
- Uses the devkit `Result`/`Result<T>` pattern for client-facing scheduling, control and query operations.

### Code-First Job Model

- Jobs are implemented with `IJob` or the base `JobBase` class.
- Jobs execute through `ExecuteAsync(IJobExecutionContext ctx, CancellationToken cancellationToken)`.
- Constructor dependency injection is supported.
- The default job lifetime is transient, with scoped or singleton lifetime available when explicitly configured.
- Typed data contracts are inferred from `JobBase<TData>` and exposed through `IJobExecutionContext<TData>` or equivalent typed access.
- `JobBase` is shorthand for `JobBase<Unit>` and represents a job with no data.
- Inline lambda/delegate jobs may be supported as a lightweight convenience, but they still execute through the normal job definition, context, trigger and history pipeline.
- Fluent configuration is the canonical authoring model; attributes and job properties may provide defaults or properties when they do not obscure the runtime contract.
- The runtime passes all execution state through `IJobExecutionContext`.
- Method-based job handlers are intentionally not part of the target model.

### Trigger-Centered Scheduling

- A job can have one or more triggers.
- Supported trigger types include manual, one-time, delayed, startup-delay, cron, calendar and event-based triggers.
- Trigger-level configuration can override job defaults for schedule, data, priority, retry policy, timeout, enabled state and scheduler instance targeting.
- Event-based triggers are provider-neutral in the core and can be connected through built-in Notifier adapters and optional Messaging/Queueing adapters. Requester-backed work is modeled as outbound job integration by default, not as an event source.
- Triggers produce concrete occurrences; durable providers persist occurrences before execution.

### Cron Handling

- Cron support is based on 5-part cron expressions and 6-part cron expressions with seconds.
- Cron parsing, validation and next-occurrence calculation are behind `IJobCronEngine`.
- The default cron engine uses Cronos internally.
- Cronos types must not leak into public scheduler APIs.
- Time zone and daylight-saving-time behavior must be explicit and tested.
- Year-field cron expressions are out of scope for the initial design.

### Execution Runtime

- The scheduler runs inside the application host.
- Work is dispatched by a bounded worker pool with configurable parallelism and batch size.
- Jobs can be executed in the background, dispatched inline, or dispatched and awaited.
- Running jobs receive cancellation tokens and can be interrupted or cancelled through operational APIs.
- Retries, timeout handling, pause/resume and failure handling are part of the runtime contract.
- Jobs should be authored as idempotent or retry-tolerant units because retries, lease recovery and missed-occurrence recovery may re-attempt work.

### Previous Run Context

- `IJobExecutionContext` exposes previous run information so jobs can process deltas without querying scheduler storage directly.
- The context distinguishes the previous attempt from the previous successful execution.
- Previous-attempt lookup is scoped to the current occurrence.
- Previous-success lookup is scoped by job and trigger by default.
- A job-level previous successful execution lookup can be exposed for jobs that need the latest success across all triggers.

### Persistence and Lease Coordination

- In-memory storage is the default for tests, local development and transient scenarios.
- Durable providers persist runtime state, occurrences, executions, history and lease properties.
- Active job and trigger definitions come from the resolved startup registration, not from durable storage.
- Durable providers coordinate multi-node execution through locks or leases.
- Lease acquisition must prevent two scheduler instances from executing the same occurrence concurrently.
- Lease renewal, expiration and recovery are required for long-running jobs and host failure scenarios.
- Durable providers must support missed-occurrence detection and recovery after downtime.

### Entity Framework Provider

- The first durable provider is Entity Framework.
- EF persistence should attach to an application `DbContext` through a capability interface such as `IJobsContext`.
- The scheduler must not require a separate technical scheduler database or `DbContext`.
- The consuming application owns migrations and schema evolution.
- Jobs EF entities should carry their mapping through attributes and EF conventions so implementing `IJobsContext` is enough for host migrations.

### Operational Surface

- Query APIs expose jobs, triggers, occurrences, executions, leases, history, metrics and aggregate statistics.
- Management APIs support enable, disable, pause and resume for code-registered jobs and triggers, plus manual dispatch, cancel, interrupt, retry, archive and purge for occurrences and retained history.
- Optional REST endpoints and dashboard/UI support are built on top of the query and management APIs.
- Operational endpoints and UI must be securable through host application authentication and authorization.
- Execution history must support filtering, paging, retention and purge.

### Observability

- Job execution is logged with structured lifecycle events for start, completion, failure, retry, cancellation, pause, resume and timeout.
- Metrics and traces are emitted for execution counts, durations, failures, retries, queue/occurrence age and worker utilization where available.
- Correlation IDs flow through `IJobExecutionContext` and into logs/telemetry.
- Lease ownership and recovery should be diagnosable through query or operational surfaces.

### Replacement and Migration

- `AddJobScheduler(...)` is the new API; `AddJobScheduling(...)` remains the legacy Quartz-backed API during any deprecation window.
- Source-level migration from common JobScheduling registrations should be documented.
- Runtime compatibility with Quartz tables, Quartz triggers, Quartz job stores or serialized Quartz job data is not required.
- Quartz-style clustering, leader election, partition rebalancing, sharding and multi-tenancy are out of scope for the initial design.

---

## Execution Model

The Jobs runtime is a durable, trigger-driven execution engine.

Its execution contract is defined by the following rules:

- **Definition-oriented scheduling**

  - Active registrations describe executable job types, triggers and their defaults.
  - The active registration model is rebuilt during startup from fluent registration, attributes and appsettings.
  - A job may have multiple triggers, and each trigger creates its own occurrence stream.
  - Runtime execution is always evaluated relative to the active registration model plus persisted runtime state.
  - Durable storage must not resurrect jobs or triggers that are no longer registered.

- **Occurrence-oriented execution**

  - A trigger does not execute a job directly; it creates a job occurrence.
  - An occurrence is the durable unit of work acquired by a scheduler worker.
  - The same job implementation can be executed many times through different occurrences and triggers.
  - Durable providers must persist due occurrences before they are executed.

- **Provider-backed coordination**

  - Durable execution is coordinated through provider-backed locks or leases.
  - A worker must acquire a lease for a due occurrence before executing it.
  - A worker must still hold a valid lease when persisting the execution result.
  - Lease properties must include enough owner and expiration information for diagnostics and recovery.

- **Bounded parallelism**

  - The scheduler dispatches work through a bounded worker pool.
  - Worker parallelism, scan batch size and polling interval are configurable.
  - Multiple occurrences can execute concurrently when concurrency settings allow it.
  - Per-job, per-trigger, group/module or global concurrency limits can restrict parallel execution.

- **Context-centered job execution**

  - Jobs receive all runtime data through `IJobExecutionContext`.
  - The context includes job identity, trigger identity, occurrence identity, data, properties, correlation id, scheduler instance id and cancellation token.
  - The context includes previous-run information so jobs can process deltas.
  - Jobs should not depend on scheduler storage directly for ordinary execution state.

- **Result-driven completion**

  - Job execution should complete with `Result` or `Result<T>` semantics.
  - Successful executions persist completion properties, result messages and duration.
  - Failed executions persist failure properties, messages, errors and duration.
  - Exceptions are captured by the runtime and passed through configured exception handling before final status is decided.

- **Durable progression points**

  - The runtime shall persist scheduler state whenever execution crosses a meaningful durable boundary.
  - Durable boundaries include:
    - active registration reconciliation
    - trigger runtime-state reconciliation or update
    - occurrence creation
    - occurrence lease acquisition
    - execution start
    - lease renewal
    - retry scheduling
    - timeout, cancellation or interrupt request
    - execution completion
    - execution failure
    - occurrence archive or purge
  - Each execution attempt shall be represented by retained execution history according to the configured retention policy.

- **Dependency injection**

  - Jobs support constructor dependency injection for integration with application services.
  - Dependency resolution happens at execution time within a DI scope owned by the scheduler runtime.
  - The default job lifetime is transient unless explicitly configured otherwise.

- **Recovery**

  - The scheduler must detect due or missed occurrences after application downtime.
  - The scheduler must recover abandoned leased occurrences after lease expiration.
  - Recovery must resume from persisted provider state, not from in-memory worker state.
  - Retried executions must preserve enough history to explain the sequence of attempts.

- **Operational control**

  - Runtime operations can manually dispatch, pause, resume, cancel, interrupt or retry jobs and occurrences.
  - Operational actions must use the same provider and history model as automatic scheduling.
  - Operational actions must return explicit `Result`/`Result<T>` outcomes for invalid state transitions, missing records and provider failures.

### Lease Contract

The Jobs runtime shall support execution on multi-node systems.

To ensure correctness in a distributed environment, every durable occurrence execution shall run under an exclusive provider-backed lease.

This includes at least:

- due occurrence acquisition
- execution start
- execution result persistence
- retry scheduling (one occurrence, multiple execution attempts)
- timeout, cancellation and interrupt handling
- lease recovery
- pause, resume and cancel operations when they mutate persisted occurrence state

The lease contract is:

- a lease is acquired per occurrence, not globally for the whole scheduler
- at most one scheduler instance may hold the active lease for a given occurrence at a time
- a scheduler instance must acquire the lease before executing the occurrence
- the scheduler instance must still hold a valid lease when persisting execution status
- leases are time-bound and renewable so another node can recover work after node failure
- lease owner identity and expiration properties are persisted or otherwise durably coordinated
- if a worker loses its lease, it must stop mutating the occurrence immediately
- a new worker may continue only from the latest persisted occurrence and execution state
- leases prevent concurrent execution of the same occurrence, but they do not by themselves make job business logic idempotent
- prevention of duplicate business effects relies on occurrence identity, optional idempotency keys and idempotent job authoring

### Execution Algorithm

The runtime shall process scheduled jobs using the following high-level algorithm:

1. Load scheduler configuration and resolve active job and trigger definitions from registration.
2. Resolve the active scheduler instance id and worker pool settings.
3. Ask trigger providers for due trigger materialization work.
4. Persist newly due occurrences for durable triggers when they do not already exist.
5. Query the provider for due, runnable and unleased occurrences within the configured batch size.
6. Order eligible occurrences by priority, due time and provider-defined stable tie-breakers.
7. Acquire an exclusive lease for one occurrence.
8. Create an execution record with status `Running`.
9. Create a DI scope for the job execution.
10. Resolve the `IJob` implementation.
11. Hydrate `IJobExecutionContext`, including data, properties, cancellation token, correlation id and previous-run information.
12. Execute `IJob.ExecuteAsync(...)`.
13. Capture returned `Result`, messages, errors, duration and any context updates.
14. If execution succeeds, persist `Completed` status, completion properties and execution history.
15. If execution fails and a retry policy applies, persist failed-attempt history and schedule the next retry attempt for the same occurrence.
16. If execution fails and no retry applies, persist terminal failure status and failure properties.
17. Release the lease after the final persisted state transition for the attempt.
18. Continue scanning and dispatching until the scheduler stops or no eligible work remains.

### Inline Dispatch Algorithm

Inline dispatch uses the same job definition, context and result semantics, but it is caller-driven.

1. Validate the requested job and optional trigger/data.
2. Create an occurrence or transient execution request depending on configuration.
3. If durable execution is requested, acquire the normal occurrence lease.
4. Execute the job in the caller's awaited flow while still using a scheduler-owned DI scope.
5. Persist execution history when the job is durable or history tracking is enabled.
6. Return `Result<JobExecutionResult>` or equivalent to the caller.

Inline dispatch must not bypass retry, timeout, cancellation, context hydration or execution-history rules unless the caller explicitly selects a transient no-history mode.

### Trigger Materialization

Trigger materialization is the process of turning trigger definitions into occurrences.

Rules:

- cron and calendar triggers materialize occurrences from scheduled time calculations
- one-time triggers materialize exactly one occurrence unless explicitly reset or recreated
- delayed triggers materialize from a calculated due UTC instant after activation
- startup-delay triggers materialize relative to scheduler startup
- manual triggers materialize only when explicitly dispatched
- event-based triggers, when explicitly enabled, materialize occurrences from their configured event sources
- durable providers must deduplicate materialized occurrences using deterministic trigger and scheduled-time identity
- missed-occurrence recovery must use the same materialization and deduplication rules as normal scheduling

### State and Status Model

The runtime should distinguish occurrence state from execution attempt state.

Occurrence-level states should cover at least:

```csharp
public enum JobOccurrenceStatus
{
    Materialized,
    Scheduled,
    Due,
    Blocked,
    Leased,
    Running,
    RetryScheduled,
    Completed,
    Failed,
    Cancelled,
    Paused,
    Archived
}
```

Execution-attempt states should cover at least:

```csharp
public enum JobExecutionStatus
{
    Started,
    Completed,
    Failed,
    TimedOut,
    Cancelled,
    Interrupted,
    Retried
}
```

Dependency, batch and scheduler-instance state should also use explicit enums:

```csharp
public enum JobDependencyStatus
{
    Pending,
    Satisfied,
    Failed,
    Skipped,
    Cancelled
}

public enum JobDependencyFailurePolicy
{
    KeepBlocked,
    Skip,
    Cancel,
    Fail
}

public enum JobBatchStatus
{
    Created,
    Processing,
    Completed,
    CompletedWithFailures,
    Failed,
    Cancelled,
    Archived
}

public enum JobBatchCompletionPolicy
{
    RequireAllSucceeded,
    AllowPartialCompletion
}

public enum JobSchedulerServerStatus
{
    Starting,
    Active,
    Draining,
    Stopped,
    Unhealthy,
    Expired
}
```

The exact enum values can evolve during implementation, but they must remain explicit devkit enum types, not free-form strings. Operational queries must make the distinction between scheduled occurrence lifecycle, individual execution attempts and batch roll-up state clear.

---

## Triggers

Triggers are the scheduler inputs that create executable job occurrences. A trigger belongs to one job definition, but a job can have multiple triggers. The scheduler must treat triggers as configuration and occurrence materialization rules; the trigger itself does not execute the job.

Common trigger rules:

- Trigger names must be stable and unique within a job.
- Trigger type and trigger options must be validated during registration or configuration validation.
- Trigger-level settings can override job-level defaults for data, priority, retry policy, timeout, concurrency behavior, enabled state, scheduler instance targeting and operational properties.
- Durable providers must persist trigger runtime state and materialized occurrences when the job or trigger is durable.
- Trigger materialization must be idempotent. The same trigger input must not create duplicate occurrences after retries, restarts or multi-node scans.
- Each materialized occurrence must include the originating job name, trigger name, trigger type, scheduled time when applicable, data, correlation properties and deterministic identity data.
- Disabled triggers must not create new occurrences, but existing occurrences should remain queryable and controllable unless explicitly cancelled or purged.

### Manual Trigger

A manual trigger creates an occurrence only when an application service, operator, API endpoint or inline dispatch call explicitly requests it.

Expected behavior:

- Manual triggers are useful for support operations, user-requested actions, administrative tasks and ad hoc reprocessing.
- A manual dispatch can provide data and properties that override or augment the trigger defaults.
- Durable manual dispatch must persist the occurrence before execution so the request is visible to operations and can survive process failure.
- Manual dispatch should still use the normal lease, retry, timeout, cancellation, context hydration and history rules.
- Manual triggers can be enabled or disabled like other triggers. A disabled manual trigger should reject new dispatch requests with a `Result` failure.

### One-Time Trigger

A one-time trigger creates one occurrence at a configured absolute due time.

Expected behavior:

- The configured time must be normalized to a UTC instant for scheduler decisions.
- The trigger materializes at most one occurrence for a given active registration unless code/configuration changes intentionally define a new registration identity or reset policy.
- If the application is offline when the due time passes, a durable provider must recover the missed occurrence according to the trigger's missed-occurrence policy.
- The operational model should distinguish a one-time trigger that has not fired yet from one that has already materialized its occurrence.
- Changing a one-time trigger's code/appsettings registration before materialization should update the pending due time after startup reconciliation. Changing it after materialization requires an explicit reset/replacement registration policy; runtime management APIs must not rewrite the trigger definition in storage.

### Delayed Trigger

A delayed trigger creates an occurrence after a relative delay.

Expected behavior:

- The delay is relative to the trigger activation point, such as startup activation, explicit scheduling or another accepted scheduler command.
- Once accepted, durable providers should persist the calculated due UTC instant so restart behavior is based on a stable timestamp instead of recalculating the delay.
- Delayed triggers are appropriate for follow-up work such as retry-like workflows, deferred notifications or short-lived scheduled actions that should not be modeled as cron schedules.
- A delayed trigger should materialize at most one occurrence per activation unless the trigger is explicitly configured as reusable.

### Startup-Delay Trigger

A startup-delay trigger creates an occurrence after the scheduler instance starts and the configured delay has elapsed.

Expected behavior:

- Startup-delay triggers are useful for warm-up, cache refresh, maintenance and compatibility with the old delayed-start job behavior.
- Startup-delay occurrences must go through the same occurrence, lease and execution pipeline as other triggers.
- In multi-node deployments, the trigger must declare whether it runs per scheduler instance or as a single durable occurrence coordinated by provider leases.
- A durable singleton startup-delay trigger must use deterministic identity data so multiple nodes starting at the same time do not create duplicate executions.
- Startup-delay triggers should not block application startup; the scheduler should register the delayed work and return control to the host.

### Cron Trigger

A cron trigger creates recurring occurrences from a cron expression and a time zone.

Expected behavior:

- Cron triggers support 5-part expressions and 6-part expressions with seconds, aligned with the default Cronos-based cron engine.
- Year-field cron expressions are not supported in the initial design.
- Cron parsing, validation, next-occurrence calculation, time zone handling and daylight-saving-time behavior must go through the devkit-owned `IJobCronEngine` abstraction.
- The scheduler must calculate occurrences from persisted UTC watermarks and explicit `TimeZoneInfo` values.
- Durable providers must deduplicate cron occurrences by job name, trigger name and scheduled UTC instant.
- Missed cron occurrences after downtime must be handled by the trigger's missed-occurrence policy and bounded by provider safety limits.

### Calendar Trigger

A calendar trigger creates recurring occurrences from higher-level calendar rules instead of direct cron expressions.

Expected behavior:

- Calendar triggers are intended for schedules that are awkward or unclear as cron, such as business-day rules, selected weekdays, monthly rules, date exclusions or explicit date sets.
- Calendar rules should be represented by scheduler-owned models, not by provider-specific calendar types.
- Calendar triggers must still materialize concrete occurrences with scheduled UTC instants before execution.
- Calendar calculation should be replaceable through an abstraction if the initial implementation delegates to a helper library or provider.
- Calendar triggers must use the same missed-occurrence, deduplication, lease and history behavior as cron triggers.

### Event-Based Trigger

An event-based trigger creates occurrences from accepted event records produced by application events (Notifier), messages (Messaging), queue activity (Queueing) or a custom event-source adapter. Requester is not an event source in the base model because `IRequester` represents request/response command and query dispatch. If a future request-observation adapter is added, it must be an explicit optional adapter that observes completed request activity without coupling the scheduler core to request handler execution.

Expected behavior:

- The scheduler core should expose a provider-neutral event-trigger model.
- Built-in adapters may connect Common Notifier features to the event-trigger model.
- Optional adapters may connect Messaging and Queueing features to the event-trigger model.
- Adapters should register event sources and mapping rules only; the scheduler core must not depend directly on Messaging or Queueing packages.
- Accepted events should create job occurrences asynchronously and should not execute the job in the publishing or receiving call path unless the caller explicitly selects inline dispatch.
- Durable event-trigger adapters must persist or otherwise durably acknowledge an accepted event before materializing a job occurrence.
- Event-trigger adapters must define the transaction boundary with the source feature. If the source feature cannot atomically persist the event and scheduler occurrence, the adapter must use an idempotency key so retries can safely re-materialize the occurrence.
- Event identity, message id, notification id or an explicit idempotency key should be used when available to prevent duplicate occurrences.
- Event data mapping should support typed job data and correlation properties propagation.
- Event-trigger failures should be reported through `Result` values, logging, metrics and execution history where applicable.

### Custom Trigger

Custom triggers allow applications or provider packages to add new trigger types without changing the scheduler core.

Expected behavior:

- Custom trigger providers must implement scheduler-owned trigger interfaces and return scheduler-owned occurrence models.
- Custom trigger state must be serializable by the configured scheduler serializer when persistence is enabled.
- Custom triggers must participate in validation, materialization, deduplication, lease coordination, operational queries and history retention.
- Custom trigger implementations must not bypass the scheduler execution pipeline.

### Missed-Occurrence Policy

Time-based triggers need explicit behavior for downtime, clock drift and long scheduler pauses.

Supported policies should include:

- `Skip`: ignore occurrences that became due while the scheduler was not active.
- `RunOnce`: create one catch-up occurrence for the missed window.
- `RunAll`: create each missed occurrence up to configured safety limits.

The provider must enforce maximum catch-up windows and batch sizes so recovery after long downtime cannot flood the worker pool or persistence store.

---

## Reliability & Resilience

Reliability is a core property of the Jobs feature. Jobs may execute on multiple application instances, may be retried after failures, and may be recovered after process or node failure. Therefore scheduler state must be durable at the points where the runtime can no longer safely reconstruct intent from in-memory data.

### Failure Handling

The scheduler shall provide built-in failure handling for technical and transient failures.

This includes:

- configurable retry policies for jobs and trigger overrides
- configurable timeout handling
- centralized exception handling through `IJobSchedulerExceptionHandler`
- durable failure and retry records when a durable provider is used
- explicit terminal states when no retry or recovery path remains

Exceptions thrown by jobs are treated as execution failures of the current execution attempt.

The runtime shall:

- persist the failed execution attempt, including exception properties suitable for diagnostics
- evaluate the trigger-level retry policy first, then the job-level retry policy, then the scheduler default
- persist retry scheduling properties before the next attempt becomes eligible
- preserve the original occurrence identity for all retry attempts so history remains understandable
- mark the occurrence `Failed` when no retry or recovery path remains

Jobs should use `Result` or `Result<T>` for expected business outcomes. Business rejection, validation failure, or negative domain outcomes should be modeled as explicit results and messages rather than as exceptions. Exceptions are intended for unexpected technical failures.

### Retry and Backoff

Retry behavior must be deterministic and visible.

Retry policy should support at least:

- maximum attempts
- fixed delay
- exponential backoff
- optional jitter
- maximum retry delay
- retryable and non-retryable exception classification where practical

Retry state must not exist only in memory for durable jobs.

Durable retry records shall include at least:

- original occurrence identifier
- execution attempt number
- failed-at timestamp
- next retry due UTC
- failure message and exception properties when available
- scheduler instance that observed the failure

Retries must re-enter the normal occurrence acquisition, lease, execution, cancellation, timeout and history pipeline.

### Idempotency

Jobs shall be authored as idempotent or retry-tolerant units.

This is required because:

- a job may be retried after failure
- a lease may expire while a worker is still alive but no longer allowed to mutate scheduler state
- a process may crash after an external side effect but before final scheduler state is persisted
- missed-occurrence recovery may create catch-up work after downtime
- event-based triggers may receive duplicate events unless an idempotency key prevents it

The scheduler prevents duplicate execution of the same leased occurrence as far as the provider can enforce it, but it cannot guarantee that job business side effects are globally idempotent.

Duplicate business effects should be prevented through:

- deterministic occurrence identity
- event or caller-supplied idempotency keys
- idempotent job implementation
- business-level uniqueness constraints where the job touches external state
- checking previous successful execution from `IJobExecutionContext` when processing deltas

### Lease Recovery

Durable execution shall run under provider-backed leases.

Lease recovery is required for:

- host crashes
- process shutdown during execution
- worker stalls
- transient database or storage connectivity failures
- scheduler instance restarts

The provider shall persist enough lease properties to diagnose and recover abandoned work:

- occurrence identifier
- lease owner scheduler instance id
- lease acquired UTC
- lease expires UTC
- last renewal UTC
- renewal count where useful

When a lease expires, another scheduler instance may acquire the occurrence and continue from the latest persisted state. A worker that loses its lease must stop mutating the occurrence immediately. If the job continues running after lease loss because cancellation is cooperative, the runtime must prevent it from finalizing the occurrence unless it reacquires or verifies ownership according to the provider contract.

### Missed Occurrence Recovery

The scheduler shall detect due or missed occurrences after application downtime.

Recovery behavior must use the same materialization and deduplication rules as normal scheduling:

- cron and calendar triggers recover from persisted watermarks and scheduled UTC instants
- one-time triggers recover the configured due UTC instant
- delayed triggers recover the calculated due UTC instant
- startup-delay triggers recover according to their per-instance or singleton mode
- event-based triggers recover only events that were durably accepted by the adapter or source system

Recovery must be bounded by configured lookback windows, batch sizes and missed-occurrence policy so a long outage cannot flood the provider or worker pool.

### Cancellation, Timeout and Shutdown

Cancellation and timeout behavior must be explicit and observable.

The runtime shall:

- pass a cancellation token to every job execution
- request cancellation during graceful scheduler shutdown
- request cancellation when an operator cancels or interrupts a running execution
- request cancellation when a timeout expires
- persist cancellation, interruption and timeout outcomes in execution history
- release or expire leases according to the final persisted execution state

Jobs are expected to honor cancellation cooperatively. If a job ignores cancellation and the lease expires, another node may recover the occurrence according to the lease contract.

### Pause, Resume and Operational Control

Operational control actions are part of resilience, not only administration.

The scheduler shall support:

- pausing and resuming jobs
- pausing and resuming triggers
- cancelling scheduled or running occurrences
- interrupting running executions
- manually retrying failed occurrences
- purging retained history through explicit maintenance operations

Control actions that mutate durable scheduler state must use provider concurrency checks or leases where needed. They must return `Result`/`Result<T>` values for invalid transitions, missing records, stale state and provider failures.

### Dependency and Chaining Failure Behavior

Job dependencies and chaining must not hide failure state.

Job chaining is a first-class Jobs capability for coupling successive background jobs. Chaining must materialize each successor as an ordinary occurrence with its own execution lifecycle, lease, retry policy, timeout, history, and operational visibility. Chaining must not bypass the scheduler pipeline or turn the Jobs feature into a workflow/state-machine runtime.

Dependency semantics are occurrence-level. A dependency does not mean a job type globally waits for another job type forever; it means a materialized occurrence has one or more persisted prerequisites that must be satisfied before that occurrence can be leased and executed.

Dependency scope must be explicit:

- **Occurrence dependency**: a dependent occurrence waits for a specific prerequisite occurrence id.
- **Chain dependency**: a successor occurrence created by chaining records the predecessor occurrence id and required predecessor outcome.
- **Definition-level dependency templates**, if supported, only describe how to create occurrence dependencies during materialization; they are not runtime state by themselves.

The persisted model must make dependency evaluation durable. Providers should represent dependency links as separate provider rows or as structured occurrence dependency properties with equivalent query and concurrency behavior. A plain serialized blob that cannot be queried for blocked-work recovery is not sufficient for durable providers.

Dependency links are not batch membership. Batches use `JobBatchOccurrence` to group parallel child occurrences for progress and bulk operations. `JobOccurrenceDependency` is used only when one occurrence must wait for another occurrence, including normal job chaining.

Dependency links should include at least:

- dependent occurrence id
- prerequisite occurrence id
- required terminal outcome, such as completed successfully or completed with an accepted status set
- dependency status, such as pending, satisfied, failed, skipped or cancelled
- failure policy for the dependent occurrence
- created UTC and updated UTC
- optional reason, source chain id and properties

When a due occurrence has unsatisfied dependencies:

- dependency state must be visible through occurrence and history records
- the occurrence must remain `Blocked` or be queryable as blocked with a reason when blocked is derived
- the occurrence must not be leased until all required dependencies are satisfied
- dependency checks must be re-evaluated when prerequisite occurrences reach terminal states, during due scans and during recovery scans

After prerequisite retry exhaustion or terminal failure, the dependent occurrence follows its configured dependency failure policy:

- `KeepBlocked`: remain blocked for operator intervention
- `Skip`: transition to a skipped/cancelled terminal state with history
- `Cancel`: transition to `Cancelled` with dependency failure reason
- `Fail`: transition to `Failed` with dependency failure reason

The default policy should be conservative: keep dependent work blocked and visible unless the registration explicitly chooses a terminal outcome. Manual retry of the prerequisite or dependent occurrence must re-evaluate dependency links and record the operator action in history.

Chained jobs must be materialized as normal occurrences with their own trigger or chaining properties and must use the same retry, lease, timeout, cancellation and history rules as directly scheduled jobs.

The Jobs feature is not a SAGA orchestration engine. Rollback-style workflows should be modeled through explicit compensating jobs, chaining, or the Orchestration feature when workflow compensation semantics are required.

- Job chaining is for successive background jobs.
- Each chain step is still a normal job occurrence.
- Each step has its own lease, retry, timeout, history, data, and status.
- Chain progression is materialization of the next occurrence, not inline nested execution.
- A chain is not a long-lived workflow state machine.
- No compensation semantics unless modeled as explicit jobs.

### Batch Execution Model

Batches are a first-class operational grouping concept. A batch groups related occurrences so operators and application code can query progress, apply bulk controls and understand a larger unit of background work without changing the execution semantics of each child occurrence.

A batch is not a worker dispatch batch, queue scan batch, dependency graph or transaction boundary for execution. Child occurrences still execute independently through the normal occurrence, lease, retry, timeout, cancellation and history pipeline.

Batch scope must be explicit:

- **Manual bulk dispatch batch**: created when an application or operator dispatches several occurrences as one accepted operation.
- **Chain fan-out batch**: optionally created when a job or chain step materializes multiple parallel successor occurrences that should be tracked together. Any ordering between occurrences still uses normal occurrence dependencies.
- **Application-defined batch**: created by application code through scheduler APIs when the application has a durable grouping id, such as an import id or reprocessing campaign id.

Batch creation paths must be explicit:

- `CreateBatchAsync(...)` creates a durable batch record and may accept zero child occurrences.
- `DispatchBatchAsync(...)` creates a durable batch and one or more child occurrences in one provider transaction.
- `AttachToBatchAsync(...)` adds child occurrences to an existing batch and moves a finished batch back to `Processing` when new non-terminal children are attached.

Durable batch creation must be atomic. If a durable provider cannot persist the batch record, child occurrence records and batch-child links as one accepted operation, no child occurrence from that operation may become runnable. Providers may use database transactions, transactional outbox-style staging or an equivalent provider-specific atomic acceptance mechanism, but partial acceptance must return a `Result` failure and leave no runnable orphaned children.

Batch dispatch items may create immediate or scheduled child occurrences. Dependency-blocked work should use the normal occurrence dependency model, not batch membership.

Empty batches are valid. An empty batch starts as `Completed` for `RequireAllSucceeded` and can move back to `Processing` when child occurrences are attached later.

Durable providers must persist batch state in queryable form. Batch membership should be represented by separate batch and batch-occurrence rows, or an equivalent provider model that supports querying by batch id, child occurrence id and child status. Batch membership must not exist only as opaque serialized occurrence properties.

A batch record should include at least:

- batch id
- description or display name
- status
- completion policy
- created UTC, started UTC, completed UTC and updated UTC where applicable
- created, pending, processing, succeeded, failed, cancelled and finished child counts
- latest failure summary
- correlation id, causation id and safe properties
- archive state and audit properties

A batch-child link should include at least:

- batch id
- occurrence id
- child status projection
- created UTC and updated UTC
- optional sequence/order value
- optional source step, source feature or source id properties

Batch status is a roll-up over persisted batch intent and child occurrence outcomes:

- `Created`: the batch exists but no child occurrence has started.
- `Processing`: at least one child occurrence is active, due, blocked, retry-scheduled or otherwise not terminal.
- `Completed`: all child occurrences completed successfully.
- `CompletedWithFailures`: all child occurrences are terminal and at least one failed, timed out or was cancelled where the batch policy accepts partial completion.
- `Failed`: the batch policy requires all children to succeed and one or more child occurrences reached terminal failure or cancellation.
- `Cancelled`: the batch was cancelled before all children completed, or all unfinished children were cancelled through a batch operation.
- `Archived`: the batch is retained for inspection but removed from active operational views.

Batch completion policy controls roll-up:

- `RequireAllSucceeded`: any terminal failed, timed-out or cancelled child makes the batch `Failed`.
- `AllowPartialCompletion`: terminal failed, timed-out or cancelled children make the batch `CompletedWithFailures` after all children finish.

Batch operations are bulk operations over eligible child occurrences. They must report per-child failures and must not bypass normal occurrence state validation:

- retry a batch retries eligible failed child occurrences
- cancel a batch cancels eligible scheduled, due, blocked, retry-scheduled or running child occurrences according to normal cancellation semantics
- archive a batch archives the batch and eligible retained child records according to retention policy
- pause/resume, if exposed for batches, must map to child occurrence pause/resume and record the batch operation in history

Workers must check batch state before starting a child occurrence. If the child belongs to a cancelled batch, the occurrence must not execute and should transition according to the normal cancellation path with a batch-cancel reason. Providers may implement this check eagerly during the cancel operation, lazily during scans, or immediately before lease/start, but the observable result must prevent new child execution after batch cancellation is accepted.

Batch operations must be idempotent. Repeating the same operation with the same batch id and idempotency key should not create duplicate child occurrences or duplicate side effects. Partial success must be visible in the returned `JobBulkOperationResult`, child occurrence history and batch summary.

Retry behavior remains child-occurrence behavior. If a batch child is also blocked by a normal occurrence dependency, that dependency is represented in `JobOccurrenceDependency`; the batch only reflects the child as non-terminal until the dependency resolves or the child reaches a terminal state. Retry exhaustion on a child contributes to the batch roll-up according to the batch completion policy.

Batch history must record batch creation, child attachment, child terminal roll-up changes, operator actions, bulk operation outcomes, archive and purge actions. Child occurrence history must include the batch id when an operation was initiated through a batch.

### Persistence Contract

Durability is mandatory for durable providers and optional for the in-memory provider.

The active job and trigger definitions are not owned by durable storage or ordinary runtime-state rows. They are resolved during application startup from code-first registration sources:

- code-first fluent registration and attributes for jobs and static triggers
- matching appsettings entries that override already registered jobs and triggers

Durable storage owns runtime state, occurrences, leases and history. Runtime-state rows may influence effective operational state for active registrations, but they must never create new job or trigger definitions.

The runtime shall persist scheduler state whenever execution crosses a meaningful durable boundary.

This includes:

- registration reconciliation state for currently registered jobs and triggers
- job runtime-state changes such as operational enable/disable, pause/resume and last-seen properties
- trigger runtime-state changes such as operational enable/disable, pause/resume and materialization watermarks
- occurrence materialization
- occurrence lease acquisition, renewal and release
- execution start
- execution completion
- execution failure
- retry scheduling
- timeout, cancellation or interrupt request
- pause and resume actions
- manual dispatch
- batch creation, child attachment and batch roll-up updates
- history archival and purge actions

The durable model shall include at least:

- **Job runtime state**

  - job name
  - registration generation or signature
  - last seen UTC
  - optional operational enabled override
  - pause state and reason where supported
  - audit properties

- **Trigger runtime state**

  - owning job name
  - trigger name
  - registration generation or signature
  - last seen UTC
  - optional operational enabled override
  - pause state and reason where supported
  - last materialized due UTC
  - next due UTC projection where useful
  - audit properties

- **Occurrences**

  - occurrence identifier
  - owning job and trigger identity
  - trigger type
  - scheduled UTC when applicable
  - due UTC
  - status
  - priority
  - data and properties
  - correlation identifier
  - idempotency key when available
  - attempt counters and retry linkage

- **Batches**

  - batch identifier
  - status
  - completion policy
  - description or display name
  - child count projections
  - latest failure summary
  - correlation and causation identifiers
  - safe properties and audit properties
  - archive state

- **Batch child links**

  - batch identifier
  - occurrence identifier
  - child status projection
  - optional sequence/order value
  - source properties when available
  - audit properties

- **Occurrence dependencies**

  - dependency identifier
  - dependent occurrence identifier
  - prerequisite occurrence identifier
  - required terminal outcome or accepted status set
  - dependency status
  - dependency failure policy
  - reason and source properties
  - audit properties

- **Execution history**

  - append-oriented records describing execution start, completion, failure, retry, timeout, cancellation, interruption, pause/resume and terminal outcomes
  - each record includes UTC timestamp, job name, trigger name, occurrence id, execution id, scheduler instance id and relevant properties
  - exception details suitable for diagnostics without requiring provider-specific access

- **Lease records**

  - occurrence identifier
  - lease owner scheduler instance id
  - acquired, renewed and expiration timestamps
  - ownership verification data required by the provider

The runtime must always be able to reconstruct runnable work, latest execution state, retry eligibility and operational history from active registrations plus persisted provider state without relying on in-memory worker state.

Source-of-truth rules:

- active registrations are the source of truth for which jobs and triggers exist
- code-first fluent registration and attributes are the only source of job and trigger definitions
- appsettings may override matching jobs and triggers, but appsettings must not create new job definitions or trigger definitions
- durable runtime state may override operational state for registered jobs and triggers, but it must not create definitions
- if a job registration is removed from code or attributes, it must no longer appear in the active job list and workers must not execute pending occurrences for that job
- if a trigger registration is removed from code or attributes, it must no longer materialize new occurrences and pending occurrences for that trigger must not execute
- removed jobs and triggers may remain visible through history or orphaned-runtime-state views for support and cleanup
- changing display name, description, group, module, schedule, retry policy or defaults in registration should affect the active model on next startup without requiring database updates
- changing the stable job name or trigger name creates a new identity; continuity with old history requires an explicit migration/rename operation
- durable providers may keep lightweight runtime-state rows for removed registrations, but such rows are non-authoritative and must be marked stale, orphaned or last-seen-only

### Auditability Contract

The Jobs feature shall provide lightweight auditability over persisted scheduler operations and records.

The auditability contract is:

- mutable persisted scheduler records shall store `CreatedDate` and `UpdatedDate`
- mutable persisted scheduler records should also store `CreatedBy` and `UpdatedBy` when a current user identity is available
- this applies at least to job runtime state, trigger runtime state, occurrences, executions, leases where persisted, and operational maintenance records where applicable
- append-oriented execution history shall capture `RecordedAt` and, when available, `RecordedBy` for the recorded scheduler action
- audit properties shall be updated as registration state is reconciled, runtime overrides are changed, occurrences are materialized, executions progress, and maintenance actions are performed

The runtime and provider may use `ICurrentUserAccessor` to resolve the current user identity for these audit fields.

`ICurrentUserAccessor` is optional:

- the Jobs feature must work correctly when `ICurrentUserAccessor` is not registered
- the Jobs feature must work correctly when no user is authenticated or no user identifier can be resolved
- in those cases the runtime/provider shall continue gracefully and leave `CreatedBy` and `UpdatedBy` empty or use a provider-defined system identity value

### Retention, Archival and Purge

The Jobs feature shall support retained operational history together with explicit archival and purge operations.

This applies to:

- completed occurrences
- failed occurrences
- cancelled occurrences
- execution history
- lease diagnostics for completed or abandoned work where retained
- trigger and job runtime-state change history where supported

The retention contract is:

- active scheduler data must not be purged while it can still affect execution
- completed and non-active data may remain queryable for operational support and dashboard scenarios
- archival and purge are explicit lifecycle or maintenance operations, not implicit loss of history
- providers may retain active and archived data differently, but archived data must remain inspectable until purged

The purge contract is:

- purge shall support at least age-based deletion
- purge should support filtering by job name, trigger name, status, completed time and archived state where meaningful
- purge operations shall be explicit maintenance actions
- purge operations shall return `Result`/`Result<T>` outcomes and record enough diagnostics for support

### Persistence Provider Model

The durable layer shall be exposed through a provider model.

The scheduler runtime, fluent registration model and public management/query services should depend on scheduler persistence abstractions rather than directly on Entity Framework types.

The provider model shall allow alternative persistence implementations later without changing the observable scheduler contract.

The provider contract shall cover at least:

- job runtime-state storage
- trigger runtime-state storage
- occurrence storage and state updates
- occurrence dependency storage and state updates
- batch storage, child membership and roll-up state updates
- execution history append
- lease acquisition, renewal, verification and release
- missed occurrence recovery support
- atomic batch acceptance and attach support
- previous execution lookup
- scheduler querying for operations, endpoints and dashboards
- serializer integration for context data and properties

Provider implementations must preserve the same observable behavior, especially:

- occurrence identity and deduplication semantics
- lease exclusivity semantics
- durable-boundary persistence rules
- retry scheduling semantics
- dependency blocking semantics
- batch atomicity, child membership and roll-up semantics
- append-oriented execution history behavior
- previous-run lookup behavior
- missed-occurrence recovery behavior

### Persistence Abstractions

The persistence provider model should expose focused scheduler persistence abstractions rather than one oversized storage service.

The abstraction set shall distinguish between:

- **Execution-facing abstractions**

  - used by the scheduler runtime to materialize, acquire, execute and finalize work safely

- **Operations-facing abstractions**

  - used by application services, administration APIs, dashboards and support tooling

### Execution-Facing Persistence Abstractions

The runtime needs the following minimum abstractions.

- **`IJobRuntimeStateStore`**

  - Stores and loads durable runtime state for registered jobs.
  - Updates operational enabled overrides, pause state and last-seen registration properties.
  - Marks stale runtime state for jobs that are no longer registered.
  - Never creates active job definitions without a matching registration.

- **`IJobTriggerRuntimeStateStore`**

  - Stores and loads durable runtime state for registered triggers.
  - Updates operational enabled overrides, pause state, next due projections and materialization watermarks.
  - Marks stale runtime state for triggers that are no longer registered.
  - Supports deterministic trigger lookup by job name and trigger name.

- **`IJobOccurrenceStore`**

  - Creates materialized occurrences idempotently.
  - Loads due occurrences eligible for execution.
  - Updates occurrence lifecycle state.
  - Preserves retry linkage and original occurrence identity.
  - Excludes or explains due occurrences blocked by unresolved dependencies or cancelled batches.

- **`IJobOccurrenceDependencyStore`**

  - Creates dependency links for occurrence dependencies and chain dependencies.
  - Must not be used to represent batch membership.
  - Loads unresolved dependencies for due occurrence eligibility checks.
  - Updates dependency status when prerequisite occurrences reach terminal states.
  - Finds dependent occurrences that need to be unblocked, failed, cancelled or skipped after prerequisite completion or retry exhaustion.
  - Supports recovery scans that can reconstruct blocked-state decisions from persisted dependency links.

- **`IJobBatchStore`**

  - Creates empty batches and dispatch batches atomically with child occurrences and batch-child links.
  - Attaches child occurrences to an existing batch atomically.
  - Represents grouping and bulk operation membership only; ordering and prerequisites remain in `IJobOccurrenceDependencyStore`.
  - Loads batch summaries and child membership by batch id.
  - Maintains batch child status projections and roll-up counts.
  - Updates batch roll-up status when child occurrences transition.
  - Applies batch cancellation, pause, resume, retry and archive operations through normal occurrence state rules.
  - Provides idempotency checks for batch creation and attach requests.

- **`IJobLeaseStore`**

  - Acquires an exclusive lease for an occurrence.
  - Renews an existing lease.
  - Releases a lease explicitly.
  - Verifies current lease ownership when finalizing state-mutating actions.
  - Finds expired leases eligible for recovery.

- **`IJobExecutionHistoryStore`**

  - Appends execution history records.
  - Stores start, completion, failure, retry, timeout, cancellation, interruption, pause/resume and terminal lifecycle events.
  - Preserves append-oriented history semantics.

- **`IJobPreviousExecutionStore`**

  - Loads the previous execution attempt for the same occurrence.
  - Loads the previous successful execution for the same job/trigger identity before the current occurrence.
  - Supports job-level previous-success lookup across triggers before the current occurrence where configured.

- **`ISerializer`**

  - Serializes `IJobExecutionContext.Data`, trigger data, occurrence data, properties and retained diagnostic data.
  - Deserializes persisted data back into the typed job data model.
  - Keeps data persistence provider-neutral and avoids leaking storage representation into the runtime.
  - The Jobs feature shall use `BridgingIT.DevKit.Common.ISerializer`.
  - If no serializer is configured explicitly, the default shall be `BridgingIT.DevKit.Common.SystemTextJsonSerializer`.

### Operations-Facing Persistence Abstractions

The feature also needs read/query abstractions over persisted scheduler state for support, monitoring, metrics, APIs and dashboard scenarios.

- **`IJobSchedulerQueryStore`**

  - Returns runtime state, occurrence, dependency, batch, execution, lease and history data that can be merged with active registrations.
  - Returns stale/orphaned runtime state for support views where requested.
  - Returns occurrence details by occurrence identifier.
  - Returns paged occurrence lists with filters such as job name, trigger name, trigger type, status, due time range, correlation id and scheduler instance id.
  - Returns batch details, child occurrence lists and child status counts.
  - Returns dependency details and blocked-work reasons where those reasons are persisted or derivable.
  - Returns execution history for a job, trigger or occurrence.
  - Returns active and expired lease diagnostics where supported by the provider.
  - Returns aggregated metrics derived from persisted scheduler data.

Metrics should support at least:

- counts by job, trigger type and lifecycle status
- due, running, completed, failed, cancelled and retried occurrence counts
- counts by batch status and batch completion policy
- batch child counts by child occurrence status
- blocked occurrence counts by dependency status and batch cancellation state
- oldest due occurrence timestamp
- execution duration averages and percentiles where the provider can compute them efficiently
- retry counts and failure rates
- timeout and cancellation counts
- lease acquisition, renewal, expiration and recovery counts
- worker utilization or backlog-oriented metrics when available

Metrics may be computed directly from durable storage, from provider-maintained summary tables, or from provider-maintained projections, as long as metrics remain derived from persisted scheduler data rather than worker-local memory.

### Abstraction Shape

The abstractions should be composable and provider-neutral.

The provider model shall follow this responsibility shape:

```csharp
public interface IJobStoreProvider
{
    IJobRuntimeStateStore JobStates { get; }
    IJobTriggerRuntimeStateStore TriggerStates { get; }
    IJobOccurrenceStore Occurrences { get; }
    IJobOccurrenceDependencyStore Dependencies { get; }
    IJobBatchStore Batches { get; }
    IJobLeaseStore Leases { get; }
    IJobExecutionHistoryStore History { get; }
    IJobPreviousExecutionStore PreviousExecutions { get; }
    IJobSchedulerQueryStore Queries { get; }
    ISerializer Serializer { get; }
}
```

Equivalent implementations are allowed, but the feature shall preserve these responsibilities.

Execution-facing abstractions should not depend on HTTP or UI concerns. Operations-facing abstractions should not require callers to understand provider-internal table layouts.

---

## Observability & Management

The Jobs feature must be observable and manageable in production without requiring direct access to provider-internal tables.

The operational model is built on the merged view of active registrations, persisted scheduler runtime state, query services, management services and optional endpoints. A dashboard UI is optional, but the query and endpoint contracts should be rich enough to support one.

### Auditing

All meaningful scheduler state changes shall be visible through durable execution history when a durable provider is used.

This includes:

- registration reconciliation and stale-runtime-state detection
- trigger registration reconciliation, active-registration removal detection, and enable/disable/pause/resume runtime-state overrides
- occurrence materialization
- execution start, completion, failure and retry decisions
- timeout, cancellation and interrupt requests
- pause and resume operations
- lease acquisition, renewal, expiration and recovery diagnostics where retained
- manual dispatch and operator-initiated retry
- archival and purge maintenance actions

Logs and durable history serve different purposes and both are required. Logs support runtime diagnostics and external telemetry. Durable history supports operational inspection, dashboard views and support workflows after the process that produced the logs is no longer running.

### Monitoring

Jobs, triggers, occurrences, executions and leases must be inspectable at runtime.

Monitoring must be based on active registrations plus persisted scheduler state rather than worker-local memory for durable providers. Worker-local metrics can enrich telemetry, but they must not be the only source for operational state that support tools depend on.

The monitoring model shall support:

- current job and trigger status
- stale/orphaned runtime state for registrations that were removed
- due, running, completed, failed, cancelled and paused occurrences
- current and expired lease diagnostics
- execution history and retry history
- missed-occurrence recovery visibility
- scheduler instance ownership and worker/backlog health
- aggregate metrics for dashboard and alerting scenarios

### Logging and Tracing

The runtime shall emit structured logs for meaningful lifecycle events.

At minimum, logs should cover:

- scheduler startup and shutdown
- trigger materialization scans
- occurrence creation and deduplication
- due occurrence acquisition
- lease acquisition, renewal, release and expiration
- execution start and completion
- execution failure and retry scheduling
- timeout, cancellation and interrupt handling
- pause, resume, manual dispatch, retry and purge actions
- provider errors and recovery actions

Each log entry should include stable identifiers where available:

- job name
- trigger name
- trigger type
- occurrence id
- execution id
- scheduler instance id
- correlation id
- lease owner

Tracing should use `ActivitySource` and create spans for:

- scheduler scan cycles
- trigger materialization
- lease acquisition and renewal
- job execution
- retry scheduling
- event-trigger acceptance
- management operations

Correlation id must flow through `IJobExecutionContext` and be included in logs, traces, metrics tags and persisted history where applicable.

### Query and Metrics Contracts

Query and metrics support are first-class feature requirements, not optional diagnostics add-ons.

The query contract shall support at least these application-facing scenarios:

- list active job definitions with paging and filtering
- inspect a specific active job definition with its runtime state
- list and inspect triggers for a job
- list due, running and past occurrences
- inspect a specific occurrence in detail
- inspect execution history for a job, trigger or occurrence
- inspect active, expired and recovered leases where supported by the provider
- inspect previous execution information for delta-processing diagnostics
- retrieve aggregated counts and duration-oriented metrics

Client-facing query operations shall use `Result`, `Result<T>` or `ResultPaged<T>` wrappers so callers can distinguish successful reads, not-found conditions, invalid filters and provider/query failures through the standard Result contract.

The query/filter contract shall support at least:

- job name
- trigger name
- trigger type
- occurrence status
- execution status
- scheduler instance id
- correlation id
- idempotency key where available
- due time range
- started time range
- completed time range
- priority
- paging and sorting

Metrics should support at least:

- active job and trigger counts
- enabled/disabled job and trigger counts
- occurrence counts by lifecycle status
- execution counts by result status
- failure, retry, cancellation and timeout counts
- average and percentile execution duration where the provider can compute them efficiently
- oldest due occurrence age
- backlog size
- missed-occurrence recovery counts
- lease acquisition, renewal, expiration and recovery counts
- worker utilization and execution slot saturation where available

Metrics may be computed from active registrations, durable storage, provider-maintained summary tables, or provider-maintained projections, as long as durable-provider metrics do not depend only on worker-local memory.

### Dashboard Query Obligations

The core Jobs feature must expose enough provider-neutral query and management contracts for an operational dashboard or external support tool to be built without private provider-table access. The spec defines the required query/API obligations; it does not prescribe dashboard layout, navigation structure, visual components or first-party UI behavior.

Dashboard-oriented query models must:

- be built from active registrations plus persisted runtime state
- expose summary counts for jobs, triggers, occurrences, retries, batches, leases and scheduler instances
- expose paged list and detail models for active jobs, triggers, recurring triggers, occurrences, retry views, batches, executions, leases, scheduler instances, history and metrics
- include enough status, timestamp, correlation, retry, dependency, batch and error-summary information to avoid per-row provider lookups for ordinary support views
- separate compact safe previews from full serialized data, exception details and integration payloads
- respect host authorization, redaction and sensitive-data policies
- remain query contracts over scheduler state, not a product specification for a particular dashboard UI

A first-party dashboard UI is optional. If provided, it must use the same public query and management APIs as external tools.

### Administration API

The feature shall expose optional administration endpoints built on top of the query and management services.

The endpoints may map service-layer `Result`, `Result<T>` and `ResultPaged<T>` values into HTTP responses using the devkit's established Result-to-HTTP mapping approach.

The endpoint surface shall support:

- dashboard summary queries for counts, provider/runtime facts and timeline series
- active job definition list and detail queries
- trigger list and detail queries
- recurring trigger list and detail queries
- occurrence list and detail queries
- retry list and detail queries
- batch list and detail queries
- execution history queries
- lease diagnostic queries
- scheduler server/instance queries
- aggregated metrics queries
- job management actions such as enable, disable, pause and resume
- trigger management actions such as enable, disable, pause and resume for code-registered triggers
- execution control actions such as manual dispatch, cancel, interrupt and retry
- bulk execution control actions such as retry/requeue, cancel, delete/archive and purge for selected occurrence ids where supported
- batch control actions such as retry/requeue selected child occurrences, cancel, pause, resume and archive/delete
- maintenance actions such as archive, purge and repair operations when supported by the provider

Management endpoints and any operational UI must be securable through host application authentication and authorization.

### Endpoint API Contract

The endpoint layer should be sufficient to power an operational dashboard without requiring any additional private persistence access path.

The endpoint design shall align with the existing messaging, queueing and orchestration operational endpoints.

That means:

- successful read endpoints return `200 OK`
- successful control endpoints return `200 OK` with a descriptive success message or result body
- missing jobs, triggers or occurrences return `404 Not Found` with a plain text message
- invalid request bodies or query parameters return `400 Bad Request` using `ProblemDetails`
- valid requests that conflict with the current scheduler state return `409 Conflict` using `ProblemDetails`
- unexpected failures return `500 Internal Server Error` using `ProblemDetails`

The endpoint contract shall expose routes shaped like:

```text
GET    /_bdk/api/jobs/dashboard
GET    /_bdk/api/jobs/dashboard/navigation
GET    /_bdk/api/jobs/dashboard/overview
GET    /_bdk/api/jobs/dashboard/timeline
GET    /_bdk/api/jobs
GET    /_bdk/api/jobs/{jobName}
GET    /_bdk/api/jobs/{jobName}/triggers
GET    /_bdk/api/jobs/{jobName}/triggers/{triggerName}
GET    /_bdk/api/jobs/recurring
GET    /_bdk/api/jobs/recurring/{jobName}/{triggerName}
GET    /_bdk/api/jobs/occurrences
GET    /_bdk/api/jobs/occurrences/{occurrenceId}
GET    /_bdk/api/jobs/occurrences/{occurrenceId}/history
GET    /_bdk/api/jobs/retries
GET    /_bdk/api/jobs/executions
GET    /_bdk/api/jobs/batches
GET    /_bdk/api/jobs/batches/{batchId}
GET    /_bdk/api/jobs/batches/{batchId}/occurrences
GET    /_bdk/api/jobs/leases
GET    /_bdk/api/jobs/servers
GET    /_bdk/api/jobs/metrics
POST   /_bdk/api/jobs/{jobName}/dispatch
POST   /_bdk/api/jobs/{jobName}/enable
POST   /_bdk/api/jobs/{jobName}/disable
POST   /_bdk/api/jobs/{jobName}/pause
POST   /_bdk/api/jobs/{jobName}/resume
POST   /_bdk/api/jobs/{jobName}/triggers/{triggerName}/enable
POST   /_bdk/api/jobs/{jobName}/triggers/{triggerName}/disable
POST   /_bdk/api/jobs/{jobName}/triggers/{triggerName}/pause
POST   /_bdk/api/jobs/{jobName}/triggers/{triggerName}/resume
POST   /_bdk/api/jobs/occurrences/{occurrenceId}/cancel
POST   /_bdk/api/jobs/occurrences/{occurrenceId}/interrupt
POST   /_bdk/api/jobs/occurrences/{occurrenceId}/retry
POST   /_bdk/api/jobs/occurrences/{occurrenceId}/archive
POST   /_bdk/api/jobs/occurrences/{occurrenceId}/repair/release-lease
POST   /_bdk/api/jobs/occurrences/bulk/retry
POST   /_bdk/api/jobs/occurrences/bulk/cancel
POST   /_bdk/api/jobs/occurrences/bulk/archive
POST   /_bdk/api/jobs/batches
POST   /_bdk/api/jobs/batches/{batchId}/attach
POST   /_bdk/api/jobs/batches/{batchId}/retry
POST   /_bdk/api/jobs/batches/{batchId}/cancel
POST   /_bdk/api/jobs/batches/{batchId}/pause
POST   /_bdk/api/jobs/batches/{batchId}/resume
POST   /_bdk/api/jobs/batches/{batchId}/archive
DELETE /_bdk/api/jobs/occurrences
```

The dashboard endpoints are convenience read models over the same query services used by the detailed resources. They must not bypass provider-neutral query abstractions or expose provider tables directly. Dashboard endpoints may combine summary counts, provider/runtime facts and timeline buckets, but their contract should stay focused on scheduler state and must not prescribe a particular UI composition.

Dashboard timeline query parameters should support at least `from`, `to`, `bucket`, `mode`, `jobName`, `triggerName`, `schedulerInstanceId` and status filters. Timeline buckets should use UTC start/end timestamps and counts grouped by public status enums.

Query parameters for `GET /_bdk/api/jobs` shall support at least:

- `jobName`
- `group`
- `module`
- `enabled`
- `includeOrphanedRuntimeState`
- `skip`
- `take`
- `sortBy`
- `sortDescending`

Job definition list models shall include status facet counts for operational clients. At minimum, the response should expose counts for enabled, disabled, paused, orphaned runtime state and failed latest execution where those concepts are available.

Query parameters for `GET /_bdk/api/jobs/occurrences` and `GET /_bdk/api/jobs/executions` shall support at least:

- `jobName`
- `triggerName`
- `triggerType`
- `statuses`
- `schedulerInstanceId`
- `correlationId`
- `idempotencyKey`
- `dueFrom`
- `dueTo`
- `startedFrom`
- `startedTo`
- `completedFrom`
- `completedTo`
- `skip`
- `take`
- `sortBy`
- `sortDescending`

Occurrence list models shall support operational list views. Each row should include at least:

- occurrence id and a short id/display id
- job name, display name and invocation display
- trigger name and trigger type
- current occurrence status and display severity
- queue/group/module where applicable
- attempt number and max attempts where applicable
- created UTC, due UTC, started UTC, completed UTC and next retry UTC where applicable
- scheduler instance id and lease owner where applicable
- correlation id and idempotency key where available
- concise reason/error summary for failed, cancelled, interrupted or retry-scheduled states

Occurrence detail models shall include:

- the same row summary fields
- sanitized job data and properties previews
- parameter names and values when safe to expose
- full attempt summary with execution ids, status, duration, result messages and error summary
- state history timeline records
- lease and recovery properties
- links or ids for batch, source feature, source id, causation id and related orchestration/message/queue records where available

`GET /_bdk/api/jobs/retries` is a filtered occurrence view optimized for retry pages. It shall return retry-scheduled or retryable failed occurrences with retry attempt count, max attempts, reason/error summary, retry due time, created time, job display and paging properties. It shall support the same paging and page-size options as occurrence lists.

`GET /_bdk/api/jobs/batches` shall return batch summaries. Each row should include batch id, display id, description, current status, progress percentage, child occurrence counts by status, created UTC, started UTC, completed UTC, latest failure summary and whether bulk operations are available.

`GET /_bdk/api/jobs/batches/{batchId}` shall return:

- batch id and description
- current status and progress percentage
- child occurrence counts by state
- created, started, completed and updated timestamps
- safe properties and correlation values
- capability flags for retry, cancel, pause, resume, archive/delete and selected-child operations

`GET /_bdk/api/jobs/batches/{batchId}/occurrences` shall support filtering by child occurrence status and the same paging, sorting and selected bulk-action model as occurrence lists.

`GET /_bdk/api/jobs/recurring` shall return recurring trigger rows with job name, trigger name, display name, cron/calendar expression, time zone, enabled/paused state, last occurrence, next due occurrence, last execution status, missed-occurrence policy and safe properties.

`GET /_bdk/api/jobs/servers` shall return scheduler server/instance rows with scheduler instance id, host name, process id where available, app version where available, started UTC, last heartbeat UTC, heartbeat age, queues/groups/modules handled, worker slot count, active execution count, acquired lease count and status such as `Active`, `Stale` or `Offline`.

HTTP query values for status filters shall use the public enum member names. Endpoint models should accept route and query strings for natural HTTP binding, then validate and normalize them before calling runtime or query services. Internal query services should bind status filters to `JobOccurrenceStatus`, `JobExecutionStatus`, `JobBatchStatus` or `JobSchedulerServerStatus` instead of passing raw strings through the application.

Query parameters for `GET /_bdk/api/jobs/metrics` shall support the same filter subset that is meaningful for aggregated metrics.

Operational list endpoints shall support page sizes commonly used by support tools, including `10`, `20`, `50`, `100`, `500`, `1000` and `5000`, while still allowing hosts to configure a maximum page size. Requests above the configured maximum shall return `400 Bad Request` or be capped according to explicit endpoint options.

The `POST /dispatch` request body shall support at least:

```csharp
public class JobDispatchRequest
{
    public string TriggerName { get; set; }
    public string CorrelationId { get; set; }
    public string IdempotencyKey { get; set; }
    public object Data { get; set; }
    public IDictionary<string, string> Properties { get; set; }
}
```

Runtime trigger endpoints operate only on existing code-registered triggers. They must not accept request bodies that define trigger type, schedule options or trigger data. Definition changes are made in code and become active on application startup after validation.

The `POST /pause`, `POST /resume`, `POST /cancel`, `POST /interrupt`, `POST /retry`, `POST /archive` and repair endpoints shall support a reason-oriented request body where meaningful:

```csharp
public class JobOperationReasonRequest
{
    public string Reason { get; set; }
}
```

Bulk occurrence action endpoints shall support a selected-id request body:

```csharp
public class JobBulkOccurrenceOperationRequest
{
    public IReadOnlyList<Guid> OccurrenceIds { get; set; }
    public string Reason { get; set; }
}
```

Bulk action responses shall include requested count, succeeded count, failed count and per-occurrence failures for invalid state, not found, authorization failure or provider failure.

Batch creation endpoints shall use the same request shapes as `CreateBatchAsync(...)`, `DispatchBatchAsync(...)` and `AttachToBatchAsync(...)`. A batch creation request with no child items creates an empty completed batch. A batch dispatch or attach request with child items must atomically accept the batch operation or return a `Result`/HTTP failure without leaving runnable orphaned occurrences.

The `DELETE /_bdk/api/jobs/occurrences` endpoint shall support purge-style query parameters comparable to the retained operational endpoints used by queueing, messaging and orchestration, for example:

- `olderThan`
- `statuses`
- `jobName`
- `triggerName`
- `isArchived`

The `statuses` values on purge endpoints shall use `JobOccurrenceStatus` enum names.

The `ProblemDetails` contract shall use stable job-specific `type` values, for example:

- `/problems/jobs/validation`
- `/problems/jobs/not-found`
- `/problems/jobs/invalid-state`
- `/problems/jobs/concurrency-conflict`
- `/problems/jobs/unsupported-operation`
- `/problems/jobs/provider-failure`

---

## Testing

Jobs shall be easy to test with xUnit without requiring full application hosting, a real database, background worker threads, or wall-clock waiting.

The Jobs feature should provide test helper utilities for:

- unit testing job logic directly
- testing `IJobExecutionContext` behavior
- testing typed data handling
- testing previous-run/delta behavior
- testing trigger materialization
- testing retries, timeouts, cancellation and failure handling
- testing execution history and operational state
- testing scheduler behavior with in-memory persistence

### Testing Utilities

The feature shall provide job-focused test utilities so job authors can test jobs and scheduler behavior with low friction.

The testing utility contract shall support at least:

- creating a job execution context without starting a hosted scheduler
- supplying typed data, properties, correlation id, scheduler instance id and cancellation tokens
- supplying `PreviousExecution` and `PreviousSuccessfulExecution` records
- executing a job inline for deterministic unit tests
- registering jobs and triggers in a test scheduler runtime
- using in-memory persistence suitable for unit tests
- controlling time for cron, delayed, one-time and startup-delay trigger tests
- advancing scheduler time without waiting for real time to pass
- materializing due trigger occurrences deterministically
- running one due occurrence, all due occurrences, or a bounded batch
- asserting execution result, context messages, occurrence status and execution history
- asserting retry scheduling, timeout handling, cancellation and lease recovery behavior
- substituting fake cron, clock, lease, trigger adapter and persistence infrastructure where suitable
- adding test services to the job dependency injection container

The testing surface should make common job tests easy without requiring the test to understand provider internals or run a real hosted service. Integration tests can still use the normal host and EF provider when database behavior is the subject of the test.

### Job Unit Test Harness

The simplest testing path should execute a single job with a synthetic `IJobExecutionContext`.

The intended unit-test surface should provide a small job test harness around the normal job contract, for example:

- a test builder or fixture for creating a job instance through DI
- helpers for creating `IJobExecutionContext`
- typed data helpers
- previous-execution helpers
- assertion helpers for result, messages, context properties and context items
- cancellation helpers

Typical job logic testing should support a shape like:

```csharp
public class SyncCustomersJobTests
{
    [Fact]
    public async Task ExecuteAsync_should_sync_changes_since_previous_success()
    {
        var customers = Substitute.For<ICustomerRepository>();
        customers.FindChangedSinceAsync(
                new DateTimeOffset(2026, 05, 01, 10, 0, 0, TimeSpan.Zero),
                Arg.Any<CancellationToken>())
            .Returns([new CustomerChange("CUST-42")]);

        var harness = JobTestHarness.Create()
            .WithService(customers)
            .WithJob<SyncCustomersJob>("sync-customers")
            .WithPreviousSuccessfulExecution(previous => previous
                .CompletedUtc(new DateTimeOffset(2026, 05, 01, 10, 0, 0, TimeSpan.Zero)))
            .Build();

        var result = await harness.ExecuteAsync<SyncCustomersJob>();

        result.IsSuccess.ShouldBeTrue();
        harness.Context.Messages.ShouldContain(
            message => message.Contains("CUST-42", StringComparison.Ordinal));
    }
}
```

This example is illustrative of the intended authoring experience. The normative requirement is that job authors can instantiate and execute a job in an ordinary xUnit test with supplied dependencies and context data.

### Scheduler Test Harness

The feature should also make it easy to test scheduling behavior without a real background service.

The intended scheduler test harness should provide:

- in-memory job and trigger registration
- in-memory occurrence, lease and history persistence
- deterministic inline execution
- a controllable test clock
- helper methods for advancing time, materializing triggers and running due work
- helper methods for loading jobs, triggers, occurrences, leases and execution history
- assertion helpers for missed occurrences, retries and previous-run state

Typical scheduler behavior testing should support a shape like:

```csharp
public class RebuildSearchIndexJobTests
{
    [Fact]
    public async Task Cron_trigger_should_create_and_run_due_occurrence()
    {
        var harness = JobSchedulerTestHarness.Create()
            .WithClock(new DateTimeOffset(2026, 05, 08, 7, 59, 0, TimeSpan.Zero))
            .WithJob<RebuildSearchIndexJob>("search-index", job => job
                .WithName("Search Index Rebuild")
                .WithDescription("Rebuilds the search index for customer-facing queries.")
                .AddTrigger("weekday-morning", trigger => trigger
                    .Cron("0 8 * * MON-FRI")
                    .TimeZone(TimeZoneInfo.Utc)))
            .Build();

        await harness.AdvanceToAsync(
            new DateTimeOffset(2026, 05, 08, 8, 0, 0, TimeSpan.Zero));

        await harness.MaterializeDueTriggersAsync();
        var run = await harness.RunDueAsync();

        run.IsSuccess.ShouldBeTrue();

        var occurrences = await harness.GetOccurrencesAsync("search-index");
        occurrences.Value.ShouldContain(occurrence =>
            occurrence.TriggerName == "weekday-morning" &&
            occurrence.Status == JobOccurrenceStatus.Completed);
    }
}
```

The harness should make scheduler tests readable in terms of job behavior and scheduling outcomes rather than infrastructure setup.

### Test Clock

Time-based scheduler behavior must be testable without waiting for real time.

The test utilities shall provide a controllable clock that can be used by:

- cron triggers
- one-time triggers
- delayed triggers
- startup-delay triggers
- timeout handling
- retry backoff
- missed-occurrence recovery
- lease expiration and renewal tests

The runtime should depend on a time abstraction so tests can advance time deterministically and assert scheduled due times.

### Outbound Integration Testing

Outbound job integrations should be testable without real Requester, Notifier, Messaging, Queueing, or Orchestration infrastructure.

The test utilities should support:

- substituting fake or mocked `IMessageBroker`, `IQueueBroker`, `INotifier`, `IRequester`, and `IOrchestrationService`
- mapping job data and properties into typed outbound payloads
- supplying correlation, causation, and idempotency values
- asserting accepted, failed, and retried outbound integration steps
- asserting resulting messages, errors, and execution history

### Acceptance Criteria

The testing support is ready when:

- a job can be executed in an xUnit test by providing dependencies and an `IJobExecutionContext`
- a test can provide typed data, properties and previous-run information
- a test can assert `Result`, messages, history and status without querying provider internals
- a test can run scheduler behavior with in-memory persistence and a controllable clock
- a test can materialize and execute cron, one-time, delayed, startup-delay, and manual triggers deterministically
- a test can substitute outbound feature abstractions and assert mapped payloads without real brokers or orchestrations
- retry, timeout, cancellation and lease recovery behavior can be tested without sleeping or relying on real background worker timing

---

## Execution Lifecycle

Job execution progresses through a well-defined lifecycle from trigger materialization to terminal completion. The lifecycle is occurrence-driven: triggers create occurrences, workers acquire occurrences, and each execution attempt records what happened while trying to run the job.

The runtime must distinguish:

- **Job registration lifecycle**: code-first registration, enable/disable defaults, startup validation, appsettings override merge, update through code changes and removal from active registration when code no longer registers the job.
- **Trigger registration lifecycle**: code-first registration, enable/disable defaults, startup validation, appsettings override merge and removal from active registration when code no longer registers the trigger.
- **Runtime-state lifecycle**: durable operational overrides, pause/resume state, materialization watermarks and stale/orphaned state after registrations are removed.
- **Occurrence lifecycle**: the scheduled unit of work created by a trigger.
- **Execution attempt lifecycle**: one attempt to run a materialized occurrence.

Operational queries and persisted history must make this distinction visible.

### Occurrence Lifecycle Phases

- **Materialized**

  - A trigger has produced a concrete occurrence.
  - The occurrence has a stable identifier, job name, trigger name, trigger type, due time, data, properties and correlation data.
  - Durable providers persist the occurrence before it can be acquired for execution.

- **Scheduled**

  - The occurrence exists but is not yet due.
  - Time-based occurrences remain scheduled until their due UTC instant is reached.
  - Manual and event-based occurrences may skip this phase when they are immediately due.

- **Due**

  - The occurrence is eligible for execution.
  - A worker may attempt to acquire a lease when the occurrence is due, enabled, not paused, and allowed by concurrency rules.
  - Due occurrences are ordered by priority, due time and provider-defined stable tie-breakers.

- **Blocked**

  - The occurrence is due but cannot run because a dependency, concurrency limit, pause state, host target or operational condition prevents execution.
  - Blocked state must be queryable when the provider can represent it explicitly.
  - If blocked state is derived rather than persisted, the query model must still explain why the occurrence is not currently runnable.

- **Leased**

  - A scheduler instance has acquired exclusive provider-backed ownership of the occurrence.
  - The lease owner and expiration data are persisted or durably coordinated.
  - Only the lease owner may start or finalize execution for that occurrence.

- **Running**

  - An execution attempt has started for the occurrence.
  - `IJobExecutionContext` has been hydrated and `IJob.ExecuteAsync(...)` is executing.
  - Cancellation, timeout, lease renewal and operational interrupt requests are active concerns while running.

- **RetryScheduled**

  - An execution attempt failed or timed out and a retry policy selected a later retry.
  - Retry properties is persisted before the occurrence becomes eligible again.
  - The occurrence remains linked to its original scheduled intent and previous attempts.

- **Paused**

  - An operator or management policy has suspended execution progression for the job, trigger or occurrence.
  - No new execution attempt may start while the occurrence is paused.
  - Existing running attempts should receive cancellation or interrupt requests only when the pause operation explicitly requests it.

- **Completed**

  - The occurrence finished successfully.
  - Completion properties, result messages, duration and history are persisted.
  - Completed occurrences are no longer runnable but remain queryable until archived or purged according to retention settings.

- **Failed**

  - The occurrence has no remaining retry or recovery path.
  - Failure properties, error details, final attempt information and history are persisted.
  - Failed occurrences may be manually retried when the management API allows the transition.

- **Cancelled**

  - The occurrence was cancelled before or during execution.
  - Cancellation reason, actor properties when available, timestamps and history are persisted.
  - Cancelled occurrences are terminal unless a management operation explicitly dispatches a replacement occurrence or starts an allowed retry attempt.

- **Archived**

  - The occurrence is retained for inspection but removed from active operational views.
  - Archived occurrences are not runnable.
  - Archived records remain inspectable until purged.

### Execution Attempt Lifecycle Phases

Execution attempts are durable execution records linked to one occurrence. Retries create additional execution records for the same occurrence; they do not create retry occurrences and do not erase previous attempts.

- **Created**

  - The runtime has decided to run an occurrence and prepares an attempt record.
  - Attempt number and owning scheduler instance are assigned.

- **Started**

  - The attempt has begun.
  - The runtime has created a DI scope, resolved the job, hydrated `IJobExecutionContext`, and recorded started UTC.

- **Running**

  - The job implementation is actively executing.
  - Timeout, cancellation token propagation, lease renewal and behavior decorators operate around the execution.

- **Succeeded**

  - `ExecuteAsync(...)` returned a successful `Result` or completed according to the configured success contract.
  - The occurrence may transition to `Completed`.

- **Failed**

  - `ExecuteAsync(...)` returned a failed `Result` or threw an exception.
  - The runtime evaluates retry policy and either schedules a retry or marks the occurrence failed.

- **TimedOut**

  - The configured timeout elapsed.
  - The runtime requests cancellation and records timeout properties.
  - Timeout may lead to retry, failure or cancellation depending on policy.

- **Cancelled**

  - Cancellation was requested and the attempt ended as cancelled.
  - Cancellation may come from shutdown, an operator action, timeout handling or a cooperative job decision.

- **Interrupted**

  - An operator requested interruption of a running attempt.
  - The runtime requests cancellation and records the interrupt reason separately from ordinary cancellation.

- **Finalized**

  - The attempt outcome has been persisted.
  - The lease has been released, renewed for retry scheduling, or allowed to expire according to the provider contract.

### Job Execution Outcomes

Each execution attempt produces an outcome that controls occurrence progression.

Supported outcomes should include:

- **Success**

  - The job completed successfully.
  - The occurrence transitions to `Completed`.

- **Failure**

  - The job returned a failed `Result` or threw an exception.
  - Retry policy is evaluated before the occurrence becomes terminal.

- **Retry**

  - The runtime schedules another attempt according to retry policy.
  - Retry intent and next due UTC are persisted before the occurrence becomes eligible again.

- **Cancel**

  - Execution ends in a controlled cancelled state.
  - This can be requested by an operator, shutdown, timeout handling or job logic through the execution context where supported.

- **Timeout**

  - The runtime detected that the attempt exceeded its configured maximum runtime.
  - Timeout is recorded as a distinct outcome so operations can distinguish it from ordinary failure.

- **Interrupt**

  - An operator requested a running attempt to stop.
  - Interrupt is recorded as a distinct outcome so operations can distinguish it from cancellation caused by shutdown or timeout.

Business rejection or expected negative domain outcomes should be represented with `Result` messages and application-specific result data. Unexpected technical failures should be represented by exceptions or failed results and handled through the scheduler failure pipeline.

### Pause and Resume Semantics

Pausing is externally imposed operational control. It is not the same as a scheduled delay or a job waiting for future work.

Pausing may target:

- an active job definition
- an active trigger definition
- a materialized occurrence

While paused:

- active trigger definitions should not materialize new occurrences when the trigger or job is paused
- existing due occurrences must not start new execution attempts when their job, trigger or occurrence is paused
- running executions continue unless the pause operation also requests cancellation or interruption
- pause reason, actor properties when available, and timestamps are persisted

Resuming re-enables normal scheduling and execution progression. If a paused occurrence became overdue while paused, it is handled according to its missed-occurrence and recovery policy after resume.

### Cancellation and Interruption Semantics

Cancellation and interruption are distinct operational concepts.

- **Cancellation** is a request to stop scheduled or running work in a controlled way and usually moves the occurrence toward a terminal `Cancelled` state.
- **Interruption** is a request to stop the currently running attempt while preserving the possibility of retry or later recovery depending on policy.

The runtime shall:

- persist cancellation or interruption request properties
- propagate cancellation tokens to running jobs
- record whether the job cooperatively observed cancellation
- prevent stale workers from finalizing occurrences after lease loss
- expose cancellation and interruption outcomes in history and metrics

### Retry Progression

Retry is part of the occurrence lifecycle, not a separate job definition or trigger.

When retry is selected:

1. The failed attempt is finalized in its execution record and append-only lifecycle history.
2. Retry policy calculates the next retry due UTC.
3. Retry properties is persisted.
4. The same occurrence transitions to `RetryScheduled` or equivalent provider state.
5. When retry due time arrives, the occurrence becomes due again and must be reacquired through the normal lease pipeline.

Retry attempts must preserve enough history to answer:

- how many attempts were made
- which scheduler instances executed them
- why each attempt failed or timed out
- when the next retry became eligible
- why retry eventually stopped, if it did

### Lease and Recovery Transitions

Lease state controls who may mutate a durable occurrence.

The lifecycle rules are:

- an occurrence must be due and runnable before a worker can acquire a lease
- an occurrence must be leased before a durable execution attempt starts
- lease renewal must occur for long-running jobs before lease expiration
- final execution state may only be persisted by a worker that still owns a valid lease
- expired leases make occurrences eligible for recovery by another scheduler instance
- recovered occurrences continue from the latest persisted occurrence and execution state

Lease recovery may transition an occurrence back to `Due`, `RetryScheduled`, `Failed` or another provider-supported recovery state depending on the latest persisted attempt and configured recovery policy.

### State Transition Rules

The scheduler shall enforce predictable lifecycle transitions.

At minimum:

- `Materialized` may transition to `Scheduled`, `Due`, `Cancelled` or `Archived`.
- `Scheduled` may transition to `Due`, `Paused`, `Cancelled` or `Archived`.
- `Due` may transition to `Blocked`, `Leased`, `Paused`, `Cancelled` or `Archived`.
- `Blocked` may transition back to `Due` when the blocking condition is removed, or to `Cancelled`/`Archived` through operations.
- `Leased` may transition to `Running`, `Due` after lease recovery, or `Cancelled` when cancellation happens before execution starts.
- `Running` may transition to `Completed`, `RetryScheduled`, `Failed` or `Cancelled` through attempt finalization. Timeout and interruption are recorded on the execution attempt and then mapped to the configured occurrence outcome.
- `RetryScheduled` may transition to `Due`, `Paused`, `Cancelled` or `Archived`.
- `Paused` may transition back to the previous runnable state, or to `Cancelled`/`Archived` through operations.
- `Completed`, `Failed` and `Cancelled` are terminal occurrence states unless an explicit management action starts an allowed retry attempt or dispatches a replacement occurrence.
- `Archived` is terminal for active scheduling and can only transition to purge/removal.

Invalid transitions must return `Result`/`Result<T>` failures from management APIs and should be represented as `409 Conflict` by operational endpoints.

### Lifecycle History

Lifecycle changes must be visible in execution history.

Execution records and lifecycle history serve different purposes:

- `JobExecution` records summarize attempts and are the primary source for attempt status, timestamps, duration, result/error summary, scheduler instance and attempt number.
- `JobExecutionHistory` records are append-only timeline entries for occurrence changes, execution attempt changes, lease events, retry scheduling, operator actions and diagnostics.

The history model should record at least:

- occurrence materialization
- due-state changes
- blocking and unblocking where represented explicitly
- lease acquisition, renewal, release, expiration and recovery
- execution attempt start
- execution attempt completion
- failure, timeout, cancellation and interruption
- retry scheduling and retry exhaustion
- pause and resume operations
- manual dispatch and operator-initiated retry
- archival and purge actions where retained

History records should include UTC timestamps, scheduler instance id, actor identity when available, reason text when supplied, correlation id and enough properties to reconstruct the operational story of an occurrence.

---

## Job Context

Each job execution operates with a dedicated `IJobExecutionContext`.

The job context is the shared execution contract passed to `IJob.ExecuteAsync(...)`. It gives the job access to scheduler properties, typed input data, previous-run information, messages, correlation data, cancellation state and controlled scheduler operations. `Application.Jobs` may provide concrete `JobExecutionContext` implementations, but public job implementations should depend on the interfaces.

Unlike an orchestration context, a job context is execution-scoped. It is not intended to be a long-lived workflow state snapshot. Durable business state should be stored in application-owned persistence. The scheduler persists the runtime facts needed to explain, retry, recover and inspect the job execution.

### Context Responsibilities

The job context contains:

- **Execution identity and status**

  - job name
  - job type identity
  - trigger name
  - trigger type
  - occurrence identifier
  - execution identifier
  - attempt number
  - scheduler instance id
  - correlation identifier
  - idempotency key when available
  - scheduled UTC, due UTC, started UTC and completed UTC where available
  - current status and derived runtime information

- **Data**

  - optional trigger, dispatch or event input data
  - typed data access through `ctx.Data` for jobs configured with typed data
  - untyped data access for generic infrastructure and diagnostics
  - input source properties, such as manual dispatch, cron, notification, message or queue adapter

- **Properties and items**

  - immutable or scheduler-owned properties from the job definition, trigger and occurrence
    - execution-scoped items for values produced during the current attempt
  - correlation and diagnostic properties used by logs, telemetry and history

- **Previous run information**

  - previous execution attempt for the same occurrence, when the current attempt is not the first attempt
  - previous successful execution for the same job/trigger identity before the current occurrence
  - optional job-level previous successful execution across triggers before the current occurrence
  - previous status, timestamps, data source properties, result messages and error details when retained

- **Runtime services**

  - cancellation token visibility
  - current time through the scheduler time abstraction
  - optional scoped service provider access for advanced scenarios
  - logging or telemetry hooks where exposed through devkit abstractions

- **Execution output**

  - messages produced by the job
  - warnings or diagnostics
    - execution-scoped result items
  - optional tags/properties to persist into execution history

- **Control operations**

  - request retry where policy allows it
  - request cancellation or cooperative stop
  - request pause or follow-up scheduling where supported
  - publish execution messages or progress updates

### Context Characteristics

- The `IJobExecutionContext` itself is always present.
- Typed data is optional; jobs without data still receive a context.
- Jobs should read scheduler-provided runtime data through the context rather than querying scheduler storage directly.
- Jobs should use constructor dependency injection for ordinary application services.
- Context service-provider access, if exposed, is an advanced escape hatch and should not replace constructor injection for normal job dependencies.
- Job execution is context-centered, but job results are still returned through `Result` or `Result<T>`.
- Context messages and execution items supplement the returned result; they do not replace the result contract.
- Context items are execution-scoped unless explicitly persisted into execution history by the runtime.
- Context types and data must remain serialization-friendly when durable providers persist them.
- Context should not hold non-durable live resources as persisted data, such as open streams, database connections or unmanaged handles.

### Typed Data Access

Jobs with structured input should be able to use typed data access.

The primary data contract is the job base type:

- `JobBase<TData>` declares that the job expects `TData`.
- `JobBase` is shorthand for `JobBase<Unit>` and declares that the job has no data.
- `WithData<TData>()` is optional when the job type already inherits `JobBase<TData>`.
- `WithData<TData>()` can still be used as explicit properties, for non-generic `IJob` implementations, or when fluent configuration must declare the data contract for generated/dynamic job definitions.
- If both `JobBase<TData>` and `WithData<TOther>()` are present and the types differ, startup validation must fail.
- The typed input is exposed through the execution context as `ctx.Data`.

The feature should support either a typed context or an equivalent typed accessor model:

```csharp
public class GenerateInvoiceJob : JobBase<GenerateInvoiceRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<GenerateInvoiceRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        var invoiceId = ctx.Data.InvoiceId;

        ctx.Messages.Add($"Generating invoice {invoiceId}.");

        // job work omitted

        return Result.Success();
    }
}
```

Equivalent APIs are acceptable if they preserve:

- compile-time data type clarity at the job boundary
- validation when the persisted data cannot be deserialized into the expected type
- access to raw data source properties for diagnostics
- provider-neutral serialization through `ISerializer`

Jobs without data use `Unit`:

```csharp
public class RebuildSearchIndexJob : JobBase
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<Unit> ctx,
        CancellationToken cancellationToken = default)
    {
        // no data is expected
        return Result.Success();
    }
}
```

### Previous Run Context

Previous-run information is part of the job context because many scheduled jobs process deltas.

The context should expose:

- `PreviousExecution`: the immediately preceding attempt for the same occurrence, populated only when a previous attempt exists for the occurrence
- `PreviousSuccessfulExecution`: the most recent successful execution for the same job/trigger identity before the current occurrence
- optional job-level previous successful execution across all triggers before the current occurrence
- previous data source properties when retained
- previous messages and error summaries when retained
- previous started, completed and duration values

Previous-run lookup should happen before invoking `ExecuteAsync(...)` so the job can use this information without directly depending on scheduler persistence abstractions. The lookup must exclude the current occurrence when resolving `PreviousSuccessfulExecution` so retry attempts do not shift the delta boundary.

### Context Control Operations

The context may expose controlled operations that communicate intent back to the scheduler runtime.

Supported operations should include:

- adding execution messages
- adding execution items or tags
- requesting retry with a reason
- requesting cancellation with a reason
- requesting pause with a reason where policy allows it
- checking whether cancellation or interruption has been requested
- reporting progress for operational inspection where supported

Context control operations must not bypass scheduler policy. For example, requesting retry does not guarantee retry if retry policy is exhausted or the occurrence is no longer eligible.

### Context Persistence Rules

The job context is part of the durable runtime contract, but it is not persisted as a full mutable workflow snapshot.

The runtime shall persist the durable parts of the context at meaningful boundaries:

- occurrence data and properties when the occurrence is materialized
- execution items when an attempt starts
- previous-run references or enough identifiers to reconstruct them where needed
- context messages and execution items selected for history when an attempt completes, fails, times out, is cancelled or is interrupted
- result messages, errors, exception properties and duration at attempt finalization
- correlation id and idempotency key throughout occurrence and execution history

The runtime should not persist arbitrary live context objects. Persisted context data must be serializable, provider-neutral and safe to inspect through operations APIs.

### Context and Idempotency

The context helps jobs make idempotent decisions, but it does not make job side effects idempotent by itself.

Jobs should use context data such as occurrence id, idempotency key, previous successful execution and correlation id when coordinating with application storage or external systems.

For delta processing, the preferred pattern is:

1. Read `PreviousSuccessfulExecution` from the context.
2. Use its completion timestamp or checkpoint properties as the lower bound.
3. Process changed application data.
4. Return `Result.Success()` with messages or execution items that explain the processed range.

### Context Testing

The testing utilities shall make `IJobExecutionContext` easy to construct in unit tests.

Tests should be able to supply:

- job, trigger, occurrence and execution identifiers
- typed data
- properties and items
- previous execution records
- correlation id and idempotency key
- cancellation tokens
- test clock values
- expected context messages and output items

---

## Execution Semantics and Constraints

Execution semantics describe the rules the scheduler must follow when deciding what work exists, what can run, and how outcomes are recorded.

The Jobs feature is not a workflow engine. It executes independent job occurrences created by triggers. Dependencies and chaining may connect occurrences, but each occurrence still follows the scheduler lifecycle and provider lease rules.

### Deterministic Scheduling Behavior

Scheduler decisions must be derivable from registered definitions, trigger state, persisted occurrences and provider state.

For durable providers:

- trigger materialization must be idempotent
- occurrence identity must be deterministic enough to prevent duplicates across restarts and multi-node scans
- due occurrence selection must use stable ordering such as priority, due UTC and provider-defined tie-breakers
- retry eligibility must be derived from persisted attempt and retry properties
- missed-occurrence recovery must use persisted trigger runtime state, watermarks and due UTC values
- a worker must not rely on worker-local memory as the only source for durable scheduling decisions

For in-memory providers, the same observable behavior should be preserved within a single process lifetime, but durable recovery across restarts is not required.

### Idempotency Expectation

Jobs may interact with databases, file systems, message brokers, APIs or other external systems. Therefore job implementations must tolerate re-execution.

Re-execution can happen when:

- retry policy schedules another attempt
- a worker crashes after producing a business side effect but before final scheduler state is persisted
- a lease expires and another node recovers the occurrence
- event-based triggers receive duplicate source events
- an operator manually retries a failed occurrence

The scheduler provides occurrence identity, idempotency keys, previous-run data and execution history to help job authors implement idempotency, but it cannot make external business side effects idempotent by itself.

### Lease and Ownership Constraints

Durable execution is constrained by provider-backed ownership.

The runtime shall enforce:

- a durable occurrence must be leased before execution starts
- at most one scheduler instance may hold the active lease for an occurrence
- lease ownership must be verified before final state is persisted
- a worker that loses its lease may not finalize the occurrence
- lease renewal must be used for long-running jobs
- abandoned leases must be recoverable after expiration

Lease constraints are occurrence-scoped. They prevent concurrent execution of the same occurrence, but they do not prevent two different occurrences of the same job from running concurrently unless job, trigger, group or scheduler concurrency settings require that.

### Concurrency Constraints

Concurrency is configurable at multiple levels.

The scheduler should support:

- global worker pool concurrency
- per-job concurrency
- per-trigger concurrency
- optional group or module concurrency
- host or scheduler instance targeting

Concurrency limits must be evaluated before a due occurrence is started. When concurrency prevents execution, the occurrence should remain due or be represented as blocked so operations can explain why it is not running.

Concurrency constraints must not be implemented only inside the job implementation. They are scheduler concerns and must be visible through runtime state, metrics and operational queries.

### Time Semantics

Time-based decisions must use scheduler-owned time abstractions.

The runtime shall:

- calculate scheduled and due instants in UTC
- use explicit `TimeZoneInfo` values for cron and calendar trigger calculations
- use the devkit cron abstraction for cron parsing and next occurrences
- avoid `DateTime.Now` and ambient local-time decisions inside scheduler logic
- expose the scheduler clock to tests so time can be advanced deterministically

Daylight-saving-time behavior, missed occurrences, delayed triggers, retry backoff, timeout handling and lease expiration must all be testable without sleeping on real time.

### Pause and Resume

Jobs may be paused externally through management operations.

Pausing may target:

- an active job definition
- an active trigger definition
- an occurrence

While paused:

- paused jobs and triggers must not materialize new occurrences
- paused occurrences must not start new execution attempts
- running executions continue unless cancellation or interruption is requested explicitly
- pause properties and reason should be persisted for durable providers

Resuming re-enables normal scheduler progression from the latest persisted state. If work became overdue while paused, missed-occurrence and recovery policies decide what is eligible after resume.

### Cancellation, Timeout and Shutdown

Cancellation is cooperative.

The runtime shall:

- pass a `CancellationToken` to every job execution
- request cancellation during graceful scheduler shutdown
- request cancellation when an operator cancels or interrupts work
- request cancellation when timeout expires
- record cancellation, interruption and timeout outcomes distinctly
- release, renew or expire leases according to final persisted state

Jobs are expected to honor cancellation promptly. If a job ignores cancellation and the lease expires, another node may recover the occurrence according to the lease contract.

### Programmatic API

Jobs can be dispatched and controlled directly from application code.

The programmatic API should support:

- dispatching a job immediately
- dispatching a job and waiting for completion
- enabling, disabling, pausing and resuming code-registered triggers
- pausing and resuming jobs, triggers or occurrences
- cancelling or interrupting running work
- retrying failed occurrences
- querying active job definitions, triggers, occurrences, executions, leases, history and metrics

Programmatic dispatch must still use the active job definition, context, retry, timeout, cancellation, history and lease semantics unless the caller explicitly selects a transient no-history mode.

### Runtime Service API

The feature shall expose a clear application-facing service API for job scheduling, execution, control and querying.

Client-facing job scheduler service methods shall follow the devkit Result pattern as described in [Results Feature](../features-results.md).

Public runtime and query methods shall therefore return `Result`, `Result<T>` or `ResultPaged<T>` so callers can inspect success, failure, messages and errors explicitly instead of inferring runtime failure from exceptions or ad-hoc status handling.

This requirement applies to application-facing services. It does not require internal runtime components, persistence providers, execution helpers or other non-client-facing implementation methods to use the Result pattern internally.

Public scheduler contracts should use simple `string` and `Guid` identifiers for stable job, trigger, occurrence, execution, batch and scheduler-instance identities. The argument and property names are part of the contract and make the identifier role clear without adding wrapper types to the public API. Implementations should still validate and normalize names at the boundary, including required values, length limits and allowed characters.

Dispatch data is intentionally opaque at the scheduler boundary. The job-type overload accepts `object data` so callers do not have to repeat generic data arguments, while the runtime still validates the supplied value against the registered job data type before accepting an occurrence. String-based dispatch exists for endpoint binding and operational tooling and follows the same validation rules.

The public service surface should distinguish between:

- **runtime/control operations**

  - dispatch, dispatch-and-wait, pause, resume, cancel, interrupt, retry and trigger management

- **query operations**

  - load active job definitions merged with runtime state, triggers, recurring triggers, occurrences, retry views, batches, executions, history, servers, leases, dashboard summaries and aggregated metrics

The runtime and query service shape should follow this responsibility model:

```csharp
public interface IJob
{
    Task<Result> ExecuteAsync(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default);
}

public interface IJob<TData> : IJob
{
    Task<Result> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default);
}

public interface IJobSchedulerService
{
    Task<Result<JobDispatchResult>> DispatchAsync<TJob>(
        object data = null,
        JobDispatchOptions options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    Task<Result<JobDispatchResult>> DispatchAsync(
        string jobName,
        object data = null,
        JobDispatchOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobExecutionResult>> DispatchAndWaitAsync<TJob>(
        object data = null,
        JobDispatchOptions options = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    Task<Result> EnableTriggerAsync(
        string jobName,
        string triggerName,
        CancellationToken cancellationToken = default);

    Task<Result> DisableTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> PauseTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> ResumeTriggerAsync(
        string jobName,
        string triggerName,
        CancellationToken cancellationToken = default);

    Task<Result> EnableJobAsync(
        string jobName,
        CancellationToken cancellationToken = default);

    Task<Result> DisableJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> PauseJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> ResumeJobAsync(
        string jobName,
        CancellationToken cancellationToken = default);

    Task<Result> CancelOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> InterruptOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> RetryOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobBulkOperationResult>> RetryOccurrencesAsync(
        IReadOnlyList<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobBulkOperationResult>> CancelOccurrencesAsync(
        IReadOnlyList<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobBulkOperationResult>> ArchiveOccurrencesAsync(
        IReadOnlyList<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobBatchDispatchResult>> CreateBatchAsync(
        JobBatchCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobBatchDispatchResult>> DispatchBatchAsync(
        JobBatchDispatchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobBatchDispatchResult>> AttachToBatchAsync(
        string batchId,
        JobBatchDispatchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobBulkOperationResult>> RetryBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobBulkOperationResult>> CancelBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> PauseBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result> ResumeBatchAsync(
        string batchId,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);
}

public interface IJobSchedulerQueryService
{
    Task<Result<JobDashboardModel>> GetDashboardAsync(
        JobDashboardRequest request = null,
        CancellationToken cancellationToken = default);

    Task<Result<JobDashboardNavigationModel>> GetDashboardNavigationAsync(
        CancellationToken cancellationToken = default);

    Task<Result<JobDashboardOverviewModel>> GetDashboardOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<Result<JobDashboardTimelineModel>> GetDashboardTimelineAsync(
        JobDashboardTimelineRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobDefinitionModel>> GetJobAsync(
        string jobName,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobDefinitionModel>> QueryJobsAsync(
        JobDefinitionQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<JobTriggerModel>>> GetTriggersAsync(
        string jobName,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobRecurringTriggerModel>> QueryRecurringTriggersAsync(
        JobRecurringTriggerQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobOccurrenceModel>> GetOccurrenceAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobOccurrenceModel>> QueryOccurrencesAsync(
        JobOccurrenceQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobRetryModel>> QueryRetriesAsync(
        JobRetryQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobBatchModel>> QueryBatchesAsync(
        JobBatchQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobBatchModel>> GetBatchAsync(
        string batchId,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobOccurrenceModel>> QueryBatchOccurrencesAsync(
        string batchId,
        JobOccurrenceQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<JobExecutionHistoryModel>>> GetHistoryAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobExecutionModel>> QueryExecutionsAsync(
        JobExecutionQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobLeaseModel>> QueryLeasesAsync(
        JobLeaseQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerServerModel>> QueryServersAsync(
        JobSchedulerServerQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobSchedulerMetricsModel>> GetMetricsAsync(
        JobSchedulerMetricsRequest request = null,
        CancellationToken cancellationToken = default);
}
```

Equivalent service names are allowed, but the feature shall preserve these responsibilities.

The typed `IJob<TData>` contract exists for registration-time data contract inference and typed execution context access. `JobBase<TData>` should bridge the non-generic `IJob.ExecuteAsync(IJobExecutionContext, ...)` call to the typed `ExecuteAsync(IJobExecutionContext<TData>, ...)` override after the runtime has validated and deserialized the occurrence data. The dispatch API intentionally accepts `object data` so callers can dispatch by job type without specifying duplicate generic type arguments; the scheduler validates the supplied data against the registered job data contract and returns a `Result` failure for mismatches.

Manual dispatch target rules:

- the target job must be enabled
- if `TriggerName` is supplied, it must identify an enabled manual trigger on the job
- if `TriggerName` is omitted and the job has exactly one enabled manual trigger, that trigger is used
- if `TriggerName` is omitted and the job has no manual trigger, dispatch should fail with a validation `Result`
- if `TriggerName` is omitted and the job has multiple enabled manual triggers, dispatch should fail with an ambiguous-dispatch `Result`
- transient no-history dispatch, if supported, must be explicitly requested and must not be the default behavior

### Service Models

The following request and value models are part of the intended contract. They are returned inside the appropriate Result wrapper on the public client-facing API.

```csharp
public class JobDispatchOptions
{
    public string TriggerName { get; set; }
    public string CorrelationId { get; set; }
    public string IdempotencyKey { get; set; }
    public PropertyBag Properties { get; set; }
    public bool Durable { get; set; } = true;
}

public class JobDispatchResult
{
    public string JobName { get; set; }
    public string TriggerName { get; set; }
    public Guid OccurrenceId { get; set; }
    public string CorrelationId { get; set; }
    public DateTimeOffset AcceptedUtc { get; set; }
}

public class JobExecutionResult
{
    public string JobName { get; set; }
    public string TriggerName { get; set; }
    public Guid OccurrenceId { get; set; }
    public Guid ExecutionId { get; set; }
    public JobExecutionStatus Status { get; set; }
    public bool TimedOut { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public IReadOnlyList<string> Messages { get; set; }
}

public class JobBulkOperationResult
{
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<JobBulkOperationFailureModel> Failures { get; set; }
}

public class JobBatchCreateRequest
{
    public string BatchId { get; set; }
    public string Description { get; set; }
    public JobBatchCompletionPolicy CompletionPolicy { get; set; } = JobBatchCompletionPolicy.RequireAllSucceeded;
    public string CorrelationId { get; set; }
    public string CausationId { get; set; }
    public string IdempotencyKey { get; set; }
    public PropertyBag Properties { get; set; }
}

public class JobBatchDispatchRequest : JobBatchCreateRequest
{
    public IReadOnlyList<JobBatchDispatchItem> Items { get; set; }
}

public class JobBatchDispatchItem
{
    public string JobName { get; set; }
    public object Data { get; set; }
    public JobDispatchOptions Options { get; set; }
    public int? Sequence { get; set; }
    public DateTimeOffset? DueUtc { get; set; }
    public string SourceStep { get; set; }
}

public class JobBatchDispatchResult
{
    public string BatchId { get; set; }
    public JobBatchStatus Status { get; set; }
    public int AcceptedCount { get; set; }
    public IReadOnlyList<Guid> OccurrenceIds { get; set; }
}

public class JobDashboardRequest
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string TimelineBucket { get; set; }
    public string TimelineMode { get; set; }
}

public class JobDashboardTimelineRequest
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string Bucket { get; set; }
    public string Mode { get; set; }
    public string JobName { get; set; }
    public string TriggerName { get; set; }
    public string SchedulerInstanceId { get; set; }
    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; set; }
}

public class JobDefinitionQueryRequest
{
    public string JobName { get; set; }
    public string Group { get; set; }
    public string Module { get; set; }
    public bool? Enabled { get; set; }
    public bool IncludeOrphanedRuntimeState { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string SortBy { get; set; } = "JobName";
    public bool SortDescending { get; set; }
}

public class JobOccurrenceQueryRequest
{
    public string JobName { get; set; }
    public string TriggerName { get; set; }
    public string TriggerType { get; set; }
    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; set; }
    public string SchedulerInstanceId { get; set; }
    public string CorrelationId { get; set; }
    public string IdempotencyKey { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? StartedFrom { get; set; }
    public DateTimeOffset? StartedTo { get; set; }
    public DateTimeOffset? CompletedFrom { get; set; }
    public DateTimeOffset? CompletedTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string SortBy { get; set; } = "DueUtc";
    public bool SortDescending { get; set; } = true;
}

public class JobRetryQueryRequest : JobOccurrenceQueryRequest
{
    public int? MinAttemptNumber { get; set; }
    public int? MaxAttemptNumber { get; set; }
    public DateTimeOffset? RetryDueFrom { get; set; }
    public DateTimeOffset? RetryDueTo { get; set; }
}

public class JobBatchQueryRequest
{
    public string BatchId { get; set; }
    public string Description { get; set; }
    public IReadOnlyList<JobBatchStatus> Statuses { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public DateTimeOffset? CompletedFrom { get; set; }
    public DateTimeOffset? CompletedTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string SortBy { get; set; } = "CreatedUtc";
    public bool SortDescending { get; set; } = true;
}

public class JobRecurringTriggerQueryRequest
{
    public string JobName { get; set; }
    public string TriggerName { get; set; }
    public string Group { get; set; }
    public string Module { get; set; }
    public bool? Enabled { get; set; }
    public bool? Paused { get; set; }
    public DateTimeOffset? NextDueFrom { get; set; }
    public DateTimeOffset? NextDueTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string SortBy { get; set; } = "NextDueUtc";
    public bool SortDescending { get; set; }
}

public class JobSchedulerServerQueryRequest
{
    public string SchedulerInstanceId { get; set; }
    public string HostName { get; set; }
    public JobSchedulerServerStatus? Status { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string SortBy { get; set; } = "LastHeartbeatUtc";
    public bool SortDescending { get; set; } = true;
}

public class JobSchedulerMetricsRequest
{
    public string JobName { get; set; }
    public string TriggerName { get; set; }
    public string TriggerType { get; set; }
    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }
    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? CompletedFrom { get; set; }
    public DateTimeOffset? CompletedTo { get; set; }
}
```

Additional models such as `JobDefinitionModel`, `JobTriggerModel`, `JobRecurringTriggerModel`, `JobOccurrenceModel`, `JobRetryModel`, `JobBatchModel`, `JobExecutionModel`, `JobLeaseModel`, `JobExecutionHistoryModel`, `JobSchedulerServerModel`, `JobDashboardModel`, `JobDashboardNavigationModel`, `JobDashboardOverviewModel`, `JobDashboardTimelineModel`, `JobBulkOperationFailureModel` and `JobSchedulerMetricsModel` should mirror the query and endpoint contracts described in the Observability & Management section.

`JobDefinitionModel` must be built from the active registration model plus persisted runtime state. It must include at least job name, display name, job type identity, description, group, module, effective enabled state, data type properties, trigger count and latest execution summary so dashboards and operational clients can present registered jobs without inspecting implementation types.

Runtime-state query models should also expose whether a persisted runtime-state row is orphaned because its job or trigger is no longer registered. Orphaned rows are support/cleanup data, not active definitions.

### Semantic Constraints

The scheduler shall preserve these constraints across providers and integrations:

- An active trigger definition creates occurrences; it does not execute jobs directly.
- A durable occurrence must be persisted before execution.
- A durable execution attempt must run under an occurrence lease.
- A retry attempt must reuse the original occurrence and link to previous execution attempts through attempt numbers and history.
- An occurrence is executable only when its job and trigger still exist in the active registration model.
- Persisted runtime state for removed jobs or triggers must not make them executable again.
- A job must not mutate scheduler provider state directly for normal execution flow.
- A job must receive scheduler state through `IJobExecutionContext`.
- A job may use application services through dependency injection.
- A job may produce messages and properties through the context and returned `Result`.
- A job may request control operations through the context only where policy allows it.
- Provider implementations may optimize storage and query shape, but they may not weaken occurrence identity, lease, retry, history or query semantics.

---

## Typical Use Cases

The Jobs feature should support these common scheduling and execution scenarios:

- One-time scheduled jobs, such as running a database cleanup at 2:00 AM tomorrow.
- Recurring jobs, such as sending a weekly newsletter every Monday at 9:00 AM.
- Delayed startup jobs, such as running a health check or cache warm-up two minutes after application startup.
- Manual ad-hoc jobs, such as an operator-triggered data backfill or report generation.
- Event-triggered jobs, such as processing a notification, message or queue item through an adapter trigger.
- Cron-based jobs, such as running a report every hour on the hour using a cron expression.
- Calendar-based jobs, such as running a payroll process on the last day of every month.

---

## Scope Boundaries

The initial Jobs feature is focused on application-hosted scheduling, execution, persistence, leasing and operations.

Out of scope for the initial implementation:

- automatic migration of persisted Quartz tables, Quartz triggers, Quartz run history or serialized Quartz job definitions
- Quartz-style clustered scheduler semantics, cluster membership management, leader election and partition rebalancing
- distribution features beyond provider-backed locks or leases for coordinating execution across nodes
- sharding
- multi-tenancy
- integration with external job schedulers
- integration with external schedulers or task queues outside the devkit Queueing adapter model

These boundaries do not prevent later extensions, but they must not weaken the first implementation's provider-neutral job, trigger, occurrence, lease and history contracts.

---

## Example Job: Database Cleanup

This example shows a single job with two triggers:

- `nightly`: recurring cron trigger for automatic maintenance
- `manual`: on-demand trigger for operator-initiated cleanup

The job implementation is the same for both triggers. Trigger data define the normal recurring behavior, and manual dispatch can override the data for a specific run.

```csharp
public record DatabaseCleanupRequest(
    int RetentionDays,
    int BatchSize,
    bool DryRun);

public record DatabaseCleanupSummary(
    int DeletedRows,
    int ScannedRows,
    DateTimeOffset CutoffUtc);

public interface IDatabaseCleanupService
{
    Task<DatabaseCleanupSummary> CleanupAsync(
        DateTimeOffset cutoffUtc,
        int batchSize,
        bool dryRun,
        CancellationToken cancellationToken);
}

public class DatabaseCleanupJob(IDatabaseCleanupService cleanup) : JobBase<DatabaseCleanupRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<DatabaseCleanupRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        var request = ctx.Data;
        var cutoffUtc = ctx.StartedUtc.AddDays(-request.RetentionDays);

        var summary = await cleanup.CleanupAsync(
            cutoffUtc,
            request.BatchSize,
            request.DryRun,
            cancellationToken);

        ctx.Messages.Add(
            request.DryRun
                ? $"Database cleanup dry run scanned {summary.ScannedRows} rows before {summary.CutoffUtc:O}."
                : $"Database cleanup deleted {summary.DeletedRows} rows before {summary.CutoffUtc:O}.");

        ctx.Properties["DeletedRows"] = summary.DeletedRows;
        ctx.Properties["ScannedRows"] = summary.ScannedRows;
        ctx.Properties["CutoffUtc"] = summary.CutoffUtc;

        return Result.Success();
    }
}
```

Registration:

```csharp
builder.Services.AddScoped<IDatabaseCleanupService, DatabaseCleanupService>();

builder.Services.AddJobScheduler(builder.Configuration.GetSection("JobScheduler"))
    .UseEntityFramework<AppDbContext>(storage => storage
        .HistoryRetention(TimeSpan.FromDays(30))
        .LeaseDuration(TimeSpan.FromMinutes(2))
        .LeaseRenewalInterval(TimeSpan.FromSeconds(30)))
    .WorkerPool(pool => pool
        .MaxConcurrency(8)
        .BatchSize(100))
    .WithJob<DatabaseCleanupJob>("database-cleanup", job => job
        .WithName("Database Cleanup")
        .WithDescription("Deletes expired operational data from application tables.")
        .Group("maintenance")
        .Enabled(true)
        .WithConcurrency(limit: 1)
        .WithTimeout(TimeSpan.FromMinutes(15))
        .WithRetry(retry => retry
            .MaxAttempts(3)
            .ExponentialBackoff(
                initialDelay: TimeSpan.FromMinutes(1),
                maxDelay: TimeSpan.FromMinutes(10)))
        .AddTrigger("nightly", trigger => trigger // cron trigger for nightly cleanup
            .Cron("0 0 3 * * *")
            .TimeZone(TimeZoneInfo.Utc)
            .Priority(25)
            .Data(new DatabaseCleanupRequest(
                RetentionDays: 90,
                BatchSize: 1_000,
                DryRun: false))
            .Enabled())
        .AddTrigger("manual", trigger => trigger // manual trigger for on-demand cleanup
            .Manual()
            .Priority(90)
            .Data(new DatabaseCleanupRequest(
                RetentionDays: 30,
                BatchSize: 500,
                DryRun: true))
            .Enabled()));
```

On-demand execution:

```csharp
var result = await jobScheduler.DispatchAsync<DatabaseCleanupJob>(
    data: new DatabaseCleanupRequest(
        RetentionDays: 180,
        BatchSize: 250,
        DryRun: false),
    options: new JobDispatchOptions
    {
        TriggerName = "manual",
        CorrelationId = $"maintenance-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
        IdempotencyKey = "database-cleanup-2026-05-manual"
    },
    cancellationToken: cancellationToken);

if (result.IsFailure)
{
    logger.LogWarning(
        "Database cleanup dispatch failed: {Message}",
        result.Messages.FirstOrDefault());
}
```

Expected behavior:

- The nightly trigger materializes a durable occurrence every day at 03:00 UTC.
- The manual trigger does not create work until it is dispatched through the scheduler service or operations API.
- Both triggers execute the same `DatabaseCleanupJob` implementation and receive a typed `DatabaseCleanupRequest`.
- The job-level concurrency limit prevents recurring and manual cleanup from running at the same time.
- Durable providers lease the occurrence before execution, persist history, and retain context messages and cleanup properties for operations.

---

## Example Job: Batch Reprocessing

This example shows how application code can create a durable batch for fan-out work. The batch groups many normal job occurrences so operators can track progress, retry failed children and cancel remaining work.

Example scenario:

1. A support user starts order projection reprocessing for a date range.
2. The application splits the range into chunks.
3. Each chunk becomes a normal `RecalculateOrderProjectionChunkJob` occurrence.
4. If more affected orders are discovered later, the application attaches more child occurrences to the same batch.

Data passed to the jobs:

```csharp
public record RecalculateOrderProjectionChunkRequest(
    string ReprocessingId,
    int ChunkNumber,
    IReadOnlyList<Guid> OrderIds);

```

Job implementation:

```csharp
public interface IOrderProjectionChunkService
{
    Task<IReadOnlyList<Guid>> RecalculateAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken);
}

public class RecalculateOrderProjectionChunkJob(IOrderProjectionChunkService projections)
    : JobBase<RecalculateOrderProjectionChunkRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<RecalculateOrderProjectionChunkRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        var updated = await projections.RecalculateAsync(
            ctx.Data.OrderIds,
            cancellationToken);

        ctx.Messages.Add(
            $"Recalculated {updated.Count} orders for chunk {ctx.Data.ChunkNumber}.");

        ctx.Properties["ReprocessingId"] = ctx.Data.ReprocessingId;
        ctx.Properties["ChunkNumber"] = ctx.Data.ChunkNumber;
        ctx.Properties["UpdatedOrderCount"] = updated.Count;

        return Result.Success();
    }
}

```

Registration:

```csharp
builder.Services.AddJobScheduler()
    .WithJob<RecalculateOrderProjectionChunkJob>("orders-recalculate-projection-chunk", job => job
        .WithName("Recalculate Order Projection Chunk")
        .WithDescription("Recalculates order projections for a chunk of order ids.")
        .WithConcurrency(limit: 8)
        .WithRetry(retry => retry
            .MaxAttempts(3)
            .ExponentialBackoff(
                initialDelay: TimeSpan.FromMinutes(1),
                maxDelay: TimeSpan.FromMinutes(10)))
        .AddTrigger("manual", trigger => trigger
            .Manual()
            .Enabled()));
```

Creating and dispatching the batch:

```csharp
var reprocessingId = "orders-reprocess-2026-05";
var chunks = orderIds
    .Chunk(100)
    .Select((ids, index) => new JobBatchDispatchItem
    {
        JobName = "orders-recalculate-projection-chunk",
        Sequence = index + 1,
        SourceStep = "projection-chunk",
        Data = new RecalculateOrderProjectionChunkRequest(
            ReprocessingId: reprocessingId,
            ChunkNumber: index + 1,
            OrderIds: ids.ToArray()),
        Options = new JobDispatchOptions
        {
            TriggerName = "manual",
            CorrelationId = reprocessingId,
            IdempotencyKey = $"{reprocessingId}-chunk-{index + 1}"
        }
    })
    .ToArray();

var batch = await jobScheduler.DispatchBatchAsync(
    new JobBatchDispatchRequest
    {
        BatchId = reprocessingId,
        Description = "Recalculate May 2026 order projections.",
        CompletionPolicy = JobBatchCompletionPolicy.RequireAllSucceeded,
        CorrelationId = reprocessingId,
        IdempotencyKey = reprocessingId,
        Items = chunks
    },
    cancellationToken);

if (batch.IsFailure)
{
    logger.LogWarning(
        "Order reprocessing batch dispatch failed: {Message}",
        batch.Messages.FirstOrDefault());
}
```

Attaching late-discovered work:

```csharp
var attach = await jobScheduler.AttachToBatchAsync(
    batchId: reprocessingId,
    request: new JobBatchDispatchRequest
    {
        CorrelationId = reprocessingId,
        IdempotencyKey = $"{reprocessingId}-late-orders",
        Items = new[]
        {
            new JobBatchDispatchItem
            {
                JobName = "orders-recalculate-projection-chunk",
                Sequence = 10_001,
                SourceStep = "late-projection-chunk",
                Data = new RecalculateOrderProjectionChunkRequest(
                    ReprocessingId: reprocessingId,
                    ChunkNumber: 10_001,
                    OrderIds: lateOrderIds),
                Options = new JobDispatchOptions
                {
                    TriggerName = "manual",
                    CorrelationId = reprocessingId,
                    IdempotencyKey = $"{reprocessingId}-late-orders-1"
                }
            }
        }
    },
    cancellationToken);
```

Operational control:

```csharp
await jobScheduler.RetryBatchAsync(
    batchId: reprocessingId,
    reason: "Retry failed chunks after fixing projection data.",
    cancellationToken);

await jobScheduler.CancelBatchAsync(
    batchId: reprocessingId,
    reason: "Reprocessing request was started with the wrong date range.",
    cancellationToken);
```

Expected behavior:

- `DispatchBatchAsync(...)` atomically creates the batch, child occurrences and batch-child links.
- Each chunk occurrence is leased, retried, timed out and recorded independently.
- The batch status rolls up from child occurrence outcomes.
- `AttachToBatchAsync(...)` adds new child occurrences and moves a finished batch back to `Processing` when the attached work is non-terminal.
- `RetryBatchAsync(...)` retries only eligible failed child occurrences and reports per-child failures.
- `CancelBatchAsync(...)` prevents remaining child occurrences from starting and records batch-cancel history.

---

## Example Job: Chaining multiple Jobs

This example shows a simple sequential job chain where each job runs only after the previous job completed successfully.

The Jobs feature should model chaining as ordinary scheduler work:

- each chain step is its own job definition
- each chain step produces its own occurrence and execution history
- each chained occurrence is lease-protected like directly scheduled work
- each step can have its own retry, timeout, data and concurrency settings
- failed steps stop the chain unless an explicit failure policy says otherwise

Example scenario:

1. Import orders from an external source.
2. Recalculate order projections.
3. Send a completion notification.

Data passed through the chain:

```csharp
public record OrderImportChainRequest(
    string ImportBatchId,
    Uri SourceUri,
    bool NotifyWhenComplete);

public record RecalculateOrderProjectionsRequest(
    string ImportBatchId,
    bool NotifyWhenComplete);

public record SendOrderImportNotificationRequest(
    string ImportBatchId);
```

Job implementations:

```csharp
public class ImportOrdersJob(IOrderImportService imports) : JobBase<OrderImportChainRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<OrderImportChainRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        var imported = await imports.ImportAsync(
            ctx.Data.ImportBatchId,
            ctx.Data.SourceUri,
            cancellationToken);

        ctx.Messages.Add($"Imported {imported.Count} orders for batch {ctx.Data.ImportBatchId}.");
        ctx.Properties["ImportedOrderCount"] = imported.Count;

        return Result.Success();
    }
}

public class RecalculateOrderProjectionsJob(IOrderProjectionService projections)
    : JobBase<RecalculateOrderProjectionsRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<RecalculateOrderProjectionsRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        var updated = await projections.RecalculateAsync(
            ctx.Data.ImportBatchId,
            cancellationToken);

        ctx.Messages.Add($"Updated {updated.Count} order projections.");
        ctx.Properties["UpdatedProjectionCount"] = updated.Count;

        return Result.Success();
    }
}

public class SendOrderImportNotificationJob(INotificationService notifications)
    : JobBase<SendOrderImportNotificationRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<SendOrderImportNotificationRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        await notifications.SendOrderImportCompletedAsync(
            ctx.Data.ImportBatchId,
            cancellationToken);

        ctx.Messages.Add($"Sent import completion notification for batch {ctx.Data.ImportBatchId}.");

        return Result.Success();
    }
}
```

Registration:

```csharp
builder.Services.AddJobScheduler()
    .WithJob<ImportOrdersJob>("orders-import", job => job
        .WithName("Order Import")
        .WithDescription("Imports orders from an external source.")
        .WithConcurrency(limit: 1)
        .WithRetry(retry => retry
            .MaxAttempts(3)
            .ExponentialBackoff(
                initialDelay: TimeSpan.FromMinutes(1),
                maxDelay: TimeSpan.FromMinutes(10)))
        .AddTrigger("manual", trigger => trigger
            .Manual()
            .Enabled())
        // chain the next job on successful completion of this job
        .Then<RecalculateOrderProjectionsJob>("orders-recalculate-projections", chain => chain
            .MapContext((ctx, _) => new RecalculateOrderProjectionsRequest(
                ctx.Data.ImportBatchId,
                ctx.Data.NotifyWhenComplete))
            .OnFailure(JobChainFailure.Stop)))
    .WithJob<RecalculateOrderProjectionsJob>("orders-recalculate-projections", job => job
        .WithName("Recalculate Order Projections")
        .WithDescription("Refreshes read-side order projections after an import.")
        .WithConcurrency(limit: 1)
        .WithRetry(retry => retry
            .MaxAttempts(2)
            .FixedDelay(TimeSpan.FromMinutes(2)))
        // conditionally chain the next job only if NotifyWhenComplete is true, and stop the chain if it fails
        .Then<SendOrderImportNotificationJob>("orders-import-notify", chain => chain
            .When(ctx => ctx.Data.NotifyWhenComplete)
            .MapContext((ctx, _) => new SendOrderImportNotificationRequest(
                ctx.Data.ImportBatchId))
            .OnFailure(JobChainFailure.Continue)))
    .WithJob<SendOrderImportNotificationJob>("orders-import-notify", job => job
        .WithName("Order Import Notification")
        .WithDescription("Sends the completion notification for an order import.")
        .WithTimeout(TimeSpan.FromMinutes(2)));
```

Dispatching the first job starts the chain:

```csharp
var dispatch = await jobScheduler.DispatchAsync<ImportOrdersJob>(
    data: new OrderImportChainRequest(
        ImportBatchId: "import-2026-05-08-001",
        SourceUri: new Uri("https://imports.example.test/orders/import-2026-05-08-001.json"),
        NotifyWhenComplete: true),
    options: new JobDispatchOptions
    {
        TriggerName = "manual",
        CorrelationId = "orders-import-2026-05-08-001",
        IdempotencyKey = "orders-import-2026-05-08-001"
    },
    cancellationToken: cancellationToken);
```

Expected behavior:

- `orders-import` creates and executes the first occurrence.
- When `orders-import` completes successfully, the runtime materializes an `orders-recalculate-projections` occurrence.
- When `orders-recalculate-projections` completes successfully and the `When(...)` condition passes, the runtime materializes an `orders-import-notify` occurrence.
- Each chained occurrence has its own lease, retry policy, timeout and execution history.
- The chain correlation id flows into every chained occurrence.
- If `orders-import` fails after retry exhaustion, the chain stops and later jobs are not materialized.
- If `orders-import-notify` fails after retry exhaustion, the chain records the failure but does not change the successful outcome of the previous steps.

---

## Replacement of JobScheduling

`Application.Jobs` is intended to supersede the existing `Application.JobScheduling` feature. The current JobScheduling implementation is Quartz-backed and exposes `AddJobScheduling(...)`, `JobBase`, Quartz-flavored cron behavior, Quartz persistence setup, and `/_bdk/api/jobs` operational endpoints. The new Jobs feature should provide equivalent scheduling value through devkit-owned abstractions, persistence providers, leases, and management APIs.

Replacement goals:

- move new development from `AddJobScheduling(...)` to `AddJobScheduler(...)`
- move job implementations from `JobBase.Process(...)` to `IJob.ExecuteAsync(...)` or the new base `JobBase`
- replace Quartz-specific persistence setup with devkit storage providers such as in-memory and Entity Framework
- replace Quartz-specific concurrency attributes with fluent job/trigger concurrency settings
- preserve operational capabilities such as trigger, pause, resume, cancel, retry, history, stats, and endpoint/dashboard support
- provide a migration guide from common JobScheduling registrations to equivalent Jobs registrations

Compatibility expectations:

- Existing `Application.JobScheduling` can remain available for a deprecation window, but it should not receive new feature investment once `Application.Jobs` is ready.
- The new feature does not need runtime compatibility with Quartz job stores or Quartz trigger records.
- The new feature should support source-level migration paths for common registrations and job implementations.
- API names should make the boundary clear: `AddJobScheduling(...)` is the legacy Quartz-backed feature; `AddJobScheduler(...)` is the new devkit-native feature.

Common migration mapping:

| Existing JobScheduling                                | New Jobs                                                                    |
| ----------------------------------------------------- | --------------------------------------------------------------------------- |
| `AddJobScheduling(...)`                               | `AddJobScheduler(...)`                                                      |
| `JobBase`                                             | new `JobBase` base class or direct `IJob` implementation                    |
| `Process(IJobExecutionContext, CancellationToken)`    | `ExecuteAsync(IJobExecutionContext, CancellationToken)`                     |
| `.WithJob<T>().Cron(...).Named(...).RegisterScoped()` | `.WithJob<T>("name", job => job.WithDescription("...").AddTrigger("trigger", t => t.Cron(...)))` |
| `.WithData(key, value)`                               | `.Data(...)`, `.WithProperty(...)`, or typed `IJobExecutionContext<TData>`      |
| job group                                             | job group, module, or properties-based scope                                  |
| `LastProcessedDate`                                   | `IJobExecutionContext.PreviousSuccessfulExecution.CompletedUtc` or equivalent |
| `ElapsedMilliseconds`                                 | execution history duration                                                  |
| `Status` / `ErrorMessage`                             | execution history status, messages, and errors                              |
| `CronExpressions` / `CronExpressionBuilder`           | devkit cron helpers and new fluent cron builder                             |
| `[DisallowConcurrentExecution]`                       | `.WithConcurrency(limit: 1)`                                                |
| Quartz SQL tables                                     | devkit Entity Framework provider tables                                     |
| `IJobService`                                         | new scheduler management/query services returning `Result`, `Result<T>` or `ResultPaged<T>` |

## Fluent Configuration Model

The fluent API should make the common setup compact while still exposing enough detail for durable, multi-node workloads. The intended shape is one scheduler builder, one job builder per `IJob` implementation, and one trigger builder per trigger attached to that job.

Fluent registration is the primary configuration source and the authoritative source of active job and trigger definitions. Appsettings-based configuration is intended for environment-specific values such as schedules, enabled state, retry settings and scheduler instance targeting, and it may only override matching registrations. Attribute-based and property-based configuration may provide defaults or properties on job classes, but must remain visible through the same resolved job and trigger definitions as fluent configuration.

Durable storage overlays runtime state onto the active registration model. It must not become the source of truth for job names, display names, descriptions, groups, modules, job types, trigger schedules or default configuration.

The resolved configuration model should support behavior/decorator registration through fluent setup and attributes. Behaviors are used for cross-cutting concerns such as logging, metrics, tracing, validation and custom execution policies; they must wrap the normal scheduler execution pipeline rather than bypassing it.

`AddJobScheduler(...)` may be called multiple times during application startup. This is the expected model for modular applications:

- the application host can register scheduler-wide infrastructure such as storage, worker pool, endpoints and provider settings
- modules can call `AddJobScheduler(...)` again to contribute jobs and triggers
- repeated calls merge into one resolved scheduler definition before startup validation
- reconciliation with durable runtime state happens after the active registration model is resolved
- scheduler-wide settings must be compatible; incompatible duplicate infrastructure settings should fail validation
- duplicate job names are allowed only when they refer to the same job type and compatible properties
- duplicate trigger names for the same job are allowed only when they define compatible trigger configuration or an explicit override is configured
- serializer configuration is scheduler-wide; repeated `AddJobScheduler(...)` calls must use the same compatible serializer configuration or fail startup validation
- module registrations must not create separate scheduler runtimes unless the API explicitly supports named scheduler instances later

Module name resolution:

- a module name can be supplied explicitly through the job builder, for example `.Module("Billing")`
- if no module name is supplied and `IModuleContextAccessor` is registered, the scheduler should resolve the module from the job type using `IModuleContextAccessor.Find(jobType)` and use the returned `IModule.Name`
- if `IModuleContextAccessor.Find(jobType)` returns no module, the job has no module name
- if no explicit module name is supplied and no module can be resolved, the job has no module name
- an explicit `.Module(...)` value wins over `IModuleContextAccessor`
- module name is properties for grouping, querying, operations, concurrency scopes and diagnostics; it must not change job identity by itself

Group resolution:

- a group can be supplied explicitly through the job builder, for example `.Group("Billing")`
- if no group is supplied, the resolved group must be `DEFAULT`
- `DEFAULT` is a normal group value for querying, dashboard grouping, operational APIs and concurrency scopes
- resolved job definitions and query models must expose the resolved group value, not a null group
- explicit group values should be normalized consistently with existing devkit naming conventions; an empty or whitespace group is invalid

Display name resolution:

- a display name can be supplied explicitly through the job builder, for example `.WithName("Billing Reconciliation")`
- if no display name is supplied and no module is resolved, the resolved display name must be the dashified job type name, for example `GenerateDailyReportsJob` resolves to `generate-daily-reports-job`
- if no display name is supplied and a module is resolved, the resolved display name must prefix the dashified job type name with the dashified module name, for example module `Billing` and `GenerateDailyReportsJob` resolve to `billing-generate-daily-reports-job`
- query APIs and dashboard views must expose the resolved display name
- an explicit empty or whitespace display name is invalid

Enablement semantics:

- job definitions can be enabled or disabled through registration with `.Enabled()` or `.Enabled(false)`
- trigger definitions can be enabled or disabled through registration with `.Enabled()` or `.Enabled(false)`
- operational enable/disable changes made through management APIs are stored as runtime-state overrides for registered jobs and triggers
- appsettings and fluent registration still determine whether a job or trigger exists; runtime-state overrides only affect registered items
- effective enabled state is calculated from the active registration plus runtime-state overrides; an operational enable may clear a persisted disable override, but it must not make a registration-disabled or missing job executable
- disabled jobs must not materialize new occurrences and workers must not acquire existing pending occurrences for that job
- disabled triggers must not materialize new occurrences, but existing occurrences remain queryable and controllable unless explicitly cancelled or purged
- disabling a job or trigger must not interrupt an already running execution; cancellation or interruption remains an explicit operation
- appsettings can override enabled state for jobs and triggers so environments can turn scheduled workflows on or off without code changes
- query APIs and dashboard views must expose job and trigger enabled state together with the configured display name and description

### Basic Shape

```csharp
builder.Services.AddJobScheduler(builder.Configuration.GetSection("JobScheduler"))
    .InstanceId(ctx => ctx.Environment.MachineName)
    .StartupDelay(TimeSpan.FromSeconds(10))
    .WorkerPool(pool => pool
        .MaxConcurrency(16)
        .PollInterval(TimeSpan.FromSeconds(5))
        .BatchSize(100))
    .Serializer(new SystemTextJsonSerializer())
    .UseInMemoryStorage()
    .WithExceptionHandler<JobSchedulerExceptionHandler>()
    .WithBehavior<JobLoggingBehavior>()
    .AddEndpoints(options => options.RequireAuthorization(), enabled: true)
    .WithJob<GenerateDailyReportsJob>("daily-reports", job => job
        .WithName("Daily Reports")
        .WithDescription("Generates daily operational reports.")
        .Group("operations")
        .Module("Reporting")
        .Enabled()
        .UseLifetime(ServiceLifetime.Transient)
        .WithConcurrency(limit: 1)
        .WithTimeout(TimeSpan.FromMinutes(30))
        .WithPriority(50)
        .WithRetry(retry => retry
            .MaxAttempts(3)
            .ExponentialBackoff(
                initialDelay: TimeSpan.FromSeconds(30),
                maxDelay: TimeSpan.FromMinutes(10)))
        .AddTrigger("nightly", trigger => trigger
            .Cron("0 0 2 * * *")
            .TimeZone(TimeZoneInfo.Utc)
            .Enabled()));
```

The top-level builder owns scheduler-wide concerns:

- scheduler identity and startup behavior
- worker pool settings
- serializer configuration for context data and properties
- storage provider and lease settings
- global behaviors
- configuration merge behavior
- exception handling
- optional endpoint/dashboard registration
- built-in Requester/Notifier outbound integrations and optional Messaging/Queueing/Orchestration outbound integrations

The job builder owns defaults for one `IJob` implementation:

- stable job name
- optional display name for APIs, dashboard views and operational discovery
- description for APIs, dashboard views and operational discovery
- enabled/disabled state
- properties
- DI lifetime
- default priority
- default retry policy
- default concurrency limit
- inferred or explicitly configured data type
- one or more triggers

Serializer configuration should follow the existing devkit builder style used by messaging and queueing infrastructure:

```csharp
builder.Services.AddJobScheduler()
    .Serializer(new SystemTextJsonSerializer());
```

Serializer behavior:

- `.Serializer(ISerializer serializer)` configures the scheduler-wide serializer and returns the scheduler builder.
- If `.Serializer(...)` is not called, the scheduler uses `SystemTextJsonSerializer`.
- The serializer is used for `IJobExecutionContext.Data`, trigger data, occurrence data, properties and retained diagnostic data.
- Durable providers must use the configured serializer when writing and reading persisted scheduler data.
- In-memory providers should use the same serializer boundary where serialization behavior is observable, so tests can catch serializer compatibility issues.
- Null serializer configuration must not replace the current or default serializer.

The trigger builder owns execution conditions and trigger-specific overrides:

- trigger name
- trigger type
- schedule or event source
- enabled/disabled state
- data
- priority override
- retry override
- host/scheduler instance targeting

Behavior/decorator registration should support both forms:

```csharp
builder.Services.AddJobScheduler()
    .WithBehavior<JobLoggingBehavior>()
    .WithBehavior<JobMetricsBehavior>()
    .WithJob<GenerateDailyReportsJob>("daily-reports", job => job
        .WithName("Daily Reports")
        .WithDescription("Generates daily operational reports.")
        .AddTrigger("nightly", trigger => trigger.Cron("0 0 2 * * *")));

[WithBehavior(typeof(JobLoggingBehavior))]
public class GenerateDailyReportsJob : JobBase
{
    public override Task<Result> ExecuteAsync(
        IJobExecutionContext ctx,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}
```

### Durable Provider Setup

The in-memory provider is the default. Durable providers must expose lease settings because they own multi-node coordination.

```csharp
builder.Services.AddJobScheduler(builder.Configuration.GetSection("JobScheduler"))
    .UseEntityFramework<AppDbContext>(storage => storage
        .HistoryRetention(TimeSpan.FromDays(30))
        .LeaseDuration(TimeSpan.FromMinutes(2))
        .LeaseRenewalInterval(TimeSpan.FromSeconds(30))
        .MissedJobCheckInterval(TimeSpan.FromSeconds(15))
        .MaxLeaseRecoveryBatchSize(100))
    .WorkerPool(pool => pool
        .MaxConcurrency(32)
        .BatchSize(250));
```

The provider contract should make these concepts explicit:

- `LeaseDuration`: how long a node owns a due occurrence before it is considered abandoned.
- `LeaseRenewalInterval`: how often a running node renews ownership for long-running jobs.
- `MissedJobCheckInterval`: how often the scheduler looks for due or missed occurrences.
- `MaxLeaseRecoveryBatchSize`: how many abandoned or missed occurrences one scan may recover.
- `HistoryRetention`: how long execution history is retained before purge operations can remove it.
- `BatchOperationSize`: optional limit for how many child occurrence state transitions one batch control operation should process in one provider transaction.
- `BatchRollupMode`: provider choice for synchronous roll-up updates, provider-maintained projections or background roll-up repair, provided observable query results remain consistent after accepted operations.

Batch provider behavior must be explicit:

- durable batch creation and attachment must be atomic with child occurrence creation and batch-child link creation
- batch child links must be queryable by batch id and occurrence id
- batch roll-up counts and status must be updated when child occurrences transition
- accepted batch cancellation must prevent not-yet-started children from executing, even when cancellation is enforced lazily during due scans
- provider recovery must repair stale batch roll-ups and dependency releases after process failure
- purge and archive operations must treat batches, batch-child links, child occurrences and history consistently according to retention policy

### Entity Framework DbContext Integration Contract

The Entity Framework provider shall follow the same composition model used by existing EF-backed devkit features.

This means:

- a project-specific application `DbContext` may implement multiple feature capability interfaces at once
- job scheduler persistence must be able to live in the same application `DbContext`
- the Jobs feature must not force consumers to introduce a second technical `DbContext` only for scheduler persistence
- migrations and schema evolution are owned by the consuming application's `DbContext` and migration workflow

The EF provider shall use a capability interface shape similar to:

```csharp
public interface IJobsContext
{
    DbSet<JobRuntimeState> JobRuntimeStates { get; set; }
    DbSet<JobTriggerRuntimeState> JobTriggerRuntimeStates { get; set; }
    DbSet<JobOccurrence> JobOccurrences { get; set; }
    DbSet<JobOccurrenceDependency> JobOccurrenceDependencies { get; set; }
    DbSet<JobBatch> JobBatches { get; set; }
    DbSet<JobBatchOccurrence> JobBatchOccurrences { get; set; }
    DbSet<JobExecution> JobExecutions { get; set; }
    DbSet<JobExecutionHistory> JobExecutionHistory { get; set; }
    DbSet<JobLease> JobLeases { get; set; }
}
```

The entity types should be explicit and annotation-friendly so consuming applications get predictable migrations through their own `DbContext`.

The Jobs EF entities should follow the same conventions as the Entity Framework Orchestration provider:

- table names use the `__Jobs_` prefix and plural resource names
- runtime-created rows use `Guid` identifiers unless the row is keyed by a logical registration identity
- status fields use explicit devkit enums rather than unbounded strings
- CLR type identifiers use `[MaxLength(2048)]`
- names, correlation identifiers, scheduler instance identifiers and actor identifiers use `[MaxLength(256)]`
- status reasons, error summaries and diagnostic text use `[MaxLength(4000)]`
- mutable rows use `[ConcurrencyCheck] Guid ConcurrencyVersion` and an `AdvanceConcurrencyVersion()` helper
- auditable mutable rows use `CreatedDate`, `UpdatedDate`, `CreatedBy` and `UpdatedBy`
- archive state uses `IsArchived` and `ArchivedUtc` where rows are retained but removed from the active working set
- serialized job data is stored in a column named `Data`; properties is stored in a column named `Properties`
- EF persistence entities use `DateTimeOffset` timestamp columns consistently with the other Entity Framework feature providers. Provider implementations must handle database-specific translation limits internally, such as SQLite client-side ordering for `DateTimeOffset` values, without requiring consumer `DbContext` model-builder configuration.

The following shape is the minimum target model. The database stores runtime state and execution records, not authoritative job or trigger definitions. Jobs EF entities should be annotation/convention based so a host only has to implement `IJobsContext` and expose the sets. Implementations may add provider-specific column types or filtered indexes internally where the database supports them, but they should preserve these property names, identity fields and indexes unless there is a documented provider reason. The status enum names are illustrative of the required Application-layer lifecycle enums and should map directly to the lifecycle states defined in this specification.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_RuntimeState")]
[Index(nameof(IsOrphaned), nameof(LastSeenUtc))]
[Index(nameof(OperationalEnabled), nameof(Paused))]
/// <summary>
/// Represents the durable runtime state row for a registered job.
/// </summary>
public class JobRuntimeState
{
    /// <summary>
    /// Gets or sets the stable job definition name.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the registration generation that last reconciled this row.
    /// </summary>
    [MaxLength(128)]
    public string RegistrationGeneration { get; set; }

    /// <summary>
    /// Gets or sets the registration signature that last reconciled this row.
    /// </summary>
    [MaxLength(512)]
    public string RegistrationSignature { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the active registration was last seen.
    /// </summary>
    public DateTimeOffset? LastSeenUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional operational enabled override.
    /// </summary>
    public bool? OperationalEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the job is paused operationally.
    /// </summary>
    [Required]
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets the optional pause reason.
    /// </summary>
    [MaxLength(4000)]
    public string PauseReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the row no longer matches an active registration.
    /// </summary>
    [Required]
    public bool IsOrphaned { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the row became orphaned.
    /// </summary>
    public DateTimeOffset? OrphanedUtc { get; set; }

    /// <summary>
    /// Gets or sets serialized operational properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

[Table("__Jobs_TriggerRuntimeState")]
[PrimaryKey(nameof(JobName), nameof(TriggerName))]
[Index(nameof(IsOrphaned), nameof(NextDueUtc))]
[Index(nameof(JobName), nameof(IsOrphaned), nameof(NextDueUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(OperationalEnabled))]
/// <summary>
/// Represents the durable runtime state row for a registered job trigger.
/// </summary>
public class JobTriggerRuntimeState
{
    /// <summary>
    /// Gets or sets the stable job definition name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the stable trigger name within the owning job.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the registration generation that last reconciled this row.
    /// </summary>
    [MaxLength(128)]
    public string RegistrationGeneration { get; set; }

    /// <summary>
    /// Gets or sets the registration signature that last reconciled this row.
    /// </summary>
    [MaxLength(512)]
    public string RegistrationSignature { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the active registration was last seen.
    /// </summary>
    public DateTimeOffset? LastSeenUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional operational enabled override.
    /// </summary>
    public bool? OperationalEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the trigger is paused operationally.
    /// </summary>
    [Required]
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets the optional pause reason.
    /// </summary>
    [MaxLength(4000)]
    public string PauseReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the row no longer matches an active registration.
    /// </summary>
    [Required]
    public bool IsOrphaned { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the row became orphaned.
    /// </summary>
    public DateTimeOffset? OrphanedUtc { get; set; }

    /// <summary>
    /// Gets or sets the projected next UTC due time for the trigger.
    /// </summary>
    public DateTimeOffset? NextDueUtc { get; set; }

    /// <summary>
    /// Gets or sets the last UTC due time materialized into an occurrence.
    /// </summary>
    public DateTimeOffset? LastMaterializedDueUtc { get; set; }

    /// <summary>
    /// Gets or sets serialized operational properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

[Table("__Jobs_Occurrences")]
[Index(nameof(OccurrenceKey), IsUnique = true)]
[Index(nameof(Status), nameof(DueUtc), nameof(Priority))]
[Index(nameof(IsArchived), nameof(Status), nameof(DueUtc), nameof(Priority))]
[Index(nameof(IsArchived), nameof(ArchivedUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(Status), nameof(DueUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(ScheduledUtc))]
[Index(nameof(JobName), nameof(Status), nameof(DueUtc))]
[Index(nameof(CorrelationId))]
[Index(nameof(JobName), nameof(IdempotencyKey))]
/// <summary>
/// Represents a durable job occurrence row stored in Entity Framework persistence.
/// </summary>
public class JobOccurrence
{
    /// <summary>
    /// Gets or sets the occurrence identifier.
    /// </summary>
    [Key]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the deterministic occurrence key used for materialization idempotency.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string OccurrenceKey { get; set; }

    /// <summary>
    /// Gets or sets the stable job definition name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the stable trigger name within the owning job.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger type that created the occurrence.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string TriggerType { get; set; }

    /// <summary>
    /// Gets or sets the registration generation active when the occurrence was materialized.
    /// </summary>
    [MaxLength(128)]
    public string RegistrationGeneration { get; set; }

    /// <summary>
    /// Gets or sets the job registration signature active when the occurrence was materialized.
    /// </summary>
    [MaxLength(512)]
    public string JobRegistrationSignature { get; set; }

    /// <summary>
    /// Gets or sets the trigger registration signature active when the occurrence was materialized.
    /// </summary>
    [MaxLength(512)]
    public string TriggerRegistrationSignature { get; set; }

    /// <summary>
    /// Gets or sets the current occurrence lifecycle status.
    /// </summary>
    [Required]
    public JobOccurrenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the scheduler priority.
    /// </summary>
    [Required]
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the scheduled UTC timestamp when the occurrence is time-based.
    /// </summary>
    public DateTimeOffset? ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the occurrence becomes due.
    /// </summary>
    [Required]
    public DateTimeOffset DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the job data type identifier.
    /// </summary>
    [MaxLength(2048)]
    public string DataType { get; set; }

    /// <summary>
    /// Gets or sets serialized job data.
    /// </summary>
    [Column("Data")]
    public string SerializedData { get; set; }

    /// <summary>
    /// Gets or sets serialized occurrence properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the optional idempotency key.
    /// </summary>
    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts already recorded for this occurrence.
    /// </summary>
    [Required]
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum attempt count when a retry policy is configured.
    /// </summary>
    public int? MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the occurrence has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the UTC archive timestamp when the occurrence has been archived.
    /// </summary>
    public DateTimeOffset? ArchivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

[Table("__Jobs_OccurrenceDependencies")]
[Index(nameof(DependentOccurrenceId), nameof(Status))]
[Index(nameof(PrerequisiteOccurrenceId), nameof(Status))]
/// <summary>
/// Represents a durable prerequisite relationship between two job occurrences.
/// </summary>
public class JobOccurrenceDependency
{
    /// <summary>
    /// Gets or sets the dependency identifier.
    /// </summary>
    [Key]
    public Guid DependencyId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence that is blocked by this dependency.
    /// </summary>
    [Required]
    public Guid DependentOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the prerequisite occurrence that must reach the required outcome.
    /// </summary>
    [Required]
    public Guid PrerequisiteOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the required prerequisite status set as serialized enum names.
    /// </summary>
    [Column("RequiredStatuses")]
    public string SerializedRequiredStatuses { get; set; }

    /// <summary>
    /// Gets or sets the current dependency status.
    /// </summary>
    [Required]
    public JobDependencyStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the dependent occurrence policy when the prerequisite cannot satisfy the dependency.
    /// </summary>
    [Required]
    public JobDependencyFailurePolicy FailurePolicy { get; set; }

    /// <summary>
    /// Gets or sets the optional source chain identifier.
    /// </summary>
    [MaxLength(256)]
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the latest reason for the dependency status.
    /// </summary>
    [MaxLength(4000)]
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets serialized dependency properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

[Table("__Jobs_Batches")]
[Index(nameof(Status), nameof(CreatedUtc))]
[Index(nameof(IsArchived), nameof(Status), nameof(UpdatedDate))]
[Index(nameof(IsArchived), nameof(ArchivedUtc))]
[Index(nameof(CorrelationId))]
/// <summary>
/// Represents a durable operational grouping of related job occurrences.
/// </summary>
public class JobBatch
{
    /// <summary>
    /// Gets or sets the batch identifier.
    /// </summary>
    [Key]
    [MaxLength(256)]
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the display name or description.
    /// </summary>
    [MaxLength(512)]
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the current batch roll-up status.
    /// </summary>
    [Required]
    public JobBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the batch completion policy.
    /// </summary>
    [Required]
    public JobBatchCompletionPolicy CompletionPolicy { get; set; }

    /// <summary>
    /// Gets or sets the total number of child occurrences.
    /// </summary>
    [Required]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the number of pending child occurrences.
    /// </summary>
    [Required]
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets the number of processing child occurrences.
    /// </summary>
    [Required]
    public int ProcessingCount { get; set; }

    /// <summary>
    /// Gets or sets the number of successfully completed child occurrences.
    /// </summary>
    [Required]
    public int SucceededCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failed child occurrences.
    /// </summary>
    [Required]
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of cancelled child occurrences.
    /// </summary>
    [Required]
    public int CancelledCount { get; set; }

    /// <summary>
    /// Gets or sets the latest failure summary.
    /// </summary>
    [MaxLength(4000)]
    public string LatestFailureSummary { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation identifier.
    /// </summary>
    [MaxLength(256)]
    public string CausationId { get; set; }

    /// <summary>
    /// Gets or sets serialized batch properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the batch was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the batch started processing.
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the batch reached a terminal roll-up status.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the batch has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the UTC archive timestamp when the batch has been archived.
    /// </summary>
    public DateTimeOffset? ArchivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

[Table("__Jobs_BatchOccurrences")]
[Index(nameof(BatchId), nameof(ChildStatus))]
[Index(nameof(OccurrenceId))]
[Index(nameof(BatchId), nameof(OccurrenceId))]
[Index(nameof(BatchId), nameof(Sequence))]
/// <summary>
/// Represents membership of an occurrence in a durable job batch.
/// </summary>
public class JobBatchOccurrence
{
    /// <summary>
    /// Gets or sets the batch occurrence link identifier.
    /// </summary>
    [Key]
    public Guid BatchOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the batch identifier.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the child occurrence identifier.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the projected child occurrence status.
    /// </summary>
    [Required]
    public JobOccurrenceStatus ChildStatus { get; set; }

    /// <summary>
    /// Gets or sets the optional sequence value within the batch.
    /// </summary>
    public int? Sequence { get; set; }

    /// <summary>
    /// Gets or sets the optional source step.
    /// </summary>
    [MaxLength(256)]
    public string SourceStep { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;
}

[Table("__Jobs_Executions")]
[Index(nameof(OccurrenceId), nameof(Attempt))]
[Index(nameof(JobName), nameof(TriggerName), nameof(Status), nameof(StartedUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(Status), nameof(CompletedUtc))]
[Index(nameof(Status), nameof(StartedUtc))]
[Index(nameof(IsArchived), nameof(Status), nameof(CompletedUtc))]
[Index(nameof(IsArchived), nameof(ArchivedUtc))]
[Index(nameof(CorrelationId))]
[Index(nameof(SchedulerInstanceId), nameof(Status))]
/// <summary>
/// Represents one durable execution attempt for a job occurrence.
/// </summary>
public class JobExecution
{
    /// <summary>
    /// Gets or sets the execution identifier.
    /// </summary>
    [Key]
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence identifier.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the stable job definition name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the stable trigger name within the owning job.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the current execution attempt status.
    /// </summary>
    [Required]
    public JobExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    [Required]
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance identifier that executed the attempt.
    /// </summary>
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when execution started.
    /// </summary>
    [Required]
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when execution completed.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    public long? DurationMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the attempt timed out.
    /// </summary>
    [Required]
    public bool TimedOut { get; set; }

    /// <summary>
    /// Gets or sets serialized execution messages.
    /// </summary>
    [Column("Messages")]
    public string SerializedMessages { get; set; }

    /// <summary>
    /// Gets or sets serialized execution properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the exception type when the attempt failed.
    /// </summary>
    [MaxLength(512)]
    public string ErrorType { get; set; }

    /// <summary>
    /// Gets or sets the latest error summary.
    /// </summary>
    [MaxLength(4000)]
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the UTC archive timestamp when the execution has been archived.
    /// </summary>
    public DateTimeOffset? ArchivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

[Table("__Jobs_ExecutionHistory")]
[Index(nameof(OccurrenceId), nameof(RecordedAt))]
[Index(nameof(ExecutionId), nameof(RecordedAt))]
[Index(nameof(JobName), nameof(TriggerName), nameof(RecordedAt))]
[Index(nameof(EventType), nameof(RecordedAt))]
[Index(nameof(IsArchived), nameof(RecordedAt))]
[Index(nameof(IsArchived), nameof(ArchivedUtc))]
/// <summary>
/// Represents an append-oriented job execution history row stored in Entity Framework persistence.
/// </summary>
public class JobExecutionHistory
{
    /// <summary>
    /// Gets or sets the history entry identifier.
    /// </summary>
    [Key]
    public Guid EntryId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence identifier.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the execution identifier when the event belongs to an execution attempt.
    /// </summary>
    public Guid? ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the stable job definition name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the stable trigger name within the owning job.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the occurrence status associated with the event when available.
    /// </summary>
    public JobOccurrenceStatus? OccurrenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the execution status associated with the event when available.
    /// </summary>
    public JobExecutionStatus? ExecutionStatus { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was recorded.
    /// </summary>
    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance identifier that recorded the event.
    /// </summary>
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the actor that recorded the event when available.
    /// </summary>
    [MaxLength(256)]
    public string RecordedBy { get; set; }

    /// <summary>
    /// Gets or sets additional event details.
    /// </summary>
    [MaxLength(4000)]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets serialized event data.
    /// </summary>
    [Column("Data")]
    public string SerializedData { get; set; }

    /// <summary>
    /// Gets or sets serialized event properties.
    /// </summary>
    [Column("Properties")]
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the exception type when the event represents a failure.
    /// </summary>
    [MaxLength(512)]
    public string ErrorType { get; set; }

    /// <summary>
    /// Gets or sets the latest error summary.
    /// </summary>
    [MaxLength(4000)]
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the UTC archive timestamp when the entry has been archived.
    /// </summary>
    public DateTimeOffset? ArchivedUtc { get; set; }
}

[Table("__Jobs_Leases")]
[Index(nameof(OccurrenceId), IsUnique = true)]
[Index(nameof(LeaseUntilUtc))]
[Index(nameof(SchedulerInstanceId), nameof(LeaseUntilUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(LeaseUntilUtc))]
/// <summary>
/// Represents the durable lease row for a job occurrence.
/// </summary>
public class JobLease
{
    /// <summary>
    /// Gets or sets the lease identifier.
    /// </summary>
    [Key]
    public Guid LeaseId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence identifier owned by this lease.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the stable job definition name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the stable trigger name within the owning job.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance identifier that owns the lease.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the lease acquisition timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset AcquiredUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest lease renewal timestamp.
    /// </summary>
    public DateTimeOffset? RenewedUtc { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset LeaseUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets the fencing token used to reject stale workers.
    /// </summary>
    [Required]
    public long FencingToken { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
```

Indexing requirements:

- `JobOccurrence.IsArchived, Status, DueUtc, Priority` is the hot path for due-work scanning.
- `JobLease.OccurrenceId` must be unique so only one active lease row can own an occurrence.
- `JobOccurrence.OccurrenceKey` must be unique and deterministic for trigger materialization idempotency.
- Job and trigger runtime-state indexes must support registration reconciliation, orphan detection, operational enable/disable overrides and next-due projections.
- Batch indexes must support listing active batches by status, finding child links by batch id, finding batch membership by occurrence id and ordering child links by sequence.
- Dependency indexes must support finding unresolved prerequisites for a dependent occurrence and finding dependents to release or fail when a prerequisite reaches a terminal state.
- Occurrence, execution and history archive indexes must support retention and purge scans without walking the active working set.
- Execution and history indexes must support detail timelines, retry diagnostics and retention/purge scans.
- The execution completed-time index must support previous-success lookup without scanning all executions for a job.
- Provider-specific internals may add filtered unique indexes for non-null idempotency keys and active leases where the database supports them.

And the consuming application may compose it into its own context together with other feature contracts:

```csharp
public class AppDbContext : DbContext,
    IMessagingContext,
    IQueueingContext,
    IJobsContext
{
    public DbSet<BrokerMessage> BrokerMessages { get; set; }
    public DbSet<QueueMessage> QueueMessages { get; set; }
    // Job scheduler sets
    public DbSet<JobRuntimeState> JobRuntimeStates { get; set; }
    public DbSet<JobTriggerRuntimeState> JobTriggerRuntimeStates { get; set; }
    public DbSet<JobOccurrence> JobOccurrences { get; set; }
    public DbSet<JobOccurrenceDependency> JobOccurrenceDependencies { get; set; }
    public DbSet<JobBatch> JobBatches { get; set; }
    public DbSet<JobBatchOccurrence> JobBatchOccurrences { get; set; }
    public DbSet<JobExecution> JobExecutions { get; set; }
    public DbSet<JobExecutionHistory> JobExecutionHistory { get; set; }
    public DbSet<JobLease> JobLeases { get; set; }
}
```

Registration and implementation patterns should follow the existing feature style:

- EF scheduler services use generic registration constrained as `where TContext : DbContext, IJobsContext`
- the provider reads and writes scheduler rows through the application's own `DbContext`
- the provider controls its own `DbContext` scope by resolving scoped `TContext` instances from `IServiceProvider`
- runtime code depends on scheduler persistence abstractions, not directly on Entity Framework APIs
- implementing `IJobsContext` and exposing the Jobs sets must be enough for the host application's migrations to include the Jobs tables and indexes

### Multiple Triggers Per Job

A single job can have several triggers. Each trigger creates its own occurrence stream but points to the same job implementation.

```csharp
builder.Services.AddJobScheduler()
    .WithJob<SendAccountDigestJob>("account-digest", job => job
        .WithName("Account Digest")
        .WithDescription("Sends account digest notifications on configured schedules.")
        .WithData<AccountDigestRequest>()
        .AddTrigger("weekday-morning", trigger => trigger
            .Cron(cron => cron
                .AtTime(8, 0)
                .Weekdays()
                .Build())
            .Data(new AccountDigestRequest(DigestKind.Weekday)))
        .AddTrigger("monthly-summary", trigger => trigger
            .Cron(CronExpressions.MonthlyOnFirstDayAt(hour: 7, minute: 0))
            .Priority(75)
            .Data(new AccountDigestRequest(DigestKind.Monthly))
            .Enabled(false))
        .AddTrigger("manual", trigger => trigger
            .Manual()
            .Enabled()));
```

Trigger-level settings override job-level defaults. For example, a job can define a default retry policy while one high-priority trigger uses a shorter retry interval or a different data value.

### Cron Expression Support

Cron triggers should accept the common 5-field form and the 6-field form with seconds:

| Parts          | Format                                              | Example             |
| -------------- | --------------------------------------------------- | ------------------- |
| 5              | `minute hour day-of-month month day-of-week`        | `0 8 * * MON-FRI`   |
| 6 with seconds | `second minute hour day-of-month month day-of-week` | `0 0 8 * * MON-FRI` |

The cron builder should make this explicit:

```csharp
trigger.Cron("0 8 * * MON-FRI"); // 5 parts, minute precision

trigger.Cron("0 0 8 * * MON-FRI"); // 6 parts, seconds precision

trigger.Cron(cron => cron
    .WithSeconds()
    .AtTime(8, 0)
    .Weekdays()
    .Build());
```

Predefined cron helpers should be available for common schedules so application code does not need to hand-write common expressions:

```csharp
trigger.Cron(CronExpressions.Hourly());
trigger.Cron(CronExpressions.DailyAt(hour: 2, minute: 0));
trigger.Cron(CronExpressions.WeekdaysAt(hour: 8, minute: 0));
trigger.Cron(CronExpressions.MonthlyOnFirstDayAt(hour: 7, minute: 0));
```

Invalid field counts and unsupported cron tokens should fail during registration or startup configuration validation rather than at execution time. Invalid cron configuration should fail scheduler startup for code/appsettings configuration.

### Cron Engine Abstraction

Cron parsing and next-occurrence calculation are deceptively complex because of time zones, daylight-saving-time transitions, optional seconds, unsupported dates, and expression compatibility differences. The scheduler should not expose a third-party cron library directly through public APIs.

The Jobs feature should define a devkit-owned abstraction, with [Cronos](https://github.com/HangfireIO/Cronos) as the default implementation:

```csharp
public interface IJobCronEngine
{
    Result<JobCronExpression> Parse(
        string expression,
        JobCronParseOptions options = null);

    Result<DateTimeOffset?> GetNextOccurrence(
        JobCronExpression expression,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone,
        bool inclusive = false);

    Result<IReadOnlyCollection<DateTimeOffset>> GetOccurrences(
        JobCronExpression expression,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeZoneInfo timeZone);
}
```

Design rules:

- Public scheduler APIs use `IJobCronEngine`, `JobCronExpression`, and devkit options/models, not Cronos types.
- The default implementation uses Cronos for cron parsing and occurrence calculation.
- The abstraction owns compatibility behavior for 5-part expressions and 6-part expressions with seconds.
- Year-field (7-part) cron expressions are intentionally not supported because the default Cronos engine does not support them.
- If future requirements need year-aware scheduling, the interface allows replacing or extending the engine without changing job or trigger configuration APIs.
- The cron engine must always calculate from a UTC instant and an explicit `TimeZoneInfo`; local `DateTime.Now` style inputs should not be used for scheduler decisions.
- Daylight-saving-time behavior must be covered by focused tests for spring-forward and fall-back transitions.

The Cronos package is allowed here because it is a cron expression library, not an external job scheduler. The scheduler remains devkit-owned; Cronos is an internal implementation detail behind the cron engine abstraction.

### One-Time, Delayed, and Startup Triggers

```csharp
builder.Services.AddJobScheduler()
    .WithJob<RebuildSearchIndexJob>("search-index-rebuild", job => job
        .WithName("Search Index Rebuild")
        .WithDescription("Rebuilds the search index after startup or on demand.")
        .AddTrigger("on-startup", trigger => trigger
            .StartupDelay(TimeSpan.FromMinutes(2)))
        .AddTrigger("scheduled-maintenance", trigger => trigger
            .At(new DateTimeOffset(2026, 6, 1, 2, 0, 0, TimeSpan.Zero)))
        .AddTrigger("manual", trigger => trigger
            .Manual()));
```

`At(...)` represents a persisted one-time trigger. `StartupDelay(...)` is evaluated when the scheduler starts and should still go through the same occurrence and lease pipeline as other triggers.

### Outbound Integration Helpers

Jobs may invoke other devkit features directly during execution through their public abstractions.

Example shape:

```csharp
public class GenerateInvoiceJob(
    IMessageBroker messageBroker,
    IQueueBroker queueBroker,
    INotifier notifier,
    IRequester requester)
    : JobBase<InvoicePaidJobRequest>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<InvoicePaidJobRequest> ctx,
        CancellationToken cancellationToken = default)
    {
        await messageBroker.Publish(
            new InvoicePaidMessage { InvoiceId = ctx.Data.InvoiceId },
            cancellationToken);

        await queueBroker.Enqueue(
            new GenerateInvoiceQueueMessage { InvoiceId = ctx.Data.InvoiceId },
            cancellationToken);

        await notifier.PublishAsync(
            new InvoicePaidNotification { InvoiceId = ctx.Data.InvoiceId },
            cancellationToken: cancellationToken);

        var requestResult = await requester.SendAsync<InvoicePaidRequest, Result>(
            new InvoicePaidRequest { InvoiceId = ctx.Data.InvoiceId },
            cancellationToken: cancellationToken);

        return requestResult.IsSuccess
            ? Result.Success()
            : Result.Failure(requestResult.Errors.Select(e => e.Message).ToArray());
    }
}
```

These integrations should use public feature abstractions only. They should not make the scheduler core depend directly on Messaging, Queueing, or Orchestration provider packages.

Appendix A expands the outbound integration requirements for Queueing, Messaging, Requester, Notifier, Orchestration, built-in maintenance jobs, Pipeline and the xUnit harness.

### Trigger Management

Triggers are registered by the host from code during application startup. The Jobs feature must not persist trigger definitions or let runtime management APIs create, update or delete trigger definitions in the database. Code is the leading source of truth for job and trigger shape.

Runtime management APIs may change operational state for code-registered jobs and triggers:

- enable or disable a registered job or trigger through runtime-state overrides
- pause or resume a registered job, trigger or occurrence
- manually dispatch a registered job through a registered manual trigger
- cancel, interrupt, retry or archive materialized occurrences
- inspect active job definitions, active trigger definitions, runtime state, executions, leases and history
- return `Result`/`Result<T>` for recoverable validation and lifecycle failures

If an application needs request-driven or user-created scheduling, the host application should persist that business intent in its own domain/application storage and register a stable code-defined job/trigger that reads those records. Scheduler persistence still owns only scheduler runtime state, occurrences, leases and history.

### Previous Run Context

Jobs should not need to query the scheduler store directly to calculate deltas. The runtime should hydrate previous run information into `IJobExecutionContext` before invoking the job.

```csharp
public class SyncCustomersJob(ICustomerRepository customers) : JobBase
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext ctx,
        CancellationToken cancellationToken = default)
    {
        var changedSince = ctx.PreviousSuccessfulExecution?.CompletedUtc
            ?? DateTimeOffset.MinValue;

        var customersToSync = await customers.FindChangedSinceAsync(
            changedSince,
            cancellationToken);

        ctx.Messages.Add($"Syncing {customersToSync.Count} customer changes since {changedSince:O}.");

        // sync work omitted

        return Result.Success();
    }
}
```

The context should distinguish between:

- `PreviousExecution`: the immediately preceding attempt for the same occurrence, populated for retry attempts
- `PreviousSuccessfulExecution`: the most recent successful execution for the same job/trigger identity before the current occurrence
- current execution identity and status such as execution id, job name, trigger name, started UTC, correlation id, data, and scheduler instance id

For jobs with multiple triggers, previous successful execution lookup should be scoped by job and trigger by default. A job-level lookup can be exposed separately for scenarios that need the latest successful execution across all triggers for the same job.

### Appsettings Merge Rules

Configuration from appsettings should be useful for environment-specific schedules, but fluent registration should remain the canonical code-first model.

Recommended merge behavior:

1. Register jobs and triggers from code.
2. Load matching appsettings entries by job name and trigger name.
3. Apply appsettings values for job enabled state, trigger enabled state, schedules, priority, retry settings and host targeting to matching code registrations.
4. Resolve the active job and trigger definition model.
5. Reconcile active definitions with persisted runtime state by job name and trigger name.
6. Apply operational runtime-state overrides only to active registered jobs and triggers.
7. Mark persisted runtime-state rows with no matching active registration as orphaned/stale.
8. Fail fast for unknown job names in appsettings, unknown trigger names in appsettings, duplicate trigger names in code registration, invalid cron expressions, invalid lease settings and unsupported trigger types.

Example:

```json
{
  "JobScheduler": {
    "WorkerPool": {
      "MaxConcurrency": 16,
      "PollInterval": "00:00:05"
    },
    "Jobs": {
      "account-digest": {
        "Enabled": true,
        "Triggers": {
          "weekday-morning": {
            "Cron": "0 0 8 * * MON-FRI",
            "Enabled": true
          },
          "monthly-summary": {
            "Enabled": false
          }
        }
      }
    }
  }
}
```

### Validation Rules

- Job names must be stable and unique.
- Scheduler job registrations must define a non-empty description through `.WithDescription(...)`.
- If no display name is configured, validation must resolve the display name from the optional module name and dashified job type name.
- If no group is configured, validation must resolve the group to `DEFAULT`.
- Trigger names must be unique within a job.
- A job must have at least one code-registered trigger; manual-only jobs register a manual trigger in code.
- Job context data, trigger data and properties must be serializable by the configured scheduler serializer.
- Durable triggers must have deterministic persisted identifiers.
- Lease settings must be valid before the scheduler starts.
- host/scheduler instance targeting must fail clearly when no eligible scheduler instance can execute a trigger.
- Duplicate registrations from multiple modules should merge only when they refer to the same job and trigger identity with compatible configuration.
- Persisted runtime-state rows that do not match an active registration must be treated as orphaned and must not be executable.

---

## Appendix A: Devkit Integrations

The Jobs feature should become the durable background execution layer for the devkit, while keeping the scheduler core independent from optional features.

The integrations in this appendix are outbound job integration capabilities. Requester and Notifier integrations can be implemented in the core Jobs package because their abstractions live in Common. Other integrations should be implemented as optional packages, extension methods, or built-in job catalogs. The scheduler core must remain usable without Queueing, Messaging, Domain Events, Orchestration, Presentation or Entity Framework. It may reference Requester/Notifier types directly, but it must still work when those services are not registered.

The preferred authoring model for routine integrations should align with the built-in outbound activity helpers described in [features-orchestrations.md](../features-orchestrations.md). In the same way that orchestration authors can configure `SendRequestActivity(...)`, `PublishMessageActivity(...)`, `PublishNotificationActivity(...)`, `SendQueueMessageActivity(...)`, `ExecutePipelineActivity(...)`, and child orchestration helpers declaratively, job authors should be able to configure default integration jobs declaratively without creating a one-off bootstrap job whose only responsibility is to translate `ctx.Data` and call another feature.

This is a meaningful product advantage for the devkit. Many schedulers and background-processing libraries stop at generic execution and trigger management, leaving integration bootstrapping entirely to custom code. The devkit should stand out by making common outbound integrations first-class, fluent, and consistent with the rest of the application model while still preserving an escape hatch for custom job implementations.

### Integration Principles

All integrations must preserve the core Jobs contracts:

- integrations are part of normal job execution; they do not bypass the job runtime
- job data, properties, correlation, previous-run information, and execution context should be explicitly mappable into target payloads and target properties
- integrations must use public feature abstractions rather than provider tables or transport-specific internals
- integrations must propagate correlation id, causation id, tenant/module properties, job identity, occurrence identity, and execution identity where available
- integrations must return `Result`/`Result<T>` for recoverable execution, mapping and validation failures unless the target abstraction uses exceptions/results differently
- integrations must be testable with substitutes or fakes for the target abstractions
- integrations must not make `Application.Jobs` depend directly on optional source feature packages unless those abstractions already live in Common

### Low-Coupling Abstraction Placement

The minimal job execution contracts belong below `Application.Jobs` so other devkit features can depend on job concepts without depending on the scheduler runtime.

The target dependency shape is:

- `Common.Abstractions` owns the smallest stable job contracts that other features may reference
- `Application.Jobs` owns the scheduler runtime, worker pool, fluent registration, concrete trigger implementations, persistence abstractions, management services and query services
- `Application.Jobs` may own Requester and Notifier outbound integrations because they depend only on Common abstractions
- feature packages own feature-specific outbound integrations through Common.Abstractions job contracts without depending on the Jobs runtime package, for example `Application.Queueing`, `Application.Messaging`, `Application.Pipelines` and `Application.Orchestrations`
- provider packages own durable implementations, for example `Infrastructure.EntityFramework.Jobs`
- presentation packages own operational endpoints and optional dashboard assets

`Common.Abstractions` should own:

- `IJob`
- `IJob<TData>`
- `IJobExecutionContext`
- `IJobExecutionContext<TData>`
- `IJobDataMapper<TSource, TData>`
- status enums such as `JobOccurrenceStatus` and `JobExecutionStatus` if they are returned by cross-feature APIs

The `IJob` and `IJob<TData>` method signatures must depend only on `IJobExecutionContext`/`IJobExecutionContext<TData>` and other Common abstractions. They must not require concrete `Application.Jobs` context classes, provider contracts, hosting services or storage types.

`Application.Jobs` should own:

- `JobBase` and `JobBase<TData>` when they contain scheduler conveniences, context helpers or behavior-pipeline assumptions
- concrete `JobExecutionContext` implementations
- trigger builders and scheduler registration builders
- scheduler services, query services, runtime stores, hosted services and provider abstractions

`JobBase` can move to a lower-level package only if it remains free of scheduler runtime, storage, hosting and provider dependencies.

The low-level abstractions must not reference:

- Entity Framework
- ASP.NET Core
- hosted services
- scheduler stores
- concrete trigger builders
- Messaging, Queueing, Orchestration or Pipeline packages

Requester and Notifier are exceptions to this rule because their abstractions already live in the Common namespace/package.

### Outbound Job Integrations

Jobs are executable application code and can therefore integrate outward with other devkit features directly during job execution.

Typical outbound job integrations include:

- publish a message (messaging)
- enqueue a queue item (queueing)
- publish a notification (notifier)
- send a request (requester)
- start an orchestration (orchestration)
- execute a Pipeline (pipeline)

These integrations should use the public abstractions of the target feature and map `IJobExecutionContext<TData>` into the target payload, and into target-specific properties only where that target feature exposes a clear properties surface.

### Declarative Integration Jobs

For the common case, outbound integrations should be authored through built-in configurable job types that mirror the outbound orchestration activity helpers instead of requiring a bespoke bootstrap job per integration.

Providing these integrations through fluent setup is a real benefit of the devkit. It reduces repetitive boilerplate, shortens the path from scheduling intent to working integration, and makes the Jobs feature more compelling than non-integrated scheduling solutions that only offer raw callback execution.

The target helper catalog should align conceptually with the orchestration feature:

- `CommandJob` and `SendRequestJob` for `IRequester`
- `PublishNotificationJob` for `INotifier`
- `PublishMessageJob` and `SendQueueMessageJob` for `IMessageBroker` and `IQueueBroker`
- `ExecutePipelineJob` for `IPipelineFactory`
- `StartOrchestrationJob` for `IOrchestrationService`

The important design point is that these integrations share a declarative authoring style, not a single identical payload contract. Each target feature has its own payload model, properties surface, success semantics, and follow-up behavior:

- requester jobs build request objects and may optionally map typed responses
- notifier jobs build notification objects and usually only care whether publish succeeded
- messaging jobs build broker message payloads plus broker-specific properties
- queueing jobs build queue message payloads plus enqueue properties
- orchestration jobs build orchestration start data and may optionally capture the dispatched instance identity
- pipeline jobs build pipeline contexts and may map pipeline state back after execution

Because of that, the APIs should feel structurally similar across integrations, but they should remain integration-specific instead of forcing every feature into one generic lowest-common-denominator payload builder.

Registration model:

- requester-backed jobs should be first-class registrations on the base job scheduler builder because Requester abstractions already live in Common, for example `WithCommandJob<TCommand>(...)` and `WithRequestSendJob<TRequest>(...)`
- notifier-backed jobs should also be first-class registrations on the base job scheduler builder because Notifier abstractions already live in Common, for example `WithNotificationPublishJob<TNotification>(...)`
- messaging integrations should remain optional package extensions, exposed through registration methods such as `WithMessagingPublishJob<TMessage>(...)`
- queueing integrations should remain optional package extensions, exposed through registration methods such as `WithQueueingSendJob<TQueueMessage>(...)`
- pipeline integrations should remain optional package extensions, exposed through registration methods such as `WithPipelineExecuteJob<TPipeline, TPipelineContext>(...)`
- orchestration integrations should remain optional package extensions, exposed through registration methods such as `WithOrchestrationExecuteJob<TOrchestration, TOrchestrationData>(...)`

Example shape:

```csharp
builder.Services.AddJobScheduler()
    .WithRequestSendJob<ExportCustomersCommand>("export-customers", job => job
        .WithDescription("Exports changed customers to storage each night.")
        .WithData<ExportCustomersData>()
        .WithRequest(context => new ExportCustomersCommand
            {
                Profile = context.Data.Profile,
                DeltaMode = context.Data.DeltaMode,
                SinceUtc = context.PreviousSuccessfulExecution?.CompletedUtc,
            }))
        .AddTrigger("nightly", trigger => trigger
            .Cron("0 0 2 * * *")
            .Data(new ExportCustomersData("customers", DeltaMode.ChangedSincePreviousSuccess))));
```

Equivalent command-oriented shape:

```csharp
builder.Services.AddJobScheduler()
    .WithCommandJob<ExportCustomersCommand>("export-customers", job => job
        .WithDescription("Exports changed customers to storage each night.")
        .WithData<ExportCustomersData>()
        .WithCommand(context => new ExportCustomersCommand
        {
            Profile = context.Data.Profile,
            DeltaMode = context.Data.DeltaMode,
            SinceUtc = context.PreviousSuccessfulExecution?.CompletedUtc,
        })
        .AddTrigger("nightly", trigger => trigger
            .Cron("0 0 2 * * *")
            .Data(new ExportCustomersData("customers", DeltaMode.ChangedSincePreviousSuccess))));
```

Representative fluent shapes for the other integrations:

```csharp
builder.Services.AddJobScheduler()
    .WithNotificationPublishJob<CustomerReviewedNotification>("notify-customer-review", job => job
        .WithData<CustomerReviewedJobData>()
        .WithNotification(notification => notification
            .Notification(context => new CustomerReviewedNotification(context.Data.CustomerId))));
```

```csharp
builder.Services.AddJobScheduler()
    .WithMessagingPublishJob<InvoicePaidMessage>("publish-invoice-paid", job => job
        .WithData<InvoicePaidJobData>()
        .WithMessage(message => message
            .Message(context => new InvoicePaidMessage
            {
                InvoiceId = context.Data.InvoiceId,
                Amount = context.Data.Amount,
            })));
```

```csharp
builder.Services.AddJobScheduler()
    .WithQueueingSendJob<GenerateInvoiceQueueMessage>("queue-invoice-processing", job => job
        .WithData<QueueInvoiceProcessingData>()
        .WithMessage(message => message
            .Message(context => new GenerateInvoiceQueueMessage
            {
                InvoiceId = context.Data.InvoiceId,
            })));
```

```csharp
builder.Services.AddJobScheduler()
    .WithPipelineExecuteJob<OrderSyncPipeline, OrderSyncPipelineContext>("sync-order-pipeline", job => job
        .WithData<OrderSyncJobData>()
        .WithPipeline(pipeline => pipeline
            .Context(context => new OrderSyncPipelineContext
            {
                OrderId = context.Data.OrderId,
            })
            .MapFromContext((context, pipelineContext) => context.Items["SyncToken"] = pipelineContext.Token)));
```

```csharp
builder.Services.AddJobScheduler()
    .WithOrchestrationExecuteJob<OrderFulfillmentOrchestration, OrderFulfillmentData>("start-order-fulfillment", job => job
        .WithData<StartOrderFulfillmentJobData>()
        .WithOrchestration(orchestration => orchestration
            .Data(context => new OrderFulfillmentData
            {
                OrderId = context.Data.OrderId,
            })
            .StoreInstanceId((context, instanceId) => context.Items["ChildInstanceId"] = instanceId)));
```

Pseudo implementation:

```csharp
public record SendRequestJobOptions<TData, TRequest, TResponse>
{
    public required Func<IJobExecutionContext<TData>, TRequest> RequestFactory { get; init; }

    public Action<IJobExecutionContext<TData>, TResponse>? MapResult { get; init; }

    public Func<TResponse, Result>? SuccessEvaluator { get; init; }
}

public class SendRequestJob<TData, TRequest, TResponse>(
    IRequester requester = null,
    SendRequestJobOptions<TData, TRequest, TResponse> options)
    : JobBase<TData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default)
    {
        if (requester == null)
        {
            return Result.Failure(
                $"Outbound job integration failed: Requester is not registered for job '{context.JobName}'.");
        }

        var request = options.RequestFactory(context);

        var response = await requester.SendAsync<TRequest, TResponse>(
            request,
            cancellationToken: cancellationToken);

        if (response is Result result && result.IsFailure)
        {
            return Result.Failure(result.Errors.Select(e => e.Message).ToArray());
        }

        var evaluated = options.SuccessEvaluator?.Invoke(response);
        if (evaluated?.IsFailure == true)
        {
            return evaluated;
        }

        options.MapResult?.Invoke(context, response);
        return evaluated ?? Result.Success();
    }
}
```

Conceptually, `WithRequestSendJob<TRequest>(...)` and `WithRequest(...)` close and configure a typed implementation like the pseudo code above. The same pattern should apply across the integration family: the target request, message, notification, pipeline, or orchestration type belongs on the `With...Job<...>(...)` registration method, while the inner fluent builder focuses on mapping `IJobExecutionContext<TData>` into that already-declared target shape. The simple requester fluent shape should assume the common `Result` response contract by default, while additional overloads may support explicit `TResponse` and result mapping when a request returns richer typed data. `WithCommandJob<TCommand>(...)` should be treated as a thin semantic alias over `WithRequestSendJob<TRequest>(...)`, and `WithCommand(...)` should be treated as a thin alias over `WithRequest(...)`. The alias exists for readability and intent, not for a different runtime model. Jobs are activated through DI. Optional outbound integration dependencies may therefore be injected as nullable or otherwise optional constructor parameters so the application can compose without every integration present. When a job actually executes an integration path and the required target abstraction is missing, the job must fail with a clear error through the normal job failure path. The public surface should prefer integration-specific registration methods such as `WithRequestSendJob<TRequest>(...)`, `WithMessagingPublishJob<TMessage>(...)`, `WithQueueingSendJob<TQueueMessage>(...)`, `WithNotificationPublishJob<TNotification>(...)`, `WithPipelineExecuteJob<TPipeline, TPipelineContext>(...)`, and `WithOrchestrationExecuteJob<TOrchestration, TOrchestrationData>(...)` because they read as first-class devkit capabilities instead of generic jobs with a special implementation type behind them. Other integration jobs should follow the same pattern: fluent configuration at registration time, typed payload factory at runtime, and feature-specific execution logic behind the scenes.

These helpers should keep integration work explicit in the job definition:

- the outbound payload is built from `IJobExecutionContext<TData>`
- builders should primarily support constructing the outbound payload directly from context, for example through `Request(context => ...)`, `Notification(context => ...)`, `Message(context => ...)`, or equivalent integration-specific factories
- builders may additionally support mutating a pre-created payload through hooks such as `MapToRequest(...)`, `MapToNotification(...)`, `MapToMessage(...)`, `MapToQueueMessage(...)`, `MapToPipelineContext(...)`, and `MapToOrchestrationData(...)` when that is useful
- target-specific properties mapping is optional and should only be exposed where the target abstraction has a clear properties surface; the primary required contract is context-to-payload construction
- requester-, pipeline-, and orchestration-based helpers may optionally map successful results back into job messages, execution items, or follow-up dispatch decisions
- missing registrations, failed `Result`s, and thrown exceptions should fail the job through the normal job failure and retry behavior

This means the feature should prefer a family of integration-specific builders with a consistent mental model over a single giant builder that tries to configure requests, notifications, queue messages, broker messages, and orchestration starts through the same property set.

Unlike orchestration activities, these job helpers do not define workflow state transitions. Their customization points are outbound payload construction, target-specific properties mapping where supported, trigger configuration, and optional success/failure handling inside the normal job execution model.

Custom `IJob` and `JobBase` implementations remain supported and are still required for advanced cases such as multi-step integration flows, bespoke batching, conditional branching across several dependencies, or domain-specific side effects. The following sections keep those custom job shapes as the escape hatch and underlying execution model.

### Outbound Job Integration Principles

All outbound job integrations must preserve the core Jobs contracts:

- integrations are part of normal job execution; they do not bypass the job runtime
- job data, correlation, previous-run information, and execution context should be explicitly mappable into target payloads, and into target-specific properties where available
- integrations must use public feature abstractions rather than provider tables or transport-specific internals
- integrations must be testable with substitutes or fakes for the target abstractions
- correlation id, causation id, tenant/module properties, job name, trigger name, occurrence id, and execution id should flow into target feature properties where available
- the target feature remains owner of its own runtime state and dispatch lifecycle after acceptance

Additional outbound-job-specific principles:

- missing required infrastructure is a job execution failure, not a warning-only skip
- if the required target abstraction is not registered, the job should fail with an explicit error
- if the target abstraction throws or returns a failed `Result`, the job should fail unless the job explicitly handles that outcome
- jobs may retry according to normal job retry policy, which means target integrations should be authored as idempotent or retry-tolerant

### Optional Registration Behavior for Outbound Job Integrations

Outbound job integrations may depend on optional feature registrations such as:

- `IMessageBroker`
- `IQueueBroker`
- `INotifier`
- `IRequester`
- `IPipelineFactory`
- `IOrchestrationService`

These registrations must remain optional for the overall application composition.

However, if a specific job executes an integration path that requires one of them and the abstraction is not registered, that is a hard job failure.

Therefore:

- the presence of outbound job integration support must not force the host application to register every possible target feature
- jobs should still be activated through normal DI
- a concrete job may still depend on one or more target feature abstractions, resolved as optional constructor dependencies when the integration itself is optional
- if the job reaches an integration step and the required abstraction is not registered, the execution should fail with a clear error
- the resulting failure should be visible through normal job result, error, history, and retry behavior

Example failure shape:

```text
Outbound job integration failed: Messaging is not registered for PublishInvoicePaidJob.
```

### Job Messaging Integration

A job may publish a message through the public Messaging abstraction. The preferred authoring model is a configurable `PublishMessageJob` aligned with `PublishMessageActivity(...)` in the orchestration feature; a custom job implementation remains supported for advanced scenarios.

Declarative shape:

```csharp
builder.Services.AddJobScheduler()
    .WithMessagingPublishJob<InvoicePaidMessage>("publish-invoice-paid", job => job
        .WithData<InvoicePaidJobData>()
        .WithMessage(message => message
            .Message(context => new InvoicePaidMessage
            {
                InvoiceId = context.Data.InvoiceId,
                Amount = context.Data.Amount,
            })));
```

Example shape:

```csharp
public class PublishInvoicePaidJob(IMessageBroker broker)
    : JobBase<InvoicePaidJobData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<InvoicePaidJobData> ctx,
        CancellationToken cancellationToken = default)
    {
        var message = new InvoicePaidMessage
        {
            InvoiceId = ctx.Data.InvoiceId,
            Amount = ctx.Data.Amount
        };

        await broker.Publish(message, cancellationToken);
        return Result.Success();
    }
}
```

Requirements:

- a declarative `PublishMessageJob` should be available through optional Messaging integration registration methods such as `WithMessagingPublishJob<TMessage>(...)`, and custom jobs must remain supported
- jobs may publish messages through the public Messaging abstraction
- job data should be explicitly mappable into message payload
- message-specific properties may be mapped when the messaging abstraction exposes a clear property surface
- if Messaging is not registered or publish fails, the job execution fails with an error

### Job Queueing Integration

A job may enqueue a queue message through the public Queueing abstraction. The preferred authoring model is a configurable `SendQueueMessageJob` aligned with `SendQueueMessageActivity(...)` in the orchestration feature; a custom job implementation remains supported for advanced scenarios.

Declarative shape:

```csharp
builder.Services.AddJobScheduler()
    .WithQueueingSendJob<GenerateInvoiceQueueMessage>("queue-invoice-processing", job => job
        .WithData<QueueInvoiceProcessingData>()
        .WithMessage(message => message
            .Message(context => new GenerateInvoiceQueueMessage
            {
                InvoiceId = context.Data.InvoiceId,
            })));
```

Example shape:

```csharp
public class QueueInvoiceProcessingJob(IQueueBroker broker)
    : JobBase<QueueInvoiceProcessingData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<QueueInvoiceProcessingData> ctx,
        CancellationToken cancellationToken = default)
    {
        var message = new GenerateInvoiceQueueMessage
        {
            InvoiceId = ctx.Data.InvoiceId
        };

        await broker.Enqueue(message, cancellationToken);
        return Result.Success();
    }
}
```

Requirements:

- a declarative `SendQueueMessageJob` should be available through optional Queueing integration registration methods such as `WithQueueingSendJob<TQueueMessage>(...)`, and custom jobs must remain supported
- jobs may enqueue queue messages through the public Queueing abstraction
- job data should be explicitly mappable into queue payload
- queue-specific properties may be mapped when the queueing abstraction exposes a clear properties surface
- if Queueing is not registered or enqueue fails, the job execution fails with an error

### Job Requester Integration

A job may send a request through `IRequester`. The preferred authoring model is a configurable `CommandJob` or `SendRequestJob` aligned with the equivalent orchestration activity helpers; a custom job implementation remains supported for advanced scenarios.

Jobs should model background actions. Because of that, a dedicated query-oriented job helper is not needed. Query-style requests read state rather than triggering work, so they should remain part of normal request handling or be embedded inside a custom job only when they are a subordinate step in a larger action-oriented execution path.

`CommandJob` should be treated as a thin semantic alias over `SendRequestJob` when the target request is a command. It improves readability at the registration site, but it should not introduce a distinct runtime, persistence, retry, or execution model.

Declarative shape:

```csharp
builder.Services.AddJobScheduler()
    .WithRequestSendJob<ExportCustomersCommand>("export-customers", job => job
        .WithData<ExportCustomersData>()
        .WithRequest(context => new ExportCustomersCommand
            {
                Profile = context.Data.Profile,
                DeltaMode = context.Data.DeltaMode,
                SinceUtc = context.PreviousSuccessfulExecution?.CompletedUtc,
            }));
```

Example shape:

```csharp
public class StartCustomerSyncJob(IRequester requester)
    : JobBase<StartCustomerSyncJobData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<StartCustomerSyncJobData> ctx,
        CancellationToken cancellationToken = default)
    {
        var result = await requester.SendAsync<StartCustomerSyncCommand, Result>(
            new StartCustomerSyncCommand
            {
                CustomerId = ctx.Data.CustomerId
            },
            cancellationToken: cancellationToken);

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Message).ToArray());
    }
}
```

Requirements:

- declarative requester-backed jobs such as `CommandJob` and `SendRequestJob` should be available through first-class base builder methods such as `WithCommandJob<TCommand>(...)` and `WithRequestSendJob<TRequest>(...)`, and custom jobs must remain supported
- `WithCommandJob<TCommand>(...)` should be a thin alias over `WithRequestSendJob<TRequest>(...)`, and `WithCommand(...)` should be a thin alias over `WithRequest(...)`
- jobs may send requests through `IRequester`
- job data should be explicitly mappable into request payload
- request-specific properties may be mapped when the requester abstraction exposes a clear request context surface
- successful requester responses may be mapped into job messages, execution items, or follow-up behavior when the configured default job supports that shape
- if Requester is not registered or send fails, the job execution fails with an error

### Job Notifier Integration

A job may publish a notification through `INotifier`. The preferred authoring model is a configurable `PublishNotificationJob` aligned with `PublishNotificationActivity(...)` in the orchestration feature; a custom job implementation remains supported for advanced scenarios.

Declarative shape:

```csharp
builder.Services.AddJobScheduler()
    .WithNotificationPublishJob<CustomerReviewedNotification>("notify-customer-review", job => job
        .WithData<CustomerReviewedJobData>()
        .WithNotification(notification => notification
            .Notification(context => new CustomerReviewedNotification(context.Data.CustomerId))));
```

Example shape:

```csharp
public class PublishCustomerReviewedNotificationJob(INotifier notifier)
    : JobBase<CustomerReviewedJobData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<CustomerReviewedJobData> ctx,
        CancellationToken cancellationToken = default)
    {
        var notification = new CustomerReviewedNotification
        {
            CustomerId = ctx.Data.CustomerId
        };

        var result = await notifier.PublishAsync(notification, cancellationToken: cancellationToken);
        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Message).ToArray());
    }
}
```

Requirements:

- a declarative `PublishNotificationJob` should be available through a first-class base builder method such as `WithNotificationPublishJob<TNotification>(...)`, and custom jobs must remain supported
- jobs may publish notifications through `INotifier`
- job data should be explicitly mappable into notification payload
- notification-specific properties may be mapped when the notifier abstraction exposes a clear context surface
- if Notifier is not registered or publish fails, the job execution fails with an error

### Job Pipeline Integration

A job may execute a pipeline through the public pipeline abstractions. The preferred authoring model is a configurable `ExecutePipelineJob` aligned with `ExecutePipelineActivity(...)` in the orchestration feature; a custom job implementation remains supported for advanced scenarios.

Declarative shape:

```csharp
builder.Services.AddJobScheduler()
    .WithPipelineExecuteJob<OrderSyncPipeline, OrderSyncPipelineContext>("sync-order-pipeline", job => job
        .WithData<OrderSyncJobData>()
        .WithPipeline(pipeline => pipeline
            .Context(context => new OrderSyncPipelineContext
            {
                OrderId = context.Data.OrderId,
            })
            .MapFromContext((context, pipelineContext) => context.Items["SyncToken"] = pipelineContext.Token)));
```

Example shape:

```csharp
public class ExecuteOrderSyncPipelineJob(IPipelineFactory pipelines)
    : JobBase<OrderSyncJobData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<OrderSyncJobData> ctx,
        CancellationToken cancellationToken = default)
    {
        var context = new OrderSyncPipelineContext
        {
            OrderId = ctx.Data.OrderId,
            CorrelationId = ctx.CorrelationId
        };

        var result = await pipelines.Create<OrderSyncPipeline>()
            .ExecuteAsync(context, cancellationToken);

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Message).ToArray());
    }
}
```

Requirements:

- a declarative `ExecutePipelineJob` should be available through optional Pipeline integration registration methods such as `WithPipelineExecuteJob<TPipeline, TPipelineContext>(...)`, and custom jobs must remain supported
- jobs may execute pipelines through the public pipeline abstractions
- job data should be explicitly mappable into pipeline context
- pipeline-state mapping back into job items or execution items may be supported where the configured default job exposes that hook
- successful pipeline execution may map pipeline state back into job messages, items, or execution items
- if Pipeline integration is not registered or pipeline execution fails, the job execution fails with an error

### Job Orchestration Integration

A job may start an orchestration through the public orchestration service. The preferred authoring model is a configurable `StartOrchestrationJob` aligned conceptually with the orchestration feature's orchestration-start helpers; a custom job implementation remains supported for advanced scenarios.

Declarative shape:

```csharp
builder.Services.AddJobScheduler()
    .WithOrchestrationExecuteJob<OrderFulfillmentOrchestration, OrderFulfillmentData>("start-order-fulfillment", job => job
        .WithData<StartOrderFulfillmentJobData>()
        .WithOrchestration(orchestration => orchestration
            .Data(context => new OrderFulfillmentData
            {
                OrderId = context.Data.OrderId,
            })
            .StoreInstanceId((context, instanceId) => context.Items["ChildInstanceId"] = instanceId)));
```

Example shape:

```csharp
public class StartOrderFulfillmentJob(IOrchestrationService orchestrations)
    : JobBase<StartOrderFulfillmentJobData>
{
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<StartOrderFulfillmentJobData> ctx,
        CancellationToken cancellationToken = default)
    {
        var result = await orchestrations.DispatchAsync<OrderFulfillmentOrchestration, OrderFulfillmentData>(
            new OrderFulfillmentData
            {
                OrderId = ctx.Data.OrderId
            },
            cancellationToken);

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Message).ToArray());
    }
}
```

Requirements:

- a declarative `StartOrchestrationJob` should be available through optional orchestration integration registration methods such as `WithOrchestrationExecuteJob<TOrchestration, TOrchestrationData>(...)`, and custom jobs must remain supported
- jobs may start orchestrations through `IOrchestrationService`
- job data should be explicitly mappable into orchestration start data
- orchestration dispatch properties and returned instance identifiers may be mapped when the orchestration abstractions expose that surface
- successful orchestration dispatch results may be mapped into job messages or execution items when the configured default job supports that shape
- if orchestration services are not registered or dispatch fails, the job execution fails with an error

### Built-In Maintenance Jobs

The Jobs feature should ship or enable a catalog of built-in maintenance jobs for devkit infrastructure. These jobs should be opt-in, named consistently, module-aware where applicable, and safe to run across multiple nodes through normal scheduler leases.

All built-in maintenance jobs must:

- derive from `JobBase` or implement `IJob`
- define clear names, display names, descriptions and default groups
- be disabled by default unless the feature has a safe default retention behavior
- expose typed data/options for retention windows, batch sizes, archive flags and dry-run mode
- use public feature services or provider abstractions rather than private table access where possible
- record counts, affected ids where safe, skipped records and failure summaries in job messages/history
- support cancellation tokens and batch limits
- be testable through the xUnit harness

Proposed maintenance jobs:

| Job | Purpose |
| --- | ------- |
| `jobs-archive-occurrences` | Archives completed, failed or cancelled job occurrences after a retention window. |
| `jobs-purge-history` | Purges archived job execution history older than a configured retention period. |
| `jobs-release-expired-leases` | Releases or repairs expired scheduler leases after verifying provider ownership. |
| `jobs-recover-stuck-occurrences` | Moves abandoned due/running occurrences back to a recoverable state according to policy. |
| `jobs-detect-orphaned-runtime-state` | Reports or marks runtime-state rows whose registrations no longer exist. |

Acceptance criteria:

- Given a maintenance job is registered, when it runs on multiple nodes, then lease rules prevent duplicate mutation of the same scheduler occurrence.
- Given dry-run mode is enabled, when the job executes, then it records what would be changed without mutating target data.
- Given a batch limit is configured, when more records match, then the job processes at most the configured batch and records remaining-work properties.

### XUnit Harness Integration

The Jobs test harness should make outbound job integrations testable without real infrastructure.

Requirements:

- the harness must support registering jobs and triggers in memory
- the harness must support substituting fake or mocked `IMessageBroker`, `IQueueBroker`, `INotifier`, `IRequester`, `IPipelineFactory`, and `IOrchestrationService`
- the harness must support fake clock control for cron, delayed, one-time, startup-delay and missed-occurrence scenarios
- the harness must support in-memory occurrence, lease, execution and history stores
- the harness must expose assertions for materialized occurrences, execution status, messages, properties, previous-run context, retries, leases and idempotency
- the harness must support both declarative integration jobs and custom job implementations with the same observable runtime behavior
- outbound integration helpers should expose test-friendly seams for asserting mapped payloads and any target-specific properties supported by that integration
- tests must be able to run a single job, run one due occurrence, or run until idle
- tests must not require ASP.NET Core hosting, real EF storage, real brokers or wall-clock waiting

Example shape:

```csharp
var harness = JobSchedulerHarness.Create()
    .WithService(Substitute.For<IMessageBroker>())
    .WithMessagingPublishJob<InvoicePaidMessage>("publish-invoice-paid", job => job
        .WithDescription("Publishes an integration message after invoice processing.")
        .WithData<InvoicePaidJobData>()
        .WithMessage(message => message
            .Message(context => new InvoicePaidMessage
            {
                InvoiceId = context.Data.InvoiceId,
                Amount = context.Data.Amount,
            }))
        .AddTrigger("manual", trigger => trigger.Manual()));

await harness.DispatchAsync(
    "publish-invoice-paid",
    triggerName: "manual",
    data: new InvoicePaidJobData { InvoiceId = "INV-123", Amount = 42m });

var broker = harness.GetRequiredService<IMessageBroker>();
await broker.Received(1).Publish(
    Arg.Is<InvoicePaidMessage>(m => m.InvoiceId == "INV-123"),
    Arg.Any<CancellationToken>());

harness.History.ShouldContainMessage("publish-invoice-paid");
```

Acceptance criteria:

- Given a fake broker is registered, when a job publishes a message, then the harness can assert the mapped payload and any supported target properties.
- Given an outbound integration dependency is not registered, when the job executes that path, then the failed `Result` and execution history are assertable.
- Given a cron-triggered declarative outbound job has a previous successful execution, when the harness runs the job, then `ctx.PreviousSuccessfulExecution` is populated.

### Integration Configuration and Appsettings

Integration features should support code-first registration and appsettings overrides without making appsettings the source of truth.

Appsettings may configure:

- enabled state for outbound integration helpers
- feature-specific target names such as queue name or message type alias
- schedule overrides for built-in maintenance jobs
- host targeting for expensive jobs

Appsettings must not create active jobs or triggers. Every appsettings job or trigger entry must match a code-registered job or trigger.
Built-in maintenance job parameters such as batch size, retention window, dry-run mode and target filters must be supplied as typed dispatch data or explicit maintenance-service requests. They must not be resolved from appsettings, because maintenance behavior is operationally sensitive and code must remain leading.

### Integration Security and Operations

Operational endpoints and dashboards must expose integration properties safely.

Requirements:

- sensitive message data, queue content, domain event data, import rows and file contents must not be exposed by default
- properties should prefer identifiers, counts, types and hashes over full data bodies
- dashboard models should show source feature, source id, correlation id, causation id, module and group
- operational actions such as retry, archive, purge and repair must respect the authorization model of the owning feature
- built-in maintenance jobs should support dry-run mode before destructive purge operations
