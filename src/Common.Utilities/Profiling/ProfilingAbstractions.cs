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
public interface IProfilingStore
{
    /// <summary>Gets provider capabilities.</summary>
    ProfilingStoreCapabilities Capabilities { get; }

    /// <summary>Atomically creates a session or returns the existing active session.</summary>
    Task<Result<ProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
        ProfilingSessionCreateRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets the active logical session when one exists.</summary>
    Task<Result<ProfilingSession>> GetActiveSessionAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Finds a session by public readable key.</summary>
    Task<Result<ProfilingSession>> FindSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lists stored sessions in reverse chronological order.</summary>
    Task<Result<IReadOnlyList<ProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Updates editable descriptive session metadata.</summary>
    Task<Result<ProfilingSession>> UpdateSessionMetadataAsync(
        string sessionKey,
        ProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>Transitions a session when its current state matches an expected state.</summary>
    Task<Result<ProfilingSession>> TryTransitionSessionAsync(
        Guid sessionId,
        IReadOnlyCollection<ProfilingSessionState> expectedStates,
        ProfilingSessionState nextState,
        DateTimeOffset transitionedUtc,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets or creates the stable profiling node for one Broadcast process registration.</summary>
    Task<Result<ProfilingNode>> GetOrCreateNodeAsync(
        ProfilingNodeCorrelation correlation,
        ProfilingNode proposedNode,
        CancellationToken cancellationToken = default
    );

    /// <summary>Adds or updates a node's session participation state and cumulative totals.</summary>
    Task<Result<ProfilingNodeParticipation>> UpsertParticipationAsync(
        ProfilingNodeParticipation participation,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stores immutable node runtime context once per session and node.</summary>
    Task<Result<ProfilingRuntimeContext>> AddRuntimeContextAsync(
        ProfilingRuntimeContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>Appends one immutable runtime snapshot.</summary>
    Task<Result<ProfilingSnapshot>> AddSnapshotAsync(
        ProfilingSnapshot snapshot,
        CancellationToken cancellationToken = default
    );

    /// <summary>Atomically adds a shared marker only while the session remains active.</summary>
    Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
        ProfilingPhaseMarker marker,
        CancellationToken cancellationToken = default
    );

    /// <summary>Appends one immutable node-local action marker.</summary>
    Task<Result<ProfilingActionMarker>> AddActionMarkerAsync(
        ProfilingActionMarker marker,
        CancellationToken cancellationToken = default
    );

    /// <summary>Adds or closes a node-owned segment.</summary>
    Task<Result<ProfilingSegment>> UpsertSegmentAsync(
        ProfilingSegment segment,
        CancellationToken cancellationToken = default
    );

    /// <summary>Appends one immutable custom metric observation.</summary>
    Task<Result<ProfilingMetricObservation>> AddMetricObservationAsync(
        ProfilingMetricObservation observation,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads all records for one session.</summary>
    Task<Result<ProfilingSessionData>> GetSessionDataAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Atomically inserts one complete, already-remapped terminal session graph.</summary>
    /// <param name="data">The complete terminal session graph to insert.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>The inserted session or a typed validation or provider failure.</returns>
    /// <example><code>var imported = await store.ImportSessionAsync(data, cancellationToken);</code></example>
    Task<Result<ProfilingSession>> ImportSessionAsync(
        ProfilingSessionData data,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            Result<ProfilingSession>
                .Failure()
                .WithError(
                    new ProfilingUnavailableError(
                        "The configured profiling store does not support archive import."
                    )
                )
        );

    /// <summary>Deletes one terminal session and all associated records.</summary>
    Task<Result<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes all unpinned terminal sessions.</summary>
    Task<Result<int>> DeleteUnpinnedSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically clears the complete store only when no session is active.</summary>
    Task<Result<ProfilingClearResult>> ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies configured age and count retention to unpinned terminal sessions.</summary>
    Task<Result<int>> ApplyRetentionAsync(
        int maximumRetainedSessions,
        TimeSpan maximumSessionAge,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Provides the stable process-lifetime profiling node identity.</summary>
/// <example><code>var node = await provider.GetAsync(registration, cancellationToken);</code></example>
public interface IProfilingNodeIdentityProvider
{
    /// <summary>Resolves one Broadcast registration to a stable profiling node.</summary>
    Task<Result<ProfilingNode>> GetAsync(
        BroadcastNodeRegistration registration,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Captures one provider-neutral runtime snapshot without scheduling or persistence.</summary>
/// <example><code>var snapshot = await probe.CaptureAsync(request, cancellationToken);</code></example>
public interface IProfilingSnapshotProbe
{
    /// <summary>Captures one immutable runtime snapshot.</summary>
    Task<Result<ProfilingSnapshot>> CaptureAsync(
        ProfilingCaptureRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Creates immutable non-sensitive context for one session node.</summary>
/// <example><code>var context = factory.Create(session, node);</code></example>
public interface IProfilingRuntimeContextFactory
{
    /// <summary>Creates the context that is stored once per session and node.</summary>
    ProfilingRuntimeContext Create(ProfilingSession session, ProfilingNode node);
}

/// <summary>Owns node-local collection admission and single-flight capture.</summary>
/// <example><code>await collector.StartAsync(session, cancellationToken);</code></example>
public interface IProfilingCollector
{
    /// <summary>Starts or idempotently accepts local collection for a session.</summary>
    Task<Result> StartAsync(
        ProfilingSession session,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stops local collection for the identified session.</summary>
    Task<Result> StopAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Captures one immediate local snapshot for a session.</summary>
    Task<Result<ProfilingSnapshot>> CaptureAsync(
        ProfilingSession session,
        ProfilingNodeRole role,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Provides the one dashboard, console, and programmatic profiling control path.
/// </summary>
/// <example><code>var result = await control.StartAsync(request, cancellationToken);</code></example>
public interface IProfilingControlService
{
    /// <summary>Gets feature availability and active-session status.</summary>
    Task<Result<ProfilingStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts or returns the active deployment-wide session.</summary>
    Task<Result<ProfilingControlResult>> StartAsync(
        ProfilingStartRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stops the active deployment-wide session.</summary>
    Task<Result<ProfilingControlResult>> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Collects one deployment-wide manual snapshot.</summary>
    Task<Result<ProfilingControlResult>> SnapshotAsync(
        string standaloneSessionName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Triggers one normal deployment-wide <see cref="GC.Collect()"/> action.</summary>
    Task<Result<ProfilingControlResult>> CollectGarbageAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Adds one shared marker to the active session.</summary>
    Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
        string name,
        CancellationToken cancellationToken = default
    );

    /// <summary>Restarts a selected session as a new clean session.</summary>
    Task<Result<ProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes one terminal session.</summary>
    Task<Result<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes all unpinned terminal sessions.</summary>
    Task<Result<int>> DeleteUnpinnedSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears all stored profiling data after caller confirmation.</summary>
    Task<Result<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Coordinates Profiling's fixed target snapshots over the standalone Broadcast service.
/// </summary>
/// <example><code>var targets = await broadcasts.PrepareTargetsAsync(scopes, cancellationToken);</code></example>
public interface IProfilingBroadcastService
{
    /// <summary>Prepares the exact active registrations used by one Profiling operation.</summary>
    Task<Result<ProfilingBroadcastTargetSnapshot>> PrepareTargetsAsync(
        IEnumerable<string> targetScopes = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Publishes one Profiling command to an already prepared target set.</summary>
    Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
        TBroadcast payload,
        ProfilingBroadcastTargetSnapshot targetSnapshot,
        BroadcastPublishOptions options = null,
        CancellationToken cancellationToken = default
    )
        where TBroadcast : IProfilingBroadcast;
}

/// <summary>Queries stored profiling data without implementing lifecycle behavior.</summary>
/// <example><code>var data = await queries.GetSessionAsync(sessionKey, cancellationToken);</code></example>
public interface IProfilingQueryService
{
    /// <summary>Lists stored sessions.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Stored sessions in provider order, or a typed availability failure.</returns>
    /// <example><code>var sessions = await queries.ListSessionsAsync(cancellationToken);</code></example>
    Task<Result<IReadOnlyList<ProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads one complete session data set.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The complete provider-neutral session data set.</returns>
    /// <example><code>var data = await queries.GetSessionAsync(sessionKey, cancellationToken);</code></example>
    Task<Result<ProfilingSessionData>> GetSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads the selected node timeline and its related session records.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="nodeKey">The public node key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The selected node read model without deployment aggregation.</returns>
    /// <example><code>var node = await queries.GetNodeSessionAsync(sessionKey, nodeKey, cancellationToken);</code></example>
    Task<Result<ProfilingNodeSessionData>> GetNodeSessionAsync(
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
    Task<Result<ProfilingSession>> UpdateMetadataAsync(
        string sessionKey,
        ProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>Restarts a selected session through the shared lifecycle service.</summary>
    /// <param name="sessionKey">The public source-session key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shared lifecycle result for the replacement session.</returns>
    /// <example><code>var restarted = await queries.RestartAsync(sessionKey, cancellationToken);</code></example>
    Task<Result<ProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes one selected terminal session through the shared lifecycle service.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the selected terminal session was deleted.</returns>
    /// <example><code>var deleted = await queries.DeleteSessionAsync(sessionKey, cancellationToken);</code></example>
    Task<Result<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes every unpinned terminal session through the shared lifecycle service.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of deleted unpinned terminal sessions.</returns>
    /// <example><code>var count = await queries.DeleteUnpinnedSessionsAsync(cancellationToken);</code></example>
    Task<Result<int>> DeleteUnpinnedSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears all profiling records through the confirmed shared lifecycle service.</summary>
    /// <param name="confirmed">Whether the caller explicitly confirmed the destructive reset.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The committed clear result.</returns>
    /// <example><code>var cleared = await queries.ClearAsync(true, cancellationToken);</code></example>
    Task<Result<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    );

    /// <summary>Serializes only normal immutable snapshots as raw JSON.</summary>
    /// <param name="sessionKey">The public session key.</param>
    /// <param name="nodeKey">An optional public node key; omit it for the complete session.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>A JSON array containing only normal runtime snapshots.</returns>
    /// <example><code>var json = await queries.ExportSnapshotsJsonAsync(sessionKey, nodeKey, cancellationToken);</code></example>
    Task<Result<string>> ExportSnapshotsJsonAsync(
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
    Task<Result<ProfilingSnapshotComparison>> CompareSnapshotsAsync(
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
    Task<Result<ProfilingEvaluationResult>> EvaluateAsync(
        ProfilingEvaluationRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Represents one automatically closing measured profiling scope.</summary>
/// <example><code>await using var scope = await measurements.BeginAsync("load", cancellationToken);</code></example>
public interface IProfilingMeasurementScope : IAsyncDisposable
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
public interface IProfilingMeasurementService
{
    /// <summary>Begins a new owning session or a segment in the active session.</summary>
    Task<Result<IProfilingMeasurementScope>> BeginAsync(
        string name,
        CancellationToken cancellationToken = default
    );

    /// <summary>Measures one asynchronous operation and records its outcome.</summary>
    Task<Result> MeasureAsync(
        string name,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Starts a configurable, bounded host-local workload used to exercise Profiling.</summary>
/// <example><code>var result = stress.TryStart(ProfilingStressRequest.Default, applicationStopping);</code></example>
public interface IProfilingStressService
{
    /// <summary>Gets whether the process-local stress workload is currently running.</summary>
    /// <example><code>var running = stress.IsRunning;</code></example>
    bool IsRunning { get; }

    /// <summary>Starts one configured workload when another run is not already active.</summary>
    /// <param name="request">The duration, CPU-worker, and retained-memory configuration.</param>
    /// <param name="cancellationToken">Stops the workload when the host shuts down.</param>
    /// <returns>The requested workload shape and whether this call started it.</returns>
    /// <example><code>var result = stress.TryStart(ProfilingStressRequest.Default, applicationStopping);</code></example>
    ProfilingStressResult TryStart(
        ProfilingStressRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Computes deterministic, unpersisted single-node profiling analysis.</summary>
/// <example><code>var result = await evaluator.EvaluateAsync(request, cancellationToken);</code></example>
public interface IProfilingEvaluationService
{
    /// <summary>Evaluates either two snapshots or the complete available node timeline.</summary>
    Task<Result<ProfilingEvaluationResult>> EvaluateAsync(
        ProfilingEvaluationRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Exports and imports portable Profiling session archives through caller-owned streams.</summary>
/// <example><code>var imported = await archives.ImportAsync(stream, cancellationToken);</code></example>
public interface IProfilingArchiveService
{
    /// <summary>Exports one complete terminal session archive.</summary>
    /// <param name="sessionKey">The source session key.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A successful result only after the complete JSON document was written.</returns>
    /// <example><code>await archives.ExportSessionAsync(sessionKey, stream, cancellationToken);</code></example>
    Task<Result> ExportSessionAsync(
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
    Task<Result> ExportSnapshotAsync(
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
    Task<Result<ProfilingArchiveImportResult>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Exports complete Profiling sessions as Perfetto-compatible Trace Event JSON.</summary>
/// <example><code>await perfetto.ExportSessionAsync(sessionKey, destination, cancellationToken);</code></example>
public interface IProfilingPerfettoExportService
{
    /// <summary>Exports one complete terminal session for visual investigation in Perfetto.</summary>
    /// <param name="sessionKey">The public source-session key.</param>
    /// <param name="destination">The caller-owned writable destination stream.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A successful result only after the complete trace document was written.</returns>
    /// <example><code>await perfetto.ExportSessionAsync(sessionKey, destination, cancellationToken);</code></example>
    Task<Result> ExportSessionAsync(
        string sessionKey,
        Stream destination,
        CancellationToken cancellationToken = default
    );
}