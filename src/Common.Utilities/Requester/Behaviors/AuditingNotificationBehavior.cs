// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Records Notifier logical dispatch outside short-circuit and retry policies only when explicitly registered.</summary>
/// <example><code>services.AddNotifier().WithBehavior(typeof(AuditingNotificationBehavior&lt;,&gt;));</code></example>
public sealed class AuditingNotificationBehavior<TRequest, TResponse>(IAuditContext audit = null, IServiceProvider services = null, NotifierAuditOptions captureOptions = null)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : class where TResponse : IResult
{
    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
        => audit?.IsEnabled != true ? next() : AuditingRequesterBoundary.RunAsync(request, handlerType, next, audit, services, notifications: captureOptions,
            notification: true, handler: false, publish: options as PublishOptions);

    /// <inheritdoc />
    public bool IsHandlerSpecific() => false;
}
