// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>
/// Singleton that discovers the dashboard entities once and serves them by key.
/// Stores metadata only, never entity data.
/// </summary>
internal sealed class EntitiesDashboardCatalog
{
    private readonly Lazy<IReadOnlyList<EntitiesDashboardEntity>> entities;
    private readonly Lazy<Dictionary<string, EntitiesDashboardEntity>> byKey;

    /// <summary>Initializes a new instance of the <see cref="EntitiesDashboardCatalog"/> class.</summary>
    /// <param name="rootServices">The root service provider.</param>
    public EntitiesDashboardCatalog(IServiceProvider rootServices)
    {
        ArgumentNullException.ThrowIfNull(rootServices);

        this.entities = new Lazy<IReadOnlyList<EntitiesDashboardEntity>>(
            () => EntitiesDashboardDiscovery.Discover(rootServices, EntitiesDashboardOptionsAccessor.Get(rootServices)),
            LazyThreadSafetyMode.ExecutionAndPublication);
        this.byKey = new Lazy<Dictionary<string, EntitiesDashboardEntity>>(
            () => this.entities.Value.ToDictionary(e => e.Key, StringComparer.Ordinal),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Gets the discovered entities, ordered by name.</summary>
    public IReadOnlyList<EntitiesDashboardEntity> Entities => this.entities.Value;

    /// <summary>Gets a value indicating whether at least one entity source was discovered.</summary>
    public bool HasSources => this.Entities.Count > 0;

    /// <summary>Finds an entity by its key (ordinal).</summary>
    /// <param name="key">The entity key.</param>
    /// <returns>The entity, or null.</returns>
    public EntitiesDashboardEntity Find(string key) =>
        key != null && this.byKey.Value.TryGetValue(key, out var entity) ? entity : null;
}
