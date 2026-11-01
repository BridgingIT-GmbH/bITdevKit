// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Records Requester actual handler invocations only when explicitly registered.</summary>
/// <example><code>services.AddRequester().WithBehavior(typeof(AuditingRequestHandlerBehavior&lt;,&gt;));</code></example>
public sealed class AuditingRequestHandlerBehavior<TRequest, TResponse>(IAuditContext audit = null, IServiceProvider services = null, RequesterAuditOptions captureOptions = null)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : class where TResponse : IResult
{
    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
        => audit?.IsEnabled != true ? next() : AuditingRequesterBoundary.RunAsync(request, handlerType, next, audit, services, requests: captureOptions,
            notification: false, handler: true, publish: options as PublishOptions);

    /// <inheritdoc />
    public bool IsHandlerSpecific() => true;
}
