// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Idempotently completes an elapsed profiling session.</summary>
/// <param name="store">The profiling session store.</param>
/// <param name="options">The shared profiling options.</param>
/// <param name="timeProvider">The UTC time provider.</param>
/// <example><code>var result = await finalizer.FinalizeAsync(session, cancellationToken);</code></example>
public sealed class ProfilingSessionFinalizer(
    IProfilingStore store,
    ProfilingOptions options,
    TimeProvider timeProvider
)
{
    /// <summary>Completes an elapsed running session using store compare-and-set semantics.</summary>
    /// <param name="session">The session to finalize.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal session, or a failure when finalization is not yet valid.</returns>
    /// <example><code>await finalizer.FinalizeAsync(session, cancellationToken);</code></example>
    public async Task<Result<ProfilingSession>> FinalizeAsync(
        ProfilingSession session,
        CancellationToken cancellationToken = default
    )
    {
        if (session is null)
        {
            return Failure<ProfilingSession>(
                new ProfilingValidationError("A profiling session is required.")
            );
        }

        var currentResult = await store
            .FindSessionAsync(session.Identity.Key, cancellationToken)
            .ConfigureAwait(false);
        if (currentResult.IsFailure)
        {
            return CopyFailure<ProfilingSession, ProfilingSession>(currentResult);
        }

        var current = currentResult.Value;
        if (current.State != ProfilingSessionState.Running)
        {
            return Result<ProfilingSession>.Success(current);
        }

        var finalizationUtc = current.EndsUtc.Add(options.FinalizationGracePeriod);
        if (timeProvider.GetUtcNow() < finalizationUtc)
        {
            return Failure<ProfilingSession>(
                new ProfilingInvalidStateError(
                    "The profiling session cannot be finalized before its end and grace period."
                )
            );
        }

        var dataResult = await store
            .GetSessionDataAsync(current.Identity.Key, cancellationToken)
            .ConfigureAwait(false);
        if (dataResult.IsFailure)
        {
            return CopyFailure<ProfilingSession, ProfilingSessionData>(dataResult);
        }

        var hasWarnings = dataResult.Value.Participations.Any(participation =>
            participation.Role == ProfilingNodeRole.ExpectedParticipant
            && (
                participation.State != ProfilingParticipationState.Completed
                || participation.FailedCaptureCount > 0
            )
        );
        var interruptionResult = await this.InterruptIncompleteSegmentsAsync(
                dataResult.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (interruptionResult.IsFailure)
        {
            return CopyFailure<ProfilingSession, bool>(interruptionResult);
        }

        var nextState = hasWarnings
            ? ProfilingSessionState.CompletedWithWarnings
            : ProfilingSessionState.Completed;
        var transitionResult = await store
            .TryTransitionSessionAsync(
                current.Identity.Id,
                [ProfilingSessionState.Running],
                nextState,
                timeProvider.GetUtcNow(),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (transitionResult.IsSuccess)
        {
            return transitionResult;
        }

        var competingResult = await store
            .FindSessionAsync(current.Identity.Key, cancellationToken)
            .ConfigureAwait(false);
        return
            competingResult.IsSuccess
            && competingResult.Value.State != ProfilingSessionState.Running
            ? Result<ProfilingSession>.Success(competingResult.Value)
            : CopyFailure<ProfilingSession, ProfilingSession>(transitionResult);
    }

    private async Task<Result<bool>> InterruptIncompleteSegmentsAsync(
        ProfilingSessionData data,
        CancellationToken cancellationToken
    )
    {
        var completedNodeIds = data
            .Participations.Where(participation =>
                participation.State == ProfilingParticipationState.Completed
            )
            .Select(participation => participation.NodeId)
            .ToHashSet();
        foreach (
            var segment in data.Segments.Where(segment =>
                segment.Outcome == ProfilingSegmentOutcome.Open
                && !completedNodeIds.Contains(segment.NodeId)
            )
        )
        {
            var interruptionResult = await store
                .UpsertSegmentAsync(
                    segment with
                    {
                        Outcome = ProfilingSegmentOutcome.Interruption,
                        CollectionEndedBeforeOperation = true,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (interruptionResult.IsSuccess)
            {
                continue;
            }

            var competingResult = await store
                .GetSessionDataAsync(data.Session.Identity.Key, cancellationToken)
                .ConfigureAwait(false);
            if (
                competingResult.IsSuccess
                && competingResult.Value.Segments.Any(candidate =>
                    candidate.Id == segment.Id && candidate.Outcome != ProfilingSegmentOutcome.Open
                )
            )
            {
                continue;
            }

            return CopyFailure<bool, ProfilingSegment>(interruptionResult);
        }

        return Result<bool>.Success(true);
    }

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(Result<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);
}
