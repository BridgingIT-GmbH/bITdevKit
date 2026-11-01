// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns a finite execution slice and persists only an authenticated causal token across durable waits.</summary>
/// <example><code>services.AddOrchestrations().WithAuditingBehavior();</code></example>
public sealed class OrchestrationAuditingExecutionScope(IServiceScopeFactory scopes)
{
    /// <summary>Copies the current selected cause into the existing snapshot property bag before its normal save.</summary>
    /// <example><code>adapter.StampCause(context);</code></example>
    public void StampCause<TData>(OrchestrationContext<TData> context) where TData : class, IOrchestrationData
    {
        var token = AuditHandoffMetadata.Capture(scopes, "Orchestrations", context.InstanceId.ToString("D"));
        try { if (token is not null) context.Properties[AuditHandoffMetadata.PropertyName] = token; }
        catch (Exception) { /* Source snapshot persistence remains authoritative if optional enrichment fails. */ }
    }

    /// <summary>Joins awaited initial work and isolates resumed slices, ending before the next durable wait.</summary>
    /// <example><code>await adapter.RunAsync(context, () => ProcessAsync());</code></example>
    public Task<OrchestrationStatus> RunAsync<TData>(OrchestrationContext<TData> context, Func<Task> next) where TData : class, IOrchestrationData
    {
        var token = AuditHandoffMetadata.ReadToken(context.Properties.Get(AuditHandoffMetadata.PropertyName));
        var resumed = context.Status != OrchestrationStatus.Created;
        if (!resumed && token is not null)
        {
            try
            {
                using var services = scopes.CreateScope();
                resumed = services.ServiceProvider.GetService<IAuditContext>()?.Current is null;
            }
            catch (Exception) { resumed = true; }
        }

        async Task<OrchestrationStatus> Execute()
        {
            await next().ConfigureAwait(false);
            return context.Status;
        }

        void Configure(AuditScopeOptionsBuilder builder) => builder.Key("orchestration.execute").Kind("Orchestration");
        void Starting(IServiceProvider services, IAuditScope scope)
        {
            scope.SetDetail("orchestration.name", context.OrchestrationName.Length <= 512 ? AuditValue.From(context.OrchestrationName) : AuditValue.Policy("Oversized"));
            this.StampCause(context);
        }

        void Completed(IAuditScope scope, OrchestrationStatus status)
        {
            scope.Complete(status == OrchestrationStatus.Failed ? AuditOutcome.Failed : status == OrchestrationStatus.Cancelled ? AuditOutcome.Canceled : AuditOutcome.Succeeded);
            (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = status.ToString() });
        }

        var metadata = new AuditSourceMetadata { Feature = "Orchestrations", FlowId = context.InstanceId.ToString("D"), ExecutionStatus = "Invoked" };
        return resumed
            ? AuditSourceExecutor.RunWorkerAsync(scopes, "Orchestrations", context.InstanceId.ToString("D"), token, Configure, Execute, metadata, Starting, Completed, correlationId: context.CorrelationId)
            : AuditSourceExecutor.RunAsync(scopes, Configure, Execute, metadata, Starting, Completed);
    }
}
