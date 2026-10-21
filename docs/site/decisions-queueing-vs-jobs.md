---
title: Queueing or Jobs
---

<img class="bdk-doc-icon" src="assets/features/queueing.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/jobs.svg" width="32" height="32" alt="" align="right">

# Queueing or Jobs

Queueing dispatches work items to a logical consumer. Jobs manages job definitions, triggers, occurrences, and execution attempts. Both can run background work.

## Choose Queueing when

- a producer sends a work item that one registered handler owns
- producers and consumers communicate through a queue contract
- a queue transport or competing workers fit the deployment
- waiting work, subscriptions, and queue controls are the main operational concerns

`IQueueBroker` enqueues messages for `IQueueMessageHandler<TMessage>` implementations. Each queued message type has one logical handler registration, even when several workers compete for work.

## Choose Jobs when

- work runs on a recurring schedule, calendar, delay, or manual trigger
- operators need to inspect occurrences, attempts, and execution history
- job-level concurrency, chaining, batches, or interruption matter
- accepted events should create scheduler-managed work

`IJobSchedulerService` dispatches registered jobs. A manual job can fit even without a recurring schedule when execution history and scheduler controls matter.

## Comparison

| Concern | Queueing | Jobs |
| --- | --- | --- |
| Main contract | Work message and its consumer | Job definition and triggers |
| Unit of work | Queued message | Occurrence with execution attempts |
| Timing | Consumer processes available work according to broker semantics | Trigger and scheduler determine when work becomes due |
| Operations | Waiting messages, subscriptions, retry, archive, and pause controls | Run history, statistics, trigger, pause, resume, and interrupt |
| Typical use | Generate a report from an incoming work item | Run nightly cleanup or a scheduled import |

## Persistence and repeated execution

The in-process queue is local to one process and does not provide automatic retry after a handler failure. Use an appropriate durable broker when messages must survive restarts.

Jobs uses in-memory persistence by default. Configure its Entity Framework provider when occurrences and recovery must survive restarts.

One logical consumer does not guarantee exactly one execution. Broker redelivery, retries, and scheduler recovery can repeat work. Make external side effects safe to repeat.

## Example

Enqueue a report-generation message when a user submits a request. Schedule a Job to remove expired reports each night.

The features can work together. A Job can enqueue work, and the Queueing integration can capture accepted events for job triggers when configured. Give each component a clear responsibility for retries and execution ownership.

## Related features

- [Queueing](reference/features-queueing.md)
- [Jobs](reference/features-jobs.md)
- [Messaging or Queueing](decisions-messaging-vs-queueing.md)
- [Jobs or Startup Tasks](decisions-jobs-vs-startup-tasks.md)
