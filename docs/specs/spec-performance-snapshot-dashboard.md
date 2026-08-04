---
status: draft
---

# Design Specification: Performance Snapshot Dashboard

> This draft specification defines a lightweight, developer-focused performance dashboard for short-lived runtime diagnostics. The feature is intended for stress testing, warm-up validation, and ad-hoc troubleshooting, not for long-term production monitoring.

[TOC]

## Overview

The performance dashboard introduces a dedicated dashboard page for collecting and reviewing short bursts of runtime performance data while an application is running. The feature is designed for application developers who want to start a workload, collect a focused set of performance snapshots, and inspect the results immediately in the browser.

The feature shall also provide a lightweight programmatic control surface so application code can start, stop, or manage collection sessions directly. This makes it suitable for integrated DevKit usage where a feature or test workflow can trigger collection around a specific operation without requiring manual dashboard interaction.

Deployment-wide control shall use the DevKit Broadcast feature from `Common.Utilities/Broadcasting`. The performance feature shall not poll its session store for control commands. Broadcast supplies the registered-node snapshot and direct push delivery used for session start, stop, manual snapshot, and manual GC actions.

This feature complements, rather than replaces, long-term observability platforms such as Grafana, Prometheus, OpenTelemetry, or vendor APM tools.

The primary goals are:

- make runtime performance inspection easy during local development and stress testing
- capture lightweight snapshots for a short period of time
- expose the results in a dashboard with clear visual summaries
- support multi-node deployments by collecting data from each node and letting the user focus on one node at a time
- support named sessions so developers can identify and revisit a specific run later
- support shareable session links so another developer can open a specific session directly from a URL
- support programmatic control so application code can start and stop sessions as part of automated workflows or integration tests

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
- delete all unpinned sessions at once, with an explicit option to include pinned sessions for complete deletion
- add session tags and notes for later review
- pin sessions that must be excluded from automatic retention
- trigger a one-off manual snapshot collection for immediate diagnostics
- trigger an explicit GC collection for immediate diagnostics

Feature enablement shall be controlled through application configuration and shall default to disabled. A dashboard or programmatic caller shall not be able to enable a configuration-disabled feature. When enabled, the runtime collection state shall be idle, running, or one of the defined terminal session states.

If no name is supplied, the system shall assign a default session name using the current timestamp in ISO 8601 format. If a name is supplied, it shall be stored and shown in the UI and programmatic results.

When started, the background collector shall begin sampling at the configured interval and shall stop automatically when the configured duration expires. The dashboard shall expose the current state as idle, running, completed, completed with warnings, stopped, or failed. A session shall complete with warnings when collection finishes but one or more known participating nodes report a node-level failure or incomplete collection.

The manual snapshot action shall broadcast a one-off snapshot command through the DevKit Broadcast feature. When a session is active, every participating node that accepts the command shall capture and store one immediate snapshot. When no session is active, the action shall create a dedicated one-snapshot session, broadcast the command within the configured application scope, use the immediate delivery responses to determine the responding nodes, and complete the session after the participation window. The default name shall use the form `Manual snapshot — <ISO 8601 timestamp>`. This action is intended for ad-hoc diagnostics and does not replace the scheduled background collection loop.

The manual GC action shall be available whether or not a collection session is active. It shall be broadcast through the DevKit Broadcast feature, and every registered node in the configured scope that accepts the command shall invoke a normal `GC.Collect()` call. When a session is active, the dashboard shall show the immediate per-node broadcast responses, while each accepting node shall record the GC action as a local session marker when it handles the command. When no session is active, the dashboard shall show which registered nodes responded to the broadcast request, shall not wait for handler completion, and shall not create a collection session. The action shall not additionally wait for pending finalizers or request separate LOH compaction. Broadcast `Accepted` means that the receiving node accepted the command for local execution; it does not mean that garbage collection or any other local handler work has completed.

The collection lifecycle shall also be available programmatically so application code, background jobs, integration tests, or custom developer workflows can start and stop sessions directly. The programmatic surface shall support an optional session name and expose the same start, stop, status, restart, and deletion semantics as the dashboard.

The dashboard shall support shareable session URLs. A session link identifies the target session but does not bypass normal access restrictions.

The programmatic control surface shall be resilient to missing infrastructure. If the collector, session store, or required Broadcast integration is not registered or available, calls shall return a safe no-op result or a clear unavailable state, emit a warning log entry, and leave the application running normally.

### Session ownership and lifecycle

Only one logical collection session may be active for an application deployment at a time. A new logical session shall not be accepted while another session is running. An active session shall not be deleted directly and must first be stopped.

Starting a session shall atomically create the shared session identity and publish a typed start command through the DevKit Broadcast feature. The broadcast shall target the configured application scope or scopes and shall use the registry snapshot that exists when the operation begins. There is no master node; any actively registered node may initiate the operation.

The participation window shall be short because local development is the primary usage scenario. It shall be configurable, with a default of one second, and shall be applied as the broadcast lifetime or caller deadline for the start command. Nodes that return an immediate accepted response before the deadline form the fixed participant set for the session. The initiating control service shall record those accepted nodes as expected participants from the Broadcast result so they remain visible even if local execution fails before a node writes its own participation update. Each accepted node shall then start its local collector and confirm or update its participation state in the shared session store. Nodes that reject, do not support, time out, or cannot be reached do not become participants, although their immediate delivery outcomes may be shown to the developer. Nodes registering after the registry snapshot was taken shall not join the session automatically. The configured collection duration shall be measured from the logical session start time rather than independently from each node's arrival time.

Concurrent start attempts shall resolve to the existing active session. They shall not publish an additional start broadcast or create a second session. The dashboard shall navigate to the active session and clearly indicate that only one deployment-wide session can run at a time.

A session reaches a terminal state when either:

- its configured collection duration expires
- it is explicitly stopped by a developer or programmatic caller

Stopping is a best-effort deployment-wide broadcast. The logical session shall be shown as stopped immediately when the stop broadcast operation is accepted by the local control surface. Participating nodes that accept the stop command shall stop collecting. A participating node that does not receive or accept the command may continue collecting until the original session end time, and the store may continue accepting that node's late snapshots until that time. The feature shall not introduce retries, durable command storage, or additional recovery solely for missed stop broadcasts.

A normally elapsed session shall be distinguishable from a session that was stopped early. The session shall retain its collected snapshots, participating-node information, segments, tags, notes, pin state, and completion metadata after it reaches a terminal state.

Restarting a session shall create a new session identity. When the selected session is active, restart shall first stop it and create the replacement only after the stop operation has been accepted. The previous session shall remain available with a stopped state. The new session shall copy the previous session name with a restart suffix or timestamp, sampling interval, duration, and tags. It shall not copy notes, pin state, snapshots, segments, or custom metric observations. Like every deployment-wide start, the restarted session shall publish a new typed start broadcast and establish its participants again from the current registration snapshot.

### Broadcast integration

The performance feature shall consume the standalone DevKit Broadcast feature and shall not implement its own node registry, HTTP receiver, delivery transport, or broadcast polling loop.

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

For local single-process development, the Broadcast feature may use its in-memory registry and local dispatch path. For multi-node usage, the Broadcast feature shall use its shared Entity Framework registry provider and direct HTTP push. The performance feature remains independent of how node addresses are resolved or how registrations are maintained.

### Scoped programmatic sessions and segments

Application code shall be able to measure a code section through a scoped session API. The scope shall automatically close when disposed, including when the measured operation fails or is cancelled.

When no session is active, opening a scoped session shall start a new deployment-wide collection session. That scope owns the session and shall stop it when the scope is closed, unless the configured duration has already elapsed.

The configured collection duration remains a safety maximum for scoped sessions. When the duration expires before the measured code section completes, snapshot collection shall stop, while the segment shall remain open until the code scope ends. The segment shall record that collection ended before the operation completed.

When a scoped session is opened while a session is already active, the nested start request shall not create or replace the active session. Instead, the scope shall join the active session and register a named segment within it. Closing such a nested scope shall close only its segment and shall not stop the active session.

The programmatic control surface shall support both raw scopes and execution helpers. A raw scope shall default to a completed outcome unless the caller explicitly marks it as failed or cancelled. An execution helper such as `MeasureAsync(...)` shall determine and record success, failure, or cancellation from the wrapped operation. For failed operations, the segment shall record the exception type and message when available, but shall not persist the stack trace.

A session may contain multiple named segments. Segments are timeline annotations and may overlap. A segment may optionally reference a parent segment, but strict nesting shall not be required and independent overlapping segments shall remain valid. Each segment shall record, when available:

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
- GC collection counts by generation (Gen0, Gen1, Gen2)
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

Request counts, request timings, route diagnostics, and other HTTP-request analytics are outside the scope of this feature. The supplied dashboard images are visual layout references only; request-related cards shown in those images are not requirements for this dashboard.

Metrics unavailable on the current runtime, operating system, or hosting environment shall be represented as unavailable rather than silently reported as zero. Sessions may therefore contain different available metric sets across nodes.

All timestamps shall use UTC. The dashboard shall preserve each node's original snapshot timestamp and tolerate small clock differences between nodes.

Derived metrics such as CPU usage, allocation rate, and percentages shall be calculated consistently from consecutive samples. The exact runtime and operating-system APIs used to obtain the values remain an implementation decision.

### Custom metrics integration

The dashboard shall support custom application metrics in addition to the standard runtime metrics.

Custom metrics shall use the existing DevKit metrics abstraction rather than introduce a second application-facing metrics API. When a performance session is active, supported metric observations shall be associated with that session. When no session is active, the integration shall add negligible overhead and shall not retain session data.

Supported custom metric forms shall include at least:

- counters or incremental totals
- current gauges or live values
- duration measurements or tracked scopes

Custom metric names shall be stable identifiers defined by the application or feature. Dynamic metric names and arbitrary high-cardinality dimensions are outside the supported model. No separate dashboard-specific metric registration step shall be required; supported observations shall be captured through the existing DevKit metrics abstraction using their stable identifiers.

Custom metric observations shall be persisted with the active session. An observation shall also inherit the current ambient segment identifier when one exists, without requiring the caller to pass a segment identifier explicitly. Observations emitted outside a segment shall remain session-level.

Custom metrics may be displayed in an optional generic panel without changing the requirement for the two primary runtime charts. The panel shall use the stable metric identifier as the display name and show the recorded values and timestamps. Metric kind, unit, or segment association may be shown when that information is already available from the existing metrics observation, but no separate dashboard registration or descriptive metadata model is required.

### Snapshot payload

Each snapshot shall include:

- timestamp in UTC
- node identifier
- machine or container hostname
- process identifier
- collection session identifier
- collected metric values for memory, CPU, GC, allocations, thread pool, sockets, and available custom metrics

Session name, tags, notes, segments, pin state, and other session-level metadata shall be stored with the session rather than duplicated into every snapshot.

The node identifier shall combine the machine or container hostname with the process identifier. It shall remain stable for the lifetime of that application process. An application restart that receives a different process identifier shall appear as a new node instance.

The dashboard shall display a clear indication of when the data was captured and which node produced it.

### Multi-node behavior

A collection session shall be deployment-wide within one or more configured Broadcast scopes. Starting a session shall use the DevKit Broadcast feature to read the current active registration snapshot and directly push the collection-start command to every registered target node. Nodes do not poll the performance store or Broadcast registry for commands.

The performance feature shall use the node identity and delivery outcomes supplied by Broadcast. Nodes that return `Accepted` within the participation deadline become the fixed participant set. Registered nodes that return another response or do not respond may be shown in the immediate start-operation delivery summary but are not session participants. The Broadcast feature does not persist that delivery summary as broadcast history. A node registered after the target snapshot was read shall not join the session automatically.

Each participating node shall record its participation and collect and store its own node-identified snapshots. The session shall record collection status independently for each participant. A participating node that fails during collection or does not complete shall not stop the remaining nodes from collecting. Partial completion and node-level failures shall be visible in the session view.

A session that reaches its end while one or more known participating nodes failed or remained incomplete shall be marked completed with warnings. A session shall be failed only when no meaningful collection occurred or the logical session itself could not be established or maintained.

The dashboard shall present one selected node at a time. It shall not calculate an aggregate deployment view or attempt to identify a best, worst, fastest, or slowest node. Developers may switch between participating nodes to inspect their individual timelines.

Manual snapshots and deployment-wide diagnostic actions shall use typed DevKit Broadcast commands. The dashboard shall show the immediate per-node delivery responses, while local action completion remains outside Broadcast.

### Refresh behavior

The dashboard shall support a configurable refresh interval, following the same pattern as other dashboard pages.

Supported refresh modes should include:

- off
- 1 second
- 5 seconds
- 10 seconds
- 15 seconds
- 30 seconds
- 60 seconds

## Dashboard Requirements

### Dashboard layout

The dashboard view shall include:

- a status section showing whether collection is idle, running, completed, completed with warnings, stopped, or failed
- controls for start, stop, duration, interval, manual snapshot collection, and manual GC collection
- a current-snapshot summary in a compact card grid
- exactly two primary charts: a Memory History chart and a GC Pressure History chart
- a node selector
- session selection and metadata controls

The current-snapshot cards shall display the following runtime indicators when available:

- CPU usage and process identity
- system platform and runtime version
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

Unavailable indicators shall be clearly shown as unavailable rather than as zero.

### Visualizations

The dashboard shall present exactly two primary runtime charts:

- **Memory History:** a time series of managed memory, heap size, private memory, working set, committed memory, and LOH size
- **GC Pressure History:** a time series of CPU usage, memory pressure, heap fragmentation, LOH fragmentation, GC pause, and allocation rate

The charts shall use the dashboard's standard charting technology and shall be optimized for short diagnostic windows. Named session segments shall be rendered as markers or highlighted time ranges so runtime changes can be related to measured code sections.

Each chart panel shall include:

- a concise panel title
- a clear legend
- readable units
- a retained-point indicator
- node and session context

The supplied visuals define the intended information density and layout direction: a compact current-snapshot card grid followed by two full-width history panels. They are visual references rather than pixel-perfect requirements. Request-related cards visible in the reference images are excluded from the feature scope.

![Current snapshot card-grid reference](./performance-dashboard-current-snapshot.png)

![Memory history chart reference](./performance-dashboard-memory-history.png)

![GC pressure history chart reference](./performance-dashboard-gc-pressure-history.png)

### Developer productivity features

The dashboard shall provide tools that help developers inspect diagnostic sessions:

- editable session name, tags, and note
- selection of exactly two snapshots from the current session and selected node for side-by-side comparison in a table
- per-metric earlier value, later value, absolute difference, and percentage difference in that comparison table
- percentage differences shown as unavailable when the earlier value is zero or the calculation is otherwise not meaningful
- inline help explaining the meaning and unit of each card and chart series
- export of normal runtime snapshots for the selected node or the complete session as JSON
- copy the current snapshot to the clipboard as JSON
- bookmarkable session and node selections

The feature shall not provide session-to-session comparison, cross-node snapshot comparison, baseline designation, automatic threshold warnings, health scoring, or automatic node ranking. Snapshot comparison is limited to exactly two explicitly selected snapshots within the currently selected session and node and is presented as a table rather than as an overlaid chart. The comparison shall show earlier and later values, absolute differences, and percentage differences where they can be calculated safely.

### Dashboard screen details

The dashboard shall reflect the following visual structure:

- a top **Current Snapshot** panel with a dense card-grid layout
- a full-width **Memory History** panel
- a full-width **GC Pressure History** panel
- optional custom metric and segment-detail panels below the two primary charts

The Current Snapshot panel shall clearly show the selected node identity and the snapshot timestamp. Cards shall emphasize one primary value and use smaller supporting lines for related values, matching the supplied visual reference.

The Memory History panel shall plot memory series on a shared timeline and show the number of retained points.

The GC Pressure History panel shall plot percentage-based pressure series and allocation activity on a shared timeline. Multiple units may use separate axes where necessary, provided the chart remains readable.

The dashboard shall not include request-throughput, request-latency, route, or slow-request panels.

### Node-aware drill-down

The dashboard shall allow the user to:

- select one participating node from a dropdown control
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
- explicitly include pinned sessions when performing a complete deletion
- select a previously saved or completed session
- open a session directly from a shareable URL
- trigger a manual one-off snapshot collection
- trigger a manual deployment-wide GC collection
- inspect named code segments and their start, end, duration, outcome, optional parent, and correlation metadata
- edit the session name, tags, notes, and pin state without changing collected snapshots or metric values
- export normal runtime snapshots for either the currently selected node or the complete multi-node session as JSON

Snapshot export shall use JSON only. A selected-node export shall contain the normal runtime snapshots collected for that node. A complete-session export shall contain the normal runtime snapshots collected across all participating nodes. The node identity is part of each exported snapshot. Session metadata, segment records, custom metric observations, and other auxiliary records are not included in snapshot exports.

Session metadata shall use zero or more plain string tags and one optional free-text note. Tag hierarchies, key/value labels, comment threads, and metadata history are outside scope.

Collected snapshots and metric observations shall remain immutable after they are written. Descriptive session metadata may remain editable after completion.

## Storage Model

### Provider model

Session and snapshot storage shall use a provider abstraction so applications can select storage appropriate to their development and deployment model.

The feature shall provide:

- an Entity Framework provider as the first-class durable provider
- an in-memory provider for tests and frictionless local development
- extensibility for additional providers without changing the application-facing collection and dashboard model

The in-memory provider is intentionally ephemeral. Its sessions remain available only while the application process is running and are lost on restart. It is suitable for local diagnostics where durability is not required.

A durable provider shall preserve sessions, snapshots, segments, custom metrics, notes, tags, pin state, and node participation data across page refreshes, application restarts, and later review.

Multi-node sessions require a shared provider accessible to all participating nodes. The in-memory provider shall not be presented as supporting deployment-wide multi-node collection across independent application processes.

### Retention rules

The feature shall remain bounded and shall not be designed as a long-term archive. Retention shall apply automatically according to configurable limits.

Snapshot count shall not be capped per node or session when the selected provider can persist the data. Session-level retention shall remain bounded through the following suggested defaults:

- maximum retained completed sessions: 20
- maximum session age: 7 days

Sessions may be pinned by a developer. Pinned sessions shall be excluded from automatic retention so important diagnostic records can be retained.

When a retention limit is reached, the oldest unpinned completed sessions shall be removed first. Active sessions shall never be removed automatically. Manual deletion shall remain available for both pinned and unpinned sessions, subject to authorization.

## Architecture

The feature should follow a small, separated architecture:

- a background collector running inside each participating application process
- the standalone DevKit Broadcast feature for node registration, registry lookup, direct HTTP push, local self-delivery, and immediate per-node delivery responses
- one local performance broadcast handler per supported control command
- a session store for sessions, node participation, snapshots, segments, custom metrics, and metadata
- a dashboard query layer that loads one selected session and node timeline
- an application-facing control surface for programmatic sessions and scoped segments
- an integration with the existing DevKit metrics abstraction for stable custom metrics
- a dashboard page for control, session selection, current values, snapshot comparison tables, export, and metadata editing

The collector shall remain independent from dashboard rendering so collection continues without an open browser page.

The performance feature shall not implement message polling, its own node registry, heartbeat infrastructure, delivery transport, deployment-wide metric aggregation, request-diagnostics infrastructure, or session-comparison infrastructure. Node registration and optional low-frequency registration leases belong to the Broadcast feature.

## Configuration and Defaults

The feature shall be configured through application options and environment settings.

Suggested defaults:

- enabled: false
- minimum sampling interval: 500 ms
- sampling interval: 1 second
- duration: 30 seconds and required for every session
- automatic stop: true
- default session name: ISO timestamp
- max retained completed sessions: 20
- max session age: 7 days
- refresh interval: 5 seconds
- storage provider: in-memory by default for local development; Entity Framework for durable or multi-node usage
- broadcast scope: configured for the application host and shared by its replicas
- broadcast integration: in-memory registry/local dispatch for single-process development; shared Entity Framework registry plus direct HTTP push for multi-node usage
- start-command participation deadline: 1 second
- gc trigger: available from the dashboard
- programmatic control: enabled through an application service interface

## Security and Operational Notes

- the feature is intended primarily for application developers and local or controlled development environments
- it shall be opt-in and disabled by default
- applications should normally keep the feature completely disabled in production
- when enabled, dashboard access and control operations shall follow the host application's normal authorization model
- the feature shall not capture request payloads, user data, or other business content
- manual GC is a deliberate developer diagnostic action and requires no additional feature-specific safeguard beyond the feature being enabled and accessible
- the feature shall be documented as a short-lived diagnostic tool, not a production monitoring replacement

## Acceptance Criteria

The feature is considered complete when:

- when the feature is enabled through configuration, a developer can start a short performance collection session from the dashboard
- only one logical deployment-wide session can run at a time
- new logical start attempts are rejected or redirected to the active session while it is running
- starting a session uses the DevKit Broadcast feature and does not poll the session store or registry for commands
- any actively registered node may initiate the broadcast; no master node is required
- the broadcast targets the current active registrations in the configured application scope
- nodes returning an accepted response within the short participation window become the fixed participant set and begin collecting
- registered nodes that reject, are unsupported, expire, time out, or are unreachable are reported as immediate delivery outcomes but do not become participants
- snapshots contain UTC timestamps and a node identity based on hostname and process identifier
- the dashboard can select one participating node and show its current snapshot and history
- the dashboard shows memory, allocations, GC, CPU, thread pool, and socket metrics when available
- unavailable metrics are displayed as unavailable rather than zero
- the dashboard presents a dense Current Snapshot card grid and exactly two primary runtime charts
- named segments appear as timeline markers or highlighted ranges
- the dashboard supports configurable refresh intervals
- sample intervals below 500 ms are rejected and every session requires a duration
- sessions can be named, tagged, noted, pinned, edited, selected, shared by URL, restarted, exported, copied as JSON, and deleted
- active sessions cannot be deleted directly and must be stopped first
- restarting an active session stops it first, preserves it as stopped, and creates the replacement only after the stop is accepted
- a manual snapshot without an active session creates a completed one-snapshot session with a timestamp-based default name
- exactly two snapshots from the currently selected session and node can be selected for side-by-side metric comparison in a table, including absolute and percentage differences
- application code can create scoped sessions and measured segments
- a scoped code section starts and owns a session when none is active
- a scoped code section encountered during an active session joins it as a named segment
- raw scopes default to completed unless explicitly marked failed or cancelled
- execution helpers automatically record success, failure, or cancellation
- failed measured operations record the exception type and message without persisting the stack trace
- segments may overlap and may optionally reference a parent segment
- collection duration acts as a safety maximum while a segment may remain open until its code scope completes
- explicit stop is a best-effort Broadcast command; participating nodes missing or rejecting it may continue until the original session end time
- a session with failed or incomplete known participants completes with warnings without stopping healthy participants
- custom metrics use stable identifiers and inherit the ambient segment when present
- the in-memory provider supports frictionless local development
- the Entity Framework provider preserves sessions durably and supports shared multi-node collection
- automatic retention removes the oldest unpinned completed sessions first
- the feature can be disabled without affecting normal application behavior
- request analytics, aggregate-node views, session comparison, cross-node snapshot comparison, baselines, threshold warnings, and node rankings are absent from the feature

## Resolved Decisions

The following decisions are resolved for the feature:

- the specification defines the complete feature and does not use implementation phases or feature versions
- application developers are the primary users
- sessions may be started manually or programmatically around code sections
- only one logical session may be active for the deployment at a time
- embedded scoped starts join the active session as named segments instead of starting another session
- session scopes are disposable and execution helpers automatically capture outcomes
- collection duration is a safety maximum; a segment may outlive collection and records that condition
- segments may overlap and may optionally reference a parent without strict nesting
- deployment-wide control uses the standalone DevKit Broadcast feature from `Common.Utilities/Broadcasting`
- broadcasts are direct push operations and the performance feature performs no polling for commands
- any actively registered node may initiate a session; there is no master node
- the current active registrations in the configured Broadcast scope form the target snapshot
- nodes returning an accepted response within the short configurable participation deadline become participants; the default deadline is one second
- nodes registering later do not join automatically
- the publishing node handles its own performance broadcast locally rather than calling its own HTTP endpoint
- immediate delivery responses show who responded but do not represent local handler completion
- concurrent logical start attempts do not create another session
- explicit stop is a best-effort Broadcast command; a participating node missing or rejecting it may continue until the original end time
- a session ends when its configured duration expires or it is explicitly stopped
- sessions with failed or incomplete known participants finish as completed with warnings
- node identity combines machine or container hostname with process identifier and is stable for the process lifetime
- the dashboard presents one selected node at a time and provides no aggregate-node view or automatic ranking
- manual snapshots use DevKit Broadcast and target the registered nodes in the configured scope; when no session is active, the action creates and completes a dedicated one-snapshot session named `Manual snapshot — <ISO 8601 timestamp>`
- manual GC is always available and uses DevKit Broadcast; it does not require an active session, and the dashboard reports immediate per-node delivery responses rather than handler completion
- manual GC performs a normal `GC.Collect()` call only, without waiting for pending finalizers or requesting separate LOH compaction
- manual GC has no additional feature-specific safeguards because this is a developer feature that is normally disabled in production
- request counts, request timings, route diagnostics, and slow-request views are outside scope
- the dashboard uses the supplied visuals as layout and information-density references, excluding their request-related cards
- the dashboard contains exactly two primary runtime charts
- custom metrics use stable identifiers; dynamic names and high-cardinality dimensions are not supported
- custom metrics require no separate dashboard-specific registration
- custom metric observations inherit the ambient segment when present
- session-to-session comparison is not part of the feature; exactly two explicitly selected snapshots within the current session and selected node are compared in a table with absolute and safe percentage differences
- automatic threshold warnings and health scoring are not part of the feature
- session metadata remains editable after completion while collected observations remain immutable
- no metadata audit trail is required
- sessions may be pinned and pinned sessions are excluded from automatic retention
- normal bulk deletion removes only unpinned sessions; pinned sessions are removed only when explicitly included
- an active session cannot be deleted directly; restart first stops an active session, preserves it as stopped, and then creates a new session copying name, interval, duration, and tags, but not notes, pin state, snapshots, segments, or custom metrics
- clear-session behavior is not part of the feature; developers restart or delete instead
- storage uses a provider abstraction
- the in-memory provider is valid for tests and local development and is intentionally ephemeral
- Entity Framework is the first-class durable provider
- multi-node sessions require a shared provider accessible to all participating nodes
- snapshot count is not capped within a session; retention defaults to 20 completed sessions and 7 days
- oldest unpinned completed sessions are removed first and active sessions are never removed automatically
- the minimum sampling interval is 500 ms and lower values are rejected
- every session requires a collection duration and open-ended sessions are not supported
- failed measured operations persist exception type and message but no stack trace
- node selection uses a dropdown and dashboard selections are preserved in browser localStorage
