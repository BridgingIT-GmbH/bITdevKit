# Jobs Feature - Agent Execution Prompts

This document contains bounded execution prompts for implementing the Jobs feature described in [spec-application-jobs.md](spec-application-jobs.md).

The prompts are designed for AI coding agents such as VS Code Copilot Agent Mode, Copilot CLI `/fleet`, Cursor Agents, Claude Code, or similar tools.

Use these prompts to implement the feature incrementally. Do not ask one agent to implement the entire Jobs feature in a single pass.

The prompts are intentionally split into:

- architecture analysis
- shared governance
- foundation and authoring model
- runtime and trigger execution
- persistence and provider boundaries
- concurrency, leases and recovery
- dependency, chaining and batch semantics
- query/API/endpoint layers
- integration features
- test harnesses and final hardening

Each implementation phase must end with build, test and review checkpoints.

---

# Architecture Analysis Prompt

Use this first. Do not allow implementation yet.

```text
Do not implement anything yet.

Read docs/specs/spec-application-jobs.md completely.
Read the repository architecture guidance and existing feature patterns before proposing implementation.

Your task right now is architectural analysis only.

Produce:

- subsystem decomposition
- project/package placement proposal
- dependency graph across Common, Application.Jobs, Infrastructure.EntityFramework.Jobs, Presentation endpoints and optional integration packages
- capability-layer implementation order
- critical runtime invariants
- trigger materialization invariants
- occurrence/execution/history separation
- retry and timeout invariants
- lease and multi-node concurrency risks
- persistence consistency risks
- batch semantics and storage risks
- dependency/chaining risks
- endpoint/query risks
- test strategy by phase
- suggested agent ownership boundaries
- unclear or ambiguous areas in the specification
- recommended implementation sequence

Do not create or modify files.
Do not scaffold projects.
Do not implement code.
Do not generate placeholder interfaces beyond analysis.

End with:

- architecture review checklist
- explicit go/no-go recommendation for starting Prompt 1
```

---

# Shared Governance Instructions

Add the following instruction block at the beginning of every implementation prompt.

```text
Before implementing, re-read the relevant sections of docs/specs/spec-application-jobs.md.

The Jobs specification is the primary behavioral source of truth.

General rules:

- implement only the scope requested in this prompt
- do not implement later phases early
- do not create unused abstractions, placeholder systems or speculative extension points
- do not persist job or trigger definitions in the database
- code-first registration is the source of truth for jobs and triggers
- appsettings may only override matching code-registered jobs and triggers
- public APIs use simple string and Guid identifiers where specified
- runtime code must not depend on Entity Framework, ASP.NET Core endpoint types or optional integration packages
- Infrastructure may depend on Application/Common, but Application must not depend on Infrastructure
- Presentation endpoints must depend on service/query abstractions, not provider tables
- Common abstractions must stay small and stable
- use Result/Result<T>/ResultPaged<T> for client-facing operations
- add XML documentation for public/protected APIs introduced in the phase
- add tests for every runtime behavior implemented in the phase

Runtime invariants to protect:

- triggers create occurrences; triggers do not execute jobs directly
- durable occurrences are persisted before execution
- a JobOccurrence is the scheduled unit of work
- a JobExecution is one attempt to run an occurrence
- JobExecutionHistory is append-oriented lifecycle history
- retries create additional JobExecution attempts for the same JobOccurrence
- leases protect durable occurrence execution and finalization
- no two scheduler instances may execute the same leased occurrence concurrently
- PreviousExecution is scoped to the same occurrence
- PreviousSuccessfulExecution is scoped to the same job/trigger before the current occurrence
- JobBatchOccurrence represents batch membership
- JobOccurrenceDependency represents ordering/prerequisites, including chaining
- batches are parallel grouping only, not dependency graphs or workflows

Before modifying files, state:

- affected capability layer
- affected projects/files
- runtime invariants touched
- persistence implications
- concurrency implications
- required tests

At the end of the phase:

- run a targeted build
- run relevant unit tests
- run relevant integration tests when provider or endpoint behavior changed
- summarize files changed
- summarize behaviors implemented
- list deferred non-goals
- list residual risks or assumptions
```

---

# Prompt 1 — Foundation and Public Contracts

```text
Implement the Jobs foundation contracts only.

Implementation focus:

- smallest Common abstractions required by Jobs:
  - IJob
  - IJob<TData>
  - IJobExecutionContext
  - IJobExecutionContext<TData>
  - minimal public status enums needed across packages
- Application.Jobs base types:
  - JobBase
  - JobBase<TData>
  - Unit data marker if not already available
- core model types for job definitions, trigger definitions, retry, timeout, priority, concurrency and metadata
- fluent registration builder for code-first jobs and triggers
- startup validation for job names, trigger names, data contracts and required descriptions
- appsettings merge model that only overrides matching code-registered jobs/triggers
- XML documentation and examples on public APIs

Implementation exclusions:

- do not implement hosted runtime
- do not implement background workers
- do not implement EF provider
- do not implement operational endpoints
- do not implement leases
- do not implement retries beyond configuration types
- do not implement outbound integrations
- do not create provider tables

Required tests:

- job registration succeeds for valid jobs
- duplicate job names fail validation
- duplicate trigger names within a job fail validation
- missing description fails validation
- JobBase<TData> infers the typed data contract
- mismatched explicit data contract fails validation
- appsettings can override enabled state and schedule for matching registrations
- appsettings unknown job or trigger fails fast
- runtime-state/database is not used as a definition source

Behavioral guarantees:

- jobs and triggers are code-first
- appsettings cannot create jobs or triggers
- public job contracts depend on interfaces, not concrete context classes
- no infrastructure dependency leaks into Common or Application runtime core

Validation checkpoint:

- build the affected projects
- run foundation unit tests
- review public API names before continuing
```

---

# Prompt 2 — Trigger Model, Cron Engine and Materialization Rules

```text
Implement trigger calculation and materialization logic without executing jobs.

Implementation focus:

- trigger types:
  - manual
  - one-time
  - delayed
  - startup-delay
  - cron
  - calendar if specified by existing scheduler-owned models
  - custom trigger provider interface only where needed by the spec
- IJobCronEngine abstraction
- default Cronos-backed cron implementation hidden behind IJobCronEngine
- 5-part and 6-part cron support
- explicit time zone behavior
- missed-occurrence policies: Skip, RunOnce, RunAll
- deterministic occurrence keys for trigger materialization
- controllable clock abstraction for tests

Implementation exclusions:

- do not execute jobs
- do not implement leases
- do not implement EF provider
- do not add operational endpoints
- do not add event-trigger adapters yet
- do not implement outbound integrations

Required tests:

- manual trigger materializes only on dispatch request
- one-time trigger materializes once
- delayed trigger persists/calculates stable due UTC
- startup-delay trigger calculates due UTC without blocking startup
- cron supports 5-part and 6-part expressions
- invalid cron expressions fail validation
- time zone and DST behavior is explicit
- missed occurrence policies produce correct materialization decisions
- deterministic occurrence keys prevent duplicate materialization

Behavioral guarantees:

- trigger materialization creates occurrences only
- trigger definitions are not persisted as authoritative database records
- Cronos types do not leak into public APIs
- all trigger calculations use the scheduler time abstraction

Validation checkpoint:

- build affected projects
- run trigger and cron unit tests
- review materialization invariants before runtime execution begins
```

---

# Prompt 3 — Persistence Abstractions and In-Memory Provider

```text
Implement provider-neutral persistence abstractions and the in-memory provider.

Implementation focus:

- IJobSchedulerStoreProvider
- IJobRuntimeStateStore
- IJobTriggerRuntimeStateStore
- IJobOccurrenceStore
- IJobOccurrenceDependencyStore
- IJobBatchStore
- IJobLeaseStore abstraction shape only, without distributed semantics yet
- IJobExecutionHistoryStore
- IJobPreviousExecutionStore
- IJobSchedulerQueryStore
- serializer integration through the devkit serializer abstraction
- in-memory provider for tests/local development
- append-oriented in-memory execution history
- in-memory occurrence, execution, dependency, batch and runtime-state storage

Implementation exclusions:

- do not implement Entity Framework
- do not implement endpoint models
- do not implement multi-node distributed correctness
- do not implement query endpoints
- do not implement provider-specific optimizations

Required tests:

- occurrence create/load/update behavior
- deterministic occurrence deduplication
- execution history append behavior
- previous execution lookup
- previous successful execution lookup
- dependency link storage and lookup
- batch creation and membership storage
- serializer is used for data/metadata boundaries
- in-memory provider does not act as authoritative job/trigger definition storage

Behavioral guarantees:

- persistence abstractions are provider-neutral
- runtime core depends on abstractions only
- JobBatchOccurrence stores batch membership
- JobOccurrenceDependency stores ordering/prerequisites only
- history is append-oriented

Validation checkpoint:

- build Application/Common projects
- run persistence abstraction and in-memory provider tests
- confirm no EF package reference in runtime core
```

---

# Prompt 4 — Runtime Dispatch and Execution Engine

```text
Implement the core runtime execution engine using the in-memory provider.

Implementation focus:

- IJobSchedulerService runtime/control methods for:
  - DispatchAsync<TJob>
  - DispatchAsync(jobName)
  - DispatchAndWaitAsync<TJob>
  - cancel/interrupt/retry/archive occurrence stubs only where needed by runtime state
- DI scope creation per execution
- job resolution
- IJobExecutionContext hydration
- typed data deserialization and validation
- execution record creation
- Result/exception capture
- execution attempt finalization
- append history at meaningful lifecycle boundaries
- inline dispatch semantics
- durable/manual dispatch default behavior

Implementation exclusions:

- do not implement background worker host yet
- do not implement durable leases yet
- do not implement EF provider
- do not implement operational endpoints
- do not implement batches beyond dispatch result compatibility
- do not implement outbound integration helpers

Required tests:

- dispatch by job type
- dispatch by job name
- dispatch rejects unknown job
- dispatch rejects disabled job/trigger
- dispatch rejects missing or ambiguous manual trigger
- typed data is available through IJobExecutionContext<TData>
- invalid data fails with Result failure
- job success records execution and history
- failed Result records failed attempt
- thrown exception records failed attempt
- DispatchAndWait returns completed execution result
- cancellation token reaches job

Behavioral guarantees:

- triggers and dispatch create occurrences before execution
- execution attempts are separate from occurrences
- history records lifecycle transitions
- job context is execution-scoped, not a workflow snapshot

Validation checkpoint:

- build runtime projects
- run runtime dispatch unit tests
- review occurrence/execution/history separation
```

---

# Prompt 5 — Background Scheduler, Workers and Trigger Scanning

```text
Implement background scheduler execution over the in-memory provider.

Implementation focus:

- hosted scheduler runtime
- bounded worker pool
- polling loop
- due occurrence scan
- trigger materialization scan
- stable ordering by priority, due UTC and provider tie-breaker
- worker targeting hooks only if required by current registration model
- graceful startup and shutdown
- scheduler instance identity for diagnostics
- server status model if already required by query contracts

Implementation exclusions:

- do not implement EF provider
- do not implement real distributed leases yet
- do not implement endpoints
- do not add speculative queueing/messaging integrations
- do not add dashboard UI

Required tests:

- due occurrences execute in priority/due order
- batch size limits scans
- worker pool max concurrency is respected
- disabled/paused jobs or triggers do not materialize new work
- scheduler startup-delay triggers do not block host startup
- graceful shutdown stops dispatching new work
- in-flight jobs receive cancellation during shutdown

Behavioral guarantees:

- worker memory is not the source of truth for durable behavior
- background execution uses the same pipeline as manual dispatch
- no job starts unless its active job and trigger registration still exist

Validation checkpoint:

- build runtime projects
- run scheduler worker tests
- review runtime logs for lifecycle clarity
```

---

# Prompt 6 — Retry, Timeout, Cancellation, Pause and Resume

```text
Implement resilience control semantics.

Implementation focus:

- retry policy evaluation
- attempt counters
- RetryScheduled occurrence state
- next retry due UTC
- timeout handling and cancellation request
- cooperative cancellation
- interrupt semantics
- cancel occurrence
- pause/resume job
- pause/resume trigger
- pause/resume occurrence
- archive occurrence
- Result failures for invalid lifecycle transitions
- lifecycle history for all control actions

Implementation exclusions:

- do not implement EF provider
- do not implement endpoint routes
- do not implement batch control yet
- do not add advanced exception-classification beyond what the spec requires

Required tests:

- failed execution schedules retry for same occurrence
- retry creates a new JobExecution attempt, not a new occurrence
- retry exhaustion marks occurrence failed
- timeout records timed-out execution outcome
- cancellation before execution marks occurrence cancelled
- cancellation during execution propagates token
- interrupt is recorded distinctly from cancellation
- pause prevents new attempts from starting
- resume restores eligibility
- invalid transitions return Result failure

Behavioral guarantees:

- retry attempts preserve original occurrence identity
- retry state is persisted before the occurrence becomes eligible
- cancellation, timeout and interrupt outcomes are distinguishable
- pause/resume does not mutate code-first definitions

Validation checkpoint:

- build runtime projects
- run retry/control tests
- review lifecycle transition matrix
```

---

# Prompt 7 — Leases, Ownership and Recovery

```text
Implement lease and ownership semantics.

Implementation focus:

- lease acquisition abstraction behavior
- lease renewal
- lease expiration
- lease ownership verification before finalization
- lease loss handling
- abandoned occurrence recovery
- stale worker protection
- provider-neutral concurrency checks around state mutation
- in-memory lease behavior sufficient for deterministic tests

Implementation exclusions:

- do not implement EF provider yet
- do not implement endpoint repair operations yet
- do not add cluster membership or leader election
- do not implement sharding or partition rebalancing

Required tests:

- only one worker can acquire an occurrence lease
- concurrent acquisition attempts are rejected
- lease renewal extends ownership
- expired leases become recoverable
- worker cannot finalize after lease loss
- recovered occurrence returns to a valid runnable/retry/failure state
- shutdown releases or expires leases according to policy

Behavioral guarantees:

- all durable execution attempts run under occurrence-scoped ownership
- final state mutation requires current ownership
- lease recovery resumes from persisted occurrence/execution state
- leases do not replace job-level idempotency

Validation checkpoint:

- build runtime projects
- run concurrency and lease tests
- perform code review focused only on ownership boundaries
```

---

# Prompt 8 — Dependencies and Chaining

```text
Implement occurrence dependencies and job chaining.

Implementation focus:

- JobOccurrenceDependency runtime behavior
- occurrence dependency creation
- chain dependency creation from fluent .Then(...) registrations
- blocked occurrence evaluation
- release dependent occurrences when prerequisites satisfy required outcome
- failure policy behavior:
  - KeepBlocked
  - Skip
  - Cancel
  - Fail
- history entries for dependency state changes
- queryable blocked reason data

Implementation exclusions:

- do not use JobOccurrenceDependency for batch membership
- do not implement workflow/SAGA compensation
- do not implement orchestration feature behavior
- do not implement endpoint layer yet
- do not add nested batches or batch continuations

Required tests:

- chained successor materializes as normal occurrence
- successor waits for predecessor success
- predecessor retry keeps successor blocked
- predecessor retry exhaustion applies dependency failure policy
- blocked occurrences are not leased
- dependency release makes occurrence due/runnable
- dependency state survives in-memory provider restart simulation where possible
- batch membership never creates dependency links

Behavioral guarantees:

- dependencies are occurrence-level prerequisites
- chaining materializes normal occurrences
- each chain step has its own lease, retry, timeout, history and data
- batches remain parallel grouping only

Validation checkpoint:

- build runtime projects
- run dependency/chaining tests
- review dependency vs batch separation
```

---

# Prompt 9 — Batch Runtime Semantics

```text
Implement batch runtime behavior.

Implementation focus:

- CreateBatchAsync
- DispatchBatchAsync
- AttachToBatchAsync
- JobBatchCreateRequest
- JobBatchDispatchRequest
- JobBatchDispatchItem
- JobBatchDispatchResult
- JobBatchCompletionPolicy
- JobBatchStatus roll-up
- JobBatchOccurrence membership
- atomic in-memory batch creation with child occurrences
- batch retry/cancel/pause/resume/archive operations
- child status projection and roll-up updates
- idempotency for batch create/dispatch/attach

Implementation exclusions:

- do not implement nested batches
- do not implement batch continuations
- do not use JobOccurrenceDependency for batch membership
- do not implement endpoint routes yet
- do not implement EF provider yet

Required tests:

- empty batch creation
- dispatch batch creates batch, occurrences and child links atomically
- attach adds new child occurrences atomically
- failed partial accept leaves no runnable orphaned child occurrence
- batch status rolls up from child statuses
- RequireAllSucceeded yields Failed on child failure
- AllowPartialCompletion yields CompletedWithFailures
- retry batch retries eligible failed children only
- cancel batch prevents not-yet-started children from executing
- pause/resume batch maps to eligible child occurrences
- archive batch respects retention state

Behavioral guarantees:

- batches are grouping and bulk operation membership only
- child occurrences execute independently through normal occurrence pipeline
- batch cancellation must prevent new child starts after cancellation is accepted
- batch roll-up must be repairable from persisted child state

Validation checkpoint:

- build runtime projects
- run batch tests
- review batch atomicity and membership model
```

---

# Prompt 10 — Job Test Harness

```text
Implement Jobs test harness support.

Implementation focus:

- job unit test context builders
- scheduler test harness with in-memory provider
- fake clock
- trigger materialization helpers
- dispatch helpers
- history assertions
- retry assertions
- batch assertions
- dependency/chaining assertions
- cancellation/timeout helpers
- substitutes/fakes for injected dependencies

Implementation exclusions:

- do not implement EF provider
- do not implement endpoint tests yet
- do not add optional outbound integrations beyond fakeable test seams

Required tests:

- job can run with synthetic IJobExecutionContext<TData>
- harness can materialize cron/manual/delayed/startup triggers
- harness can advance fake time deterministically
- harness can assert messages, properties and history
- harness can assert retry attempts
- harness can assert blocked dependencies
- harness can assert batch roll-up

Behavioral guarantees:

- tests do not depend on real sleeping/background timing
- tests do not require provider internals for normal assertions
- harness uses the same runtime pipeline where practical

Validation checkpoint:

- build test projects
- run harness tests
- add at least one example test for each major runtime capability
```

---

# Prompt 11 — Entity Framework Provider

```text
Implement the Entity Framework provider.

Implementation focus:

- Infrastructure.EntityFramework.Jobs package/project
- IJobSchedulerContext capability interface
- EF persistence models from the specification
- Job runtime state table
- Trigger runtime state table
- Occurrence table
- Occurrence dependency table
- Batch table
- Batch occurrence table
- Execution table
- Execution history table
- Lease table
- EF store implementations
- transactional acceptance for:
  - occurrence materialization
  - dispatch
  - batch dispatch/attach
  - dependency creation
  - execution finalization
- optimistic concurrency using ConcurrencyVersion
- serializer integration
- UseModelConfiguration() or equivalent

Implementation exclusions:

- do not add endpoints
- do not add dashboard UI
- do not change runtime core to depend on EF
- do not persist authoritative job/trigger definitions
- do not add database-specific behavior without provider abstraction

Required tests:

- EF context can host scheduler sets with application DbContext
- migrations/model configuration include required tables and indexes
- occurrence deduplication by deterministic key
- lease uniqueness and ownership checks
- execution/history persistence
- previous successful execution lookup
- dependency release/failure persistence
- batch atomic creation and attach transaction behavior
- batch roll-up repair after stale state
- archive/purge queries avoid active working set scans

Behavioral guarantees:

- EF provider preserves runtime semantics exactly
- runtime core has no EF references
- consuming application owns migrations
- provider writes runtime state, occurrences, leases, dependencies, batches, executions and history only

Validation checkpoint:

- build solution
- run EF integration tests
- inspect generated model/indexes
- review transaction boundaries before continuing
```

---

# Prompt 12 — Query Services, Metrics and Dashboard Contracts

```text
Implement query services and metrics.

Implementation focus:

- IJobSchedulerQueryService
- query models:
  - job definitions merged with runtime state
  - triggers
  - recurring triggers
  - occurrences
  - retry view
  - batches
  - batch child occurrences
  - executions
  - execution history
  - leases
  - scheduler instances/servers
  - metrics
  - dashboard summary/timeline models
- paging, sorting and filtering
- enum-bound status filters
- safe data/metadata previews
- provider capability flags where necessary

Implementation exclusions:

- do not implement HTTP endpoints yet
- do not expose provider tables directly
- do not include dashboard UI layout or visual concerns
- do not add new runtime behavior

Required tests:

- query jobs with runtime state overlay
- query triggers and recurring triggers
- query occurrences by status/date/job/trigger/correlation
- query retries
- query batch summaries and child occurrences
- query execution history
- query leases
- query metrics from persisted data
- paging and sorting are deterministic
- sensitive serialized data is not returned by default

Behavioral guarantees:

- query data comes from active registrations plus persisted provider state
- worker-local memory is not the durable source of truth
- query contracts do not leak EF types
- dashboard contracts remain query/API obligations, not UI product specs

Validation checkpoint:

- build query projects
- run query and metrics tests
- review response models for data safety
```

---

# Prompt 13 — Operational Endpoints

```text
Implement optional administration endpoints.

Implementation focus:

- endpoint registration extension
- route mapping under /api/_system/jobs
- Result-to-HTTP mapping
- ProblemDetails mapping
- read endpoints for jobs, triggers, recurring triggers, occurrences, retries, batches, executions, history, leases, servers and metrics
- control endpoints for dispatch, job enable/disable/pause/resume, trigger enable/disable/pause/resume
- occurrence endpoints for cancel, interrupt, retry, archive and repair/release-lease
- bulk occurrence endpoints
- batch endpoints for create, attach, retry, cancel, pause, resume and archive
- purge endpoint
- authorization hooks

Implementation exclusions:

- do not implement dashboard UI
- do not reference EF directly
- do not implement new runtime behavior
- do not expose private provider tables

Required tests:

- endpoint route registration
- successful read endpoints return 200
- successful control endpoints return 200
- missing jobs/triggers/occurrences return 404
- invalid request bodies return 400 ProblemDetails
- invalid lifecycle transitions return 409 ProblemDetails
- unexpected failures map to 500 ProblemDetails
- authorization can be required by host configuration
- batch endpoints call service abstractions correctly

Behavioral guarantees:

- endpoints are thin adapters over service/query abstractions
- endpoints do not bypass Result handling
- endpoint models normalize strings before calling runtime/query services

Validation checkpoint:

- build presentation projects
- run endpoint integration tests
- review route surface against specification
```

---

# Prompt 14 — Outbound Integration Helpers

```text
Implement outbound job integration helpers.

Implementation focus:

- core Requester integration helpers because Requester abstractions live in Common
- core Notifier integration helpers because Notifier abstractions live in Common
- optional package extension points for:
  - Messaging publish jobs
  - Queueing send jobs
  - Pipeline execute jobs
  - Orchestration execute jobs
- declarative integration job registration methods
- context-to-payload factories
- target-specific metadata mapping where target abstraction supports it
- clear failure Result when required target abstraction is not registered

Implementation exclusions:

- do not make scheduler core depend on Messaging, Queueing, Pipeline or Orchestration packages
- do not implement event-trigger adapters in this prompt
- do not add generic catch-all integration builders
- do not bypass normal job runtime/retry/history behavior

Required tests:

- Requester job sends mapped request
- Notifier job publishes mapped notification
- missing Requester/Notifier dependency fails job clearly
- optional Messaging/Queueing helpers compile only in optional packages
- integration jobs use normal execution history and retry behavior
- context data, correlation and PreviousSuccessfulExecution can be mapped into payloads

Behavioral guarantees:

- outbound integrations are normal jobs
- target feature owns its own lifecycle after dispatch/publish
- optional integrations remain optional
- scheduler core remains low-coupled

Validation checkpoint:

- build core and optional integration projects
- run integration helper tests
- review dependency graph for unwanted package references
```

---

# Prompt 15 — Event Trigger Adapters and Built-In Maintenance Jobs

```text
Implement event-trigger adapters and built-in maintenance jobs.

Implementation focus:

- provider-neutral event-trigger adapter model
- Notifier event-trigger adapter where Common abstractions allow it
- optional Messaging/Queueing event-trigger adapters in optional packages
- idempotency keys for accepted events
- transaction-boundary documentation/tests for event-to-occurrence materialization
- built-in maintenance jobs:
  - purge history
  - release expired leases
  - recover stuck occurrences
  - detect orphaned runtime state
- dry-run mode and batch limits for maintenance jobs

Implementation exclusions:

- do not treat Requester as an event source unless a separate request-observation adapter is explicitly designed
- do not couple scheduler core to Messaging/Queueing packages
- do not bypass occurrence materialization or lease pipeline
- do not add unrelated operational jobs

Required tests:

- accepted event materializes one occurrence idempotently
- duplicate event does not create duplicate occurrence
- adapter failure returns Result/logs diagnostics
- missing optional adapter dependency fails clearly
- maintenance jobs respect leases and batch limits
- dry-run maintenance records what would happen
- maintenance jobs write history/diagnostics

Behavioral guarantees:

- event triggers create occurrences asynchronously
- source event identity or idempotency key prevents duplicates
- maintenance jobs are ordinary jobs using scheduler/provider abstractions

Validation checkpoint:

- build optional integration projects
- run adapter and maintenance tests
- review transaction/idempotency behavior
```

---

# Prompt 16 — Migration Guidance, Examples and Developer Documentation

```text
Implement documentation and source-level migration guidance.

Implementation focus:

- migration notes from Application.JobScheduling to Application.Jobs
- examples that compile against the implemented API:
  - database cleanup
  - batch reprocessing
  - chaining
  - outbound integration helper
- XML documentation examples for public APIs
- README/docs feature page if required by repository conventions

Implementation exclusions:

- do not change runtime behavior
- do not introduce new public APIs only for documentation
- do not document unsupported future behavior

Required tests:

- example code compiles where examples are included in test projects
- documentation snippets match actual API names
- migration mappings are accurate

Behavioral guarantees:

- examples reinforce code-first trigger registration
- examples use current dispatch and batch APIs
- examples do not imply persisted dynamic trigger definitions

Validation checkpoint:

- build documentation sample tests if available
- run docs link/snippet checks if available
- review examples against public API
```

---

# Final Hardening Prompt

```text
Perform final hardening and cleanup only.

Implementation focus:

- XML documentation completeness
- public API naming consistency
- code-first registration consistency
- lifecycle transition consistency
- retry, lease and timeout edge cases
- provider transaction boundaries
- batch atomicity and roll-up repair
- dependency/chaining behavior
- endpoint error mappings
- data redaction/safe previews
- test coverage gaps
- dead code removal
- unused abstraction removal
- performance review for hot paths

Do not implement new features.
Do not add new optional capabilities.
Do not change public API shape unless fixing a reviewed inconsistency.
Do not add placeholders for future providers or dashboards.

Verify:

- runtime core has no EF dependency
- runtime core has no ASP.NET Core endpoint dependency
- runtime core has no optional Messaging/Queueing/Pipeline/Orchestration dependency
- EF provider has no endpoint dependency
- endpoint layer depends only on services/query abstractions
- job and trigger definitions are never loaded from database as authoritative definitions
- retries create new execution attempts for the same occurrence
- leases protect durable occurrence finalization
- batch membership uses JobBatchOccurrence
- dependencies/chaining use JobOccurrenceDependency
- all client-facing operations return Result/Result<T>/ResultPaged<T>
- tests cover happy paths, failure paths and concurrency-sensitive paths

Run:

- full solution build
- all unit tests
- all integration tests
- analyzers
- formatting
- architecture/dependency review

End with:

- final risk list
- public API review summary
- test coverage summary
- performance/concurrency review summary
- recommendation for human acceptance
```

---

# Optional Fleet Prompt

Use this with Copilot CLI `/fleet` only after Prompt 1 foundation boundaries are approved.

```text
/fleet

Agent A:
Implement Foundation and Authoring Model from Prompt 1.
Own Common abstractions, JobBase types, definitions, builder API and validation tests.
Do not touch runtime workers, EF provider or endpoints.

Agent B:
Implement Trigger Model and Cron Materialization from Prompt 2.
Own trigger calculators, cron engine, missed-occurrence policy and trigger tests.
Do not execute jobs and do not touch EF provider.

Agent C:
Implement Persistence Abstractions and In-Memory Provider from Prompt 3.
Own store interfaces, in-memory provider, serializer boundaries and persistence tests.
Do not touch endpoints or EF provider.

Agent D:
Implement Test Harness foundations from Prompt 10 after Agents A-C expose stable seams.
Own fake clock, context builders and harness assertions.

Fleet rules:

- do not modify the same files simultaneously
- communicate interface assumptions explicitly
- keep changes small and reviewable
- compile after each major change
- add tests continuously
- stop when an architectural seam is unclear
- do not implement EF, endpoints, integrations or hardening in this fleet run
```

Optional later fleet after runtime is stable:

```text
/fleet

Agent A:
Implement EF provider from Prompt 11.

Agent B:
Implement query services and metrics from Prompt 12.

Agent C:
Implement operational endpoints from Prompt 13.

Agent D:
Implement outbound integration helpers from Prompt 14.

Fleet rules:

- Agent B may depend on provider/query abstractions but must not read EF tables directly outside provider stores
- Agent C must depend only on service/query abstractions
- Agent D must keep optional package dependencies optional
- all agents must run targeted builds/tests before handoff
- pause for human review before merging branch outputs
```

---

# Recommended Execution Order

1. Architecture Analysis Prompt
2. Prompt 1 — Foundation and Public Contracts
3. Prompt 2 — Trigger Model, Cron Engine and Materialization Rules
4. Prompt 3 — Persistence Abstractions and In-Memory Provider
5. Prompt 4 — Runtime Dispatch and Execution Engine
6. Prompt 5 — Background Scheduler, Workers and Trigger Scanning
7. Prompt 6 — Retry, Timeout, Cancellation, Pause and Resume
8. Prompt 7 — Leases, Ownership and Recovery
9. Prompt 8 — Dependencies and Chaining
10. Prompt 9 — Batch Runtime Semantics
11. Prompt 10 — Job Test Harness
12. Prompt 11 — Entity Framework Provider
13. Prompt 12 — Query Services, Metrics and Dashboard Contracts
14. Prompt 13 — Operational Endpoints
15. Prompt 14 — Outbound Integration Helpers
16. Prompt 15 — Event Trigger Adapters and Built-In Maintenance Jobs
17. Prompt 16 — Migration Guidance, Examples and Developer Documentation
18. Final Hardening Prompt

Stop after every phase.
Run the specified build/test checkpoint.
Review architecture before continuing.
Do not let later phases backfill foundational semantics without human approval.
Validate API shape before continuing.
