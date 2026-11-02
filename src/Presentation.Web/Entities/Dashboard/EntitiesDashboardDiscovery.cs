// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Discovers the entity types the Entities dashboard can show, from repository registrations and the ActiveEntity registry.</summary>
internal static class EntitiesDashboardDiscovery
{
    /// <summary>Discovers, filters and merges entity sources into dashboard entities.</summary>
    /// <param name="services">The root service provider.</param>
    /// <param name="options">The dashboard options.</param>
    /// <returns>The entities ordered by name, then key.</returns>
    public static IReadOnlyList<EntitiesDashboardEntity> Discover(IServiceProvider services, EntitiesDashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var candidates = new Dictionary<Type, Candidate>();

        var serviceCatalog = services.GetService<EntitiesDashboardServiceCatalog>();
        foreach (var entityType in serviceCatalog?.GetRepositoryEntityTypes() ?? [])
        {
            var candidate = GetCandidate(candidates, entityType);
            candidate.Repository = true;
            candidate.IdType ??= TryGetIdType(entityType);
        }

        var registry = services.GetService<ActiveEntityRegistry>();
        foreach (var descriptor in registry?.Descriptors ?? [])
        {
            var candidate = GetCandidate(candidates, descriptor.EntityType);
            candidate.ActiveEntity = true;
            candidate.IdType = descriptor.IdType;
        }

        var result = new List<EntitiesDashboardEntity>();
        foreach (var (entityType, candidate) in candidates)
        {
            options.EntityConfigurations.TryGetValue(entityType, out var configuration);

            if (options.ExcludedTypes.Contains(entityType) || configuration?.Excluded == true)
            {
                continue;
            }

            if (EntitiesDashboardDenyList.IsDenied(entityType) && !options.IncludedTypes.Contains(entityType))
            {
                continue;
            }

            if (candidate.IdType == null)
            {
                continue;
            }

            var sources = new List<IEntitiesDashboardSource>(2);
            if (candidate.Repository)
            {
                sources.Add(new RepositoryEntitiesDashboardSource(entityType, candidate.IdType));
            }

            if (candidate.ActiveEntity)
            {
                sources.Add(new ActiveEntityEntitiesDashboardSource(entityType, candidate.IdType));
            }

            result.Add(EntityPropertyDescriptorFactory.Create(entityType, candidate.IdType, configuration, sources));
        }

        var ordered = result
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        EntitiesDashboardReferenceResolver.Resolve(ordered, options);

        return ordered;
    }

    private static Candidate GetCandidate(Dictionary<Type, Candidate> candidates, Type entityType)
    {
        if (!candidates.TryGetValue(entityType, out var candidate))
        {
            candidate = new Candidate();
            candidates[entityType] = candidate;
        }

        return candidate;
    }

    // IEntity<TId> exposes the id type of repository entities
    private static Type TryGetIdType(Type entityType) =>
        entityType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(BridgingIT.DevKit.Domain.Model.IEntity<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();

    private sealed class Candidate
    {
        public bool Repository { get; set; }

        public bool ActiveEntity { get; set; }

        public Type IdType { get; set; }
    }
}
