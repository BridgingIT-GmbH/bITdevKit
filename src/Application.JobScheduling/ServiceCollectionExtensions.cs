// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Collections.Specialized;
using BridgingIT.DevKit.Application.JobScheduling;
using Configuration;
using Extensions;
using Microsoft.Extensions.Hosting;

public static class ServiceCollectionExtensions
{
    private static JobSchedulingOptions contextOptions;
    //private static bool schedulerIsAdded;

    /// <summary>
    ///     Adds the job scheduler
    /// </summary>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        Builder<JobSchedulingOptionsBuilder, JobSchedulingOptions> optionsBuilder,
        IConfiguration configuration = null,
        NameValueCollection properties = null)
    {
        return services.AddJobScheduling(optionsBuilder(new JobSchedulingOptionsBuilder()).Build(),
            null,
            configuration,
            properties);
    }

    /// <summary>
    ///     Adds the job scheduler
    /// </summary>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        Builder<JobSchedulingOptionsBuilder, JobSchedulingOptions> optionsBuilder,
        Action<IServiceCollectionQuartzConfigurator> configure,
        IConfiguration configuration = null,
        NameValueCollection properties = null)
    {
        return services.AddJobScheduling(optionsBuilder(new JobSchedulingOptionsBuilder()).Build(),
            configure,
            configuration,
            properties);
    }

    /// <summary>
    ///     Adds the job scheduler
    /// </summary>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        JobSchedulingOptions options = null,
        Action<IServiceCollectionQuartzConfigurator> configure = null,
        IConfiguration configuration = null,
        NameValueCollection properties = null)
    {
        contextOptions ??= options;

        properties ??= [];
        if (configuration != null)
        {
            var section = configuration.GetSection("JobScheduling:Quartz", false);
            services.Configure<QuartzOptions>(section);
        }

        services.TryAddSingleton<IJobFactory, ScopedJobFactory>();        // https://github.com/quartznet/quartznet/blob/main/src/Quartz/Configuration/ServiceCollectionExtensions.cs#L31
        services.AddQuartz(properties, configure);

        services.AddHostedService(sp =>
            new JobSchedulingService( // QuartzHostedService https://github.com/quartznet/quartznet/blob/main/src/Quartz/Hosting/QuartzHostedService.cs#L21
                sp.GetService<ILoggerFactory>(),
                sp.GetRequiredService<ISchedulerFactory>(),
                sp.GetRequiredService<IJobFactory>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetServices<JobSchedule>(),
                contextOptions));

        //schedulerIsAdded = true;
        //}

        return new JobSchedulingBuilderContext(services, null, contextOptions);
    }

    /// <summary>
    ///     Adds a scoped scheduled job
    /// </summary>
    /// <param name="context">The builder context</param>
    /// <param name="cronExpression">the cron expression: https://www.freeformatter.com/cron-expression-generator-quartz.html</param>
    public static JobSchedulingBuilderContext WithJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression,
        string name = null,
        Dictionary<string, string> data = null,
        bool enabled = true)
        where TJob : class, IJob
    {
        return context.WithScopedJob<TJob>(cronExpression, name, data, enabled);
    }

    /// <summary>
    ///     Adds a scoped scheduled job
    /// </summary>
    /// <param name="context">The builder context</param>
    /// <param name="cronExpression">the cron expression: https://www.freeformatter.com/cron-expression-generator-quartz.html</param>
    public static JobSchedulingBuilderContext WithScopedJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression,
        string name = null,
        Dictionary<string, string> data = null,
        bool enabled = true)
        where TJob : class, IJob
    {
        if (enabled)
        {
            context.Services.AddScoped<TJob>();
            context.Services.AddSingleton(new JobSchedule(typeof(TJob), cronExpression, name, data));
        }

        return context;
    }

    /// <summary>
    /// Adds a scoped or singleton scheduled job with fluent configuration.
    /// </summary>
    /// <example>
    /// services.AddJobScheduling()
    ///         .WithJob<EchoJob>()
    ///             .Cron(CronExpressions.Every10Seconds)
    ///             .WithData("message", "Second echo")
    ///             .RegisterScoped();
    /// </example>
    public static JobScheduleBuilder<TJob> WithJob<TJob>(this JobSchedulingBuilderContext context)
        where TJob : class, IJob
    {
        return new JobScheduleBuilder<TJob>(context.Services);
    }

    /// <summary>
    ///     Adds a singleton scheduled job
    /// </summary>
    /// <param name="context">The builder context</param>
    /// <param name="cronExpression">the cron expression: https://www.freeformatter.com/cron-expression-generator-quartz.html</param>
    public static JobSchedulingBuilderContext WithSingletonJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression,
        string name = null,
        Dictionary<string, string> data = null)
        where TJob : class, IJob
    {
        context.Services.AddSingleton<TJob>();
        context.Services.AddSingleton(new JobSchedule(typeof(TJob), cronExpression, name, data));

        return context;
    }

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