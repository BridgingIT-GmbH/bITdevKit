// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Builds provider-neutral Entity Framework metadata and values for one entity bulk insert operation.
/// </summary>
/// <typeparam name="TEntity">The entity type represented by the bulk insert batch.</typeparam>
/// <example>
/// <code>
/// var batch = new EntityBulkInsertMappingBuilder&lt;Person&gt;()
///     .Build(dbContext, people, options);
/// </code>
/// </example>
public sealed class EntityBulkInsertMappingBuilder<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityBulkInsertMappingBuilder{TEntity}"/> class.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = new EntityBulkInsertMappingBuilder&lt;Person&gt;();
    /// </code>
    /// </example>
    public EntityBulkInsertMappingBuilder()
    {
    }

    /// <summary>
    /// Creates a provider-neutral bulk insert batch from EF metadata and the supplied entities.
    /// </summary>
    /// <param name="context">The Entity Framework context that owns the entity metadata.</param>
    /// <param name="entities">The entities to prepare in insertion order.</param>
    /// <param name="options">The provider-neutral bulk insert options.</param>
    /// <returns>The prepared table metadata, ordered columns, converted value accessors, and entities.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity is not mapped, has no writable columns, or maps duplicate writable columns.</exception>
    /// <exception cref="NotSupportedException">Thrown when the entity contains unsupported navigations or populated owned collections.</exception>
    /// <example>
    /// <code>
    /// var batch = builder.Build(dbContext, people, new EntityBulkInsertOptions());
    /// </code>
    /// </example>
    public EntityBulkInsertBatch<TEntity> Build(
        DbContext context,
        IReadOnlyList<TEntity> entities,
        EntityBulkInsertOptions options)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(entities, nameof(entities));
        EnsureArg.IsNotNull(options, nameof(options));

        options.Validate();

        var entityType = context.Model.FindEntityType(typeof(TEntity)) ??
            throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' is not part of the DbContext model.");
        var tableName = entityType.GetTableName() ??
            throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' is not mapped to a relational table.");

        this.EnsureSupportedNavigations(entityType, entities);
        this.AssignClientGeneratedValues(entityType, entities, options);

        var mappings = this.CreatePropertyMappings(entityType, entityType, item => item, options)
            .ToList();
        if (mappings.Count == 0)
        {
            throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' has no writable columns for bulk insert.");
        }

        var duplicateColumnNames = mappings
            .GroupBy(mapping => mapping.ColumnName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateColumnNames.Count != 0)
        {
            throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' maps multiple writable properties to the same bulk insert column(s): {string.Join(", ", duplicateColumnNames)}.");
        }

        var columns = mappings
            .Select(mapping => new EntityBulkInsertColumn<TEntity>(
                mapping.Property,
                mapping.ColumnName,
                GetProviderClrType(mapping),
                mapping.Property.ValueGenerated,
                item => GetProviderValue(mapping, item)))
            .ToList();

        return new EntityBulkInsertBatch<TEntity>(
            entityType,
            entityType.GetSchema(),
            tableName,
            entities,
            columns,
            options);
    }

    private void EnsureSupportedNavigations(IEntityType entityType, IReadOnlyCollection<TEntity> items)
    {
        var unsupportedNavigations = entityType.GetNavigations()
            .Concat<INavigationBase>(entityType.GetSkipNavigations())
            .Where(navigation => !navigation.TargetEntityType.IsOwned())
            .Select(navigation => navigation.Name)
            .ToList();
        if (unsupportedNavigations.Count != 0)
        {
            throw new NotSupportedException(
                $"Bulk insert for '{typeof(TEntity).Name}' supports aggregate-root columns and owned values only. Unsupported navigations: {string.Join(", ", unsupportedNavigations)}.");
        }

        var ownedCollectionsWithItems = entityType.GetNavigations()
            .Where(navigation => navigation.TargetEntityType.IsOwned() && navigation.IsCollection)
            .Where(navigation => items.Any(item => HasCollectionItems(navigation.PropertyInfo?.GetValue(item))))
            .Select(navigation => navigation.Name)
            .ToList();
        if (ownedCollectionsWithItems.Count != 0)
        {
            throw new NotSupportedException(
                $"Bulk insert for '{typeof(TEntity).Name}' does not insert owned collection rows. Clear or persist these collections separately: {string.Join(", ", ownedCollectionsWithItems)}.");
        }
    }

    private void AssignClientGeneratedValues(
        IEntityType entityType,
        IReadOnlyCollection<TEntity> items,
        EntityBulkInsertOptions options)
    {
        if (options.AssignConcurrencyVersions)
        {
            foreach (var entity in items.OfType<IConcurrency>())
            {
                entity.ConcurrencyVersion = GuidGenerator.CreateSequential();
            }
        }

        if (!options.AssignSequentialGuidKeys)
        {
            return;
        }

        foreach (var property in entityType.GetProperties().Where(property =>
                     property.IsKey() &&
                     IsGuidProviderProperty(property) &&
                     property.ValueGenerated == ValueGenerated.OnAdd &&
                     property.PropertyInfo?.CanWrite == true))
        {
            foreach (var entity in items)
            {
                if (IsDefaultGuidKey(property, entity))
                {
                    SetGuidKey(property, entity, GuidGenerator.CreateSequential());
                }
            }
        }
    }

    private IEnumerable<PropertyColumnMapping> CreatePropertyMappings(
        IEntityType rootEntityType,
        IEntityType currentEntityType,
        Func<TEntity, object> instanceAccessor,
        EntityBulkInsertOptions options)
    {
        var storeObject = StoreObjectIdentifier.Table(rootEntityType.GetTableName(), rootEntityType.GetSchema());

        foreach (var property in currentEntityType.GetProperties()
                     .Where(property => !property.IsShadowProperty() && property.PropertyInfo is not null)
                     .Where(property => ShouldInclude(property, options)))
        {
            var columnName = property.GetColumnName(storeObject);
            if (string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            yield return new PropertyColumnMapping(
                property,
                columnName,
                property.GetTypeMapping().Converter,
                instanceAccessor);
        }

        var ownedReferenceNavigations = currentEntityType.GetNavigations()
            .Where(navigation => navigation.TargetEntityType.IsOwned())
            .Where(navigation => !navigation.IsCollection)
            .Where(navigation => IsMappedToSameTable(rootEntityType, navigation.TargetEntityType))
            .Where(navigation => navigation.PropertyInfo is not null);

        foreach (var navigation in ownedReferenceNavigations)
        {
            foreach (var mapping in this.CreatePropertyMappings(
                         rootEntityType,
                         navigation.TargetEntityType,
                         item =>
                         {
                             var instance = instanceAccessor(item);
                             return instance is null ? null : navigation.PropertyInfo.GetValue(instance);
                         },
                         options))
            {
                yield return mapping;
            }
        }
    }

    private static bool ShouldInclude(IProperty property, EntityBulkInsertOptions options)
    {
        if (property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
        {
            return false;
        }

        return property.ValueGenerated != ValueGenerated.OnAdd ||
            IsGuidProviderProperty(property) ||
            options.KeepGeneratedIdentityValues;
    }

    private static object GetProviderValue(PropertyColumnMapping mapping, TEntity item)
    {
        var instance = mapping.InstanceAccessor(item);
        var value = instance is null ? null : mapping.Property.PropertyInfo.GetValue(instance);

        return value is null || mapping.Converter is null
            ? value
            : mapping.Converter.ConvertToProvider(value);
    }

    private static bool IsMappedToSameTable(IEntityType rootEntityType, IEntityType targetEntityType)
    {
        return string.Equals(rootEntityType.GetTableName(), targetEntityType.GetTableName(), StringComparison.Ordinal) &&
            string.Equals(rootEntityType.GetSchema(), targetEntityType.GetSchema(), StringComparison.Ordinal);
    }

    private static bool HasCollectionItems(object value)
    {
        return value is System.Collections.IEnumerable enumerable && enumerable.Cast<object>().Any();
    }

    private static bool IsGuidProviderProperty(IProperty property)
    {
        return property.ClrType == typeof(Guid) ||
            property.GetTypeMapping().Converter?.ProviderClrType == typeof(Guid);
    }

    private static bool IsDefaultGuidKey(IProperty property, TEntity entity)
    {
        var value = property.PropertyInfo.GetValue(entity);

        return value switch
        {
            null => true,
            Guid guid => guid == Guid.Empty,
            EntityId<Guid> id => id.Value == Guid.Empty,
            _ => false
        };
    }

    private static void SetGuidKey(IProperty property, TEntity entity, Guid value)
    {
        if (property.ClrType == typeof(Guid))
        {
            property.PropertyInfo.SetValue(entity, value);
            return;
        }

        if (typeof(EntityId<Guid>).IsAssignableFrom(property.ClrType))
        {
            var createMethod = property.ClrType.GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static,
                [typeof(Guid)]);
            if (createMethod is null)
            {
                throw new NotSupportedException(
                    $"Typed ID '{property.ClrType.Name}' must expose a public static Create(Guid) method for bulk insert key generation.");
            }

            property.PropertyInfo.SetValue(entity, createMethod.Invoke(null, [value]));
        }
    }

    private static Type GetProviderClrType(PropertyColumnMapping mapping)
    {
        var clrType = mapping.Converter?.ProviderClrType ?? mapping.Property.ClrType;

        return Nullable.GetUnderlyingType(clrType) ?? clrType;
    }

    private sealed record PropertyColumnMapping(
        IProperty Property,
        string ColumnName,
        ValueConverter Converter,
        Func<TEntity, object> InstanceAccessor);
}
