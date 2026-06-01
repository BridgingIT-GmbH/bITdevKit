// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Jobs;

/// <summary>
/// Adds Jobs web endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds jobs endpoints with a fluent options builder.
    /// </summary>
    public static JobBuilderContext AddEndpoints(
        this JobBuilderContext context,
        Builder<JobSchedulerEndpointsOptionsBuilder, JobSchedulerEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        var options = optionsBuilder?.Invoke(new JobSchedulerEndpointsOptionsBuilder()).Build();
        return context.AddEndpoints(options, enabled);
    }

    /// <summary>
    /// Adds jobs endpoints with explicit options.
    /// </summary>
    public static JobBuilderContext AddEndpoints(
        this JobBuilderContext context,
        JobSchedulerEndpointsOptions options,
        bool enabled = true)
    {
        if (enabled)
        {
            if (options is not null)
            {
                context.Services.AddSingleton(options);
            }

            context.Services.AddEndpoints<JobSchedulerEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Adds jobs endpoints using the default options.
    /// </summary>
    public static JobBuilderContext AddEndpoints(
        this JobBuilderContext context,
        bool enabled = true)
    {
        if (enabled)
        {
            context.Services.AddEndpoints<JobSchedulerEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Adds jobs console commands to the service collection.
    /// </summary>
    public static JobBuilderContext AddConsoleCommands(
        this JobBuilderContext context,
        bool enabled = true)
    {
        if (enabled)
        {
            context.Services.AddTransient<IConsoleCommand, JobSchedulerListConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerTriggersConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerOccurrencesConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerHistoryConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerMetricsConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerDispatchConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerPauseConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerResumeConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerEnableConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerDisableConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerStopConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerCancelConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerRetryConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerArchiveConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobSchedulerReleaseLeaseConsoleCommand>();
        }

        return context;
    }
}
