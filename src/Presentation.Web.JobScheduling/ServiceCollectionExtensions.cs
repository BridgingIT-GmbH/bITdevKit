// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.JobScheduling;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds job scheduling endpoints to the service collection with a fluent options builder.
    /// </summary>
    /// <param name="context">The JobSchedulingBuilderContext instance.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">A condition to determine if the endpoints should be registered (default: true).</param>
    /// <returns>The updated JobSchedulingBuilderContext for further configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduling()
    ///     .AddEndpoints(options => options
    ///         .RequireAuthorization()
    ///         .GroupPath("/api/_system/jobs"), enabled: true);
    /// </code>
    /// </example>
    public static JobSchedulingBuilderContext AddEndpoints(
        this JobSchedulingBuilderContext context,
        Builder<JobSchedulingEndpointsOptionsBuilder, JobSchedulingEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        var options = optionsBuilder?.Invoke(new JobSchedulingEndpointsOptionsBuilder()).Build();

        return context.AddEndpoints(options, enabled);
    }

    /// <summary>
    /// Adds job scheduling endpoints to the service collection with an optional condition.
    /// </summary>
    /// <param name="context">The JobSchedulingBuilderContext instance.</param>
    /// <param name="enabled">A condition to determine if the endpoints should be registered (default: true).</param>
    /// <returns>The updated JobSchedulingBuilderContext for further configuration.</returns>
    public static JobSchedulingBuilderContext AddEndpoints(
        this JobSchedulingBuilderContext context,
        JobSchedulingEndpointsOptions options,
        bool enabled = true)
    {
        if (enabled)
        {
            if (options != null)
            {
                context.Services.AddSingleton(options);
            }

            context.Services.AddEndpoints<JobSchedulingEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Adds job scheduling endpoints to the service collection with an optional condition.
    /// </summary>
    /// <param name="context">The JobSchedulingBuilderContext instance.</param>
    /// <param name="enabled">A condition to determine if the endpoints should be registered (default: true).</param>
    /// <returns>The updated JobSchedulingBuilderContext for further configuration.</returns>
    public static JobSchedulingBuilderContext AddEndpoints(
        this JobSchedulingBuilderContext context,
        bool enabled = true)
    {
        if (enabled)
        {
            context.Services.AddEndpoints<JobSchedulingEndpoints>(enabled);
        }

        return context;
    }

    public static JobSchedulingBuilderContext AddConsoleCommands(
        this JobSchedulingBuilderContext context,
        bool enabled = true)
    {
        if (enabled)
        {
            context.Services.AddTransient<IConsoleCommand, JobListConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobTriggerConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobHistoryConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobStatsConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobInterruptConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobPauseConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, JobResumeConsoleCommand>();
        }

        return context;
    }
}
