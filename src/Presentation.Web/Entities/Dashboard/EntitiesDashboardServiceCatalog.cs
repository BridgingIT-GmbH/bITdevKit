// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Reads the registered repository entity types from the service collection, before the container is built.</summary>
/// <param name="services">The service collection to scan.</param>
internal sealed class EntitiesDashboardServiceCatalog(IServiceCollection services)
{
    /// <summary>Gets the entity types of all closed <c>IGenericRepository&lt;T&gt;</c> registrations.</summary>
    /// <returns>The distinct entity types.</returns>
    public IReadOnlyList<Type> GetRepositoryEntityTypes()
    {
        var types = new List<Type>();
        var seen = new HashSet<Type>();

        foreach (var descriptor in services.ToArray())
        {
            var serviceType = descriptor.ServiceType;
            if (!serviceType.IsGenericType || serviceType.IsGenericTypeDefinition || serviceType.ContainsGenericParameters
                || serviceType.GetGenericTypeDefinition() != typeof(IGenericRepository<>))
            {
                continue;
            }

            var entityType = serviceType.GetGenericArguments()[0];

            // decorator registrations (Scrutor) use a different service type and are skipped by the check above
            if (serviceType != typeof(IGenericRepository<>).MakeGenericType(entityType)
                || entityType.IsAbstract
                || entityType.IsGenericTypeDefinition
                || entityType.ContainsGenericParameters
                || !typeof(IEntity).IsAssignableFrom(entityType))
            {
                continue;
            }

            if (seen.Add(entityType))
            {
                types.Add(entityType);
            }
        }

        return types;
    }
}
