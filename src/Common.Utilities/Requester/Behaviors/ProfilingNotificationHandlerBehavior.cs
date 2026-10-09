// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Profiles each concrete notification handler, with independent ownership for fire-and-forget work.</summary>
/// <typeparam name="TRequest">The notification type.</typeparam>
/// <typeparam name="TResponse">The handler result type.</typeparam>
/// <param name="profiling">The optional profiler; omission preserves ordinary handler execution.</param>
/// <example><code>services.AddNotifier().WithBehavior(typeof(ProfilingNotificationHandlerBehavior&lt;,&gt;));</code></example>
public sealed class ProfilingNotificationHandlerBehavior<TRequest, TResponse>(
    IOperationProfiler profiling = null
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private static readonly string notificationType =
        typeof(TRequest).FullName ?? typeof(TRequest).Name;
    private static readonly string keyPrefix = "notifier:" + typeof(TRequest).Name + ":handler:";

    /// <inheritdoc />
    /// <example><code>var result = await behavior.HandleAsync(notification, options, typeof(OrderChangedHandler), next, cancellationToken);</code></example>
    public Task<TResponse> HandleAsync(
        TRequest request,
        object options,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken = default
    )
    {
        if (profiling is null || request is null)
        {
            return next();
        }

        return options is PublishOptions { ExecutionMode: ExecutionMode.FireAndForget }
            ? this.HandleDetachedAsync(handlerType, next, cancellationToken)
            : this.ProfileAsync(handlerType, next, cancellationToken);
    }

    /// <inheritdoc />
    /// <example><code>var perHandler = behavior.IsHandlerSpecific(); // true</code></example>
    public bool IsHandlerSpecific() => true;

    private async Task<TResponse> HandleDetachedAsync(
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken
    )
    {
        using var boundary = profiling.BeginSafeExecutionBoundary();
        return await this.ProfileAsync(handlerType, next, cancellationToken).ConfigureAwait(false);
    }

    private Task<TResponse> ProfileAsync(
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken
    )
    {
        var handlerName = handlerType?.FullName ?? handlerType?.Name ?? "Unknown";
        return profiling.JoinOrStartAsync(
            new OperationProfilingStartRequest
            {
                Key = keyPrefix + (handlerType?.Name ?? "Unknown"),
                Kind = "NotifierHandler",
            },
            (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension("notifier.notificationType", notificationType);
                        scope.SetDimension("notifier.handlerType", handlerName);
                    }
                }
                catch (Exception)
                { /* Observation faults cannot change handler execution. */
                }

                return next();
            },
            cancellationToken,
            result => OperationProfilingHelpers.ClassifyResult(result)
        );
    }
}
