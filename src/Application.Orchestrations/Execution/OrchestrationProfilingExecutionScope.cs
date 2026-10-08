// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>Measures one bounded executor slice, closing before durable waiting or a later resume.</summary>
/// <example><code>services.AddOrchestrations().WithProfilingBehavior();</code></example>
public sealed class OrchestrationProfilingExecutionScope(IOperationProfiler profiling = null)
{
    /// <summary>Runs existing executor work once, joining or owning capture and retaining only bounded metadata.</summary>
    /// <example>Used by the executor around ProcessContext; a later resume starts another correlated slice.</example>
    public Task RunAsync<TData>(OrchestrationContext<TData> context, Func<Task> next, CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        if (profiling is null) { return next(); }

        return profiling.JoinOrStartAsync(new OperationProfilingStartRequest
        {
            Key = "orchestration:" + context.OrchestrationName, Kind = OperationProfilingKind.Orchestration.ToString(), CorrelationId = context.CorrelationId,
        }, async (scope, _) =>
        {
            Enrich(scope, context);
            await next().ConfigureAwait(false);
            try { if (scope.IsRecording) { scope.SetDimension("orchestration.status", context.Status.ToString()); } }
            catch (Exception) { /* Executor state is authoritative even if enrichment fails. */ }

            return context.Status;
        }, cancellationToken, status => new ProfilingResultClassification
        {
            Outcome = status == OrchestrationStatus.Cancelled ? OperationProfilingOutcome.Canceled
                : status == OrchestrationStatus.Failed ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed,
            Failure = status == OrchestrationStatus.Failed ? new ProfilingFailureDescriptor { Source = "Orchestration", Code = "Failed" } : null,
        });
    }

    /// <summary>Clears inherited operation ownership only at an independent scheduled worker entry.</summary>
    /// <example><code>using var boundary = adapter.BeginIndependentExecutionBoundary();</code></example>
    public IDisposable BeginIndependentExecutionBoundary()
    {
        return profiling.BeginSafeExecutionBoundary();
    }

    private static void Enrich<TData>(IProfilingRecordingScope scope, OrchestrationContext<TData> context)
        where TData : class, IOrchestrationData
    {
        try
        {
            if (!scope.IsRecording) { return; }

            scope.SetDimension("orchestration.instanceId", context.InstanceId.ToString("D"));
            scope.SetDimension("orchestration.correlationId", context.CorrelationId);
        }
        catch (Exception) { /* No services, context, data or reason messages are retained. */ }
    }
}
