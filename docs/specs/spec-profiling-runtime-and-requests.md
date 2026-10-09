---
created: 2026-10-06
status: Ready for implementation
---

# Runtime and Operation Profiling Specification

Profiling is one devkit feature with two capabilities: Runtime Profiling and Operation Profiling. Runtime Profiling records process measurements during bounded sampling sessions. Operation Profiling records how a particular execution spends its time in named segments. Both use shared process identity, recording conventions, storage configuration, and dashboard infrastructure.

An operation can be an HTTP request, a Blazor Server interaction, an application service invocation, a job execution, a pipeline run, or a bounded orchestration execution. Request Profiling is the HTTP adapter over Operation Profiling. Controllers, services, and feature behaviors use the same operation and segment abstractions. Pipeline steps and orchestration actions contribute segments to that operation.

Operation segment data is aggregated by key path. The goal is timings and measurements for named work, including repeated calls in loops, rather than a dependency trace of every invocation. The system retains the operation record and bounded segment summaries. It does not persist an operation's sequence of segment invocations.

This document specifies the final system, including its public contracts, observable behavior, and acceptance criteria. The APIs shown are specified contracts, not a claim that they are already implemented. The feature is new and unused. Its final names apply consistently across code, configuration, tests, examples, and documentation without compatibility aliases. This file is the canonical behavior contract for the implementation plan.

## 1. Scope and terminology

### 1.1 Required capabilities

| ID | Requirement |
| --- | --- |
| REQ-001 | Provide one Profiling feature with Runtime and Operations dashboard views. Provide Requests as an HTTP-filtered view over Operations. |
| REQ-002 | Start an operation independently of HTTP, a DI request scope, or an active runtime sampling session. Support zero or more segments per operation. |
| REQ-003 | Compose explicit runtime, operation, and request capabilities through `AddProfiling(...).WithRuntimeProfiling(...).WithOperationProfiling(...).WithRequestProfiling(...)`. Enabled Request Profiling requires enabled Operation Profiling. |
| REQ-004 | Default Request Profiling to capturing every request that passes the blacklist. Allow a pluggable request-sampling strategy to select fewer requests without endpoint opt-in. |
| REQ-005 | Give each operation a unique ID, stable grouping key, kind, outcome, duration, and developer-supplied dimensions. Identify segment summaries by their full stable key path within the operation. |
| REQ-006 | Measure nested, repeated, and parallel segment invocations through shared helpers and behaviors. Aggregate counts, durations, outcomes, and measurements without persisting an invocation trace. |
| REQ-007 | Capture HTTP method, route, status, observed request duration, and body-size information at the operation level through HTTP metadata. Return its operation ID in `X-Request-Profiling-Id`. |
| REQ-008 | Abstract storage through one DI-selected provider contract. Supply in-memory and Entity Framework implementations and a registration extension for additional providers. Store completed operations through bounded buffering and periodic background batches. |
| REQ-009 | Provide Slow, Recent, and By count dashboard modes, configurable result limits, grouping by keys and dimensions, and selectable auto refresh. |
| REQ-010 | Show a percentage segment summary bar for each operation occurrence, with elapsed-time tooltips and an honest representation of overlapping segments. |
| REQ-011 | Relate operations to runtime evidence by process identity and overlapping UTC time intervals. Show runtime overlays in operation details and allow navigation in both directions. |
| REQ-012 | Support filtering, exact lookup, segment analysis, distributions, and comparison between two filter selections. Show sample counts and recording quality. |
| REQ-013 | Use UTC for stored datetimes, API values, datetime dimensions, and all profiling views. Public datetimes use ISO 8601 with a `Z` suffix. |
| REQ-014 | Keep measurements generic so file storage, SQL, caches, services, jobs, and calculations can participate without dependency-specific core models. |
| REQ-015 | Expose capture loss, truncation, retention coverage, and persistence freshness so users can judge the available evidence. |
| REQ-016 | Default an HTTP operation's key to its incoming path, with an optional prefix removed. Match blacklist patterns against the original incoming path before starting a profiling scope. |
| REQ-017 | Emit minimal structured operation and segment start/stop events at Verbose level, respecting the host's logging filters and profiling suppression. |
| REQ-018 | Provide all-request, probability, and rate-limited request sampling through fluent setup. Decide once at request entry and preserve the decision throughout its execution. |
| REQ-019 | Supply profiling behaviors for jobs, pipelines, and orchestrations. A pipeline run contains step segments; a bounded orchestration execution contains action segments. Both join an existing operation when one is active. |
| REQ-020 | Make profiling dependencies optional in participating feature behaviors and adapters. When `AddProfiling` is omitted, they still resolve and execute normally without registering profiling services or recording data. |
| REQ-021 | Require every storage provider to clear all stored profiling history or history within a UTC time range. Support Runtime, Operations, or both datasets, remove owned data consistently, and prevent delayed writes from restoring cleared history. |
| REQ-022 | Record the executing node on every operation using a shared cached process identity. Preserve Runtime's Broadcast control while allowing Operations to run without Broadcast. |
| REQ-023 | Group operation keys and segment key paths case-insensitively with identical comparison behavior in every provider. |
| REQ-024 | Bound the recording lifetime of abandoned or long-running operations. Expiring capture releases diagnostic state without canceling business work or claiming that execution completed. |

Application performance takes priority over diagnostic retention. Recording is best effort under overload. The system does not promise zero overhead or lossless persistence. Application execution never waits for queue capacity or diagnostic database writes.

The feature stores metadata, outcomes, and measurements. It does not store HTTP bodies, application results, SQL text, parameter values, or arbitrary object graphs. Runtime CPU, memory, allocation, and GC measurements remain process observations. Operation Profiling does not introduce a second runtime sampler or claim to attribute process totals to individual operations.

OpenTelemetry, Aspire, Azure Monitor, AI evaluation, automatic concurrency tuning, and required stress-test entities are outside scope. CSV and new operation exports are deferred. Existing runtime archive, JSON, and Perfetto capabilities remain available.

### 1.2 Vocabulary

| Term | Meaning |
| --- | --- |
| Profiling | The overarching feature, configuration, provider selection, and dashboard integration. |
| Runtime session | A bounded collection of runtime snapshots, markers, and segments from participating nodes. |
| Runtime snapshot | Runtime measurements recorded for one node at a particular moment or over a stated sample interval. |
| Operation profile | One recorded execution with its metadata, duration, outcome, and zero or more segments. |
| Operation scope | The live recording context for one operation execution. Its lifetime follows that execution. |
| Request Profiling | The configurable HTTP adapter that creates an operation for each eligible request and supplies HTTP metadata. |
| Segment | One timed invocation of named work, possibly inside another segment. The same timing concept applies to Runtime and Operation Profiling. |
| Segment key path | The sequence of keys from the outer segment to the current segment, such as `Load / Read`. It identifies a class of nested work. |
| Segment summary | Aggregated invocation counts, durations, outcomes, and measurements for one segment key path within an operation. |
| Marker | An instantaneous annotation. A marker has no inferred duration. |
| Node | One application process lifetime. A restart receives a new identity, even on the same machine. |
| Key | An identifier for grouping comparable work, such as `/calculations`, `Recalculate`, or `Load`. HTTP defaults to the path; application overrides can supply a stable logical key. |
| Dimension | A bounded typed value used for filtering or grouping. |
| Measurement | A named numeric value with an explicit unit. An absent measurement is different from zero. |
| Request sampling | Selection of eligible HTTP requests for operation capture, before their work starts. It is separate from the runtime snapshot sampling interval. |

An operation ID identifies one occurrence. A correlation ID can relate several operations. A key identifies a class of work. These values are separate. Operation IDs are opaque GUIDs. Active segment handles distinguish simultaneous invocations internally, but operation storage exposes summaries rather than a GUID for every invocation. The existing readable runtime `SessionKey`, `NodeKey`, and `SnapshotKey` conventions remain lookup identities, not grouping dimensions.

A segment belongs to exactly one runtime session or one operation, and to one node. A parent segment has the same owner and node. An operation's summaries stay owned by that operation when runtime sampling is active. Temporal correlation never transfers ownership. Runtime interval records needed by its existing exports remain a runtime concern; operation summaries do not imply reconstructable trace events.

## 2. Architecture and public contracts

### 2.1 Shared core and adapters

The core recording API has no dependency on `HttpContext`, controllers, Blazor circuits, EF Core, or a particular storage service. Adapters translate their execution context into the generic operation model.

```text
HTTP middleware     Blazor action / service     Feature behaviors
       \                     |                          /
                    IOperationProfiler
                            |
                 Operation scope + segments
                            |
                 Immutable completed record
                            |
                 Bounded nonblocking queue
                            |
                   Periodic batch writer
                            |
                IProfilingStorageProvider
                In memory / EF / custom
                            |
               Operations query and dashboard

Runtime sampling -> Runtime records in the same selected provider
                    Related by node and overlapping time intervals
```

Shared infrastructure covers cached node identity, application version, clocks, segment recording, metadata validation, provider selection, retention utilities, and dashboard integration. Runtime session control and probes remain specific to Runtime Profiling. HTTP lifecycle and body observation remain specific to Request Profiling.

| Final contract | Responsibility |
| --- | --- |
| `ProfilingOptions`, `ProfilingBuilderContext` | Feature enablement and composition, with separate `Runtime`, `Operations`, and `Requests` options. |
| `ProfilingNode` | Shared process identity and application metadata. |
| `IProfilingNodeIdentityProvider` | Cached local process identity, independent of Runtime and Broadcast; Runtime attaches its Broadcast correlation to that same identity. |
| `RuntimeProfilingSession`, `RuntimeProfilingSnapshot` | Runtime sampling records. |
| `IRuntimeProfilingControlService`, `IRuntimeProfilingQueryService` | Runtime lifecycle and queries. |
| `IRuntimeProfilingMeasurementService`, `IRuntimeProfilingEvaluationService` | Runtime-owned measurements and evaluation. Runtime export services also use the `RuntimeProfiling` prefix. |
| `OperationProfilingRecord` | HTTP-independent completed execution record. |
| `IOperationProfiler` | Start an operation, access the current operation, enrich it, and record segments. |
| `IProfilingOperationScope` | Execution-owned identity, enrichment, segment creation, outcome, and finalization. |
| `IProfilingSegmentScope` | Lightweight operation segment scope using the shared timing/outcome engine, without Runtime control or persistence dependencies. |
| `IRuntimeProfilingMeasurementScope` | Async Runtime measurement scope that retains Runtime session ownership and durable interval semantics. |
| `ProfilingSegmentSummary` | Operation-owned aggregate for a segment key path. |
| `ProfilingSegment` | Runtime-owned segment interval where required by runtime measurement and export contracts. |
| `ProfilingMarker` | Instantaneous runtime annotation, exposed through `AddMarkerAsync`. |
| `IOperationProfilingQueryService` | Operation lists, grouped summaries, analysis, and runtime correlation. |
| `HttpRequestProfilingMetadata` | Typed HTTP adapter projection, including method, route, status, and size observations. |
| `IRequestProfilingFeature` | Request-local capture identity and safe exception/route reporting used by the outer middleware and exception observer. |
| `IRequestProfilingSamplingStrategy` | Synchronous request-entry decision to capture or skip an eligible HTTP execution. |
| `RequestProfilingSamplingContext`, `RequestProfilingSamplingDecision` | Immutable decision input and bounded decision result, independent of storage. |
| `IProfilingStorageProvider` | Provider contract composed from `IRuntimeProfilingStore` and `IOperationProfilingStore`, including provider capabilities and shared history clearing. |
| `ProfilingClearRequest`, `ProfilingClearResult`, `ProfilingDataSet` | Backend-independent clearing selection, execution result, and dataset selector (`Runtime`, `Operations`, or `All`). |
| `OperationProfilingWriteEnvelope`, `ProfilingWriterLease` | Internal persistence protocol values carrying the immutable record, writer identity, sequence, and provider-issued write permission. They are not dashboard DTOs or distributed tracing identifiers. |
| `InMemoryProfilingStorageProvider` | Bounded process-local provider for both profiling datasets. |
| `EntityFrameworkProfilingStorageProvider<TContext>` | Durable provider using an application-owned EF context through independent scopes. |
| `IProfilingDbContext` | Application-owned EF context contract for the selected profiling provider. |

Public recording contracts, storage-provider contracts, and their shared records and query DTOs belong in `src/Common.Abstractions/Profiling/`. Recording, operation orchestration, buffering, query implementations, and the in-memory provider belong under `src/Common.Utilities/Profiling/`. HTTP adapters, request-sampling contracts, and built-in request strategies belong under `src/Presentation.Web/Profiling/Requests/`. The core operation recorder does not depend on HTTP sampling types. The EF provider remains under `src/Infrastructure.EntityFramework/Profiling/`. Another provider can live in a separate package without a dependency from the core to that package.

Public control, query, and storage contracts in `Common.Abstractions` return its existing `IResult` or `IResult<T>` interfaces, including asynchronous wrappers. Implementations construct the existing `Result` or `Result<T>` values from `Common.Results`. Do not add a reference from `Common.Abstractions` to `Common.Results` or `Common.Utilities`; either would reverse the dependency direction. Concrete error factories, runtime Broadcast adapters, and behavior that depends on those implementations remain outside the abstractions assembly.

Feature behaviors consume the common recording contracts. They do not depend on web middleware or EF. The shared segment engine handles timing and bounded state. The operation recorder folds completed invocations into summaries. Runtime adapters retain explicit session control and the interval information required by their existing exports. Starting an operation or a segment does not create a runtime session or synchronously access runtime storage.

### 2.2 Registration

The specified registration shape is:

```csharp
builder.Services
	.AddProfiling(options => options
		.Enabled(builder.Environment.IsDevelopment()))
	.WithRuntimeProfiling(options => options
		.Enabled(true)
		.SamplingInterval(TimeSpan.FromSeconds(1))
		.Duration(TimeSpan.FromSeconds(30)))
	.WithOperationProfiling(options => options
		.Enabled(true)
		.FlushInterval(TimeSpan.FromSeconds(1))
		.BatchSize(512)
		.MaxBatchesPerFlush(8)
		.FlushTimeBudget(TimeSpan.FromMilliseconds(250)))
	.WithRequestProfiling(options => options
		.Enabled(builder.Configuration.GetValue("Profiling:Requests:Enabled", true))
		.StripPathPrefix("/api")
		.WithSampling(sampling => sampling.AllRequests()))
	.WithEntityFrameworkProvider<AppDbContext>();

builder.Services.AddDashboard(options => options
	.Enabled(builder.Environment.IsDevelopment()));

var app = builder.Build();

app.UseRequestProfiling();
app.UseExceptionHandler(); // Use the host's configured error handler.
app.UseRequestProfilingExceptionObserver();
app.UseRouting();
// Other middleware follows the supported order in section 4.9.
app.MapEndpoints();
```

`AddProfiling` registers shared infrastructure and returns `ProfilingBuilderContext`. It does not implicitly enable a subfeature. Each `With...Profiling` method returns that same builder and configures one capability. Its callback is optional, and calling the method enables that capability by default. `.Enabled(false)` retains explicit configuration while disabling it. An omitted subfeature is disabled.

| Fluent method | Responsibility |
| --- | --- |
| `AddProfiling(options => options.Enabled(...))` | Shared infrastructure, master enablement, and injectable recording contracts. The master flag defaults to false. |
| `WithRuntimeProfiling(...)` | Runtime sessions, probes, snapshot intervals, control, and existing runtime exports. Registration alone does not start a sampling session. |
| `WithOperationProfiling(...)` | Injectable operation/segment recording for any execution, plus bounded buffering and the periodic writer. |
| `WithRequestProfiling(...)` | HTTP adapter, path keys, blacklist, and request sampling. It requires Operation Profiling. |
| `WithInMemoryProvider()`, `WithEntityFrameworkProvider<TContext>()`, `WithProvider<TProvider>()` | One provider selection shared by the enabled capabilities. In-memory is the default when omitted. |

The following is sufficient for services, Blazor actions, jobs, or other non-HTTP work:

```csharp
builder.Services
	.AddProfiling(options => options.Enabled(true))
	.WithOperationProfiling();
```

Those callers inject `IOperationProfiler` through DI and use the same `BeginOperation`, `RunOperation`, and segment helpers described below. They need no HTTP middleware, runtime session, or web DI scope. When `AddProfiling` is present but Operation Profiling is disabled, the registered façade performs no recording so consumers can retain the same injection contract.

Only enabled Runtime Profiling installs the current Broadcast handlers, runtime collectors, and runtime lifecycle workers. Its start/stop/snapshot/GC commands, participant discovery, delivery, and finalization semantics remain unchanged. Operation-only setup does not install or require Broadcast. Both capabilities resolve the same local node identity service described in section 7.1.

Omitting `AddProfiling` is a separate supported configuration: no profiling services are registered, including a no-op façade, provider, or background writer. Participating feature behaviors and companion adapters accept an absent `IOperationProfiler` and execute normally, as specified in section 6.7. Registering those integrations never implicitly calls `AddProfiling`. Application services that must work in both configurations use the same optional injection pattern; required profiler injection is appropriate only when the host guarantees registration.

To add automatic request capture with all default settings, the minimal chain is:

```csharp
builder.Services
	.AddProfiling(options => options.Enabled(true))
	.WithOperationProfiling()
	.WithRequestProfiling(); // AllRequests is the default sampling strategy.

// After building the host:
app.UseRequestProfiling();
// If the host uses an exception handler, place the observer immediately after it.
app.UseExceptionHandler();
app.UseRequestProfilingExceptionObserver();
```

Enabled Request Profiling requires an explicitly enabled `WithOperationProfiling` registration. It never silently activates a capability that the host omitted or disabled. Validate this dependency after the complete fluent configuration is built, so chaining order does not affect the result. If the master is enabled and Requests is enabled without Operations, startup validation reports the missing dependency. With the master disabled, no subfeature starts capture workers.

The request flag controls automatic HTTP capture only. Setting it to false does not disable enabled Operation Profiling, Runtime Profiling, or queries of retained request records. `UseRequestProfiling` safely passes through when the HTTP adapter is disabled. It adds no HTTP operation, profiling ID header, body observer, or request lifecycle log. Manually started operations remain available when Operations is enabled.

Enablement and sampling selection are resolved during setup, with no required live reconfiguration. Repeated registration composes configuration without duplicate DI services, writers, or middleware records. Defaults initialize a capability once; subsequent calls change only explicitly configured values. A later explicit enablement value replaces an earlier value for the same capability; the final effective options are validated together. Provider conflicts follow the separate selection rules in section 8.2.

| Master feature | Operations | Requests | Effective operation capture |
| --- | --- | --- | --- |
| Disabled | Any | Any | No profiling capture or capture workers. |
| Enabled | Enabled | Omitted or disabled | Manual and behavior-based operations; no automatic HTTP root. |
| Enabled | Enabled | Enabled | Operations plus HTTP capture subject to blacklist, sampling, and resource limits. |
| Enabled | Omitted or disabled | Enabled | Configuration error identifying the required Operation Profiling capability. |
| Enabled | Omitted or disabled | Omitted or disabled | No operation capture; runtime capture is independently available only with enabled `WithRuntimeProfiling`. |

The master flag gates capture and profiling views. With the master enabled, retained datasets remain queryable even when their capture capability is disabled. The dashboard shows each capability's effective state separately. The feature retains the devkit's development-use deployment guidance.

### 2.3 Execution ownership and DI lifetimes

An operation scope belongs to an execution, not to a DI scope. This distinction is required for Blazor Server, where scoped services can live for an entire circuit. Two overlapping UI actions on the same circuit must have separate operation records. See [ASP.NET Core Blazor dependency injection](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/dependency-injection).

`IOperationProfiler` is a singleton façade over execution-local context. It does not store a mutable current operation in a singleton field or a circuit-scoped service. Logical execution context carries ambient ownership across `await`. Explicit scope handles support parallel branches. A mutable shared segment stack is not sufficient. The façade captures no scoped dependency; application services, controllers, endpoint parameters, and feature behaviors resolve this same contract.

`BeginOperation`, `RunOperation`, and `RunOperationAsync` always establish a new operation boundary. They restore the prior ambient operation when finished. An explicit nested operation may retain a `ParentOperationId` link, but it remains an independently persisted record. Ordinary inner service work uses segments to avoid accidental root proliferation.

`BeginSegment`, `RunSegment`, and `RunSegmentAsync` use the current operation. With no active operation they return a no-op scope or execute their delegate without recording. They do not silently create a root. Feature behaviors use the distinct join-or-start contract in section 6.

A rejected recording admission still establishes a lightweight disabled execution boundary. Inner work must not accidentally attach to an unrelated prior operation. Late writes to a closed scope are ignored and counted where useful. Finalized state cannot be reactivated through a captured execution context.

Queued, detached, or resumed work starts a fresh operation at its execution boundary. It carries copied correlation identifiers where appropriate, not a live operation scope, request context, or service provider. Workers clear inherited recording context before processing independent work. All joined segments must finish within their owning operation's lifetime.

Node identity, admission accounting, immutable options, and the writer are process-wide. EF batches and queries own independent service scopes and contexts. No queued record retains an application DI scope.

Operation and segment scopes expose `IsRecording`. An enabled, unsuppressed operation start produces an opaque ID even when admission fails; the ID never guarantees persistence. Disabled or suppressed starts return a no-op scope with `Guid.Empty` and `IsRecording = false`. Segment scopes reuse their owner's operation ID rather than allocating an ID per invocation. A closed or rejected execution boundary remains distinguishable from the absence of an operation, so join-or-start behaviors do not create replacement roots after capture ends. Explicit handles used across tasks still obey their owner's lifetime and cannot reparent an invocation.

### 2.4 Mapping existing Runtime Profiling to the final system

This mapping defines the resulting contracts, not a compatibility layer. Runtime remains a bounded snapshot/measurement feature controlled through Broadcast. Operation capture is a separate recorder that shares identity and timing primitives. Existing Runtime behavior is retained where specified below; its session-starting measurement service is not used to implement `IOperationProfiler`.

| Existing element | Final element and behavior |
| --- | --- |
| `ProfilingOptions` and `AddProfiling(...)` runtime settings | `ProfilingOptions` holds the master flag and shared node/provider settings. Sampling interval, duration, automatic stop, participation deadline, finalization grace, and runtime retention move to `RuntimeProfilingOptions` configured through `WithRuntimeProfiling(...)`. Operations/Requests have their own options. Dashboard refresh belongs to dashboard/view configuration. |
| `IProfilingControlService`, `ProfilingControlService` | `IRuntimeProfilingControlService`, `RuntimeProfilingControlService`; retain start, stop, snapshot, GC, restart, pinning/metadata, terminal-session deletion, and lifecycle validation. |
| `IProfilingBroadcastService`, its handlers and Broadcast messages | Runtime-prefixed profiling adapters/messages; continue to use the existing Broadcast registration, target selection, transport, participation, delivery, and finalization mechanisms. The general Broadcast feature is unchanged. |
| `ProfilingCollector`, snapshot probe, active-session context, reconciler, finalizer, runtime context factory | Runtime-prefixed implementations registered by enabled Runtime Profiling only. Keep bounded collection, missed/failed snapshot reporting, startup reconciliation, and terminal-session consistency. |
| `ProfilingSession`, `ProfilingSnapshot`, participation/runtime-context records | `RuntimeProfilingSession`, `RuntimeProfilingSnapshot`, and Runtime-prefixed supporting records. Existing readable session/node/snapshot keys and runtime evidence remain available. |
| `ProfilingNode`, node identity resolution tied to Broadcast | Shared `ProfilingNode` and cached `IProfilingNodeIdentityProvider`. A Runtime adapter attaches private Broadcast correlation as described in section 7.1; Operations uses the same node without Broadcast. |
| `ProfilingSegment`, `ParentSegmentId` | Retain `ProfilingSegment` and `ParentSegmentId`: one Runtime-owned interval per invocation, preserving its session, node, timing, parent, and collection-ended quality. Runtime intervals are not replaced by operation aggregates. |
| `ProfilingSegmentOutcome` | Runtime segment state is separately open/closed; an open interval has no terminal outcome. `Success` maps to `Completed`, `Failure` to `Failed`, `Cancellation` to `Canceled`, and `Interruption` to `Incomplete`. Closed segment outcomes use the common terminal vocabulary. |
| `ProfilingPhaseMarker`, `ProfilingActionMarker` | `ProfilingMarker` with explicit session-wide or node-specific scope, marker kind, label, and UTC timestamp. A session-wide marker has no invented node. An instantaneous marker never becomes a duration-bearing segment. |
| `AddPhaseMarkerAsync` | `AddMarkerAsync`; retain the existing session-wide annotation use case. Node-local actions create node-scoped markers. |
| `IProfilingMeasurementService`, `IProfilingMeasurementScope` | `IRuntimeProfilingMeasurementService`, `IRuntimeProfilingMeasurementScope`. Retain async `BeginAsync`/`MeasureAsync`, joining an active runtime session or owning a bounded new session when none exists, durable interval start/end, and owned-session cleanup. Runtime measurement errors remain explicit `Result` outcomes. |
| `ProfilingMetricObservation.SegmentId` and the custom metric listener | Runtime metric observations reference `SegmentId`; the Runtime listener and meter capture remain Runtime-specific. They do not create operation segments or infer per-operation CPU/memory use. |
| `IProfilingQueryService`, runtime evaluation, archive, and Perfetto services | Runtime-prefixed query/evaluation/archive/export services with the existing Runtime capabilities. Operations uses its separate query service and aggregated segment model. |
| `IProfilingStore`, `InMemoryProfilingStore`, `EntityFrameworkProfilingStore<TContext>` | Focused `IRuntimeProfilingStore`/`IOperationProfilingStore` facets behind the selected `IProfilingStorageProvider` and the two provider implementations specified here. Runtime lifecycle writes retain their existing guarantees; operation writes use periodic batches. |
| `IProfilingContext`, `WithEntityFrameworkStore<TContext>()` | `IProfilingDbContext`, `WithEntityFrameworkProvider<TContext>()`. The EF context interface and entities remain in `Infrastructure.EntityFramework`, never in the backend-independent contracts assembly. |
| Runtime JSON collections named `Segments`, `PhaseMarkers`, and `ActionMarkers` | Runtime `Segments` and `Markers` with explicit marker scope/kind. Parent and metric references use segment identities. Operation segment summaries remain separate records. |
| Existing `profiling` runtime console commands | `profiling runtime` commands retain the existing runtime actions through the renamed services. Runtime clear selects the Runtime dataset. This does not add an Operations CLI or custom-client HTTP API. |
| Existing Runtime dashboard routes and slices | Runtime pages/actions/content live under the dashboard's `/profiling/runtime` group. Their existing actions, exports, and refresh behavior use renamed Runtime services; Operations/Requests have the separate views defined in section 9. |

The existing Runtime measurement API intentionally has different cost and ownership from an operation segment. Its store calls and optional session start remain confined to explicitly invoked Runtime measurement. `IOperationProfiler` helpers never call that service, start Runtime collection, or await storage. The two paths can reuse clocks, bounded metadata/error handling, and interval arithmetic without sharing ambient session ownership or changing their persistence behavior.

Keep the existing Runtime defaults unless this specification overrides them: 1-second snapshot interval, 500 ms minimum interval, 30-second automatically stopped sessions, 1-second participation deadline, 1-second finalization grace, and retention of 20 unpinned terminal sessions or 7 days. Runtime outcome/evaluation logic continues to distinguish missing evidence from zero. Regression coverage includes Broadcast target failures, missed snapshots, restart/finalization, pinning, custom metrics, evaluation, and Runtime exports.

EF hosts implement the final `IProfilingDbContext`, call `ConfigureProfiling()` from model setup, and provide host-owned migrations. The mapping includes Runtime segments/markers, operation roots/summaries/typed metadata, shared nodes, and the writer/clear coordination rows in section 8.7. Common recording/query contracts expose none of those EF entities. Every repository-owned example context, migration, seed/fixture, and test uses the final model; `WithEntityFrameworkProvider` alone does not create or migrate tables at application startup.

This unused feature adopts a deliberate breaking storage/serialization revision. Existing disposable profiling tables can be rebuilt through an explicit host migration/reset; unrelated application tables are preserved and startup never silently deletes an old schema. Runtime archives use format `bitdevkit.profiling.archive`, version 2, with `Segments`, scoped `Markers`, and updated parent/metric references. Version 1 import returns an unsupported-version result before mutation; no silent interpretation of old fields or names is allowed. Import still remaps identities and never links imported evidence to live local operations. Raw Runtime JSON and Perfetto output use the final segment/marker vocabulary and retain their export purpose. No operation export is introduced.

## 3. Data and lifecycle contracts

### 3.1 Operation record

`OperationProfilingRecord` contains the following fields:

| Field group | Recorded information |
| --- | --- |
| Identity | `Id`, stable `Key`, optional display name, `Kind`, correlation ID, optional parent operation ID. |
| Execution | `NodeId`, `NodeKey`, bounded node/host display name, process ID, application version, and process-start UTC context. |
| Time | `StartedUtc`, `CompletedUtc`, monotonic elapsed duration, and same-process timing offsets when available. |
| Result | Outcome, bounded safe failure information, and a separate indication of observed failed, canceled, or incomplete segment invocations. |
| Enrichment | Typed dimensions, named measurements, and bounded source metadata. |
| Context | Active admitted recording operations on the node at entry, including this operation, and optional runtime session-at-start hints. This is observed recording concurrency, not total application concurrency. |
| Capture quality | Truncation, incomplete segments, rejected metadata, clock discontinuity, and observation limitations. |
| Work breakdown | Zero or more owned `ProfilingSegmentSummary` entries and a bounded root wall-time breakdown. |

Built-in kinds are `HttpRequest`, `BlazorInteraction`, `Service`, `Job`, `Pipeline`, `Orchestration`, and `Custom`. An adapter can supply a bounded stable custom kind. Grouping includes kind and key by default so unrelated workloads with the same key do not merge. Correlation IDs and occurrence IDs are lookup values, not automatic grouping keys.

Source metadata uses a bounded typed envelope identified by adapter kind and schema version. `HttpRequestProfilingMetadata` projects the HTTP envelope without adding mandatory HTTP fields to the core record. Other adapters use the same mechanism. Known adapter fields can have indexed storage projections, but unknown metadata never requires deserializing application object graphs.

The runtime-at-start hint is optional. Missing runtime evidence does not make an operation invalid. A parent operation link is also non-owning and may resolve to unavailable or expired data.

### 3.2 Repeated and nested segment aggregation

The operation recorder groups invocations by their full key path. The path is a structured sequence of keys, not an ambiguous concatenated string. A summary retains its `Key`, path, parent path, optional display name, and operation/node identity. It contains no list of completed invocation events.

For example, five outer calls with three inner calls each produce two summaries:

```csharp
for (var batch = 0; batch < 5; batch++)
{
	profiling.RunSegment("Load", load =>
	{
		for (var item = 0; item < 3; item++)
		{
			load.RunSegment("Read", read =>
			{
				ReadItem(batch, item);
				read.SetMeasurement("items.read", 1, "count", MeasurementAggregation.Sum);
			});
		}
	});
}
```

| Summary path | Invocation count | Relationship |
| --- | --- | --- |
| `Load` | 5 | Top-level named work. |
| `Load / Read` | 15 | Child work across all five `Load` invocations. |

Another `Calculate / Read` segment produces a separate summary. Nesting by path preserves that distinction even when leaf keys match. Repeated invocations at the same path merge regardless of loop iteration or execution order. Iteration indexes are not appended to the key.

Each summary stores these statistics:

| Statistic | Meaning |
| --- | --- |
| Invocation count | Number of admitted segment invocations contributing to this summary. |
| Outcome counts | Completed, failed, canceled, and incomplete invocation counts. |
| Total duration | Sum of each invocation's inclusive elapsed duration. |
| Average duration | Total duration divided by the contributing invocation count. |
| Minimum and maximum duration | Smallest and largest observed invocation duration. |
| Total self duration | Sum of invocation elapsed time outside its directly nested child intervals. |
| Per-outcome duration statistics | Count, sum, minimum, maximum, and self-time sum for each outcome, so outcome filters remain meaningful. |
| Measurements | Values reduced with their declared aggregation and unit, including contributing sample count. |
| Metadata and quality | Consistent segment dimensions, mixed/missing flags, bounded error categories, and capture completeness. |

Parent total duration already includes child work. Do not add `Load` and `Load / Read` to obtain operation duration. For one invocation, self duration subtracts the union of its direct children's intervals, clipped to that invocation. Subtracting the sum would be wrong when children overlap.

Across repeated or parallel invocations, total duration and total self duration are cumulative work measurements. They can exceed operation wall time. Self duration means time outside instrumented children, not CPU time. It includes waits and any uninstrumented work.

For example, a 100 ms parent with 80 ms and 70 ms children overlapping for 50 ms has 100 ms inclusive duration and 0 ms self duration. Its children's total is 150 ms. The dashboard displays these values without presenting 150 ms as a share of the 100 ms operation.

The recorder keeps only bounded active invocation state plus accumulated summaries. It can measure child coverage using active-child transitions and a monotonic clock. On completion it updates the path's statistics and releases the invocation state. It does not retain an interval list for every loop iteration. Updates are concurrency-safe for parallel calls to the same path.

An active parent handle identifies the actual parent invocation while work runs. Reusing the same path does not merge those live handles. Each child contributes coverage to its actual parent before its values fold into the shared path summary. If a parent closes with live children, those recording scopes close as incomplete at that boundary. Later disposal cannot add their values again or extend the parent's measured lifetime.

Limits apply to distinct paths, nesting depth, and simultaneously active segment scopes. A million sequential calls to the same admitted path do not consume a million retained slots or stop recording after the first 128 calls. Their instrumentation still has per-call cost; applications can profile a whole batch when per-item timing is unnecessary.

A rejected segment start still isolates its descendant recording context until that helper/scope exits. Descendants are not silently attached to the rejected segment's parent or treated as top-level work. No rejected invocation is added to a valid summary; affected self-time and wall-time breakdown remain explicitly partial. An invalid initial operation key similarly rejects capture while its helper still executes the business delegate once. An invalid `SetKey` preserves the previously accepted key.

### 3.3 Dimensions and measurements

Dimensions support strings, Booleans, integers, finite decimal or floating-point numbers, and UTC datetimes. Unsupported objects are rejected from capture without interrupting application execution. Stored and returned values preserve their types. The following validation rules apply identically in every recorder and provider.

- Keys and names must be nonempty, contain a non-whitespace character, and fit their configured length limit. Leading or trailing whitespace is significant. Null values, unsupported objects, out-of-range integers, non-finite numbers, overlong developer keys, and overlong dimension values are rejected, never silently shortened into another group. A rejected replacement leaves the previous accepted value intact and sets a quality flag.
- `DateTimeOffset` dimensions require a zero offset; `DateTime` dimensions require `Kind = Utc`. The caller performs any intended conversion. Integers normalize to `Int64`, decimal retains its type, and floating-point inputs normalize to finite `Double`. JSON uses explicit type tags and invariant, lossless scalar text for numeric dimension/filter values, so browser number precision cannot merge distinct groups. Datetimes use the required ISO 8601 `Z` representation. Omitted values stay absent; empty strings remain valid values.
- Adding a distinct entry beyond a count or payload limit rejects that entry. Replacing an existing entry is allowed only if the resulting retained payload fits. Updating one key is atomic; concurrent updates use the last accepted write and do not imply deterministic application ordering. Display names and sanitized error messages may be shortened at a valid Unicode boundary with an explicit truncation flag. The separately specified hashed HTTP path shortening remains the only automatic grouping-key shortening.

Operation-level measurements have a key, finite numeric value, and explicit unit, such as `count`, `bytes`, or `ms`. Replacing an existing key updates its value. It does not implicitly add to it. Missing values remain distinct from zero and empty strings. Queries never combine incompatible units or dimension types.

Segment measurements additionally declare how values from repeated invocations combine: `Sum`, `Min`, `Max`, `Average`, or `Last`. `SetMeasurement` replaces the value inside the live invocation; the recorder applies the reduction once when that invocation closes. Bytes, row counts, and retry counts normally use `Sum`. `Average` retains sum and contributing sample count so averages remain weighted correctly. `Last` means the last observed invocation completion and is labeled accordingly.

The same path and measurement key must use compatible units and aggregation rules. Conflicts produce an explicit unavailable/conflicting measurement and quality flag, never a silently mixed value. Missing measurements do not contribute zero samples. Outcome-specific reductions support the same outcome policy as timing statistics. A summary retains the contributing count for each measurement; a valid value of zero still contributes one sample. `Last` uses a recorder-assigned completion sequence within the operation, retaining that sequence and the invocation completion UTC alongside its value. Cross-operation `Last` selection orders these UTC values with operation ID and local sequence as deterministic tie-breakers; it is labeled observed time and does not claim causal ordering between nodes with skewed clocks. Integer overflow or an unrepresentable aggregate marks that measurement unavailable instead of wrapping or clamping it into a plausible result.

Segment dimensions describe the summary's named work. A value is shown as consistent only when contributing invocations agree. Different values, or missing values on some invocations, are marked mixed or partial with bounded metadata. The system does not keep hidden per-invocation records to recover a later dimension split. Segment-dimension filters match consistent summaries; mixed summaries are separately selectable. Use distinct stable segment keys for components that need separate timing totals. Operation dimensions remain the primary way to compare workloads.

Controller code can derive dimensions from validated query parameters. The profiling core does not interpret their application-specific meaning or collect query strings automatically. Storage, database, tenant, workload, and scenario labels are ordinary dimensions supplied by application code.

The recorder captures `StartedUtc`, `CompletedUtc`, and elapsed duration automatically on each operation profile. Developers do not need to duplicate these fields as dimensions. An application input date has a different meaning from execution time. Record it only when it helps workload analysis, using a descriptive name such as `inputFromUtc`. Prefer bounded workload categories for grouping instead of a distinct timestamp for every execution.

Stable logical keys describe work. Dimensions describe bounded comparison characteristics. HTTP path keys can contain concrete path segments, including identifiers. Controllers can override them with a logical key when those values should share one group. URL authority, query strings, headers, credentials, and application payload values are not automatically added to keys.

#### Comparison and grouping

Operation keys, each component of a segment key path, custom kind identifiers, dimension names, and measurement names use one invariant, case-insensitive comparison rule. `Load`, `load`, and `LOAD` identify the same group; `Load / Read` and `load / read` identify the same segment summary. Identical leaf keys under different parent paths remain separate. Colors, filters, distinct-path limits, dimension replacement, and group counts use that same identity.

The shared comparer constructs a comparison key using `.ToUpperInvariant()` and compares the result ordinally. No culture-sensitive database transformation, accent folding, whitespace trimming, or additional Unicode normalization is implicit. Providers persist/index this computed comparison key using ordinal/binary comparison semantics rather than their database's default collation. Structured paths preserve component boundaries; joining components with an ambiguous separator is not a valid identity. Bounded default-path hashing uses the full comparison key before shortening, so differently cased long paths do not split into different groups.

Records keep the supplied casing for display. A segment summary retains the first admitted invocation's label; a cross-operation group can display the earliest retained occurrence's label, with ID as a deterministic tie-breaker. Its comparison key remains stable when that display label changes. Canonical key bytes or a collision-checked representation must be portable across all supported providers.

Dimension values retain their data semantics: strings are ordinal and case-sensitive, Booleans remain Boolean, supported integer inputs normalize to signed 64-bit integers, and decimal and floating-point values retain separate type tags. Thus integer `1`, decimal `1`, floating-point `1`, and string `"1"` are distinct dimension values. Equivalent values within one numeric type compare numerically, UTC datetimes compare by their UTC instant, and a missing dimension stays distinct from an empty string or zero. Units remain case-sensitive. Applications normalize string dimension values explicitly when their domain needs case-insensitive value grouping.

### 3.4 Outcomes and finalization

Persisted operation outcomes are `Completed`, `Failed`, `Canceled`, `Aborted`, and `Incomplete`. Segment invocation outcomes are `Completed`, `Failed`, `Canceled`, and `Incomplete`. A persisted segment summary contains outcome counts rather than one outcome that hides mixed results. Live scopes can be open, but persisted records contain no open segment handles.

Delegate helpers record normal completion, propagate the original return value, and rethrow application exceptions unchanged. They classify cancellation using the relevant token. An unrelated `OperationCanceledException` is not automatically a canceled operation. Adapters can classify failed `Result` values and domain outcomes even when no exception is thrown.

Manual scopes expose `Complete`, `Fail`, and `Cancel`. `Fail` accepts either an exception for safe classification or a bounded failure descriptor for a domain/result failure. HTTP lifecycle code can mark an operation aborted. These methods declare the outcome; disposal records the end and finalizes the scope. Unmarked disposal produces `Incomplete`. A plain `using` scope cannot infer which exception caused stack unwinding, so automatic exception classification belongs to delegate helpers or explicit calls to `Fail` and `Cancel`.

Every recording scope transitions once from open to finalizing to finalized. Repeated declarations and disposal do not increment counts again. During ordinary finalization, an observed failure takes precedence over a declared success or cancellation; success cannot erase a failure in the same invocation. HTTP transport abort takes precedence at its observation boundary while retaining any observed failure information. Forced capture expiry follows section 3.8 because the execution's terminal outcome has not been observed. Cancellation requires evidence as defined below. Once finalized, late declarations cannot change a record or summary.

Operation finalization closes still-open segment invocations at the observed boundary as `Incomplete`, folds them into their summaries, freezes retained state, and makes one nonblocking enqueue attempt. It releases admission accounting exactly once. A failed inner invocation does not automatically imply failure of a successfully recovered outer operation. Both outcomes remain visible through the root outcome and segment outcome counts.

Recording failures must not suppress or duplicate business work, change results, or replace application exceptions. Instrumentation helpers execute their delegate exactly once even if capture is disabled, rejected, or internally fails.

### 3.5 Failure and recovery rules

Application outcome and capture quality are independent. A completed operation can contain failed segment invocations, and a failed operation can have complete diagnostic capture. A recorder or persistence fault is a diagnostic failure, not evidence that the business operation failed.

| Situation | Recorded result | Application behavior |
| --- | --- | --- |
| An exception escapes a segment helper | That invocation is failed, with elapsed time and safe failure classification. | The original exception propagates with its stack preserved. |
| The exception also escapes enclosing segment or operation helpers | Each affected invocation and the operation independently record their observed failure. | No extra wrapping, retry, or exception replacement occurs. |
| An outer scope catches a failed segment and successfully recovers | The inner invocation remains failed; the outer scope can complete. The operation exposes its failed-segment count. | Recovery remains entirely application-owned. |
| A delegate returns a failed domain `Result` | A feature adapter, explicit result classifier, or manual `Fail` declaration marks that scope failed. | The original result is returned unchanged; no synthetic exception is thrown. |
| A cancellation exception is attributable to the relevant execution token | The affected scope is canceled, with elapsed time retained. | Original cancellation behavior propagates unchanged. |
| A deadline or timeout is identified by the adapter | Failed with a timeout classification, unless the adapter explicitly identifies ordinary caller cancellation. | No retry or timeout is added by profiling. |
| A request's transport is interrupted | The request operation is aborted, with final observable status and partial-byte information. | The HTTP adapter follows the host's existing abort behavior. |
| A scope closes without an outcome, or its parent closes first | The recording invocation is incomplete and its duration is bounded by the observed close. | Profiling does not cancel or stop the underlying work. |
| The recorder, enrichment, or result-classification hook fails | Capture is marked partial when a consistent record remains possible; otherwise capture is dropped and counted. | Business execution and its result or exception are preserved. |
| Enqueue or persistence fails | Rejection, retry, or permanent loss follows the bounded storage policy. | No application-path database write, blocking flush, or error response is introduced. |

A generic helper cannot infer failure from an arbitrary return object. Result classification is explicit and belongs to an adapter or a supplied classifier. If that classifier fails, the outcome is unknown/incomplete with a classification-quality flag unless another reliable outcome was observed. It must not convert an unclassified result into a confident success or business failure.

Cancellation is not inferred from a token becoming canceled after work already completed. By default, a helper classifies an `OperationCanceledException` as canceled only when its supplied token is cancelable, is canceled, and equals the exception's token. A different or absent exception token follows the failure path unless an adapter or explicit manual declaration supplies reliable attribution. Linked deadline tokens require adapter-provided attribution to distinguish timeout from caller cancellation. HTTP transport interruption retains the adapter's Aborted classification. Profiling forwards the application's token without adding cancellation checks that prevent an otherwise invoked delegate from running.

In parallel work, each child helper records its own outcome. The parent reflects the result it actually observes when joining those tasks. The recorder neither cancels siblings on the first failure nor creates an `AggregateException` to change the caller's exception shape. Capturing only the exception observed at `Task.WhenAll` must not erase failures already recorded by the child helpers.

Repeated invocations retain both successful and unsuccessful timing samples. For example, a segment path with eight completed calls and two failed calls has count ten, completed count eight, failed count two, and separate duration statistics for each outcome. Measurements supplied before a failure remain in that outcome's bucket; measurements never supplied remain absent. A successful retry adds a new completed invocation; it does not relabel or remove the failed attempt. Counts across nested paths represent failed invocations, not a count of unique root causes.

### 3.6 Cleanup and failure isolation

Helpers run timing finalization and ambient-context restoration from exception-safe cleanup paths. Cleanup never throws a recoverable profiler exception over a business exception, cancellation, or successful return. An internal error after the delegate ran must not cause a fallback path to execute that delegate again.

Operation observation is fail-safe for recoverable instrumentation faults: starting scopes, reading their status, enrichment, clocks, sampling, logging, body observation, finalization, and persistence must not throw into application execution. A failed recording startup falls back to observation-free execution. Delegate helpers and middleware invoke business work exactly once; cleanup faults never replace its result, exception, cancellation token, or stack. Background worker initialization, timers, warnings, and shutdown are guarded as well as provider calls, so a recoverable diagnostic worker fault cannot trigger the host's unhandled-background-exception shutdown policy. Fault counters remain observable without recursively using the failed instrumentation.

Unexpected clock or aggregation faults that leave state inconsistent discard the capture and immediately release recording admission. Invalid or unsupported metadata is rejected locally; it does not require an exception or termination of otherwise consistent capture. An unavailable queue age is explicitly labeled unavailable rather than interpreted as zero.

Each admitted scope releases its active-state budget once, including when timing aggregation, record freezing, or enqueue fails. Once a record is accepted, payload accounting transfers to the queue/writer until persistence or loss releases it. Failed capture cannot leave a circuit, thread, or subsequent operation associated with stale state. Child closure, parent closure, and concurrent disposal coordinate one finalization rather than duplicate summary updates.

Recorder guards surround instrumentation-owned work, not arbitrary business code. Errors while application code computes a dimension value, for example, remain application errors. If an aggregate cannot be updated consistently, it is labeled invalid/partial or excluded; the profiler does not publish plausible but corrupted totals. Loss reporting uses bounded counters and rate-limited logging and does not recursively invoke profiling.

Invalid registration or configuration is reported through normal startup validation. Explicit profiling control/query APIs report their own failures through the repository's `Result` and HTTP conventions. Failure isolation applies to observing application work; it does not make failed administration queries look successful. Runtime probe errors are reported as unavailable samples or failed collection according to runtime lifecycle rules, without inventing zero-valued measurements.

This failure-isolation guarantee covers recoverable exceptions in profiling-owned code and participating adapters. It cannot cover process-fatal failures such as stack overflow, unrecoverable memory exhaustion, native crashes, forced termination, or arbitrary application code executed to compute metadata. Invalid explicit registration remains a startup validation error, not an observation fault.

An ungraceful process termination cannot guarantee scope cleanup or a final batch. The system does not fabricate completed operations after restart. Buffered diagnostic loss remains an explicit limit of this persistence model.

### 3.7 Bounded failure details

An operation failure and segment failure summaries use a small descriptor containing source category, safe error code, exception type when available, and an optional sanitized message. They never retain exception objects, `Exception.Data`, result payloads, response bodies, or automatic stack-trace dumps. Existing application logs remain the detailed error source, linked through operation and correlation IDs where available.

Segment failure categories aggregate by stable source/code/type. Keep counts and first/last observed UTC times, plus at most one sanitized message sample per category. Default to eight categories per path, with remaining classified failures counted in `Other`. The same category is counted at most once per failed invocation, even when `Fail` is called repeatedly. Cancellation and incomplete reasons remain separate from failed-invocation categories.

Messages do not form aggregation keys. Raw exception messages are not retained by default; a host's safe-error policy may supply a sanitized bounded message. If sanitization fails, omit the message and keep safe type/code information. All failure metadata shares the operation payload budget, so repeated errors in a loop cannot create an unbounded error list.

### 3.8 Maximum recording lifetime

`WithOperationProfiling` configures `MaxRecordingDuration`, defaulting to 15 minutes, and `CleanupInterval`, defaulting to 5 seconds. Both are positive finite durations; the cleanup interval cannot exceed the recording duration. A host profiling longer jobs can raise the recording duration during setup. This is a diagnostic recording limit, not an application timeout.

An independent recorder cleanup worker uses the injected monotonic clock and scans only the bounded admitted-operation registry. It does not depend on a healthy provider, a successful flush, or a request completion callback. Normal recorder calls also check expiry. On expiry, close the recording at its logical deadline, close open segment invocations as incomplete, freeze one best-effort record, and release recording capacity within the next cleanup tick while the process is scheduled. Previously accumulated failure information remains available, but the root outcome is `Incomplete` with reason `CaptureDeadlineExceeded`; provisional declarations do not claim an execution outcome that was never observed.

The record's completion UTC denotes the end of observation. Capture quality states that actual execution completion was not observed and the recorded duration is a lower bound on execution duration. Normal latency comparisons exclude this incomplete record by default. Segment arithmetic and wall-time coverage stop at that same deadline, even when cleanup runs later.

After expiry, live handles retain only the minimal closed-boundary state needed for safe no-op calls and context restoration. They release aggregate buffers and any completed-record reference after enqueue or rejection. Descendant segment helpers and join-or-start behaviors cannot reopen that boundary. A deliberately started independent operation can still record under the ordinary ownership/suppression rules. Business delegates continue and return or throw unchanged; later disposal or HTTP callbacks cannot mutate or enqueue the record again. HTTP body observers become pass-through and release recording references without prematurely disposing the underlying transport.

Host shutdown first closes remaining capture as incomplete with reason `HostStopping`, then performs the configured bounded queue drain. Neither cleanup nor shutdown cancels application tokens. Abrupt process termination remains best effort and cannot fabricate recovery records.

## 4. Request Profiling adapter

### 4.1 Capture boundary and enrichment

The HTTP middleware owns the outer `HttpRequest` operation. Controllers and services enrich that operation and create segments through `IOperationProfiler`. A controller does not start another root for the same request.

The middleware observes the full downstream server request lifecycle, including response serialization and streaming. Controller completion is not the operation end. Requests rejected before the ASP.NET pipeline are outside its observation boundary.

The middleware runs before components that can terminate an observed request. With an empty blacklist and the default all-request strategy, capture includes authorization failures, unmatched routes, preflights, downstream static responses, and profiling endpoints. The application can exclude paths or choose reduced sampling. There is no endpoint opt-in. Pipeline placement and an idempotent request feature preserve one sampling decision and prevent duplicate records during error-handler re-execution or replacement DI scopes.

The adapter performs these actions:

1. Check effective request enablement and evaluate blacklist patterns against the original incoming path. A disabled or excluded request does not start an HTTP operation.
2. For a non-excluded request, calculate its initial path key, read cached node identity, and evaluate the configured sampling strategy once. A skipped request executes under capture suppression.
3. For a selected request, create an operation ID, retain its sampling metadata, and increment node-local selected-request concurrency accounting.
4. Attempt nonblocking operation admission and read cached runtime context. Selection does not bypass resource limits.
5. Register header and completion observation before invoking downstream work.
6. Capture the original endpoint route template as separate metadata after endpoint selection, using post-routing enrichment where required.
7. Observe status, errors, cancellation, body sizes, and lifecycle completion.
8. Finalize once when normal response processing has completed, or after execution unwinds on failure or abort.

For selected requests, elapsed time starts at middleware entry, including eligibility and sampling work, using a captured monotonic entry timestamp. The scope and body observers are allocated only after selection and admission. All paths invoke downstream request handling normally.

The default key is the incoming path after optional prefix stripping, as defined in section 4.4. HTTP method and endpoint route template remain separate metadata. Unmatched paths follow the same rule. The adapter preserves the original path key and route metadata across error re-execution and never overwrites a developer's explicit key. It does not persist the full URL or query string.

The lifecycle implementation coordinates `OnStarting`, `OnCompleted`, exception handling, cancellation, and abort observations. A missing normal completion callback must not leak an active scope on a terminated response. Status comes from the final observable response rather than an unobserved default value.

Normal HTTP completion below status 500 maps to `Completed`. Status remains available to distinguish 4xx responses. A final 5xx, an exception reaching the HTTP error-handling boundary, or an explicitly failed request outcome maps to `Failed`. A recovered inner segment failure alone does not fail the request. Matched application cancellation maps to `Canceled`. Transport interruption maps to `Aborted`, including interruption after a 200 header was sent. Store no status when none was observed. Retain error classification separately when a failed request also aborts; a token canceled after normal completion does not retroactively turn it into an aborted request.

### 4.2 HTTP metadata and response ID

The HTTP metadata projection contains application request ID, method, original route template, final observable status, active selected requests at entry, declared request length, observed request bytes, declared response length, and observed response bytes. The concurrency field counts sampling-selected requests before admission, excludes blacklist/sampling skips and disabled capture, and is labeled accordingly. Each byte observation includes measurement kind and completeness. Optional values remain absent when unavailable.

Selected records also contain the sampling strategy key, configuration key, and inclusion probability when the strategy can state one. These fields describe the policy applied at entry and remain unchanged by controller enrichment. The configuration key identifies the effective policy settings for comparison; it is not an application grouping key.

`X-Request-Profiling-Id` contains the operation's full opaque GUID. For sampling-selected requests it is emitted before headers are sent, including when capture admission is rejected. Disabled, blacklisted, or sampling-skipped requests receive no header from this feature. An ID is a lookup value, not proof of persistence and not an authorization credential. Non-HTTP callers obtain the same kind of ID from their operation scope.

The host's CORS policy exposes the header when a cross-origin frontend needs to read it. At `OnStarting`, the outer middleware replaces any server-cache-supplied profiling header with the selected request's own ID, or removes it when this invocation was not selected. This small header-cleanup callback remains when the installed HTTP adapter is disabled, excluded, or sampling-skipped; it creates no profiling scope, record, body observer, or lifecycle log. A response served wholly from a browser or upstream cache causes no new server operation and may contain an earlier ID.

### 4.3 Byte observation

Response size means successfully observed body bytes at the documented outer application response-body boundary, after application compression when present. It does not mean total network traffic or receipt by the client. Stream writes, `BodyWriter`, and send-file paths are covered by a response-body feature adapter without buffering content or double-counting forwarded writes.

The observer preserves streaming, flush, cancellation, and error behavior. An unsupported transport path reports unavailable or partial measurement. Declared `Content-Length` is stored separately and never substituted silently for measured bytes.

Request bodies are not read solely to count bytes. `ObserveRequestBodyBytes` defaults to false; declared length remains available and observed request bytes remain absent. When explicitly enabled, the adapter counts bytes consumed by normal application reads through supported stream and pipe-reader paths, without initiating reads or buffering content. Unsupported or replaced body features mark measurement unavailable or partial rather than reporting zero.

Long-lived responses remain active until their observation boundary closes or the recording limit in section 3.8 expires. Upgraded connections, WebSockets, and long-lived streaming responses do not produce a record per message; applications can profile independent message work explicitly. Expiry records an incomplete observation while the connection continues. Final records become queryable only after finalization and persistence.

### 4.4 Path keys and prefix stripping

For this adapter, the incoming path is `Request.PathBase + Request.Path` as observed at middleware entry, excluding query string, scheme, and authority. This definition retains a mount prefix when the host has already separated it into `PathBase`. The adapter uses the server's parsed path representation without another URI-decoding pass. Empty paths become `/`.

`StripPathPrefix` defaults to empty. A configured prefix is removed once from the start, on a path-segment boundary, using ordinal case-insensitive comparison. `/api` matches `/api` and `/api/calculations`, but not `/apiary`. A configured trailing slash is normalized away, with `/` treated as no stripping. If removal leaves no path, the key is `/`. Otherwise the remaining leading slash and path casing are preserved. Prefix text in the middle of a path is never removed.

| Incoming path | Configured prefix | Default key |
| --- | --- | --- |
| `/api/calculations` | `/api` | `/calculations` |
| `/api/orders/42` | `/api` | `/orders/42` |
| `/api` | `/api` | `/` |
| `/apiary/items` | `/api` | `/apiary/items` |
| `/health` | Empty | `/health` |

The default contains neither HTTP method nor an inferred route template. Method remains a filter and optional grouping field. Different query strings produce the same default key. Concrete path segments remain concrete unless a controller calls `SetKey`, for example with `/orders/{id}`. Automatic prefix stripping does not alter developer-supplied overrides.

Path keys obey the configured key-length bound. Overlong defaults use a bounded readable prefix plus a stable hash suffix and a shortened-key flag, rather than silently merging all paths with the same leading characters. Matching exclusions uses the full incoming path before shortening or prefix stripping. No unbounded copy of that path is retained as extra metadata.

### 4.5 Request blacklist

`BlacklistPatterns` defaults to `/_bdk/**`, `/health*`, `/swagger/**`, `/scalar/**`, and `/openapi/**`. `Blacklist(...)` replaces the complete collection; `Blacklist()` clears it. Any matching pattern excludes the request; order does not matter. In this setting, route matching means matching the incoming request path, not the endpoint's `{parameter}` template. This permits the skip decision before routing and before a diagnostic scope exists.

Patterns are anchored to the entire incoming path and use ordinal case-insensitive comparison. Literal segments match literally, `*` matches zero or more non-slash characters within one segment, and `**` as a complete segment matches zero or more path segments. `/swagger/**` matches both `/swagger` and descendants. Regex syntax, method filters, query strings, and route-parameter expansion are not part of this matcher. A trailing slash is ignored for matching only, except for the root path.

| Pattern | Examples matched | Example not matched |
| --- | --- | --- |
| `/health` | `/health`, `/health/` | `/health/details` |
| `/health*` | `/health`, `/healthz`, `/health-ready` | `/health/live` |
| `/health/**` | `/health`, `/health/live` | `/healthcheck` |
| `/api/internal/*` | `/api/internal/status` | `/api/internal/jobs/42` |
| `/_bdk/**` | `/_bdk/dashboard/profiling/requests` | `/api/calculations` |

Matching uses the original incoming path before prefix stripping, controller key overrides, or error-handler re-execution. Changing a grouping key cannot change request eligibility. Host rewrite placement determines the path visible at middleware entry and is documented by the host.

A blacklist match creates no HTTP operation ID, recording context, segment data, body observer, profiling header, or lifecycle logs. A lightweight execution-local suppression flag prevents downstream segment helpers and join-or-start behaviors from creating replacement diagnostic operations for that excluded request. Explicit operation helpers in the same suppressed flow also perform business work without capture. Suppression is restored after the request. Independent queued or detached executions establish their own capture context and eligibility.

This differs from `Requests.Enabled(false)`, which disables only the automatic HTTP adapter and leaves explicit operation instrumentation available. Exclusions are intentional coverage policy, not dropped records. Health may expose one aggregate excluded-request count without recording the excluded paths or emitting a log per match.

Validate and prepare patterns once during setup. Invalid patterns fail configuration validation. Use a bounded, non-backtracking matcher; do not compile regexes or access storage on each request. Defaults allow at most 128 patterns of 256 characters each. Built-in entries exclude DevKit, root health endpoints, Swagger, Scalar, and OpenAPI paths. Applications can replace or clear these exclusions.

### 4.6 Request-sampling strategy

Request sampling is a pluggable policy selected inside `WithRequestProfiling`. Omitting `WithSampling` selects `AllRequests`. Explicitly selecting another policy replaces this default. A configured request adapter has exactly one effective strategy; the last explicit sampling selection during setup wins. Switching means choosing another strategy at setup and restarting/reconfiguring the host through its normal startup flow. Live strategy changes are not required.

The policy decides at request entry, after blacklist evaluation and before an operation or segment is recorded. This is head sampling: later status, latency, or errors are not available to the decision. The distinction follows [OpenTelemetry's sampling concepts](https://opentelemetry.io/docs/concepts/sampling/#head-sampling); this feature introduces no OpenTelemetry dependency, distributed trace protocol, or parent-trace sampler.

The supplied strategies are:

| Fluent selection | Behavior | Scope and parameters |
| --- | --- | --- |
| `AllRequests()` | Select every request that passed the blacklist. This is the default. | Inclusion probability is 1, subject to later capture and persistence limits. |
| `Probability(0.10)` | Independently select each eligible request with a 10% probability. | A finite probability from 0 to 1 inclusive. Zero selects none and one selects all. The fraction is approximate, not an exact quota in every batch or interval. |
| `RateLimit(requestsPerSecond: 25, burst: 50)` | Select immediately when the node-local token budget permits; otherwise skip capture. | Positive finite refill rate and positive integer burst capacity. Applies across all eligible paths on that node. |

These are the three built-in strategies in scope. `RateLimit` uses a thread-safe token bucket with a monotonic clock, initially full up to its burst capacity. Its state is process-local, not recreated for each request or key. Process restart resets it. It limits selection attempts, not the number of successfully persisted records; a later admission rejection does not refund a token. It has no meaningful fixed per-request inclusion probability.

Sampling never delays requests, waits for a token, returns HTTP 429, cancels application work, or changes business retries. Probability decisions use thread-safe unbiased random input without a new random generator per request. Clock and random sources are controllable in tests. Neither alternative makes storage or network calls.

To change the default policy, select one of these setup alternatives:

```csharp
// Probability-based selection of roughly 10% of eligible requests.
profilingBuilder.WithRequestProfiling(requests => requests
	.WithSampling(sampling => sampling.Probability(0.10)));

// A per-node selection budget with a burst allowance.
profilingBuilder.WithRequestProfiling(requests => requests
	.WithSampling(sampling => sampling.RateLimit(
		requestsPerSecond: 25,
		burst: 50)));

// An application-supplied strategy registered through DI.
profilingBuilder.WithRequestProfiling(requests => requests
	.WithSampling(sampling => sampling.UseStrategy<MyRequestSamplingStrategy>()));
```

Each example assumes enabled `WithOperationProfiling` and represents a separate policy choice. The shared queue, periodic batch persistence, and segment aggregation remain unchanged.

### 4.7 Sampling extension and execution contract

`IRequestProfilingSamplingStrategy` exposes a synchronous `Decide(RequestProfilingSamplingContext)` operation returning `RequestProfilingSamplingDecision`. A decision contains capture/skip, a bounded reason code, and an optional known inclusion probability. The core supplies validated strategy/configuration identifiers from the registration descriptor.

The immutable context contains the HTTP method, incoming path, initial key, cached node identity, and UTC entry time. It does not contain the live `HttpContext`, scoped services, body, query values, final route template, response status, duration, or controller-derived dimensions. A strategy must not retain this context or perform I/O. Custom strategies are thread-safe singleton services, optionally registered through a DI factory, with bounded state and request-path execution cost.

All nested segments and behaviors inherit the root decision without consulting the sampler again. A selected request can still have truncated or rejected capture under resource limits; sampling does not imply a complete record. A skipped request uses the same execution-local suppression mechanism as a blacklist match. It creates no operation record, segment aggregates, profiling header, or lifecycle events, even if downstream code explicitly starts an operation. Helpers still invoke business delegates once.

The decision survives error-handler re-execution, retries inside the same HTTP execution, and controller key changes. It is not revisited when an error appears later. Independent queued work starts its own operation under normal Operation Profiling rules, with no inherited HTTP sampling suppression. Manual operations, jobs, and Blazor actions outside a sampled HTTP flow are not selected by this request-only strategy.

Invalid built-in parameters fail setup validation. A custom strategy throwing or returning invalid decision data causes capture to be skipped with a sampling-error counter. It never fails the HTTP request or falls back to unexpectedly capturing all traffic. Blacklist exclusions, intentional sampling skips, strategy errors, admission rejection, and persistence loss are separate health categories.

Only entry-based strategies are included. Retaining every slow or failed request would require a decision after observing execution, with different buffering and overhead. This design does not promise that skipped requests can later be recovered, or that an arbitrary sample contains every important failure.

### 4.8 Sampling evidence and analysis

Health reports the effective strategy and settings plus evaluated, selected, intentionally skipped, and strategy-error counts per node and configuration. Evaluated decisions equal selected plus intentional skips plus errors. Blacklisted requests do not invoke the sampler or consume rate tokens. Counters have an explicit process/time scope and are not silently treated as exact counts for a dashboard's arbitrary date filter.

The Requests view labels its data as retained sampled executions whenever reduced or mixed sampling policies apply. Slow means slowest retained selected requests; By count ranks retained counts, not total traffic. Filters support strategy/configuration identifiers, and comparisons show whether policies differ. Default all-request sampling remains subject to independent overload and persistence loss.

Known inclusion probability describes selection only. It is not the probability that a record survives queueing, persistence, and retention. The dashboard does not multiply sampled counts by an inverse probability or claim that sampled percentiles describe all traffic. Rate-budget selection can favor requests arriving while tokens are available, so its observed distribution is not presented as an unbiased population estimate. Manual-operation records are labeled outside HTTP sampling rather than being assigned an invented request probability.

### 4.9 Supported HTTP middleware integration

Use an outer request observer and a separate exception observer immediately inside the host's exception handler. The following order is the reference arrangement. The host registers the services required by the optional compression, CORS, authentication, and cache middleware; Profiling does not choose the application's error responses or security policies.

```csharp
// Existing trusted-proxy/path-base normalization, if configured, precedes this.
app.UseRequestProfiling();
app.UseExceptionHandler(); // Or the host's existing /Error re-execution handler.
app.UseRequestProfilingExceptionObserver();

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCompression();
app.UseOutputCache(); // If used; response caching also belongs inside profiling.
app.UseStaticFiles(); // If used; endpoint-mapped assets are likewise inside it.
app.MapEndpoints();
```

The host omits middleware it does not use and preserves its required relative order. Explicit `UseRouting` keeps endpoint selection inside the observation boundary instead of relying on minimal-hosting automatic insertion. If the host needs CORS for static files, CORS remains before the static-file middleware. Response compression stays inside the byte observer and before response caching/output caching in this reference arrangement, so measured bytes are what that arrangement writes after compression. These ordering constraints follow the [ASP.NET Core middleware guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/middleware?view=aspnetcore-10.0).

`UseRequestProfiling` owns the root operation, response-header callback, body feature wrapper, admission counters, and one finalization state. It encloses exception handling, routing, authentication/authorization, caching, static responses, endpoints, and serialization. Infrastructure normalization deliberately outside it is not included in the duration. It catches exceptions that escape the complete downstream pipeline, records the observed failure safely, and rethrows unchanged. It does not implement its own error response.

`UseRequestProfilingExceptionObserver` invokes its next delegate once, captures the selected endpoint route before an escaping exception can be replaced by error-handler re-execution, reports that exception through `IRequestProfilingFeature`, and rethrows with the original stack. The existing outer exception handler can then produce its usual response, including a handled 4xx/200, while the profile retains the observed exception. A normal 4xx without an observed exception still follows the Completed rule in section 4.1. The observer creates no operation and does not call Runtime or storage.

The feature lives on `HttpContext.Features` and carries the original request identity, sampling/suppression decision, initial key, original route when known, and the explicit recording handle. Route capture is first-write-wins at normal response start, normal execution unwind, or exception observation. An error endpoint never replaces a known original route, and an initially unmatched request does not acquire the error endpoint's route. Re-execution and a replacement DI scope reuse that feature and the same operation ID. Duplicate observations of an exception cannot finalize the root or add duplicate segment samples.

Custom error middleware or MVC filters that consume an exception inside the observer boundary report it through the feature's safe `ReportException(Exception)` hook before handling it. The feature applies bounded sanitization and retains no exception object in the completed record. If no feature exists, that reporting is a no-op. The built-in observer covers exceptions reaching the configured outer handler; it cannot discover exceptions swallowed inside arbitrary application code. Recovered business/segment failures continue to follow the independent root/segment outcome rules.

Do not depend on exception logs, `DiagnosticListener` events, or a handler returning false for correctness. In .NET 10, framework diagnostics can be suppressed for handled exceptions; the explicit observer reports before handling and leaves the host's diagnostic configuration unchanged. See Microsoft's [handled-exception diagnostics change](https://learn.microsoft.com/aspnet/core/breaking-changes/10/exception-handler-diagnostics-suppressed?view=aspnetcore-10.0).

After downstream execution returns, restore the ambient operation context immediately; completion callbacks retain an explicit handle rather than an ambient scope. Normal `OnCompleted` closes the root after downstream response processing. Abort/exception paths coordinate fallback cleanup after execution has unwound if normal completion will not run. Token callbacks do not prematurely dispose a body wrapper while application code is still using it. Release wrapper references, registrations, and capture capacity exactly once, and never enqueue `HttpContext` or its features. A late abort after finalization cannot mutate a stored record.

The response-header callback runs after inner middleware/cache callbacks and overwrites or removes cached profiling IDs according to section 4.2. Test cache hits with a newly selected request and with a skipped/disabled request. A cached body can still produce a newly measured server request; profiling does not disable caching, buffer streamed content, force a flush, or change cache keys. CORS exposure is configured by the host. Header mutation and body observation respect `HasStarted` and never convert a response failure into a second response.

Transport-byte accounting distinguishes response attempts. Bytes staged in an application buffer and later discarded by an error handler are not counted as delivered body bytes. A failed write with unknown partial delivery marks the observation partial and does not invent an exact count. Body-feature replacement must preserve counting once or report unavailable coverage. Finalization cannot promote an incomplete byte observation to complete merely because a `Content-Length` header exists.

The two profiling middleware extensions tolerate unavailable/disabled profiling and preserve normal execution; only the minimal header cleanup described above remains where the outer adapter is installed. The exception observer must sit after the handler it is intended to observe for and before the protected work. Hosts using a developer exception page use the same arrangement. Integration tests cover this complete pipeline, not just an isolated delegate that throws.

## 5. Developer instrumentation

Examples below use the specified `IOperationProfiler profiling` façade. Application types and services illustrate a consuming application. Helpers have value-returning and non-value-returning, synchronous and asynchronous forms.

### 5.1 Enrich an HTTP operation and record segments

Controllers and minimal API handlers inject `IOperationProfiler` and enrich the operation already started by middleware. They do not call `BeginOperation` for that same HTTP execution. These are separate endpoint examples using `Microsoft.AspNetCore.Mvc` binding attributes:

```csharp
app.MapGet("/products", (
	[FromQuery] string category,
	[FromServices] IOperationProfiler profiling) =>
{
	profiling.SetKey("Products.List");
	profiling.SetDimension("category", category);
	return Results.Ok();
});
```

```csharp
[ApiController]
[Route("products")]
public sealed class ProductsController(IOperationProfiler profiling) : ControllerBase
{
	private readonly IOperationProfiler profiling = profiling;

	[HttpGet]
	public IActionResult List([FromQuery] string category)
	{
		this.profiling.SetKey("Products.List");
		this.profiling.SetDimension("category", category);
		return this.Ok();
	}
}
```

These examples require `AddProfiling` registration. Disabled or sampling-skipped capture makes enrichment a no-op. A component that also supports omitted registration uses optional constructor injection and a null guard as in section 6.7; a minimal API parameter for that case uses `[FromServices] IOperationProfiler profiling = null` and guards enrichment. Registration absence is not inferred as a body-binding parameter. See [ASP.NET Core parameter binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0#parameter-binding-with-dependency-injection).

Execution timestamps are already recorded; the dimensions below describe the workload:

```csharp
profiling.SetKey("/calculations");
profiling.SetDimension("productType", query.ProductType.ToString());
profiling.SetDimension("resolution", query.Resolution);

var inputs = await profiling.RunSegmentAsync(
	"Load",
	async (segment, ct) =>
	{
		var loaded = await loader.LoadAsync(query, ct);
		segment.SetMeasurement("records.loaded", loaded.Count, "count", MeasurementAggregation.Sum);
		return loaded;
	},
	cancellationToken);

var transformed = profiling.RunSegment(
	"Transform",
	segment =>
	{
		var output = transformer.Transform(inputs);
		segment.SetMeasurement("records.output", output.Count, "count", MeasurementAggregation.Sum);
		return output;
	});

var result = profiling.RunSegment(
	"Calculate",
	_ => calculator.Calculate(transformed));
```

`SetKey`, `SetDimension`, and `SetMeasurement` on the façade target the current operation. They safely do nothing when no operation is active. Segment enrichment is explicit on the segment handle. This distinction prevents a dependency from accidentally changing operation dimensions when it intends to annotate a segment.

### 5.2 Start a non-HTTP operation

A Blazor Server event handler starts one operation for its action:

```csharp
await profiling.RunOperationAsync(
	"Recalculate",
	OperationProfilingKind.BlazorInteraction,
	async (operation, ct) =>
	{
		operation.SetDimension("productType", selectedProduct);

		var result = await operation.RunSegmentAsync(
			"LoadAndCalculate",
			(_, segmentToken) => calculationService.CalculateAsync(segmentToken),
			ct);

		ApplyResult(result);
	},
	cancellationToken);
```

The operation begins on each invocation, even when the injected profiler is shared by circuit services. Services reached during the action can add segments through the façade. An independently invoked service can use the same helper with kind `Service`. Host-specific cancellation tokens remain owned by the application.

### 5.3 Manual operation and segment scopes

Manual scopes are useful when a caller needs the ID before completion or maps a domain result explicitly:

```csharp
using var operation = profiling.BeginOperation(
	"RefreshReferenceData",
	OperationProfilingKind.Service);

var operationId = operation.Id;
operation.SetDimension("source", "reference-data");

try
{
	await operation.RunSegmentAsync(
		"ReadInputs",
		(_, ct) => referenceData.RefreshAsync(ct),
		cancellationToken);

	operation.Complete();
}
catch (OperationCanceledException exception) when (
	exception.CancellationToken == cancellationToken && cancellationToken.IsCancellationRequested)
{
	operation.Cancel();
	throw;
}
catch (Exception exception)
{
	operation.Fail(exception);
	throw;
}
```

This example manages the outer operation manually and uses a helper for the inner segment. If `ReadInputs` throws, the helper classifies its invocation and rethrows; the outer catch classifies the operation. Disposal closes both once. Fully manual segments use the same explicit `Complete`, `Fail`, and `Cancel` pattern. An unmarked manual segment is incomplete, even if its parent observed an exception.

### 5.4 Nested and parallel dependency work

The same segment contract measures different dependencies:

```csharp
var loaded = await profiling.RunSegmentAsync(
	"Load",
	async (load, ct) =>
	{
		var sqlTask = load.RunSegmentAsync(
			"ReadSettings",
			async (read, readToken) =>
			{
				read.SetDimension("dependency", "sql");
				var rows = await repository.ReadAsync(readToken);
				read.SetMeasurement("rows.returned", rows.Count, "count", MeasurementAggregation.Sum);
				return rows;
			},
			ct);

		var documentsTask = load.RunSegmentAsync(
			"ReadDocuments",
			async (read, readToken) =>
			{
				read.SetDimension("dependency", "file-storage");
				var documents = await documentStore.ReadAsync(readToken);
				read.SetMeasurement("bytes.read", documents.BytesRead, "bytes", MeasurementAggregation.Sum);
				return documents;
			},
			ct);

		await Task.WhenAll(sqlTask, documentsTask);
		return Combine(await sqlTask, await documentsTask);
	},
	cancellationToken);
```

Both children explicitly belong to `Load`. They contribute to separate paths, `Load / ReadSettings` and `Load / ReadDocuments`, even when called repeatedly. Their overlap affects child coverage and self time without requiring persisted invocation events. Generic measurements can include files, rows, bytes, retries, cache hits, and cache misses. Values come from observations already available to the application. The profiler does not scan inputs, inspect SQL, or create a segment per item by default.

When useful, application instrumentation separates waiting for a limiter or dependency from execution using different segment keys. Elapsed time then identifies the visible waiting boundary without claiming its underlying CPU or network cause.

### 5.5 Recover from a failed segment

Application code can recover while preserving the inner failure in its summary:

```csharp
var result = await profiling.RunOperationAsync(
	"ReadReferenceData",
	OperationProfilingKind.Service,
	async (operation, ct) =>
	{
		try
		{
			return await operation.RunSegmentAsync(
				"ReadRemote",
				(_, segmentToken) => remoteSource.ReadAsync(segmentToken),
				ct);
		}
		catch (RemoteReadException)
		{
			return await operation.RunSegmentAsync(
				"ReadFallback",
				(_, segmentToken) => fallbackSource.ReadAsync(segmentToken),
				ct);
		}
	},
	cancellationToken);
```

`RemoteReadException` represents an application-defined recoverable error. If the fallback succeeds, the operation is completed, `ReadRemote` has one failed invocation, and `ReadFallback` has one completed invocation. If the fallback also fails, its failure propagates and the operation is failed. Profiling adds no fallback policy of its own.

## 6. Devkit feature participation

### 6.1 Join or start behavior contract

Feature behaviors wrap the work already exposed by a feature's behavior pipeline. An absent profiler directly passes execution to the next delegate. A registered but disabled profiler, or a suppressed execution, also passes through without capture. When recording is available, behaviors use a shared join-or-start recording helper with these rules:

1. If the same execution has a live operation, create a segment beneath its current segment or operation root.
2. If no operation exists at this execution boundary, start and own an operation with the adapter's kind and stable key.
3. Invoke the next business delegate exactly once and return its result unchanged.
4. Classify exceptions, cancellation, and supported domain result outcomes, then close only the segment or operation the behavior owns.

The behavior does not close an operation started by middleware or an outer caller. Independent worker boundaries clear inherited context before these rules apply. A behavior never joins a stale request operation because a task happened to inherit execution context.

Registration uses each feature's existing behavior registration mechanism and prevents duplicate registration at the same boundary. Behaviors are optional integrations. Automatic HTTP capture follows the adapter's blacklist and sampling policy. Behaviors honor execution-local suppression, including HTTP blacklist exclusions and sampling skips, before applying their join-or-start rule. Recording failure or absence of profiling services never disables a feature's execution.

For Pipelines the registration method is `AddBehavior`; Jobs and Orchestrations use their own existing behavior registration APIs. The common profiling contract does not replace those feature APIs. Enabled `WithOperationProfiling` makes the recorder available, while feature behavior registration selects which execution boundaries contribute automatically.

| Feature boundary | With no active operation | Within an existing operation |
| --- | --- | --- |
| Job execution | Own a `Job` operation. | Add a job segment. |
| Pipeline run | Own a `Pipeline` operation; steps are its segments. | Add a pipeline segment; steps are nested segments. |
| Orchestration execution slice | Own an `Orchestration` operation; actions are its segments. | Add an orchestration segment; actions are nested segments. |

All entries require an available, enabled recorder and honor capture suppression before creating scopes. Optional dependency resolution follows section 6.7. The term diagnostic session in feature usage refers to this operation scope; it does not require a Runtime Profiling session.

### 6.2 Jobs

`JobProfilingBehavior` implements the existing `IJobBehavior` contract. It measures the invocation represented by that contract and uses stable job-definition identity for its key. Job execution ID, trigger kind, correlation ID, and attempt information are bounded metadata rather than unique grouping keys.

An independent job invocation owns a `Job` operation. A job invoked inline within a profiled operation contributes a segment invocation, aggregated by path with other invocations of that job. Time waiting in a scheduler queue is separate metadata or a separately instrumented segment where the scheduler exposes that boundary. It is not silently included in measured execution time.

The behavior maps the actual job result, including failed, canceled, timed-out, or interrupted results that do not throw. Timeout is a failed outcome with a timeout classification. Retry status and attempt information remain visible. The profiler adds no retry or scheduling behavior.

Behavior ordering determines whether a boundary includes an entire retry policy or one attempt. The registration and displayed metadata identify that boundary. Separate attempt segments exist only when the feature exposes each attempt; the profiler does not infer attempts from total duration or duplicate one invocation record.

### 6.3 Pipelines and step segments

`PipelineProfilingBehavior` implements the Pipelines feature's `IPipelineBehavior<PipelineContextBase>`. This is the workflow-style Pipelines feature, not the Requester behavior interface with a similar name. Register it through the pipeline definition's `.AddBehavior<PipelineProfilingBehavior>()` extension point.

The existing behavior contract supplies both required boundaries:

- `ExecuteAsync` wraps the pipeline execution. It starts an operation of kind `Pipeline` when no operation exists, or adds a pipeline segment beneath the current segment/operation.
- `ExecuteStepAsync` wraps each executed step attempt. It always contributes a child segment to that pipeline boundary rather than starting an operation per step.

Use `pipeline:<definition-name>` as the outer key and `step:<step-name>` as each step's key. Definition and step names are stable keys; execution ID and correlation ID are metadata. Never retain the pipeline context, property bag, or carried `Result` object. Individual safe result codes or counts can be extracted under the existing metadata limits.

Repeated steps and retries at the same path increase the existing summary's invocation count, duration statistics, and outcome counts. A nested pipeline creates another pipeline segment inside the calling step, with its steps below it. Concurrent pipeline invocations retain separate live handles and safely aggregate matching paths in their common operation.

Only executed step attempts contribute timed segments. A step bypassed before invocation has no fabricated zero-duration segment. A step that executes and returns `PipelineControl.Skip` has a measured invocation because code ran. `Continue`, `Skip`, `Retry`, `Break`, and `Terminate` remain flow-control metadata. Their names alone do not imply failure. Classification observes the carried result, exceptions, and cancellation, with the overall pipeline outcome based on its actual final result.

Returned-result failure counts describe observed step outcomes, including a failure carried from earlier work; they do not claim to identify unique error origins. Retry attempts are separate invocations in the same summary. Retry delays outside the step delegate are included in outer execution time, not silently credited to step execution.

The behavior invokes each `next` delegate once and preserves `ValueTask`, result, cancellation, and exception behavior. It does not alter hooks, retry policy, execution tracking, or progression through steps. Existing tracing/timing behaviors can coexist, but this profiling behavior is registered only once per boundary and owns its own recording scopes.

### 6.4 Orchestration actions and execution scopes

An orchestration action is represented by an activity in the current devkit contracts. `OrchestrationProfilingBehavior` implements `IOrchestrationBehavior` around each activity attempt, so regular actions, signal activities, and compensation actions each contribute a segment. This mapping preserves the feature's API vocabulary without creating another action abstraction.

The orchestration integration also establishes an outer scope at the executor's bounded execution boundary. With no active operation, it owns an operation of kind `Orchestration`; otherwise it adds an orchestration segment beneath the current operation/segment. Action behaviors join that scope and do not create unrelated roots for each action in the same execution slice.

`IOrchestrationBehavior` currently wraps individual activities, not the executor's entire execution. The profiling integration therefore includes a small companion execution-scope adapter at the executor boundary. Both parts are enabled together for profiled orchestrations, with one owner responsible for closing the outer scope. Both also tolerate omitted profiling registration: neither the executor nor the action behavior requires profiling services to resolve or run. When recording is available, an independently invoked action outside that boundary can fall back to a bounded orchestration operation containing its action segment.

Use `orchestration:<definition-name>` for the outer key. Action keys include stable state, activity name, and activity kind so identically named work in different states or compensation paths stays distinguishable. Instance ID, correlation ID, and attempt number are bounded metadata. Service providers and orchestration context objects are never stored.

Repeated actions and retries within a slice aggregate by key path. The same action across later slices has a summary in each operation and can be compared through definition key, action path, and correlation metadata. Attempt-varying metadata is marked mixed rather than implying that every invocation used the same attempt number.

The owned outer operation, or wrapper segment within a caller's operation, measures an actual execution slice rather than a durable workflow's full elapsed lifetime. That scope closes when execution completes, fails, cancels, waits, or pauses; the caller's operation remains owned by its caller. Waiting for a signal, scheduled delays between executions, and process restarts do not keep recording scopes open. A resumed execution starts a new correlated operation unless it joins an already active caller operation. Its records never mutate a previous slice.

Domain outcomes remain visible as metadata. `Continue`, `Complete`, and `Wait` can represent successfully executed action code. `Wait` closes the current action measurement normally with that outcome recorded, and the outer slice ends when the executor yields. `Retry` records the attempt's domain outcome, while an observed exception or failed result remains failed. Cancellation maps to `Canceled`; termination includes its reason and the feature's success or failure classification.

### 6.5 Example grouping

An HTTP operation that invokes a pipeline and an orchestration can contain these segment paths:

```text
Operation: /imports
  pipeline:order-import
    step:Validate
    step:Load
      ReadInputs
    step:Persist
  orchestration:OrderApproval
    action:Authorize.ReservePayment.Activity
    action:Complete.SendConfirmation.Activity
```

This is the summary hierarchy, not execution order. Ten calls to `ReadInputs` inside repeated `step:Load` invocations update the same path summary. In a standalone pipeline or orchestration operation, the operation's key identifies the outer feature and step/action paths start directly beneath it. In a request, those segments appear below the corresponding feature wrapper segment.

### 6.6 Other feature adapters

Requester, messaging, queueing, repository, and storage behaviors can follow the same contract through their existing extension points. Their inclusion does not require core model changes. Jobs, Pipelines, and Orchestrations are the concrete built-in behavior integrations in this specification. Other features can add adapters without introducing another profiling subsystem.

Adapters use stable keys, bounded metadata, and result classification appropriate to their feature. They do not automatically serialize arguments or return values. Instrumentation of the profiling writer, query internals, or its own storage dependencies is suppressed to avoid recursion. The outer HTTP operation for a dashboard API follows the same blacklist and request-sampling policy as other HTTP requests.

### 6.7 Optional injection and pass-through

Built-in profiling behaviors depend on the public `IOperationProfiler` façade as an optional injected dependency. Its absence is expected when the host omits `AddProfiling`; it is not an error or a reason to skip business execution. Jobs, Pipelines, Orchestrations, and future feature integrations follow this same contract, including the orchestration companion execution-scope adapter.

For example, the pipeline behavior's constructor accepts an optional parameter. This excerpt follows the repository's disabled nullable-annotation convention:

```csharp
private readonly IOperationProfiler profiling;

public PipelineProfilingBehavior(IOperationProfiler profiling = null)
{
	this.profiling = profiling;
}
```

Each execution entry point handles absence before preparing keys, dimensions, timers, or recording scopes:

```csharp
if (this.profiling is null)
{
	return next();
}
```

This guard applies to both pipeline entry points (`ExecuteAsync` and `ExecuteStepAsync`) and the equivalent job/action/execution-scope boundaries. The remaining recording branch follows section 6.1. The absent path returns the original `Task` or `ValueTask` where the contract permits and invokes `next` exactly once. It preserves the returned result, exception, and cancellation behavior, including synchronous exceptions and failed domain results. It does not create a profiling context, emit profiling lifecycle logs or warnings, increment profiling counters, or perform storage work.

Feature registration must preserve this optional dependency through every construction path. Constructors and activation helpers honor the default parameter; explicit DI factories use optional resolution (`GetService<IOperationProfiler>()`) instead of `GetRequiredService`. Behaviors do not require profiling options, providers, queues, writers, samplers, or profiling loggers as additional injected services. The façade owns those dependencies when profiling is registered. A shared recording helper must not introduce a mandatory service that prevents constructing an otherwise optional integration.

Feature setup registers its behaviors and adapters only. It does not install fallback profiling services, create a separate service provider, or resolve a profiler on every invocation to work around missing registration. If `AddProfiling` is present but Operations is disabled, its injected façade supplies the no-recording behavior; if capture is enabled but the execution is suppressed, the same façade preserves suppression without starting a replacement operation. Intentional absence, disabled capture, and suppression are all silent configurations.

Optional means an unregistered profiler can be absent. An explicitly registered profiler whose factory or dependencies fail to resolve remains a setup error; integrations must not catch DI construction errors and disguise them as omitted registration. Runtime recording failures remain isolated from business execution under the failure-handling contract.

## 7. Runtime correlation and timing analysis

### 7.1 Shared identity and time

Both datasets use the same cached `ProfilingNode` identity for a process lifetime. `IProfilingNodeIdentityProvider` creates one local GUID and readable `NodeKey` without I/O, caches process-start UTC and process ID, and supplies bounded application/host display metadata. The node display name defaults to the host name and can be configured through shared Profiling options. Display names are not unique identities; two processes on the same host have different node IDs, and a restart always receives a new one.

Every operation carries its executing `NodeId` and `NodeKey`. Lists show the node display name/key, details show host, process ID, and process-start UTC, and filtering/grouping can select the node ID. Segment summaries inherit their owning operation's node. An operation is local to one process; independent work on another node gets its own operation and can carry a correlation ID, without introducing a distributed operation trace.

Node persistence is performed by background writer registration or the existing runtime control path, never by `BeginOperation` or a segment call. Registration upserts the cached identity idempotently before accepting dependent records. Database unavailability does not prevent application startup or business execution; recording/persistence availability and any loss follow the bounded writer rules in section 8.7. Startup validation errors such as invalid options or a broken DI graph still fail normally.

Runtime keeps its current Broadcast registry, targeting, and control behavior. The Runtime adapter associates its Broadcast process registration with the already cached profiling node. Attaching that private correlation must reuse the existing `NodeId`/`NodeKey`, including when Operations persisted the node first. It does not create a second profiling node or expose Broadcast registration values in the dashboard. Operation-only hosts need no Broadcast registration, runtime session, or runtime collector. A Runtime-only host initializes the same local identity through Runtime setup.

The core keeps a process-local view of active runtime collection. An operation may record `RuntimeSessionIdAtStart` and `RuntimeSessionKeyAtStart` when that node participates in a session. These are navigation hints, not ownership or a required foreign key.

The authoritative relationship is the same node plus overlapping observed intervals. An operation may begin before a session, span several sessions, or have no runtime evidence. Correlation uses each node's actual collection window, including partial participation and manual stops.

All dates are UTC. Durations and segment offsets use a monotonic clock. Same-process timing offsets support correlation when available. Clock discontinuities are visible limitations rather than negative elapsed durations or fabricated sample coverage.

### 7.2 Overlay contract

Operation details show the segment-summary table alongside runtime charts for the operation's UTC interval. CPU, memory, allocation, and GC charts retain their own units and axes. The operation's start and end highlight the runtime interval being inspected.

Segment summaries do not retain when every invocation occurred. The UI therefore cannot overlay an aggregated segment as one continuous interval or reconstruct its order from totals. Runtime overlay applies to the whole operation or a user-selected time range. Exact segment-to-snapshot attribution would require additional interval capture and is outside this specification.

For an operation interval `[start, end)`, queries return matching runtime sessions and samples inside the interval. Nearest samples before and after it can be returned as explicitly labeled surrounding context. Interval-derived metrics describe their actual sample interval, which can extend beyond a short operation. A zero-duration operation is treated as a timestamped observation. Reverse overlap queries include operations that start before the selected range but finish within or after it.

Built-in Runtime store facets implement `IRuntimeProfilingCorrelationStore.QueryCorrelationAsync` for bounded provider-side selection. The selector requires the node GUID/key, hostname, PID, and exact cached process-start UTC; names alone never identify a process. It reads actual participation windows plus interval samples and nearest surrounding observations, bounding session/participation metadata as well as snapshots. EF indexes lossless UTC tick projections for snapshot and participation intervals; it does not deserialize entire session histories to perform correlation. A custom Runtime facet without this optional capability returns explicit unavailable correlation instead of an unbounded fallback scan.

The overlay reports observed coverage, sampling gaps, and unavailable evidence. It does not stretch a nearby snapshot across a gap or attribute a process spike to the selected operation. Runtime may be idle while operation capture remains enabled.

The Runtime view can open Operations or Requests filtered to the selected node and interval. The Operations view can open Runtime with its interval highlighted. This overlay and navigation provide the combined view; a third standalone dashboard is not required.

Correlation is non-owning. Runtime retention or deletion does not delete operations, and operation retention does not delete runtime evidence. Imported archives with remapped identities do not acquire links to local operations merely because hostnames or dates match. Shared node retention accounts for references from both datasets.

### 7.3 Analysis rules

Operation latency analysis uses kind, stable key, selected dimensions, and compatible measurement units. It reports sample count, median, p95, and maximum. Median uses the midpoint of the two central values for an even count. P95 uses nearest rank. Percentiles are calculated from retained operation observations, never averaged across group percentiles.

Segment analysis groups summaries by full key path and compatible metadata. It shows both the number of owning operations and the number of segment invocations. Total duration and invocation counts add across owners. The invocation mean is summed duration divided by summed count, not an average of per-operation averages. Minima and maxima combine directly, and measurement reducers preserve their declared semantics.

Median and p95 of a segment's per-operation total or self duration can be computed from the retained summaries. They are labeled per operation. Exact invocation-level percentiles cannot be reconstructed from count, sum, minimum, and maximum; the API does not invent them. Invocation histograms and raw traces are outside this scope.

Latency analysis defaults to completed operations and displays its outcome filter. Segment timing can additionally select invocation outcomes using the retained outcome buckets. Failed, canceled, aborted, and incomplete root records remain selectable. Cumulative segment time can exceed root duration, especially across siblings or parallel invocations. It is not used as an additive percentage of request duration.

The recorder maintains top-level wall-time coverage separately from cumulative segment statistics. Time outside observed segments and parallel time are captured as described in section 9.4. Neither is labeled CPU time or framework overhead. Truncation makes the breakdown partial. Workload measurements and concurrency at entry can be compared with latency, with a reminder that entry concurrency is one observation rather than a concurrency history.

Two labeled filter selections support comparison without a stress-test entity. The service does not claim they represent identical workloads. Exact distributions and comparison selections are bounded to 10,000 matched operations each by default. Above the bound, return a clear limit result requiring narrower filters. Never use the first page as an unlabeled substitute for the full selection.

## 8. Storage, batching, and resource limits

### 8.1 Persistence lifecycle

The write path for every operation kind is live bounded recording, immutable completed record, bounded queue, periodic writer, and provider batch append. Both in-memory and EF providers follow this lifecycle. Completed records become queryable after a successful flush. There is no separate durable live-operation store.

Enqueue is nonblocking and rejects the incoming record when capacity is exhausted. Its result accurately reports acceptance. A channel that silently drops an item while returning success does not meet this contract. Admission counters, retained payloads, queued data, and in-flight batches all have explicit bounds.

One writer per process wakes on the configured periodic tick. At that tick it captures the queue's current completion-sequence watermark and drains consecutive batches up to that watermark, stopping when the queue is empty, the batch-count budget is reached, or the flush-time budget has elapsed. It checks the time budget before starting each additional batch; an already running provider call is allowed to finish within its own timeout. Arrivals after the captured watermark wait for a later tick, so sustained traffic cannot keep one flush running forever.

Writer synchronization and pending-clear acknowledgements run before batch selection, including on empty ticks. A tick's cutoff includes its active lease identity as well as sequence, because a replacement lease starts a new sequence domain. Retiring a lease discards its unwriteable envelopes before activating a replacement; it never mixes two leases under one numeric queue watermark. Successful or terminal per-record outcomes are settled before retry selection. Only retryable or unknown outcomes remain in the bounded retry set; successful members of a partially accepted batch are not re-added.

Writes and ticks never overlap. Missed ticks are coalesced, with no backlog of scheduled flush tasks or immediate loop that drains continuously. Record count or queue pressure never triggers a flush from application code. A periodic wakeup with no records performs no append. Backlog can delay visibility beyond one flush interval. Writer cancellation is independent of the original request, job, or action token.

The defaults are a 1-second interval, up to 512 records/4 MiB per batch, up to 8 batches per tick, and a 250 ms budget for starting further batches. The count-based scheduling ceiling is therefore 4,096 small records per second per process, not a guaranteed database throughput. Byte limits, provider latency, retries, and the time budget can lower it. Multiple bounded batches permit useful stress-test capture while keeping database work configurable.

The EF writer creates a scope and context per attempt. Each operation and its retained segment summaries are atomic. Stable operation IDs and transactional idempotency handle retries after an uncertain commit, including batches containing both existing and new IDs. Retrying persistence does not add an already stored summary's counts again. Exhausted or nontransient failures are recorded as diagnostic loss.

A write attempt has a 5-second timeout by default. A retry retains the same batch identities, writer lease, and completion sequences. Backoff does not bypass the tick budgets: a retry becoming eligible after the current budget waits for a later tick. Every append attempt, including a retry, consumes a batch-start slot and its time counts toward the flush budget. A provider that does not honor cancellation leaves the writer unhealthy and prevents overlapping replacement calls; capture stays nonblocking and bounded.

Cancellation/timeout with an uncertain commit is resolved through stable-ID idempotency before reporting a confirmed persistence or loss outcome. If the retry budget is exhausted without resolving a returned attempt, permanently stop replaying those records and classify their persistence outcome as unknown, not confirmed loss. The provider's atomic settled-watermark advance then fences any delayed write for those sequences: an earlier committed root remains stored, while a later attempt cannot insert it. No watermark advances past a still-running provider call. This settlement shares the append/retention synchronization contract in section 8.7 and does not require an unbounded retry loop or an extra record per uncertain attempt.

The hosted writer contains recoverable persistence exceptions so a diagnostic batch failure does not escape its worker and stop the application host. After a failed batch exhausts its policy, it releases that batch's capacity and continues on a later tick. A corrupt writer state disables affected capture and exposes unhealthy status instead of entering a busy retry loop or leaking queued state. Worker shutdown cancellation follows the bounded drain policy and does not masquerade as a failed business operation.

Queued records contain values only. They retain no `HttpContext`, application objects, scoped services, or tracked entities. The writer suppresses its own instrumentation and rate-limits error logging. On shutdown it attempts a bounded drain; process termination can still lose buffered data.

### 8.2 Provider and query behavior

`IProfilingStorageProvider` is the extension boundary for all profiling storage. The built-in implementations are `InMemoryProfilingStorageProvider` and `EntityFrameworkProfilingStorageProvider<TContext>`. Additional packages implement the same contract and register through `WithProvider<TProvider>()`; core services and dashboard components contain no switch on a hardcoded list of backend types.

The provider composes focused runtime and operation store contracts. Core services consume the smallest applicable contract, and DI maps both facets to the same selected provider. The contract owns these persistence responsibilities:

| Responsibility | Provider behavior |
| --- | --- |
| Shared identity and runtime lifecycle | Resolve shared node metadata and perform atomic runtime lifecycle transitions. |
| Operation batch append | Persist immutable operations and their summaries atomically per operation, with stable-ID idempotency and a defined batch result. |
| Queries and analysis inputs | Apply typed filters, ordering, paging, group counts, and bounded analysis requests; return records and DTOs independent of the backend. |
| Retention and clearing | Apply bounded retention and implement `ClearAsync` for all history or a UTC range in selected datasets. Coordinate clear generations so buffered records cannot resurrect deleted history. |
| Maintenance recovery | Resume sealed clears, compact safe fences, and apply bounded retention through `ResumeMaintenanceAsync`, invoked by the shared background maintenance worker. |
| Capabilities and health | Report provider identity, node-local versus shared scope, supported optional functions, and query consistency. |
| Failures and cancellation | Return typed failures, including transient classification, and honor worker/query cancellation without changing application execution. |

`ProfilingMaintenanceResult.RetentionRemovedOperations` counts retention deletion work performed by that maintenance call. `CapacityEvictedOperations` reports append-time capacity evictions since the previous successful maintenance observation; it is reset once reported and does not consume the current call's root work budget. The node-local `RetentionRemovals` health counter includes both values. An accepted incoming record can evict older retained history without constituting an incoming capture/persistence loss.

The shared provider contract exposes no `DbContext`, `DbSet`, `IQueryable`, SQL, or backend-specific client. EF mapping and query translation belong to the EF provider. The in-memory provider applies the same observable filter, grouping, time, outcome, retention, and idempotency rules. Performance characteristics and supported read consistency may differ and are reported explicitly.

Provider selection is a setup decision. There is exactly one selected provider for Runtime and Operation Profiling. An explicit choice replaces the implicit in-memory default; conflicting explicit choices fail startup validation. Repeating the same registration is idempotent. There is no implicit write fan-out, runtime provider switching, or migration between providers.

The provider façade is thread-safe and registered with a lifetime suitable for concurrent writer, runtime, and query calls. The EF implementation uses independent scoped contexts internally and never captures a scoped `DbContext` in the singleton provider. Custom providers follow the same lifetime boundary. Factories may be supplied through the registration extension when a custom provider needs additional host services.

The bounded queue, periodic scheduling, and operation capture remain in the shared core. Every provider receives operation writes through this same batch lifecycle. A provider replacement does not enable application-path writes or change flush semantics. Runtime control writes retain their separate lifecycle guarantees. Retry policies have a bounded documented budget; provider-specific retries must not multiply the writer's retries without accounting for them.

All providers satisfy the required storage semantics and shared conformance tests. Capabilities describe optional differences such as shared-node access and snapshot-consistent queries. An unsupported optional function returns an explicit unsupported result. The UI does not infer capabilities from the provider's class name or display name.

Example alternatives, each selecting one provider for the complete feature, are:

```csharp
// Process-local storage.
profilingBuilder.WithInMemoryProvider();

// Durable storage through the application's EF context.
profilingBuilder.WithEntityFrameworkProvider<AppDbContext>();

// Host-supplied implementation of IProfilingStorageProvider.
profilingBuilder.WithProvider<CustomProfilingStorageProvider>();
```

These are separate setup alternatives, not calls to combine in one registration chain.

The selected provider owns shared nodes, runtime records, and independently queryable operation records. High-volume operations are not embedded in runtime-session JSON. Segment summaries are uniquely addressed by operation and structured key path, with an optional internal row ID. Persistence validates node identity and parent paths. No row is inserted for every loop iteration.

The EF context contract is deliberately small: `IProfilingDbContext` requires only `DbSet<TEntity> Set<TEntity>() where TEntity : class`, which the application's inherited `DbContext.Set<TEntity>()` already supplies. Hosts implement the interface and call `ConfigureProfiling()`; they need no profiling-specific `DbSet` properties. The provider opens independent contexts per operation and accesses configured sets through this generic contract.

EF stores the complete bounded immutable operation in `RecordJson` on its root and each segment summary in `SummaryJson` on its indexed segment projection. Operation and segment measurements, reducer values, outcome buckets, failure summaries, wall-time buckets, capture quality, and non-filtered adapter/source details stay in those parent JSON payloads. There is no separate operation-measurement table, collection navigation, or row per measurement. JSON serialization preserves typed values, UTC timestamps, sample counts, and reducer quality across SQLite, SQL Server, and PostgreSQL; reads validate and freeze the bounded graph before returning it.

Relational segment projections retain owner/path and timing/outcome fields needed for filtering. Typed dimension projections remain relational because equality/presence/missing/mixed filters and grouping must execute in storage without scanning or deserializing all history. Root HTTP/filter fields remain indexed projections. Shared nodes, independently addressable Runtime snapshots/participations/metric observations, and writer/clear/gate coordination retain their existing relational boundaries because they require independent queries or transactional concurrency. Parent JSON is not queried as an unbounded substitute for those projections. Payload and projections are saved and removed atomically; app-owned migrations explicitly remove the redundant measurement table when upgrading this unused feature.

Typed metadata tables or equivalent portable projections support dimension filters without deserializing every retained record. HTTP fields required for request filtering have efficient adapter projections. Relevant indexes cover operation ID, node and start time, kind/key and start time, completion time, typed dimension lookups, and segment summary owner/path/parent path.

EF query behavior covers the repository's SQLite, SQL Server, and PostgreSQL providers. Queries have validated page sizes, group limits, time windows, cancellation, and execution bounds. Count and ranking queries aggregate in storage. Statistical analysis respects the separate selection limit. Provider query tests verify translation and representative plans rather than relying on in-memory LINQ behavior.

Runtime control transitions retain their consistency guarantees and do not wait behind the operation queue. Shared provider selection does not imply one global lock or one transaction stream for all profiling work. Query contexts are independent from writer contexts.

#### Query boundaries and resource limits

Operation list and analysis filters use completion UTC in a half-open `[FromUtc, ToUtc)` interval. Reverse Runtime navigation explicitly selects interval overlap instead. Filter keys use the canonical comparer; dimension filters require a type and value. Supported dimension operators are equality, present, missing, and the separately defined mixed-summary selection. Free-form expressions, regular expressions, and arbitrary database sorting are not query contracts. Multiple filters combine with AND; repeated values within a supported multi-select field combine with OR. Segment predicates for one selected path must match that same summary, rather than different summaries on the same operation.

Both built-in providers support bounded, stable operation paging. A query boundary contains the store epoch, a provider commit watermark, a deletion revision, the evaluated UTC window, and normalized filters/grouping/order. The commit watermark is assigned with publication visibility: no later commit becomes visible below an already issued watermark. Each stored operation is immutable and carries its commit position. Subsequent pages exclude records above that watermark, even if their execution completion time is old. Ordering uses the requested supported sort plus the canonical GUID representation as a deterministic tie-breaker, never a backend-specific GUID ordering.

The cursor includes the boundary, last ordered key, and a five-minute expiry. It is opaque, validated, and bound to its query; it is not an authorization token or a retained database transaction. Every request rechecks authorization. Any deletion revision change, store-epoch change, expiry, or mismatched filters produces an explicit expired/invalid-boundary result instead of skipping or duplicating rows. Clear and retention deletion increment the revision atomically with root removal. The provider checks for a racing deletion before returning a response. A refresh starts a fresh boundary; a user paging through an expired view is asked to refresh.

Rows, selected group counts, and their chart data in a content response use one boundary. Counts cover the full matching retained selection below its watermark. Built-in providers return these values consistently or return a boundary-expired result; they do not silently mix snapshots. Custom providers unable to support stable paging must report that capability as unsupported rather than fabricate a cursor. Runtime's existing query behavior remains separate.

Default query execution timeout is five seconds, with at most four concurrent profiling queries per serving process. Capacity is acquired without waiting; excess dashboard queries return Busy and preserve their previous displayed data. A view-model build is one query admission, including its subordinate group/overlay work, to avoid nested admission deadlocks. Exact lookup bypasses list-window/outcome filters, but not authorization or provider scope. Lists accept at most eight general dimension predicates and four selected grouping dimensions. Expanding a previously grouped bucket carries at most four additional exact/missing `GroupDimensions` selectors, separately bounded; original general and segment predicates remain in force. Missing grouped values select absence instead of all values. Both providers apply these selectors before ranking/counts, and cursor fingerprints include them. Overlay queries return at most 2,000 snapshots; larger selections require a narrower window and are never silently truncated or interpolated. Provider access honors cancellation and server-side bounds.

### 8.3 Defaults and retention

The following defaults are configurable and form the specified baseline. Performance validation reports their cost and supports any subsequent adjustment with measurements.

| Option | Default |
| --- | --- |
| A subfeature when first registered through its `With...Profiling` method | Enabled, subject to the master flag and validated dependencies |
| An omitted subfeature | Disabled |
| Request sampling strategy | `AllRequests` |
| Observe request-body bytes | False; declared length only unless normal-read observation is explicitly enabled |
| Probability/rate parameters | Required only when selecting the corresponding alternative strategy |
| Request path prefix to strip | Empty |
| Request blacklist | `/_bdk/**`, `/health*`, `/swagger/**`, `/scalar/**`, `/openapi/**`; maximum 128 patterns of 256 characters each |
| Operation and segment lifecycle log level | Verbose; Trace through `Microsoft.Extensions.Logging` |
| Maximum admitted active operation contexts | 1,024 |
| Maximum operation recording duration / cleanup interval | 15 minutes / 5 seconds; capture expiry never cancels business execution |
| Maximum distinct segment key paths per operation | 128 |
| Maximum simultaneously active segment scopes per operation | 256 |
| Maximum segment nesting depth | 32 |
| Maximum dimensions, measurements, or source metadata fields per applicable scope | 32 each |
| Maximum key, kind, or display-name length | 128 characters |
| Maximum string metadata value | 256 characters |
| Maximum measurement unit length | 32 characters |
| Maximum safe error message | 1,024 characters |
| Maximum segment failure categories per path | 8, plus an `Other` count |
| Maximum charged payload per operation | 64 KiB |
| Maximum aggregate active-recording payload | 32 MiB |
| Queue and in-flight capacity | 8,192 records and 64 MiB charged payload |
| Flush interval | 1 second |
| Maximum batch | 512 records and 4 MiB charged payload |
| Maximum batches started per flush tick | 8 |
| Flush time budget before starting another batch | 250 ms, including time already spent writing/backing off |
| Provider write-attempt timeout | 5 seconds |
| Additional transient write attempts | 2, after delays of 1 and 2 seconds |
| Shutdown drain deadline | 5 seconds |
| Operation writer lease lifetime / renewal interval | 30 seconds / 5 seconds, measured by provider authority |
| Pending-clear polling | Every writer tick, including ticks with no operation records |
| Clear preparation deadline | 10 seconds to obtain writer acknowledgements or establish expiry |
| Maximum active writer leases / unfinished clear fences per provider scope | 128 / 64; maintenance beyond these bounds returns an explicit capacity/busy result |
| In-memory operation retention | 10,000 records, 128 MiB charged payload, or 24 hours, whichever limit is reached first |
| EF operation retention | 100,000 records or 7 days, whichever limit is reached first |
| Default and maximum query page size | 50 and 200 |
| Maximum operations per exact distribution or comparison selection | 10,000 |
| Operation query timeout / concurrent query builds per process | 5 seconds / 4; excess queries return Busy without a waiting queue |
| Maximum dimension predicates / grouping dimensions | 8 / 4 |
| Operation paging boundary lifetime | 5 minutes; deletion or store reset can invalidate it sooner |
| Maximum Runtime snapshots per operation overlay | 2,000; larger selections return a limit result |
| Shared maintenance tick / root deletion batch / time budget | 5 seconds / 512 roots / 250 ms before starting another batch |

Counts also bound object overhead. Charged payload bytes are conservative accounting, not a claim about exact CLR heap usage. Fixed envelope fields, active scopes, source metadata, dimensions, measurements, and segment summaries all count toward limits. A repeated invocation of an admitted path reuses its aggregate entry. Validation occurs before retaining oversized values. Finalization does not create an unbounded second copy of active state.

Rejected paths or active scopes increment capture-loss counters and mark affected coverage/self-time statistics partial. An active-scope limit is not a silent limit on loop iterations. Counter overflow or non-finite accumulated measurements is flagged rather than wrapping into plausible values. Nesting and path limits prevent recursive or dynamically generated keys from growing state without bound.

Runtime and operation retention have independent budgets. Runtime pinning remains a runtime feature. Operation retention runs in bounded batches, evicting oldest completion UTC first with canonical operation ID as the tie-breaker. Age retention uses the provider's UTC cutoff. Counts and charged bytes are enforced during append; a record already outside age retention is returned as RetentionRejected rather than inserted only to disappear immediately. Clearing operation history invalidates matching buffered generations so a later flush cannot recreate cleared data. Runtime clear controls affect runtime data unless a combined deletion is explicitly labeled. Explicit clearing follows section 8.6 and includes selected pinned history; automatic retention continues to honor pinning.

Retention cannot remove a record whose live writer has not yet settled that sequence. Otherwise a lost commit acknowledgement followed by a retry could recreate the deleted root. Such records count against retention capacity. If eligible eviction cannot create enough space within the bounded append work, reject the incoming record with RetentionCapacity and retry only under the existing bounded retry policy; never exceed a hard cap or wait on the business path. Settled records and records owned by permanently expired/retired leases can be evicted safely. Explicit clearing can remove unsettled roots because its durable fence rejects their later retries. Writer settlement, retention, and append validation are atomic with respect to this rule.

The shared maintenance worker expires abandoned clear preparation, resumes sealed deletion, performs bounded retention, and removes unreferenced node metadata. Its own tick budget uses the same rule as flushing: check before starting another bounded batch, and honor the provider-call timeout for work already started. Shared providers coordinate ownership/progress transactionally so multiple hosts do not apply the same maintenance work inconsistently. Maintenance does not activate Runtime Broadcast or operation capture when those capabilities are disabled.

### 8.4 Health and completeness

Health queries expose effective runtime/operation/request capture state, configured provider identity and scope, request sampling policy and configuration, intentional blacklist exclusions, sampling evaluations/selections/skips/errors, rejected admission, truncated records, recorder/classification faults, rejected completed records, permanent persistence failures, queue depth and payload, in-flight payload, oldest queued completion, last successful flush, and retained time range. Writer readiness/lease loss, administrative clear discards, clear progress, batches/duration per flush, and whether time/count/byte budgets stopped a flush are separately visible. Counters have node and time scope so restarts do not look like negative deltas. Intentional exclusions, sampling skips, and maintenance discards are separate from overload loss.

The dashboard distinguishes records with incomplete instrumentation from operations that failed. Aggregate capture loss warns that counts describe retained records rather than every application execution. Diagnostics are not an authoritative business counter under overload.

Unknown persistence outcomes after exhausted uncertain commits are reported separately from confirmed loss. Recorder, sampling, active-scope, queue, and flush counters describe the serving process only. A shared provider can report store-wide retained counts and writer-lease state, but it does not infer live remote counters from those rows or sum counters from different process lifetimes. Health responses label each scope and timestamp; cluster-wide live health fan-out is not part of Operation Profiling. Runtime Broadcast behavior remains unchanged.

### 8.5 Minimal lifecycle logging

The shared recorder emits structured lifecycle events for operation-owned scopes through the existing logging abstraction. They use `LogLevel.Trace` with `Microsoft.Extensions.Logging`, corresponding to Serilog's Verbose level. Routine lifecycle events do not use Information, Warning, or Error. The host's existing logging configuration controls whether they are emitted. Runtime-owned scopes retain their runtime lifecycle integration and do not invent an operation ID for these events.

| Event | Fields |
| --- | --- |
| `ProfilingOperationStarted` | Operation ID, key at start, kind, and existing correlation ID when available. |
| `ProfilingOperationStopped` | Operation ID, final key, kind, outcome, and elapsed milliseconds. |
| `ProfilingSegmentStarted` | Operation ID, full segment key path, and an operation-local invocation sequence. |
| `ProfilingSegmentStopped` | The same segment identity fields, outcome, and elapsed milliseconds. |

Events use stable IDs and parameterized templates following the repository's `[LogKey] ... (property={Property})` convention. Operation and segment categories can be filtered independently. Segment invocation sequences distinguish repeated or concurrent calls in logs without generating or persisting a GUID per call. The logging timestamp comes from the host logger; captured profiling datetimes remain UTC.

Attempt at most one start event and one terminal event per admitted recording scope. Terminal events cover completed, failed, canceled, aborted operations, and incomplete scopes through the same finalization path. Parent-driven closure and later disposal must not produce duplicate stop events. A key changed by application code appears as the initial key on start and final key on stop, linked by operation ID.

Check the level before preparing log-only state, formatting paths, or allocating property collections. Disabled, suppressed, blacklisted, sampling-skipped, and admission-rejected scopes produce no lifecycle events. Log templates include no bodies, dimension dumps, measurement dumps, raw URLs, or exception payloads. A safe failure code/type may accompany a failed stop event without creating a second per-segment error log.

Lifecycle logging is independent of persistence and does not flush batches. It creates no new trace table and does not replace segment aggregation. When Verbose/Trace is enabled, repeated segments intentionally produce per-invocation start/stop log events; the host's sink and filter choices therefore affect stress-test cost. Normal performance validation runs with lifecycle events filtered out and separately measures an enabled logging configuration.

Logger failures follow capture-failure isolation and cannot escape into business work or change the recorded business outcome. Report a logging fault through bounded health accounting without recursively logging through the failing path. A crash, a changed filter, or sink loss can leave an unmatched start event; the system does not claim that these optional logs form a complete execution trace.

### 8.6 Clear stored history

Every `IProfilingStorageProvider` implements this required maintenance operation. It is available for the in-memory, EF, and custom providers through the same public contract:

```csharp
Task<IResult<ProfilingClearResult>> ClearAsync(
	ProfilingClearRequest request,
	CancellationToken cancellationToken = default);
```

`ProfilingClearRequest` contains a `DataSet` selector defaulting to `All` and optional `FromUtc` and `ToUtc` bounds. Both bounds omitted means all history in the selected datasets. Both supplied means the half-open interval `[FromUtc, ToUtc)`: the lower bound is included and the upper bound excluded. Bounds use UTC `DateTimeOffset` values, serialize with a `Z` suffix, and must satisfy `FromUtc < ToUtc`. A missing single bound, non-UTC value, or invalid dataset returns a validation failure before mutation.

For example, these are separate maintenance calls:

```csharp
// Clear all Runtime and Operation history in this provider's storage scope.
await provider.ClearAsync(new ProfilingClearRequest
{
	DataSet = ProfilingDataSet.All
}, cancellationToken);

// Clear Operation history completed within one UTC hour.
await provider.ClearAsync(new ProfilingClearRequest
{
	DataSet = ProfilingDataSet.Operations,
	FromUtc = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
	ToUtc = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero)
}, cancellationToken);
```

Time-range selection applies to root records, using an operation's `CompletedUtc` or a terminal runtime session's completion UTC. A standalone runtime snapshot belongs to its terminal standalone session and follows the same rule. Selection uses recorded execution time, never database insertion or batch-flush time. A root that starts before the range and completes inside it is selected; a root that only overlaps the range but completes outside it is retained.

Removing a selected root removes its complete owned data: operation segment summaries, dimensions, measurements, and HTTP metadata, or runtime snapshots, segments, markers, and stored derived data. Owned records can have timestamps outside the selected interval; they are removed with their root rather than leaving a partial profile. Unselected roots are unchanged. Shared node metadata is removed only when no retained data or active capture still needs it. Correlation and parent-operation links are non-owning and never cascade deletion to another root or dataset.

`All` includes both datasets and selected pinned runtime history. A dataset-specific clear never deletes the other dataset merely because their times overlap. The storage scope is the selected provider's configured scope: node-local for the in-memory provider and shared where the provider reports shared storage. No application tables, feature configuration, or unrelated logging data are cleared. Existing Runtime-only clear controls call this contract with `DataSet = Runtime`.

Preserve Runtime Profiling's active-session guard. If a selected dataset includes Runtime and a runtime session is active in the provider's scope, reject the entire clear with a typed busy result before removing either dataset. Operations-only clearing remains available during runtime collection. Clearing does not stop collection, cancel application work, or disable future operation capture.

Each accepted clear establishes the provider-coordinated boundary specified in section 8.7. For Operations this is a completion-sequence cutoff acknowledged by each participating writer, not a comparison of machine clocks. Matching completed records already queued, in flight, or awaiting retry at that writer's cutoff belong to cleared history as well as matching stored roots. They must not reappear after a successful clear. Operations publishing their completion after their writer's cutoff and writers registered after preparation began are outside that clear. Unmatched buffered records remain eligible for persistence. The result exposes the preparation/seal UTC times; those timestamps describe maintenance progress and are not a fictional simultaneous cutoff across all nodes.

Generation or deletion-fence checks and writes are coordinated by the provider so a racing append cannot undo a completed clear. Coordination stays off the business execution path. Maintenance may delay background writes, but application execution never waits for provider maintenance or a database operation; the existing bounded-buffer loss policy continues to apply. The brief local recorder synchronization used to read a cutoff never encloses provider calls or waits for acknowledgements. EF uses independent contexts and bounded deletion batches, with each root and its owned data removed consistently. Providers do not load the entire history into application memory to delete it.

`ProfilingClearResult` reports a provider-generated clear ID, dataset, normalized bounds, preparation/seal/completion UTC times when reached, provider scope, completion state, and deleted stored-root counts for Runtime and Operations. Writer cutoffs are retained as maintenance evidence by the provider. Buffered records discarded by clearing are accounted for separately from deleted stored roots and overload loss. Completion means the selected history is gone and protected from late-write resurrection; it does not promise that continuing capture leaves the store empty. Query caches and paging boundaries affected by deletion are invalidated, and retention/coverage metadata is updated.

Cancellation or failure can interrupt bounded deletion after some roots have been removed. Return a typed outcome with known progress instead of claiming atomic rollback or successful completion. A provider must not report completion if it could not establish the required writer boundary. Each new `ClearAsync` call establishes a new boundary, so repeating a call can also select history completed since the prior call; the API does not promise replay of a previous maintenance operation. Keep deletion-fence state bounded, but do not expire protection for removed history while stale writes remain admissible. These semantics are part of provider conformance, not optional capabilities.

### 8.7 Writer registration and clear coordination contract

The purpose of this contract is simple: if node A has a completed operation waiting in its queue when history is cleared, writing that old queue entry later must not restore the deleted history. At the same time, work completing after the clear boundary must remain recordable. The protocol below provides that distinction without database calls from application execution or synchronized node clocks. It is provider-based and does not add Broadcast dependencies to Operations.

#### Writer identity and completion order

The provider owns a persistent store-epoch identifier, a monotonically increasing writer-registration generation, and a bounded registry of active writer leases. An empty/rebuilt store receives a new epoch. Each background writer opens a lease with its cached node descriptor; a lease has a unique writer ID/token, the store epoch, its registration generation, and provider-authoritative expiry. Node identity and writer identity differ: a node represents the process, while a lease can be replaced after an outage without changing that process's node ID.

Within a lease, completed immutable operation records receive strictly increasing `CompletionSequence` values. Sequence assignment and publication to the bounded queue share one short in-memory synchronization boundary. A clear acknowledgement reads that same boundary. The sequence orders publication of completed diagnostic records, not HTTP arrival, business start, or UTC timestamps. Queue rejection can leave sequence gaps; it never reuses a number. Each write envelope retains its original lease, node, operation ID, completion sequence, and immutable record through every attempt. Providers store the write identity alongside the root for later clearing.

Only the background writer registers/renews leases, polls maintenance state, acknowledges boundaries, and appends batches. Before its first successful lease activation, completed captures are dropped with a writer-unavailable reason rather than accumulated and later presented as new post-clear records. A scope that finishes after activation can use that active lease. During a temporary outage, already leased records can remain in the bounded queue; lease expiry causes a distinct loss outcome when they can no longer be accepted. No stale record is relabeled with a replacement lease. Business execution and scope cleanup proceed normally in every case.

#### Required store operations

The focused operation-store facet exposes typed operations with these semantics. Concrete parameter/result types remain backend-independent.

| Operation | Contract |
| --- | --- |
| Open writer | Idempotently upsert the cached node and issue a lease in the current store epoch/registration generation. An open attempt has a stable client attempt ID so retrying an unknown result does not consume another active lease. Reject registration beyond configured capacity without affecting business execution. |
| Synchronize writer | Renew an unexpired lease, report the writer's settled-through sequence, and return pending clear requests and authoritative lease/epoch status. An expired or unknown token cannot be renewed into a new identity. |
| Acknowledge clear | Supply a completion-sequence cutoff read atomically from the local recorder for the indicated clear ID. Cache that cutoff locally and resend the same value after an unknown response. Repeating the acknowledgement returns the same cutoff; it never advances that clear's boundary. |
| Append operations | Accept envelopes under their original lease, checking epoch, expiry, settled watermark, applicable clear fences, retention admission, and stable operation-ID idempotency atomically with each accepted root write. Return per-record accepted/already-stored, already-settled, cleared, stale-writer, retention-rejected/capacity, or failure outcomes. |
| Close writer | Retire the lease after drain or expiry handling. Later writes with that token are rejected. A restarted/reconnected writer obtains a fresh lease. |

A settled-through sequence means every sequence up to that value is no longer eligible for replay: it is persisted, administratively cleared, discarded, or abandoned with an explicitly unknown persistence outcome. No queued, paused-finalization, still-running, or retry-eligible attempt remains below it. The provider atomically advances this watermark against append validation, including delayed transactions from an already returned uncertain attempt. It never creates a new root for a write at or below the acknowledged watermark. An earlier committed root can still return an existing idempotent result, but cannot be recreated after retention or clearing removes it. Local bookkeeping advances only after that settlement is confirmed; an unknown synchronization response is retried idempotently.

#### Preparing, sealing, and applying a clear

1. **Prepare.** Serialize clear preparation within the provider scope and validate the selection. For a Runtime-inclusive clear, atomically check the active-session guard and reserve a maintenance gate against a racing Runtime start/import. A conflict returns Busy before deleting either dataset. For Operations, advance the registration generation and snapshot the eligible unexpired writer leases. New leases use the new generation and are outside this clear.
2. **Collect cutoffs.** Each eligible writer observes the pending clear on its background tick, reads its local last-published completion sequence, and acknowledges that fixed cutoff. Business work continues publishing later sequence numbers. A retired/expired writer without an acknowledgement has no admissible future writes, so its stored roots belong to pre-clear history. An acknowledged cutoff remains fixed even if that lease subsequently expires: roots above that cutoff stay outside this clear. If a writer remains unreachable with an unexpired lease when the preparation deadline expires, return Busy and remove the unsealed request without deleting history. Late acknowledgements cannot revive a canceled preparation. Do not guess a cutoff from its machine time.
3. **Seal.** After all eligible writers have acknowledged or expired, durably store the selection, registration-generation boundary, and every acknowledged writer cutoff as a clear fence, including acknowledgements from writers that subsequently retired or expired. Publishing this fence is atomic with respect to append validation. A pre-fence commit is included in deletion; a later matching append is rejected as Cleared. The Runtime maintenance gate and existing invalidated-session guards protect its selected roots and late interval/snapshot writes.
4. **Delete.** Remove matching stored roots in bounded batches using the same fence predicate: the selected dataset/range and, for Operations, the writer generation/cutoff. Whole owned graphs are deleted consistently. A root published above its writer's cutoff or under a newer registration generation is retained even if its completion UTC falls within the requested range. UTC bounds select history; sequences establish the clear boundary.
5. **Acknowledge disposal.** Writers discard matching queued/retry envelopes under the fence, account for administrative loss, and continue persisting unmatched or newer envelopes. They report settled-through progress. A busy or disconnected writer cannot bypass the fence by submitting a delayed batch.
6. **Complete.** Report success once matching stored roots are gone and the durable fence prevents their return. Writers need not finish physically draining their old buffers for deletion to be safe. Release Runtime maintenance gating so normal control resumes. A store/query revision invalidates affected cached pages and summaries.

For example, node A acknowledges cutoff 40. Its matching operation at sequence 40 is removed or rejected even if its batch arrives later. Sequence 41 is outside that clear. Node B can acknowledge a different cutoff, independently of clock skew. A different-range record at sequence 39 is preserved and can still be flushed. A writer with an expired lease cannot submit any old envelope, even after registering a new lease.

Lease expiry, generation changes, fences, and append checks use provider-authoritative state. A shared EF provider enforces them transactionally across processes; an in-process lock alone is insufficient. The in-memory provider implements the same semantics with a local synchronization gate. Different stores are separate scopes and never claim a shared clear boundary.

#### Failure and bounded maintenance state

Before sealing, cancellation/failure releases preparation state and the Runtime gate without deleting history. Unsealed preparation has a provider-authoritative deadline; recovery expires abandoned preparation and releases its gate without deleting roots. A late caller cannot seal that expired clear. After sealing, the fence remains effective even if the caller disconnects or deletion fails. Return known partial progress when a response is still possible. The shared profiling maintenance worker invokes the selected provider's bounded `ResumeMaintenanceAsync` contract after recovery/startup and on maintenance ticks; it needs no enabled Runtime or Operation capture capability, only enabled shared Profiling. There is no backend-type switch in that worker. Runtime starts/imports receive an explicit maintenance-busy result while their sealed clear requires the gate. Operation capture continues under the existing buffer policy. Health exposes preparing, applying, completed, and failed/retrying clear states, plus administrative discard counts.

A fence can be compacted for a writer after its settled watermark passes that cutoff or its lease is permanently retired. Unknown/retired tokens always reject writes, including after registry cleanup, and a new registration cannot reuse them. This makes fence cleanup safe without retaining every deleted operation ID. Keep the active-lease and unfinished-fence limits in section 8.3; when they cannot be respected, reject new maintenance/registration rather than retaining unbounded metadata or removing a still-needed fence. Clearing all history preserves the small coordination records and live node identities needed to enforce these guarantees; it removes profiling evidence, not the safeguards against stale writes.

## 9. Dashboard pages and internal endpoints

### 9.1 Views and routes

One Profiling navigation entry contains Runtime and Operations. Requests is a visible HTTP-filtered entry within Operations using the same Razor components, records, query engine, and detail rendering. It keeps request-focused HTTP columns without creating a second data store.

The profiling HTTP endpoints defined here serve the dashboard pages only, including their JSON data requests and actions. They are internal dashboard contracts, with no supported HTTP API for custom clients or external integrations. Endpoint registration follows dashboard enablement, its configured route group, and its authorization policy. Registering Profiling without the dashboard makes the in-process services available but exposes none of these HTTP endpoints.

Routes use the configured dashboard group path. The default paths are:

| Route | Purpose |
| --- | --- |
| `/_bdk/dashboard/profiling` | Entry: Runtime when Runtime capture is enabled, otherwise Operations when Operation capture is enabled; with both capture capabilities disabled, open Runtime history with navigation to both retained datasets. |
| `/_bdk/dashboard/profiling/runtime` | Runtime sessions, snapshots, segments, markers, and existing exports. |
| `/_bdk/dashboard/profiling/runtime/content` | Refreshable Runtime Razor content. |
| `/_bdk/dashboard/profiling/operations` | All operation kinds with filters and analysis modes. |
| `/_bdk/dashboard/profiling/operations/content` | Refreshable Operations Razor content using the selected filters and mode. |
| `/_bdk/dashboard/profiling/requests` | Operations constrained to `HttpRequest`, with HTTP columns. |
| `/_bdk/dashboard/profiling/requests/content` | The shared Operations content renderer constrained to `HttpRequest`. |
| `/_bdk/dashboard/profiling/operations/{id}` | Canonical operation detail, segment-summary tree/table, and runtime overlay. |
| `/_bdk/dashboard/profiling/operations/{id}/content` | Refreshable operation details and runtime overlay. |
| `/_bdk/dashboard/profiling/requests/{id}` | HTTP detail entry resolving the same operation record and summaries. |
| `/_bdk/dashboard/profiling/requests/{id}/content` | The shared detail renderer constrained to an HTTP operation. |
| `/_bdk/dashboard/profiling/operations/api/records` | Paged records with validated filters and ordering. |
| `/_bdk/dashboard/profiling/operations/api/records/{id}` | Exact lookup. |
| `/_bdk/dashboard/profiling/operations/api/groups` | Group counts, ranking, and supported aggregate summaries. |
| `/_bdk/dashboard/profiling/operations/api/analysis` | Bounded operation/segment distributions and two-selection comparison. |
| `/_bdk/dashboard/profiling/operations/api/records/{id}/runtime` | Related runtime evidence and coverage. |
| `/_bdk/dashboard/profiling/operations/api/health` | Capture, persistence, and retention status. |

The dashboard loads operation data through `IOperationProfilingQueryService`. The Requests view supplies an HTTP-kind filter to those queries. Shared storage supports shared queries; an in-memory provider exposes its node-local scope instead of implying cluster-wide results. The DI query contracts remain independent of the dashboard's internal HTTP routes.

The initial page render uses the existing dashboard's RazorSlices pattern. Its server-side view-model builder resolves query services through DI and calls them directly: `IRuntimeProfilingQueryService` for Runtime data and `IOperationProfilingQueryService` for Operations and Requests data. Runtime lifecycle status and commands continue to use `IRuntimeProfilingControlService`. A page does not make an HTTP request back into its own application to load its initial model.

Shared Profiling registration supplies retained-data query services for both datasets even when one capture capability is omitted. Runtime capture/control workers remain capability-specific. A Runtime history page does not require an active collector or Broadcast registration; unavailable start/snapshot/GC controls are shown disabled rather than causing DI resolution failure. Clearing Operations remains available through the provider's in-process maintenance contract. A new Operations clearing page or HTTP action is not required by this version; existing Runtime clearing controls retain their Runtime-only scope.

Each query service reads through its focused store contract, `IRuntimeProfilingStore` or `IOperationProfilingStore`, mapped to the selected `IProfilingStorageProvider`. Filtering, grouping, paging, and analysis use the shared query contracts. Razor pages, view-model builders, and HTTP endpoints do not access EF contexts, provider implementations, live recorder scopes, or pending operation queues directly.

Initial rendering and subsequent content requests use the same view-model builder and Razor content slice. The browser requests the appropriate `/content` endpoint, whose server-side handler invokes those same DI query services and returns rendered HTML with the chart data and freshness metadata required by the view. Extend the Runtime dashboard's existing `Content`/`Data` slice structure and shared refresh helper for Operations, Requests, and detail views. The Requests adapters enforce their HTTP-kind restriction while reusing the Operations builder and renderer.

JSON endpoints support targeted data requests made by the dashboard pages. Their routes and DTOs follow dashboard needs and may evolve with the pages; they carry no separate external-client compatibility contract. Each endpoint has a dashboard use case and invokes the same query services, without implementing a second set of filter or aggregation rules. An ordinary content refresh obtains its rows, counts, and chart payload together, without separately polling JSON endpoints for the same displayed data. Both rendering and JSON paths propagate request cancellation to their queries.

An operation detail's runtime overlay uses the shared query-service correlation use case to combine operation data with runtime evidence for the same node and overlapping UTC interval. Correlation logic remains outside Razor markup and JavaScript. The builder exposes the resulting bounded data to the content slice, including unavailable runtime evidence and coverage information.

Dashboard authorization applies to every view, content endpoint, detail URL, and internal JSON endpoint. IDs do not bypass authorization. Metadata and errors render as text. Queries, polling, and retention must remain bounded when capture is under load.

### 9.2 Shared filters and grouping

Filters include operation ID, correlation ID, parent operation ID, UTC range, key, kind, node, application version, outcome, operation dimensions, segment key/path, and segment dimensions. Requests additionally supports application request ID, method, route, HTTP status, and sampling strategy/configuration identifiers. Developer-supplied dimensions appear as ordinary dimension filters.

Default grouping is `(Kind, Key)`. Users can add selected dimensions, such as `productType`, to keep distinct workloads separate. Requests can additionally group by HTTP method because the default path key does not include it. Missing dimensions remain a distinct group value. Filters and selected grouping dimensions are visible beside counts and timing summaries.

The default list window is the last 15 minutes by completion time, with completed outcomes selected. Users can choose fixed UTC ranges, other outcomes, or all outcomes. Runtime correlation uses interval overlap instead of this list-window membership rule. The UI labels the active time and outcome policy.

### 9.3 Slow, Recent, and By count

The Razor page offers these modes:

| Mode | Selection and ordering | Group display |
| --- | --- | --- |
| Slow | Select the N slowest individual matching operations by duration descending, with a deterministic ID tie-breaker. | Group selected occurrences by the chosen grouping fields. Order groups by their slowest selected occurrence. |
| Recent | Select the N most recently completed matching operations, ordered by completion UTC and ID descending. | Show chronological occurrences, with optional grouping by the same fields. |
| By count | Select the N groups with the greatest retained matching operation count, with a deterministic group-key tie-breaker. | Expand a group into paged occurrences, sortable by duration or recency. |

The result limit defaults to 25, with choices 10, 25, 50, and 100. The label reads operations in Slow and Recent, and groups in By count. A group count always covers all retained records matching the current filter window, not just the displayed N rows. For example, a group can show 3 slow rows from 420 matching retained executions. With reduced sampling, those 420 are retained selected executions, not the total number of requests that ran.

Each group shows its key, kind, selected dimensions, retained count, maximum duration, and links to its occurrences and analysis. Median and p95 are shown when bounded analysis can compute them; otherwise the UI asks for a narrower selection. Counts are never silently limited to the first analysis page.

Each occurrence shows its ID, executing node display name/key, UTC start/completion, duration, outcome, segment bar, and relevant dimensions. Node details include process identity so two processes with the same host/display name remain distinguishable. HTTP occurrences also show method, route, status, and response-size quality. Non-HTTP occurrences use adapter metadata without empty mandatory HTTP columns.

Root outcome, observed segment failures, and capture quality have separate indicators. A completed operation with a recovered segment failure remains discoverable through a `hasSegmentFailures` filter, including when the root outcome filter selects completed operations. Details show segment outcome counts, bounded failure categories, and timing statistics for each outcome. Truncated capture cannot prove the absence of segment failures; the UI labels these indicators as observed evidence.

### 9.4 Segment summaries and percentage bars

Operation details contain a segment-summary tree or table. Rows show full key path, invocation count, outcome counts, total duration, total self duration, average, minimum, maximum, and reduced measurements. Sorting by total or self duration reveals expensive components. A tree preserves nesting context without implying invocation order. It does not expand into a trace of individual calls.

Each operation occurrence also has a horizontal stacked summary bar whose full width represents the operation's wall-clock duration. Colors are stable by segment key. Hover and keyboard-focus tooltips show the bucket, percentage, and elapsed milliseconds or seconds. The bar represents accumulated coverage, not a chronological sequence. An accessible text summary accompanies it.

The recorder accumulates wall-time buckets online at each top-level segment start or end, using the elapsed time since the previous transition:

- Exactly one active top-level invocation assigns that elapsed time to its key path.
- Multiple active top-level invocations assign it once to a shared Parallel bucket, including overlapping invocations with the same key.
- No active top-level invocation assigns it to Outside segments.

Operation finalization accounts for the last interval. Only the active set and accumulated bucket values are retained. There is no stored transition log. Nested segments affect the segment-summary tree and self durations, not additional slices of the root percentage bar.

For sequential loops, contributions from the same top-level key add into one slice. A `Load` parent containing parallel database and document-storage work still occupies one top-level slice. Concurrent top-level work goes into Parallel instead of crediting the same elapsed time to two slices. The sum is 100% for complete capture without dividing overlapping cumulative durations by root duration.

The tooltip labels the bar's wall-time contribution separately from the segment table's cumulative duration. A group summary does not manufacture a bar by combining maxima from different operations. Bars belong to real operation occurrences.

If capture is rejected or truncated, affected time is marked unobserved where identifiable and the entire breakdown is labeled partial. Unknown coverage is not confidently assigned to a known key. A zero-duration operation shows `0 ms` without percentage division. An operation with no segment instrumentation shows its full duration as Outside segments.

### 9.5 Auto refresh and freshness

Refresh choices are Paused, 2 seconds, 5 seconds, 10 seconds, and 30 seconds, with 5 seconds as the default. Refresh uses bounded GET polling of the Razor content endpoints through the existing `window.bdkDashboard.createRefresher` mechanism used by Runtime Profiling. Each view has one refresh loop, with at most one refresh in flight. Filter changes cancel obsolete work, hidden tabs pause polling, and late responses cannot overwrite newer filters.

Refresh preserves filters, selected mode, expansion state, scroll position, and selected operation. A sliding window advances on refresh; a fixed UTC range stays fixed. Each response includes its evaluated time boundary, provider scope, persistence watermark, and coverage information. Content responses expose these through their rendered view model and associated data attributes or chart payloads; JSON responses expose them as DTO fields. Built-in Operation providers follow the consistent query-boundary contract in section 8.2; a custom provider reports unsupported capabilities or weaker non-paged consistency explicitly. Busy, timeout, and expired-boundary responses retain the last successfully rendered content with a stale/error indicator instead of replacing it with an empty-success view. Automatic retries wait for the next selected refresh interval.

The page shows last successful refresh and last successful persistence flush separately. A faster refresh interval does not flush capture buffers or make pending records available. Queue delay and capture loss remain visible alongside analysis.

Paging has stable ordering and a captured query boundary. New flushes do not move already paged records silently. Expired boundaries or retention movement return an explicit result rather than claiming immutable history. Live refresh can deliberately start a new boundary.

A header ID may not resolve until its operation completes and a batch persists it. Exact lookup returns unavailable when state is unknown, with a manual or bounded retry in the UI. It says pending, dropped, or expired only when that state is known. The API does not infer a missing record's history from its ID alone.

## 10. Acceptance criteria

The following scenarios describe observable completion of this specification. They are not delivery stages.

### 10.1 General operation capture

As an application developer, I can profile an execution independently of HTTP so services and Blazor actions share one analysis model.

- Given no HTTP request or runtime session, a completed manual operation persists its ID, key, kind, duration, dimensions, and segments.
- Given two overlapping actions in one Blazor circuit, their records and nested segments remain isolated across `await` and parallel branches.
- Given no active operation, a segment helper executes its business delegate once and creates no implicit root. An explicit new root restores previous ambient context on disposal.
- Given disabled capture, rejected admission, or a recording exception, application results and exceptions remain unchanged and no stale context receives inner segments.
- Minimal API service-parameter and controller-constructor injection enrich the middleware-owned operation with the same ID. Registered-disabled capture is a no-op, and explicitly optional injection also works when `AddProfiling` is absent. No scoped service is captured by the singleton façade.

### 10.2 HTTP capture and lookup

As an API developer, I can find an entire server request by its response header ID and enrich it in a controller.

- With all-request sampling, every non-blacklisted request reaching enabled middleware receives its own ID and capture attempt, including preflights, early downstream responses, and requests with no segments. Disabled, blacklisted, or sampling-skipped requests do not start HTTP operations.
- Controller key overrides and developer-supplied dimensions appear on that same operation. Error re-execution does not duplicate it.
- Serialization, compressed streaming, `BodyWriter`, and send-file paths preserve response behavior, record observed bytes with quality, and finalize exactly once on normal completion or abort.
- Exact lookup handles the flush delay honestly. No body content, raw query string, or cached ID from another server invocation enters the record.
- A real host using the reference middleware order captures downstream serialization, compression, cache hits, static/authorization short circuits, routing failures, and handled exceptions. A downstream exception handled as 200/4xx remains observed as a failure, while an ordinary 4xx remains Completed. Framework diagnostic suppression does not affect capture.
- Error-handler re-execution with a replacement DI scope retains the original operation ID, sampling decision, path key, and original route, including an originally unmatched request. Custom exception filters can report through the safe feature hook without changing their response behavior.
- Cache-hit responses replace an earlier cached profiling ID for a selected request and remove it for disabled, excluded, or sampling-skipped requests. Compression/stream/BodyWriter/send-file and abort cases preserve byte-observation quality and exactly-once cleanup. Completion callbacks use explicit handles after ambient context is restored.

### 10.3 Segments and feature behaviors

As a devkit feature author, I can wrap an existing execution boundary and contribute to the active operation.

- Five `Load` calls with three `Read` calls each produce `Load` count 5 and `Load / Read` count 15. `Calculate / Read` stays separate. A million sequential calls to the same path reuse bounded retained state.
- Sequential, nested, and parallel invocations produce correct cumulative, self-time, min/max, and outcome statistics. Missing measurements, incompatible reducers, mixed dimensions, and unfinished scopes remain distinguishable from valid zero values.
- A job behavior joins an active operation as a segment or owns a root at an independent worker boundary. It invokes the next delegate once and maps both exceptions and job result outcomes.
- A standalone pipeline produces one operation containing step segments. Inside an existing operation it adds a pipeline segment with nested steps. Repeated/retried steps aggregate by path, never-run steps create no timing samples, and the behavior preserves the actual final result and control flow.
- A bounded orchestration execution contains action segments for regular, signal, and compensation activity attempts. A Wait/pause closes the current slice, and a resumed slice is correlated without mutating prior records. Registration enables the companion outer scope as well as the action behavior.
- With profiling behaviors registered but no `AddProfiling`, DI validation and activation succeed for jobs, both pipeline entry points, orchestration actions, and the companion execution-scope adapter. No profiling service or fallback façade is registered as a side effect, and no profiling provider, writer, options, or logger is required to construct them.
- Each optional integration passes through success, failed domain results, synchronous/asynchronous exceptions, and cancellation with exactly one invocation of the next delegate and unchanged application behavior. Omitted registration produces no profiling state, lifecycle log, warning, or persistence work. The same business outcomes hold with registered-but-disabled profiling and with execution-local suppression; enabled capture still follows the normal join-or-start rules.
- A registered profiler with broken DI construction fails validation or activation normally rather than silently being treated as absent. Runtime recording faults remain isolated as specified in section 10.7.
- No adapter persists arguments, results, service providers, or live application contexts. Persistence internals do not recursively record themselves. A prematurely closed parent finalizes live children as incomplete without double-counting later disposal.
- `Load`, `load`, and `LOAD` aggregate into one path, including nested and concurrent calls, and consume one distinct-path slot. Different parents remain distinct. Both providers return matching groups and filters under different process cultures, with binary comparison projections and explicit typed dimension-value semantics. Original casing remains available for display.
- Rejected keys, values, and replacements preserve the documented state and quality flags. Integer values beyond browser-safe number precision, distinct decimal/double values, UTC kinds/offsets, missing values, and empty strings survive round-trip/filter/group tests without type or identity loss. Descendants of a rejected segment never attach to another summary path.

### 10.4 Bounded persistence

As a developer running a stress test, I can collect useful evidence without blocking application work on diagnostic persistence.

- Both providers expose completed records through periodic batches. A slow or failed store cannot block request, action, or job execution on queue space.
- Active state, queue, in-flight batch, metadata, and retention limits hold under overload. Drops and truncation appear in health and quality data.
- Retries after an uncertain commit create no duplicate records, double-counted summaries, or partial graphs. EF batches use independent contexts and caller cancellation cannot cancel the writer's unrelated batch.
- Clearing history prevents buffered resurrection. Shutdown honors the drain deadline and records available loss information.
- Every provider clears all history or a UTC range through `ClearAsync`, for Runtime, Operations, or both. Range tests cover inclusive/exclusive boundaries, root completion versus start/insertion time, selected owned data outside the interval, and unchanged unselected roots. Invalid requests mutate nothing.
- Clearing includes selected pinned history, preserves shared nodes still in use, and never follows non-owning correlation links into other roots. A runtime-active guard rejects a combined clear before either dataset changes; Operations-only clearing remains available.
- Queued, in-flight, retried, and remote-node appends cannot restore matching history after successful clearing. Unmatched queued records and operations published above their writer's acknowledged cutoff remain eligible for persistence, without blocking business delegates. Lease expiry after acknowledgement does not widen that cutoff to include already persisted newer records.
- Failure or cancellation during bounded deletion exposes partial progress and does not report complete success. Repeating a clear establishes a new boundary and can include subsequently completed history. Queries, cached summaries, and paging boundaries reflect the deletion.
- Controlled-clock writer tests verify 512-record/4 MiB batches, no more than 8 batch starts per tick, the 250 ms next-batch budget, a fixed per-tick queue watermark, coalesced ticks, and no writes triggered by application enqueue or dashboard refresh. Timeouts/retries do not create overlapping append attempts.
- Two independently hosted writers against one shared provider exercise clear cutoffs with different/skewed UTC clocks, racing appends, an unreachable unexpired lease, expiry, a new writer registered during preparation, and an active Runtime start race. Matching old records cannot reappear; unmatched and post-cutoff records survive. Reconnected writers cannot relabel stale buffers.
- A crash/cancellation before sealing leaves history unchanged; after sealing, durable fences remain active and maintenance resumes deletion. Advancing settled watermarks cannot resurrect retained/deleted IDs, and fence/lease compaction rejects late writes even after bookkeeping cleanup. Store-epoch reset rejects all previous leases. Capacity limits return explicit Busy/unavailable outcomes.
- Lost commit responses followed by retention, retries, and settled-watermark advancement cannot resurrect an evicted record. Unsettled live-writer rows remain protected; retention-capacity rejection preserves hard limits. Exhausted unknown commits are distinguished from confirmed loss and fenced before replay eligibility is discarded. Partial batches settle successful members without incrementing aggregates again.
- Idle writer ticks still renew leases and acknowledge clear requests. Lease replacement cannot reuse an old numeric queue cutoff. An unknown open/clear-acknowledgement response reuses its original attempt identity/cutoff instead of creating duplicate leases or widening deletion.

### 10.5 Dashboard analysis

As a developer, I can find slow, recent, and frequent work, then inspect the segments that explain its elapsed time.

- Slow and Recent limit occurrences; By count limits groups. Group counts cover the entire retained filtered window, including records outside the visible top N.
- Filters, grouping dimensions, operation-kind selection, outcome policy, and UTC boundaries affect rows and counts consistently. Requests uses the same records with an HTTP filter.
- Bars count wall time once for sequential, nested, parallel, and no-segment examples. Zero-duration records avoid percentage division. Segment tables distinguish invocation count, owning-operation count, cumulative time, and wall-time contribution without reconstructing a trace.
- Refresh preserves view state, prevents overlapping polls, displays persistence freshness, and cannot apply an obsolete response after filters change.
- Initial Razor rendering and content refreshes call the same DI query services and view-model builder, without HTTP calls back into the host or direct storage access from the presentation layer. Runtime, Operations, Requests, and detail views use the shared Razor-content refresh mechanism with one polling loop per view.
- For the same filters and evaluated boundary, rendered content and internal JSON endpoints expose matching records, counts, segment summaries, and freshness metadata with both built-in providers. Requests keeps its HTTP-kind restriction on initial, content, detail, and JSON paths. Content endpoints enforce dashboard authorization and propagate cancellation just like JSON endpoints.
- Every profiling JSON endpoint serves a dashboard use case and follows dashboard enablement, route configuration, and authorization. With dashboard registration omitted or disabled, none of the profiling dashboard or JSON endpoints is exposed, while configured in-process profiling services remain usable. No HTTP endpoints are added for custom clients or external integrations.
- A completed operation becomes visible through the normal batch writer. Rendering, refreshing, and JSON queries neither flush pending operations nor read them from recorder memory. Runtime overlays use the shared correlation use case and show missing coverage without introducing query logic in the browser.
- Operation-only setup opens the Operations dashboard directly. Rows, details, filters, and grouping expose the executing node, distinguishing same-name hosts/processes and restarts. Disabled capture still permits the shared feature's retained-history views under its existing enablement rules.
- Paging while new batches arrive retains its commit boundary; racing clear/retention, store reset, altered filters, and expiry return explicit boundary errors. Rows and group counts share the same boundary. Query/overlay limits and concurrent admission remain bounded, and failed refreshes retain visibly stale content without a retry loop. Shared-store health never presents one process's live counters as cluster totals.
- Runtime retained-history views resolve in Operation-only hosts without Runtime capture/control workers or Broadcast dependencies. Disabled controls cannot trigger unavailable services.

### 10.6 Runtime context and evidence quality

As a developer investigating a slow operation, I can inspect runtime activity on the same process during its execution.

- A runtime session starting after an operation begins is still found by interval overlap. Several overlapping sessions and no matching sessions both render correctly.
- Overlay samples and surrounding context use the operation's interval and UTC. Aggregated segments are not rendered as continuous time spans. Missing coverage and clock discontinuities remain visible.
- Runtime-to-operation navigation applies the selected node and interval. Another node, a process restart, or an imported archive cannot produce a false identity match.
- Process metrics are never labeled as per-operation CPU or memory. Independent retention does not cascade-delete the other dataset.
- Operations start in a host with no Broadcast or Runtime registration and report their cached process/node identity. An initial database outage does not fail the application; writer-unavailable loss is visible. A combined host uses the same node when Runtime later participates, without a duplicate identity or a request-path registration call.
- Runtime Broadcast commands, collection/finalization, pinning, metrics, evaluation, and exports preserve the behavior mapped in section 2.4. Runtime markers remain instantaneous and runtime segments remain intervals; operation summaries never replace either. Version 2 archives round-trip and remap references; version 1 fails validation before import mutation.

### 10.7 Failures and cleanup

As a developer, I can diagnose unsuccessful work while preserving application behavior and keeping profiling state bounded.

- An exception escaping nested helpers preserves the original exception and stack, records each affected invocation once, restores ambient state, and leaves no active recording budget after cleanup. Repeated `Fail`, `Complete`, or disposal calls cannot turn failure into success or add samples twice.
- A recovered failure or a successful retry retains failed segment counts and durations while allowing the outer operation to complete. Multiple parallel child failures remain visible independently of which exception the join exposes. Failed `Result` classification returns the unchanged result, and a broken classifier reports diagnostic incompleteness without inventing business success or failure.
- Matched cancellation, unrelated cancellation exceptions, deadlines, HTTP transport abort, and late token cancellation follow their documented classifications. Incomplete child closure does not cancel business work, and later disposal cannot extend or mutate the frozen summary.
- Fault injection at recorder start, metadata update, outcome declaration, finalization, freezing, enqueue, and persistence does not duplicate the delegate, mask its exception, replace its return value, or leave stale ambient state. Consistent partial records and health counters describe the available evidence.
- Repeated error categories and messages stay bounded. Failure details retain no exception objects or sensitive payloads. Dashboard filters distinguish failed operations, completed operations with segment failures, and incomplete capture.
- Controlled time expires an abandoned root and all its active segments at the configured observation deadline, including when the writer/provider is unavailable. Capacity is released once, business work continues, and later completion cannot mutate or duplicate the incomplete record. HTTP streaming/upgraded connections continue after capture expiry with pass-through body observation and no retained recording buffers.
- Shutdown closes remaining capture before bounded draining. A process kill produces no fabricated completion on restart. Request-body observation is off by default; enabling it observes ordinary reads only, and replaced/unsupported body features retain explicit byte-quality limits.

### 10.8 Provider selection and request eligibility

As a host developer, I can choose a storage provider and configure which HTTP requests start profiling operations.

- The in-memory and EF providers pass the same storage conformance cases. A custom provider can register through the public contract without modifying the recorder, writer, query service, or UI. Conflicting explicit provider selections fail setup validation.
- An enabled request adapter requires enabled Operation Profiling. Missing or disabled dependencies fail validation when the master is enabled, regardless of fluent call order. Disabling Requests leaves manually started operations available; disabling the master stops all capture. Runtime configuration is independent.
- `/api/calculations?profile=a` with prefix `/api` gets key `/calculations`. `/apiary/items` is unchanged, exact-prefix input gets `/`, controller overrides remain intact, and overlong keys are bounded without simple prefix-only collisions.
- Blacklist tests cover exact, single-segment, recursive, case, trailing-slash, unmatched-route, and query-string cases. Matching uses the incoming path before key transformations. Excluded flows produce no root, segment, profiling header, or lifecycle log, including from downstream join-or-start behaviors. Suppression does not leak to the next request or an independent worker execution.
- Health and the dashboard distinguish disabled capture and intentional exclusions from overload loss, and report the selected provider's actual query scope and capabilities.

### 10.9 Lifecycle logging

As a developer, I can enable verbose logs to see when profiling scopes start and stop without changing storage behavior.

- With Verbose/Trace enabled, admitted operations and segment invocations emit their structured start/stop pairs. Failed, canceled, aborted, and incomplete scopes emit the appropriate stop outcome without duplicates.
- With lifecycle logging filtered out, no log-only path formatting or property allocation occurs. Blacklisted, suppressed, and disabled scopes emit no lifecycle events even when the log level is enabled.
- Concurrent repeated segments can be distinguished through operation ID, key path, and invocation sequence. Logs contain no bodies, raw URLs, metadata dumps, or exception payloads.
- A throwing logger does not mask an application failure, rerun a delegate, alter its return value, or force synchronous persistence. Logging-fault accounting does not recursively log another failure.

### 10.10 Fluent composition and sampling

As a host developer, I can enable only the profiling capabilities I need and choose how much HTTP traffic to record.

- `AddProfiling(...).WithOperationProfiling()` makes the same `IOperationProfiler` injectable in services, singleton consumers, Blazor actions, and workers without HTTP or Runtime Profiling. Disabled recording returns no-op scopes while business delegates still execute once. An omitted capability starts no worker for that capability.
- With omitted sampling configuration or explicit `AllRequests`, every non-blacklisted request is selected before admission limits. `Probability(0)` selects none, `Probability(1)` selects all, and controlled random inputs verify intermediate probabilities without flaky statistical assertions. Invalid probabilities fail setup.
- With a controlled monotonic clock and concurrent callers, rate sampling respects initial burst and refill, shares its budget across routes on one node, and never waits or changes HTTP responses. Blacklisted requests consume no token. A restart starts a new process-local budget.
- Sampling runs once before scope creation and survives error re-execution. A skipped flow creates no replacement root through manual or behavior-based instrumentation, no segments, and no profiling header or lifecycle logs. Independent worker executions do not inherit that suppression.
- A throwing or invalid custom strategy preserves business execution and increments sampling errors. Selected records retain policy metadata, dashboards distinguish sampled counts from total traffic, and policy differences remain visible during comparisons. Repeated fluent configuration changes only explicit settings without duplicate services or writers.

### 10.11 Performance and integration evidence

Implementation validation compares omitted profiling registration with participating behaviors still installed, registered-but-disabled capture, enabled empty operations, representative nested and parallel segments, and saturated queues. Measurements include throughput, latency distributions, allocations, and retained memory at representative concurrency. These validate profiler overhead; they do not add per-operation CPU or memory fields to the product.

Validation includes polling and EF batch writes during load, failure injection for providers, recorders, samplers, and loggers, shared provider conformance cases, and EF integration tests for SQLite, SQL Server, and PostgreSQL. Request matching, sampler concurrency, and disabled/skipped-capture overhead are included. Results document the environment, sampling policy, lifecycle log level, and configuration rather than promising a universal overhead percentage. Runtime control, exports, and authorization remain covered by regression tests.

Use a reference load of 1,000 completed operations/second with four sequential/nested segments and at most 4 KiB charged payload per record to evaluate the default writer settings. Report sustained persisted rate, queue age/growth, drops, allocations, and endpoint p50/p95 latency against capture-disabled runs for each provider on the documented test host. Controlled-time tests prove the scheduling capacity independently of database speed. If a provider cannot sustain that workload within the default time budget, report the measured limit and a tested configuration/sampling adjustment; do not claim the nominal 4,096-record ceiling as achieved throughput. Retention removals are accounted for separately from capture loss.

## 11. Repository references

The following files provide the existing feature context and extension points:

- [Profiling feature](../features-profiling.md)
- [Existing Runtime dashboard content builder](../../src/Presentation.Web/Profiling/Dashboard/Pages/Content.cshtml)
- [Existing Runtime dashboard refresh integration](../../src/Presentation.Web/Profiling/Dashboard/Pages/Index.cshtml)
- [Existing Runtime dashboard endpoints](../../src/Presentation.Web/Profiling/Dashboard/DashboardEndpoints.cs)
- [Runtime snapshot dashboard specification](spec-performance-snapshot-dashboard.md)
- [Jobs feature](../features-jobs.md)
- [Job behavior contract](../../src/Common.Abstractions/Jobs/Behaviors/IJobBehavior.cs)
- [Job metrics behavior](../../src/Application.Jobs/Behaviors/JobMetricsBehavior.cs)
- [Pipelines feature](../features-pipelines.md)
- [Pipeline behavior contract](../../src/Common.Utilities/Pipeline/Behaviors/IPipelineBehavior.cs)
- [Pipeline timing behavior](../../src/Common.Utilities/Pipeline/Behaviors/PipelineTimingBehavior.cs)
- [Pipeline execution context](../../src/Common.Utilities/Pipeline/Context/PipelineExecutionContext.cs)
- [Orchestrations feature](../features-orchestrations.md)
- [Orchestration behavior contract](../../src/Application.Orchestrations/Behaviors/IOrchestrationBehavior.cs)
- [Orchestration activity execution context](../../src/Application.Orchestrations/Behaviors/OrchestrationActivityExecutionContext.cs)
- [Orchestration executor](../../src/Application.Orchestrations/Execution/InMemoryOrchestrationExecutor.cs)
- [Orchestration integrations specification](spec-application-orchestration-integrations.md)

This specification defines the shared final vocabulary and behavior. Runtime and operation feature documentation, public XML examples, dashboard labels, and adapter documentation use these same terms.

## 12. Implementation readiness

The specified scope and observable contracts are ready for implementation. This status applies to the specification, not to completed code or verified throughput. Recording lifetime, identity, grouping, HTTP ownership, provider coordination, and dashboard query boundaries have explicit decisions and defaults. No new product decision is required to choose behavior for those cases.

Before the implementation is accepted, compile the documented fluent/DI/scope examples against the actual public API, run the provider conformance suite on in-memory, SQLite, SQL Server, and PostgreSQL stores, exercise the full HTTP middleware pipeline and optional feature registrations, and measure the workload defined in section 10.11. Include the lifecycle-expiry, retention/retry, cross-node clear, and paging race cases above. A failed performance or correctness check requires an implementation fix or an explicit specification amendment; it must not silently weaken the contract. Existing Runtime Broadcast and export regression checks remain required.
