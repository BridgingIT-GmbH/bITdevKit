// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Persists complete Runtime and Operation graphs, writer ownership, query boundaries, and clear fences.</summary>
/// <example><code>services.AddProfiling().WithLiteDbProvider();</code></example>
public sealed partial class LiteDbProfilingStorageProvider : IProfilingStorageProvider, IOperationProfilingStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateStorageJsonOptions();
    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly LiteDbFeatureDatabase database;
    private readonly LiteDbKeyedCollection<string, NativeState> control;
    private readonly LiteDbKeyedCollection<string, NativeRow> rows;
    private readonly ProfilingOptions options;
    private readonly TimeProvider clock;

    /// <summary>Creates the provider without opening or initializing the native file.</summary>
    /// <param name="database">The owned native namespace.</param>
    /// <param name="scope">A stable safe identity derived from the registered database file and namespace.</param>
    /// <param name="options">The existing profiling policies and bounds.</param>
    /// <param name="clock">The lease, query, and maintenance clock.</param>
    public LiteDbProfilingStorageProvider(LiteDbFeatureDatabase database, string scope, ProfilingOptions options = null, TimeProvider clock = null)
    {
        this.database = database;
        var selected = options ?? new();
        selected.Validate();
        this.options = new()
        {
            Operations = selected.Operations.Snapshot(),
            Storage = JsonSerializer.Deserialize<ProfilingStorageOptions>(JsonSerializer.Serialize(selected.Storage)),
            Queries = JsonSerializer.Deserialize<ProfilingQueryOptions>(JsonSerializer.Serialize(selected.Queries))
        };
        this.clock = clock ?? TimeProvider.System;
        this.control = database.Collection<string, NativeState>("state");
        this.rows = database.Collection<string, NativeRow>("records");
        var capabilities = new Frame(new NativeState(), scope, this.options, this.clock).Capabilities;
        this.Capabilities = capabilities;
        this.Runtime = new RuntimeStore(this);
    }

    /// <inheritdoc />
    public ProfilingProviderCapabilities Capabilities { get; }

    /// <inheritdoc />
    public IRuntimeProfilingStore Runtime { get; }

    /// <inheritdoc />
    public IOperationProfilingStore Operations => this;

    /// <inheritdoc />
    public async Task<IResult<ProfilingClearResult>> ClearAsync(ProfilingClearRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await this.RunAsync(frame => Task.FromResult<IResult<Guid>>(frame.PrepareClear(request)), cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure) return Result<ProfilingClearResult>.Failure(prepared);
        try
        {
            while (true)
            {
                var signal = this.changed.Task;
                var step = await this.RunAsync(frame => Task.FromResult<IResult<ProfilingClearResult>>(frame.AdvanceClear(prepared.Value, cancellationToken)), cancellationToken, false).ConfigureAwait(false);
                if (step.IsFailure || step.Value.State != ProfilingClearState.Preparing) return step;
                try { await signal.WaitAsync(TimeSpan.FromMilliseconds(50), this.clock, cancellationToken).ConfigureAwait(false); }
                catch (TimeoutException) { }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var canceled = await this.RunAsync(frame => Task.FromResult<IResult<ProfilingClearResult>>(frame.AdvanceClear(prepared.Value, default, true)), default).ConfigureAwait(false);
            return Result<ProfilingClearResult>.Failure(canceled.Value).WithError(new ProfilingUnavailableError("Profiling clear was canceled; known progress is retained."));
        }
    }

    /// <inheritdoc />
    public async Task<IResult> CloseWriterAsync(ProfilingWriterLease lease, CancellationToken cancellationToken = default) =>
        await this.RunAsync<bool>(async frame =>
        {
            var result = await frame.CloseWriterAsync(lease, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure(result);
        }, cancellationToken).ConfigureAwait(false);

    private async Task<IResult<T>> RunAsync<T>(Func<Frame, Task<IResult<T>>> action, CancellationToken token, bool notify = true)
    {
        var completed = false;
        try
        {
            var committed = await this.database.ExecuteAsync(() =>
            {
                var state = this.control.GetOrAdd("current", _ => new());
                if (state.Epoch == Guid.Empty || state.Secret?.Length != 32 || state.Watermark < 0 || state.DeletionRevision < 0) throw new InvalidOperationException("Invalid native profiling coordination state.");
                var original = this.rows.ToDictionary(pair => pair.Key, pair => pair.Value);
                var frame = new Frame(state, this.Capabilities.Scope, this.options, this.clock);
                frame.RestoreRows(original.Values);
                var pending = action(frame);
                if (!pending.IsCompleted) throw new InvalidOperationException("Native profiling commands cannot await external operations.");
                var result = pending.GetAwaiter().GetResult();
                var current = frame.CaptureRows().ToDictionary(row => row.Identity, StringComparer.Ordinal);
                foreach (var key in original.Keys.Where(key => !current.ContainsKey(key))) this.rows.Remove(key);
                foreach (var row in current)
                {
                    if (!original.TryGetValue(row.Key, out var prior) || prior.Kind != row.Value.Kind || prior.Value.GetRawText() != row.Value.Value.GetRawText()) this.rows[row.Key] = row.Value;
                }

                this.control["current"] = frame.Capture();
                completed = true;
                return Task.FromResult(result);
            }, token).ConfigureAwait(false);
            if (notify) Interlocked.Exchange(ref this.changed, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            return committed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<T>.Failure(new ProfilingPersistenceError(completed, true)); }
    }

    private static NativeRow Pack<TKey, TValue>(string kind, TKey key, TValue value) => new()
    {
        Kind = kind, Key = JsonSerializer.SerializeToElement(key, JsonOptions), Value = JsonSerializer.SerializeToElement(value, JsonOptions)
    };

    /// <inheritdoc />
    public Task<IResult<ProfilingWriterLease>> OpenWriterAsync(ProfilingOpenWriterRequest request, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.OpenWriterAsync(request, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<ProfilingWriterSynchronizationResult>> SynchronizeWriterAsync(ProfilingWriterSynchronizationRequest request, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.SynchronizeWriterAsync(request, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<ProfilingClearAcknowledgement>> AcknowledgeClearAsync(ProfilingClearAcknowledgement request, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.AcknowledgeClearAsync(request, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<ProfilingBatchWriteResult>> AppendAsync(IReadOnlyList<ProfilingWriteEnvelope> records, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.AppendAsync(records, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.FindAsync(id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<ProfilingMaintenanceResult>> ResumeMaintenanceAsync(ProfilingMaintenanceRequest request, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.ResumeMaintenanceAsync(request, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.QueryAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.GroupAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingAnalysisSelection>> SelectAnalysisAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.SelectAnalysisAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingTimeline>> TimelineAsync(OperationProfilingQuery query, int bucketCount = 72, CancellationToken cancellationToken = default) => this.RunAsync(frame => frame.TimelineAsync(query, bucketCount, cancellationToken), cancellationToken);

    private sealed class RuntimeStore(LiteDbProfilingStorageProvider owner) : IRuntimeProfilingStore, IRuntimeProfilingCorrelationStore
    {
        public ProfilingStoreCapabilities Capabilities { get; } = new(false);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
            RuntimeProfilingSessionCreateRequest request,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.GetOrCreateActiveSessionAsync(request, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSession>> GetActiveSessionAsync(
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.GetActiveSessionAsync(cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSession>> FindSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.FindSessionAsync(sessionKey, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<IReadOnlyList<RuntimeProfilingSession>>> ListSessionsAsync(
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.ListSessionsAsync(cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSession>> UpdateSessionMetadataAsync(
            string sessionKey,
            RuntimeProfilingSessionMetadata metadata,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.UpdateSessionMetadataAsync(sessionKey, metadata, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSession>> TryTransitionSessionAsync(
            Guid sessionId,
            IReadOnlyCollection<RuntimeProfilingSessionState> expectedStates,
            RuntimeProfilingSessionState nextState,
            DateTimeOffset transitionedUtc,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.TryTransitionSessionAsync(sessionId, expectedStates, nextState, transitionedUtc, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<ProfilingNode>> FindNodeAsync(RuntimeProfilingNodeCorrelation correlation, CancellationToken cancellationToken = default) => owner.RunAsync(frame => frame.Runtime.FindNodeAsync(correlation, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<ProfilingNode>> GetOrCreateNodeAsync(
            RuntimeProfilingNodeCorrelation correlation,
            ProfilingNode proposedNode,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.GetOrCreateNodeAsync(correlation, proposedNode, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingNodeParticipation>> UpsertParticipationAsync(
            RuntimeProfilingNodeParticipation participation,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.UpsertParticipationAsync(participation, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingContext>> AddRuntimeContextAsync(
            RuntimeProfilingContext context,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.AddRuntimeContextAsync(context, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSnapshot>> AddSnapshotAsync(
            RuntimeProfilingSnapshot snapshot,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.AddSnapshotAsync(snapshot, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<ProfilingMarker>> AddMarkerAsync(
            ProfilingMarker marker,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.AddMarkerAsync(marker, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<ProfilingSegment>> UpsertSegmentAsync(
            ProfilingSegment segment,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.UpsertSegmentAsync(segment, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<ProfilingMetricObservation>> AddMetricObservationAsync(
            ProfilingMetricObservation observation,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.AddMetricObservationAsync(observation, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSessionData>> GetSessionDataAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.GetSessionDataAsync(sessionKey, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingSession>> ImportSessionAsync(
            RuntimeProfilingSessionData data,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.ImportSessionAsync(data, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<bool>> DeleteSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.DeleteSessionAsync(sessionKey, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<int>> DeleteUnpinnedSessionsAsync(
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.DeleteUnpinnedSessionsAsync(cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<ProfilingClearResult>> ClearAsync(
            CancellationToken cancellationToken = default
        ) => owner.ClearAsync(new ProfilingClearRequest { DataSet = ProfilingDataSet.Runtime }, cancellationToken);

        /// <inheritdoc />
        public Task<IResult<int>> ApplyRetentionAsync(
            int maximumRetainedSessions,
            TimeSpan maximumSessionAge,
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) => owner.RunAsync(frame => frame.Runtime.ApplyRetentionAsync(maximumRetainedSessions, maximumSessionAge, utcNow, cancellationToken), cancellationToken);

        /// <inheritdoc />
        public Task<IResult<RuntimeProfilingCorrelationSelection>> QueryCorrelationAsync(RuntimeProfilingCorrelationRequest request, CancellationToken cancellationToken = default) => owner.RunAsync(frame => ((IRuntimeProfilingCorrelationStore)frame.Runtime).QueryCorrelationAsync(request, cancellationToken), cancellationToken);
    }
}
