// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides Pipeline-backed outbound integration registration helpers for jobs.
/// </summary>
public static class JobSchedulerPipelineIntegrationExtensions
{
    /// <summary>
    /// Registers a Pipeline-backed outbound job.
    /// </summary>
    /// <typeparam name="TData">The typed job data contract.</typeparam>
    /// <typeparam name="TPipelineDefinition">The pipeline definition source type.</typeparam>
    /// <typeparam name="TPipelineContext">The pipeline context type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The outbound job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    public static JobBuilderContext WithPipelineExecuteJob<TData, TPipelineDefinition, TPipelineContext>(
        this JobBuilderContext context,
        string jobName,
        Action<JobPipelineExecuteDefinitionBuilder<TData, TPipelineDefinition, TPipelineContext>> configure)
        where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
        where TPipelineContext : PipelineContextBase
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobPipelineExecuteDefinitionBuilder<TData, TPipelineDefinition, TPipelineContext>(jobName);
        configure?.Invoke(builder);

        EnsurePipelineRegistrations(context.Services).Add(jobName, builder.BuildSettings());
        context.Services.AddTransient<PipelineExecuteJob<TData, TPipelineDefinition, TPipelineContext>>(sp =>
            new PipelineExecuteJob<TData, TPipelineDefinition, TPipelineContext>(
                sp,
                sp.GetRequiredService<PipelineExecuteJobRegistrationStore>()));
        context.Registrations.Add(builder.BuildDefinition());

        return context;
    }

    private static PipelineExecuteJobRegistrationStore EnsurePipelineRegistrations(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(PipelineExecuteJobRegistrationStore));
        if (descriptor?.ImplementationInstance is PipelineExecuteJobRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new PipelineExecuteJobRegistrationStore();
        services.AddSingleton(registrations);
        return registrations;
    }
}