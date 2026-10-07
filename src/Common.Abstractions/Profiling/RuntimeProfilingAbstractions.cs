// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Stores profiling sessions and their immutable diagnostic observations.
/// </summary>
/// <remarks>
/// Implementations own atomic lifecycle coordination. Application-facing callers use readable
/// keys, while provider and runtime code may use internal identifiers after one resolution.
/// </remarks>
/// <example><code>var active = await store.GetActiveSessionAsync(cancellationToken);</code></example>
public interface IRuntimeProfilingStore
{
    /// <summary>Gets provider capabilities.</summary>
    ProfilingStoreCapabilities Capabilities { get; }

    /// <summary>Atomically creates a session or returns the existing active session.</summary>
    Task<IResult<RuntimeProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
        RuntimeProfilingSessionCreateRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets the active logical session when one exists.</summary>
    Task<IResult<RuntimeProfilingSession>> GetActiveSessionAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Finds a session by public readable key.</summary>
    Task<IResult<RuntimeProfilingSession>> FindSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lists stored sessions in reverse chronological order.</summary>
    Task<IResult<IReadOnlyList<RuntimeProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Updates editable descriptive session metadata.</summary>
    Task<IResult<RuntimeProfilingSession>> UpdateSessionMetadataAsync(
        string sessionKey,
        RuntimeProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>Transitions a session when its current state matches an expected state.</summary>
    Task<IResult<RuntimeProfilingSession>> TryTransitionSessionAsync(
        Guid sessionId,
        IReadOnlyCollection<RuntimeProfilingSessionState> expectedStates,
        RuntimeProfilingSessionState nextState,
        DateTimeOffset transitionedUtc,
        CancellationToken cancellationToken = default
    );

    /// <summary>Finds registered Runtime process metadata; absent remote registrations return an absent value.</summary>
    /// <example><code>var node = await store.FindNodeAsync(correlation, cancellationToken);</code></example>
    Task<IResult<ProfilingNode>> FindNodeAsync(RuntimeProfilingNodeCorrelation correlation, CancellationToken cancellationToken = default);

    /// <summary>Gets or creates the stable profiling node for one Broadcast process registration.</summary>
    Task<IResult<ProfilingNode>> GetOrCreateNodeAsync(
        RuntimeProfilingNodeCorrelation correlation,
        ProfilingNode proposedNode,
        CancellationToken cancellationToken = default
    );

    /// <summary>Adds or updates a node's session participation state and cumulative totals.</summary>
    Task<IResult<RuntimeProfilingNodeParticipation>> UpsertParticipationAsync(
        RuntimeProfilingNodeParticipation participation,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stores immutable node runtime context once per session and node.</summary>
    Task<IResult<RuntimeProfilingContext>> AddRuntimeContextAsync(
        RuntimeProfilingContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>Appends one immutable runtime snapshot.</summary>
    Task<IResult<RuntimeProfilingSnapshot>> AddSnapshotAsync(
        RuntimeProfilingSnapshot snapshot,
        CancellationToken cancellationToken = default
    );

    /// <summary>Atomically adds a shared marker only while the session remains active.</summary>
    Task<IResult<ProfilingMarker>> AddMarkerAsync(
        ProfilingMarker marker,
        CancellationToken cancellationToken = default
    );

    /// <summary>Adds or closes a node-owned segment.</summary>
    Task<IResult<ProfilingSegment>> UpsertSegmentAsync(
        ProfilingSegment segment,
        CancellationToken cancellationToken = default
    );

    /// <summary>Appends one immutable custom metric observation.</summary>
    Task<IResult<ProfilingMetricObservation>> AddMetricObservationAsync(
        ProfilingMetricObservation observation,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads all records for one session.</summary>
    Task<IResult<RuntimeProfilingSessionData>> GetSessionDataAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Atomically inserts one complete, already-remapped terminal session graph.</summary>
    /// <param name="data">The complete terminal session graph to insert.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>The inserted session or a typed validation or provider failure.</returns>
    /// <example><code>var imported = await store.ImportSessionAsync(data, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingSession>> ImportSessionAsync(
        RuntimeProfilingSessionData data,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes one terminal session and all associated records.</summary>
    Task<IResult<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes all unpinned terminal sessions.</summary>
    Task<IResult<int>> DeleteUnpinnedSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically clears the complete store only when no session is active.</summary>
    Task<IResult<ProfilingClearResult>> ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies configured age and count retention to unpinned terminal sessions.</summary>
    Task<IResult<int>> ApplyRetentionAsync(
        int maximumRetainedSessions,
        TimeSpan maximumSessionAge,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Captures one provider-neutral runtime snapshot without scheduling or persistence.</summary>
/// <example><code>var snapshot = await probe.CaptureAsync(request, cancellationToken);</code></example>
public interface IRuntimeProfilingSnapshotProbe
{
    /// <summary>Captures one immutable runtime snapshot.</summary>
    Task<IResult<RuntimeProfilingSnapshot>> CaptureAsync(
        RuntimeProfilingCaptureRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Creates immutable non-sensitive context for one session node.</summary>
/// <example><code>var context = factory.Create(session, node);</code></example>
public interface IRuntimeProfilingContextFactory
{
    /// <summary>Creates the context that is stored once per session and node.</summary>
    RuntimeProfilingContext Create(RuntimeProfilingSession session, ProfilingNode node);
}

/// <summary>Owns node-local collection admission and single-flight capture.</summary>
/// <example><code>await collector.StartAsync(session, cancellationToken);</code></example>
public interface IRuntimeProfilingCollector
{
    /// <summary>Starts or idempotently accepts local collection for a session.</summary>
    Task<IResult> StartAsync(
        RuntimeProfilingSession session,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stops local collection for the identified session.</summary>
    Task<IResult> StopAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Captures one immediate local snapshot for a session.</summary>
    Task<IResult<RuntimeProfilingSnapshot>> CaptureAsync(
        RuntimeProfilingSession session,
        RuntimeProfilingNodeRole role,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Provides the one dashboard, console, and programmatic profiling control path.
/// </summary>
/// <example><code>var result = await control.StartAsync(request, cancellationToken);</code></example>
public interface IRuntimeProfilingControlService
{
    /// <summary>Gets feature availability and active-session status.</summary>
    Task<IResult<RuntimeProfilingStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts or returns the active deployment-wide session.</summary>
    Task<IResult<RuntimeProfilingControlResult>> StartAsync(
        RuntimeProfilingStartRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stops the active deployment-wide session.</summary>
    Task<IResult<RuntimeProfilingControlResult>> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Collects one deployment-wide manual snapshot.</summary>
    Task<IResult<RuntimeProfilingControlResult>> SnapshotAsync(
        string standaloneSessionName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Triggers one normal deployment-wide <see cref="GC.Collect()"/> action.</summary>
    Task<IResult<RuntimeProfilingControlResult>> CollectGarbageAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Adds one shared marker to the active session.</summary>
    Task<IResult<ProfilingMarker>> AddMarkerAsync(
        string name,
        CancellationToken cancellationToken = default
    );

    /// <summary>Restarts a selected session as a new clean session.</summary>
    Task<IResult<RuntimeProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes one terminal session.</summary>
    Task<IResult<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes all unpinned terminal sessions.</summary>
    Task<IResult<int>> DeleteUnpinnedSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears all stored profiling data after caller confirmation.</summary>
    Task<IResult<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Queries stored profiling data without implementing lifecycle behavior.</summary>
/// <example><code>var data = await queries.GetSessionAsync(sessionKey, cancellationToken);</code></example>
public interface IRuntimeProfilingQueryService
{
    /// <summary>Lists stored sessions.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Stored sessions in provider order, or a typed availability failure.</returns>
    /// <example><code>var sessions = await queries.ListSessionsAsync(cancellationToken);</code></example>
    Task<IResult<IReadOnlyList<RuntimeProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads one complete session data set.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The complete provider-neutral session data set.</returns>
    /// <example><code>var data = await queries.GetSessionAsync(sessionKey, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingSessionData>> GetSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads the selected node timeline and its related session records.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="nodeKey">The public node key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The selected node read model without deployment aggregation.</returns>
    /// <example><code>var node = await queries.GetNodeSessionAsync(sessionKey, nodeKey, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingNodeSessionData>> GetNodeSessionAsync(
        string sessionKey,
        string nodeKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Updates descriptive session metadata.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="metadata">The complete editable metadata replacement.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>The updated session without modifying observations.</returns>
    /// <example><code>var session = await queries.UpdateMetadataAsync(sessionKey, metadata, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingSession>> UpdateMetadataAsync(
        string sessionKey,
        RuntimeProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>Restarts a selected session through the shared lifecycle service.</summary>
    /// <param name="sessionKey">The public source-session key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shared lifecycle result for the replacement session.</returns>
    /// <example><code>var restarted = await queries.RestartAsync(sessionKey, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes one selected terminal session through the shared lifecycle service.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the selected terminal session was deleted.</returns>
    /// <example><code>var deleted = await queries.DeleteSessionAsync(sessionKey, cancellationToken);</code></example>
    Task<IResult<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes every unpinned terminal session through the shared lifecycle service.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of deleted unpinned terminal sessions.</returns>
    /// <example><code>var count = await queries.DeleteUnpinnedSessionsAsync(cancellationToken);</code></example>
    Task<IResult<int>> DeleteUnpinnedSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears all profiling records through the confirmed shared lifecycle service.</summary>
    /// <param name="confirmed">Whether the caller explicitly confirmed the destructive reset.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The committed clear result.</returns>
    /// <example><code>var cleared = await queries.ClearAsync(true, cancellationToken);</code></example>
    Task<IResult<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    );

    /// <summary>Serializes only normal immutable snapshots as raw JSON.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="nodeKey">An optional public node key; omit it for the complete session.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A JSON array containing only normal runtime snapshots.</returns>
    /// <example><code>var json = await queries.ExportSnapshotsJsonAsync(sessionKey, nodeKey, cancellationToken);</code></example>
    Task<IResult<string>> ExportSnapshotsJsonAsync(
        string sessionKey,
        string nodeKey = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Compares exactly two ordered snapshots from the same session and node.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="nodeKey">The public node key.</param>
    /// <param name="snapshotAKey">The earlier public snapshot key.</param>
    /// <param name="snapshotBKey">The later public snapshot key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Fixed raw metric deltas with safe percentage values.</returns>
    /// <example><code>var comparison = await queries.CompareSnapshotsAsync(sessionKey, nodeKey, firstKey, secondKey, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingSnapshotComparison>> CompareSnapshotsAsync(
        string sessionKey,
        string nodeKey,
        string snapshotAKey,
        string snapshotBKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Delegates deterministic analysis without persisting its result.</summary>
    /// <param name="request">The public-key evaluation request.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The computed, unpersisted evaluation result.</returns>
    /// <example><code>var analysis = await queries.EvaluateAsync(request, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingEvaluationResult>> EvaluateAsync(
        RuntimeProfilingEvaluationRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Represents one automatically closing measured profiling scope.</summary>
/// <example><code>await using var scope = await measurements.BeginAsync("load", cancellationToken);</code></example>
public interface IRuntimeProfilingMeasurementScope : IAsyncDisposable
{
    /// <summary>Gets the public session key.</summary>
    string SessionKey { get; }

    /// <summary>Marks the raw scope as failed without storing a stack trace.</summary>
    void MarkFailed(Exception exception);

    /// <summary>Marks the raw scope as cancelled.</summary>
    void MarkCancelled();
}

/// <summary>Creates raw scopes and execution helpers over the shared profiling lifecycle.</summary>
/// <example><code>await measurements.MeasureAsync("load", action, cancellationToken);</code></example>
public interface IRuntimeProfilingMeasurementService
{
    /// <summary>Begins a new owning session or a segment in the active session.</summary>
    Task<IResult<IRuntimeProfilingMeasurementScope>> BeginAsync(
        string name,
        CancellationToken cancellationToken = default
    );

    /// <summary>Measures one asynchronous operation and records its outcome.</summary>
    Task<IResult> MeasureAsync(
        string name,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Starts a configurable, bounded host-local workload used to exercise Profiling.</summary>
/// <example><code>var result = stress.TryStart(RuntimeProfilingStressRequest.Default, applicationStopping);</code></example>
public interface IRuntimeProfilingStressService
{
    /// <summary>Gets whether the process-local stress workload is currently running.</summary>
    /// <example><code>var running = stress.IsRunning;</code></example>
    bool IsRunning { get; }

    /// <summary>Starts one configured workload when another run is not already active.</summary>
    /// <param name="request">The duration, CPU-worker, and retained-memory configuration.</param>
    /// <param name="cancellationToken">Stops the workload when the host shuts down.</param>
    /// <returns>The requested workload shape and whether this call started it.</returns>
    /// <example><code>var result = stress.TryStart(RuntimeProfilingStressRequest.Default, applicationStopping);</code></example>
    RuntimeProfilingStressResult TryStart(
        RuntimeProfilingStressRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Computes deterministic, unpersisted single-node profiling analysis.</summary>
/// <example><code>var result = await evaluator.EvaluateAsync(request, cancellationToken);</code></example>
public interface IRuntimeProfilingEvaluationService
{
    /// <summary>Evaluates either two snapshots or the complete available node timeline.</summary>
    Task<IResult<RuntimeProfilingEvaluationResult>> EvaluateAsync(
        RuntimeProfilingEvaluationRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Exports and imports portable Profiling session archives through caller-owned streams.</summary>
/// <example><code>var imported = await archives.ImportAsync(stream, cancellationToken);</code></example>
public interface IRuntimeProfilingArchiveService
{
    /// <summary>Exports one complete terminal session archive.</summary>
    /// <param name="sessionKey">The source session key.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A successful result only after the complete JSON document was written.</returns>
    /// <example><code>await archives.ExportSessionAsync(sessionKey, stream, cancellationToken);</code></example>
    Task<IResult> ExportSessionAsync(
        string sessionKey,
        Stream destination,
        CancellationToken cancellationToken = default
    );

    /// <summary>Exports one immutable snapshot with its minimum source context.</summary>
    /// <param name="sessionKey">The source session key.</param>
    /// <param name="nodeKey">The source node key.</param>
    /// <param name="snapshotKey">The source snapshot key.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A successful result only after the complete JSON document was written.</returns>
    /// <example><code>await archives.ExportSnapshotAsync(sessionKey, nodeKey, snapshotKey, stream, cancellationToken);</code></example>
    Task<IResult> ExportSnapshotAsync(
        string sessionKey,
        string nodeKey,
        string snapshotKey,
        Stream destination,
        CancellationToken cancellationToken = default
    );

    /// <summary>Validates and atomically imports one supported archive.</summary>
    /// <param name="source">The readable JSON archive stream.</param>
    /// <param name="cancellationToken">Cancels validation or import.</param>
    /// <returns>The fresh readable identities created for the imported session graph.</returns>
    /// <example><code>var imported = await archives.ImportAsync(stream, cancellationToken);</code></example>
    Task<IResult<RuntimeProfilingArchiveImportResult>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Exports complete Profiling sessions as Perfetto-compatible Trace Event JSON.</summary>
/// <example><code>await perfetto.ExportSessionAsync(sessionKey, destination, cancellationToken);</code></example>
public interface IRuntimeProfilingPerfettoExportService
{
    /// <summary>Exports one complete terminal session for visual investigation in Perfetto.</summary>
    /// <param name="sessionKey">The public source-session key.</param>
    /// <param name="destination">The caller-owned writable destination stream.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A successful result only after the complete trace document was written.</returns>
    /// <example><code>await perfetto.ExportSessionAsync(sessionKey, destination, cancellationToken);</code></example>
    Task<IResult> ExportSessionAsync(
        string sessionKey,
        Stream destination,
        CancellationToken cancellationToken = default
    );
}
