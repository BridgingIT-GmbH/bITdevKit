// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;

/// <summary>
/// Configures ChangeHistory capture scope and persistence metadata.
/// </summary>
/// <example>
/// <code>
/// var options = new ChangeHistoryOptions()
///     .Track&lt;Customer&gt;()
///         .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
///         .Exclude(c =&gt; c.PasswordHash)
///         .Options;
/// </code>
/// </example>
public class ChangeHistoryOptions
{
    /// <summary>
    /// Gets or sets the global capture strategy used by opted-in entities without an override.
    /// </summary>
    public ChangeHistoryCaptureStrategy DefaultCaptureStrategy { get; set; } = ChangeHistoryCaptureStrategy.RepositorySnapshot;

    /// <summary>
    /// Gets or sets the default safety limit for set-based update capture.
    /// </summary>
    public int DefaultUpdateSetMaxAffectedRows { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum stored serialized value length. Null keeps values unbounded.
    /// </summary>
    public int? MaxStoredValueLength { get; set; }

    /// <summary>
    /// Gets or sets how values exceeding <see cref="MaxStoredValueLength" /> are handled.
    /// </summary>
    public ChangeHistoryOversizedValuePolicy OversizedValuePolicy { get; set; } = ChangeHistoryOversizedValuePolicy.Include;

    /// <summary>
    /// Gets or sets the value policy applied to common sensitive property names when no explicit policy exists.
    /// </summary>
    public ChangeHistoryValuePolicy SensitiveValuePolicy { get; set; } = ChangeHistoryValuePolicy.HashOnly;

    /// <summary>
    /// Gets or sets a value indicating whether common sensitive property names are protected by default.
    /// </summary>
    public bool ProtectSensitivePropertyNames { get; set; } = true;

    /// <summary>
    /// Gets or sets the global authorization policy name required to read ChangeHistory when endpoints or authorizers honor policies.
    /// </summary>
    public string ReadAuthorizationPolicy { get; set; }

    /// <summary>
    /// Gets or sets the global authorization policy name required to restore ChangeHistory when endpoints or authorizers honor policies.
    /// </summary>
    public string RestoreAuthorizationPolicy { get; set; }

    /// <summary>
    /// Gets the explicitly tracked entity configurations.
    /// </summary>
    public IDictionary<Type, ChangeHistoryEntityOptions> TrackedEntities { get; } = new Dictionary<Type, ChangeHistoryEntityOptions>();

    /// <summary>
    /// Sets the default capture strategy used by tracked entities.
    /// </summary>
    /// <param name="strategy">The default capture strategy.</param>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions UseCaptureStrategy(ChangeHistoryCaptureStrategy strategy)
    {
        this.DefaultCaptureStrategy = strategy;

        return this;
    }

    /// <summary>
    /// Sets the default safety limit for set-based update capture.
    /// </summary>
    /// <param name="maxAffectedRows">The maximum number of affected entities to snapshot.</param>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions UseDefaultUpdateSetMaxAffectedRows(int maxAffectedRows)
    {
        this.DefaultUpdateSetMaxAffectedRows = maxAffectedRows < 1 ? 1 : maxAffectedRows;

        return this;
    }

    /// <summary>
    /// Sets the default policy for oversized serialized values.
    /// </summary>
    /// <param name="policy">The oversized value policy.</param>
    /// <param name="maxStoredValueLength">The maximum stored value length.</param>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions UseOversizedValuePolicy(
        ChangeHistoryOversizedValuePolicy policy,
        int? maxStoredValueLength)
    {
        this.OversizedValuePolicy = policy;
        this.MaxStoredValueLength = maxStoredValueLength.HasValue && maxStoredValueLength.Value > 0 ? maxStoredValueLength : null;

        return this;
    }

    /// <summary>
    /// Sets the default policy for common sensitive property names.
    /// </summary>
    /// <param name="policy">The value policy.</param>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions UseSensitiveValuePolicy(ChangeHistoryValuePolicy policy)
    {
        this.SensitiveValuePolicy = policy;
        this.ProtectSensitivePropertyNames = policy != ChangeHistoryValuePolicy.Include;

        return this;
    }

    /// <summary>
    /// Disables automatic protection of common sensitive property names.
    /// </summary>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions DisableSensitivePropertyNameProtection()
    {
        this.ProtectSensitivePropertyNames = false;

        return this;
    }

    /// <summary>
    /// Sets the global authorization policy name required to read ChangeHistory.
    /// </summary>
    /// <param name="policy">The authorization policy name.</param>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions UseReadAuthorizationPolicy(string policy)
    {
        this.ReadAuthorizationPolicy = string.IsNullOrWhiteSpace(policy) ? null : policy;

        return this;
    }

    /// <summary>
    /// Sets the global authorization policy name required to restore ChangeHistory.
    /// </summary>
    /// <param name="policy">The authorization policy name.</param>
    /// <returns>The current options instance.</returns>
    public ChangeHistoryOptions UseRestoreAuthorizationPolicy(string policy)
    {
        this.RestoreAuthorizationPolicy = string.IsNullOrWhiteSpace(policy) ? null : policy;

        return this;
    }

    /// <summary>
    /// Adds or returns the capture configuration for the specified entity type.
    /// Concurrency versions are excluded by convention for entities implementing <see cref="IConcurrency" />.
    /// </summary>
    /// <typeparam name="TEntity">The tracked entity type.</typeparam>
    /// <returns>A builder for entity-specific capture configuration.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> Track<TEntity>()
        where TEntity : class, IEntity
    {
        var entityType = typeof(TEntity);
        if (!this.TrackedEntities.TryGetValue(entityType, out var entityOptions))
        {
            entityOptions = new ChangeHistoryEntityOptions(entityType);
            if (typeof(IConcurrency).IsAssignableFrom(entityType))
            {
                entityOptions.PropertyPolicies[nameof(IConcurrency.ConcurrencyVersion)] = ChangeHistoryValuePolicy.Exclude;
            }

            this.TrackedEntities.Add(entityType, entityOptions);
        }

        return new ChangeHistoryEntityOptionsBuilder<TEntity>(this, entityOptions);
    }

    /// <summary>
    /// Gets the entity-specific options for the supplied entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The entity options when tracked; otherwise null.</returns>
    public ChangeHistoryEntityOptions GetEntityOptions(Type entityType)
    {
        if (entityType is null)
        {
            return null;
        }

        return this.TrackedEntities.TryGetValue(entityType, out var options) ? options : null;
    }

    /// <summary>
    /// Validates the configured ChangeHistory options and throws when a restore policy is incomplete.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an invalid ChangeHistory configuration is found.</exception>
    /// <example>
    /// <code>
    /// options.Track&lt;Customer&gt;().AllowRestore(c =&gt; c.Name).UseValidatedSetter();
    /// options.Validate();
    /// </code>
    /// </example>
    public void Validate()
    {
        foreach (var entityOptions in this.TrackedEntities.Values)
        {
            if (entityOptions.BulkInsertCaptureMode == ChangeHistoryBulkInsertCaptureMode.Detailed &&
                entityOptions.BulkInsertMaxDetailedEntities < 1)
            {
                throw new InvalidOperationException($"ChangeHistory detailed bulk-insert capture for {entityOptions.EntityType.Name} requires a maximum entity limit greater than zero.");
            }

            foreach (var restorePolicy in entityOptions.RestorePolicies.Values)
            {
                if (restorePolicy.ExecutionMode == ChangeHistoryRestoreExecutionMode.DomainLogic &&
                    restorePolicy.DomainMethod is null &&
                    restorePolicy.HandlerType is null)
                {
                    throw new InvalidOperationException($"ChangeHistory restore policy for {entityOptions.EntityType.Name}.{restorePolicy.PropertyName} uses DomainLogic but does not define a domain method or handler.");
                }
            }
        }
    }
}

/// <summary>
/// Configures ChangeHistory capture for one entity type.
/// </summary>
/// <example>
/// <code>
/// var entityOptions = new ChangeHistoryEntityOptions(typeof(Customer));
/// entityOptions.CaptureDirectMutations = true;
/// </code>
/// </example>
public class ChangeHistoryEntityOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryEntityOptions" /> class.
    /// </summary>
    /// <param name="entityType">The tracked entity type.</param>
    public ChangeHistoryEntityOptions(Type entityType)
    {
        this.EntityType = entityType;
    }

    /// <summary>
    /// Gets the tracked entity type.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Gets or sets the entity-specific capture strategy override.
    /// </summary>
    public ChangeHistoryCaptureStrategy? CaptureStrategy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether direct mutation capture is enabled.
    /// </summary>
    public bool CaptureDirectMutations { get; set; }

    /// <summary>
    /// Gets or sets the direct mutation capture mode.
    /// </summary>
    public ChangeHistoryCaptureMode DirectMutationMode { get; set; } = ChangeHistoryCaptureMode.BestEffort;

    /// <summary>
    /// Gets or sets a value indicating whether repository insert/create initial-value capture is enabled.
    /// </summary>
    public bool CaptureCreates { get; set; }

    /// <summary>
    /// Gets or sets the native bulk-insert capture mode.
    /// </summary>
    public ChangeHistoryBulkInsertCaptureMode BulkInsertCaptureMode { get; set; } = ChangeHistoryBulkInsertCaptureMode.Disabled;

    /// <summary>
    /// Gets or sets the maximum batch size accepted by detailed native bulk-insert capture.
    /// </summary>
    public int BulkInsertMaxDetailedEntities { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether set-based update capture is enabled.
    /// </summary>
    public bool CaptureUpdateSet { get; set; }

    /// <summary>
    /// Gets or sets the set-based update capture mode.
    /// </summary>
    public ChangeHistoryCaptureMode UpdateSetMode { get; set; } = ChangeHistoryCaptureMode.BestEffort;

    /// <summary>
    /// Gets or sets the per-entity set-based update safety limit.
    /// </summary>
    public int? UpdateSetMaxAffectedRows { get; set; }

    /// <summary>
    /// Gets the configured per-property value policies.
    /// </summary>
    public IDictionary<string, ChangeHistoryValuePolicy> PropertyPolicies { get; } = new Dictionary<string, ChangeHistoryValuePolicy>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the configured per-property restore policies.
    /// </summary>
    public IDictionary<string, ChangeHistoryRestorePropertyOptions> RestorePolicies { get; } = new Dictionary<string, ChangeHistoryRestorePropertyOptions>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the configured owned, collection, and graph capture paths.
    /// </summary>
    public IList<ChangeHistoryCapturePathOptions> CapturePaths { get; } = [];

    /// <summary>
    /// Gets or sets the restore concurrency policy.
    /// </summary>
    public ChangeHistoryRestoreConcurrencyPolicy RestoreConcurrencyPolicy { get; set; } = ChangeHistoryRestoreConcurrencyPolicy.ExpectedVersion;

    /// <summary>
    /// Gets or sets the restore authorizer type.
    /// </summary>
    public Type RestoreAuthorizerType { get; set; }
}

/// <summary>
/// Configures one advanced ChangeHistory capture path.
/// </summary>
/// <example>
/// <code>
/// var path = new ChangeHistoryCapturePathOptions("BillingAddress", ChangeHistoryCapturePathKind.Owned);
/// </code>
/// </example>
public class ChangeHistoryCapturePathOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryCapturePathOptions" /> class.
    /// </summary>
    /// <param name="path">The property or graph path.</param>
    /// <param name="kind">The path kind.</param>
    public ChangeHistoryCapturePathOptions(string path, ChangeHistoryCapturePathKind kind)
    {
        this.Path = path;
        this.IncludePath = path;
        this.Kind = kind;
    }

    /// <summary>
    /// Gets the property or graph path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets or sets the EF include path used to load the baseline.
    /// </summary>
    public string IncludePath { get; set; }

    /// <summary>
    /// Gets the path kind.
    /// </summary>
    public ChangeHistoryCapturePathKind Kind { get; }

    /// <summary>
    /// Gets or sets the collection item type for identifiable collection paths.
    /// </summary>
    public Type CollectionItemType { get; set; }

    /// <summary>
    /// Gets or sets the collection item identity accessor.
    /// </summary>
    public Func<object, string> CollectionItemIdentity { get; set; }

    /// <summary>
    /// Gets configured graph collection identity rules by relative collection path.
    /// </summary>
    public IDictionary<string, ChangeHistoryGraphIdentityOptions> GraphIdentities { get; } = new Dictionary<string, ChangeHistoryGraphIdentityOptions>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the graph restore plan name.
    /// </summary>
    public string RestorePlanName { get; set; }

    /// <summary>
    /// Gets or sets the graph restore plan type.
    /// </summary>
    public Type RestorePlanType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether missing graph identity rules should fail validation.
    /// </summary>
    public bool RequireExplicitGraphIdentities { get; set; } = true;
}

/// <summary>
/// Configures identity for one graph collection path.
/// </summary>
/// <example>
/// <code>
/// var identity = new ChangeHistoryGraphIdentityOptions("Orders.Items", item =&gt; item.Id.ToString());
/// </code>
/// </example>
public sealed class ChangeHistoryGraphIdentityOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryGraphIdentityOptions" /> class.
    /// </summary>
    /// <param name="path">The relative graph collection path.</param>
    /// <param name="identity">The identity accessor.</param>
    public ChangeHistoryGraphIdentityOptions(string path, Func<object, string> identity)
    {
        this.Path = path;
        this.Identity = identity;
    }

    /// <summary>
    /// Gets the relative graph collection path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the collection item identity accessor.
    /// </summary>
    public Func<object, string> Identity { get; }
}

/// <summary>
/// Fluent builder for graph capture paths.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .CaptureGraph("Orders", graph =&gt; graph
///         .UseIdentity&lt;Order, Guid&gt;("Orders", o =&gt; o.Id)
///         .UseIdentity&lt;OrderItem, Guid&gt;("Orders.Items", i =&gt; i.Id));
/// </code>
/// </example>
public sealed class ChangeHistoryGraphOptionsBuilder<TEntity>
    where TEntity : class, IEntity
{
    internal ChangeHistoryGraphOptionsBuilder(
        ChangeHistoryEntityOptionsBuilder<TEntity> entityBuilder,
        ChangeHistoryCapturePathOptions pathOptions)
    {
        this.EntityBuilder = entityBuilder;
        this.PathOptions = pathOptions;
    }

    /// <summary>
    /// Gets the entity builder.
    /// </summary>
    public ChangeHistoryEntityOptionsBuilder<TEntity> EntityBuilder { get; }

    /// <summary>
    /// Gets the graph path options.
    /// </summary>
    public ChangeHistoryCapturePathOptions PathOptions { get; }

    /// <summary>
    /// Configures identity for a collection path inside the graph.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <typeparam name="TKey">The identity value type.</typeparam>
    /// <param name="path">The relative collection path.</param>
    /// <param name="identity">The identity expression.</param>
    /// <returns>The current graph builder.</returns>
    public ChangeHistoryGraphOptionsBuilder<TEntity> UseIdentity<TItem, TKey>(
        string path,
        Expression<Func<TItem, TKey>> identity)
    {
        var identityAccessor = identity.Compile();
        this.PathOptions.GraphIdentities[path] = new ChangeHistoryGraphIdentityOptions(
            path,
            item => identityAccessor((TItem)item)?.ToString());

        return this;
    }

    /// <summary>
    /// Configures the restore plan name required to restore this graph safely.
    /// </summary>
    /// <param name="name">The restore plan name.</param>
    /// <returns>The current graph builder.</returns>
    public ChangeHistoryGraphOptionsBuilder<TEntity> UseRestorePlan(string name)
    {
        this.PathOptions.RestorePlanName = name;

        return this;
    }

    /// <summary>
    /// Configures the typed restore plan required to restore this graph safely.
    /// </summary>
    /// <typeparam name="TRestorePlan">The graph restore plan type.</typeparam>
    /// <returns>The current graph builder.</returns>
    public ChangeHistoryGraphOptionsBuilder<TEntity> UseRestorePlan<TRestorePlan>()
        where TRestorePlan : IChangeHistoryGraphRestorePlan<TEntity>
    {
        this.PathOptions.RestorePlanType = typeof(TRestorePlan);
        this.PathOptions.RestorePlanName = typeof(TRestorePlan).Name;

        return this;
    }

    /// <summary>
    /// Returns to the entity builder.
    /// </summary>
    /// <returns>The entity builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> Done() => this.EntityBuilder;
}

/// <summary>
/// Fluent builder for owned and collection capture paths.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .CaptureOwned(c =&gt; c.Address, path =&gt; path.UseRestorePlan&lt;AddressRestorePlan&gt;());
/// </code>
/// </example>
public sealed class ChangeHistoryPathOptionsBuilder<TEntity>
    where TEntity : class, IEntity
{
    internal ChangeHistoryPathOptionsBuilder(
        ChangeHistoryEntityOptionsBuilder<TEntity> entityBuilder,
        ChangeHistoryCapturePathOptions pathOptions)
    {
        this.EntityBuilder = entityBuilder;
        this.PathOptions = pathOptions;
    }

    /// <summary>
    /// Gets the entity builder.
    /// </summary>
    public ChangeHistoryEntityOptionsBuilder<TEntity> EntityBuilder { get; }

    /// <summary>
    /// Gets the path options.
    /// </summary>
    public ChangeHistoryCapturePathOptions PathOptions { get; }

    /// <summary>
    /// Configures the restore plan name required to restore this path safely.
    /// </summary>
    /// <param name="name">The restore plan name.</param>
    /// <returns>The current path builder.</returns>
    public ChangeHistoryPathOptionsBuilder<TEntity> UseRestorePlan(string name)
    {
        this.PathOptions.RestorePlanName = name;

        return this;
    }

    /// <summary>
    /// Configures the typed restore plan required to restore this path safely.
    /// </summary>
    /// <typeparam name="TRestorePlan">The restore plan type.</typeparam>
    /// <returns>The current path builder.</returns>
    public ChangeHistoryPathOptionsBuilder<TEntity> UseRestorePlan<TRestorePlan>()
        where TRestorePlan : IChangeHistoryGraphRestorePlan<TEntity>
    {
        this.PathOptions.RestorePlanType = typeof(TRestorePlan);
        this.PathOptions.RestorePlanName = typeof(TRestorePlan).Name;

        return this;
    }

    /// <summary>
    /// Returns to the entity builder.
    /// </summary>
    /// <returns>The entity builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> Done() => this.EntityBuilder;
}

/// <summary>
/// Configures how one entity property/path can be restored.
/// </summary>
/// <example>
/// <code>
/// var policy = new ChangeHistoryRestorePropertyOptions("FirstName")
/// {
///     ExecutionMode = ChangeHistoryRestoreExecutionMode.ValidatedSetter
/// };
/// </code>
/// </example>
public class ChangeHistoryRestorePropertyOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryRestorePropertyOptions" /> class.
    /// </summary>
    /// <param name="propertyName">The property/path name.</param>
    public ChangeHistoryRestorePropertyOptions(string propertyName)
    {
        this.PropertyName = propertyName;
    }

    /// <summary>
    /// Gets the property/path name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets or sets the restore execution mode.
    /// </summary>
    public ChangeHistoryRestoreExecutionMode ExecutionMode { get; set; } = ChangeHistoryRestoreExecutionMode.DomainLogic;

    /// <summary>
    /// Gets or sets a domain restore delegate.
    /// </summary>
    public Delegate DomainMethod { get; set; }

    /// <summary>
    /// Gets or sets the typed restore handler type.
    /// </summary>
    public Type HandlerType { get; set; }

    /// <summary>
    /// Gets or sets the restore handler name used for diagnostics.
    /// </summary>
    public string HandlerName { get; set; }
}

/// <summary>
/// Fluent builder for entity-specific ChangeHistory capture configuration.
/// </summary>
/// <typeparam name="TEntity">The tracked entity type.</typeparam>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .UseCaptureStrategy(ChangeHistoryCaptureStrategy.EntityChangeOnly)
///     .Exclude(c =&gt; c.PasswordHash);
/// </code>
/// </example>
public sealed class ChangeHistoryEntityOptionsBuilder<TEntity>
    where TEntity : class, IEntity
{
    internal ChangeHistoryEntityOptionsBuilder(ChangeHistoryOptions options, ChangeHistoryEntityOptions entityOptions)
    {
        this.Options = options;
        this.EntityOptions = entityOptions;
    }

    /// <summary>
    /// Gets the root options instance.
    /// </summary>
    public ChangeHistoryOptions Options { get; }

    /// <summary>
    /// Gets the entity-specific options instance.
    /// </summary>
    public ChangeHistoryEntityOptions EntityOptions { get; }

    /// <summary>
    /// Overrides the capture strategy for this entity.
    /// </summary>
    /// <param name="strategy">The strategy to use for this entity.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseCaptureStrategy(ChangeHistoryCaptureStrategy strategy)
    {
        this.EntityOptions.CaptureStrategy = strategy;

        return this;
    }

    /// <summary>
    /// Enables direct-mutation capture for this entity.
    /// </summary>
    /// <param name="strategy">The direct-mutation capture strategy.</param>
    /// <param name="mode">The direct-mutation capture mode.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureDirectMutations(
        ChangeHistoryCaptureStrategy strategy = ChangeHistoryCaptureStrategy.RepositorySnapshot,
        ChangeHistoryCaptureMode mode = ChangeHistoryCaptureMode.BestEffort)
    {
        this.EntityOptions.CaptureDirectMutations = mode != ChangeHistoryCaptureMode.Disabled;
        this.EntityOptions.DirectMutationMode = mode;
        this.EntityOptions.CaptureStrategy = strategy;

        return this;
    }

    /// <summary>
    /// Enables initial-value capture for repository inserts and insert-style upserts.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureCreates()
    {
        this.EntityOptions.CaptureCreates = true;

        return this;
    }

    /// <summary>
    /// Enables ChangeHistory capture for explicitly configured native entity bulk inserts.
    /// </summary>
    /// <param name="mode">The amount of history captured for each native bulk insert.</param>
    /// <param name="maxDetailedEntities">The safety limit used when <paramref name="mode" /> is <see cref="ChangeHistoryBulkInsertCaptureMode.Detailed" />.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// options.Track&lt;Customer&gt;().CaptureBulkInserts();
    /// </code>
    /// </example>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureBulkInserts(
        ChangeHistoryBulkInsertCaptureMode mode = ChangeHistoryBulkInsertCaptureMode.Summary,
        int maxDetailedEntities = 1000)
    {
        this.EntityOptions.BulkInsertCaptureMode = mode;
        this.EntityOptions.BulkInsertMaxDetailedEntities = maxDetailedEntities;

        return this;
    }

    /// <summary>
    /// Enables set-based update capture for this entity.
    /// </summary>
    /// <param name="mode">The capture mode.</param>
    /// <param name="maxAffectedRows">The optional per-entity safety limit.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureUpdateSet(
        ChangeHistoryCaptureMode mode = ChangeHistoryCaptureMode.BestEffort,
        int? maxAffectedRows = null)
    {
        this.EntityOptions.CaptureUpdateSet = mode != ChangeHistoryCaptureMode.Disabled;
        this.EntityOptions.UpdateSetMode = mode;
        this.EntityOptions.UpdateSetMaxAffectedRows = maxAffectedRows;

        return this;
    }

    /// <summary>
    /// Enables the standard ChangeHistory capture sources for this entity.
    /// </summary>
    /// <param name="directMutationMode">The direct-mutation capture mode.</param>
    /// <param name="updateSetMode">The set-based update capture mode.</param>
    /// <param name="updateSetMaxAffectedRows">The optional per-entity set-based update safety limit.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// options.Track&lt;Customer&gt;().CaptureChanges();
    /// </code>
    /// </example>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureChanges(
        ChangeHistoryCaptureMode directMutationMode = ChangeHistoryCaptureMode.Required,
        ChangeHistoryCaptureMode updateSetMode = ChangeHistoryCaptureMode.BestEffort,
        int? updateSetMaxAffectedRows = null)
    {
        this.CaptureCreates();
        this.CaptureDirectMutations(
            this.EntityOptions.CaptureStrategy ?? this.Options.DefaultCaptureStrategy,
            directMutationMode);
        this.CaptureUpdateSet(updateSetMode, updateSetMaxAffectedRows);

        return this;
    }

    /// <summary>
    /// Excludes a property from persisted change-history rows.
    /// </summary>
    /// <typeparam name="TProperty">The property value type.</typeparam>
    /// <param name="property">The property expression.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> Exclude<TProperty>(Expression<Func<TEntity, TProperty>> property)
        => this.SetPolicy(property, ChangeHistoryValuePolicy.Exclude);

    /// <summary>
    /// Stores a row for a property but redacts its old and new values.
    /// </summary>
    /// <typeparam name="TProperty">The property value type.</typeparam>
    /// <param name="property">The property expression.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> Redact<TProperty>(Expression<Func<TEntity, TProperty>> property)
        => this.SetPolicy(property, ChangeHistoryValuePolicy.Redact);

    /// <summary>
    /// Stores a row for a property but persists only hashes for its old and new values.
    /// </summary>
    /// <typeparam name="TProperty">The property value type.</typeparam>
    /// <param name="property">The property expression.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> HashOnly<TProperty>(Expression<Func<TEntity, TProperty>> property)
        => this.SetPolicy(property, ChangeHistoryValuePolicy.HashOnly);

    /// <summary>
    /// Allows restore for the selected property.
    /// </summary>
    /// <typeparam name="TProperty">The property value type.</typeparam>
    /// <param name="property">The property expression.</param>
    /// <returns>A restore policy builder for the selected property.</returns>
    public ChangeHistoryRestorePropertyOptionsBuilder<TEntity, TProperty> AllowRestore<TProperty>(
        Expression<Func<TEntity, TProperty>> property)
    {
        var name = GetPropertyName(property);
        if (!this.EntityOptions.RestorePolicies.TryGetValue(name, out var restoreOptions))
        {
            restoreOptions = new ChangeHistoryRestorePropertyOptions(name);
            this.EntityOptions.RestorePolicies.Add(name, restoreOptions);
        }

        return new ChangeHistoryRestorePropertyOptionsBuilder<TEntity, TProperty>(this, restoreOptions);
    }

    /// <summary>
    /// Allows restore through validated setters for every property in a projection.
    /// </summary>
    /// <typeparam name="TProperties">The projection type.</typeparam>
    /// <param name="properties">A single property or anonymous-object property projection.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// options.Track&lt;Customer&gt;()
    ///     .AllowRestoreUsingValidatedSetters(customer =&gt; new
    ///     {
    ///         customer.FirstName,
    ///         customer.LastName
    ///     });
    /// </code>
    /// </example>
    public ChangeHistoryEntityOptionsBuilder<TEntity> AllowRestoreUsingValidatedSetters<TProperties>(
        Expression<Func<TEntity, TProperties>> properties)
    {
        var propertyNames = GetPropertyNames(properties).ToArray();
        foreach (var name in propertyNames)
        {
            if (!this.EntityOptions.RestorePolicies.TryGetValue(name, out var restoreOptions))
            {
                restoreOptions = new ChangeHistoryRestorePropertyOptions(name);
                this.EntityOptions.RestorePolicies.Add(name, restoreOptions);
            }

            restoreOptions.ExecutionMode = ChangeHistoryRestoreExecutionMode.ValidatedSetter;
            restoreOptions.DomainMethod = null;
            restoreOptions.HandlerType = null;
            restoreOptions.HandlerName = ChangeHistoryRestoreExecutionMode.ValidatedSetter.ToString();
        }

        return this;
    }

    /// <summary>
    /// Captures scalar changes below an owned value-object path.
    /// </summary>
    /// <typeparam name="TProperty">The owned value-object type.</typeparam>
    /// <param name="path">The owned path expression.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureOwned<TProperty>(
        Expression<Func<TEntity, TProperty>> path,
        string includePath = null)
    {
        var propertyPath = GetPropertyPath(path);
        this.EntityOptions.CapturePaths.Add(new ChangeHistoryCapturePathOptions(propertyPath, ChangeHistoryCapturePathKind.Owned)
        {
            IncludePath = includePath ?? propertyPath
        });

        return this;
    }

    /// <summary>
    /// Captures scalar changes below an owned value-object path and configures path restore.
    /// </summary>
    /// <typeparam name="TProperty">The owned value-object type.</typeparam>
    /// <param name="path">The owned path expression.</param>
    /// <param name="configure">The path policy configuration.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureOwned<TProperty>(
        Expression<Func<TEntity, TProperty>> path,
        Action<ChangeHistoryPathOptionsBuilder<TEntity>> configure,
        string includePath = null)
    {
        var propertyPath = GetPropertyPath(path);
        var pathOptions = new ChangeHistoryCapturePathOptions(propertyPath, ChangeHistoryCapturePathKind.Owned)
        {
            IncludePath = includePath ?? propertyPath
        };
        this.EntityOptions.CapturePaths.Add(pathOptions);
        configure?.Invoke(new ChangeHistoryPathOptionsBuilder<TEntity>(this, pathOptions));

        return this;
    }

    /// <summary>
    /// Captures scalar changes for identifiable collection items.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <typeparam name="TKey">The collection item identity type.</typeparam>
    /// <param name="path">The collection path expression.</param>
    /// <param name="identity">The item identity expression.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureCollection<TItem, TKey>(
        Expression<Func<TEntity, IEnumerable<TItem>>> path,
        Expression<Func<TItem, TKey>> identity,
        string includePath = null)
    {
        var propertyPath = GetPropertyPath(path);
        var identityAccessor = identity.Compile();
        this.EntityOptions.CapturePaths.Add(new ChangeHistoryCapturePathOptions(propertyPath, ChangeHistoryCapturePathKind.Collection)
        {
            IncludePath = includePath ?? propertyPath,
            CollectionItemType = typeof(TItem),
            CollectionItemIdentity = item => identityAccessor((TItem)item)?.ToString()
        });

        return this;
    }

    /// <summary>
    /// Captures scalar changes for identifiable collection items using EF Core key metadata to infer item identity.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <param name="path">The collection path expression.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureCollection<TItem>(
        Expression<Func<TEntity, IEnumerable<TItem>>> path,
        string includePath = null)
    {
        var propertyPath = GetPropertyPath(path);
        this.EntityOptions.CapturePaths.Add(new ChangeHistoryCapturePathOptions(propertyPath, ChangeHistoryCapturePathKind.Collection)
        {
            IncludePath = includePath ?? propertyPath,
            CollectionItemType = typeof(TItem)
        });

        return this;
    }

    /// <summary>
    /// Captures scalar changes for identifiable collection items and configures path restore.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <typeparam name="TKey">The collection item identity type.</typeparam>
    /// <param name="path">The collection path expression.</param>
    /// <param name="identity">The item identity expression.</param>
    /// <param name="configure">The path policy configuration.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureCollection<TItem, TKey>(
        Expression<Func<TEntity, IEnumerable<TItem>>> path,
        Expression<Func<TItem, TKey>> identity,
        Action<ChangeHistoryPathOptionsBuilder<TEntity>> configure,
        string includePath = null)
    {
        var propertyPath = GetPropertyPath(path);
        var identityAccessor = identity.Compile();
        var pathOptions = new ChangeHistoryCapturePathOptions(propertyPath, ChangeHistoryCapturePathKind.Collection)
        {
            IncludePath = includePath ?? propertyPath,
            CollectionItemType = typeof(TItem),
            CollectionItemIdentity = item => identityAccessor((TItem)item)?.ToString()
        };
        this.EntityOptions.CapturePaths.Add(pathOptions);
        configure?.Invoke(new ChangeHistoryPathOptionsBuilder<TEntity>(this, pathOptions));

        return this;
    }

    /// <summary>
    /// Captures scalar changes for identifiable collection items using EF Core key metadata and configures path restore.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <param name="path">The collection path expression.</param>
    /// <param name="configure">The path policy configuration.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureCollection<TItem>(
        Expression<Func<TEntity, IEnumerable<TItem>>> path,
        Action<ChangeHistoryPathOptionsBuilder<TEntity>> configure,
        string includePath = null)
    {
        var propertyPath = GetPropertyPath(path);
        var pathOptions = new ChangeHistoryCapturePathOptions(propertyPath, ChangeHistoryCapturePathKind.Collection)
        {
            IncludePath = includePath ?? propertyPath,
            CollectionItemType = typeof(TItem)
        };
        this.EntityOptions.CapturePaths.Add(pathOptions);
        configure?.Invoke(new ChangeHistoryPathOptionsBuilder<TEntity>(this, pathOptions));

        return this;
    }

    /// <summary>
    /// Configures the restore concurrency policy for this entity.
    /// </summary>
    /// <param name="policy">The concurrency policy.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseRestoreConcurrencyPolicy(ChangeHistoryRestoreConcurrencyPolicy policy)
    {
        this.EntityOptions.RestoreConcurrencyPolicy = policy;

        return this;
    }

    /// <summary>
    /// Configures the restore authorizer for this entity. When configured through
    /// <c>AddChangeHistory</c>, the concrete authorizer is registered as a scoped service.
    /// </summary>
    /// <typeparam name="TAuthorizer">The authorizer type.</typeparam>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseRestoreAuthorizer<TAuthorizer>()
        where TAuthorizer : IChangeHistoryRestoreAuthorizer<TEntity>
    {
        this.EntityOptions.RestoreAuthorizerType = typeof(TAuthorizer);

        return this;
    }

    /// <summary>
    /// Adds a graph include path for future graph-aware capture and restore plans.
    /// </summary>
    /// <param name="path">The graph path.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureGraph(string path, string includePath = null)
    {
        this.EntityOptions.CapturePaths.Add(new ChangeHistoryCapturePathOptions(path, ChangeHistoryCapturePathKind.Graph)
        {
            IncludePath = includePath ?? path
        });

        return this;
    }

    /// <summary>
    /// Adds and configures a graph capture path.
    /// </summary>
    /// <param name="path">The graph path.</param>
    /// <param name="configure">The graph policy configuration.</param>
    /// <param name="includePath">The optional EF include path.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> CaptureGraph(
        string path,
        Action<ChangeHistoryGraphOptionsBuilder<TEntity>> configure,
        string includePath = null)
    {
        var graphOptions = new ChangeHistoryCapturePathOptions(path, ChangeHistoryCapturePathKind.Graph)
        {
            IncludePath = includePath ?? path
        };
        this.EntityOptions.CapturePaths.Add(graphOptions);
        configure?.Invoke(new ChangeHistoryGraphOptionsBuilder<TEntity>(this, graphOptions));

        return this;
    }

    private ChangeHistoryEntityOptionsBuilder<TEntity> SetPolicy<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        ChangeHistoryValuePolicy policy)
    {
        var name = GetPropertyName(property);
        this.EntityOptions.PropertyPolicies[name] = policy;

        return this;
    }

    private static string GetPropertyName<TProperty>(Expression<Func<TEntity, TProperty>> property)
        => GetPropertyName(property?.Body, nameof(property));

    private static IEnumerable<string> GetPropertyNames(LambdaExpression properties)
    {
        IEnumerable<Expression> expressions = properties?.Body switch
        {
            NewExpression newExpression when newExpression.Arguments.Count > 0 => newExpression.Arguments,
            MemberExpression memberExpression => [memberExpression],
            UnaryExpression unaryExpression => [unaryExpression],
            _ => throw new ArgumentException(
                "Expression must select a property or create an anonymous-object property projection.",
                nameof(properties))
        };

        foreach (var expression in expressions)
        {
            yield return GetPropertyName(expression, nameof(properties));
        }
    }

    private static string GetPropertyName(Expression expression, string parameterName)
    {
        var memberExpression = expression switch
        {
            MemberExpression member => member,
            UnaryExpression { Operand: MemberExpression member } => member,
            _ => null
        };

        if (memberExpression is not null)
        {
            return memberExpression.Member.Name;
        }

        throw new ArgumentException("Expression must select a property.", parameterName);
    }

    private static string GetPropertyPath(LambdaExpression expression)
    {
        var members = new Stack<string>();
        var current = expression?.Body is UnaryExpression unaryExpression ? unaryExpression.Operand : expression?.Body;
        while (current is MemberExpression memberExpression)
        {
            if (memberExpression.Member is not PropertyInfo)
            {
                break;
            }

            members.Push(memberExpression.Member.Name);
            current = memberExpression.Expression;
        }

        if (members.Count == 0)
        {
            throw new ArgumentException("Expression must select a property path.", nameof(expression));
        }

        return string.Join('.', members);
    }
}

/// <summary>
/// Fluent builder for one restorable property/path.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TProperty">The property value type.</typeparam>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .AllowRestore(c =&gt; c.FirstName)
///     .UseDomainMethod((customer, value) =&gt; customer.ChangeFirstName(value));
/// </code>
/// </example>
public sealed class ChangeHistoryRestorePropertyOptionsBuilder<TEntity, TProperty>
    where TEntity : class, IEntity
{
    internal ChangeHistoryRestorePropertyOptionsBuilder(
        ChangeHistoryEntityOptionsBuilder<TEntity> entityBuilder,
        ChangeHistoryRestorePropertyOptions restoreOptions)
    {
        this.EntityBuilder = entityBuilder;
        this.RestoreOptions = restoreOptions;
    }

    /// <summary>
    /// Gets the entity builder.
    /// </summary>
    public ChangeHistoryEntityOptionsBuilder<TEntity> EntityBuilder { get; }

    /// <summary>
    /// Gets the restore options.
    /// </summary>
    public ChangeHistoryRestorePropertyOptions RestoreOptions { get; }

    /// <summary>
    /// Restores the property through a synchronous domain method.
    /// </summary>
    /// <param name="method">The domain method.</param>
    /// <returns>The entity builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseDomainMethod(Func<TEntity, TProperty, Result> method)
    {
        this.RestoreOptions.ExecutionMode = ChangeHistoryRestoreExecutionMode.DomainLogic;
        this.RestoreOptions.DomainMethod = method;
        this.RestoreOptions.HandlerName = method.Method.Name;

        return this.EntityBuilder;
    }

    /// <summary>
    /// Restores the property through an asynchronous domain method.
    /// </summary>
    /// <param name="method">The asynchronous domain method.</param>
    /// <returns>The entity builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseDomainMethod(Func<TEntity, TProperty, CancellationToken, Task<Result>> method)
    {
        this.RestoreOptions.ExecutionMode = ChangeHistoryRestoreExecutionMode.DomainLogic;
        this.RestoreOptions.DomainMethod = method;
        this.RestoreOptions.HandlerName = method.Method.Name;

        return this.EntityBuilder;
    }

    /// <summary>
    /// Restores the property through a typed domain restore handler.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>The entity builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseDomainHandler<THandler>()
        where THandler : IChangeHistoryRestoreHandler<TEntity>
    {
        this.RestoreOptions.ExecutionMode = ChangeHistoryRestoreExecutionMode.DomainLogic;
        this.RestoreOptions.HandlerType = typeof(THandler);
        this.RestoreOptions.HandlerName = typeof(THandler).Name;

        return this.EntityBuilder;
    }

    /// <summary>
    /// Allows the restore command to set this public property directly after validation.
    /// </summary>
    /// <returns>The entity builder.</returns>
    public ChangeHistoryEntityOptionsBuilder<TEntity> UseValidatedSetter()
    {
        this.RestoreOptions.ExecutionMode = ChangeHistoryRestoreExecutionMode.ValidatedSetter;
        this.RestoreOptions.HandlerName = ChangeHistoryRestoreExecutionMode.ValidatedSetter.ToString();

        return this.EntityBuilder;
    }
}
