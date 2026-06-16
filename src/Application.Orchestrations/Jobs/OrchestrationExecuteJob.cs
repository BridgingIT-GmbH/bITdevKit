// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Executes or dispatches an orchestration from a scheduled job occurrence.
/// </summary>
/// <typeparam name="TData">The job data type.</typeparam>
/// <typeparam name="TOrchestration">The orchestration type.</typeparam>
/// <typeparam name="TOrchestrationData">The orchestration data type.</typeparam>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithOrchestrationExecuteJob&lt;Unit, MyOrchestration, MyData&gt;("my-job", job => job.Dispatch());
/// </code>
/// </example>
public sealed class OrchestrationExecuteJob<TData, TOrchestration, TOrchestrationData> : IJob<TData>
    where TOrchestration : class, IOrchestration<TOrchestrationData>
    where TOrchestrationData : class, IOrchestrationData
{
    private readonly IServiceProvider serviceProvider;
    private readonly OrchestrationExecuteJobRegistrationStore registrations;

    internal OrchestrationExecuteJob(IServiceProvider serviceProvider, OrchestrationExecuteJobRegistrationStore registrations)
    {
        this.serviceProvider = serviceProvider;
        this.registrations = registrations;
    }

    /// <summary>
    /// Executes the orchestration integration for the current job occurrence.
    /// </summary>
    /// <param name="context">The current job execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The integration result.</returns>
    public async Task<IResult> ExecuteAsync(IJobExecutionContext<TData> context, CancellationToken cancellationToken = default)
    {
        var settings = this.registrations.Get<TData, TOrchestration, TOrchestrationData>(context.JobName);
        if (settings is null)
        {
            return JobIntegrationResult.Failure($"No Orchestration integration is registered for job '{context.JobName}'.");
        }

        var service = this.serviceProvider.GetService<IOrchestrationService>();
        if (service is null)
        {
            return JobIntegrationResult.Failure($"IOrchestrationService is not registered for job '{context.JobName}'.");
        }

        var data = settings.DataFactory(context);
        if (data is null)
        {
            return JobIntegrationResult.Failure($"The Orchestration job '{context.JobName}' produced a null orchestration payload.");
        }

        if (settings.Dispatch)
        {
            var result = await service.DispatchAsync<TOrchestration, TOrchestrationData>(data, cancellationToken, context.CorrelationId).ConfigureAwait(false);
            return JobIntegrationResult.From(result);
        }

        var execute = await service.ExecuteAsync<TOrchestration, TOrchestrationData>(data, cancellationToken, context.CorrelationId).ConfigureAwait(false);
        return JobIntegrationResult.From(execute);
    }

    async Task<IResult> IJob.ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        if (context is not IJobExecutionContext<TData> typedContext)
        {
            return JobIntegrationResult.Failure($"The Orchestration job '{context.JobName}' expected data contract '{typeof(TData).FullName}'.");
        }

        return await this.ExecuteAsync(typedContext, cancellationToken).ConfigureAwait(false);
    }
}
