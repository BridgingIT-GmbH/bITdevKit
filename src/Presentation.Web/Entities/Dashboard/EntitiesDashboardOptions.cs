// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Domain.Model;

/// <summary>Configures the Entities dashboard page: edit access, limits, entity inclusion and per-entity rules.</summary>
/// <example>
/// <code>
/// services.AddEntitiesDashboard(options =>
/// {
///     options.EditPolicy = "EntitiesEditor";
///     options.BatchSize = 25;
///     options.Exclude&lt;Region&gt;()
///         .Entity&lt;Customer&gt;(e => e.Mask(c => c.Email));
/// });
/// </code>
/// </example>
public class EntitiesDashboardOptions
{
    private readonly HashSet<Type> excludedTypes = [];
    private readonly HashSet<Type> includedTypes = [];
    private readonly Dictionary<Type, EntitiesDashboardEntityConfiguration> entityConfigurations = [];

    /// <summary>Gets or sets the authorization policy that enables edit mode. Null leaves the page read-only unless <see cref="EditRoles"/> is set.</summary>
    /// <example>
    /// <code>
    /// options.EditPolicy = "EntitiesEditor";
    /// </code>
    /// </example>
    public string EditPolicy { get; set; }

    /// <summary>Gets or sets the roles that enable edit mode. When a policy is also set, both must be satisfied.</summary>
    /// <example>
    /// <code>
    /// options.EditRoles.Add("Administrator");
    /// </code>
    /// </example>
    public IList<string> EditRoles { get; set; } = new List<string>();

    /// <summary>Gets or sets the number of rows loaded per batch (default 50, never above <see cref="MaxBatchSize"/>).</summary>
    /// <example>
    /// <code>
    /// options.BatchSize = 25;
    /// </code>
    /// </example>
    public int BatchSize { get; set; } = 50;

    /// <summary>Gets or sets the largest batch size that can be requested (default 200).</summary>
    /// <example>
    /// <code>
    /// options.MaxBatchSize = 100;
    /// </code>
    /// </example>
    public int MaxBatchSize { get; set; } = 200;

    /// <summary>Gets or sets the maximum number of rows in an export (default 5000).</summary>
    /// <example>
    /// <code>
    /// options.MaxExportRows = 1000;
    /// </code>
    /// </example>
    public int MaxExportRows { get; set; } = 5000;

    /// <summary>Gets or sets the maximum number of rows that can be selected for bulk delete (default 100).</summary>
    /// <example>
    /// <code>
    /// options.MaxBulkSelection = 50;
    /// </code>
    /// </example>
    public int MaxBulkSelection { get; set; } = 100;

    internal IReadOnlySet<Type> ExcludedTypes => this.excludedTypes;

    internal IReadOnlySet<Type> IncludedTypes => this.includedTypes;

    internal IReadOnlyDictionary<Type, EntitiesDashboardEntityConfiguration> EntityConfigurations =>
        this.entityConfigurations;

    internal bool IsEditConfigured =>
        !string.IsNullOrWhiteSpace(this.EditPolicy) || (this.EditRoles?.Count ?? 0) > 0;

    internal int EffectiveBatchSize => Math.Clamp(this.BatchSize, 1, Math.Max(1, this.MaxBatchSize));

    internal IReadOnlyList<int> PageSizes =>
        new[] { 10, 25, 50, 100, 200, 500 }
            .Where(s => s <= Math.Max(1, this.MaxBatchSize))
            .Append(this.EffectiveBatchSize)
            .Distinct()
            .Order()
            .ToList();

    internal int ResolveBatchSize(int requested) =>
        requested > 0 ? Math.Clamp(requested, 1, Math.Max(1, this.MaxBatchSize)) : this.EffectiveBatchSize;

    /// <summary>Excludes an entity type from the dashboard.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The same options.</returns>
    /// <example>
    /// <code>
    /// options.Exclude&lt;Region&gt;();
    /// </code>
    /// </example>
    public EntitiesDashboardOptions Exclude<TEntity>() => this.Exclude(typeof(TEntity));

    /// <summary>Excludes an entity type from the dashboard.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The same options.</returns>
    /// <example>
    /// <code>
    /// options.Exclude(typeof(Region));
    /// </code>
    /// </example>
    public EntitiesDashboardOptions Exclude(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        this.excludedTypes.Add(entityType);
        this.includedTypes.Remove(entityType);

        return this;
    }

    /// <summary>Re-includes an entity type that the default deny list would otherwise hide.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The same options.</returns>
    /// <example>
    /// <code>
    /// options.Include&lt;Customer&gt;();
    /// </code>
    /// </example>
    public EntitiesDashboardOptions Include<TEntity>() => this.Include(typeof(TEntity));

    /// <summary>Re-includes an entity type that the default deny list would otherwise hide.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The same options.</returns>
    /// <example>
    /// <code>
    /// options.Include(typeof(Customer));
    /// </code>
    /// </example>
    public EntitiesDashboardOptions Include(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        this.includedTypes.Add(entityType);
        this.excludedTypes.Remove(entityType);

        return this;
    }

    /// <summary>Configures masking, hiding, references and write opt-ins for an entity type.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="configure">The entity builder callback.</param>
    /// <returns>The same options.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Customer&gt;(e => e.Mask(c => c.Email).Hide("InternalNotes"));
    /// </code>
    /// </example>
    public EntitiesDashboardOptions Entity<TEntity>(Action<EntitiesDashboardEntityBuilder<TEntity>> configure)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!this.entityConfigurations.TryGetValue(typeof(TEntity), out var configuration))
        {
            configuration = new EntitiesDashboardEntityConfiguration(typeof(TEntity));
            this.entityConfigurations[typeof(TEntity)] = configuration;
        }

        configure(new EntitiesDashboardEntityBuilder<TEntity>(configuration));

        return this;
    }
}
