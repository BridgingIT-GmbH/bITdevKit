// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Profiles a Requester pipeline, joining an existing operation or owning an independent operation.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The pipeline result type.</typeparam>
/// <param name="profiling">The optional profiler; omission preserves ordinary request execution.</param>
/// <example><code>services.AddRequester().WithBehavior(typeof(ProfilingRequestBehavior&lt;,&gt;));</code></example>
public sealed class ProfilingRequestBehavior<TRequest, TResponse>(
    IOperationProfiler profiling = null
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private static readonly string requestType = typeof(TRequest).FullName ?? typeof(TRequest).Name;
    private static readonly string key = "requester:" + typeof(TRequest).Name;

    /// <inheritdoc />
    /// <example><code>var result = await behavior.HandleAsync(request, options, typeof(FindProductsHandler), next, cancellationToken);</code></example>
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
            new OperationProfilingStartRequest { Key = key, Kind = "Requester" },
            (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension("requester.requestType", requestType);
                        if (handlerType is not null)
                        {
                            scope.SetDimension(
                                "requester.handlerType",
                                handlerType.FullName ?? handlerType.Name
                            );
                        }
                    }
                }
                catch (Exception)
                { /* Observation faults cannot change request execution. */
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
