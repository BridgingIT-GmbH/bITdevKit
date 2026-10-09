// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>Profiles message handling through optional, failure-isolated operation capture.</summary>
/// <param name="profiling">The optional profiler; omission preserves ordinary handler execution.</param>
/// <example><code>services.AddMessaging().WithBehavior&lt;MessageHandlerProfilingBehavior&gt;();</code></example>
public sealed class MessageHandlerProfilingBehavior(IOperationProfiler profiling = null)
    : IMessageHandlerBehavior,
        IProfilingExecutionBoundaryBehavior
{
    /// <inheritdoc />
    /// <example><code>using var boundary = behavior.BeginExecutionBoundary(); await ProcessHandlerAsync();</code></example>
    public IDisposable BeginExecutionBoundary() => profiling.BeginSafeExecutionBoundary();

    /// <inheritdoc />
    /// <example><code>await behavior.Handle(message, cancellationToken, handler, next);</code></example>
    public Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next
    )
        where TMessage : IMessage
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
            if (
                message.Properties?.TryGetValue(Constants.CorrelationIdKey, out var value) == true
                && value is string { Length: <= 128 } text
            )
            {
                correlationId = text;
            }
        }
        catch (Exception)
        { /* Observation faults cannot change handler execution. */
        }

        return profiling.JoinOrStartAsync(
            new OperationProfilingStartRequest
            {
                Key =
                    "messaging:"
                    + messageType.Name
                    + ":handler:"
                    + (handlerType?.Name ?? "Unknown"),
                Kind = "MessageHandler",
                CorrelationId = correlationId,
            },
            async (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension(
                            "messaging.messageType",
                            messageType.FullName ?? messageType.Name
                        );
                        scope.SetDimension(
                            "messaging.handlerType",
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
