// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Collections.Specialized;
using BridgingIT.DevKit.Application;
using BridgingIT.DevKit.Application.JobScheduling;
using Configuration;
using Extensions;
using Quartz.Impl;

public static class ServiceCollectionExtensions
{
    private static JobSchedulingOptions contextOptions;
    //private static bool schedulerIsAdded;

    /// <summary>
    /// Adds the Quartz.NET-based job scheduler to the service collection with custom options and configuration.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the scheduler to.</param>
    /// <param name="optionsBuilder">A builder delegate to configure JobSchedulingOptions (e.g., startup delay).</param>
    /// <param name="configuration">Optional IConfiguration instance to load Quartz settings from (e.g., appsettings.json).</param>
    /// <param name="properties">Optional NameValueCollection for additional Quartz properties.</param>
    /// <returns>A JobSchedulingBuilderContext for further job configuration.</returns>
    /// <example>
    /// // Basic setup with configuration from appsettings.json
    /// services.AddJobScheduling(o => o.StartupDelay(5000), builder.Configuration);
    /// </example>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        Builder<JobSchedulingOptionsBuilder, JobSchedulingOptions> optionsBuilder,
        IConfiguration configuration = null,
        NameValueCollection properties = null)
    {
        return services.AddJobScheduling(optionsBuilder(
            new JobSchedulingOptionsBuilder()).Build(),
            null,
            configuration,
            properties);
    }

    /// <summary>
    /// Adds the Quartz.NET-based job scheduler with custom options, Quartz configuration, and app configuration.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the scheduler to.</param>
    /// <param name="optionsBuilder">A builder delegate to configure JobSchedulingOptions.</param>
    /// <param name="configure">Optional action to configure Quartz-specific settings (e.g., thread pool size).</param>
    /// <param name="configuration">Optional IConfiguration instance to load Quartz settings from.</param>
    /// <param name="properties">Optional NameValueCollection for additional Quartz properties.</param>
    /// <returns>A JobSchedulingBuilderContext for further job configuration.</returns>
    /// <example>
    /// // Setup with custom Quartz configuration
    /// services.AddJobScheduling(
    ///     o => o.StartupDelay(3000),
    ///     q => q.UseThreadPool(t => t.MaxConcurrency = 10),
    ///     builder.Configuration);
    /// </example>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        Builder<JobSchedulingOptionsBuilder, JobSchedulingOptions> optionsBuilder,
        Action<StdSchedulerFactory> configure,
        IConfiguration configuration = null,
        NameValueCollection properties = null)
    {
        return services.AddJobScheduling(
            optionsBuilder(new JobSchedulingOptionsBuilder()).Build(),
            configure,
            configuration,
            properties);
    }

    /// <summary>
    /// Adds the Quartz.NET-based job scheduler with detailed configuration options.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the scheduler to.</param>
    /// <param name="options">Optional JobSchedulingOptions instance (e.g., for startup delay).</param>
    /// <param name="configure">Optional action to configure Quartz-specific settings.</param>
    /// <param name="configuration">Optional IConfiguration instance to load Quartz settings from (e.g., persistence settings).</param>
    /// <param name="properties">Optional NameValueCollection for additional Quartz properties.</param>
    /// <returns>A JobSchedulingBuilderContext for further job configuration.</returns>
    /// <example>
    /// // Comprehensive setup with persistence and jobs
    /// services.AddJobScheduling(
    ///     new JobSchedulingOptions { StartupDelay = TimeSpan.FromSeconds(5) },
    ///     null,
    ///     builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0 * * * * ?")
    ///         .Named("echo")
    ///         .WithData("message", "Hello")
    ///         .RegisterScoped();
    /// </example>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        JobSchedulingOptions options = null,
        Action<StdSchedulerFactory> configure = null,
        IConfiguration configuration = null,
        NameValueCollection properties = null)
    {
        contextOptions ??= options ?? new JobSchedulingOptions();

        properties ??= [];
        if (configuration != null)
        {
            var section = configuration.GetSection("JobScheduling:Quartz");
            services.Configure<QuartzOptions>(section);
            foreach (var key in section.GetChildren().Select(c => c.Key))
            {
                var value = section[key];
                if (!string.IsNullOrEmpty(value))
                {
                    properties[key] = value;
                }
            }
        }

        // Register Quartz dependencies
        services.TryAddSingleton<IJobFactory, ScopedJobFactory>();
        services.AddSingleton<ISchedulerFactory>(sp =>
        {
            var factory = new StdSchedulerFactory(properties);
            configure?.Invoke(factory);
            return factory;
        });

        services.AddSingleton<IJobStoreProvider, NullJobStoreProvider>();
        services.AddSingleton<IJobService>(sp => new JobService(
            sp.GetService<ILoggerFactory>(),
            sp.GetRequiredService<ISchedulerFactory>(),
            sp.GetRequiredService<IJobStoreProvider>()));

        if (options.GroupOptions != null)
        {
            services.AddSingleton(options.GroupOptions);
            services.AddSingleton<ConcurrentGroupExecutionListener>(); // scheduler.ListenerManager.AddJobListener called in JobSchedulingService
        }

        services.AddSingleton<JobRunHistoryListener>(); // scheduler.ListenerManager.AddJobListener called in JobSchedulingService
        services.TryAddSingleton(contextOptions);

        services.AddHostedService<JobSchedulingService>();

        return new JobSchedulingBuilderContext(services, null, contextOptions);
    }

    /// <summary>
    /// Configures the job scheduling to use in-memory persistence with an optional retention period.
    /// </summary>
    /// <param name="context">The job scheduling builder context from AddJobScheduling.</param>
    /// <param name="retentionPeriod">The duration to retain job run history (default: 1 hour).</param>
    /// <returns>The updated job scheduling builder context.</returns>
    public static JobSchedulingBuilderContext WithInMemoryStore(
        this JobSchedulingBuilderContext context,
        TimeSpan? retentionPeriod = null)
    {
        context.Services.AddSingleton<IJobStoreProvider>(sp => new InMemoryJobStoreProvider(
            sp.GetService<ILoggerFactory>(),
            retentionPeriod));
        context.Services.AddSingleton<IJobService>(sp => new JobService(
            sp.GetService<ILoggerFactory>(),
            sp.GetRequiredService<ISchedulerFactory>(),
            sp.GetRequiredService<IJobStoreProvider>()));

        return context;
    }

    /// <summary>
    /// Adds a scoped scheduled job with a cron expression and optional metadata.
    /// </summary>
    /// <typeparam name="TJob">The job type implementing IJob.</typeparam>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <param name="cronExpression">Cron expression defining the schedule (see https://www.freeformatter.com/cron-expression-generator-quartz.html).</param>
    /// <param name="name">Optional unique name for the job (defaults to type name if null).</param>
    /// <param name="data">Optional dictionary of key-value pairs to pass to the job.</param>
    /// <param name="enabled">Whether the job is enabled (default: true).</param>
    /// <returns>The JobSchedulingBuilderContext for chaining additional configurations.</returns>
    /// <example>
    /// // Add a job running every minute
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>("0 * * * * ?", "minuteEcho", new Dictionary<string, string> { { "msg", "Tick" } });
    /// </example>
    public static JobSchedulingBuilderContext WithJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression,
        string name = null,
        string group = "DEFAULT",
        Dictionary<string, string> data = null,
        bool enabled = true)
        where TJob : class, IJob
    {
        return context.WithScopedJob<TJob>(cronExpression, name, group, data, enabled);
    }

    /// <summary>
    /// Adds a scoped scheduled job with a cron expression and optional metadata.
    /// </summary>
    /// <typeparam name="TJob">The job type implementing IJob.</typeparam>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <param name="cronExpression">Cron expression defining the schedule (see https://www.freeformatter.com/cron-expression-generator-quartz.html).</param>
    /// <param name="name">Optional unique name for the job (defaults to type name if null).</param>
    /// <param name="data">Optional dictionary of key-value pairs to pass to the job.</param>
    /// <param name="enabled">Whether the job is enabled (default: true).</param>
    /// <returns>The JobSchedulingBuilderContext for chaining additional configurations.</returns>
    /// <example>
    /// // Add a scoped job running every 5 seconds
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithScopedJob<MyJob>("0/5 * * * * ?", "quickJob", new Dictionary<string, string> { { "key", "value" } });
    /// </example>
    public static JobSchedulingBuilderContext WithScopedJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression,
        string name = null,
        string group = "DEFAULT",
        Dictionary<string, string> data = null,
        bool enabled = true)
        where TJob : class, IJob
    {
        if (enabled)
        {
            context.Services.AddScoped<TJob>();
            context.Services.AddSingleton(new JobSchedule(typeof(TJob), cronExpression, name, group, data));
        }

        return context;
    }

    /// <summary>
    /// Adds a scoped or singleton scheduled job with fluent configuration options.
    /// </summary>
    /// <typeparam name="TJob">The job type implementing IJob.</typeparam>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <returns>A JobScheduleBuilder for fluent job configuration.</returns>
    /// <example>
    /// // Fluent job configuration
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0/10 * * * * ?") // Every 10 seconds
    ///         .Named("echo")
    ///         .WithData("message", "Hello")
    ///         .Enabled(true)
    ///         .RegisterScoped();
    /// </example>
    public static JobScheduleBuilder<TJob> WithJob<TJob>(this JobSchedulingBuilderContext context)
        where TJob : class, IJob
    {
        return new JobScheduleBuilder<TJob>(context.Services);
    }

    /// <summary>
    /// Adds a singleton scheduled job with a cron expression and optional metadata.
    /// </summary>
    /// <typeparam name="TJob">The job type implementing IJob.</typeparam>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <param name="cronExpression">Cron expression defining the schedule (see https://www.freeformatter.com/cron-expression-generator-quartz.html).</param>
    /// <param name="name">Optional unique name for the job (defaults to type name if null).</param>
    /// <param name="data">Optional dictionary of key-value pairs to pass to the job.</param>
    /// <returns>The JobSchedulingBuilderContext for chaining additional configurations.</returns>
    /// <example>
    /// // Add a singleton job running every hour
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithSingletonJob<MonitorJob>("0 0 * * * ?", "hourlyMonitor");
    /// </example>
    public static JobSchedulingBuilderContext WithSingletonJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression,
        string name = null,
        string group = "DEFAULT",
        Dictionary<string, string> data = null)
        where TJob : class, IJob
    {
        context.Services.AddSingleton<TJob>();
        context.Services.AddSingleton(new JobSchedule(typeof(TJob), cronExpression, name, group, data));

        return context;
    }

    /// <summary>
    /// Adds a behavior to modify job execution (e.g., logging, retry logic) by registering a behavior type.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type implementing IJobSchedulingBehavior.</typeparam>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <param name="behavior">Optional instance of the behavior; if null, resolved via DI.</param>
    /// <returns>The JobSchedulingBuilderContext for chaining additional configurations.</returns>
    /// <example>
    /// // Add a logging behavior
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>("0 * * * * ?")
    ///     .WithBehavior<LoggingBehavior>();
    /// </example>
    public static JobSchedulingBuilderContext WithBehavior<TBehavior>(
        this JobSchedulingBuilderContext context,
        IJobSchedulingBehavior behavior = null)
        where TBehavior : class, IJobSchedulingBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IJobSchedulingBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IJobSchedulingBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Adds a behavior to modify job execution using a factory method.
    /// </summary>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <param name="implementationFactory">Factory method to create the behavior instance.</param>
    /// <returns>The JobSchedulingBuilderContext for chaining additional configurations.</returns>
    /// <example>
    /// // Add a custom retry behavior via factory
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>("0 * * * * ?")
    ///     .WithBehavior(sp => new RetryBehavior(sp.GetService<ILoggerFactory>()));
    /// </example>
    public static JobSchedulingBuilderContext WithBehavior(
        this JobSchedulingBuilderContext context,
        Func<IServiceProvider, IJobSchedulingBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IJobSchedulingBehavior), implementationFactory);
        }

        return context;
    }

    /// <summary>
    /// Adds a behavior to modify job execution using a pre-instantiated behavior instance.
    /// </summary>
    /// <param name="context">The JobSchedulingBuilderContext from AddJobScheduling.</param>
    /// <param name="behavior">The behavior instance implementing IJobSchedulingBehavior.</param>
    /// <returns>The JobSchedulingBuilderContext for chaining additional configurations.</returns>
    /// <example>
    /// // Add a pre-instantiated behavior
    /// var retryBehavior = new RetryBehavior(loggerFactory);
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>("0 * * * * ?")
    ///     .WithBehavior(retryBehavior);
    /// </example>
    public static JobSchedulingBuilderContext WithBehavior(
        this JobSchedulingBuilderContext context,
        IJobSchedulingBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IJobSchedulingBehavior), behavior);
        }

        return context;
    }
}