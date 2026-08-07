// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;
using Microsoft.EntityFrameworkCore;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ChangeHistory HTTP endpoints and requester handlers for one entity and EF Core context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type exposed by the endpoints.</typeparam>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">A value indicating whether endpoint registration is enabled.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddRequester();
    /// services.AddChangeHistoryEndpoints&lt;Customer, AppDbContext&gt;(options =&gt; options
    ///     .GroupPath("/_bdk/api/customers/history")
    ///     .RequireReadPolicy("Customers.History.Read")
    ///     .RequireRestorePolicy("Customers.History.Restore"));
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryEndpoints<TEntity, TContext>(
        this IServiceCollection services,
        Builder<ChangeHistoryEndpointsOptionsBuilder, ChangeHistoryEndpointsOptions> optionsBuilder,
        bool enabled = true)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = optionsBuilder?.Invoke(new ChangeHistoryEndpointsOptionsBuilder()).Build();

        return services.AddChangeHistoryEndpoints<TEntity, TContext>(options, enabled);
    }

    /// <summary>
    /// Registers ChangeHistory HTTP endpoints and requester handlers for one entity and EF Core context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type exposed by the endpoints.</typeparam>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The endpoint options.</param>
    /// <param name="enabled">A value indicating whether endpoint registration is enabled.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddChangeHistoryEndpoints&lt;Customer, AppDbContext&gt;(new ChangeHistoryEndpointsOptions());
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryEndpoints<TEntity, TContext>(
        this IServiceCollection services,
        ChangeHistoryEndpointsOptions options = null,
        bool enabled = true)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (!enabled)
        {
            return services;
        }

        options ??= new ChangeHistoryEndpointsOptions();
        var changeHistoryOptions = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(ChangeHistoryOptions))
            ?.ImplementationInstance as ChangeHistoryOptions;
        options.ReadPolicy ??= changeHistoryOptions?.ReadAuthorizationPolicy;
        options.RestorePolicy ??= changeHistoryOptions?.RestoreAuthorizationPolicy;

        services.AddChangeHistoryServices<TEntity, TContext>();
        services.AddEndpoints(new ChangeHistoryEndpoints<TEntity, TContext>(options), options.Enabled);
        services.AddSingleton(new ChangeHistoryDashboardDescriptor(typeof(TEntity), typeof(TContext), options));

        return services;
    }
}
