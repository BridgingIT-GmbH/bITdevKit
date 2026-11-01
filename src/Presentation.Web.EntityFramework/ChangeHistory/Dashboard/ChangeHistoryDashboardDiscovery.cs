// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Discovers tracked entity/context pairs and applies explicit dashboard overrides.</summary>
/// <example><code>var registrations = ChangeHistoryDashboardDiscovery.GetRegistrations(requestServices);</code></example>
public static class ChangeHistoryDashboardDiscovery
{
    /// <summary>Returns one descriptor per tracked entity/context pair under the current service scope.</summary>
    /// <example><code>var registrations = ChangeHistoryDashboardDiscovery.GetRegistrations(requestServices);</code></example>
    public static IReadOnlyList<ChangeHistoryDashboardDescriptor> GetRegistrations(IServiceProvider services)
    {
        var registrations = services.GetServices<ChangeHistoryDashboardDescriptor>()
            .Where(descriptor => descriptor.IsValid)
            .ToDictionary(descriptor => descriptor.Key);
        var trackedTypes = services.GetService<ChangeHistoryOptions>()?.TrackedEntities.Keys.ToArray() ?? [];

        if (trackedTypes.Length > 0)
        {
            // Use both DevKit registrations and standard EF registrations, including pooled contexts.
            var contextTypes = services.GetServices<DbContextRegistration>().Select(registration => registration.ContextType)
                .Concat(services.GetServices<DbContextOptions>().Select(options => options.ContextType))
                .Distinct()
                .Where(type => !type.IsAbstract && typeof(DbContext).IsAssignableFrom(type));

            foreach (var contextType in contextTypes)
            {
                if (services.GetService(contextType) is not DbContext context ||
                    context.Model.FindEntityType(typeof(ChangeHistoryEntry)) is null)
                {
                    continue;
                }

                foreach (var entityType in trackedTypes.Where(type => context.Model.FindEntityType(type) is not null))
                {
                    var descriptor = new ChangeHistoryDashboardDescriptor(entityType, contextType, null);
                    registrations.TryAdd(descriptor.Key, descriptor);
                }
            }
        }

        return registrations.Values
            .OrderBy(descriptor => descriptor.EntityTypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.ContextTypeName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
