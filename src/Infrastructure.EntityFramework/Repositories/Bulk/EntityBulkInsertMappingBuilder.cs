// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Collections;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Analyzes and builds provider-neutral Entity Framework values for one entity bulk insert operation.
/// </summary>
/// <typeparam name="TEntity">The entity type represented by the bulk insert batch.</typeparam>
/// <example>
/// <code>
/// var analysis = builder.Analyze(dbContext, people, options);
/// var batch = builder.Build(analysis);
/// </code>
/// </example>
public sealed class EntityBulkInsertMappingBuilder<TEntity>
    where TEntity : class, IEntity
{
    private readonly IReadOnlyList<IEntityBulkInsertShadowValueProvider<TEntity>> shadowProviders;

    /// <summary>
    /// Initializes a mapping builder with deterministic shadow-property value providers.
    /// </summary>
    /// <param name="shadowProviders">The ordered shadow-property providers.</param>
    /// <example>
    /// <code>
    /// var builder = new EntityBulkInsertMappingBuilder&lt;Person&gt;(shadowProviders);
    /// </code>
    /// </example>
    public EntityBulkInsertMappingBuilder(
        IEnumerable<IEntityBulkInsertShadowValueProvider<TEntity>> shadowProviders = null
    )
    {
        this.shadowProviders = (shadowProviders ?? [])
            .Where(provider => provider is not null)
            .ToArray();
    }

    /// <summary>
    /// Performs side-effect-free model, graph, tracking, shadow, and writable-column analysis.
    /// </summary>
    /// <param name="context">The Entity Framework context owning the model.</param>
    /// <param name="entities">The already materialized entities in insertion order.</param>
    /// <param name="options">The provider-neutral bulk options.</param>
    /// <returns>An immutable analysis that can be finalized after bulk behaviors run.</returns>
    /// <exception cref="InvalidOperationException">Thrown for invalid tracking, duplicate references, required values, or mappings.</exception>
    /// <exception cref="NotSupportedException">Thrown for unsupported graphs or multi-store mappings.</exception>
    /// <example>
    /// <code>
    /// var analysis = builder.Analyze(dbContext, people, options);
    /// </code>
    /// </example>
    public EntityBulkInsertMappingAnalysis<TEntity> Analyze(
        DbContext context,
        IReadOnlyList<TEntity> entities,
        EntityBulkInsertOptions options
    )
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(entities, nameof(entities));
        EnsureArg.IsNotNull(options, nameof(options));
        options.Validate();

        EnsureDistinctReferences(entities);
        EnsureDetached(context, entities);

        var entityType =
            context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' is not part of the DbContext model."
            );
        var tableName =
            entityType.GetTableName()
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' is not mapped to a relational table."
            );
        var schema = entityType.GetSchema();
        var storeObject = StoreObjectIdentifier.Table(tableName, schema);

        EnsureSingleTableHierarchy(entityType);
        this.EnsureSupportedGraph(entityType, entities, storeObject);

        var mappings = new List<EntityBulkInsertPropertyMapping<TEntity>>();
        this.AddPropertyMappings(
            context,
            entityType,
            entityType,
            entities,
            storeObject,
            item => item,
            false,
            options,
            mappings
        );

        if (mappings.Count == 0)
        {
            throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' has no writable columns for bulk insert."
            );
        }

        var duplicateColumns = mappings
            .GroupBy(mapping => mapping.ColumnName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' maps multiple writable properties to the same bulk insert column(s): {string.Join(", ", duplicateColumns)}."
            );
        }

        return new EntityBulkInsertMappingAnalysis<TEntity>(
            context,
            entityType,
            schema,
            tableName,
            entities,
            mappings.AsReadOnly(),
            options
        );
    }

    /// <summary>
    /// Finalizes a successful analysis after behaviors have mutated supported CLR values.
    /// </summary>
    /// <param name="analysis">The side-effect-free mapping analysis.</param>
    /// <returns>The finalized provider-neutral batch.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a required finalized value is missing.</exception>
    /// <example>
    /// <code>
    /// var batch = builder.Build(analysis);
    /// </code>
    /// </example>
    public EntityBulkInsertBatch<TEntity> Build(EntityBulkInsertMappingAnalysis<TEntity> analysis)
    {
        EnsureArg.IsNotNull(analysis, nameof(analysis));
        analysis.Options.Validate();
        EnsureRequiredValues(analysis);
        AssignClientGeneratedValues(analysis.EntityType, analysis.Entities, analysis.Options);

        var columns = analysis
            .Mappings.Select(mapping => new EntityBulkInsertColumn<TEntity>(
                mapping.Property,
                mapping.ColumnName,
                mapping.ProviderClrType,
                mapping.ValueGenerated,
                mapping.Source,
                mapping.IsIdentity,
                mapping.ProviderValueAccessor
            ))
            .ToArray();

        return new EntityBulkInsertBatch<TEntity>(
            analysis.EntityType,
            analysis.Schema,
            analysis.TableName,
            analysis.Entities,
            columns,
            analysis.Options
        );
    }

    /// <summary>
    /// Analyzes and immediately builds a batch for callers without lifecycle behaviors.
    /// </summary>
    /// <param name="context">The Entity Framework context.</param>
    /// <param name="entities">The materialized entities.</param>
    /// <param name="options">The provider-neutral options.</param>
    /// <returns>The finalized provider-neutral batch.</returns>
    /// <example><code>var batch = builder.Build(dbContext, people, options);</code></example>
    public EntityBulkInsertBatch<TEntity> Build(
        DbContext context,
        IReadOnlyList<TEntity> entities,
        EntityBulkInsertOptions options
    ) => this.Build(this.Analyze(context, entities, options));

    private static void AssignClientGeneratedValues(
        IEntityType entityType,
        IReadOnlyCollection<TEntity> entities,
        EntityBulkInsertOptions options
    )
    {
        if (!options.AssignSequentialGuidKeys)
        {
            return;
        }

        foreach (
            var property in entityType
                .GetProperties()
                .Where(property =>
                    property.IsKey()
                    && IsGuidProviderProperty(property)
                    && property.ValueGenerated == ValueGenerated.OnAdd
                    && property.PropertyInfo?.CanWrite == true
                )
        )
        {
            foreach (var entity in entities.Where(entity => IsDefaultGuidKey(property, entity)))
            {
                SetGuidKey(property, entity, GuidGenerator.CreateSequential());
            }
        }
    }

    private static void EnsureDetached(DbContext context, IReadOnlyCollection<TEntity> entities)
    {
        var tracked = context
            .ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        if (entities.Any(entity => tracked.Contains(entity)))
        {
            throw new InvalidOperationException(
                $"Bulk insert for '{typeof(TEntity).Name}' requires detached entities; at least one supplied instance is already tracked by the active DbContext."
            );
        }
    }

    private static void EnsureDistinctReferences(IReadOnlyCollection<TEntity> entities)
    {
        var references = new HashSet<TEntity>(ReferenceEqualityComparer.Instance);
        if (entities.Any(entity => !references.Add(entity)))
        {
            throw new InvalidOperationException(
                $"Bulk insert for '{typeof(TEntity).Name}' contains the same entity instance more than once. Duplicate object references are not supported."
            );
        }
    }

    private static void EnsureRequiredValues(EntityBulkInsertMappingAnalysis<TEntity> analysis)
    {
        foreach (var mapping in analysis.Mappings.Where(mapping => mapping.IsRequired))
        {
            foreach (var entity in analysis.Entities)
            {
                if (mapping.ProviderValueAccessor(entity) is null)
                {
                    throw new InvalidOperationException(
                        $"Required bulk insert property '{mapping.Property.DeclaringType.DisplayName()}.{mapping.Property.Name}' has no value for entity '{typeof(TEntity).Name}'."
                    );
                }
            }
        }
    }

    private static void EnsureSingleTableHierarchy(IEntityType entityType)
    {
        var rootType = entityType.GetRootType();
        var strategy = rootType.GetMappingStrategy();
        if (
            string.Equals(
                strategy,
                RelationalAnnotationNames.TptMappingStrategy,
                StringComparison.Ordinal
            )
            || string.Equals(
                strategy,
                RelationalAnnotationNames.TpcMappingStrategy,
                StringComparison.Ordinal
            )
        )
        {
            throw new NotSupportedException(
                $"Bulk insert for '{typeof(TEntity).Name}' supports TPH inheritance only. Mapping strategy '{strategy}' requires multiple table writes."
            );
        }

        var hierarchy = rootType.GetDerivedTypesInclusive();
        if (
            hierarchy.Any(type =>
                type.GetMappingFragments(StoreObjectType.Table).Any() || !SameTable(rootType, type)
            )
        )
        {
            throw new NotSupportedException(
                $"Bulk insert for '{typeof(TEntity).Name}' does not support entity splitting or other multi-table mappings."
            );
        }
    }

    private void EnsureSupportedGraph(
        IEntityType rootEntityType,
        IReadOnlyList<TEntity> entities,
        StoreObjectIdentifier rootStoreObject
    )
    {
        this.EnsureSupportedNavigations(
            rootEntityType,
            rootEntityType,
            entities,
            rootStoreObject,
            entity => entity,
            rootEntityType.DisplayName()
        );
    }

    private void EnsureSupportedNavigations(
        IEntityType rootEntityType,
        IEntityType currentEntityType,
        IReadOnlyList<TEntity> entities,
        StoreObjectIdentifier rootStoreObject,
        Func<TEntity, object> instanceAccessor,
        string path
    )
    {
        foreach (
            var navigation in currentEntityType
                .GetNavigations()
                .Cast<INavigationBase>()
                .Concat(currentEntityType.GetSkipNavigations())
        )
        {
            if (navigation is INavigation { IsOnDependent: true, ForeignKey.IsOwnership: true })
            {
                continue;
            }

            var navigationPath = $"{path}.{navigation.Name}";
            var values = entities
                .Select(entity => GetNavigationValue(navigation, instanceAccessor(entity)))
                .ToArray();

            if (!navigation.TargetEntityType.IsOwned())
            {
                if (values.Any(HasNavigationValue))
                {
                    throw new NotSupportedException(
                        $"Bulk insert for '{typeof(TEntity).Name}' is root-table-only and cannot persist populated navigation '{navigationPath}'."
                    );
                }

                continue;
            }

            if (navigation.IsCollection)
            {
                if (values.Any(HasCollectionItems))
                {
                    throw new NotSupportedException(
                        $"Bulk insert for '{typeof(TEntity).Name}' cannot persist populated owned collection '{navigationPath}'."
                    );
                }

                continue;
            }

            if (navigation.TargetEntityType.IsMappedToJson())
            {
                throw new NotSupportedException(
                    $"Bulk insert for '{typeof(TEntity).Name}' does not support JSON-owned reference '{navigationPath}'."
                );
            }

            if (!IsMappedToStore(navigation.TargetEntityType, rootStoreObject))
            {
                throw new NotSupportedException(
                    $"Bulk insert for '{typeof(TEntity).Name}' does not support separate-table owned reference '{navigationPath}'."
                );
            }

            this.EnsureSupportedNavigations(
                rootEntityType,
                navigation.TargetEntityType,
                entities,
                rootStoreObject,
                entity => GetNavigationValue(navigation, instanceAccessor(entity)),
                navigationPath
            );
        }
    }

    private void AddPropertyMappings(
        DbContext context,
        IEntityType rootEntityType,
        IEntityType currentEntityType,
        IReadOnlyList<TEntity> entities,
        StoreObjectIdentifier storeObject,
        Func<TEntity, object> instanceAccessor,
        bool owned,
        EntityBulkInsertOptions options,
        ICollection<EntityBulkInsertPropertyMapping<TEntity>> mappings
    )
    {
        var discriminatorName = rootEntityType.GetRootType().GetDiscriminatorPropertyName();
        foreach (var property in currentEntityType.GetProperties())
        {
            var columnName = property.GetColumnName(storeObject);
            if (string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            if (owned && IsOwnershipForeignKey(property))
            {
                continue;
            }

            var isDiscriminator = string.Equals(
                property.Name,
                discriminatorName,
                StringComparison.Ordinal
            );
            var isIdentity = IsIdentity(property);
            if (!ShouldInclude(property, storeObject, options, isIdentity, isDiscriminator))
            {
                continue;
            }

            if (isDiscriminator)
            {
                var constant = ConvertToProvider(property, rootEntityType.GetDiscriminatorValue());
                mappings.Add(
                    CreateMapping(
                        property,
                        columnName,
                        EntityBulkInsertColumnSource.MetadataConstant,
                        isIdentity,
                        false,
                        _ => constant
                    )
                );
                continue;
            }

            if (property.IsShadowProperty())
            {
                this.AddShadowMapping(
                    context,
                    entities,
                    property,
                    columnName,
                    isIdentity,
                    storeObject,
                    mappings
                );
                continue;
            }

            if (property.PropertyInfo is null && property.FieldInfo is null)
            {
                throw new NotSupportedException(
                    $"Bulk insert property '{property.DeclaringType.DisplayName()}.{property.Name}' has no readable CLR member."
                );
            }

            mappings.Add(
                CreateMapping(
                    property,
                    columnName,
                    owned
                        ? EntityBulkInsertColumnSource.OwnedProperty
                        : EntityBulkInsertColumnSource.ClrProperty,
                    isIdentity,
                    IsRequiredValue(property, storeObject),
                    entity =>
                    {
                        var instance = instanceAccessor(entity);
                        return ConvertToProvider(property, GetPropertyValue(property, instance));
                    }
                )
            );
        }

        foreach (
            var navigation in currentEntityType
                .GetNavigations()
                .Where(navigation =>
                    navigation.TargetEntityType.IsOwned()
                    && !navigation.IsCollection
                    && !navigation.TargetEntityType.IsMappedToJson()
                    && IsMappedToStore(navigation.TargetEntityType, storeObject)
                )
        )
        {
            this.AddPropertyMappings(
                context,
                rootEntityType,
                navigation.TargetEntityType,
                entities,
                storeObject,
                entity => GetNavigationValue(navigation, instanceAccessor(entity)),
                true,
                options,
                mappings
            );
        }
    }

    private void AddShadowMapping(
        DbContext context,
        IReadOnlyList<TEntity> entities,
        IProperty property,
        string columnName,
        bool isIdentity,
        StoreObjectIdentifier storeObject,
        ICollection<EntityBulkInsertPropertyMapping<TEntity>> mappings
    )
    {
        var values = new Dictionary<TEntity, object>(ReferenceEqualityComparer.Instance);
        foreach (var entity in entities)
        {
            var supplied = this
                .shadowProviders.Select(provider =>
                {
                    var success = provider.TryGetValue(
                        new EntityBulkInsertShadowPropertyContext<TEntity>(
                            entity,
                            property,
                            context
                        ),
                        out var value
                    );
                    return (success, value);
                })
                .Where(result => result.success)
                .ToArray();

            if (supplied.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple bulk shadow-value providers supplied property '{property.DeclaringType.DisplayName()}.{property.Name}'."
                );
            }

            if (supplied.Length == 1)
            {
                values[entity] = ConvertToProvider(property, supplied[0].value);
            }
        }

        var required = IsRequiredValue(property, storeObject);
        if (
            required
            && entities.Any(entity => !values.TryGetValue(entity, out var value) || value is null)
        )
        {
            throw new InvalidOperationException(
                $"Required shadow property '{property.DeclaringType.DisplayName()}.{property.Name}' needs an EF metadata constant or one registered {nameof(IEntityBulkInsertShadowValueProvider<TEntity>)}."
            );
        }

        if (values.Count == 0)
        {
            return;
        }

        mappings.Add(
            CreateMapping(
                property,
                columnName,
                EntityBulkInsertColumnSource.ShadowProvider,
                isIdentity,
                required,
                entity => values.TryGetValue(entity, out var value) ? value : null
            )
        );
    }

    private static EntityBulkInsertPropertyMapping<TEntity> CreateMapping(
        IProperty property,
        string columnName,
        EntityBulkInsertColumnSource source,
        bool isIdentity,
        bool isRequired,
        Func<TEntity, object> accessor
    ) =>
        new(
            property,
            columnName,
            GetProviderClrType(property),
            property.ValueGenerated,
            source,
            isIdentity,
            isRequired,
            accessor
        );

    private static bool ShouldInclude(
        IProperty property,
        StoreObjectIdentifier storeObject,
        EntityBulkInsertOptions options,
        bool isIdentity,
        bool isDiscriminator
    )
    {
        if (isDiscriminator)
        {
            return true;
        }

        if (IsComputedOrRowVersion(property, storeObject) || HasStoreDefault(property, storeObject))
        {
            return false;
        }

        if (isIdentity)
        {
            return options.KeepGeneratedIdentityValues;
        }

        return property.ValueGenerated switch
        {
            ValueGenerated.Never => true,
            ValueGenerated.OnAdd => IsGuidProviderProperty(property),
            _ => false,
        };
    }

    private static bool IsRequiredValue(IProperty property, StoreObjectIdentifier storeObject) =>
        !property.IsNullable
        && !IsComputedOrRowVersion(property, storeObject)
        && !HasStoreDefault(property, storeObject)
        && !IsIdentity(property);

    private static bool HasStoreDefault(IProperty property, StoreObjectIdentifier storeObject) =>
        property.GetDefaultValueSql(storeObject) is not null
        || property.TryGetDefaultValue(storeObject, out _);

    private static bool IsComputedOrRowVersion(
        IProperty property,
        StoreObjectIdentifier storeObject
    ) =>
        property.GetComputedColumnSql(storeObject) is not null
        || property.ValueGenerated == ValueGenerated.OnAddOrUpdate
        || (property.IsConcurrencyToken && property.ClrType == typeof(byte[]));

    private static bool IsIdentity(IProperty property) =>
        property
            .GetAnnotations()
            .Any(annotation =>
                annotation.Name.EndsWith(":ValueGenerationStrategy", StringComparison.Ordinal)
                && annotation
                    .Value?.ToString()
                    ?.Contains("Identity", StringComparison.OrdinalIgnoreCase) == true
            );

    private static bool IsOwnershipForeignKey(IProperty property) =>
        property.GetContainingForeignKeys().Any(foreignKey => foreignKey.IsOwnership);

    private static bool IsMappedToStore(
        IEntityType entityType,
        StoreObjectIdentifier storeObject
    ) =>
        string.Equals(entityType.GetTableName(), storeObject.Name, StringComparison.Ordinal)
        && string.Equals(entityType.GetSchema(), storeObject.Schema, StringComparison.Ordinal);

    private static bool SameTable(IEntityType first, IEntityType second) =>
        string.Equals(first.GetTableName(), second.GetTableName(), StringComparison.Ordinal)
        && string.Equals(first.GetSchema(), second.GetSchema(), StringComparison.Ordinal);

    private static object GetNavigationValue(INavigationBase navigation, object instance)
    {
        if (instance is null)
        {
            return null;
        }

        return navigation.PropertyInfo?.GetValue(instance)
            ?? navigation.FieldInfo?.GetValue(instance);
    }

    private static bool HasNavigationValue(object value) =>
        value switch
        {
            null => false,
            IEnumerable enumerable when value is not string => enumerable.Cast<object>().Any(),
            _ => true,
        };

    private static bool HasCollectionItems(object value) =>
        value is IEnumerable enumerable && enumerable.Cast<object>().Any();

    private static object GetPropertyValue(IProperty property, object instance)
    {
        if (instance is null)
        {
            return null;
        }

        return property.PropertyInfo?.GetValue(instance) ?? property.FieldInfo?.GetValue(instance);
    }

    private static object ConvertToProvider(IProperty property, object value)
    {
        if (value is null)
        {
            return null;
        }

        var converter = property.GetTypeMapping().Converter;
        if (converter is not null)
        {
            return converter.ConvertToProvider(value);
        }

        var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        return enumType.IsEnum
            ? Convert.ChangeType(value, Enum.GetUnderlyingType(enumType))
            : value;
    }

    private static Type GetProviderClrType(IProperty property)
    {
        var converter = property.GetTypeMapping().Converter;
        var clrType = converter?.ProviderClrType ?? property.ClrType;
        clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return clrType.IsEnum ? Enum.GetUnderlyingType(clrType) : clrType;
    }

    private static bool IsGuidProviderProperty(IProperty property) =>
        property.ClrType == typeof(Guid)
        || property.GetTypeMapping().Converter?.ProviderClrType == typeof(Guid);

    private static bool IsDefaultGuidKey(IProperty property, TEntity entity)
    {
        var value = GetPropertyValue(property, entity);
        return value switch
        {
            null => true,
            Guid guid => guid == Guid.Empty,
            EntityId<Guid> id => id.Value == Guid.Empty,
            _ => false,
        };
    }

    private static void SetGuidKey(IProperty property, TEntity entity, Guid value)
    {
        if (property.ClrType == typeof(Guid))
        {
            property.PropertyInfo?.SetValue(entity, value);
            return;
        }

        if (typeof(EntityId<Guid>).IsAssignableFrom(property.ClrType))
        {
            var createMethod = property.ClrType.GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static,
                [typeof(Guid)]
            );
            if (createMethod is null)
            {
                throw new NotSupportedException(
                    $"Typed ID '{property.ClrType.Name}' must expose a public static Create(Guid) method for bulk insert key generation."
                );
            }

            property.PropertyInfo?.SetValue(entity, createMethod.Invoke(null, [value]));
        }
    }
}
