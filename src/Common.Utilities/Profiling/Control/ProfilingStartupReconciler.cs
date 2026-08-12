// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Finalizes profiling sessions abandoned beyond their lifecycle deadline.</summary>
/// <param name="store">The profiling session store.</param>
/// <param name="options">The shared profiling options.</param>
/// <param name="timeProvider">The UTC time provider.</param>
/// <param name="finalizer">The idempotent session finalizer.</param>
/// <remarks>This startup-only operation does not poll the session store.</remarks>
/// <example><code>await reconciler.ReconcileAsync(cancellationToken);</code></example>
public sealed class ProfilingStartupReconciler(
    IProfilingStore store,
    ProfilingOptions options,
    TimeProvider timeProvider,
    ProfilingSessionFinalizer finalizer
)
{
    /// <summary>Performs one bounded startup reconciliation pass.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the number of sessions finalized by this pass.</returns>
    /// <example><code>var result = await reconciler.ReconcileAsync(cancellationToken);</code></example>
    public async Task<Result<int>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var sessionsResult = await store.ListSessionsAsync(cancellationToken).ConfigureAwait(false);
        if (sessionsResult.IsFailure)
        {
            return Result<int>
                .Failure()
                .WithErrors(sessionsResult.Errors)
                .WithMessages(sessionsResult.Messages);
        }

        var now = timeProvider.GetUtcNow();
        var overdue = sessionsResult.Value.Where(session =>
            session.State == ProfilingSessionState.Running
            && now >= session.EndsUtc.Add(options.FinalizationGracePeriod)
        );
        var finalizedCount = 0;
        foreach (var session in overdue)
        {
            var result = await finalizer
                .FinalizeAsync(session, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure)
            {
                return Result<int>
                    .Failure(finalizedCount)
                    .WithErrors(result.Errors)
                    .WithMessages(result.Messages);
            }

            finalizedCount++;
        }

        return Result<int>.Success(finalizedCount);
    }
}
