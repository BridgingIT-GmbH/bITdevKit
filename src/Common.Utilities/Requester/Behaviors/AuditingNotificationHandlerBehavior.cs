// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Records Notifier actual handler invocations only when explicitly registered.</summary>
/// <example><code>services.AddNotifier().WithBehavior(typeof(AuditingNotificationHandlerBehavior&lt;,&gt;));</code></example>
public sealed class AuditingNotificationHandlerBehavior<TRequest, TResponse>(IAuditContext audit = null, IServiceProvider services = null, NotifierAuditOptions captureOptions = null)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : class where TResponse : IResult
{
    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
        => audit?.IsEnabled != true ? next() : AuditingRequesterBoundary.RunAsync(request, handlerType, next, audit, services, notifications: captureOptions,
            notification: true, handler: true, publish: options as PublishOptions);

    /// <inheritdoc />
    public bool IsHandlerSpecific() => true;
}
