---
title: Jobs or Startup Tasks
---

<img class="bdk-doc-icon" src="assets/features/jobs.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/startup-tasks.svg" width="32" height="32" alt="" align="right">

# Jobs or Startup Tasks

Use Startup Tasks for initialization tied to a host start. Use Jobs when the application needs scheduling, retained execution state, or operational control.

## Choose Startup Tasks when

- work belongs to each application start
- the task prepares a cache, checks resources, or seeds development data
- start and completion logs provide enough visibility
- initialization can begin after the host reports that it has started

`AddStartupTasks(...)` runs `IStartupTask` implementations after `ApplicationStarted`. Startup tasks do not block the host from reaching that state.

## Choose Jobs when

- work runs on a schedule, after a delay, or on operator demand
- one-time work needs scheduler-managed occurrences and execution history
- retries, concurrency controls, pause, resume, or interruption matter
- work needs recovery or coordination across hosts with durable persistence configured

`AddJobScheduler(...)` registers jobs and triggers. The in-memory provider supports transient execution. Configure the Entity Framework provider for durable state and recovery.

## Comparison

| Concern | Startup Tasks | Jobs |
| --- | --- | --- |
| Trigger | Each host start, with optional delays | Manual, cron, calendar, delayed, one-time, or event triggers |
| State | Host-local initialization and logs | Occurrences, execution attempts, history, and leases |
| Repetition | Runs again when the host starts again | Defined by triggers and dispatch calls |
| Operations | Enablement, delays, and task behaviors | Query, trigger, pause, resume, interrupt, and maintenance APIs |
| Typical use | Prepare local startup state | Recurring cleanup or retryable background processing |

## Execution details that affect the choice

Startup tasks run concurrently. `Order` sorts their launch order and does not make one task wait for another. Put dependent initialization steps in one task or an explicit pipeline.

Each host runs its own startup tasks. Shared seeding or resource creation must tolerate repeated execution. Jobs can also execute again after retries or recovery, so their external side effects need the same care.

If initialization must finish before dependent requests run, define an explicit readiness requirement. Registering a Startup Task alone does not provide that guarantee.

## Example

Prepare an instance's local reference-data cache with a Startup Task. Delete expired weather records every night with a Job. A Job can also invoke a Pipeline for its processing steps.

## Related features

- [Startup Tasks](reference/features-startuptasks.md)
- [Jobs](reference/features-jobs.md)
- [Pipelines](reference/features-pipelines.md)
