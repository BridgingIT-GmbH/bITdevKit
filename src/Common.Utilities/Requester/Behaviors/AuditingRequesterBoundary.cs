// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Shares selection and scope ownership for explicit Requester and Notifier adapters.</summary>
/// <example><code>services.AddRequester().WithAuditing();</code></example>
public static class AuditingRequesterBoundary
{
    /// <summary>Observes one logical dispatch or actual handler while preserving the original result and exception.</summary>
    /// <example><code>return await AuditingRequesterBoundary.RunAsync(request, handlerType, next, audit, services);</code></example>
    public static async Task<TResponse> RunAsync<TRequest, TResponse>(TRequest request, Type handlerType, Func<Task<TResponse>> next,
        IAuditContext audit, IServiceProvider services = null, RequesterAuditOptions requests = null, NotifierAuditOptions notifications = null,
        bool notification = false, bool handler = false, PublishOptions publish = null)
        where TRequest : class where TResponse : IResult
    {
        var attempt = handler ? AuditRequestInvocation.Invoked(request, handlerType) : null;
        if (audit?.IsEnabled != true || request is null) return await next().ConfigureAwait(false);
        var kind = "Notification";
        var selected = false;
        try
        {
            if (notification)
            {
                selected = (notifications ?? new()).Select(typeof(TRequest));
            }
            else
            {
                requests ??= new();
                kind = requests.ResolveKind(typeof(TRequest));
                selected = kind is "Command" or "Query" or "Request" && (requests.Select?.Invoke(typeof(TRequest)) ?? kind == "Command");
            }
        }
        catch (Exception)
        {
            try { (audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditSourceSelectionFailed"); }
            catch (Exception) { /* Diagnostics cannot change source execution. */ }
        }

        if (!selected) return await next().ConfigureAwait(false);

        using var invocation = handler ? null : new AuditRequestInvocation(request);
        var source = new AuditSourceMetadata
        {
            Feature = notification ? "Notifier" : "Requester", RequestType = typeof(TRequest).FullName ?? typeof(TRequest).Name,
            HandlerType = handlerType?.FullName, Attempt = attempt,
            RequestId = request is IRequest sourceRequest ? sourceRequest.RequestId.ToString("D") : request is INotification sourceNotification ? sourceNotification.NotificationId.ToString("D") : null,
            ExecutionStatus = handler ? "Invoked" : "Dispatched"
        };
        return await AuditBehaviorExecutor.RunAsync(
            () => handler
                ? audit.BeginScope(builder => builder.For(handlerType ?? typeof(TRequest), "Handle").Kind(notification ? "NotificationHandler" : "RequestHandler"))
                : audit.BeginScopeFor(request, services, builder => builder.Kind(kind)), next, source,
            (scope, result) =>
            {
                if (handler) return;
                var status = invocation.ExecutionStatus ?? (notification && publish?.ExecutionMode == ExecutionMode.FireAndForget ? "Accepted"
                    : invocation.Count == 0 ? "ShortCircuited" : "Invoked");
                (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = status });
            }).ConfigureAwait(false);
    }
}
