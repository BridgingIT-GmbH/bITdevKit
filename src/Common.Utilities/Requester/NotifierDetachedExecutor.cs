// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Resolves detached notification handlers and their behaviors inside independently owned worker scopes.</summary>
/// <example><code>var task = NotifierDetachedExecutor.Start(scopes, notification, options, trustedLink, handlerCount);</code></example>
public static class NotifierDetachedExecutor
{
    /// <summary>Starts detached business work without retaining initiating handlers, behaviors, or ambient request state.</summary>
    /// <example><code>var task = NotifierDetachedExecutor.Start(scopes, notification, options, link, _ => 2);</code></example>
    public static Task Start<TNotification>(IServiceScopeFactory scopes, TNotification notification, PublishOptions options, AuditExecutionLink trustedLink, Func<Type, int> handlerCount = null)
        where TNotification : class, INotification
    {
        var copiedContext = options.Context is null ? null : new RequestContext();
        if (copiedContext is not null) foreach (var pair in options.Context.Properties) copiedContext.Properties.Add(pair.Key, pair.Value);
        var copied = new PublishOptions { ExecutionMode = options.ExecutionMode, HandleExceptionsAsResultError = options.HandleExceptionsAsResultError, Progress = options.Progress, Context = copiedContext };
        using (ExecutionContext.SuppressFlow()) return Task.Run(async () =>
        {
            int count;
            if (handlerCount is not null) count = handlerCount(typeof(TNotification));
            else
            {
                await using var discovery = scopes.CreateAsyncScope();
                using var boundary = discovery.ServiceProvider.GetService<IAuditExecutionBoundary>()?.DetachExecution(trustedLink);
                count = discovery.ServiceProvider.GetRequiredService<INotificationHandlerProvider>().GetHandlers<TNotification>(discovery.ServiceProvider).Count;
            }

            await Task.WhenAll(Enumerable.Range(0, count).Select(index => ExecuteAsync(scopes, notification, copied, trustedLink, index))).ConfigureAwait(false);
        }, CancellationToken.None);
    }

    private static async Task ExecuteAsync<TNotification>(IServiceScopeFactory scopes, TNotification notification, PublishOptions options, AuditExecutionLink trustedLink, int index)
        where TNotification : class, INotification
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var boundary = services.GetService<IAuditExecutionBoundary>()?.DetachExecution(trustedLink);
        using var invocation = new AuditRequestInvocation(notification);
        var token = services.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;
        var handlers = services.GetRequiredService<INotificationHandlerProvider>().GetHandlers<TNotification>(services);
        if (index >= handlers.Count) return;
        var handler = handlers[index];
        var behaviors = services.GetRequiredService<INotificationBehaviorsProvider>().GetBehaviors<TNotification>(services)
            .Where(behavior => behavior.IsHandlerSpecific()).OrderBy(behavior => behavior is AuditingNotificationHandlerBehavior<TNotification, IResult> ? 1 : 0).ToArray();
        Func<Task<IResult>> next = async () => await handler.HandleAsync(notification, options, token).ConfigureAwait(false);
        for (var offset = behaviors.Length - 1; offset >= 0; offset--)
        {
            var behavior = behaviors[offset];
            var current = next;
            next = () => behavior.HandleAsync(notification, options, handler.GetType(), current, token);
        }

        try { await next().ConfigureAwait(false); }
        catch (Exception) { services.GetService<ILoggerFactory>()?.CreateLogger("Notifier").LogError("[NOT] detached notification failed (reason={Reason})", "NotificationHandlerFailed"); }
    }
}
