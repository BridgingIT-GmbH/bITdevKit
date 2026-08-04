---
status: draft
---

# Design Specification: Performance Snapshot Dashboard

> This draft specification defines a lightweight, developer-focused performance dashboard for short-lived runtime diagnostics. The feature is intended for stress testing, warm-up validation, and ad-hoc troubleshooting, not for long-term production monitoring.

[TOC]

## Overview

The performance dashboard introduces a dedicated dashboard page for collecting and reviewing short bursts of runtime performance data while an application is running. The feature is designed for developers who want to start a workload, collect a focused set of performance snapshots, and inspect the results immediately in the browser.

The feature shall also provide a lightweight programmatic control surface so application code can start, stop, or manage collection sessions directly. This makes it suitable for integrated DevKit usage where a feature or test workflow can trigger collection around a specific operation without requiring manual dashboard interaction.

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

This dashboard is intended to make those questions actionable by surfacing allocation-rate spikes, accelerating managed-memory growth, and abnormal session-to-session behavior rather than by enforcing a fixed production threshold.

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

## Proposed Scope

The initial version shall include:

- a dedicated performance dashboard page
- a background snapshot collector service
- manual one-off snapshot collection controls
- a dashboard view for showing recent snapshots as charts and summary cards
- node-aware filtering and selection
- configurable refresh intervals
- durable snapshot storage for sessions and snapshots across page refreshes and restarts
- a manual GC trigger button for immediate diagnostics

## Functional Requirements

### Collection control

The feature shall provide a collection control surface from the dedicated performance page.

The user shall be able to:

- enable or disable the feature
- start a collection session with an optional custom name
- start a collection session
- stop a collection session
- choose the capture interval
- choose the collection duration
- clear the current session data
- delete a selected session and remove all collected data from the store
- delete all sessions at once and remove all collected data from the store
- add session tags, labels, or notes for later review
- trigger a one-off manual snapshot collection for immediate diagnostics
- trigger an explicit GC collection for immediate diagnostics

Collection shall default to disabled.

If no name is supplied, the system shall assign a default session name using the current timestamp in ISO 8601 format. If a name is supplied, it shall be stored and shown in the UI and API results.

When started, the background collector shall begin sampling at the configured interval and shall stop automatically when the configured duration expires. The dashboard shall expose the current state as disabled, idle, running, or completed.

Each collection session shall have an optional user-defined name. If no name is provided, the session shall default to a timestamp in ISO 8601 format so that sessions are easy to identify and revisit later.

The manual snapshot action shall be available as a dedicated button on the dashboard and shall request a single immediate collection pass from all participating nodes. This action is intended for ad-hoc diagnostics and does not replace the scheduled background collection loop.

In distributed mode, the manual GC trigger shall be broadcast to all participating nodes so that the developer can force a collection cycle across the deployment and compare the resulting memory and allocation behavior immediately.

The collection lifecycle shall also be available programmatically through an application-facing control API so that application code, background jobs, integration tests, or custom developer workflows can start and stop sessions directly. The API shall support an optional session name and shall expose the same start, stop, and status semantics as the dashboard controls. It shall also provide a bulk operation to delete all sessions at once.

The dashboard shall support shareable session URLs so that a session can be opened directly by another developer using a link. The URL shall identify the target session and load the session view without requiring the current user to manually locate the session from a list.

The programmatic API shall be resilient to missing infrastructure. If the metrics system, collector service, or session store is not registered or available, the API shall not throw runtime exceptions. Instead, it shall return a safe no-op result or a clear unavailable state, emit a warning log entry, and leave the application running normally.

### Snapshot frequency and duration

The feature shall allow the developer to configure:

- sample interval, for example 500 ms, 1 s, 2 s, or 5 s etc.
- collection duration, for example 30 s, 1 min, 5 min, 10 min, or longer when explicitly configured, 10 min, or longer when explicitly configured
- max retained snapshots per session
- automatic stop behavior after the configured duration elapses

The default configuration shall be conservative and lightweight.

### Metrics to collect

At minimum, the collector shall capture the following runtime metrics for each snapshot:

- CPU usage percent
- working set bytes
- private memory bytes
- managed heap bytes
- total physical memory bytes
- available physical memory bytes
- used physical memory bytes
- heap size bytes
- fragmented bytes
- memory load bytes
- total available memory bytes
- high memory load threshold bytes
- total committed bytes
- total allocated bytes
- allocation rate bytes per second
- GC collection counts by generation (Gen0, Gen1, Gen2)
- pause percent
- pinned objects count
- finalization pending count
- fragmentation percent
- LOH size and LOH fragmentation bytes/percent
- memory pressure percent
- server GC mode indicator
- thread pool thread count
- thread pool completed work item count
- thread pool pending work item count
- active TCP connection count
- TCP listener count
- UDP listener count
- total used socket count

The implementation may also capture additional cheap, clearly useful runtime counters for diagnostics, such as:

- process handle count
- process thread count or active thread count
- thread pool available worker threads and completion port threads
- GC latency mode or server/workstation GC mode
- OS-level physical memory metrics when available

Request-timing metrics and domain-specific calculation counters are out of scope for the initial dashboard release.

#### 6.3.1 Metric collection sample

A concrete implementation may collect metrics from the runtime using the following pattern:

```csharp
using var process = Process.GetCurrentProcess();
process.Refresh();

var gcInfo = GC.GetGCMemoryInfo();
var timestampUtc = DateTime.UtcNow;
var totalCpuDuration = process.TotalProcessorTime;
var totalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
var allocationRateBytesPerSecond = CalculateAllocationRate(totalAllocatedBytes, timestampUtc);

var snapshot = new RuntimeMetricsSnapshot
{
    TimestampUtc = timestampUtc,
    CpuUsagePercent = CalculateCpuUsage(totalCpuDuration, timestampUtc),
    WorkingSetBytes = process.WorkingSet64,
    PrivateMemoryBytes = process.PrivateMemorySize64,
    ManagedBytes = GC.GetTotalMemory(forceFullCollection: false),
    HeapSizeBytes = gcInfo.HeapSizeBytes,
    FragmentedBytes = gcInfo.FragmentedBytes,
    MemoryLoadBytes = gcInfo.MemoryLoadBytes,
    TotalAvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
    HighMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes,
    TotalCommittedBytes = gcInfo.TotalCommittedBytes,
    TotalAllocatedBytes = totalAllocatedBytes,
    AllocationRateBytesPerSecond = allocationRateBytesPerSecond,
    Gen0Collections = GC.CollectionCount(0),
    Gen1Collections = GC.CollectionCount(1),
    Gen2Collections = GC.CollectionCount(2),
    PausePercent = gcInfo.PauseTimePercentage,
    PinnedObjects = gcInfo.PinnedObjectsCount,
    FinalizationPending = gcInfo.FinalizationPendingCount,
    FragmentationPercent = gcInfo.HeapSizeBytes == 0 ? 0 : 100.0 * gcInfo.FragmentedBytes / gcInfo.HeapSizeBytes,
    LohSizeBytes = gcInfo.GenerationInfo[3].SizeAfterBytes,
    LohFragmentedBytes = gcInfo.GenerationInfo[3].FragmentationAfterBytes,
    LohFragmentationPercent = gcInfo.GenerationInfo[3].SizeAfterBytes == 0 ? 0 : 100.0 * gcInfo.GenerationInfo[3].FragmentationAfterBytes / gcInfo.GenerationInfo[3].SizeAfterBytes,
    MemoryPressurePercent = gcInfo.HighMemoryLoadThresholdBytes == 0 ? 0 : 100.0 * gcInfo.MemoryLoadBytes / gcInfo.HighMemoryLoadThresholdBytes,
    ServerGc = System.Runtime.GCSettings.IsServerGC,
    ProcessHandleCount = process.HandleCount,
    ProcessThreadCount = process.Threads.Count,
    ThreadPoolThreadCount = ThreadPool.ThreadCount,
    ThreadPoolCompletedWorkItemCount = ThreadPool.CompletedWorkItemCount,
    ThreadPoolPendingWorkItemCount = ThreadPool.PendingWorkItemCount,
    ActiveTcpConnectionCount = socketMetrics.ActiveTcpConnectionCount,
    TcpListenerCount = socketMetrics.TcpListenerCount,
    UdpListenerCount = socketMetrics.UdpListenerCount,
    UsedSocketCount = socketMetrics.UsedSocketCount,
};
```

The code sample is intentionally aligned with the runtime snapshot fields defined in the performance dashboard and may be adapted to the available host environment.

### 6.3.2 Custom metrics integration

The dashboard shall support custom application metrics in addition to runtime GC/process metrics.

The custom metric integration shall be built on the existing common metrics abstraction:

- use the shared `IMetricsService` abstraction in `src/Common.Utilities/Metrics/MetricsService.cs`
- continue to register the metric feature through `services.AddMetrics(...)` in `src/Common.Utilities/Metrics/MetricsServiceCollectionExtensions.cs`
- register a session-aware decorator around the existing `MetricsService` implementation using DI, so that the decorated metrics service forwards values into the active dashboard session when one is collecting
- implement the decorator using a behavior-like wrapper pattern, making it easy to add additional integrations later
- make session collection the default behavior for custom counters, gauges, and timings
- allow the same API to be used from job, orchestration, queueing, and storage features so domain-specific runtime values can be captured in the same session context

Custom metric calls shall be no-ops or low-cost no-ops when no session is active, to avoid adding permanent instrumentation overhead outside performance diagnostics.

Custom metric types shall include at least:

- counters or incremental totals
- current gauges/live values
- duration measurements or tracked scopes

Custom metric series shall be persisted with the active session and exposed through the performance dashboard when the session is viewed.

The dashboard shall display custom metrics in their own chart panel or allow them to be added to a “Custom Metrics” chart if any custom series exist for the session.

#### Example: register the metrics service

A concrete implementation shall register the shared metrics feature and enable the session-aware decorator through `AddMetrics(...)`:

```csharp
builder.Services.AddMetrics(options => options
    .Enabled(true)
    .WithBehavior<SnapshotForwarderMetricsBehavior>()
    .AddEndpoints(true));

// The snapshot forwarder behavior wraps IMetricsService and forwards custom
// metric values into the active dashboard session when one is collecting.
```

This ensures the same `IMetricsService` API can be used throughout the application while the behavior forwards custom metrics into active dashboard sessions.

### Snapshot payload

Each snapshot shall include:

- timestamp
- node or instance identifier
- process identifier if available
- collection session identifier
- session name if available
- optional session tags, labels, or notes
- collected metric values for memory, CPU, GC, allocations, thread pool, and sockets

The dashboard shall display a clear indication of when the data was captured and from which node it originated.

### Multi-node behavior

When the application is deployed across multiple nodes:

- each node collects its own snapshots
- the dashboard shall show the node identity alongside each sample
- the user shall be able to filter to one node or compare multiple nodes
- the dashboard shall support a zoom or focus view for a single node

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

The initial dashboard view shall include:

- a status section showing whether collection is running, stopped, or disabled
- controls for start, stop, duration, interval, manual snapshot collection, and manual GC trigger
- a summary section with the latest values and trend indicators
- exactly two primary charts: a Memory History chart and a GC Pressure History chart
- a node selector or filter

The summary section shall display the following runtime indicators in card form:

- CPU usage percent and process/core identity
- system platform and runtime version
- physical memory: available and used memory
- process memory: private memory and working set
- GC memory: managed bytes, heap after latest GC, committed memory after GC
- fragmentation: heap fragmentation and LOH fragmentation
- LOH size after latest GC
- GC pressure: memory pressure percent and load/threshold details
- GC collection counts by generation and clear health indicators
- allocation rate and total allocated bytes
- socket activity counts for active TCP, TCP listeners, UDP listeners, and total sockets
- request throughput: requests per minute and total requests
- thread pool status: thread count, pending work item count, completed work item count

### Visualizations

The dashboard shall present exactly two primary charts:

- Memory History chart: a time series of memory-related metrics such as managed bytes, heap size, private memory, working set, committed memory, and LOH size
- GC Pressure History chart: a time series of pressure and activity metrics such as CPU usage, memory pressure percent, fragmentation percent, pause percent, and allocation rate

The charts shall be rendered with Plotly, consistent with the existing dashboard pages, and shall be readable and optimized for short analysis windows rather than dense long-term history.

### Developer productivity features

The dashboard shall also provide tools that help developers interpret and compare runtime diagnostics:

- session tagging and notes so a session can be labeled with purpose or context
- session comparison mode to view two sessions side-by-side or compare a session against a baseline
- a delta view for selected metrics between two sessions
- inline metric help text to explain the meaning of each card and chart
- export snapshot data to JSON or CSV for offline analysis
- a copy current snapshot to clipboard button that produces JSON for the active sample
- bookmarkable views for node/filter selections and saved session state
- threshold highlighting or warnings for sustained high memory pressure, allocation spikes, or long GC pauses
- an audit trail of session creation, end time, and user-provided comments where applicable

### Dashboard screen details

The initial dashboard shall reflect the following visual structure:

- A top summary panel showing the current snapshot in a card-grid layout with cards for:
  - CPU usage with process/thread identity
  - system/runtime summary
  - physical memory availability and usage
  - process memory (private memory, working set)
  - GC memory (managed bytes, heap after latest GC, committed memory after latest GC)
  - fragmentation details
  - LOH size after latest GC
  - GC pressure with memory pressure percent and threshold load
  - GC collection counts by generation and a health indicator
  - allocation rate and total allocated bytes
  - socket activity counts for TCP/UDP listeners and active connections
  - request throughput and total requests
  - thread pool status, including thread count, pending work items, and completed work items

- A Memory History chart panel with a retained-point indicator and a time-series plot of memory metrics including managed memory, heap size, committed memory, private memory, working set, and LOH size.

- A GC Pressure History chart panel with a retained-point indicator and a time-series plot of pressure/activity metrics including CPU percent, memory pressure percent, fragmentation percent, pause percent, and allocation rate.

Each chart panel shall include a concise title, a legend for the plotted series, and a point retention summary so the developer can understand how many samples are currently retained in the session.

### Node-aware drill-down

The dashboard shall allow the user to:

- select a node from a dropdown control
- focus on a single node’s timeline
- compare a selected node against the aggregate view
- preserve the current node selection and other dashboard filters across page reloads using browser localStorage
- identify the most memory-intensive or slowest node in the current session

### Session view

The dashboard shall clearly show the active session and allow the user to:

- view the current session snapshot window
- inspect the latest values
- clear or restart the session
- delete the session and remove all collected data from the store
- delete all sessions at once and remove all collected data from the store
- select a previously saved or completed session for review
- open a shared session directly from a session URL
- trigger a manual one-off snapshot collection from the dashboard
- trigger a manual GC collection from the dashboard

## Storage Model

### Default approach

For the initial version, the feature shall use durable storage for sessions and snapshots. This is required because the dashboard needs to preserve data across page refreshes, application restarts, and later review of completed sessions.

### Durable storage behavior

The feature shall support durable persistence from the initial release.

Recommended behavior:

- durable storage is the default and required behavior for the first release
- sessions and snapshots shall remain available after refreshes, restarts, and navigation away from the page
- durable storage should be implemented using shared storage or a node-accessible file or blob location when available
- each node should write its own snapshots to the configured store so the dashboard can aggregate or inspect them by node

In-memory-only storage is not sufficient for this release.

### Retention rules

The feature shall not be designed as a long-term archive.

Retention defaults should be short, for example:

- current session only
- a small rolling window of recent snapshots
- optional export or download for deeper analysis

## Architecture

The proposed implementation should follow a simple architecture:

- a background performance collector service running inside the application process
- a session store that holds the current snapshots for the active session and any completed sessions
- a dashboard endpoint or page that renders the current snapshot state and allows session selection
- a node-aware aggregation layer for multi-node scenarios
- an application-facing control API for programmatic start, stop, status, and deletion operations
- an extensible session-aware custom metrics pipeline integrated through `IMetricsService` and `services.AddMetrics(...)`
- a small control surface for start, stop, duration, interval, manual snapshot collection, manual GC trigger, and session deletion
- shareable session URL handling so a specific session can be loaded directly by route

The collector service should be isolated from the dashboard rendering layer so that collection can be started or stopped independently of the UI.

## Configuration and Defaults

The feature shall be configured through application options and environment settings.

Suggested defaults:

- enabled: false
- sampling interval: 1 second
- duration: 30 seconds
- automatic stop: true
- default session name: ISO timestamp
- max retained snapshots: 100
- refresh interval: 5 seconds
- storage mode: in-memory by default
- gc trigger: available from the dashboard
- programmatic control: enabled through an application service interface

## Security and Operational Notes

- the feature shall be restricted to authorized users or developer environments
- it shall not capture sensitive user data beyond runtime-performance indicators
- it shall be clearly documented as a diagnostic feature, not a production monitoring replacement
- it should be easy to disable in production or test environments

## Acceptance Criteria

The feature is considered complete when:

- a developer can enable and start a short performance collection session from the dashboard
- the dashboard shows at least memory, allocations, GC, and CPU metrics
- snapshots are displayed with timestamps and node identity
- the user can filter or focus on a specific node
- the dashboard supports configurable refresh intervals
- sessions can be given a custom name, or default to an ISO timestamp name
- the user can select a completed or saved session for later viewing
- application code can start and stop sessions programmatically, including an optional session name
- the programmatic API handles missing collectors or metrics infrastructure gracefully without runtime errors, logging a warning instead
- sessions can be deleted from the UI or API, removing all collected data from the active store regardless of whether it is in-memory or durable
- a session can be opened directly through a shareable link and displayed in the dashboard
- the current snapshot can be copied to clipboard as JSON via a dashboard button
- the dashboard supports exporting session data in JSON or CSV format
- the feature can be turned off or disabled without affecting normal application behavior

## Open Questions

The following decisions are now resolved for the initial implementation:

- durable storage is required for the first release; sessions and snapshots shall be persisted and available beyond a single browser session
- the initial dashboard shall include exactly two primary charts; any additional detailed views can be introduced later
- the exact metric sources and formulas for each runtime target will be finalized during implementation, while the core metrics listed above remain part of the initial release
- node selection shall be implemented as a dropdown
- dashboard filters and selections shall be persisted in browser localStorage so that page reloads and returns restore the same view state
