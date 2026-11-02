// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the Entities dashboard page, which browses (and optionally edits) the entities
    /// discovered from repository registrations and ActiveEntity types.
    /// Call it before or after <c>AddDashboard</c>; the configuration is validated eagerly and fails fast.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure the <see cref="EntitiesDashboardOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    /// <example>
    /// <code>
    /// services.AddEntitiesDashboard(options =>
    /// {
    ///     options.EditPolicy = "EntitiesEditors";
    ///     options.Exclude&lt;Region&gt;();
    ///     options.Entity&lt;Customer&gt;(e => e
    ///         .Mask(c => c.Email)
    ///         .Reference(c => c.PreferredRegionId).To&lt;Region&gt;());
    /// });
    /// services.AddDashboard(o => o.Enabled());
    /// </code>
    /// </example>
    public static IServiceCollection AddEntitiesDashboard(
        this IServiceCollection services,
        Action<EntitiesDashboardOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new EntitiesDashboardOptions();
        configure?.Invoke(options);
        EntitiesDashboardConfigurationValidator.Validate(options);

        services.RemoveAll<EntitiesDashboardOptions>();
        services.AddSingleton(options);
        services.AddEntitiesDashboardServices();

        return services;
    }

    /// <summary>Registers the Entities dashboard services without replacing existing registrations.</summary>
    internal static IServiceCollection AddEntitiesDashboardServices(this IServiceCollection services)
    {
        // the catalog needs the registrations as they are, to find the repository entity types
        services.TryAddSingleton(new EntitiesDashboardServiceCatalog(services));
        services.TryAddSingleton<EntitiesDashboardCatalog>(sp => new EntitiesDashboardCatalog(sp));
        services.TryAddSingleton<EntitiesDashboardAccessEvaluator>(sp =>
            new EntitiesDashboardAccessEvaluator(EntitiesDashboardOptionsAccessor.Get(sp), sp));
        services.TryAddSingleton<EntitiesDashboardQueryService>();
        services.TryAddSingleton<EntitiesDashboardWriteService>();
        services.TryAddSingleton<EntitiesDashboardExportService>();

        return services;
    }
}
