// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Orchestration-backed outbound jobs.
/// </summary>
public static class JobSchedulerOrchestrationIntegrationExtensions
{
    /// <summary>
    /// Represents with orchestration execute job.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TOrchestrationData">The orchestration data type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="configure">The delegate used to configure the component.</param>
    public static JobBuilderContext WithOrchestrationExecuteJob<TData, TOrchestration, TOrchestrationData>(
        this JobBuilderContext context,
        string jobName,
        Action<JobOrchestrationExecuteDefinitionBuilder<TData, TOrchestration, TOrchestrationData>> configure)
        where TOrchestration : class, IOrchestration<TOrchestrationData>
        where TOrchestrationData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobOrchestrationExecuteDefinitionBuilder<TData, TOrchestration, TOrchestrationData>(jobName);
        configure?.Invoke(builder);

        EnsureRegistrations(context.Services).Add(jobName, builder.BuildSettings());
        context.Services.AddTransient<OrchestrationExecuteJob<TData, TOrchestration, TOrchestrationData>>(sp =>
            new OrchestrationExecuteJob<TData, TOrchestration, TOrchestrationData>(sp, sp.GetRequiredService<OrchestrationExecuteJobRegistrationStore>()));
        context.Registrations.Add(builder.BuildDefinition());
        return context;
    }

    private static OrchestrationExecuteJobRegistrationStore EnsureRegistrations(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(OrchestrationExecuteJobRegistrationStore));
        if (descriptor?.ImplementationInstance is OrchestrationExecuteJobRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new OrchestrationExecuteJobRegistrationStore();
        services.AddSingleton(registrations);
        return registrations;
    }
}

internal sealed class OrchestrationExecuteJobSettings<TData, TOrchestration, TOrchestrationData>
    where TOrchestration : class, IOrchestration<TOrchestrationData>
    where TOrchestrationData : class, IOrchestrationData
{
    public required Func<IJobExecutionContext<TData>, TOrchestrationData> DataFactory { get; init; }

    public bool Dispatch { get; init; }
}

internal sealed class OrchestrationExecuteJobRegistrationStore
{
    private readonly Dictionary<string, object> registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Add<TData, TOrchestration, TOrchestrationData>(string jobName, OrchestrationExecuteJobSettings<TData, TOrchestration, TOrchestrationData> settings)
        where TOrchestration : class, IOrchestration<TOrchestrationData>
        where TOrchestrationData : class, IOrchestrationData
    {
        this.registrations[jobName] = settings;
    }

    public OrchestrationExecuteJobSettings<TData, TOrchestration, TOrchestrationData> Get<TData, TOrchestration, TOrchestrationData>(string jobName)
        where TOrchestration : class, IOrchestration<TOrchestrationData>
        where TOrchestrationData : class, IOrchestrationData
    {
        return this.registrations.TryGetValue(jobName, out var settings)
            ? settings as OrchestrationExecuteJobSettings<TData, TOrchestration, TOrchestrationData>
            : null;
    }
}

/// <summary>
/// Builds an orchestration execution job definition.
/// </summary>
public sealed class JobOrchestrationExecuteDefinitionBuilder<TData, TOrchestration, TOrchestrationData>
    : JobOutboundIntegrationDefinitionBuilderBase<JobOrchestrationExecuteDefinitionBuilder<TData, TOrchestration, TOrchestrationData>, OrchestrationExecuteJob<TData, TOrchestration, TOrchestrationData>, TData>
    where TOrchestration : class, IOrchestration<TOrchestrationData>
    where TOrchestrationData : class, IOrchestrationData
{
    private Func<IJobExecutionContext<TData>, TOrchestrationData> dataFactory;
    private bool dispatch;

    /// <summary>
    /// Initializes a new instance of the <c>JobOrchestrationExecuteDefinitionBuilder</c> class.
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    public JobOrchestrationExecuteDefinitionBuilder(string jobName)
        : base(jobName)
    {
    }

    /// <summary>
    /// Executes the with input operation.
    /// </summary>
    /// <param name="factory">The factory used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public JobOrchestrationExecuteDefinitionBuilder<TData, TOrchestration, TOrchestrationData> WithInput(Func<IJobExecutionContext<TData>, TOrchestrationData> factory)
    {
        this.dataFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>
    /// Executes the dispatch operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public JobOrchestrationExecuteDefinitionBuilder<TData, TOrchestration, TOrchestrationData> Dispatch(bool value = true)
    {
        this.dispatch = value;
        return this;
    }

    internal OrchestrationExecuteJobSettings<TData, TOrchestration, TOrchestrationData> BuildSettings()
    {
        if (this.dataFactory is null)
        {
            throw new InvalidOperationException($"The Orchestration job '{this.JobName}' requires a configured orchestration input factory.");
        }

        return new OrchestrationExecuteJobSettings<TData, TOrchestration, TOrchestrationData>
        {
            DataFactory = this.dataFactory,
            Dispatch = this.dispatch,
        };
    }
}
