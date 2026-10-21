---
title: Pipeline or Orchestration
---

<img class="bdk-doc-icon" src="assets/features/pipelines.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/orchestrations.svg" width="32" height="32" alt="" align="right">

# Pipeline or Orchestration

Both features compose work from steps. The key question is whether the process needs persisted business state between executions.

## Choose Pipeline when

- work follows a known sequence within the current process
- steps share a context and return explicit success or failure results
- retries, early exits, hooks, and execution behaviors are enough
- background execution needs progress tracking but does not need restart recovery

Pipelines support awaited execution and `ExecuteAndForgetAsync(...)`. Background execution and tracking do not make a pipeline a durable workflow.

## Choose Orchestration when

- the process has explicit business states and transitions
- work waits for an external signal, approval, or timer
- instances need pause, resume, cancellation, or inspection
- the workflow must resume after a restart with durable persistence configured

Orchestrations use in-memory persistence by default. Configure the Entity Framework provider and background execution when recovery across restarts matters.

## Comparison

| Concern | Pipeline | Orchestration |
| --- | --- | --- |
| Main model | Ordered steps and a shared context | States, activities, and transitions |
| Execution | Awaited or background execution in the current process | Inline execution or background progression |
| External waiting | Application code owns coordination | Signals and timers belong to the workflow model |
| Recovery | Background tracking does not provide durable recovery | Durable provider and recovery service resume eligible work |
| Typical use | Import, validation, enrichment, and synchronization | Approval, fulfillment, and timeout-driven business processes |

## Example

An import that reads weather data, validates it, maps records, and saves them fits a pipeline.

An order that waits for payment, requests approval, and cancels after a deadline fits an orchestration. One activity can execute a pipeline for its local processing steps.

Choose based on persistence and waiting requirements. A long calculation can still be a pipeline, while a short approval process can need orchestration.

## Related features

- [Pipelines](reference/features-pipelines.md)
- [Orchestrations](reference/features-orchestrations.md)
- [Jobs](reference/features-jobs.md) for scheduled execution
