// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Computes fixed, deterministic analysis for one profiling node and session.
/// </summary>
/// <remarks>
/// Evaluation reads stored observations once and never mutates profiling state. The same input
/// under the same application build produces a structurally equal result.
/// </remarks>
/// <example><code>var result = await evaluator.EvaluateAsync(new(sessionKey, nodeKey), cancellationToken);</code></example>
public sealed partial class RuntimeProfilingEvaluator(
    ProfilingOptions options,
    IRuntimeProfilingStore store = null
) : IRuntimeProfilingEvaluationService
{
    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingEvaluationResult>> EvaluateAsync(
        RuntimeProfilingEvaluationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled)
        {
            return Failure(new ProfilingDisabledError());
        }

        if (store is null)
        {
            return Failure(new ProfilingUnavailableError("No profiling store is registered."));
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return Failure(validation);
        }

        var dataResult = await store
            .GetSessionDataAsync(request.SessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (!dataResult.IsSuccess)
        {
            return Result<RuntimeProfilingEvaluationResult>
                .Failure()
                .WithErrors(dataResult.Errors)
                .WithMessages(dataResult.Messages);
        }

        var selection = SelectScope(request, dataResult.Value);
        if (!selection.IsSuccess)
        {
            return Result<RuntimeProfilingEvaluationResult>
                .Failure()
                .WithErrors(selection.Errors)
                .WithMessages(selection.Messages);
        }

        var facts = CalculateFacts(
            selection.Value.Mode,
            dataResult.Value.Session,
            selection.Value.Participation,
            selection.Value.RuntimeContext,
            selection.Value.Snapshots
        );
        var signals = facts.CanEmitSignals ? EvaluateRules(facts) : [];

        return Result<RuntimeProfilingEvaluationResult>.Success(
            new(
                new(
                    selection.Value.Mode,
                    request.SessionKey,
                    request.NodeKey,
                    selection.Value.Mode is RuntimeProfilingEvaluationMode.TwoSnapshots
                        ? selection.Value.Snapshots.Select(x => x.Identity.Key).ToArray()
                        : [],
                    selection.Value.Snapshots.FirstOrDefault()?.TimestampUtc,
                    selection.Value.Snapshots.LastOrDefault()?.TimestampUtc,
                    selection.Value.Snapshots.Count,
                    dataResult.Value.Session.State is RuntimeProfilingSessionState.Running
                ),
                facts.DataQuality,
                facts.Kpis,
                signals,
                facts.Limitations
            )
        );
    }

    private static IResultError ValidateRequest(RuntimeProfilingEvaluationRequest request)
    {
        if (request is null)
        {
            return new ProfilingValidationError("An evaluation request is required.");
        }

        if (!IsReadableKey(request.SessionKey))
        {
            return new ProfilingInvalidKeyError("session");
        }

        if (!IsReadableKey(request.NodeKey))
        {
            return new ProfilingInvalidKeyError("node");
        }

        var hasSnapshotA = !string.IsNullOrWhiteSpace(request.SnapshotAKey);
        var hasSnapshotB = !string.IsNullOrWhiteSpace(request.SnapshotBKey);
        if (hasSnapshotA != hasSnapshotB)
        {
            return new ProfilingValidationError(
                "Two-snapshot evaluation requires both snapshot keys."
            );
        }

        if (
            (hasSnapshotA && !IsReadableKey(request.SnapshotAKey))
            || (hasSnapshotB && !IsReadableKey(request.SnapshotBKey))
        )
        {
            return new ProfilingInvalidKeyError("snapshot");
        }

        return null;
    }

    private static Result<EvaluationSelection> SelectScope(
        RuntimeProfilingEvaluationRequest request,
        RuntimeProfilingSessionData data
    )
    {
        if (
            data.Session is null
            || !string.Equals(
                data.Session.Identity.Key,
                request.SessionKey,
                StringComparison.Ordinal
            )
        )
        {
            return SelectionFailure(
                new ProfilingValidationError(
                    "The loaded profiling data does not match the requested session."
                )
            );
        }

        var participation = data.Participations.SingleOrDefault(x =>
            string.Equals(x.NodeKey, request.NodeKey, StringComparison.Ordinal)
        );
        var runtimeContext = data.RuntimeContexts.SingleOrDefault(x =>
            string.Equals(x.NodeKey, request.NodeKey, StringComparison.Ordinal)
        );
        var nodeExists =
            participation is not null
            || runtimeContext is not null
            || data.Nodes.Any(x =>
                string.Equals(x.Identity.Key, request.NodeKey, StringComparison.Ordinal)
            )
            || data.Snapshots.Any(x =>
                string.Equals(x.NodeKey, request.NodeKey, StringComparison.Ordinal)
            );
        if (!nodeExists)
        {
            return SelectionFailure(
                new NotFoundError(
                    $"Profiling node '{request.NodeKey}' was not found in session '{request.SessionKey}'."
                )
            );
        }

        var nodeSnapshots = data
            .Snapshots.Where(x =>
                string.Equals(x.SessionKey, request.SessionKey, StringComparison.Ordinal)
                && string.Equals(x.NodeKey, request.NodeKey, StringComparison.Ordinal)
            )
            .OrderBy(x => x.Sequence)
            .ThenBy(x => x.CaptureStartedElapsed)
            .ThenBy(x => x.Identity.Key, StringComparer.Ordinal)
            .ToArray();
        var isPair = !string.IsNullOrWhiteSpace(request.SnapshotAKey);
        if (!isPair)
        {
            return Result<EvaluationSelection>.Success(
                new(
                    RuntimeProfilingEvaluationMode.NodeSession,
                    nodeSnapshots,
                    participation,
                    runtimeContext
                )
            );
        }

        var snapshotA = data.Snapshots.SingleOrDefault(x =>
            string.Equals(x.Identity.Key, request.SnapshotAKey, StringComparison.Ordinal)
        );
        var snapshotB = data.Snapshots.SingleOrDefault(x =>
            string.Equals(x.Identity.Key, request.SnapshotBKey, StringComparison.Ordinal)
        );
        if (snapshotA is null || snapshotB is null)
        {
            return SelectionFailure(
                new NotFoundError("One or both selected profiling snapshots were not found.")
            );
        }

        if (!MatchesRequest(snapshotA, request) || !MatchesRequest(snapshotB, request))
        {
            return SelectionFailure(
                new ProfilingValidationError(
                    "Selected snapshots must belong to the requested session and node."
                )
            );
        }

        if (
            snapshotA.Sequence >= snapshotB.Sequence
            || snapshotA.CaptureStartedElapsed >= snapshotB.CaptureStartedElapsed
        )
        {
            return SelectionFailure(
                new ProfilingValidationError(
                    "Snapshot A must precede snapshot B in node-local sequence and monotonic time."
                )
            );
        }

        return Result<EvaluationSelection>.Success(
            new(
                RuntimeProfilingEvaluationMode.TwoSnapshots,
                [snapshotA, snapshotB],
                participation,
                runtimeContext
            )
        );
    }

    private static bool MatchesRequest(
        RuntimeProfilingSnapshot snapshot,
        RuntimeProfilingEvaluationRequest request
    ) =>
        string.Equals(snapshot.SessionKey, request.SessionKey, StringComparison.Ordinal)
        && string.Equals(snapshot.NodeKey, request.NodeKey, StringComparison.Ordinal);

    private static bool IsReadableKey(string value) =>
        value?.Length == 8
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private static Result<RuntimeProfilingEvaluationResult> Failure(IResultError error) =>
        Result<RuntimeProfilingEvaluationResult>.Failure().WithError(error);

    private static Result<EvaluationSelection> SelectionFailure(IResultError error) =>
        Result<EvaluationSelection>.Failure().WithError(error);

    private sealed record EvaluationSelection(
        RuntimeProfilingEvaluationMode Mode,
        IReadOnlyList<RuntimeProfilingSnapshot> Snapshots,
        RuntimeProfilingNodeParticipation Participation,
        RuntimeProfilingContext RuntimeContext
    );

    private sealed class EvaluationFacts
    {
        public RuntimeProfilingEvaluationMode Mode { get; init; }

        public IReadOnlyList<RuntimeProfilingSnapshot> Snapshots { get; init; } = [];

        public TimeSpan Elapsed { get; init; }

        public bool CanEmitSignals { get; init; }

        public bool HighConfidenceAllowed { get; init; }

        public bool HighConfidenceWindow { get; init; }

        public bool HasInvalidIntervals { get; init; }

        public RuntimeProfilingEvaluationDataQuality DataQuality { get; init; }

        public IReadOnlyList<RuntimeProfilingKpi> Kpis { get; init; } = [];

        public IReadOnlyList<string> Limitations { get; init; } = [];

        public double? CpuAverage { get; init; }

        public double? CpuFirstHalfAverage { get; init; }

        public double? CpuSecondHalfAverage { get; init; }

        public double? CpuEnding { get; init; }

        public double CpuAtLeast70Ratio { get; init; }

        public double CpuAtLeast80Ratio { get; init; }

        public long? ManagedHeapStart { get; init; }

        public long? ManagedHeapEnd { get; init; }

        public long? PrivateMemoryStart { get; init; }

        public long? PrivateMemoryEnd { get; init; }

        public long? LohStart { get; init; }

        public long? LohEnd { get; init; }

        public double? LohFragmentationStart { get; init; }

        public double? LohFragmentationEnd { get; init; }

        public long? LatestGen2ManagedHeapBytes { get; init; }

        public double? AllocationAverage { get; init; }

        public double? AllocationFirstHalfAverage { get; init; }

        public double? AllocationSecondHalfAverage { get; init; }

        public double? AllocationStart { get; init; }

        public double? AllocationEnd { get; init; }

        public long? Gen0Delta { get; init; }

        public long? Gen1Delta { get; init; }

        public long? Gen2Delta { get; init; }

        public double? Gen0Rate { get; init; }

        public double? Gen1Rate { get; init; }

        public double? Gen2Rate { get; init; }

        public double? GcPauseBurdenPercent { get; init; }

        public bool HasCpuInput { get; init; }

        public bool HasManagedHeapInput { get; init; }

        public bool HasPrivateMemoryInput { get; init; }

        public bool HasLohInput { get; init; }

        public bool HasAllocationInput { get; init; }

        public bool HasGcInput { get; init; }
    }

    private sealed record EvaluationInterval(
        double StartSeconds,
        double EndSeconds,
        double DurationSeconds,
        double? CpuPercent,
        double? AllocationRateBytesPerSecond,
        long? Gen0Delta,
        long? Gen1Delta,
        long? Gen2Delta,
        double? GcPauseBurdenPercent,
        bool HasCounterReset
    );
}
