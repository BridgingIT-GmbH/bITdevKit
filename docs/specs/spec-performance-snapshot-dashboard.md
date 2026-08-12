---
created: 2026-08-04
status: implemented
---

# Design Specification: Profiling Dashboard

> This specification defines a lightweight, developer-focused profiling dashboard for short-lived runtime diagnostics. The feature is intended for stress testing, warm-up validation, and ad-hoc troubleshooting, not for long-term production monitoring.

[TOC]

## Overview

The Profiling feature introduces a dedicated dashboard page for collecting and reviewing short bursts of runtime performance data while an application is running. The feature is designed for application developers who want to start a workload, collect a focused set of performance snapshots, and inspect the results immediately in the browser.

The feature shall also provide lightweight programmatic and console-command control surfaces so application code or a developer at the command line can start, stop, or manage collection sessions directly. This makes it suitable for integrated DevKit usage where a feature, test workflow, or terminal session can trigger collection around a specific operation without requiring manual dashboard interaction.

Deployment-wide control shall use the DevKit Broadcast feature from `Common.Utilities/Broadcasting`. The Profiling feature shall not poll its session store for control commands. Broadcast supplies the registered-node snapshot and direct push delivery used for session start, stop, manual snapshot, and manual GC actions.

This feature complements, rather than replaces, long-term observability platforms such as Grafana, Prometheus, OpenTelemetry, or vendor APM tools.

The primary goals are:

- make runtime performance inspection easy during local development and stress testing
- capture lightweight snapshots for a short period of time
- expose the results in a dashboard with clear visual summaries
- support multi-node deployments by collecting data from each node and letting the user focus on one node at a time
- support named sessions so developers can identify and revisit a specific run later
- support shareable session links so another developer can open a specific session directly from a URL
- support programmatic control so application code can start and stop sessions as part of automated workflows or integration tests
- support console-command control so developers can perform collection operations without opening the dashboard
- provide deterministic, evidence-backed analysis of two selected snapshots or a complete node session without using AI
- expose sampling quality so developers can judge whether the collected evidence is trustworthy
- preserve lightweight runtime context and workload phase markers that make a local run reproducible and easier to interpret

## Problem Statement

Developers often need to answer questions such as:

- Is the application allocating too much memory under load?
- Are GC collections becoming a bottleneck?
- Is CPU usage rising unexpectedly?
- Is one node behaving differently from the others?

This dashboard is intended to make those questions actionable by surfacing allocation-rate spikes, accelerating managed-memory growth, and abnormal behavior across snapshots within a focused diagnostic session rather than by enforcing a fixed production threshold.

Existing long-term observability systems are useful for production monitoring, but they are usually too heavyweight for fast, on-demand investigations. This feature fills that gap with a short-lived performance snapshot workflow.

## Goals

### Short-lived snapshot collection

The feature shall support collecting a short burst of performance snapshots that can be started and stopped on demand.

Collection shall run in a background service so it does not block the application or the dashboard UI. The service shall be controllable through dashboard actions and shall stop automatically once the configured collection duration has elapsed.

### Developer-oriented experience

The dashboard shall be optimized for quick inspection by developers during local runs, stress tests, and targeted diagnostics.

### Lightweight runtime metrics

The feature shall collect a focused set of runtime metrics that are useful for performance triage, including:

- memory usage
- allocations
- GC collections
- CPU usage

### Multi-node awareness

When the application runs on multiple nodes, the feature shall collect snapshots from each node and present them in a way that allows the user to inspect a specific node.

### Optional and bounded usage

The feature shall be opt-in and disabled by default. It shall be easy to turn off and shall be clearly scoped as a short-duration diagnostic tool, not a long-term retention system.

## Non-Goals

This feature is not intended to provide:

- long-term historical storage for production monitoring
- a replacement for OpenTelemetry or Grafana-based observability pipelines
- high-cardinality tracing or deep distributed tracing
- durable long-run analytics across days or weeks
- a full APM platform
- AI-generated performance explanations
- a single performance, health, or risk score
- production alarm thresholds or production health monitoring
- cross-node evaluation, node ranking, or deployment-wide metric aggregation

## Scope

The feature shall include:

- a dedicated performance dashboard page
- a background snapshot collector service
- manual one-off snapshot collection controls
- a dashboard view for showing recent snapshots as charts and summary cards
- node-aware filtering and selection
- configurable refresh intervals
- provider-based session and snapshot storage, including durable persistence across restarts when a durable provider is configured
- a manual GC trigger button for immediate diagnostics
- integration with the DevKit Broadcast feature for direct, non-polling node control
- integration with the existing DevKit Console Commands feature for command-line collection control
- deterministic evaluation of two selected snapshots or the complete available session timeline for one selected node
- monotonic sample timing, collection-overhead measurements, and missed-sample accounting
- immutable runtime context captured once for each participating or contributing node in a session
- manual phase markers from the dashboard, console, and programmatic API

## Functional Requirements

### Collection control

The feature shall provide a collection control surface from the dedicated performance page.

The user shall be able to:

- start a collection session with an optional custom name
- stop a collection session
- choose the capture interval
- choose the collection duration
- restart a terminal or selected session as a new clean session while retaining the previous session unless it is explicitly deleted
- delete a selected session and remove all collected data from the store
- delete all unpinned sessions at once
- clear all stored performance data so collection can restart from an empty store
- add session tags and notes for later review
- pin sessions that must be excluded from automatic retention
- trigger a one-off manual snapshot collection for immediate diagnostics
- trigger an explicit GC collection for immediate diagnostics
- add a named phase marker to the active session

Feature enablement shall be controlled through application configuration and shall default to disabled. A dashboard or programmatic caller shall not be able to enable a configuration-disabled feature. When enabled, the runtime collection state shall be idle, running, or one of the defined terminal session states.

If no name is supplied, the system shall assign a default session name using the current timestamp in ISO 8601 format. If a name is supplied, it shall be stored and shown in the UI and programmatic results.

When started, the background collector shall begin sampling at the configured interval and shall stop automatically when the configured duration expires. The dashboard shall expose the current state as idle, running, completed, completed with warnings, stopped, or failed. A session shall complete with warnings when collection finishes but one or more known participating nodes report a node-level failure or incomplete collection.

The manual snapshot action shall broadcast a one-off snapshot command through the DevKit Broadcast feature to every current registration in the configured scope, whether or not a session is active. When a session is active, every registered node that accepts the command shall capture and store one immediate snapshot in that session. A node outside the fixed expected participant set shall be recorded as an ad-hoc contributing node. It shall appear in node selection when it contributed data, but shall not join the expected participant set or affect session completion warnings. When no session is active, the action shall create a dedicated one-snapshot session, broadcast the command within the configured application scope, use the immediate accepted delivery responses to determine its participants, and complete the session after the participation window. The default name shall use the form `Manual snapshot — <ISO 8601 timestamp>`. This action is intended for ad-hoc diagnostics and does not replace the scheduled background collection loop.

The manual GC action shall be available whether or not a collection session is active. It shall be broadcast through the DevKit Broadcast feature, and every registered node in the configured scope that accepts the command shall invoke a normal `GC.Collect()` call. When a session is active, the dashboard shall show the immediate per-node broadcast responses, while each accepting node shall record the GC action as a local session marker when it handles the command. When no session is active, the dashboard shall show which registered nodes responded to the broadcast request, shall not wait for handler completion, and shall not create a collection session. The action shall not additionally wait for pending finalizers or request separate LOH compaction. Broadcast `Accepted` means that the receiving node accepted the command for local execution; it does not mean that garbage collection or any other local handler work has completed.

A manual phase marker shall be a session-level timestamped annotation for an operator-observed workload transition such as `warm-up complete`, `load started`, or `load stopped`. Each marker shall contain an internal identifier, session identifier, non-empty trimmed name of at most 100 characters, and creation timestamp in UTC. Duplicate names shall be allowed.

The session store shall atomically verify that the session is still active while adding the marker. The operation shall store the marker once without a Broadcast operation and shall reject an idle or terminal session. Phase markers shall be immutable and shall not have individual edit or delete operations; session deletion, retention, and clear-all shall remove them with the session. They shall appear on every selected-node chart for the session. They shall not restrict evaluation to an interval or alter the snapshots included in evaluation.

The collection lifecycle shall also be available programmatically so application code, background jobs, integration tests, custom developer workflows, or console commands can start and stop sessions directly. The programmatic surface shall support an optional session name and expose the same start, stop, status, restart, deletion, full-storage-reset, phase-marker, snapshot-comparison, node-session-evaluation, portable-archive, and Perfetto-trace export semantics as the dashboard.

The dashboard shall support shareable session URLs. A session link identifies the target session but does not bypass normal access restrictions.

The programmatic control surface shall be resilient to missing infrastructure. If the collector, session store, or required Broadcast integration is not registered or available, calls shall return a safe no-op result or a clear unavailable state, emit a warning log entry, and leave the application running normally.

### Identifiers

Sessions, nodes, and snapshots shall each have:

- an internal `Guid` identifier used for persistence, relationships, handlers, and Broadcast payloads
- an immutable eight-character lowercase alphanumeric key generated with `KeyGenerator.CreateLowercase(8)`

Internal GUIDs are implementation details. Dashboard routes, console arguments, application-facing programmatic APIs, JSON results, logs, and user-facing errors shall use the readable keys. The application-facing control and evaluation services shall resolve readable keys once and use GUIDs internally.

The readable keys shall be treated as practically unique for this local diagnostic feature. No configurable key length, scoped-key model, collision-management protocol, or alternative generator is required.

Hostname and process identifier shall remain node metadata. The readable node key is the displayed and communicated node identity. It shall remain stable for the process lifetime, and a restarted process shall receive a new internal GUID and readable node key.

### Full storage reset

The dashboard and application-facing control service shall provide a clear-all operation for starting again with an empty performance store. This operation is distinct from normal bulk deletion and shall remove all stored performance data from the selected provider, including:

- all terminal sessions, including completed, completed-with-warnings, stopped, and failed sessions
- pinned and unpinned sessions
- runtime snapshots
- node participation records and action markers
- manual phase markers
- immutable node runtime context
- scoped segments
- custom metric observations
- session names, tags, notes, and other persisted session metadata

The clear-all operation shall not change feature configuration, sampling defaults, Broadcast registration, or other application data outside the performance store.

An active logical session shall prevent the operation. The developer must stop the session before clearing the store. The dashboard shall require an explicit destructive-action confirmation that states pinned sessions are included. The console command shall require an explicit `--yes` option; without it, the command shall explain the required confirmation and leave the store unchanged.

The session store shall own atomic lifecycle coordination. The active-session check, clear-all reset, and session creation operation shall use the same coordination mechanism so a concurrent start cannot create a session between the clear check and reset. The Entity Framework provider shall use a transaction and concurrency constraint. The in-memory provider shall implement the same contract with a process-local lock.

The provider shall report success only after the complete reset has committed. When the store is already empty, the operation shall succeed and report that zero sessions and snapshots were removed.

Cleared, deleted, and expired session identities shall remain invalid for future writes. Delayed snapshots, segments, custom metrics, or node updates belonging to an invalid session shall be rejected and shall not recreate that session or leave orphaned performance data.

### Console-command control

When the Profiling integration is registered and the host's Console Commands capability is enabled, the feature shall register a `profiling` command group with `prof` as an alias. These commands shall execute inside the selected running application host through the existing DevKit Console Commands infrastructure. They shall invoke the same application-facing profiling control service as the dashboard and shall not implement a separate collector, session lifecycle, storage path, or Broadcast integration. Keeping the commands registered while runtime collection is configuration-disabled allows `profiling status` and attempted control operations to report that disabled state clearly.

The command group shall provide the following collection-control operations:

- `profiling status` shows feature availability, the current session state, session identity, configured interval and duration, and participant status when a session exists
- `profiling start` starts a deployment-wide session and accepts optional `--name`, `--interval`, and `--duration` options
- `profiling stop` stops the active deployment-wide session
- `profiling snapshot` performs the same one-off manual snapshot action as the dashboard and accepts an optional `--name` for a standalone one-snapshot session
- `profiling gc` performs the same deployment-wide manual GC action as the dashboard
- `profiling mark --name <text>` adds a phase marker to the active session
- `profiling clear --yes` performs the same confirmed full storage reset as the dashboard
- `profiling analyze --session <key> --node <key>` evaluates the complete available timeline for one node in one session
- `profiling analyze --session <key> --node <key> --snapshot-a <key> --snapshot-b <key>` evaluates exactly two snapshots from the same session and node
- `profiling analyze ... --json` writes the same computed evaluation result as JSON instead of the concise terminal presentation

The commands may be invoked directly through an interactive Console Commands host or forwarded to a running host through `bdk host run`, for example:

```text
profiling start --name "warm-up" --interval 1s --duration 30s
profiling status
profiling snapshot
profiling stop
profiling gc
profiling mark --name "load started"
profiling clear --yes
profiling analyze --session a1b2c3d4 --node e5f6g7h8
profiling analyze --session a1b2c3d4 --node e5f6g7h8 --snapshot-a i9j0k1l2 --snapshot-b m3n4o5p6 --json

bdk host run -- profiling start --name "warm-up" --duration 30s
bdk host run -- profiling snapshot
bdk host run -- profiling mark --name "load started"
bdk host run -- profiling stop
bdk host run -- profiling clear --yes
```

A command invoked on one host shall retain the deployment-wide semantics of the corresponding dashboard operation. The selected host is the initiating node; session start, stop, manual snapshot, and manual GC shall still use the configured DevKit Broadcast scope.

`profiling mark` shall add one shared phase marker directly to the active logical session. It shall not broadcast a per-node command. It shall reject a missing or invalid name and shall return a clear no-active-session result without creating a session when collection is idle.

Command options shall use the same validation and defaults as dashboard or programmatic requests. In particular, intervals below 500 ms shall be rejected, duration shall be required after defaults are applied, and a start attempt made while a session is already active shall report the existing active session rather than create another one.

Duration options shall accept the friendly suffixes `ms`, `s`, `m`, and `h`, as well as the standard .NET `TimeSpan` representation. Parsing shall be implemented by a small Profiling-feature-local parser and shall not add or alter a shared Console Commands binder.

Session, node, and snapshot command arguments and output shall use their readable eight-character keys. Command output shall be concise and suitable for terminal use. It shall identify the affected session and resulting state. For Broadcast-backed operations, it shall summarize immediate per-node outcomes using the same accepted, rejected, unsupported, expired, unreachable, and timed-out meanings used by the dashboard. It shall not report handler acceptance as completed local execution.

For `profiling analyze`, omitting both snapshot options selects the complete available node timeline. Supplying exactly one snapshot option shall be rejected; both are required for two-snapshot analysis. The command shall compute and print the result without storing it. JSON is an output representation for console and programmatic use, not a persisted evaluation artifact or dashboard download.

The existing `diag perf` and `diag gc` commands shall remain local, immediate, non-persisted diagnostics. The new `profiling` commands are the persisted, deployment-wide profiling-session operations and shall not change the semantics of the existing diagnostic commands.

If the Profiling feature is disabled, Console Commands are unavailable, or required performance infrastructure is missing, the operation shall fail safely with a clear unavailable or disabled message and shall not change application state. Console commands shall respect cancellation and shall not leave a second logical session running after an interrupted start attempt. A rejected or cancelled clear command shall leave the store unchanged.

### Session ownership and lifecycle

Only one logical collection session may be active for an application deployment at a time. Concurrent logical start attempts shall resolve to the shared active session. An active session shall not be deleted directly and must first be stopped.

Starting a session shall use the session store's atomic lifecycle coordination to create or resolve the shared session identity and then publish a typed start command through the DevKit Broadcast feature. The broadcast shall target the configured application scope or scopes and shall use the registry snapshot that exists when the operation begins. There is no master node; any actively registered node may initiate the operation.

The participation window shall be short because local development is the primary usage scenario. It shall be configurable, with a default of one second, and shall be applied as the broadcast lifetime or caller deadline for the start command. Nodes that return an immediate accepted response before the deadline form the fixed participant set for the session. The initiating control service shall record those accepted nodes as expected participants from the Broadcast result so they remain visible even if local execution fails before a node writes its own participation update. Each accepted node shall atomically replace any older local collector with the new session collector, then confirm or update its participation state in the shared session store. A node that misses or rejects the new start remains a nonparticipant in the new session even if it is still completing an older local session. Nodes registering after the registry snapshot was taken shall not join the session automatically. The configured collection duration shall be measured from the logical session start time rather than independently from each node's arrival time.

Concurrent start attempts shall resolve to the existing active session. They shall not publish an additional start broadcast or create a second session. The dashboard shall navigate to the active session and clearly indicate that only one deployment-wide session can run at a time.

A session reaches a terminal state when either:

- its configured collection duration expires
- it is explicitly stopped by a developer or programmatic caller

Stopping is a best-effort deployment-wide broadcast. The logical session shall be shown as stopped immediately when the stop broadcast operation is accepted by the local control surface. Participating nodes that accept the stop command shall stop collecting. A participating node that does not receive or accept the command may continue collecting until the original session end time, and the store may continue accepting that node's late snapshots until that time. The feature shall not introduce retries, durable command storage, or additional recovery solely for missed stop broadcasts.

The store may accept a late write for a stopped or superseded session only while that session still exists and the write belongs to its original collection window. Once the session is deleted, cleared, or removed by retention, all later writes for that session shall be rejected.

After the configured end time plus the finalization grace period, any node may idempotently finalize the logical session using compare-and-set semantics in the shared store. Finalization shall not depend on the initiating node. Startup reconciliation shall find sessions abandoned past that point and complete the same finalization logic so an initiating-node failure does not leave a session permanently active.

A normally elapsed session shall be distinguishable from a session that was stopped early. The session shall retain its collected snapshots, participating-node information, segments, tags, notes, pin state, and completion metadata after it reaches a terminal state.

Restarting a session shall create a new session identity. When the selected session is active, restart shall first stop it and create the replacement only after the stop operation has been accepted. The previous session shall remain available with a stopped state. The new session shall copy the previous session name with a restart suffix or timestamp, sampling interval, duration, and tags. It shall not copy notes, pin state, snapshots, phase markers, segments, custom metric observations, or node runtime context. Each participating node shall capture fresh runtime context for the replacement. Like every deployment-wide start, the restarted session shall publish a new typed start broadcast and establish its participants again from the current registration snapshot.

### Broadcast integration

The Profiling feature shall consume the standalone DevKit Broadcast feature and shall not implement its own node registry, HTTP receiver, delivery transport, or broadcast polling loop.

The integration shall use typed broadcasts for:

- start session
- stop session
- collect one immediate snapshot
- trigger garbage collection

Broadcast requirements for this feature are:

- the publishing node must be actively registered in the target scope
- the target nodes are the active registrations returned by Broadcast when the operation starts
- the publishing node handles its own broadcast locally without calling its own HTTP endpoint
- the receiving node has exactly one local handler for each performance broadcast type
- immediate delivery results are used only to show which nodes responded and whether the command was accepted, rejected, unsupported, expired, unreachable, or timed out
- Broadcast delivery results are not stored as generic broadcast history
- handler completion is not collected by Broadcast and is not awaited by the performance control operation
- duplicate delivery shall be safe; the node-local performance handler shall not start the same session or perform the same one-off action twice for the same broadcast identity
- broadcast payloads shall contain only the identifiers, timing, and options needed to perform the control action; snapshots and collected metrics shall continue to flow through the performance session store
- multi-node operation requires a shared Broadcast registry provider and directly reachable per-node receiver addresses
- a load-balanced application address is not a valid per-node broadcast address
- a manual snapshot targets every current registration, not only the expected participants of an active session

For local single-process development, the Broadcast feature may use its in-memory registry and local dispatch path. For multi-node usage, the Broadcast feature shall use its shared Entity Framework registry provider and direct HTTP push. The Profiling feature remains independent of how node addresses are resolved or how registrations are maintained.

### Scoped programmatic sessions and segments

Application code shall be able to measure a code section through a scoped session API. The scope shall automatically close when disposed, including when the measured operation fails or is cancelled.

When no session is active, opening a scoped session shall start a new deployment-wide collection session. That scope owns the session and shall stop it when the scope is closed, unless the configured duration has already elapsed.

The configured collection duration remains a safety maximum for scoped sessions. When the duration expires before the measured code section completes, snapshot collection shall stop, while the segment shall remain open until the code scope ends. The segment shall record that collection ended before the operation completed.

When a scoped session is opened while a session is already active, the nested start request shall not create or replace the active session. Instead, the scope shall join the active session and register a named segment within it. Closing such a nested scope shall close only its segment and shall not stop the active session.

The programmatic control surface shall support both raw scopes and execution helpers. A raw scope shall default to a completed outcome unless the caller explicitly marks it as failed or cancelled. An execution helper such as `MeasureAsync(...)` shall determine and record success, failure, or cancellation from the wrapped operation. For failed operations, the segment shall record the exception type and message when available, but shall not persist the stack trace.

A session may contain multiple named segments. Segments are owned by the node that creates them, are timeline annotations, and may overlap. Every segment shall have its own identifier and identify its session and node. A segment may optionally reference a parent segment from the same session and node, but strict nesting shall not be required and independent overlapping segments shall remain valid. Cross-node and cross-session parent references shall be rejected. Each segment shall record, when available:

- segment identifier
- name
- start and end timestamps
- elapsed duration
- outcome, such as success, failure, cancellation, or interruption
- exception type and message for failed measured operations, when available
- optional tags or notes
- correlation or trace identifier
- optional parent segment identifier
- whether collection ended before the measured operation completed

Segments shall be visible in the dashboard and may be shown as markers or highlighted ranges on the session charts so developers can relate runtime behavior to specific code sections.

If a process exits or fails before a scoped segment is closed, that segment shall remain identifiable as incomplete. When the session is finalized, any still-open segment belonging to an incomplete node shall be marked as interrupted rather than silently completed. Its end timestamp may remain unavailable when the actual end moment cannot be established.

### Snapshot frequency and duration

The feature shall allow the developer to configure:

- sample interval, for example 500 ms, 1 s, 2 s, or 5 s
- collection duration, for example 30 s, 1 min, 5 min, 10 min, or longer when explicitly configured
- automatic stop behavior after the configured duration elapses

The sample interval shall be at least 500 ms. Values below 500 ms shall be rejected. Every session shall have a required collection duration that acts as its automatic safety maximum; sessions without an end time are not supported. The default configuration shall remain conservative and lightweight.

Each node-local collector shall execute at most one snapshot capture at a time. When the next scheduled opportunity arrives while the previous capture is still running, the collector shall not overlap the captures. It shall skip that opportunity and increment its cumulative skipped-capture count. A capture that starts but cannot produce a valid snapshot shall increment a separate cumulative failed-capture count.

Snapshot timing shall use a node-local monotonic clock for elapsed durations, rate denominators, sampling delay, and capture duration. UTC timestamps shall remain the display and cross-node alignment timestamps, but UTC clock adjustments shall not affect node-local rate calculations.

The snapshot sequence and successful-, skipped-, and failed-capture counts shall be node-local and reset for each session. The sequence shall increment only after a snapshot is captured successfully. Skipped and failed opportunities shall not consume a snapshot sequence value. When local collection ends, the node participation record shall preserve the final successful, skipped, and failed totals so failures after the last valid snapshot remain visible.

### Profiling overhead benchmarks

The existing `Common.Benchmarks` project shall contain BenchmarkDotNet coverage for the core profiling hot paths: raw system runtime sampling, fixed-source probe and snapshot-model overhead, complete system-backed snapshot capture, GC observation calculation, runtime-context capture, and immutable in-memory snapshot append. Each benchmark shall report execution time and managed allocations through `MemoryDiagnoser`.

On a representative local development machine, the complete system-backed snapshot capture should average below 25 ms, which is five percent of the minimum supported 500 ms sampling interval. Fixed-source probe/model overhead should average below 1 ms, GC observation below 10 microseconds, runtime-context capture below 10 ms, and in-memory snapshot append below 1 ms per snapshot. These are regression-review budgets rather than cross-machine unit-test assertions; exceeding one requires investigation and documented justification because operating-system state and hardware affect benchmark results.

Disabled and idle overhead, scheduled collection at 1 second and 500 ms, missed-opportunity behavior, and capture overlap shall also be measured during final hardening. Benchmark results shall not be persisted by the profiling feature or exposed as a runtime health score.

### Metrics to collect

At minimum, the collector shall capture the following runtime metrics for each snapshot:

- CPU usage percent
- working set bytes
- private memory bytes
- managed memory bytes
- total physical memory bytes
- available physical memory bytes
- used physical memory bytes
- managed heap size bytes
- fragmented bytes
- memory load bytes
- total available memory bytes
- high memory load threshold bytes
- total committed bytes
- total allocated bytes
- allocation rate bytes per second
- cumulative process CPU duration
- logical processor count
- node-local snapshot sequence number
- scheduled monotonic elapsed duration since the logical session start
- capture-start monotonic elapsed duration since the logical session start
- snapshot capture duration measured by the monotonic clock
- cumulative skipped-capture count
- cumulative failed-capture count
- GC collection counts by generation (Gen0, Gen1, Gen2)
- latest GC sequence or index
- latest collected GC generation
- latest GC post-collection managed heap size
- latest GC post-collection LOH size, when available
- whether the latest GC was compacting
- whether the latest GC was concurrent
- latest Gen2 GC sequence or index
- latest Gen2 post-collection managed heap size
- latest Gen2 post-collection LOH size, when available
- whether the latest Gen2 GC was compacting
- whether the latest Gen2 GC was concurrent
- cumulative GC pause duration maintained by the collector
- GC pause percent
- pinned objects count
- finalization pending count
- heap fragmentation percent
- LOH size, fragmented bytes, and fragmentation percent
- memory pressure percent
- server GC mode indicator
- thread pool thread count
- thread pool completed work item count
- thread pool pending work item count
- active TCP connection count
- TCP listener count
- UDP listener count
- total used socket count

The implementation may also capture additional cheap and clearly useful runtime counters, such as:

- process handle count
- process thread count
- thread pool available worker threads and completion-port threads
- GC latency mode
- operating-system memory information when available

Request counts, request timings, route diagnostics, and other HTTP-request analytics are outside the scope of this feature.

Metrics unavailable on the current runtime, operating system, or hosting environment shall be represented as unavailable rather than silently reported as zero. Sessions may therefore contain different available metric sets across nodes.

Wall-clock timestamps shall use UTC. The dashboard shall preserve each node's original snapshot timestamp and tolerate small clock differences between nodes. Monotonic values are meaningful only within one node process and session and shall never be compared across nodes.

Derived metrics such as CPU usage, allocation rate, and percentages shall be calculated consistently from consecutive samples using monotonic elapsed time. The exact runtime and operating-system APIs used to obtain the values remain an implementation decision.

### Runtime context

The collector shall persist immutable runtime context once for each node that becomes an expected participant or ad-hoc contributor in a session. The context shall include, when available:

- application name
- entry assembly informational version
- .NET runtime description and version
- operating-system description
- operating-system architecture
- process architecture
- server or workstation GC mode
- logical processor count
- process start timestamp in UTC
- whether a debugger was attached when the node context was created

Unavailable context fields shall be represented as unavailable. Command-line arguments, environment-variable values, filesystem paths, and other potentially sensitive machine data shall not be captured.

### Custom metrics integration

The dashboard shall support custom application metrics in addition to the standard runtime metrics.

Custom metrics shall use the existing DevKit metrics abstraction rather than introduce a second application-facing metrics API. When a performance session is active, supported metric observations shall be associated with that session. When no session is active, the integration shall add negligible overhead and shall not retain session data.

Supported custom metric forms shall include at least:

- counters or incremental totals
- current gauges or live values
- duration measurements or tracked scopes

Custom metric names shall be stable identifiers defined by the application or feature. Dynamic metric names and arbitrary high-cardinality dimensions are outside the supported model. No separate dashboard-specific metric registration step shall be required; supported observations shall be captured through the existing DevKit metrics abstraction using their stable identifiers.

Custom metric observations shall be persisted independently from runtime snapshots and linked to the active session, producing node, and optional ambient segment. An observation shall inherit the current ambient segment identifier when one exists, without requiring the caller to pass a segment identifier explicitly. Observations emitted outside a segment shall remain session-level.

Custom metrics may be displayed in an optional generic panel without changing the requirement for the two primary runtime charts. The panel shall use the stable metric identifier as the display name and show the recorded values and timestamps. Metric kind, unit, or segment association may be shown when that information is already available from the existing metrics observation, but no separate dashboard registration or descriptive metadata model is required.

### Snapshot payload

Each snapshot shall include:

- snapshot identifier
- timestamp in UTC
- node identifier
- machine or container hostname
- process identifier
- collection session identifier
- node-local snapshot sequence number
- scheduled and capture-start monotonic elapsed durations
- monotonic capture duration
- cumulative skipped- and failed-capture counts
- collected runtime metric values for memory, CPU, GC, allocations, thread pool, and sockets

Custom metric observations and immutable node runtime context shall not be embedded repeatedly in runtime snapshots. Custom metric observations shall not be included in normal snapshot JSON. Session name, tags, notes, phase markers, segments, pin state, node runtime context, and other session-level metadata shall be stored separately rather than duplicated into every snapshot.

The snapshot shall persist internal session, node, and snapshot GUIDs and their readable keys according to the identifier rules. Hostname and process identifier are metadata, not the node identifier. The node identity shall remain stable for the lifetime of that application process. An application restart shall appear as a new node instance.

The dashboard shall display a clear indication of when the data was captured and which node produced it.

### Multi-node behavior

A collection session shall be deployment-wide within one or more configured Broadcast scopes. Starting a session shall use a Profiling-owned adapter over the DevKit Broadcast feature to read the current active registration snapshot and directly push the collection-start command to every registered target node. The standalone Broadcast service, its contracts, and its general behavior shall remain independent of Profiling and shall not gain Profiling-specific APIs. Nodes do not poll the performance store or Broadcast registry for commands.

Before creating a session or broadcasting a session start or standalone manual snapshot, the control service shall determine the current target count. If more than one node is targeted and the configured performance session provider reports `SupportsMultiNode = false`, the operation shall be rejected with a shared-store-required result before any store mutation or broadcast is sent. This validation shall not block manual GC because that action does not persist a session.

The Profiling feature shall use the node identity and delivery outcomes supplied by Broadcast. Its adapter shall freeze the target registrations before validation and shall delegate delivery to the unchanged Broadcast service through that fixed target view, without rereading the live registry. Nodes that return `Accepted` within the participation deadline become the fixed participant set. Registered nodes that return another response or do not respond may be shown in the immediate start-operation delivery summary but are not session participants. The Broadcast feature does not persist that delivery summary as broadcast history. A node registered after the target snapshot was read shall not join the session automatically.

Each participating node shall record its participation and collect and store its own node-identified snapshots. The session shall record collection status independently for each participant. A participating node that fails during collection or does not complete shall not stop the remaining nodes from collecting. Partial completion and node-level failures shall be visible in the session view.

A session that reaches its end while one or more known participating nodes failed or remained incomplete shall be marked completed with warnings. A session shall be failed only when no meaningful collection occurred or the logical session itself could not be established or maintained.

The dashboard shall present one selected node at a time. It shall not calculate an aggregate deployment view or attempt to identify a best, worst, fastest, or slowest node. Developers may switch between expected participants and ad-hoc contributing nodes to inspect their individual timelines.

Manual snapshots and deployment-wide diagnostic actions shall use typed DevKit Broadcast commands. During an active session, a registered nonparticipant that accepts a manual snapshot shall store an ad-hoc snapshot under that session. The node shall then appear in node selection, but shall not be added to the expected participant set or affect completion warnings. The dashboard shall show the immediate per-node delivery responses, while local action completion remains outside Broadcast.

### Refresh behavior

The dashboard shall support a configurable refresh interval, following the same pattern as other dashboard pages.

Supported refresh modes shall include:

- off
- 1 second
- 5 seconds
- 10 seconds
- 15 seconds
- 30 seconds
- 60 seconds

Immediately left of the refresh selector, the dashboard shall provide a compact, tooltip-described host-local stress action. It shall start the default 30-second background workload and return without holding the HTTP request open. The workload shall use dedicated CPU workers and sustained managed plus large-object allocations, retain a bounded memory set, and force a full GC while the retained set remains reachable so CPU, allocation, memory, LOH, GC, and post-GC evidence are observable. Its complete background execution shall be recorded as a named `Profiling stress test` segment, and that segment name shall identify its highlighted range on both primary charts. The programmatic `IProfilingStressService` surface shall accept a per-run duration, CPU-worker count, and retained-memory size so custom developer workflows can reuse the workload, while the dashboard action shall always pass the built-in defaults and shall not expose editable profiles. Only one workload may run in a process at a time; overlapping requests shall be rejected. The workload shall stop during host shutdown and shall not introduce saved profiles or recurring schedules.

## Dashboard Requirements

### Dashboard layout

The dashboard view shall include:

- a status section showing whether collection is idle, running, completed, completed with warnings, stopped, or failed
- controls for start, stop, duration, interval, manual snapshot collection, manual GC collection, and adding a phase marker
- a compact fixed-duration process stress action immediately left of the refresh interval
- a current-snapshot summary in a compact card grid
- exactly two primary charts: a Memory History chart and a GC Pressure History chart
- a node selector
- session selection and metadata controls
- a clear-all control for resetting the complete performance store
- an analysis panel with an **Analyze now** action and browser-wide **Live analysis** switch
- a compact current sampling-status summary for the selected node

The current-snapshot cards shall display the following runtime indicators when available:

- CPU usage and process identity
- application version, system platform, runtime version, GC mode, and debugger-attached state from the immutable node context
- physical memory availability and usage
- process private memory and working set
- managed memory, heap size, and committed memory
- heap and LOH fragmentation
- LOH size
- memory pressure and runtime memory-load information
- GC collection counts by generation
- GC pause, pinned-object, and finalization information
- allocation rate and total allocated bytes
- socket activity counts
- thread pool status
- latest capture duration and sampling delay, with cumulative skipped- and failed-capture counts

Unavailable indicators shall be clearly shown as unavailable rather than as zero.

The current sampling-status summary shall use the latest persisted snapshot and participation totals already returned by the normal dashboard query. Full sampling coverage and p95 timing KPIs shall be computed only as part of an explicit or Live analysis request, so displaying the dashboard does not bypass the Live analysis switch.

Periodic refresh shall update only live data regions such as collection status, current metrics, chart data, available snapshot choices, segments, and custom metrics. It shall not replace the complete Profiling content tree. User-owned interaction state—including the active workbench tab, focused controls, typed but unsaved text, selected comparison snapshots, archive file selection, expanded details, analysis results, chart filters, scroll position, and the Live analysis switch—shall survive background refresh. A deliberate session or node context change may reset context-specific controls and analysis because they no longer describe the selected context.

### Visualizations

The dashboard shall present exactly two primary runtime charts:

- **Memory History:** a time series of managed memory, heap size, private memory, working set, committed memory, and LOH size
- **GC Pressure History:** a time series of CPU usage, memory pressure, heap fragmentation, LOH fragmentation, GC pause, and allocation rate

The charts shall use the dashboard's standard charting technology and shall be optimized for short diagnostic windows. Standard Plotly interaction controls shall remain available, including drag zoom, wheel zoom, box selection, pan, autoscale, and reset. Named session segments shall be rendered as markers or highlighted time ranges so runtime changes can be related to measured code sections.

The chart X axes, segment ranges, phase markers, action markers, and every snapshot selector shall render in the browser's local time while persisted timestamps and API contracts remain UTC. Snapshot options shall show the sequence, full local date and time including milliseconds, and readable snapshot key. Rotated marker labels shall have sufficient top margin and positioning headroom to remain visible rather than being clipped by the plot boundary.

Each chart panel shall include:

- a concise panel title
- a clear legend
- readable units

The chart headers shall not repeat the selected node, session, or retained-point count because that context is already visible in the session selector and sampling summary.

The written layout requirements define the intended information density and visual direction: a compact current-snapshot card grid followed by two full-width history panels.

### Developer productivity features

The dashboard shall provide tools that help developers inspect diagnostic sessions:

- editable session name, tags, and note
- selection of exactly two snapshots from the current session and selected node for side-by-side comparison in a table
- deterministic analysis of those two snapshots above the raw comparison table
- deterministic analysis of the complete available selected-node timeline in a dedicated **Analysis** tab
- per-metric earlier value, later value, absolute difference, and percentage difference in that comparison table
- percentage differences shown as unavailable when the earlier value is zero or the calculation is otherwise not meaningful
- plain-language inline help explaining the meaning and unit of each snapshot card, chart, evaluation KPI, signal, signal-evidence value, and raw comparison metric
- visible information icons whose tooltips work with pointer hover and keyboard focus, expose equivalent accessible names to assistive technology, and retain readable fallbacks when a metric identifier is application-defined
- export of normal runtime snapshots for the selected node or the complete session as JSON
- download of a complete terminal session or one selected immutable snapshot as a portable JSON archive
- export of a complete terminal session as Perfetto-compatible Trace Event JSON for visual investigation
- upload and atomic import of a portable JSON archive as a fresh terminal session
- copy the current snapshot to the clipboard as JSON
- bookmarkable session and node selections
- named phase markers for annotating operator-observed workload transitions

The feature shall not provide session-to-session comparison, cross-node snapshot comparison, baseline designation, configurable thresholds, production alarms, health scoring, or automatic node ranking. Snapshot comparison is limited to exactly two explicitly selected snapshots within the currently selected session and node and is presented as a table rather than as an overlaid chart. The comparison shall show earlier and later values, absolute differences, and percentage differences where they can be calculated safely.

### Dashboard screen details

The selected session, selected participant, a compact metadata action, and an icon-based **Session tools** toolbar shall remain above the workbench tabs. The metadata action shall open the standard dashboard dialog for viewing and editing the selected session's name, tags, note, and pinned state; metadata fields shall not remain visible in the normal workbench. Toolbar controls shall expose descriptive accessible names and hover tooltips. Destructive and archive-import operations may be grouped into a compact overflow panel.

The dashboard shall use four workbench tabs with the following visual structure:

- **Overview**: a top **Current Snapshot** panel with a dense card-grid layout, followed by the full-width **Memory History** and **GC Pressure History** panels
- **Comparison**: exactly-two-snapshot selection, deterministic pair analysis, and raw metric deltas
- **Analysis**: complete selected-node timeline analysis with **Analyze now** and the browser-wide **Live analysis** switch
- **Info**: supporting segment and custom-metric detail, with immutable runtime capture context available from a compact information-icon action that opens the standard dashboard dialog

The Current Snapshot panel shall clearly show the selected node identity and the snapshot timestamp. Cards shall emphasize one primary value and use smaller supporting lines for related values.

The Memory History panel shall plot memory series on a shared timeline.

The GC Pressure History panel shall plot percentage-based pressure series and allocation activity on a shared timeline. Multiple units may use separate axes where necessary, provided the chart remains readable.

Manual phase markers shall be shown as labeled vertical markers on both primary charts for every selected node. Node-owned scoped segments and node action markers shall remain distinguishable from shared manual phase markers.

The dashboard shall not include request-throughput, request-latency, route, or slow-request panels.

### Node-aware drill-down

The dashboard shall allow the user to:

- select one expected participant or ad-hoc contributing node from a dropdown control
- focus on that node's current snapshot and timeline
- switch between participating nodes without changing the selected session
- preserve the current node selection and dashboard filters across page reloads using browser localStorage

No aggregate-node view or automatic node ranking is required.

### Session view

The dashboard shall clearly show the active or selected session and allow the user to:

- inspect the current or completed snapshot window
- inspect the latest snapshot for the selected node
- restart a terminal or selected session as a new clean session without deleting the previous session
- delete the session and all associated data
- delete all unpinned sessions
- clear the complete performance store after explicit confirmation, including pinned sessions and all associated records
- select a previously saved or completed session
- open a session directly from a shareable URL

The **Start** control shall be disabled while a session is running. The **Stop** control shall be disabled when no session is running.
- trigger a manual one-off snapshot collection
- trigger a manual deployment-wide GC collection
- add a named phase marker while the selected session is active
- inspect named code segments and their start, end, duration, outcome, optional parent, and correlation metadata
- inspect the immutable runtime context and sampling quality for each selected node
- edit the session name, tags, notes, and pin state without changing collected snapshots or metric values
- export normal runtime snapshots for either the currently selected node or the complete multi-node session as JSON
- download a portable archive for the complete terminal session or any selected immutable snapshot
- export a complete terminal session as a one-way Perfetto trace whose filename contains the readable session key
- upload a portable archive from the browser and select the newly imported terminal session
- explicitly analyze the selected node on demand, even when automatic live analysis is off

Snapshot export shall use JSON only. A selected-node export shall contain the normal runtime snapshots collected for that node. A complete-session export shall contain the normal runtime snapshots collected across all expected participants and ad-hoc contributing nodes. The node identity is part of each exported snapshot. Session metadata, phase markers, segment records, custom metric observations, node runtime context, and other auxiliary records are not included in snapshot exports.

Computed evaluation results shall not be offered as a dashboard export, copy, or download. This restriction does not alter the normal raw-snapshot JSON export.

### Portable session archives

Portable archives are separate from the existing raw snapshot JSON export. Raw snapshot exports remain inspection-only JSON arrays and are not accepted for import. A portable archive shall use JSON with the fixed format identifier `bitdevkit.profiling.archive`, integer compatibility version `1`, and kind `session` or `snapshot`. The maximum accepted or produced archive size is 25 MiB. Compression, encryption, archive migration frameworks, configurable format versions, automatic backup, directory watching, and cloud storage are outside scope.

A complete-session archive may be exported only after the source session reaches a terminal state. It shall contain the complete stored session graph: session metadata, contributing nodes without private Broadcast correlation, participations, runtime contexts, snapshots, phase markers, node action markers, measured segments, and custom metric observations. Segment-parent and metric-to-segment relationships shall use archive-local references. Internal GUIDs, Broadcast identities, computed evaluations, stack traces, and provider details shall not be serialized.

An immutable snapshot may be exported while its source session is active. Its archive shall contain exactly that snapshot and the minimum source session, node, participation, and runtime-context data needed for normal inspection. Import shall represent it as a new `Completed` one-snapshot session named `Imported snapshot — <source session name> — #<sequence>`. Existing evaluation behavior shall report limited or insufficient evidence naturally; import shall not synthesize additional snapshots or evaluation results.

Import shall deserialize only the fixed archive contract, reject unknown properties and unsupported format, version, or kind values, validate all identities, timestamps, enum values, and relationships before mutation, and store the complete graph atomically. Every import shall generate fresh internal GUIDs plus fresh eight-character lowercase session, node, and snapshot keys through `KeyGenerator.CreateLowercase(8)`. It shall never preserve private node correlation, overwrite existing records, merge with another session, become the active session, or allow a collector to resume it. Repeated import of the same archive shall create independent terminal copies. Normal retention and pinning rules apply after import.

`IProfilingArchiveService` shall provide caller-owned stream operations for complete-session export, selected-snapshot export, and import. Both the in-memory and Entity Framework stores shall support one atomic imported-session graph mutation using the existing physical data model; this addition shall introduce no new table or migration. Console export shall write through a temporary sibling file and replace an existing destination only when `--overwrite` is explicit. Dashboard import shall accept a browser file upload only and shall never accept a server filesystem path.

### Perfetto visualization traces

Perfetto export is separate from portable archives and shall use the Perfetto-compatible Trace Event JSON object form with a `traceEvents` array. It is a one-way visualization representation and shall never be accepted by archive import. A trace may be exported only after the complete source session reaches a terminal state.

The trace shall use session-relative microsecond timestamps, one session lane for shared phase markers, and one synthetic process per profiling node so equal operating-system process identifiers on different hosts cannot collide. Normal numeric snapshot metrics and custom numeric observations shall become counter events, phase markers and node action markers shall become instant events, and completed measured segments shall become duration events. Incomplete segment evidence shall remain visible as an instant event rather than receive a manufactured duration. Session metadata and immutable runtime context shall remain available in trace event arguments.

The export shall preserve readable session, node, and snapshot keys while excluding internal GUIDs, private Broadcast correlation, persisted-provider details, and computed evaluation results. It shall not claim to contain sampled call stacks, method-level CPU attribution, allocation stacks, or flame-graph evidence. `IProfilingPerfettoExportService` shall expose the caller-owned stream operation. The dashboard shall offer a compact tooltip-described action next to the selected session, and `profiling export --format perfetto` shall reuse the same service. Archive remains the default console format for backward compatibility; Perfetto format shall reject node or snapshot selectors.

Imported sessions appear in the ordinary session list and use the existing selection, inspection, metadata, deletion, retention, and one-session/one-node evaluation behavior. This portability capability does not add session-to-session comparison.

Session metadata shall use zero or more plain string tags and one optional free-text note. Tag hierarchies, key/value labels, comment threads, and metadata history are outside scope.

Collected snapshots and metric observations shall remain immutable after they are written. Descriptive session metadata may remain editable after completion.

## Deterministic Evaluation

### Purpose and boundaries

The feature shall explain observed changes with deterministic, programmatic calculations. It shall use no AI model, external service, network lookup, learned model, or free-form generated interpretation.

Evaluation shall operate on exactly one selected node and one session. It shall not compare nodes, rank nodes, calculate deployment-wide aggregates, compare sessions, or evaluate an arbitrary interval.

The two supported modes are:

- **two-snapshot comparison:** exactly two selected snapshots from the same session and node
- **node-session analysis:** the complete available snapshot timeline for the selected node in the selected session

Node-session analysis may run while collection is active. Such a result shall be marked provisional and shall represent the complete timeline available at calculation time. Completed, completed-with-warnings, stopped, and failed sessions remain analyzable, with limitations and confidence reflecting incomplete data. A normally completed session produces a terminal result.

Evaluation results shall be calculated server-side on demand through one application service reused by the dashboard, console command, and application-facing programmatic API. Results shall never be persisted. The browser shall render the returned contract and shall not independently implement analysis rules.

### Result contract

The result shall contain only the following top-level groups:

- `Scope`: mode, session key, node key, optional snapshot keys, evaluated UTC time range, snapshot count, and whether the result is provisional
- `DataQuality`: sufficiency state, available or missing inputs, sampling coverage, skipped and failed captures, capture overhead, and sampling delay
- `KPIs`: independently calculated values and changes
- `Signals`: zero or more evidence-backed interpretations
- `Limitations`: zero or more short deterministic statements describing material analysis constraints

No analyzer version, overall score, health state, critical label, or combined risk value is required.

KPI cards shall always be returned when their inputs are available, even when there are too few samples for interpretive signals. The primary KPIs shall include:

- CPU average, peak, ending value, and first-half versus second-half change
- managed heap, private memory, and LOH starting value, ending value, absolute change, percentage change, and peak
- ending heap and LOH fragmentation and their percentage-point changes
- allocation-rate average, peak, and first-half versus second-half change
- GC pause burden, Gen0/Gen1/Gen2 count deltas, and collection rates
- sampling coverage, skipped- and failed-capture counts, p95 capture duration, and p95 sampling delay

In two-snapshot mode, the equivalent KPI values shall use snapshot A, snapshot B, and their delta rather than timeline halves. Thread-pool and socket values may be included as raw KPIs but shall not generate automatic signals. Custom metrics shall not generate generic diagnostic signals.

Signals shall use only the labels `Notable` and `Investigate`. Each signal shall contain:

- a stable signal identifier
- one label
- one short deterministic explanation
- the raw evidence values and thresholds that caused it
- confidence of `Low`, `Medium`, or `High`
- one short deterministic suggested action

When no rule matches, the result shall contain no signals and the UI shall show `No notable behavior detected.` This does not assert that the application is healthy.

### Data sufficiency and confidence

A node-session timeline requires at least five snapshots spanning at least five seconds before interpretive signals are emitted. Before that point, the result shall return available KPIs and the message `Collecting enough data for analysis.`

A two-snapshot comparison is available immediately, but every interpretation from that mode shall have `Low` confidence.

Confidence for node-session signals shall be assigned as follows:

- `Low`: required supporting inputs are missing or the evidence is only a two-snapshot comparison
- `Medium`: at least five snapshots over at least five seconds are available and the primary rule threshold is met
- `High`: at least ten snapshots over at least ten seconds are available, all inputs for the rule are present, the condition is sustained, and at least one independent supporting condition is present

A possible-retention signal may be `High` only when a Gen2 collection occurred and the collector's direct post-collection Gen2 heap evidence remains elevated.

Missing or unavailable metrics shall reduce confidence or suppress rules that require them. A stopped, failed, or completed-with-warnings session shall include a limitation and shall not be prevented from evaluation. A debugger-attached node context shall add the limitation `Debugger attached; timing and runtime behavior may be affected.`

### Calculations

Calculations shall prefer cumulative-counter deltas divided by node-local monotonic elapsed time:

- CPU percentage = `100 × CPU-duration delta ÷ monotonic elapsed duration ÷ logical processor count`
- allocation rate = `total-allocated-bytes delta ÷ monotonic elapsed seconds`
- each GC rate = `generation collection-count delta ÷ monotonic elapsed seconds`
- GC pause burden = `cumulative GC-pause-duration delta ÷ monotonic elapsed duration`

Counter resets, negative deltas, non-increasing monotonic elapsed values, duplicate node-local sequence numbers, and missing samples shall be treated as data-quality limitations. Invalid intervals shall be excluded rather than converted to zero. UTC clock movement shall not invalidate an otherwise valid node-local interval.

Per-snapshot rate fields may be used for peaks and trends, but session averages and sustained conditions shall use cumulative deltas where possible. Averages over a timeline shall be time-weighted when intervals differ materially.

For timeline rules, the first and second halves shall be divided by elapsed time at the temporal midpoint. A sustained condition means at least 60% of valid sampled intervals meet its stated threshold unless a rule explicitly defines another proportion.

For a node-session timeline, growth rules compare the earliest and latest valid snapshots. For two-snapshot analysis, snapshot A is the baseline and snapshot B is the later observation; the request shall be rejected if their node-local sequence establishes the reverse order. Memory and allocation changes described as meaningful shall require both their relative threshold and absolute floor. Percentage changes with a zero or unavailable baseline shall remain unavailable.

Sampling quality shall be calculated as follows:

- sampling coverage = successful captures divided by successful, skipped, and failed capture attempts in the evaluated scope
- capture overhead = snapshot capture duration divided by the configured sampling interval
- sampling delay = the non-negative difference between capture-start and scheduled monotonic elapsed durations
- p95 values use the nearest-rank percentile over available snapshots

Complete node-session analysis shall use the node participation record's final totals when collection is terminal and its latest available cumulative totals while collection is active. Two-snapshot analysis shall use snapshot sequence and cumulative-counter deltas between A and B.

The evaluator shall add a data-quality limitation when any capture failed, sampling coverage is below 90%, p95 capture overhead is at least 25%, or p95 sampling delay is at least 50% of the configured interval. A signal shall not receive `High` confidence while any of these sampling-quality limitations is present or a debugger was attached. These thresholds are fixed evaluator defaults and are not performance-health signals.

### Fixed interpretation rules

The evaluator shall use fixed built-in rules. They shall not be configurable, replaceable, extended through plugins, or persisted as data. No rule versioning is required. The same inputs evaluated by the same application build shall produce the same result.

Each bold rule name below shall also define its stable lowercase kebab-case signal identifier.

CPU rules:

- **sustained CPU:** session-average CPU is at least 70% and at least 60% of valid intervals are at or above 70%
- **strong sustained CPU:** session-average CPU is at least 85% and at least 80% of valid intervals are at or above 80%
- **rising CPU:** the second-half average is at least 20 percentage points above the first-half average and the timeline ends at or above 70%
- **two-snapshot CPU rise:** snapshot B is at least 20 percentage points above snapshot A and is at or above 70%; confidence is `Low`
- an isolated CPU peak shall not produce a signal

Memory rules:

- **managed heap growth:** managed heap increases by at least 20% and at least 32 MiB
- **possible retention:** managed heap meets the growth rule, at least one Gen2 collection occurred, and the collector's latest Gen2 post-collection managed heap still exceeds the starting heap by at least 20% and at least 32 MiB
- **unexplained process-memory growth:** private memory increases by at least 20% and at least 64 MiB, while the positive managed-heap delta explains less than half of the private-memory delta
- **LOH growth:** LOH size increases by at least 20% and at least 32 MiB
- **LOH fragmentation:** ending LOH fragmentation is at least 20% and increased by at least 10 percentage points

Allocation rules:

- **rising allocation:** the second-half average allocation rate is at least twice the first-half average and increases by at least 10 MiB/s
- **sustained allocation:** average allocation rate is at least 50 MiB/s
- **allocation churn:** sustained allocation is present, Gen0 collection frequency is at least one collection per two seconds, and managed heap growth does not meet its meaningful threshold
- **allocation with heap growth:** sustained allocation and managed heap growth are both present
- **two-snapshot allocation rise:** snapshot B is at least twice snapshot A and increases by at least 10 MiB/s; confidence is `Low`

GC rules:

- **notable GC pause:** cumulative pause duration is at least 5% of evaluated wall time
- **strong GC pause:** cumulative pause duration is at least 10% of evaluated wall time
- **frequent full GC:** at least two Gen2 collections occur and the average Gen2 rate is at least one per ten seconds
- **GC pressure:** notable GC pause is present together with frequent full GC, sustained allocation, managed heap growth, or LOH growth
- **strong GC pressure:** strong GC pause is present, or notable GC pause is present with at least two supporting conditions
- Gen0 and Gen1 rates are KPI and supporting evidence only
- two-snapshot GC interpretation shall use cumulative-counter deltas and shall have `Low` confidence

If a stronger and weaker rule describe the same evidence, the evaluator shall emit the stronger signal only.

The label and suggested action shall be fixed by signal identifier:

- sustained or rising CPU: `Notable` — `Capture a CPU profile and inspect hot methods.`
- strong sustained CPU: `Investigate` — `Capture a CPU profile and inspect hot methods.`
- managed heap or LOH growth: `Notable` — `Compare heap types and retained sizes.`
- possible retention: `Investigate` — `Inspect retained object roots after Gen2.`
- unexplained process-memory growth: `Investigate` — `Review native allocations and memory mappings.`
- LOH fragmentation: `Notable` — `Inspect large-object allocation and reuse patterns.`
- rising or sustained allocation: `Notable` — `Inspect the highest allocation hot paths.`
- allocation churn: `Investigate` — `Reduce short-lived allocation in hot paths.`
- allocation with heap growth: `Investigate` — `Inspect allocations that remain reachable.`
- notable GC pause or frequent full GC: `Notable` — `Inspect GC events and heap pressure.`
- strong GC pause, GC pressure, or strong GC pressure: `Investigate` — `Inspect GC pauses, allocations, and retained heap.`

### Dashboard live analysis

The session Analysis panel shall place **Analyze now** and a **Live analysis** switch in its header. The switch shall default to off and shall be stored browser-wide in `localStorage`, so it persists across page reloads and session changes in the same browser profile. It is independent per browser profile and is not application configuration or server-side user preference.

When the switch is off, the dashboard shall perform no automatic evaluation calls; **Analyze now** shall remain available. When it is on, the dashboard shall request a new provisional analysis only after a new snapshot is observed, shall allow no overlapping evaluation calls, and shall run no more frequently than the configured dashboard refresh cadence. Turning the switch off shall prevent future automatic calls without cancelling or deleting an already returned result. The switch controls only dashboard-initiated automatic evaluation and does not disable console or programmatic evaluation.

The panel shall show KPI cards first, then noteworthy signals and their short actions, followed by limitations. In two-snapshot mode, the equivalent analysis shall appear above the authoritative raw delta table. Raw snapshots and raw deltas remain the authoritative evidence.

## Storage Model

### Provider model

Session and snapshot storage shall use a provider abstraction so applications can select storage appropriate to their development and deployment model.

Every provider shall expose a `SupportsMultiNode` capability. The in-memory provider shall report `false`; the shared Entity Framework provider shall report `true`.

The feature shall provide:

- an Entity Framework provider as the first-class durable provider
- an in-memory provider for tests and frictionless local development
- extensibility for additional providers without changing the application-facing collection and dashboard model

The in-memory provider is intentionally ephemeral. Its sessions remain available only while the application process is running and are lost on restart. It is suitable for local diagnostics where durability is not required.

A durable provider shall preserve sessions, snapshots, phase markers, segments, custom metrics, immutable node runtime context, notes, tags, pin state, and node participation data across page refreshes, application restarts, and later review. Computed evaluation results shall not be stored by any provider.

Multi-node sessions require a shared provider accessible to all participating nodes. The in-memory provider shall not be presented as supporting deployment-wide multi-node collection across independent application processes.

The Entity Framework provider shall use six physical Profiling tables:

- sessions as the aggregate root
- stable process-lifetime nodes
- mutable node participations
- immutable runtime snapshots
- immutable custom metric observations
- invalidated session identities retained as deletion/reset tombstones

Session tags, immutable node runtime contexts, phase markers, action markers, and measured segments with their tags are session-owned records. They shall be mapped as owned collections in JSON columns on the session row rather than as separate physical tables. Runtime snapshots shall remain separate rows because they form the high-volume, independently indexed timeline. Mutable node participations shall remain separate because their capture counters are updated on the collection hot path and independent nodes must not rewrite or contend on the complete session JSON document. Custom metric observations shall remain separate immutable rows because their volume can grow independently and appending one observation must not rewrite an expanding JSON array. Nodes shall remain separate because one stable process-lifetime node can participate in multiple sessions. Invalidated identities shall remain separate because their tombstones must survive session deletion.

Every mutation of a session-owned JSON collection shall advance the session aggregate concurrency token and execute through the provider's bounded transaction/retry path. Participation rows shall retain their own optimistic concurrency token. The store shall validate referenced session and node identities before changing an owned collection; JSON ownership does not replace these application-level integrity checks. Consuming DbContexts shall invoke the Profiling model-builder configuration from `OnModelCreating`. The consuming application continues to own migrations.

### Retention rules

The feature shall remain bounded and shall not be designed as a long-term archive. Retention shall apply automatically according to configurable limits.

Snapshot count shall not be capped per node or session when the selected provider can persist the data. Session-level retention shall remain bounded through the following defaults:

- maximum retained completed sessions: 20
- maximum session age: 7 days

Sessions may be pinned by a developer. Pinned sessions shall be excluded from automatic retention so important diagnostic records can be retained.

When a retention limit is reached, the oldest unpinned completed sessions shall be removed first. Active sessions shall never be removed automatically. Manual deletion shall remain available for both pinned and unpinned sessions, subject to authorization. The explicitly confirmed clear-all operation may remove all terminal sessions, including pinned sessions.

## Architecture

The feature shall follow a small, separated architecture:

- a background collector running inside each participating application process
- the standalone DevKit Broadcast feature for node registration, registry lookup, direct HTTP push, local self-delivery, and immediate per-node delivery responses
- one local performance broadcast handler per supported control command
- a session store for sessions, node participation, snapshots, phase markers, segments, custom metrics, immutable node runtime context, and metadata, including an atomic full-reset operation
- a provider-neutral portable archive service and one atomic imported-session graph operation on each store provider
- a provider-neutral, one-way Perfetto trace export service that reads stored terminal-session evidence without changing persistence
- atomic session-lifecycle coordination implemented by each store provider
- a startup reconciler and idempotent session finalizer
- a dashboard query layer that loads one selected session and node timeline
- an application-facing control surface for programmatic sessions, phase markers, and scoped segments
- a pure application evaluation service shared by the dashboard, console commands, and programmatic API
- grouped console commands that delegate to the application-facing control surface
- an integration with the existing DevKit metrics abstraction for stable custom metrics
- a dashboard page for control, session selection, current values, deterministic evaluation, snapshot comparison tables, raw snapshot export, and metadata editing
- browser-only archive download and upload controls that reuse the provider-neutral archive service

The collector shall remain independent from dashboard rendering so collection continues without an open browser page.

Profiling shall participate in the existing dashboard's automatic plugin discovery. A host enables Profiling with `AddProfiling(...)` and enables the dashboard once with the normal `services.AddDashboard(...)` call. No Profiling-specific `AddDashboard` extension, registration marker, or duplicate dashboard registration shall be required. When both features are enabled, the Profiling navigation page and endpoints shall be discovered automatically; when Profiling is disabled, its navigation page shall remain hidden.

The Profiling feature shall not implement message polling, its own node registry, heartbeat infrastructure, delivery transport, deployment-wide metric aggregation, request-diagnostics infrastructure, or session-to-session comparison infrastructure. Node registration and optional low-frequency registration leases belong to the Broadcast feature.

## Configuration and Defaults

The feature shall be configured through application options and environment settings.

Defaults:

- enabled: false
- minimum sampling interval: 500 ms
- sampling interval: 1 second
- duration: 30 seconds and required for every session
- automatic stop: true
- default session name: ISO timestamp
- max retained completed sessions: 20
- max session age: 7 days
- refresh interval: 5 seconds
- dashboard Live analysis: off, stored browser-wide in localStorage
- storage provider: in-memory by default for local development; Entity Framework for durable or multi-node usage
- broadcast scope: configured for the application host and shared by its replicas
- broadcast integration: in-memory registry/local dispatch for single-process development; shared Entity Framework registry plus direct HTTP push for multi-node usage
- start-command participation deadline: 1 second
- session-finalization grace period: 1 second
- gc trigger: available from the dashboard
- programmatic control: enabled through an application service interface
- console-command control: registered when the performance integration is present and the host Console Commands capability is enabled; control operations report unavailable when runtime collection is configuration-disabled
- evaluation rules and thresholds: fixed built-in defaults with no configuration or extension surface

## Security and Operational Notes

- the feature is intended primarily for application developers and local or controlled development environments
- it shall be opt-in and disabled by default
- applications should normally keep the feature completely disabled in production
- when enabled, dashboard access and control operations shall follow the host application's normal authorization model
- command-line access shall follow the host's existing Console Commands registration, exposure, and protection model
- clearing the complete performance store shall require explicit confirmation in both the dashboard and console-command surfaces
- the feature shall not capture request payloads, user data, or other business content
- manual GC is a deliberate developer diagnostic action and requires no additional feature-specific safeguard beyond the feature being enabled and accessible
- the feature shall be documented as a short-lived diagnostic tool, not a production monitoring replacement

## Acceptance Criteria

The feature is considered complete when:

- when the feature is enabled through configuration, a developer can start a short performance collection session from the dashboard
- the global dashboard automatically discovers enabled Profiling navigation and endpoints without a Profiling-specific dashboard registration call
- only one logical deployment-wide session can run at a time
- concurrent logical start attempts resolve to the existing active session without a second start broadcast
- starting a session uses the DevKit Broadcast feature and does not poll the session store or registry for commands
- any actively registered node may initiate the broadcast; no master node is required
- the broadcast targets the current active registrations in the configured application scope
- nodes returning an accepted response within the short participation window become the fixed participant set and begin collecting
- a node accepting a valid replacement start atomically stops its older local collector before starting the new collector
- registered nodes that reject, are unsupported, expire, time out, or are unreachable are reported as immediate delivery outcomes but do not become participants
- any node can idempotently finalize a session after its end and grace period, and startup reconciliation finalizes abandoned sessions
- snapshots contain UTC timestamps, node-local monotonic timing, capture duration, cumulative skipped and failed capture counts, node-local sequence numbers, readable keys, and hostname/process metadata
- a node-local collector never overlaps captures; a busy scheduled opportunity is skipped and recorded
- CPU, allocation, and GC rate calculations use monotonic elapsed time and are not affected by UTC clock adjustment
- snapshots contain direct latest-GC and latest-Gen2 post-collection heap evidence, including compacting and concurrent state when available
- each expected participant or ad-hoc contributor stores immutable application, runtime, operating-system, architecture, GC, processor, process-start, and debugger context once per session
- session, node, and snapshot public APIs, routes, console arguments, JSON, logs, and errors use immutable eight-character lowercase keys generated by `KeyGenerator.CreateLowercase(8)`
- the dashboard can select an expected participant or ad-hoc contributing node and show its current snapshot and history
- the dashboard shows memory, allocations, GC, CPU, thread pool, and socket metrics when available
- unavailable metrics are displayed as unavailable rather than zero
- the dashboard presents a dense Current Snapshot card grid and exactly two primary runtime charts
- named segments appear as timeline markers or highlighted ranges
- a developer can add an immutable named phase marker to an active session from the dashboard, console, or programmatic API, and it appears on every selected-node chart
- phase-marker creation rejects an invalid name or inactive session without creating a session or broadcasting
- the dashboard supports configurable refresh intervals
- the dashboard can start one non-overlapping, host-local 30-second CPU, allocation, retained-memory, LOH, and GC stress workload without blocking its request
- background refresh updates live Profiling regions without resetting focused controls, unsaved input, selections, expanded panels, analysis output, filters, or scroll position
- sample intervals below 500 ms are rejected and every session requires a duration
- sessions can be named, tagged, noted, pinned, edited, selected, shared by URL, restarted, exported, copied as JSON, and deleted
- complete terminal sessions and individual immutable snapshots can be exported as portable JSON archives and imported as fresh terminal sessions
- complete terminal sessions can be exported as one-way Perfetto Trace Event JSON without internal identifiers or computed evaluations
- the dashboard can clear all stored performance data, including pinned sessions and associated records, after explicit confirmation
- clearing is rejected while a logical session is active and leaves the store unchanged
- a completed clear leaves an empty performance store and rejects delayed writes for cleared session identities
- start, active-state checks, and clear-all are serialized by the session store's atomic lifecycle coordination
- active sessions cannot be deleted directly and must be stopped first
- restarting an active session stops it first, preserves it as stopped, and creates the replacement only after the stop is accepted
- a manual snapshot without an active session creates a completed one-snapshot session with a timestamp-based default name
- a manual snapshot targets all current registrations; during an active session, accepting nonparticipants contribute ad-hoc snapshots without joining the expected participant set or affecting warnings
- session start and standalone manual snapshot are rejected before broadcast when multiple nodes are targeted and the provider does not support multi-node storage
- exactly two snapshots from the currently selected session and node can be selected for side-by-side metric comparison in a table, including absolute and percentage differences
- application code can create scoped sessions and measured segments
- when Console Commands are enabled, `profiling` and `prof` expose status, start, stop, manual snapshot, manual GC, phase-marker, clear-all, analysis, portable export/import, and Perfetto trace export operations
- console commands invoke the same control service and preserve the same deployment-wide Broadcast behavior as dashboard operations
- console start accepts optional name, interval, and duration values and applies the same defaults and validation as the dashboard
- console durations accept `ms`, `s`, `m`, `h`, and standard `TimeSpan` formats through a feature-local parser
- `profiling analyze` supports one complete node-session timeline or exactly two same-session, same-node snapshots, with optional JSON output, and never stores the result
- `profiling export` defaults to archive format and accepts a session, optional paired node and snapshot selection, output path, optional `--format archive|perfetto`, and optional `--overwrite`; Perfetto format is session-only, while `profiling import` accepts only a local portable archive file
- archive import/export is manual, off the collection hot path, and adds no hosted service or background I/O
- existing `diag perf` and `diag gc` remain local and non-persisted
- console status and Broadcast-backed command results clearly report session state and immediate per-node outcomes
- disabled or unavailable console operations fail safely without changing collection state
- `profiling clear` changes no data without `--yes` and, when confirmed, applies the same full-reset rules as the dashboard
- a scoped code section starts and owns a session when none is active
- a scoped code section encountered during an active session joins it as a named segment
- raw scopes default to completed unless explicitly marked failed or cancelled
- execution helpers automatically record success, failure, or cancellation
- failed measured operations record the exception type and message without persisting the stack trace
- segments identify their owning session and node, may overlap, and may optionally reference a parent from the same session and node
- collection duration acts as a safety maximum while a segment may remain open until its code scope completes
- explicit stop is a best-effort Broadcast command; participating nodes missing or rejecting it may continue until the original session end time
- a session with failed or incomplete known participants completes with warnings without stopping healthy participants
- custom metrics use stable identifiers, are stored independently from runtime snapshots, identify their producing node, and inherit the ambient segment when present
- the in-memory provider supports frictionless local development
- the Entity Framework provider preserves sessions durably and supports shared multi-node collection
- automatic retention removes the oldest unpinned completed sessions first
- the feature can be disabled without affecting normal application behavior
- one application evaluation service deterministically evaluates either two same-node snapshots or a complete available single-node session timeline
- evaluation is computed on demand, never persisted, and uses no AI, network service, configurable rule, plugin rule, or overall score
- active-session evaluation is provisional; full-session interpretation begins only with at least five snapshots over five seconds
- the fixed CPU, memory, allocation, and GC rules produce only `Notable` or `Investigate` signals with `Low`, `Medium`, or `High` confidence, raw evidence, and one short suggested action
- KPI values remain available when inputs exist even when no signal is emitted; thread-pool, socket, and custom metrics do not produce generic interpretation signals
- DataQuality and KPI output show sampling coverage, skipped and failed captures, p95 capture duration, and p95 sampling delay
- failed captures, coverage below 90%, p95 capture overhead of at least 25%, excessive sampling delay, and an attached debugger prevent `High` confidence as specified
- the dashboard's browser-wide Live analysis switch defaults off, makes no automatic calls while off, and when on evaluates only after a new snapshot without overlapping calls or exceeding refresh cadence
- dashboard evaluation results cannot be persisted, copied, or exported; normal raw snapshot JSON export remains available
- request analytics, aggregate-node views, session comparison, cross-node snapshot comparison, arbitrary-interval analysis, baselines, production alarms, configurable thresholds, and node rankings are absent from the feature
- portable archives are a fixed local-development JSON contract limited to 25 MiB; they are distinct from raw snapshot export and never contain computed evaluations or internal identifiers

## Resolved Decisions

The following decisions are resolved for the feature:

- the specification defines the complete feature and does not use implementation phases or feature versions
- application developers are the primary users
- sessions may be started manually or programmatically around code sections
- collection may also be controlled through the grouped `profiling` console commands, with `prof` as an alias
- console commands reuse the application-facing control service and the same deployment-wide Broadcast operations as the dashboard
- the console-command surface provides status, start, stop, one-off snapshot, manual GC, phase-marker, confirmed clear-all, deterministic analysis, portable export, and portable import operations
- imported sessions use the existing inspection and one-session/one-node evaluation behavior without adding cross-session comparison
- friendly console durations accept `ms`, `s`, `m`, `h`, and standard `TimeSpan` values through a Profiling-feature-local parser
- the existing `diag perf` and `diag gc` commands remain local, immediate, and non-persisted
- only one logical session may be active for the deployment at a time
- embedded scoped starts join the active session as named segments instead of starting another session
- session scopes are disposable and execution helpers automatically capture outcomes
- collection duration is a safety maximum; a segment may outlive collection and records that condition
- segments are node-owned, may overlap, and may optionally reference a parent from the same session and node without strict nesting
- manual phase markers are immutable session-level annotations, require an active session, appear across selected-node charts, and do not create arbitrary evaluation intervals
- deployment-wide control uses the standalone DevKit Broadcast feature from `Common.Utilities/Broadcasting`
- broadcasts are direct push operations and the Profiling feature performs no polling for commands
- any actively registered node may initiate a session; there is no master node
- the current active registrations in the configured Broadcast scope form the target snapshot
- nodes returning an accepted response within the short configurable participation deadline become participants; the default deadline is one second
- nodes registering later do not join automatically
- the publishing node handles its own performance broadcast locally rather than calling its own HTTP endpoint
- immediate delivery responses show who responded but do not represent local handler completion
- concurrent logical start attempts do not create another session
- an accepting node atomically replaces any older local collector when it handles a valid new session start; a node missing the new start is not a participant
- explicit stop is a best-effort Broadcast command; a participating node missing or rejecting it may continue until the original end time
- a session ends when its configured duration expires or it is explicitly stopped
- any node may idempotently finalize after the end and grace period, and startup reconciliation finalizes abandoned sessions
- stopped or superseded sessions may accept valid late writes only while they still exist and within their original collection window; deleted, cleared, and expired identities reject writes
- sessions with failed or incomplete known participants finish as completed with warnings
- sessions, nodes, and snapshots use internal GUIDs plus immutable eight-character lowercase alphanumeric keys from `KeyGenerator.CreateLowercase(8)`
- public routes, console arguments, programmatic APIs, JSON, logs, errors, and displayed identities use readable keys; internal persistence, handlers, relationships, and Broadcast use GUIDs
- readable keys are treated as practically unique and require no collision protocol, scoping, versioning, configurability, or alternate generator
- a node key is stable for the process lifetime; hostname and process identifier are metadata, and a restart creates a new node key
- each expected participant or ad-hoc contributor captures immutable non-sensitive application, runtime, operating-system, architecture, GC, processor, process-start, and debugger context once per session
- the dashboard presents one selected node at a time and provides no aggregate-node view or automatic ranking
- manual snapshots use DevKit Broadcast and target all current registrations in the configured scope; during an active session, accepting nonparticipants add ad-hoc snapshots but do not join the expected participant set or affect warnings
- when no session is active, manual snapshot creates and completes a dedicated one-snapshot session named `Manual snapshot — <ISO 8601 timestamp>`
- manual GC is always available and uses DevKit Broadcast; it does not require an active session, and the dashboard reports immediate per-node delivery responses rather than handler completion
- manual GC performs a normal `GC.Collect()` call only, without waiting for pending finalizers or requesting separate LOH compaction
- manual GC has no additional feature-specific safeguards because this is a developer feature that is normally disabled in production
- request counts, request timings, route diagnostics, and slow-request views are outside scope
- the written dashboard layout is authoritative; missing image references are not part of the specification
- the dashboard contains exactly two primary runtime charts
- custom metrics use stable identifiers; dynamic names and high-cardinality dimensions are not supported
- custom metrics require no separate dashboard-specific registration
- custom metric observations are stored independently from runtime snapshots, identify their session and node, and inherit the ambient segment when present
- session-to-session comparison is not part of the feature; exactly two explicitly selected snapshots within the current session and selected node are compared and analyzed above a table with absolute and safe percentage differences
- deterministic interpretation uses fixed local rules, not configurable warning thresholds, production alarms, health scoring, or an overall score
- session metadata remains editable after completion while collected observations remain immutable
- no metadata audit trail is required
- sessions may be pinned and pinned sessions are excluded from automatic retention
- normal bulk deletion removes only unpinned sessions; pinned sessions are removed only through explicit selected-session deletion or the confirmed clear-all operation
- an active session cannot be deleted directly; restart first stops an active session, preserves it as stopped, and then creates a new session copying name, interval, duration, and tags, but not notes, pin state, snapshots, phase markers, segments, custom metrics, or node runtime context
- clearing one selected session in place is not supported; developers restart or delete that session instead
- a separate confirmed clear-all operation resets the complete performance store, includes pinned sessions, and is rejected while a logical session is active
- the session store owns atomic start, active-state, and clear coordination; Entity Framework uses a transaction and concurrency constraint, while in-memory uses a process-local lock
- cleared session identities cannot accept delayed writes or be recreated implicitly
- storage uses a provider abstraction
- the in-memory provider is valid for tests and local development and is intentionally ephemeral
- Entity Framework is the first-class durable provider
- providers expose `SupportsMultiNode`; session start and standalone manual snapshot reject before broadcast when more than one node is targeted and the provider reports false
- multi-node sessions require a shared provider accessible to all participating nodes; manual GC does not
- snapshot count is not capped within a session; retention defaults to 20 completed sessions and 7 days
- oldest unpinned completed sessions are removed first and active sessions are never removed automatically
- the minimum sampling interval is 500 ms and lower values are rejected
- every session requires a collection duration and open-ended sessions are not supported
- snapshot captures never overlap; busy opportunities are skipped, failures are counted separately, and both cumulative counts are preserved
- node-local monotonic elapsed time is authoritative for rate, delay, and capture-duration calculations; UTC remains display and cross-node alignment metadata
- snapshots preserve direct post-collection evidence for the latest GC and latest Gen2 GC, including heap, LOH, compacting, and concurrent values when available
- failed measured operations persist exception type and message but no stack trace
- node selection uses a dropdown and dashboard selections are preserved in browser localStorage
- evaluation is programmatic and deterministic, uses no AI or network dependency, and is computed server-side on demand without persistence
- evaluation supports only a complete available timeline for one selected node or exactly two selected snapshots from the same session and node
- active-session results are provisional; full-session signals require at least five snapshots over five seconds, while two-snapshot signals are always low confidence
- evaluation returns `Scope`, `DataQuality`, `KPIs`, `Signals`, and `Limitations`, with no analyzer version
- there is no cross-node, deployment-wide, session-to-session, or arbitrary-interval evaluation
- fixed built-in interpretation rules focus on CPU, memory, allocations, and GC; they are not configurable, extensible, or versioned
- signals use only `Notable` and `Investigate`, confidence uses only `Low`, `Medium`, and `High`, and every signal has one short deterministic suggested action
- KPI cards remain visible independently of signals; thread-pool and socket metrics may be KPIs but custom metrics and those secondary metrics do not produce generic signals
- DataQuality and KPI output include sampling coverage, skipped and failed captures, capture overhead, sampling delay, and debugger-attached limitations
- the dashboard Live analysis switch is browser-wide, defaults off, and persists in localStorage; explicit Analyze now remains available
- evaluation results are not persisted or exportable from the dashboard, while console and programmatic callers may request the computed contract as JSON
