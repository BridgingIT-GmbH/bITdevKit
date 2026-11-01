// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves optional scoped capture for singleton source behaviors and finite independent workers.</summary>
/// <example><code>return AuditSourceExecutor.RunAsync(scopes, b => b.Key("customers.send"), next, new() { Feature = "Messaging" });</code></example>
public static class AuditSourceExecutor
{
    /// <summary>Joins the live execution or owns one finite source call, retaining no scoped services between calls.</summary>
    /// <example><code>await AuditSourceExecutor.RunAsync(scopes, b => b.For&lt;Customer&gt;("Send"), next);</code></example>
    public static Task<T> RunAsync<T>(IServiceScopeFactory scopes, Action<AuditScopeOptionsBuilder> configure, Func<Task<T>> next,
        AuditSourceMetadata metadata = null, Action<IServiceProvider, IAuditScope> starting = null, Action<IAuditScope, T> completed = null, Action<IAuditScope, Exception> failed = null, Func<bool> select = null)
        => ExecuteAsync(scopes, configure, next, metadata, starting, completed, null, null, null, false, null, failed, true, null, select);

    /// <summary>Clears inherited audit and suppression state, authenticates only the operation-bound token, and owns a worker root.</summary>
    /// <example><code>await AuditSourceExecutor.RunWorkerAsync(scopes, "Jobs", occurrenceId, token, b => b.Key("jobs.execute"), next);</code></example>
    public static Task<T> RunWorkerAsync<T>(IServiceScopeFactory scopes, string feature, string operationId, string token,
        Action<AuditScopeOptionsBuilder> configure, Func<Task<T>> next, AuditSourceMetadata metadata = null,
        Action<IServiceProvider, IAuditScope> starting = null, Action<IAuditScope, T> completed = null, Action<IAuditScope, Exception> failed = null, string correlationId = null)
        => ExecuteAsync(scopes, configure, next, metadata, starting, completed, feature, operationId, token, true, null, failed, true, correlationId);

    /// <summary>Records selected producer intent and stamps its protected link before deferred admission.</summary>
    /// <remarks>Disabled or suppressed capture clears stale handoff metadata while skipping capture callbacks.</remarks>
    /// <example><code>await AuditSourceExecutor.RunProducerAsync(scopes, "Messaging", messageId, properties, b => b.Key("customers.publish"), next);</code></example>
    public static Task<T> RunProducerAsync<T>(IServiceScopeFactory scopes, string feature, string operationId, IDictionary<string, object> properties,
        Action<AuditScopeOptionsBuilder> configure, Func<Task<T>> next, AuditSourceMetadata metadata = null,
        Action<IServiceProvider, IAuditScope> starting = null, Action<IAuditScope, T> completed = null, Action<IAuditScope, Exception> failed = null)
        => ExecuteAsync(scopes, configure, next, metadata, starting, completed, feature, operationId, null, false, properties, failed, true, null);

    /// <summary>Isolates a real worker without creating a job, message, or other source audit occurrence.</summary>
    /// <remarks>The first selected nested boundary may own an independent root even when this source's capture is omitted.</remarks>
    /// <example><code>await AuditSourceExecutor.RunIsolatedAsync(scopes, "Jobs", occurrenceId, token, correlationId, next);</code></example>
    public static Task<T> RunIsolatedAsync<T>(IServiceScopeFactory scopes, string feature, string operationId, string token, string correlationId, Func<Task<T>> next)
        => ExecuteAsync(scopes, null, next, null, null, null, feature, operationId, token, true, null, null, false, correlationId);

    /// <summary>Schedules an independent source loop without retaining its caller's ambient context while idle.</summary>
    /// <remarks>Finite work inside the loop must still use an explicit execution boundary to establish service attribution.</remarks>
    /// <example><code>var task = AuditSourceExecutor.RunDetached(() => ProcessEventsAsync(cancellationToken), cancellationToken);</code></example>
    public static Task RunDetached(Func<Task> next, CancellationToken cancellationToken = default)
    {
        if (ExecutionContext.IsFlowSuppressed()) return Task.Run(next, cancellationToken);
        using (ExecutionContext.SuppressFlow()) return Task.Run(next, cancellationToken);
    }

    /// <summary>Removes repeated known audit adapters while preserving every custom behavior registration and its order.</summary>
    /// <example><code>var selected = AuditSourceExecutor.DistinctAuditing&lt;IProcessorBehavior, CustomerAuditBehavior&gt;(behaviors);</code></example>
    public static IEnumerable<TBehavior> DistinctAuditing<TBehavior, TAuditing>(IEnumerable<TBehavior> behaviors) where TBehavior : class where TAuditing : class, TBehavior
    {
        var seen = false;
        foreach (var behavior in behaviors ?? [])
        {
            if (behavior is TAuditing)
            {
                if (seen) continue;
                seen = true;
            }

            yield return behavior;
        }
    }

    private static async Task<T> ExecuteAsync<T>(IServiceScopeFactory scopes, Action<AuditScopeOptionsBuilder> configure, Func<Task<T>> next,
        AuditSourceMetadata metadata, Action<IServiceProvider, IAuditScope> starting, Action<IAuditScope, T> completed,
        string feature, string operationId, string token, bool worker, IDictionary<string, object> properties, Action<IAuditScope, Exception> failed, bool capture, string correlationId, Func<bool> select = null)
    {
        AsyncServiceScope? services = null;
        IDisposable boundary = null;
        IDisposable sourceCorrelation = null;
        IAuditContext audit = null;
        try
        {
            services = scopes.CreateAsyncScope();
            audit = services.Value.ServiceProvider.GetService<IAuditContext>();
            if (worker)
            {
                var execution = services.Value.ServiceProvider.GetService<IAuditExecutionBoundary>();
                boundary = execution?.DetachExecution();
                if (CorrelationId.IsValid(correlationId)) sourceCorrelation = CorrelationId.BeginScope(correlationId);
                var protector = services.Value.ServiceProvider.GetService<IAuditExecutionLinkProtector>();
                var link = protector?.Unprotect(token, feature, operationId);
                if (link is not null)
                {
                    // Native technical correlation may intentionally differ from its originating audit.
                    // Authentication remains attached to the original causal IDs and initiating identity.
                    if (CorrelationId.IsValid(correlationId)) link = link with { CorrelationId = correlationId };
                    sourceCorrelation?.Dispose();
                    sourceCorrelation = null;
                    boundary?.Dispose();
                    boundary = execution?.DetachExecution(link);
                }

                if (token is not null && link is null) (audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditHandoffUnavailable");
            }
        }
        catch (Exception) { Report(audit); }

        try
        {
            var selected = true;
            if (select is not null)
            {
                selected = false;
                try { if (audit?.IsEnabled == true) selected = select(); }
                catch (Exception) { Report(audit); }
            }

            if (audit is null || !capture || !selected)
            {
                properties?.Remove(AuditHandoffMetadata.PropertyName);
                return await next().ConfigureAwait(false);
            }

            IAuditScope Begin()
            {
                var scope = NoOpAuditScope.Instance as IAuditScope;
                try { scope = audit.BeginScope(configure); }
                finally
                {
                    try { if (properties is not null) AuditHandoffMetadata.Stamp(services.Value.ServiceProvider, scope, properties, feature, operationId); }
                    catch (Exception) { Report(audit); properties?.Remove(AuditHandoffMetadata.PropertyName); }
                }

                return scope;
            }

            return await AuditBehaviorExecutor.RunAsync(Begin, next, metadata, completed,
                scope => starting?.Invoke(services.Value.ServiceProvider, scope), failed).ConfigureAwait(false);
        }
        finally
        {
            try { sourceCorrelation?.Dispose(); }
            catch (Exception) { Report(audit); }

            try { boundary?.Dispose(); }
            catch (Exception) { Report(audit); }

            try { if (services.HasValue) await services.Value.DisposeAsync().ConfigureAwait(false); }
            catch (Exception) { Report(audit); }
        }
    }
    private static void Report(IAuditContext audit)
    {
        try { (audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditSourceScopeUnavailable"); }
        catch (Exception) { /* Diagnostic failures never replace business execution. */ }
    }
}
