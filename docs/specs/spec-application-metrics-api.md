---
status: draft
---

# Design Specification: Application Metrics Feature and Metrics API

> This design document specifies the complete devkit metrics feature. It covers metric creation across application features through optional behaviors and the existing global orchestration behavior pipeline, read-only HTTP endpoints in `Presentation.Web` for dashboard-oriented live metric consumption, and an operations page in the DoFiesta app that consumes those APIs for live dashboards.

[TOC]

## 1. Introduction

The devkit already emits a small set of runtime metrics:

- messaging publish counters from `Application.Messaging`
- messaging handler counters from `Application.Messaging`
- domain event creation counters from `Domain.Repositories`

Those metrics prove the value of lightweight, behavior-driven runtime telemetry, but the current coverage is incomplete. Important application flows such as requester, notifier, queueing, job scheduling, and orchestrations do not yet expose a consistent built-in metrics model. At the same time, there is no first-class HTTP surface for dashboards that want to read live totals from inside the application itself, and there is no reference UI that demonstrates how applications should consume those metrics.

This specification defines one complete metrics feature with three capability areas:

- runtime metric creation across devkit features
- live metrics consumption through read-only endpoints in `Presentation.Web`
- a DoFiesta operations page that visualizes those metrics through polling dashboards

The feature is intended to remain lightweight, additive, and aligned with the existing behavior-based composition style used across the devkit.

---

## 2. Goals

### 2.1 Consistent runtime telemetry across devkit features

The feature shall provide a coherent metrics model across:

- `Application.Messaging`
- requester
- notifier
- `Application.Queueing`
- `Application.JobScheduling`
- `Application.Orchestrations`
- domain event creation

### 2.2 Preserve backward compatibility

Existing metric names and the existing meter identity shall remain valid. New metrics must be additive.

### 2.3 Keep metric creation opt-in where behaviors are used

Requester, notifier, queueing, job scheduling, and orchestration metrics shall be enabled through explicit behavior registration so that adopters can choose the telemetry surface they want.

### 2.4 Make metrics useful for real dashboards

The metric model shall support:

- live totals
- failure totals
- latency visualization
- top-N charts
- polling-based time series

without requiring persistent metric storage inside the application.

### 2.5 Fit the existing architecture

The implementation shall reuse:

- behavior pipelines
- builder registration patterns
- `System.Diagnostics.Metrics`
- existing `Presentation.Web` endpoint conventions

### 2.6 Support application-internal dashboards without replacing standard observability

The devkit metrics API shall complement, not replace:

- OpenTelemetry exporters
- vendor monitoring backends
- platform-native runtime instrumentation

---

## 3. Non-goals

### 3.1 No server-side metric history store

The application shall not persist metric history for charts in the metrics API or the DoFiesta operations page. Historical charts are built client-side by polling live snapshots and appending points over time.

### 3.2 No tag-rich metrics model in the initial design

The initial design shall not depend on metric tags for filtering, grouping, or querying. Metric families remain name-encoded for consistency with the existing implementation.

### 3.3 No cluster-wide aggregation

All runtime metrics in this feature are process-local. Cross-instance aggregation remains the responsibility of external observability tooling.

### 3.4 No replacement of persisted operational query models

The metrics API shall not replace durable feature-specific operational endpoints such as orchestration query and metrics endpoints that are backed by persisted state.

### 3.5 No raw meter dump endpoint

The metrics API shall expose curated, dashboard-friendly metrics. It shall not expose an unfiltered dump of every instrument in the process.

### 3.6 No new web package for metrics

The metrics API shall be implemented inside the existing `src/Presentation.Web` project under a dedicated `Metrics/` folder.

---

## 4. Capability areas

The complete metrics feature has three connected capability areas:

- runtime metric creation through built-in optional behaviors and execution hooks
- live metrics consumption through read-only endpoints in `src/Presentation.Web/Metrics`
- a DoFiesta operations page that consumes those endpoints as a reference dashboard implementation

These capability areas are specified together because they depend on each other conceptually:

- emitted metrics define what the API can expose
- the API defines what dashboards can poll
- the DoFiesta operations page validates that the overall contract is useful in a real application

---

## 5. Compatibility baseline

The existing baseline must remain intact.

### 5.1 Meter identity

The meter name remains:

- `"bdk"`

### 5.2 Existing metric families

The following existing metrics remain unchanged:

- `messaging_publish`
- `messaging_publish_{message}`
- `messaging_publish_failure`
- `messaging_publish_{message}_failure`
- `messaging_handle`
- `messaging_handle_{message}`
- `messaging_handle_failure`
- `messaging_handle_{message}_failure`
- `domainevents_create`
- `domainevents_create_{event}`

---

## 6. Core design principles

### 6.1 Metrics are additive

New metrics must not invalidate or rename existing metrics.

### 6.2 Emission stays close to execution

Metrics should be emitted by the same pipeline or runtime surface that owns the relevant execution step:

- publisher behavior for publish dispatch
- handler behavior for handler execution
- enqueuer behavior for enqueue dispatch
- queue handler behavior for queue handling
- orchestration activity behavior for orchestration activity execution

### 6.3 Counters and durations are both first-class

Counts alone are not sufficient for useful dashboards. The design therefore includes both:

- cumulative counters
- duration histograms

where the underlying execution flow naturally supports them.

### 6.4 Failures must reflect meaningful failures

Where a feature uses the devkit `Result` pattern, failure metrics should count both:

- thrown exceptions
- returned failed results

Where a feature does not use `Result`, failure metrics are exception-based.

### 6.5 Snapshots are cumulative since process start

The metrics API returns point-in-time cumulative values. A dashboard builds a time series by polling and appending those snapshots.

### 6.6 Metric names must be stable and normalized

Dynamic metric name fragments must be transformed into a stable lower-case representation. The implementation shall not rely on raw CLR names that include unstable punctuation or generic formatting artifacts.

### 6.7 The API is read-only

The metrics API introduces no write, reset, archive, or mutation endpoints.

---

## 7. Metrics creation architecture

### 7.1 Feature coverage

This specification covers metric emission for:

- existing messaging publish and handle behaviors
- existing repository domain event metrics behavior
- requester send and handler execution
- notifier publish and handler execution
- queueing enqueue and handler execution
- job scheduling execution
- orchestration activity execution

### 7.2 Shared naming helper

The implementation shall introduce a small internal helper responsible for:

- metric family construction
- lower-case normalization
- stable type-name formatting
- orchestration activity name formatting

Recommended responsibilities:

- normalize `CustomerCreatedNotification` -> `customercreatednotification`
- normalize closed generic names into stable readable tokens
- prevent ad hoc string concatenation across behaviors

This helper is an internal implementation detail and is not intended as public API.

### 7.3 Metric kinds

The metrics feature uses:

- `Counter<int>` for cumulative totals
- `Histogram<double>` for durations

Duration values shall use milliseconds as the unit.

### 7.4 Cancellation semantics

If a pipeline is never entered because cancellation is already requested before execution starts, no metrics are emitted for that operation.

If execution begins and then ends in cancellation, the operation counts as a failure only when the feature’s observable result is a failed `Result` or an exception path that the runtime treats as failure.

### 7.5 Requester and notifier behaviors

#### 7.5.1 Requester metrics behavior

Add:

- `MetricsRequestBehavior<TRequest, TResponse>`

This behavior is registered through the existing requester behavior model and only processes request flows.

It shall emit:

- send metrics for dispatch-level attempts
- handle metrics for actual handler execution
- failure metrics for thrown exceptions and failed `Result` responses
- duration histograms for send and handle

The explicit `requester_handle_*` family is required even though requester resolves a single logical handler. This keeps the metric model consistent with other features and avoids special-casing requester in dashboards.

#### 7.5.2 Notifier metrics behaviors

Add:

- `MetricsNotificationBehavior<TRequest, TResponse>`
- `MetricsNotificationHandlerBehavior<TRequest, TResponse>`

The notification-level behavior emits publish metrics once per notification dispatch.

The handler-specific behavior emits handler metrics once per handler execution.

The handler-specific behavior shall return `true` from `IsHandlerSpecific()`.

Failure semantics:

- publish failure counts a failed overall notification publish result or thrown exception
- handler failure counts a failed handler result or thrown exception

This distinction is important because notifier can execute multiple handlers in sequential, concurrent, or fire-and-forget modes.

### 7.6 Queueing behaviors

Add:

- `MetricsQueueEnqueuerBehavior`
- `MetricsQueueHandlerBehavior`

These are registered through the existing queueing `.WithBehavior(...)` surface.

Queueing metrics are scoped to:

- enqueue dispatch
- actual queue handler execution

Queue lifecycle states such as pending, waiting for handler, dead-lettered, archived, paused, or lease-held are not created by these behaviors. Those states remain the responsibility of queue provider query services and operational endpoints.

### 7.7 Job scheduling behavior

Add:

- `MetricsJobSchedulingBehavior`

This behavior is registered through the existing job scheduling `.WithBehavior(...)` surface and wraps actual job execution through `IJobSchedulingBehavior`.

It shall emit:

- job execution attempt counters
- job execution failure counters
- job execution duration histograms

Because job scheduling executes concrete jobs through a behavior pipeline, the metrics behavior should measure actual wrapped job execution rather than registration or scheduling operations.

Failure semantics:

- failed `Result` values count as failures when a job models an expected unsuccessful outcome through the devkit result pattern
- thrown exceptions count as failures for unexpected technical failures

Recommended metric scope:

- global job scheduling families
- job-specific families based on the job type or configured job name

### 7.8 Orchestration activity behavior pipeline

`Application.Orchestrations` now provides a global activity behavior pipeline similar in intent to the behavior pipelines used in other features.

The implemented orchestration behavior surface includes:

- `OrchestrationDelegate`
- `IOrchestrationBehavior`
- `OrchestrationBehaviorBase`
- `OrchestrationActivityExecutionContext`

And builder registration on `OrchestrationBuilderContext`:

- `.WithBehavior<TBehavior>()`
- `.WithBehavior(Func<IServiceProvider, IOrchestrationBehavior>)`
- `.WithBehavior(IOrchestrationBehavior)`

The orchestration runtime resolves and applies registered behaviors around:

- normal state activities
- signal-triggered activities
- compensation activities

The orchestration behavior pipeline does not wrap:

- query endpoints
- administration endpoints
- persisted metrics queries
- definition diagram generation

Behavior registration is global for the orchestration runtime in the current application. Behaviors are not registered per orchestration type.

The current implementation also includes built-in example behaviors:

- `DummyOrchestrationBehavior`
- `ChaosExceptionOrchestrationBehavior`

`ChaosExceptionOrchestrationBehavior` remains globally registered like any other orchestration behavior, but it only injects failures for orchestration types that explicitly implement `IChaosExceptionOrchestration`.

The orchestration test harness also supports `.WithBehavior(...)`, so orchestration metrics behavior can be unit tested through the same registration model used at runtime.

#### 7.8.1 Orchestration execution context

`OrchestrationActivityExecutionContext` provides the information needed for metrics and future cross-cutting behaviors, including:

- orchestration instance id
- orchestration name
- current state name
- current activity name
- activity kind
  - normal
  - signal
  - compensation
- current attempt number
- correlation id if available
- runtime `IServiceProvider`
- access to the typed orchestration context through a non-generic object reference

#### 7.8.2 Orchestration metrics behavior

Add on top of the existing orchestration behavior pipeline:

- `MetricsOrchestrationBehavior`

This behavior emits:

- activity execution attempt counters
- activity execution failure counters
- activity duration histograms

Retries count as separate execution attempts.

Compensations count as their own executions.

To keep cardinality manageable, activity metrics use:

- orchestration name
- configured activity name

They shall not include state name in the metric family.

### 7.9 Metric catalog

#### 7.9.1 Existing metrics retained

| Feature | Metric family |
| --- | --- |
| Messaging publish | `messaging_publish*` |
| Messaging handle | `messaging_handle*` |
| Domain events | `domainevents_create*` |

#### 7.9.2 New counters

#### Requester

- `requester_send`
- `requester_send_{request}`
- `requester_send_failure`
- `requester_send_{request}_failure`
- `requester_handle`
- `requester_handle_{request}`
- `requester_handle_failure`
- `requester_handle_{request}_failure`

#### Notifier

- `notifier_publish`
- `notifier_publish_{notification}`
- `notifier_publish_failure`
- `notifier_publish_{notification}_failure`
- `notifier_handle`
- `notifier_handle_{notification}`
- `notifier_handle_failure`
- `notifier_handle_{notification}_failure`

#### Queueing

- `queueing_enqueue`
- `queueing_enqueue_{message}`
- `queueing_enqueue_failure`
- `queueing_enqueue_{message}_failure`
- `queueing_handle`
- `queueing_handle_{message}`
- `queueing_handle_failure`
- `queueing_handle_{message}_failure`

#### Job scheduling

- `jobscheduling_execute`
- `jobscheduling_execute_{job}`
- `jobscheduling_execute_failure`
- `jobscheduling_execute_{job}_failure`

#### Orchestrations

- `orchestrations_activity_execute`
- `orchestrations_activity_execute_{orchestration}_{activity}`
- `orchestrations_activity_execute_failure`
- `orchestrations_activity_execute_{orchestration}_{activity}_failure`

#### 7.9.3 New duration metrics

The following histogram families shall be added with unit `ms`.

#### Requester

- `requester_send_duration`
- `requester_send_{request}_duration`
- `requester_handle_duration`
- `requester_handle_{request}_duration`

#### Notifier

- `notifier_publish_duration`
- `notifier_publish_{notification}_duration`
- `notifier_handle_duration`
- `notifier_handle_{notification}_duration`

#### Queueing

- `queueing_enqueue_duration`
- `queueing_enqueue_{message}_duration`
- `queueing_handle_duration`
- `queueing_handle_{message}_duration`

#### Job scheduling

- `jobscheduling_execute_duration`
- `jobscheduling_execute_{job}_duration`

#### Orchestrations

- `orchestrations_activity_execute_duration`
- `orchestrations_activity_execute_{orchestration}_{activity}_duration`

#### 7.9.4 Metric semantics by feature

#### Requester

- `send` counts one dispatch attempt per `IRequester.SendAsync(...)` call
- `handle` counts one actual request handler execution
- `failure` counts failed `Result` values and thrown exceptions

#### Notifier

- `publish` counts one notification dispatch attempt per `INotifier.PublishAsync(...)` call
- `handle` counts one handler execution per notification handler
- `publish_failure` counts a failed overall publish result or thrown exception
- `handle_failure` counts a failed handler result or thrown exception

#### Queueing

- `enqueue` counts one enqueue attempt per broker enqueue operation
- `handle` counts one queue handler execution attempt
- `failure` is exception-based because queue handlers do not return `Result`

#### Job scheduling

- `execute` counts one actual job execution attempt through the behavior pipeline
- `failure` counts failed `Result` values and thrown exceptions where the job surface provides them
- retries count as separate execution attempts when retry behavior re-invokes the job

#### Orchestrations

- `execute` counts one activity attempt each time the runtime invokes an activity
- `failure` counts activity attempts that end in runtime failure handling
- retries and compensations are separate attempts

### 7.10 Registration model

#### 7.10.1 Requester and notifier

Use the existing shared requester/notifier behavior registration style.

Examples:

```csharp
services.AddRequester()
    .WithBehavior(typeof(MetricsRequestBehavior<,>));

services.AddNotifier()
    .WithBehavior(typeof(MetricsNotificationBehavior<,>))
    .WithBehavior(typeof(MetricsNotificationHandlerBehavior<,>));
```

#### 7.10.2 Queueing

Use the existing queueing behavior registration style.

Example:

```csharp
services.AddQueueing()
    .WithBehavior<MetricsQueueEnqueuerBehavior>()
    .WithBehavior<MetricsQueueHandlerBehavior>();
```

#### 7.10.3 Job scheduling

Use the existing job scheduling behavior registration style.

Example:

```csharp
services.AddJobScheduling()
    .WithBehavior<MetricsJobSchedulingBehavior>();
```

#### 7.10.4 Orchestrations

Use the implemented orchestration behavior registration directly on the orchestration builder.

Example:

```csharp
services.AddOrchestrations()
    .WithBehavior<MetricsOrchestrationBehavior>();
```

This registration applies to all orchestrations in the application.

### 7.11 Documentation impact
This specification should update:

- `docs/features-requester-notifier.md`
- `docs/features-queueing.md`
- `docs/features-jobscheduling.md`
- `docs/features-orchestrations.md`

The docs must show:

- how to enable the metrics behaviors
- which metric families are emitted
- what counts as a failure
- how orchestration activity metrics differ from persisted orchestration metrics

### 7.12 Acceptance criteria

1. When requester metrics behavior is registered and a request succeeds, then `requester_send*` and `requester_handle*` counters increase.
2. When requester handling returns a failed `Result`, then the corresponding `requester_*_failure` counters increase.
3. When notifier metrics behaviors are registered and a notification is published to multiple handlers, then `notifier_publish*` increases once and `notifier_handle*` increases once per handler execution.
4. When a notifier handler returns a failed `Result`, then `notifier_handle_failure*` increases.
5. When queueing metrics behaviors are registered and a message is enqueued and processed successfully, then enqueue and handle counters increase.
6. When a queue handler throws, then `queueing_handle_failure*` increases.
7. When job scheduling metrics behavior is registered and a job executes, then `jobscheduling_execute*` counters increase and duration histograms record execution time.
8. When a job returns a failed `Result` or throws, then `jobscheduling_execute_failure*` increases.
9. When orchestration metrics behavior is registered, then normal, signal, and compensation activities emit execution metrics.
10. When an orchestration activity retries, then each retry attempt increases the execution counter.
11. When duration metrics are enabled by the registered behaviors, then the corresponding histograms record values in milliseconds.
12. Existing `messages_*`, `messaging_*`, and `domainevents_*` metrics remain unchanged.

---

## 8. Metrics API

The metrics feature adds an application-internal metrics API for dashboards.

This API is intentionally:

- read-only
- polling-friendly
- process-local
- curated rather than raw

The metrics API is implemented inside:

- `src/Presentation.Web/Metrics`

No separate `Presentation.Web.Metrics` project or package is introduced.

### 8.1 Endpoint families

The metrics API uses one route group:

- `/api/_system/metrics`

With tag:

- `_System.Metrics`

The metrics API exposes separate endpoint families for:

- devkit business/runtime metrics
- general .NET runtime metrics
- general ASP.NET Core metrics

### 8.2 Endpoint surface

#### 8.2.1 Discovery endpoint

- `GET /api/_system/metrics`

Returns links or route values for:

- `bdk`
- `dotnet`
- `aspnet`
- `overview`

#### 8.2.2 Devkit metrics endpoint

- `GET /api/_system/metrics/bdk`

Returns live cumulative snapshots of devkit-emitted metrics from the `bdk` meter.

#### 8.2.3 .NET runtime metrics endpoint

- `GET /api/_system/metrics/dotnet`

Returns curated process and CLR runtime metrics.

#### 8.2.4 ASP.NET Core metrics endpoint

- `GET /api/_system/metrics/aspnet`

Returns curated HTTP/server metrics.

#### 8.2.5 Dashboard overview endpoint

- `GET /api/_system/metrics/overview`

Returns a composed home-dashboard snapshot that combines selected values from:

- `bdk`
- `dotnet`
- `aspnet`

The overview endpoint is a convenience read model. It does not replace the separate source endpoints.

### 8.3 Polling contract

The metrics API is optimized for charts that poll at intervals.

Each snapshot response shall include:

- `capturedAtUtc`
- `processStartedAtUtc`
- `uptimeSeconds`

Dashboard behavior is expected to be:

1. call an endpoint at a fixed interval
2. append the returned snapshot as a chart point
3. derive deltas or rates from successive cumulative totals where needed

If `processStartedAtUtc` changes, or if a cumulative total decreases unexpectedly, the client should treat that as a new series origin caused by restart or reset.

No server-side history, rolling buckets, or retained chart samples are required.

### 8.4 `bdk` metrics snapshot service

The implementation shall introduce:

- `IBdkMetricsSnapshotService`
- `BdkMetricsSnapshotService`

This service listens to the `"bdk"` meter and tracks current cumulative values in-process.

The endpoint groups emitted metrics by feature prefix:

- `messages_` and `messaging_` -> `messaging`
- `domainevents_` -> `domain`
- `requester_` -> `requester`
- `notifier_` -> `notifier`
- `queueing_` -> `queueing`
- `jobscheduling_` -> `jobscheduling`
- `orchestrations_` -> `orchestrations`

#### 8.4.1 `bdk` response shape

The `bdk` snapshot should expose:

- snapshot metadata
- feature groups
- raw cumulative values
- selected computed summaries

Recommended computed summaries:

- total successes by feature
- total failures by feature
- top failing series
- busiest series
- latency highlights based on duration histograms

### 8.5 `.NET` runtime metrics snapshot service

The implementation shall introduce:

- `IDotNetMetricsSnapshotService`
- `DotNetMetricsSnapshotService`

This service exposes curated process and CLR runtime values suitable for dashboards.

Recommended fields:

- process working set
- private bytes
- managed heap size
- GC collection counts by generation
- thread pool worker thread count
- pending work item count where available
- CPU usage percentage derived from sampled process time deltas
- allocation rate where available

The service may combine:

- built-in runtime meters
- direct runtime APIs

when that produces a clearer and more stable dashboard contract.

### 8.6 `ASP.NET Core` metrics snapshot service

The implementation shall introduce:

- `IAspNetMetricsSnapshotService`
- `AspNetMetricsSnapshotService`

This service exposes curated HTTP/server telemetry sourced from built-in ASP.NET Core and Kestrel instrumentation.

Recommended fields:

- total request count
- failed request count where derivable
- active requests where available
- request duration aggregates
- current connection count where available

The endpoint must remain curated. If a metric is not consistently available in the supported runtime setup, it should be omitted rather than synthesized unreliably.

### 8.7 Overview endpoint contract

The overview endpoint exists to support a single dashboard landing page.

It should combine:

- key devkit throughput totals
- failure hot spots
- slowest current latency families
- basic runtime health
- basic HTTP activity

Recommended overview sections:

- summary cards
- top failures
- top throughput
- latency cards
- runtime health
- HTTP health

### 8.8 Endpoint implementation inside `Presentation.Web`

The implementation shall add a `Metrics/` folder in `src/Presentation.Web` containing:

- `MetricsEndpoints`
- `MetricsEndpointsOptions`
- `MetricsEndpointsOptionsBuilder`
- `ServiceCollectionExtensions`
- response models
- snapshot services

Registration should follow the same conventions as existing web endpoint features.

Recommended registration style:

```csharp
services.AddMetricsEndpoints(options => options
    .GroupPath("/api/_system/metrics")
    .GroupTag("_System.Metrics"));
```

### 8.9 Security and exposure

The metrics endpoints are read-only, but they still expose operational data and should support the same authorization conventions as other system endpoints.

The options model should allow:

- enabling or disabling the endpoint group
- changing the route group path
- requiring authorization

The default exposure should be conservative and match existing system endpoint expectations.

### 8.10 Acceptance criteria

1. When metrics endpoints are registered, then `/api/_system/metrics` returns discovery links for `bdk`, `dotnet`, `aspnet`, and `overview`.
2. When `GET /api/_system/metrics/bdk` is called, then it returns current cumulative devkit metrics grouped by feature.
3. When `GET /api/_system/metrics/dotnet` is called, then it returns curated runtime/process values without exposing raw instrument internals.
4. When `GET /api/_system/metrics/aspnet` is called after HTTP traffic occurs, then request-oriented counters increase accordingly.
5. When `GET /api/_system/metrics/overview` is called, then it returns a composed dashboard snapshot based on the three underlying metric families.
6. When a dashboard polls these endpoints over time, then it can build historical charts by appending successive snapshots without relying on server-side retained history.
7. When the process restarts, then the response metadata allows clients to detect a new series origin.

---

## 9. DoFiesta operations page

The metrics feature includes a real application-facing operations experience that consumes the metrics endpoints.

The initial target is the DoFiesta example application. Its purpose is twofold:

- provide a useful live operations dashboard for the sample app
- serve as the canonical reference implementation for how an application should consume the metrics API

### 9.1 Scope

The DoFiesta operations page lives in the application UI layer and does not introduce new metric sources. It consumes:

- `/api/_system/metrics/overview`
- `/api/_system/metrics/bdk`
- `/api/_system/metrics/dotnet`
- `/api/_system/metrics/aspnet`

It is responsible for:

- choosing polling intervals
- storing sampled snapshots in browser memory
- appending new points to charts
- detecting process restarts
- presenting cards, charts, and rankings in an operator-friendly layout

It is not responsible for:

- emitting new server-side metrics
- storing dashboard history on the server
- introducing a second metrics API

### 9.2 UX goals

The DoFiesta operations page should make the metrics immediately understandable to developers and operators.

It should support:

- fast visual identification of failures
- quick reading of current throughput
- trend visibility over the current browser session
- a clear distinction between business/runtime metrics and platform health

### 9.3 Page structure

The page should be structured into clear sections.

Recommended sections:

- overview cards
- devkit activity
- latency charts
- runtime health
- HTTP/server health
- top failures
- top throughput

### 9.4 Overview cards

The top section should present high-signal summary cards, for example:

- total messaging activity
- total requester activity
- total notifier activity
- total queueing activity
- total job scheduling activity
- total orchestration activity
- total failures
- current requests served
- process uptime

These cards should be sourced primarily from the `overview` endpoint.

### 9.5 Charts and polling behavior

Charts are built by polling and appending snapshots in the browser.

The page should:

- poll the API at a fixed interval
- append each successful response as a new chart point
- retain only a bounded in-memory window on the client
- reset the current chart series when `processStartedAtUtc` changes

Recommended initial behavior:

- default polling interval in the low-seconds range
- client-side rolling window sized for an operations session rather than long-term retention

Exact values can remain configurable at implementation time.

### 9.6 Derived client-side metrics

The page may derive additional chart series from cumulative totals, including:

- per-interval increments
- failure rate over the current sample window
- requests per interval
- moving-average latency lines

These values must be derived client-side from polled snapshots and must not require new endpoints.

### 9.7 Restart and gap handling

The page must handle:

- application restarts
- missed polling intervals
- temporarily unavailable metrics endpoints

Expected behavior:

- restart detection starts a new series
- transient polling failures do not wipe the whole dashboard
- the UI shows stale or delayed data state clearly when polling is interrupted

### 9.8 Separation of concerns

The DoFiesta operations page should keep the metric families visually separated:

- devkit business/runtime metrics from `/bdk`
- process/runtime health from `/dotnet`
- HTTP/server health from `/aspnet`

The page may unify them in the overall layout, but it should not blur their meaning.

### 9.9 Acceptance criteria

1. When the DoFiesta operations page loads, then it retrieves the overview snapshot and renders summary cards.
2. When polling continues successfully, then charts append new points without requiring a full page reload.
3. When `processStartedAtUtc` changes, then the UI starts a new chart series instead of continuing the old one.
4. When the metrics API is briefly unavailable, then the page keeps existing chart history and shows that live updates are temporarily stale.
5. When business/runtime failures increase, then the operations page surfaces them prominently in failure-focused cards or charts.
6. When runtime or HTTP pressure increases, then the platform sections reflect that change independently from devkit feature throughput.

---

## 10. Testing strategy

### 10.1 Metrics creation tests

Add unit and integration tests that verify:

- requester send, handle, failure, and duration metrics
- notifier publish, handler, failure, and duration metrics across sequential and concurrent execution modes
- queueing enqueue, handle, failure, and duration metrics
- job scheduling execute, failure, retry, and duration metrics
- orchestration behavior registration and activity metric emission for normal, signal, retry, and compensation flows
- backward compatibility of existing messaging and domain event metrics

Recommended metric verification mechanism:

- `MeterListener`

### 10.2 Metrics API tests

Add endpoint and snapshot service tests that verify:

- metric listener subscription and aggregation
- snapshot metadata population
- feature grouping of `bdk` metrics
- endpoint registration in `Presentation.Web`
- authorization behavior
- response shape stability
- `.NET` and `ASP.NET Core` snapshots changing in response to real workload

### 10.3 DoFiesta operations page tests

Add UI and integration tests that verify:

- the DoFiesta operations page loads and requests the metrics endpoints
- polling appends new points over time
- restart metadata resets chart series
- temporary endpoint failures show a degraded live-state indication without breaking the page
- overview cards, feature charts, and platform charts render from realistic seeded snapshots

---

## 11. Documentation and examples

The feature should be documented as one complete capability with three connected areas: metric creation, metrics API exposure, and DoFiesta dashboard consumption.

### 11.1 Metrics creation docs

Update feature docs to explain:

- which metrics are emitted
- how to register the optional behaviors
- what counts as a failure
- what durations represent

### 11.2 Metrics API docs

Document:

- route structure
- endpoint purposes
- polling contract
- restart semantics
- separation between `bdk`, `.NET`, and `ASP.NET` metrics

### 11.3 DoFiesta operations page docs and example application

Document the DoFiesta operations page as:

- the reference consumer of the metrics API
- an example of client-side polling and chart assembly
- a practical guide for applications that want to build their own operations dashboards

The DoFiesta implementation should be showcased in docs and screenshots once available.

### 11.4 Example applications

At least one example application should demonstrate:

- registration of the new metrics behaviors
- OpenTelemetry metric export
- metrics endpoint registration in `Presentation.Web`
- dashboard consumption in DoFiesta

---

## 12. Out of scope for this specification revision

The following items are intentionally left out of the initial design:

- server-side metric retention for charts
- arbitrary query language over metrics
- tag-based filtering and grouping
- cluster aggregation
- alert rule management
- metric reset endpoints
- Prometheus-specific endpoint design
- correlation joins between live runtime metrics and persisted business query data

---

## 13. Final decision summary

This specification establishes:

- behavior-driven metric creation for messaging, requester, notifier, queueing, job scheduling, and orchestrations
- read-only metrics endpoints inside `Presentation.Web`
- a DoFiesta operations page as the reference dashboard consumer
- no new metrics web package
- separate endpoints for `bdk`, `.NET`, and `ASP.NET Core`
- client-side history through polling and append
- explicit `requester_handle_*` support
- additive duration metrics to make dashboards materially more useful

The result is a single coherent metrics feature that is useful through standard .NET observability tooling, dashboard-ready through a focused `Presentation.Web` read API, and demonstrably consumable through a real DoFiesta operations experience.
