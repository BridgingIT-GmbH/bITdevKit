// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides service collection extensions for registering the log entry (database logging) services.
/// </summary>
public static class LogEntryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILogEntryService" />, the maintenance queue and the background maintenance service for the logging context.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type that implements <see cref="ILoggingContext" />.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="optionsBuilder">Optional fluent configuration of the options, including enabling or disabling the services.</param>
    /// <returns>A builder context for further fluent configuration, such as exposing endpoints.</returns>
    /// <example>
    /// <code>
    /// services.AddLogEntryServices&lt;CoreDbContext&gt;(o => o.Enabled(builder.Environment.IsDevelopment()));
    /// </code>
    /// </example>
    /// <remarks>
    /// Nothing is registered when the options are disabled. During build-time OpenAPI generation only the
    /// service and queue are registered (so endpoints can be described); the background service and health check are skipped.
    /// </remarks>
    public static LogEntryServicesBuilderContext AddLogEntryServices<TContext>(
        this IServiceCollection services,
        Builder<LogEntryMaintenanceServiceOptionsBuilder, LogEntryMaintenanceServiceOptions> optionsBuilder = null)
        where TContext : DbContext, ILoggingContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = optionsBuilder is null
            ? new LogEntryMaintenanceServiceOptions()
            : optionsBuilder(new LogEntryMaintenanceServiceOptionsBuilder()).Build();

        if (!options.Enabled)
        {
            return new LogEntryServicesBuilderContext(services, options);
        }

        services.TryAddScoped<ILogEntryService, LogEntryService<TContext>>();
        services.TryAddSingleton<LogEntryMaintenanceQueue>();
        services.TryAddSingleton(options);

        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
        {
            services.AddHostedService<LogEntryMaintenanceService<TContext>>();
            services.TryAddBackgroundServiceHealthCheck<LogEntryMaintenanceService<TContext>>(
                nameof(LogEntryMaintenanceService<>),
                tags: ["background", "jobs"]);
        }

        return new LogEntryServicesBuilderContext(services, options);
    }
}
