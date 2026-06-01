// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides outbound integration registration helpers for jobs.
/// </summary>
public static class JobSchedulerIntegrationExtensions
{
    /// <summary>
    /// Registers a Requester-backed outbound job.
    /// </summary>
    /// <typeparam name="TData">The typed job data contract.</typeparam>
    /// <typeparam name="TRequest">The dispatched request type.</typeparam>
    /// <typeparam name="TValue">The request result value type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The outbound job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithRequestSendJob&lt;ExportCustomersData, ExportCustomersCommand, int&gt;("export-customers", job =&gt; job
    ///         .WithDescription("Dispatches the export command.")
    ///         .WithRequest(context =&gt; new ExportCustomersCommand(context.Data.BatchId))
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext WithRequestSendJob<TData, TRequest, TValue>(
        this JobBuilderContext context,
        string jobName,
        Action<JobRequesterDefinitionBuilder<TData, TRequest, TValue>> configure)
        where TRequest : class, IRequest<TValue>
        => context.WithRequesterJob<TData, TRequest, TValue>(jobName, configure);

    /// <summary>
    /// Registers a Requester-backed outbound command job.
    /// </summary>
    /// <typeparam name="TData">The typed job data contract.</typeparam>
    /// <typeparam name="TCommand">The dispatched command type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The outbound job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithCommandJob&lt;ExportCustomersData, ExportCustomersCommand&gt;("export-customers", job =&gt; job
    ///         .WithDescription("Dispatches the export command.")
    ///         .WithRequest(context =&gt; new ExportCustomersCommand(context.Data.BatchId))
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext WithCommandJob<TData, TCommand>(
        this JobBuilderContext context,
        string jobName,
        Action<JobRequesterDefinitionBuilder<TData, TCommand, Result>> configure)
        where TCommand : class, IRequest<Result>
        => context.WithRequesterJob<TData, TCommand, Result>(jobName, configure);

    /// <summary>
    /// Registers a Requester-backed outbound job.
    /// </summary>
    /// <typeparam name="TData">The typed job data contract.</typeparam>
    /// <typeparam name="TRequest">The dispatched request type.</typeparam>
    /// <typeparam name="TValue">The request result value type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The outbound job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithRequesterJob&lt;ExportCustomersData, ExportCustomersCommand, int&gt;("export-customers", job =&gt; job
    ///         .WithDescription("Dispatches the export command.")
    ///         .WithRequest(context =&gt; new ExportCustomersCommand(context.Data.BatchId))
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext WithRequesterJob<TData, TRequest, TValue>(
        this JobBuilderContext context,
        string jobName,
        Action<JobRequesterDefinitionBuilder<TData, TRequest, TValue>> configure)
        where TRequest : class, IRequest<TValue>
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobRequesterDefinitionBuilder<TData, TRequest, TValue>(jobName);
        configure?.Invoke(builder);

        EnsureRequesterRegistrations(context.Services).Add(jobName, builder.BuildSettings());
        context.Services.AddTransient<RequesterJob<TData, TRequest, TValue>>(sp =>
            new RequesterJob<TData, TRequest, TValue>(
                sp,
                sp.GetRequiredService<RequesterJobRegistrationStore>()));
        context.Registrations.Add(builder.BuildDefinition());

        return context;
    }

    /// <summary>
    /// Registers a Notifier-backed outbound job.
    /// </summary>
    /// <typeparam name="TData">The typed job data contract.</typeparam>
    /// <typeparam name="TNotification">The published notification type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The outbound job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithNotifierJob&lt;ExportCompletedData, ExportCompletedNotification&gt;("notify-export", job =&gt; job
    ///         .WithDescription("Publishes the export completion notification.")
    ///         .WithNotification(context =&gt; new ExportCompletedNotification(context.Data.ExportId))
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext WithNotifierJob<TData, TNotification>(
        this JobBuilderContext context,
        string jobName,
        Action<JobNotifierDefinitionBuilder<TData, TNotification>> configure)
        where TNotification : class, INotification
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobNotifierDefinitionBuilder<TData, TNotification>(jobName);
        configure?.Invoke(builder);

        EnsureNotifierRegistrations(context.Services).Add(jobName, builder.BuildSettings());
        context.Services.AddTransient<NotifierJob<TData, TNotification>>(sp =>
            new NotifierJob<TData, TNotification>(
                sp,
                sp.GetRequiredService<NotifierJobRegistrationStore>()));
        context.Registrations.Add(builder.BuildDefinition());

        return context;
    }

    /// <summary>
    /// Registers a Notifier-backed outbound job.
    /// </summary>
    /// <typeparam name="TData">The typed job data contract.</typeparam>
    /// <typeparam name="TNotification">The published notification type.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="configure">The outbound job configuration callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithNotificationPublishJob&lt;ExportCompletedData, ExportCompletedNotification&gt;("notify-export", job =&gt; job
    ///         .WithDescription("Publishes the export completion notification.")
    ///         .WithNotification(context =&gt; new ExportCompletedNotification(context.Data.ExportId))
    ///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
    /// </code>
    /// </example>
    public static JobBuilderContext WithNotificationPublishJob<TData, TNotification>(
        this JobBuilderContext context,
        string jobName,
        Action<JobNotifierDefinitionBuilder<TData, TNotification>> configure)
        where TNotification : class, INotification
        => context.WithNotifierJob<TData, TNotification>(jobName, configure);

    private static RequesterJobRegistrationStore EnsureRequesterRegistrations(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(RequesterJobRegistrationStore));
        if (descriptor?.ImplementationInstance is RequesterJobRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new RequesterJobRegistrationStore();
        services.AddSingleton(registrations);
        return registrations;
    }

    private static NotifierJobRegistrationStore EnsureNotifierRegistrations(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(NotifierJobRegistrationStore));
        if (descriptor?.ImplementationInstance is NotifierJobRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new NotifierJobRegistrationStore();
        services.AddSingleton(registrations);
        return registrations;
    }
}