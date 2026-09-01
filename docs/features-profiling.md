
# Profiling

> Collect bounded local runtime snapshots, compare them, and compute deterministic evidence-backed performance signals without an AI service or an overall score.

[TOC]

## Overview

Profiling is an opt-in developer feature for investigating CPU, memory, allocations, and garbage collection on a local development machine. A session collects immutable runtime snapshots for a bounded duration. You can control the same lifecycle through the dashboard, Console Commands, or programmatic services.

Use Profiling when you want to:

- observe a repeatable local workload over time;
- investigate CPU saturation, allocation churn, memory growth, fragmentation, or GC pressure;
- compare an earlier and later point in one run;
- preserve a useful run for later inspection.

Evaluation always targets one selected node. It can use all available snapshots for that node in a session or exactly two ordered snapshots from the same session and node. Results are calculated on demand and are never persisted.

Profiling is not an APM or production-monitoring product. It does not continuously monitor applications, compare nodes or sessions, calculate one overall performance score, or use AI analysis.

## Challenges

Local performance investigations need repeatable evidence without turning development builds into a production monitoring system. Raw point-in-time counters are difficult to interpret unless snapshots share a bounded session, node identity, timing, and data-quality context.

## Solution

Profiling collects immutable runtime snapshots in bounded sessions and stores the related runtime context, markers, segments, and custom metrics. Dashboard, console, and programmatic APIs share one lifecycle service. Evaluation applies fixed rules to one node's timeline or to two ordered snapshots and returns evidence, confidence, actions, and limitations without persisting an overall score.

## Key Features

- bounded CPU, memory, allocation, and garbage-collection sampling
- in-memory and Entity Framework stores
- dashboard, console-command, and programmatic control
- phase markers, measured segments, metadata, and supported custom metrics
- deterministic timeline and two-snapshot evaluation
- portable archive and one-way Perfetto export
- shared-store and Broadcasting support for multi-node development sessions

## Architecture

`IProfilingControlService` coordinates session lifecycle over Broadcasting. Each participating node uses the collector and runtime probe to append snapshots to `IProfilingStore`. `IProfilingQueryService` reads sessions and computes evaluations on demand. Measurement, archive, Perfetto, dashboard, and console adapters build on those same contracts.

## Use Cases

- reproduce and inspect allocation churn or memory growth in a local workload
- compare snapshots before and after a controlled operation
- mark warm-up, workload, and recovery phases on one timeline
- retain or transfer a terminal session for later inspection
- collect one development session from several directly reachable processes

## Basic Usage

The following development-only endpoint starts a bounded in-memory session, records a marker, waits for samples, and stops the session. Each result is checked before its value is used, and the response reports the session key and final state.

```csharp
using BridgingIT.DevKit.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProfiling(options => options
    .Enabled(builder.Environment.IsDevelopment())
    .SamplingInterval(TimeSpan.FromSeconds(1))
    .Duration(TimeSpan.FromSeconds(10)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/profiling/sample", async (
        IProfilingControlService profiling,
        CancellationToken cancellationToken) =>
    {
        var started = await profiling.StartAsync(
            new ProfilingStartRequest("sample workload"),
            cancellationToken);

        if (started.IsFailure)
        {
            return Results.Problem(string.Join(
                "; ",
                started.Errors.Select(error => error.Message)));
        }

        var marked = await profiling.AddPhaseMarkerAsync(
            "workload started",
            cancellationToken);

        if (marked.IsFailure)
        {
            await profiling.StopAsync(CancellationToken.None);
            return Results.Problem(string.Join(
                "; ",
                marked.Errors.Select(error => error.Message)));
        }

        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

        var stopped = await profiling.StopAsync(cancellationToken);
        if (stopped.IsFailure)
        {
            return Results.Problem(string.Join(
                "; ",
                stopped.Errors.Select(error => error.Message)));
        }

        return Results.Ok(new
        {
            Session = stopped.Value.Session.Identity.Key,
            stopped.Value.Session.State,
            ParticipatingNodes = stopped.Value.NodeOutcomes.Count
        });
    });
}

app.Run();
```

Calling `POST /dev/profiling/sample` returns the readable session key and a terminal state. The session remains available in the in-memory store until retention removes it or the process exits.

## Local development setup

Start with the process-local in-memory provider when profiling one application process. Keep collection, the dashboard, and Console Commands restricted to Development.

```csharp
builder.Services
    .AddProfiling(options => options
        .Enabled(builder.Environment.IsDevelopment())
        .SamplingInterval(TimeSpan.FromSeconds(1))
        .Duration(TimeSpan.FromSeconds(30)))
    .AddConsoleCommands(builder.Environment.IsDevelopment());

builder.Services.AddDashboard(options => options.Enabled(builder.Environment.IsDevelopment()));
```

After building the application, map the registered endpoints as usual:

```csharp
app.MapEndpoints();
```

`AddProfiling` uses `InMemoryProfilingStore` unless you explicitly select another provider. It also registers the required Profiling handlers over the standalone Broadcasting feature. When Profiling is disabled, no collector or operational runtime starts.

The global `AddDashboard` call discovers Profiling automatically. There is no Profiling-specific dashboard registration call: the navigation item appears when Profiling is enabled and stays hidden when it is disabled.

The built-in defaults are:

| Setting | Default |
| --- | ---: |
| Sampling interval | 1 second |
| Minimum sampling interval | 500 milliseconds |
| Session duration | 30 seconds |
| Dashboard refresh | 5 seconds |
| Maximum retained unpinned terminal sessions | 20 |
| Maximum unpinned terminal session age | 7 days |
| Participation deadline | 1 second |
| Finalization grace period | 1 second |

Every session has an automatic maximum duration. A manual stop ends collection early without changing that original logical end time.

## Dashboard

The dashboard plugin is available at `/_bdk/dashboard/profiling` when the dashboard uses its default group path. A selected session and node can be shared with readable eight-character keys:

```text
/_bdk/dashboard/profiling?session=a1b2c3d4&node=e5f6g7h8
```

The dashboard groups the workflow into four tabs:

| Tab | Use it to |
| --- | --- |
| **Overview** | Inspect the latest or selected snapshot, follow memory and GC-pressure charts, and view phase and action markers, measured ranges, and the selected snapshot. |
| **Analysis** | Evaluate the selected node's timeline or the selected snapshot pair using deterministic KPIs and evidence-backed signals. |
| **Comparison** | Compare exactly two ordered snapshots from the selected session and node. |
| **Info** | Inspect measured segments, custom metric observations, and immutable runtime context. |

Session controls above the tabs start or stop collection, take a manual snapshot, request a normal `GC.Collect()`, add phase markers, edit metadata, export or import sessions, and remove stored data. Information icons explain metrics and evaluation results in plain language.

### Dashboard usage guide

1. **Start a session.** Enter a short **Name**, choose the sampling interval and maximum duration, and select the green start control. The control becomes a red stop control while collection is running. Use **History** when you want to inspect an existing session instead.
2. **Reproduce one focused scenario.** Exercise the code path you want to investigate. Add phase markers before transitions such as warm-up, workload, and recovery so those moments are visible on the charts.
3. **Inspect the evidence.** In **Overview**, choose the contributing **Node** and follow the snapshot cards and charts. Select a snapshot to mark it on both charts and inspect its values. The focus actions enlarge both charts or show every metric from the selected snapshot.
4. **Compare or evaluate.** Use **Comparison** for a bounded earlier-versus-later question. Use **Analysis** for trends across the complete selected-node timeline. Read data-quality limitations first, then review each KPI, signal, supporting evidence, and suggested action.
5. **Preserve or investigate a useful run.** Stop the session, add metadata or pin it when needed, then use the download action next to **History** for an importable archive or the activity action for a Perfetto trace. Imported archive JSON reappears in History and can be inspected and evaluated normally. A Perfetto trace is for visualization only. Use **Copy JSON** when only the selected snapshot is needed.
6. **Clean up deliberately.** The overflow menu imports sessions and contains deletion operations. Delete the selected terminal session, remove all unpinned sessions, or use the confirmed clear-all operation to empty the Profiling store.

For useful timeline analysis, collect at least five snapshots over five seconds. Ten snapshots over ten seconds gives the evaluator enough coverage for high-confidence results when the required metrics and sampling quality are also available. Compare related evidence instead of interpreting one isolated value: for example, CPU together with allocation rate and GC pause, or managed-heap growth together with post-Gen2 evidence.

A manual snapshot works during collection. When no session is active, it creates a terminal standalone session containing that snapshot. Requesting garbage collection changes the runtime you are measuring, so use it deliberately when investigating post-GC retention rather than as a routine step.

Session and node selection remain visible above the tabs. Metadata is viewed and edited through a compact button and standard dashboard dialog. Session operations use icon controls with tooltips and an overflow panel for import and destructive actions. The Sessions and Current Snapshot sections can be collapsed to make more room for the charts.

### Optional local stress workload

The flame action immediately left of the refresh interval starts the default stress workload and returns without blocking the dashboard request. It uses dedicated CPU workers, sustains short-lived and large-object allocations, retains a bounded 32–128 MiB based on available memory, and forces one full GC while retained objects remain reachable. The complete background workload is recorded as a named `Profiling stress test` segment so its duration is visible as a labeled range on both charts. A second run is rejected until the current 30-second run finishes. The workload affects only the process hosting the dashboard and stops during application shutdown.

Application code can reuse `IProfilingStressService` with a `ProfilingStressRequest` to select the duration, CPU-worker count, and retained-memory size for one run. `ProfilingStressRequest.Default` provides the same adaptive 30-second settings used by the dashboard; the dashboard intentionally exposes no editable stress profile.

Use this workload only to verify that collection and visualization work or to learn how known CPU, allocation, memory, and GC activity appears. It is not a benchmark and does not represent an application's real workload.

### Live analysis, refresh, and charts

The browser-wide **Live analysis** switch is off by default. When enabled, it evaluates only after a new snapshot, never overlaps evaluation calls, and cannot run more frequently than dashboard refresh. This switch changes browser behavior only; it does not enable collection or alter console/programmatic evaluation.

Periodic refresh is state-preserving. It patches collection status, metrics, charts, snapshot choices, segments, and custom metrics in place instead of replacing the complete workbench. Focused controls, unsaved metadata and marker text, selected comparison snapshots, file selection, open detail panels, analysis output, chart filters, and scroll position remain unchanged. Selecting another session or node deliberately resets context-specific controls and analysis.

Chart timelines display browser-local time, matching the snapshot selectors, while stored and exported timestamps remain UTC. Every snapshot option includes its sequence, local date and time to the second, and readable snapshot key. Phase and action labels reserve additional space above the plotting area so their rotated text remains visible.

The charts use Plotly's standard interaction controls, including area-selection zoom, pan, autoscale, and reset. Mouse-wheel zoom is disabled so scrolling over a chart does not unexpectedly change its time range. The chart focus action opens both charts together on the same timeline.

Profiling dashboard routes inherit the dashboard's authentication and authorization policy. Do not expose the dashboard anonymously. There is intentionally no evaluation export, copy, or download route.

## Console commands

Register commands with `.AddConsoleCommands()`. The primary group is `profiling` and the short group alias is `prof`.

| Command | Purpose |
| --- | --- |
| `profiling status` | Show feature availability, the active session, state, and participating-node count. |
| `profiling start --name warmup --interval 500ms --duration 30s` | Start a session with optional name, sampling interval, and duration overrides. |
| `profiling stop` | Best-effort stop of the active logical session across the current target snapshot. |
| `profiling snapshot --name checkpoint` | Capture immediately; when idle, create one terminal standalone snapshot session. |
| `profiling gc` | Request one normal deployment-wide `GC.Collect()` action. |
| `profiling mark --name "load started"` | Add a shared phase marker to the active session. |
| `profiling analyze --session a1b2c3d4 --node e5f6g7h8` | Analyze the complete available timeline for one selected node. |
| `profiling analyze --session a1b2c3d4 --node e5f6g7h8 --snapshot-a i9j0k1l2 --snapshot-b m3n4o5p6` | Analyze exactly two ordered snapshots. |
| `profiling analyze --session a1b2c3d4 --node e5f6g7h8 --json` | Write the computed evaluation contract as JSON without persisting it. |
| `profiling export --session a1b2c3d4 --output run.json` | Export a complete terminal session archive. Add paired `--node` and `--snapshot` to export one snapshot. |
| `profiling export --session a1b2c3d4 --format perfetto --output run.perfetto.json` | Export a complete terminal session as a one-way Perfetto visualization trace. |
| `profiling export --session a1b2c3d4 --output run.json --overwrite` | Explicitly replace an existing archive after a successful temporary-file write. |
| `profiling import --file run.json` | Import an archive as a fresh terminal session and report its new key. |
| `profiling clear --yes` | Remove every stored session and snapshot, including pinned sessions. |

`profiling clear` without `--yes` changes nothing. Clear is also rejected while a session is active.

## Programmatic usage

`IProfilingControlService` is the shared lifecycle path used by dashboard and Console Commands:

```csharp
var started = await control.StartAsync(
    new ProfilingStartRequest("checkout", Duration: TimeSpan.FromSeconds(20)),
    cancellationToken);

await control.AddPhaseMarkerAsync("warmup complete", cancellationToken);
await control.SnapshotAsync(cancellationToken: cancellationToken);
await control.CollectGarbageAsync(cancellationToken);
await control.StopAsync(cancellationToken);
```

Use `IProfilingMeasurementService` for developer-defined operations. When no session is active, the outer scope owns a bounded session and stops it when disposed. During an active session it creates a node-owned segment and does not stop the shared session. Nested scopes create nested segments on the same node.

```csharp
await measurements.MeasureAsync(
    "import customers",
    token => importer.ImportAsync(token),
    cancellationToken);

await using var scope = (await measurements.BeginAsync("rebuild index", cancellationToken)).Value;
try
{
    await rebuilder.RunAsync(cancellationToken);
}
catch (Exception exception)
{
    scope.MarkFailed(exception); // stores safe exception metadata, not a stack trace
    throw;
}
```

Use `IProfilingQueryService` for stored data, raw comparisons, raw snapshot export, and computed evaluation:

```csharp
var evaluation = await queries.EvaluateAsync(
    new ProfilingEvaluationRequest(sessionKey, nodeKey),
    cancellationToken);

var pair = await queries.EvaluateAsync(
    new ProfilingEvaluationRequest(sessionKey, nodeKey, snapshotAKey, snapshotBKey),
    cancellationToken);

var rawJson = await queries.ExportSnapshotsJsonAsync(sessionKey, nodeKey, cancellationToken);
```

Use `IProfilingArchiveService` for round-trippable archives. Callers own the streams; the service never accepts a filesystem path:

```csharp
await archives.ExportSessionAsync(sessionKey, destination, cancellationToken);
await archives.ExportSnapshotAsync(
    sessionKey,
    nodeKey,
    snapshotKey,
    destination,
    cancellationToken);

var imported = await archives.ImportAsync(source, cancellationToken);
var importedSessionKey = imported.Value.SessionKey;
```

Use `IProfilingPerfettoExportService` when another developer tool needs the session as Trace Event JSON. The caller owns the stream, and only terminal sessions are accepted:

```csharp
await perfetto.ExportSessionAsync(sessionKey, destination, cancellationToken);
```

Open the resulting `*.perfetto.json` file in [Perfetto UI](https://ui.perfetto.dev/). The trace uses a session lane for shared phase markers and a separate synthetic process for each profiling node. Captured numeric metrics become counters, action and snapshot markers become instant events, and measured segments become duration events. Runtime and session context remain available in event details.

This is a visualization export, not a sampled call-stack trace. It does not create flame graphs, method-level CPU attribution, or allocation stack traces because Profiling does not collect that evidence.

Imported sessions appear in the normal session list and can be selected, inspected, and evaluated like any other terminal session. This does not add session-to-session comparison.

Application code can also emit supported stable, untagged .NET `Meter` counters, gauges, and durations. Profiling bounds accepted instrument identities and does not accept high-cardinality tags.

## Durable and multi-node registration

Most local profiling needs only the in-memory setup. Use this section when profiling must survive process restarts or one session must collect from multiple application processes.

Independent processes require both of these capabilities:

1. One shared Profiling store, normally the Entity Framework provider.
2. One shared Broadcasting registry plus a transport through which every registered node is directly reachable.

Register the shared Broadcast provider before adding the HTTP transport and Profiling:

```csharp
builder.Services
    .AddBroadcasting(options => options
        .Enabled(builder.Environment.IsDevelopment())
        .Scopes("my-application"))
    .UseRegistryProvider(typeof(MySharedBroadcastRegistry))
    .WithHttpTransport(options =>
        options.SharedSecret(builder.Configuration["Broadcasting:SharedSecret"]));

builder.Services
    .AddProfiling(options => options.Enabled(builder.Environment.IsDevelopment()))
    .WithEntityFrameworkStore<AppDbContext>()
    .AddConsoleCommands(builder.Environment.IsDevelopment());
```

`MySharedBroadcastRegistry` is an application-selected `IBroadcastRegistryStore` implementation whose `Capabilities.IsShared` value is `true`. Every process must use the same registry, Profiling database, Broadcast scopes, and HTTP authentication secret. `app.MapEndpoints()` maps the Broadcast receiver and Profiling dashboard endpoints.

Profiling rejects start and snapshot operations before creating a session or publishing a command when multiple targets are present and the selected Profiling store reports that it is process-local. This prevents a misleading partial multi-node session.

### Entity Framework context

The consuming application owns its `DbContext` and implements `IProfilingContext`:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IProfilingContext
{
    public DbSet<ProfilingSessionEntity> ProfilingSessions { get; set; }
    public DbSet<ProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }
    public DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }
    public DbSet<ProfilingParticipationEntity> ProfilingParticipations { get; set; }
    public DbSet<ProfilingSnapshotEntity> ProfilingSnapshots { get; set; }
    public DbSet<ProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureProfiling();
    }
}
```

`ConfigureProfiling()` keeps high-volume or independently addressable data in tables and maps session-owned runtime context, markers, segments, and tags into JSON columns.

The bITdevKit repository does not provide a Profiling migration. After implementing `IProfilingContext`, the consuming application creates, reviews, and deploys its own EF migration using its normal migration workflow.

## Session and node semantics

- Start freezes one exact Broadcast target snapshot. Only nodes that accept that start command are expected participants.
- A later manual snapshot targets all currently registered nodes. A late node contributes as an ad-hoc participant and does not change the expected set.
- Stop is best effort. The logical session becomes `Stopped` before the current target snapshot receives the stop command, and an unreachable node is reported in the immediate outcomes.
- Automatic finalization after the original end and grace period is idempotent. An expected participant that did not complete, or that recorded a failed capture, produces `CompletedWithWarnings`.
- Startup reconciliation performs one bounded pass for overdue running sessions. It does not poll the store.
- Late records cannot recreate a deleted or cleared session.
- Retention removes old unpinned terminal sessions by age and count. Pinned sessions are retained until explicitly deleted or included in a confirmed clear-all operation.

## Deterministic evaluation

Evaluation returns independent KPIs, evidence-backed signals, suggested actions, data quality, and limitations. There is explicitly no combined performance score. Rule thresholds and labels are built in and cannot be configured, extended, or versioned.

Timeline analysis needs at least five valid snapshots spanning five seconds before it emits interpretive signals. High confidence also needs at least ten snapshots spanning ten seconds, complete required evidence, acceptable sampling quality, and no attached debugger. Two-snapshot signals are always low confidence.

The principal fixed rules are:

| Area | Fixed evidence |
| --- | --- |
| CPU | Sustained: average at least 70% and at least 60% of intervals at 70%+. Strong: average at least 85% and at least 80% of intervals at 80%+. Rising: second-half increase of at least 20 percentage points and an elevated ending value. |
| Managed/private/LOH growth | At least 20% relative growth plus floors of 32 MiB managed heap, 64 MiB private memory, or 32 MiB LOH. |
| Retention | Managed growth remains after directly observed Gen2 evidence. Missing post-Gen2 evidence suppresses this signal; it is not inferred from ordinary before/after snapshots. |
| LOH fragmentation | Ending fragmentation at least 20% after a rise of at least 10 percentage points. |
| Allocations | Sustained average at least 50 MiB/s; rising allocation at least doubles with a 10 MiB/s increase floor; churn also requires Gen0 rate at least 0.5 collections/s without material heap growth. |
| GC | Notable pause burden at least 5%; strong at least 10%; frequent full GC requires at least two Gen2 collections and at least 0.1 Gen2 collections/s. Supporting allocation or memory evidence distinguishes broader GC pressure. |

Signals use only the simple labels `Notable` and `Investigate`. They focus on CPU, memory, allocation, and GC evidence and return one short fixed suggested action.

### Evaluation limitations

The result explicitly reports limitations instead of manufacturing certainty when:

- the selected timeline is still collecting or has fewer than five snapshots/five seconds;
- the session stopped, failed, or completed with warnings;
- a debugger is attached;
- metrics, post-GC evidence, or intervals are unavailable;
- cumulative counters reset or snapshot sequences contain gaps;
- capture failures occurred, sampling coverage is below 90%, capture overhead is high, or sampling delay is material.

Runtime availability differs by operating system and .NET runtime. An unavailable metric is represented as unavailable and suppresses only the rules that require it.

## Export boundary

Raw JSON export contains normal immutable runtime snapshots only. A selected node export contains that node's snapshots; a complete-session export contains snapshots from expected and ad-hoc contributors. It excludes runtime context, markers, segments, custom metrics, evaluation KPIs, signals, actions, and limitations.

Evaluation JSON produced by `profiling analyze --json` is computed command output, not a persisted or downloadable dashboard artifact.

Portable archives are a separate fixed JSON contract for durable local transfer. Format `bitdevkit.profiling.archive`, version `1`, supports complete terminal sessions and individual immutable snapshots up to 25 MiB. Import generates fresh eight-character lowercase session, node, and snapshot keys, validates the complete graph before one atomic provider mutation, and never restores private Broadcast identities. Re-importing creates another independent terminal copy. The in-memory and Entity Framework providers use their existing physical storage model; no archive table or migration is required.

Perfetto export is a separate, one-way Trace Event JSON representation for visual investigation. It includes session and runtime context, readable keys, snapshot counters, shared phase markers, node actions, measured segments, and custom metric counters. It excludes internal GUIDs and computed evaluation results. Perfetto JSON is not accepted by `profiling import`; use the portable archive when a session must be restored to History.

## Security and operational guidance

- Enable Profiling only for trusted local Development environments. Snapshot collection and forced GC change runtime behavior and consume CPU, memory, and storage.
- Protect the dashboard with the existing dashboard authentication and authorization policy.
- Protect multi-node Broadcast HTTP traffic with a shared secret or an application-selected `IBroadcastHttpAuthentication` implementation.
- Treat host names, process ids, runtime versions, session notes, tags, and raw metrics as diagnostic information.
- Do not put secrets, personal data, request payloads, or high-cardinality identifiers in session metadata, marker names, segment names, or custom metrics.
- Prefer the in-memory provider for one local process. Use shared Profiling and Broadcast providers only when multiple independent development processes must participate.

## Related features

- [Presentation Dashboard](./features-presentation-dashboard.md)
- [Console Commands](./features-presentation-console-commands.md)
- [Common Observability Tracing](./common-observability-tracing.md)
