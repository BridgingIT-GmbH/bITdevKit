// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Executes a Pipeline-backed outbound job registered through <see cref="Microsoft.Extensions.DependencyInjection.JobSchedulerPipelineIntegrationExtensions.WithPipelineExecuteJob{TData, TPipelineDefinition, TPipelineContext}(Microsoft.Extensions.DependencyInjection.JobBuilderContext, string, Action{JobPipelineExecuteDefinitionBuilder{TData, TPipelineDefinition, TPipelineContext}})"/>.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <typeparam name="TPipelineDefinition">The pipeline definition source type.</typeparam>
/// <typeparam name="TPipelineContext">The pipeline context type.</typeparam>
public sealed class PipelineExecuteJob<TData, TPipelineDefinition, TPipelineContext> : JobBase<TData>
    where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
    where TPipelineContext : PipelineContextBase
{
    private readonly IServiceProvider serviceProvider;
    private readonly PipelineExecuteJobRegistrationStore registrations;

    internal PipelineExecuteJob(
        IServiceProvider serviceProvider,
        PipelineExecuteJobRegistrationStore registrations)
    {
        this.serviceProvider = serviceProvider;
        this.registrations = registrations;
    }

    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = this.registrations.Get<TData, TPipelineDefinition, TPipelineContext>(context.JobName);
        if (settings is null)
        {
            return Result.Failure().WithError(new ValidationError($"No Pipeline integration is registered for job '{context.JobName}'."));
        }

        var factory = this.serviceProvider.GetService<IPipelineFactory>();
        if (factory is null)
        {
            return Result.Failure().WithError(new ValidationError($"IPipelineFactory is not registered for job '{context.JobName}'."));
        }

        var pipelineContext = settings.ContextFactory(context);
        if (pipelineContext is null)
        {
            return Result.Failure().WithError(new ValidationError($"The Pipeline job '{context.JobName}' produced a null pipeline context."));
        }

        var options = new PipelineExecutionOptions();
        settings.OptionsConfigurator?.Invoke(context, options);

        var pipeline = factory.Create<TPipelineDefinition, TPipelineContext>();
        var result = await pipeline.ExecuteAsync(pipelineContext, options, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result.Success().WithMessages(result.Messages)
            : Result.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
    }
}

internal sealed class PipelineExecuteJobSettings<TData, TPipelineDefinition, TPipelineContext>
    where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
    where TPipelineContext : PipelineContextBase
{
    public required Func<IJobExecutionContext<TData>, TPipelineContext> ContextFactory { get; init; }

    public Action<IJobExecutionContext<TData>, PipelineExecutionOptions> OptionsConfigurator { get; init; }
}

internal sealed class PipelineExecuteJobRegistrationStore
{
    private readonly Dictionary<string, object> registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Add<TData, TPipelineDefinition, TPipelineContext>(string jobName, PipelineExecuteJobSettings<TData, TPipelineDefinition, TPipelineContext> settings)
        where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
        where TPipelineContext : PipelineContextBase
    {
        this.registrations[jobName] = settings;
    }

    public PipelineExecuteJobSettings<TData, TPipelineDefinition, TPipelineContext> Get<TData, TPipelineDefinition, TPipelineContext>(string jobName)
        where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
        where TPipelineContext : PipelineContextBase
    {
        return this.registrations.TryGetValue(jobName, out var settings)
            ? settings as PipelineExecuteJobSettings<TData, TPipelineDefinition, TPipelineContext>
            : null;
    }
}

/// <summary>
/// Builds a Pipeline-backed outbound job definition.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <typeparam name="TPipelineDefinition">The pipeline definition source type.</typeparam>
/// <typeparam name="TPipelineContext">The pipeline context type.</typeparam>
public sealed class JobPipelineExecuteDefinitionBuilder<TData, TPipelineDefinition, TPipelineContext>
    : JobOutboundIntegrationDefinitionBuilderBase<JobPipelineExecuteDefinitionBuilder<TData, TPipelineDefinition, TPipelineContext>, PipelineExecuteJob<TData, TPipelineDefinition, TPipelineContext>, TData>
    where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
    where TPipelineContext : PipelineContextBase
{
    private Func<IJobExecutionContext<TData>, TPipelineContext> contextFactory;
    private Action<IJobExecutionContext<TData>, PipelineExecutionOptions> optionsConfigurator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPipelineExecuteDefinitionBuilder{TData, TPipelineDefinition, TPipelineContext}"/> class.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    public JobPipelineExecuteDefinitionBuilder(string jobName)
        : base(jobName)
    {
    }

    /// <summary>
    /// Configures the pipeline context factory.
    /// </summary>
    public JobPipelineExecuteDefinitionBuilder<TData, TPipelineDefinition, TPipelineContext> WithContext(Func<IJobExecutionContext<TData>, TPipelineContext> factory)
    {
        this.contextFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>
    /// Configures execution options for each pipeline invocation.
    /// </summary>
    public JobPipelineExecuteDefinitionBuilder<TData, TPipelineDefinition, TPipelineContext> ConfigureExecutionOptions(Action<IJobExecutionContext<TData>, PipelineExecutionOptions> configure)
    {
        this.optionsConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    internal PipelineExecuteJobSettings<TData, TPipelineDefinition, TPipelineContext> BuildSettings()
    {
        if (this.contextFactory is null)
        {
            throw new InvalidOperationException($"The Pipeline job '{this.JobName}' requires a configured pipeline context factory.");
        }

        return new PipelineExecuteJobSettings<TData, TPipelineDefinition, TPipelineContext>
        {
            ContextFactory = this.contextFactory,
            OptionsConfigurator = this.optionsConfigurator,
        };
    }
}