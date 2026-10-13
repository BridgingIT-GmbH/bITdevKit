// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Profiles one Notifier dispatch, joining an existing operation or owning an independent operation.</summary>
/// <typeparam name="TRequest">The notification type.</typeparam>
/// <typeparam name="TResponse">The dispatch result type.</typeparam>
/// <param name="profiling">The optional profiler; omission preserves ordinary notification execution.</param>
/// <example><code>services.AddNotifier().WithBehavior(typeof(ProfilingNotificationBehavior&lt;,&gt;));</code></example>
public sealed class ProfilingNotificationBehavior<TRequest, TResponse>(
    IOperationProfiler profiling = null
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private static readonly string notificationType =
        typeof(TRequest).FullName ?? typeof(TRequest).Name;
    private static readonly string key = "notifier:" + typeof(TRequest).Name;

    /// <inheritdoc />
    /// <example><code>var result = await behavior.HandleAsync(notification, options, null, next, cancellationToken);</code></example>
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

        return profiling.JoinOrStartAsync(
            new OperationProfilingStartRequest { Key = key, Kind = "Notifier" },
            (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension("notifier.notificationType", notificationType);
                    }
                }
                catch (Exception)
                { /* Observation faults cannot change notification execution. */
                }

                return next();
            },
            cancellationToken,
            result => OperationProfilingHelpers.ClassifyResult(result)
        );
    }

    /// <inheritdoc />
    /// <example><code>var perHandler = behavior.IsHandlerSpecific(); // false</code></example>
    public bool IsHandlerSpecific() => false;
}
