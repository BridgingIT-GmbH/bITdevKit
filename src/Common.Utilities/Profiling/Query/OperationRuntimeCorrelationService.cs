// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Relates independent Runtime observations to operations by exact process identity and observed UTC intervals.</summary>
/// <example><code>var overlay = await correlation.GetOverlayAsync(operation, token);</code></example>
public sealed class OperationRuntimeCorrelationService(IRuntimeProfilingCorrelationStore store, int maximumSnapshots = 2000)
{
    /// <summary>Builds a bounded overlay with explicit surrounding samples, rate intervals and coverage limitations.</summary>
    /// <example><code>var samples = (await correlation.GetOverlayAsync(operation, token)).Value.Samples;</code></example>
    public async Task<IResult<OperationProfilingRuntimeOverlay>> GetOverlayAsync(OperationProfilingRecord operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var overlay = new OperationProfilingRuntimeOverlay
        {
            OperationId = operation.Id, NodeId = operation.Node.Identity.Id, StartedUtc = operation.StartedUtc, CompletedUtc = operation.CompletedUtc,
        };
        if (operation.CompletedUtc < operation.StartedUtc || operation.Quality.ClockDiscontinuity)
        { return Result<OperationProfilingRuntimeOverlay>.Success(overlay with { ClockDiscontinuity = true, Limitation = "OperationClockDiscontinuity" }); }

        if (store is null) { return Result<OperationProfilingRuntimeOverlay>.Success(overlay with { Limitation = "CorrelationUnavailable" }); }

        var result = await store.QueryCorrelationAsync(new()
        {
            Node = operation.Node, FromUtc = operation.StartedUtc, ToUtc = operation.CompletedUtc, MaximumSnapshots = maximumSnapshots,
        }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { return Result<OperationProfilingRuntimeOverlay>.Failure(result); }

        var selection = result.Value;
        if (selection.Snapshots.Count > maximumSnapshots || selection.Participations.Count > maximumSnapshots || selection.Sessions.Count > maximumSnapshots)
        { return Result<OperationProfilingRuntimeOverlay>.Failure(new ProfilingQueryLimitError()); }

        var samples = new List<OperationProfilingRuntimeSample>();
        var gaps = new List<OperationProfilingRuntimeGap>();
        foreach (var group in selection.Snapshots.GroupBy(snapshot => snapshot.SessionId))
        {
            var session = selection.Sessions.Single(value => value.Identity.Id == group.Key);
            RuntimeProfilingSnapshot previous = null;
            foreach (var snapshot in group.OrderBy(value => value.Sequence).ThenBy(value => value.Identity.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var skew = previous is not null && (snapshot.TimestampUtc <= previous.TimestampUtc
                    || snapshot.CaptureStartedElapsed <= previous.CaptureStartedElapsed
                    || Math.Abs(((snapshot.TimestampUtc - previous.TimestampUtc) - (snapshot.CaptureStartedElapsed - previous.CaptureStartedElapsed)).TotalMilliseconds)
                        > Math.Max(100, session.SamplingInterval.TotalMilliseconds * .5));
                var gap = previous is not null && (snapshot.Sequence != previous.Sequence + 1
                    || snapshot.SkippedCaptureCount > previous.SkippedCaptureCount || snapshot.FailedCaptureCount > previous.FailedCaptureCount
                    || snapshot.CaptureStartedElapsed - previous.CaptureStartedElapsed > session.SamplingInterval * 2);
                samples.Add(new()
                {
                    Snapshot = snapshot, Relation = Relation(snapshot.TimestampUtc, operation.StartedUtc, operation.CompletedUtc),
                    MetricIntervalFromUtc = skew || previous is null || snapshot.Sequence != previous.Sequence + 1 ? null : previous.TimestampUtc,
                    MetricIntervalToUtc = previous is null || skew || snapshot.Sequence != previous.Sequence + 1 ? null : snapshot.TimestampUtc,
                    SamplingGap = gap, ClockDiscontinuity = skew,
                });
                if (gap || skew) { gaps.Add(new() { SessionKey = snapshot.SessionKey, FromUtc = previous.TimestampUtc, ToUtc = snapshot.TimestampUtc, Reason = skew ? "ClockDiscontinuity" : "SamplingGap" }); }

                previous = snapshot;
            }
        }

        var inside = samples.Count(value => value.Relation == "Inside");
        return Result<OperationProfilingRuntimeOverlay>.Success(overlay with
        {
            Available = samples.Count > 0, Snapshots = selection.Snapshots, Sessions = selection.Sessions, Participations = selection.Participations,
            Samples = Array.AsReadOnly(samples.OrderBy(value => value.Snapshot.TimestampUtc).ThenBy(value => value.Snapshot.Identity.Key, StringComparer.Ordinal).ToArray()),
            Gaps = Array.AsReadOnly(gaps.ToArray()), ClockDiscontinuity = samples.Any(value => value.ClockDiscontinuity), InsideSnapshotCount = inside,
            Limitation = samples.Count == 0 ? "NoMatchingRuntimeEvidence" : inside == 0 ? "SurroundingSamplesOnly" : gaps.Count > 0 ? "PartialRuntimeCoverage" : "ProcessMetricsAreNotOperationMetrics",
        });
    }

    /// <summary>Validates exact process identity, rejecting restarts and remapped archive identities.</summary>
    /// <example><code>var matches = OperationRuntimeCorrelationService.MatchesNode(stored, requested);</code></example>
    public static bool MatchesNode(ProfilingNode stored, ProfilingNode requested) => stored is not null && requested is not null
        && stored.Identity == requested.Identity && stored.ProcessId == requested.ProcessId
        && string.Equals(stored.HostName, requested.HostName, StringComparison.Ordinal)
        && stored.ProcessStartedUtc.UtcTicks == requested.ProcessStartedUtc.UtcTicks;

    /// <summary>Validates a bounded UTC correlation selector without inferring process identity from names.</summary>
    /// <example><code>var valid = OperationRuntimeCorrelationService.Validate(request);</code></example>
    public static IResult Validate(RuntimeProfilingCorrelationRequest request) => request?.Node is null
        || request.Node.Identity.Id == Guid.Empty || request.MaximumSnapshots <= 0 || request.MaximumSnapshots > 2000
        || request.FromUtc.Offset != TimeSpan.Zero || request.ToUtc.Offset != TimeSpan.Zero || request.ToUtc < request.FromUtc
        ? Result.Failure(new ProfilingValidationError("Correlation requires an exact node, a UTC interval and at most 2,000 samples.")) : Result.Success();

    /// <summary>Labels inside observations and explicitly contextual samples at exclusive interval boundaries.</summary>
    /// <example><code>var label = OperationRuntimeCorrelationService.Relation(sample.TimestampUtc, fromUtc, toUtc);</code></example>
    public static string Relation(DateTimeOffset timestamp, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        timestamp < fromUtc ? "Before" : timestamp < toUtc || fromUtc == toUtc && timestamp == fromUtc ? "Inside" : "After";
}
