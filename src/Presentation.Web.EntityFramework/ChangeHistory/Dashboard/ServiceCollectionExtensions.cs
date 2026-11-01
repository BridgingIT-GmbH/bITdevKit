// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;
using Microsoft.EntityFrameworkCore;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Overrides dashboard settings for an entity/context pair. Tracked entities are otherwise discovered automatically.
    /// </summary>
    /// <typeparam name="TEntity">The tracked entity type.</typeparam>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional dashboard settings for this entity/context pair.</param>
    /// <param name="enabled">Whether to apply this explicit override. Implicit discovery remains enabled.</param>
    /// <returns>The service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddChangeHistoryDashboard&lt;Customer, AppDbContext&gt;(options =&gt; options.IncludeValues = true);
    /// services.AddDashboard(options =&gt; options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryDashboard<TEntity, TContext>(
        this IServiceCollection services,
        Action<ChangeHistoryDashboardOptions> configure = null,
        bool enabled = true)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (!enabled)
        {
            return services;
        }

        var options = new ChangeHistoryDashboardOptions();
        configure?.Invoke(options);

        services.AddChangeHistoryServices<TEntity, TContext>();

        // Reconfiguring a pair replaces its descriptor instead of duplicating dashboard entries.
        var existing = services.FirstOrDefault(service =>
            service.ServiceType == typeof(ChangeHistoryDashboardDescriptor) &&
            service.ImplementationInstance is ChangeHistoryDashboardDescriptor descriptor &&
            descriptor.EntityType == typeof(TEntity) && descriptor.ContextType == typeof(TContext));
        if (existing is not null)
        {
            services.Remove(existing);
        }

        services.AddSingleton(new ChangeHistoryDashboardDescriptor(typeof(TEntity), typeof(TContext), options));

        return services;
    }
}
