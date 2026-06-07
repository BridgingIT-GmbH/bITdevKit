// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides dependency injection registration helpers for jobs.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the jobs foundation services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The optional scheduler configuration section.</param>
    /// <returns>The jobs builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithJob&lt;CleanupJob&gt;("cleanup", job =&gt; job
    ///         .WithDescription("Removes stale records.")
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext AddJobScheduler(
        this IServiceCollection services,
        IConfiguration configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<ISerializer, SystemTextJsonSerializer>();
        services.TryAddSingleton<IJobCronEngine, CronosJobCronEngine>();
        services.TryAddSingleton<IJobCalendarEngine, DefaultJobCalendarEngine>();
        services.TryAddSingleton<IJobTriggerEvaluator, JobTriggerEvaluator>();
        services.TryAddSingleton<IJobStoreProvider, InMemoryJobStoreProvider>();
        services.TryAddSingleton<InlineJobHandlerRegistry>();
        services.TryAddSingleton<JobSchedulerHostedOptions>();
        EnsureEventSourceRegistry(services).Register(JobEventSourceNames.Notifier);
        services.TryAddSingleton<IJobEventIngress, JobEventIngress>();
        services.TryAddSingleton<JobSchedulerService>();
        services.TryAddSingleton<IJobSchedulerMaintenanceService, JobSchedulerMaintenanceService>();
        services.TryAddSingleton<IJobSchedulerQueryService, JobSchedulerQueryService>();
        services.TryAddSingleton<IJobSchedulerService>(sp => sp.GetRequiredService<JobSchedulerService>());
        services.TryAddSingleton<JobSchedulerBackgroundService>();

        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
        {
            services.AddHostedService(sp => sp.GetRequiredService<JobSchedulerBackgroundService>());
            services.TryAddBackgroundServiceHealthCheck<JobSchedulerBackgroundService>(
                nameof(JobSchedulerBackgroundService),
                tags: ["background", "jobs"]);
        }

        var registrations = EnsureRegistrationStore(services);
        registrations.SetConfiguration(configuration);
        registrations.AddGlobalBehavior(typeof(JobMetricsBehavior));

        return new JobBuilderContext(services, registrations)
            .AliveEnabled();
    }

    /// <summary>
    /// Registers a code-first job definition.
    /// </summary>
    /// <typeparam name="TJob">The job type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    public static JobBuilderContext WithJob<TJob>(
        this JobBuilderContext context,
        string jobName,
        Action<JobDefinitionBuilder<TJob>> configure)
        where TJob : class, IJob
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobDefinitionBuilder<TJob>(jobName);
        configure?.Invoke(builder);
        var definition = builder.Build();
        context.Services.Add(new ServiceDescriptor(typeof(TJob), typeof(TJob), definition.Lifetime));
        context.Registrations.Add(definition);

        return context;
    }

    /// <summary>
    /// Registers a lightweight inline job definition backed by a delegate.
    /// </summary>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The inline job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithJob("cleanup-inline", job =&gt; job
    ///         .WithDescription("Runs inline cleanup logic.")
    ///         .Execute((context, cancellationToken) =&gt; Task.FromResult(Result.Success()))
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext WithJob(
        this JobBuilderContext context,
        string jobName,
        Action<InlineJobDefinitionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new InlineJobDefinitionBuilder(jobName);
        configure?.Invoke(builder);
        context.Services.AddTransient<InlineJobRuntime>();
        context.Registrations.Add(builder.Build());

        EnsureInlineJobHandlerRegistry(context.Services).Register(jobName, builder.GetHandler());
        return context;
    }

    /// <summary>
    /// Replaces the active store provider with the default in-memory implementation.
    /// </summary>
    /// <param name="context">The jobs builder context.</param>
    /// <returns>The current builder context.</returns>
    public static JobBuilderContext WithInMemoryStore(this JobBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.Replace(ServiceDescriptor.Singleton<IJobStoreProvider, InMemoryJobStoreProvider>());
        return context;
    }

    /// <summary>
    /// Registers a global job behavior.
    /// </summary>
    public static JobBuilderContext WithBehavior<TBehavior>(this JobBuilderContext context)
        where TBehavior : class, IJobBehavior
        => context.WithBehavior(typeof(TBehavior));

    /// <summary>
    /// Registers a global job behavior.
    /// </summary>
    public static JobBuilderContext WithBehavior(this JobBuilderContext context, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Registrations.AddGlobalBehavior(behaviorType);
        return context;
    }

    /// <summary>
    /// Registers a scheduler exception handler.
    /// </summary>
    public static JobBuilderContext WithExceptionHandler<THandler>(this JobBuilderContext context)
        where THandler : class, IJobSchedulerExceptionHandler
        => context.WithExceptionHandler(typeof(THandler));

    /// <summary>
    /// Registers a scheduler exception handler.
    /// </summary>
    public static JobBuilderContext WithExceptionHandler(this JobBuilderContext context, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (handlerType is null)
        {
            throw new ArgumentNullException(nameof(handlerType));
        }

        if (!typeof(IJobSchedulerExceptionHandler).IsAssignableFrom(handlerType) || handlerType.IsAbstract)
        {
            throw new InvalidOperationException($"The exception handler type '{handlerType.FullName}' must implement {nameof(IJobSchedulerExceptionHandler)}.");
        }

        context.Services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IJobSchedulerExceptionHandler), handlerType));
        return context;
    }

    /// <summary>
    /// Configures hosted background scheduler execution options.
    /// </summary>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="configure">The hosted execution configuration callback.</param>
    /// <returns>The current builder context.</returns>
    public static JobBuilderContext WithBackgroundExecution(
        this JobBuilderContext context,
        Action<JobSchedulerHostedOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = EnsureHostedOptions(context.Services);
        configure?.Invoke(options);
        return context;
    }

    /// <summary>
    /// Configures the scheduler instance identifier explicitly.
    /// </summary>
    public static JobBuilderContext InstanceId(this JobBuilderContext context, string value)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("The scheduler instance identifier requires a non-empty value.");
        }

        var options = EnsureHostedOptions(context.Services);
        options.SchedulerInstanceId = value.Trim();
        options.SchedulerInstanceIdFactory = null;
        return context;
    }

    /// <summary>
    /// Configures the scheduler instance identifier through a callback.
    /// </summary>
    public static JobBuilderContext InstanceId(this JobBuilderContext context, Func<JobSchedulerInstanceIdContext, string> factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(factory);

        var options = EnsureHostedOptions(context.Services);
        options.SchedulerInstanceId = null;
        options.SchedulerInstanceIdFactory = factory;
        return context;
    }

    /// <summary>
    /// Configures the scheduler startup delay.
    /// </summary>
    public static JobBuilderContext StartupDelay(this JobBuilderContext context, TimeSpan value)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (value < TimeSpan.Zero)
        {
            throw new InvalidOperationException("The scheduler startup delay requires a non-negative value.");
        }

        EnsureHostedOptions(context.Services).StartupDelay = value;
        return context;
    }

    /// <summary>
    /// Configures the scheduler worker pool.
    /// </summary>
    public static JobBuilderContext WorkerPool(this JobBuilderContext context, Action<JobSchedulerWorkerPoolBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobSchedulerWorkerPoolBuilder(EnsureHostedOptions(context.Services));
        configure?.Invoke(builder);
        return context;
    }

    private static JobRegistrationStore EnsureRegistrationStore(IServiceCollection services)
    {
        var holderDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(JobRegistrationStoreHolder));
        if (holderDescriptor?.ImplementationInstance is JobRegistrationStoreHolder holder)
        {
            return holder.Value;
        }

        var registrations = new JobRegistrationStore();
        services.AddSingleton(new JobRegistrationStoreHolder(registrations));
        services.AddSingleton(sp =>
        {
            registrations.SetServiceProvider(sp);
            return registrations;
        });

        return registrations;
    }

    private static JobSchedulerHostedOptions EnsureHostedOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(JobSchedulerHostedOptions));
        if (descriptor?.ImplementationInstance is JobSchedulerHostedOptions options)
        {
            return options;
        }

        options = new JobSchedulerHostedOptions();
        services.Replace(ServiceDescriptor.Singleton(options));
        return options;
    }

    private static JobEventSourceRegistry EnsureEventSourceRegistry(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(JobEventSourceRegistry));
        if (descriptor?.ImplementationInstance is JobEventSourceRegistry registry)
        {
            return registry;
        }

        registry = new JobEventSourceRegistry();
        services.Replace(ServiceDescriptor.Singleton(registry));
        return registry;
    }

    private static InlineJobHandlerRegistry EnsureInlineJobHandlerRegistry(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(InlineJobHandlerRegistry));
        if (descriptor?.ImplementationInstance is InlineJobHandlerRegistry registry)
        {
            return registry;
        }

        registry = new InlineJobHandlerRegistry();
        services.Replace(ServiceDescriptor.Singleton(registry));
        return registry;
    }

    private sealed class JobRegistrationStoreHolder(JobRegistrationStore value)
    {
        public JobRegistrationStore Value { get; } = value;
    }
}
