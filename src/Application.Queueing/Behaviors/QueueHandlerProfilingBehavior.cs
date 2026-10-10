// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>Profiles queue handling through optional, failure-isolated operation capture.</summary>
/// <param name="profiling">The optional profiler; omission preserves ordinary handler execution.</param>
/// <example><code>services.AddQueueing().WithBehavior&lt;QueueHandlerProfilingBehavior&gt;();</code></example>
public sealed class QueueHandlerProfilingBehavior(IOperationProfiler profiling = null)
    : IQueueHandlerBehavior,
        IProfilingExecutionBoundaryBehavior
{
    /// <inheritdoc />
    /// <example><code>using var boundary = behavior.BeginExecutionBoundary(); await ProcessHandlerAsync();</code></example>
    public IDisposable BeginExecutionBoundary() => profiling.BeginSafeExecutionBoundary();

    /// <inheritdoc />
    /// <example><code>await behavior.Handle(message, cancellationToken, handler, next);</code></example>
    public Task Handle(
        IQueueMessage message,
        CancellationToken cancellationToken,
        object handler,
        QueueHandlerDelegate next
    )
    {
        if (profiling is null || message is null)
        {
            return next();
        }

        var messageType = message.GetType();
        var handlerType = handler?.GetType();
        string correlationId = null;
        try
        {
            correlationId = CorrelationId.ReadFrom(message.Properties);
        }
        catch (Exception)
        { /* Observation faults cannot change handler execution. */
        }

        return profiling.JoinOrStartAsync(
            new OperationProfilingStartRequest
            {
                Key =
                    "queueing:" + messageType.Name + ":handler:" + (handlerType?.Name ?? "Unknown"),
                Kind = "QueueHandler",
                CorrelationId = correlationId,
            },
            async (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension(
                            "queueing.messageType",
                            messageType.FullName ?? messageType.Name
                        );
                        scope.SetDimension(
                            "queueing.handlerType",
                            handlerType?.FullName ?? handlerType?.Name ?? "Unknown"
                        );
                    }
                }
                catch (Exception)
                { /* Observation faults cannot change handler execution. */
                }

                await next().ConfigureAwait(false);
                return true;
            },
            cancellationToken
        );
    }
}
