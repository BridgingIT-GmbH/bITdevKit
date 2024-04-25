// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

public static class ServiceCollectionExtensions
{
    private static JobSchedulingOptions contextOptions;

    /// <summary>
    /// Adds the job scheduler
    /// </summary>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        Builder<JobSchedulingOptionsBuilder, JobSchedulingOptions> optionsBuilder)
    {
        return services.AddJobScheduling(
            optionsBuilder(new JobSchedulingOptionsBuilder()).Build());
    }

    /// <summary>
    /// Adds the job scheduler
    /// </summary>
    public static JobSchedulingBuilderContext AddJobScheduling(
        this IServiceCollection services,
        JobSchedulingOptions options = null)
    {
        contextOptions ??= options;

        // TODO: modernize quartz setup (example: https://github.com/quartznet/quartznet/tree/main/src/Quartz.Examples.AspNetCore)
        //       quartz 3.3 includes official support for Microsoft DI and ASP.NET Core Hosted Services. (http://disq.us/p/2ag47yp)
        services.TryAddSingleton<ISchedulerFactory, StdSchedulerFactory>();
        services.TryAddSingleton<IJobFactory, ScopedJobFactory>();

        services.AddHostedService(sp =>
            new JobSchedulingService(
                sp.GetService<ILoggerFactory>(),
                sp.GetRequiredService<ISchedulerFactory>(),
                sp.GetRequiredService<IJobFactory>(),
                sp.GetServices<JobSchedule>(),
                contextOptions));

        return new JobSchedulingBuilderContext(services, null, contextOptions);
    }

    /// <summary>
    /// Adds a scoped scheduled job
    /// </summary>
    /// <param name="cronExpression">the cron expression: https://www.freeformatter.com/cron-expression-generator-quartz.html</param>
    public static JobSchedulingBuilderContext WithJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression)
        where TJob : class, IJob
    {
        return context.WithScopedJob<TJob>(cronExpression);
    }

    /// <summary>
    /// Adds a scoped scheduled job
    /// </summary>
    /// <param name="cronExpression">the cron expression: https://www.freeformatter.com/cron-expression-generator-quartz.html</param>
    public static JobSchedulingBuilderContext WithScopedJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression)
        where TJob : class, IJob
    {
        context.Services.AddScoped<TJob>();
        context.Services.AddSingleton(new JobSchedule(
            jobType: typeof(TJob),
            cronExpression: cronExpression));

        return context;
    }

    /// <summary>
    /// Adds a singleton scheduled job
    /// </summary>
    /// <param name="cronExpression">the cron expression: https://www.freeformatter.com/cron-expression-generator-quartz.html</param>
    public static JobSchedulingBuilderContext WithSingletonJob<TJob>(
        this JobSchedulingBuilderContext context,
        string cronExpression)
        where TJob : class, IJob
    {
        context.Services.AddSingleton<TJob>();
        context.Services.AddSingleton(new JobSchedule(
            jobType: typeof(TJob),
            cronExpression: cronExpression));

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
