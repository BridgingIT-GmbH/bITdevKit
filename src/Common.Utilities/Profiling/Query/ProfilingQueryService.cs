// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Text.Json;

/// <summary>
/// Provides the provider-neutral profiling read, metadata, raw export, and comparison surface.
/// </summary>
/// <remarks>
/// Public readable keys are resolved against one loaded session data set. Lifecycle mutations and
/// evaluation are delegated to their existing services.
/// </remarks>
/// <param name="options">The shared profiling feature configuration.</param>
/// <param name="store">The optional configured profiling store.</param>
/// <param name="controlService">The optional shared lifecycle service.</param>
/// <param name="evaluationService">The optional deterministic evaluation service.</param>
/// <example><code>var data = await queries.GetNodeSessionAsync(sessionKey, nodeKey, cancellationToken);</code></example>
public sealed class ProfilingQueryService(
    ProfilingOptions options,
    IProfilingStore store = null,
    IProfilingControlService controlService = null,
    IProfilingEvaluationService evaluationService = null
) : IProfilingQueryService
{
    private static readonly JsonSerializerOptions ExportSerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        WriteIndented = true,
    };

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetStoreOperationalError();
        return operationalError is null
            ? store.ListSessionsAsync(cancellationToken)
            : Task.FromResult(Failure<IReadOnlyList<ProfilingSession>>(operationalError));
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSessionData>> GetSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetStoreOperationalError();
        if (operationalError is not null)
        {
            return Task.FromResult(Failure<ProfilingSessionData>(operationalError));
        }

        var keyError = ValidateKey("session", sessionKey);
        return keyError is null
            ? store.GetSessionDataAsync(sessionKey, cancellationToken)
            : Task.FromResult(Failure<ProfilingSessionData>(keyError));
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingNodeSessionData>> GetNodeSessionAsync(
        string sessionKey,
        string nodeKey,
        CancellationToken cancellationToken = default
    )
    {
        var selectionResult = await this.LoadNodeSelectionAsync(
                sessionKey,
                nodeKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (selectionResult.IsFailure)
        {
            return CopyFailure<ProfilingNodeSessionData, NodeSelection>(selectionResult);
        }

        var selection = selectionResult.Value;
        var latestSnapshot = selection.Snapshots.LastOrDefault();
        var participation = selection.Participation;
        var successfulCaptureCount = Math.Max(
            participation?.SuccessfulCaptureCount ?? 0,
            latestSnapshot?.Sequence ?? 0
        );
        var skippedCaptureCount = Math.Max(
            participation?.SkippedCaptureCount ?? 0,
            latestSnapshot?.SkippedCaptureCount ?? 0
        );
        var failedCaptureCount = Math.Max(
            participation?.FailedCaptureCount ?? 0,
            latestSnapshot?.FailedCaptureCount ?? 0
        );

        return Result<ProfilingNodeSessionData>.Success(
            new()
            {
                Session = selection.Data.Session,
                NodeKey = nodeKey,
                Node = selection.Node,
                Participation = participation,
                RuntimeContext = selection.RuntimeContext,
                LatestSnapshot = latestSnapshot,
                Snapshots = selection.Snapshots,
                PhaseMarkers = selection.Data.PhaseMarkers,
                ActionMarkers = selection
                    .Data.ActionMarkers.Where(x => x.NodeId == selection.NodeId)
                    .OrderBy(x => x.TimestampUtc)
                    .ToArray(),
                Segments = selection
                    .Data.Segments.Where(x => x.NodeId == selection.NodeId)
                    .OrderBy(x => x.StartedUtc)
                    .ToArray(),
                MetricObservations = selection
                    .Data.MetricObservations.Where(x => x.NodeId == selection.NodeId)
                    .OrderBy(x => x.TimestampUtc)
                    .ToArray(),
                SamplingStatus = new(
                    successfulCaptureCount,
                    skippedCaptureCount,
                    failedCaptureCount,
                    latestSnapshot?.CaptureDuration,
                    latestSnapshot is null
                        ? null
                        : latestSnapshot.CaptureStartedElapsed - latestSnapshot.ScheduledElapsed
                ),
            }
        );
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> UpdateMetadataAsync(
        string sessionKey,
        ProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetStoreOperationalError();
        if (operationalError is not null)
        {
            return Task.FromResult(Failure<ProfilingSession>(operationalError));
        }

        var keyError = ValidateKey("session", sessionKey);
        return keyError is null
            ? store.UpdateSessionMetadataAsync(sessionKey, metadata, cancellationToken)
            : Task.FromResult(Failure<ProfilingSession>(keyError));
    }

    /// <inheritdoc />
    public Task<Result<ProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    ) =>
        controlService is null
            ? Task.FromResult(
                Failure<ProfilingControlResult>(
                    new ProfilingUnavailableError("No profiling control service is registered.")
                )
            )
            : controlService.RestartAsync(sessionKey, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    ) =>
        controlService is null
            ? Task.FromResult(
                Failure<bool>(
                    new ProfilingUnavailableError("No profiling control service is registered.")
                )
            )
            : controlService.DeleteSessionAsync(sessionKey, cancellationToken);

    /// <inheritdoc />
    public Task<Result<int>> DeleteUnpinnedSessionsAsync(
        CancellationToken cancellationToken = default
    ) =>
        controlService is null
            ? Task.FromResult(
                Failure<int>(
                    new ProfilingUnavailableError("No profiling control service is registered.")
                )
            )
            : controlService.DeleteUnpinnedSessionsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    ) =>
        controlService is null
            ? Task.FromResult(
                Failure<ProfilingClearResult>(
                    new ProfilingUnavailableError("No profiling control service is registered.")
                )
            )
            : controlService.ClearAsync(confirmed, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<string>> ExportSnapshotsJsonAsync(
        string sessionKey,
        string nodeKey = null,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<ProfilingSnapshot> snapshots;
        if (string.IsNullOrWhiteSpace(nodeKey))
        {
            var dataResult = await this.GetSessionAsync(sessionKey, cancellationToken)
                .ConfigureAwait(false);
            if (dataResult.IsFailure)
            {
                return CopyFailure<string, ProfilingSessionData>(dataResult);
            }

            snapshots = dataResult
                .Value.Snapshots.OrderBy(x => x.TimestampUtc)
                .ThenBy(x => x.NodeKey, StringComparer.Ordinal)
                .ThenBy(x => x.Sequence)
                .ToArray();
        }
        else
        {
            var nodeResult = await this.GetNodeSessionAsync(sessionKey, nodeKey, cancellationToken)
                .ConfigureAwait(false);
            if (nodeResult.IsFailure)
            {
                return CopyFailure<string, ProfilingNodeSessionData>(nodeResult);
            }

            snapshots = nodeResult.Value.Snapshots;
        }

        return Result<string>.Success(JsonSerializer.Serialize(snapshots, ExportSerializerOptions));
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingSnapshotComparison>> CompareSnapshotsAsync(
        string sessionKey,
        string nodeKey,
        string snapshotAKey,
        string snapshotBKey,
        CancellationToken cancellationToken = default
    )
    {
        var snapshotAError = ValidateKey("snapshot", snapshotAKey);
        if (snapshotAError is not null)
        {
            return Failure<ProfilingSnapshotComparison>(snapshotAError);
        }

        var snapshotBError = ValidateKey("snapshot", snapshotBKey);
        if (snapshotBError is not null)
        {
            return Failure<ProfilingSnapshotComparison>(snapshotBError);
        }

        if (string.Equals(snapshotAKey, snapshotBKey, StringComparison.Ordinal))
        {
            return Failure<ProfilingSnapshotComparison>(
                new ProfilingValidationError("Two distinct profiling snapshots are required.")
            );
        }

        var selectionResult = await this.LoadNodeSelectionAsync(
                sessionKey,
                nodeKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (selectionResult.IsFailure)
        {
            return CopyFailure<ProfilingSnapshotComparison, NodeSelection>(selectionResult);
        }

        var snapshotA = selectionResult.Value.Snapshots.SingleOrDefault(x =>
            x.Identity.Key == snapshotAKey
        );
        var snapshotB = selectionResult.Value.Snapshots.SingleOrDefault(x =>
            x.Identity.Key == snapshotBKey
        );
        if (snapshotA is null || snapshotB is null)
        {
            return Failure<ProfilingSnapshotComparison>(
                new NotFoundError(
                    $"One or both selected snapshots were not found for node '{nodeKey}' in session '{sessionKey}'."
                )
            );
        }

        if (
            snapshotA.Sequence >= snapshotB.Sequence
            || snapshotA.CaptureStartedElapsed >= snapshotB.CaptureStartedElapsed
        )
        {
            return Failure<ProfilingSnapshotComparison>(
                new ProfilingValidationError(
                    "Snapshot A must precede snapshot B in node-local sequence and monotonic time."
                )
            );
        }

        return Result<ProfilingSnapshotComparison>.Success(
            new(
                sessionKey,
                nodeKey,
                snapshotAKey,
                snapshotBKey,
                CreateMetricDeltas(snapshotA, snapshotB)
            )
        );
    }

    /// <inheritdoc />
    public Task<Result<ProfilingEvaluationResult>> EvaluateAsync(
        ProfilingEvaluationRequest request,
        CancellationToken cancellationToken = default
    ) =>
        evaluationService is null
            ? Task.FromResult(
                Failure<ProfilingEvaluationResult>(
                    new ProfilingUnavailableError("No profiling evaluation service is registered.")
                )
            )
            : evaluationService.EvaluateAsync(request, cancellationToken);

    private async Task<Result<NodeSelection>> LoadNodeSelectionAsync(
        string sessionKey,
        string nodeKey,
        CancellationToken cancellationToken
    )
    {
        var nodeKeyError = ValidateKey("node", nodeKey);
        if (nodeKeyError is not null)
        {
            return Failure<NodeSelection>(nodeKeyError);
        }

        var dataResult = await this.GetSessionAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (dataResult.IsFailure)
        {
            return CopyFailure<NodeSelection, ProfilingSessionData>(dataResult);
        }

        var data = dataResult.Value;
        var node = data.Nodes.SingleOrDefault(x => x.Identity.Key == nodeKey);
        var participation = data.Participations.SingleOrDefault(x => x.NodeKey == nodeKey);
        var runtimeContext = data.RuntimeContexts.SingleOrDefault(x => x.NodeKey == nodeKey);
        var firstSnapshot = data.Snapshots.FirstOrDefault(x => x.NodeKey == nodeKey);
        var nodeId =
            node?.Identity.Id
            ?? participation?.NodeId
            ?? runtimeContext?.NodeId
            ?? firstSnapshot?.NodeId;
        if (nodeId is null)
        {
            return Failure<NodeSelection>(
                new NotFoundError(
                    $"Profiling node '{nodeKey}' was not found in session '{sessionKey}'."
                )
            );
        }

        var snapshots = data
            .Snapshots.Where(x => x.NodeId == nodeId.Value)
            .OrderBy(x => x.Sequence)
            .ThenBy(x => x.CaptureStartedElapsed)
            .ThenBy(x => x.Identity.Key, StringComparer.Ordinal)
            .ToArray();

        return Result<NodeSelection>.Success(
            new(data, nodeId.Value, node, participation, runtimeContext, snapshots)
        );
    }

    private IResultError GetStoreOperationalError() =>
        !options.Enabled ? new ProfilingDisabledError()
        : store is null ? new ProfilingUnavailableError("No profiling store is registered.")
        : null;

    private static IResultError ValidateKey(string kind, string value) =>
        IsReadableKey(value) ? null : new ProfilingInvalidKeyError(kind);

    private static bool IsReadableKey(string value) =>
        value?.Length == 8
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private static IReadOnlyList<ProfilingSnapshotMetricDelta> CreateMetricDeltas(
        ProfilingSnapshot earlier,
        ProfilingSnapshot later
    ) =>
        [
            Delta("sequence", "count", earlier.Sequence, later.Sequence),
            Delta(
                "scheduled-elapsed",
                "ms",
                earlier.ScheduledElapsed.TotalMilliseconds,
                later.ScheduledElapsed.TotalMilliseconds
            ),
            Delta(
                "capture-started-elapsed",
                "ms",
                earlier.CaptureStartedElapsed.TotalMilliseconds,
                later.CaptureStartedElapsed.TotalMilliseconds
            ),
            Delta(
                "capture-duration",
                "ms",
                earlier.CaptureDuration.TotalMilliseconds,
                later.CaptureDuration.TotalMilliseconds
            ),
            Delta(
                "sampling-delay",
                "ms",
                (earlier.CaptureStartedElapsed - earlier.ScheduledElapsed).TotalMilliseconds,
                (later.CaptureStartedElapsed - later.ScheduledElapsed).TotalMilliseconds
            ),
            Delta(
                "skipped-capture-count",
                "count",
                earlier.SkippedCaptureCount,
                later.SkippedCaptureCount
            ),
            Delta(
                "failed-capture-count",
                "count",
                earlier.FailedCaptureCount,
                later.FailedCaptureCount
            ),
            Delta("cpu-usage", "%", earlier.CpuUsagePercent, later.CpuUsagePercent),
            Delta(
                "process-cpu-duration",
                "ms",
                earlier.ProcessCpuDuration?.TotalMilliseconds,
                later.ProcessCpuDuration?.TotalMilliseconds
            ),
            Delta(
                "logical-processor-count",
                "count",
                earlier.LogicalProcessorCount,
                later.LogicalProcessorCount
            ),
            Delta("working-set", "bytes", earlier.WorkingSetBytes, later.WorkingSetBytes),
            Delta("private-memory", "bytes", earlier.PrivateMemoryBytes, later.PrivateMemoryBytes),
            Delta("managed-memory", "bytes", earlier.ManagedMemoryBytes, later.ManagedMemoryBytes),
            Delta(
                "total-physical-memory",
                "bytes",
                earlier.TotalPhysicalMemoryBytes,
                later.TotalPhysicalMemoryBytes
            ),
            Delta(
                "available-physical-memory",
                "bytes",
                earlier.AvailablePhysicalMemoryBytes,
                later.AvailablePhysicalMemoryBytes
            ),
            Delta(
                "used-physical-memory",
                "bytes",
                earlier.UsedPhysicalMemoryBytes,
                later.UsedPhysicalMemoryBytes
            ),
            Delta(
                "managed-heap-size",
                "bytes",
                earlier.ManagedHeapSizeBytes,
                later.ManagedHeapSizeBytes
            ),
            Delta("fragmented", "bytes", earlier.FragmentedBytes, later.FragmentedBytes),
            Delta(
                "heap-fragmentation",
                "%",
                earlier.HeapFragmentationPercent,
                later.HeapFragmentationPercent
            ),
            Delta("memory-load", "bytes", earlier.MemoryLoadBytes, later.MemoryLoadBytes),
            Delta(
                "total-available-memory",
                "bytes",
                earlier.TotalAvailableMemoryBytes,
                later.TotalAvailableMemoryBytes
            ),
            Delta(
                "high-memory-load-threshold",
                "bytes",
                earlier.HighMemoryLoadThresholdBytes,
                later.HighMemoryLoadThresholdBytes
            ),
            Delta(
                "total-committed",
                "bytes",
                earlier.TotalCommittedBytes,
                later.TotalCommittedBytes
            ),
            Delta(
                "total-allocated",
                "bytes",
                earlier.TotalAllocatedBytes,
                later.TotalAllocatedBytes
            ),
            Delta(
                "allocation-rate",
                "bytes/s",
                earlier.AllocationRateBytesPerSecond,
                later.AllocationRateBytesPerSecond
            ),
            Delta(
                "memory-pressure",
                "%",
                earlier.MemoryPressurePercent,
                later.MemoryPressurePercent
            ),
            Delta(
                "gen0-collection-count",
                "count",
                earlier.Gen0CollectionCount,
                later.Gen0CollectionCount
            ),
            Delta(
                "gen1-collection-count",
                "count",
                earlier.Gen1CollectionCount,
                later.Gen1CollectionCount
            ),
            Delta(
                "gen2-collection-count",
                "count",
                earlier.Gen2CollectionCount,
                later.Gen2CollectionCount
            ),
            Delta("latest-gc-index", "count", earlier.LatestGcIndex, later.LatestGcIndex),
            Delta(
                "latest-gc-generation",
                "generation",
                earlier.LatestGcGeneration,
                later.LatestGcGeneration
            ),
            Delta(
                "latest-gc-managed-heap",
                "bytes",
                earlier.LatestGcManagedHeapBytes,
                later.LatestGcManagedHeapBytes
            ),
            Delta(
                "latest-gc-loh",
                "bytes",
                earlier.LatestGcLargeObjectHeapBytes,
                later.LatestGcLargeObjectHeapBytes
            ),
            Delta(
                "latest-gen2-gc-index",
                "count",
                earlier.LatestGen2GcIndex,
                later.LatestGen2GcIndex
            ),
            Delta(
                "latest-gen2-managed-heap",
                "bytes",
                earlier.LatestGen2ManagedHeapBytes,
                later.LatestGen2ManagedHeapBytes
            ),
            Delta(
                "latest-gen2-loh",
                "bytes",
                earlier.LatestGen2LargeObjectHeapBytes,
                later.LatestGen2LargeObjectHeapBytes
            ),
            Delta(
                "cumulative-gc-pause",
                "ms",
                earlier.CumulativeGcPauseDuration?.TotalMilliseconds,
                later.CumulativeGcPauseDuration?.TotalMilliseconds
            ),
            Delta("gc-pause", "%", earlier.GcPausePercent, later.GcPausePercent),
            Delta(
                "pinned-object-count",
                "count",
                earlier.PinnedObjectCount,
                later.PinnedObjectCount
            ),
            Delta(
                "finalization-pending-count",
                "count",
                earlier.FinalizationPendingCount,
                later.FinalizationPendingCount
            ),
            Delta("loh-size", "bytes", earlier.LargeObjectHeapBytes, later.LargeObjectHeapBytes),
            Delta(
                "loh-fragmented",
                "bytes",
                earlier.LargeObjectHeapFragmentedBytes,
                later.LargeObjectHeapFragmentedBytes
            ),
            Delta(
                "loh-fragmentation",
                "%",
                earlier.LargeObjectHeapFragmentationPercent,
                later.LargeObjectHeapFragmentationPercent
            ),
            Delta(
                "process-handle-count",
                "count",
                earlier.ProcessHandleCount,
                later.ProcessHandleCount
            ),
            Delta(
                "process-thread-count",
                "count",
                earlier.ProcessThreadCount,
                later.ProcessThreadCount
            ),
            Delta(
                "thread-pool-thread-count",
                "count",
                earlier.ThreadPoolThreadCount,
                later.ThreadPoolThreadCount
            ),
            Delta(
                "thread-pool-completed-work-item-count",
                "count",
                earlier.ThreadPoolCompletedWorkItemCount,
                later.ThreadPoolCompletedWorkItemCount
            ),
            Delta(
                "thread-pool-pending-work-item-count",
                "count",
                earlier.ThreadPoolPendingWorkItemCount,
                later.ThreadPoolPendingWorkItemCount
            ),
            Delta(
                "thread-pool-available-worker-thread-count",
                "count",
                earlier.ThreadPoolAvailableWorkerThreadCount,
                later.ThreadPoolAvailableWorkerThreadCount
            ),
            Delta(
                "thread-pool-available-completion-port-thread-count",
                "count",
                earlier.ThreadPoolAvailableCompletionPortThreadCount,
                later.ThreadPoolAvailableCompletionPortThreadCount
            ),
            Delta(
                "active-tcp-connection-count",
                "count",
                earlier.ActiveTcpConnectionCount,
                later.ActiveTcpConnectionCount
            ),
            Delta("tcp-listener-count", "count", earlier.TcpListenerCount, later.TcpListenerCount),
            Delta("udp-listener-count", "count", earlier.UdpListenerCount, later.UdpListenerCount),
            Delta(
                "total-used-socket-count",
                "count",
                earlier.TotalUsedSocketCount,
                later.TotalUsedSocketCount
            ),
        ];

    private static ProfilingSnapshotMetricDelta Delta(
        string identifier,
        string unit,
        object earlier,
        object later
    )
    {
        var earlierValue = ConvertMetricValue(earlier);
        var laterValue = ConvertMetricValue(later);
        var difference = CalculateDifference(earlierValue, laterValue);
        var percentageDifference = CalculatePercentageDifference(earlierValue, difference);

        return new(identifier, unit, earlierValue, laterValue, difference, percentageDifference);
    }

    private static decimal? ConvertMetricValue(object value)
    {
        if (
            value is null
            || value is double doubleValue && !double.IsFinite(doubleValue)
            || value is float floatValue && !float.IsFinite(floatValue)
        )
        {
            return null;
        }

        try
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static decimal? CalculateDifference(decimal? earlierValue, decimal? laterValue)
    {
        if (earlierValue is null || laterValue is null)
        {
            return null;
        }

        try
        {
            return laterValue.Value - earlierValue.Value;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static decimal? CalculatePercentageDifference(
        decimal? earlierValue,
        decimal? difference
    )
    {
        if (earlierValue is null || earlierValue.Value == 0 || difference is null)
        {
            return null;
        }

        try
        {
            return difference.Value / earlierValue.Value * 100;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(Result<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);

    private sealed record NodeSelection(
        ProfilingSessionData Data,
        Guid NodeId,
        ProfilingNode Node,
        ProfilingNodeParticipation Participation,
        ProfilingRuntimeContext RuntimeContext,
        IReadOnlyList<ProfilingSnapshot> Snapshots
    );
}
